using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace GoalPlanner;

/// <summary>
/// Walks the game's Defs to work out what a goal needs:
/// research chain, prerequisite buildings, total materials and fuel.
/// </summary>
public class PlanCalculator
{
    // Guards against runaway recipe loops (e.g. A is made from B and B from A).
    private const int MaxCraftSteps = 200;

    private static Dictionary<ThingDef, RecipeDef> craftRecipeCache;

    private readonly GoalPlan plan = new();
    private readonly bool expandCrafting;
    private readonly HashSet<ResearchProjectDef> visitedResearch = new();
    private readonly Dictionary<ThingDef, BuildingRequirement> buildingsByDef = new();

    private PlanCalculator(bool expandCrafting)
    {
        this.expandCrafting = expandCrafting;
    }

    public static GoalPlan Calculate(Goal goal, bool expandCrafting)
    {
        var calc = new PlanCalculator(expandCrafting);
        calc.AddBuildable(goal.target, goal.count);
        calc.AddFuel(goal.target, goal.count);
        if (expandCrafting)
        {
            calc.ExpandCrafting();
        }
        return calc.plan;
    }

    // ---------------------------------------------------------------- building

    private void AddBuildable(ThingDef def, int count)
    {
        if (def.CostList != null)
        {
            foreach (ThingDefCountClass cost in def.CostList)
            {
                plan.AddMaterial(cost.thingDef, cost.count * count);
            }
        }

        int stuffCount = def.CostStuffCount;
        if (def.MadeFromStuff && stuffCount > 0)
        {
            AddStuff(def.stuffCategories, stuffCount * count);
        }

        plan.constructionSkill = Mathf.Max(plan.constructionSkill, def.constructionSkillPrerequisite);

        if (def.researchPrerequisites != null)
        {
            foreach (ResearchProjectDef research in def.researchPrerequisites)
            {
                AddResearch(research);
            }
        }

        if (def.buildingPrerequisites != null)
        {
            foreach (ThingDef building in def.buildingPrerequisites)
            {
                AddPrerequisiteBuilding(building, "GoalPlanner_ReasonBuild".Translate(def.LabelCap));
            }
        }
    }

    private void AddStuff(List<StuffCategoryDef> categories, int count)
    {
        StuffRequirement existing = plan.stuff.Find(s => s.categories.SequenceEqual(categories));
        if (existing != null)
        {
            existing.count += count;
        }
        else
        {
            plan.stuff.Add(new StuffRequirement { categories = categories, count = count });
        }
    }

    private void AddPrerequisiteBuilding(ThingDef def, string reason)
    {
        if (buildingsByDef.TryGetValue(def, out BuildingRequirement existing))
        {
            if (!existing.reasons.Contains(reason))
            {
                existing.reasons.Add(reason);
            }
            return;
        }

        var req = new BuildingRequirement { def = def, built = ColonyHas(def) };
        req.reasons.Add(reason);
        buildingsByDef[def] = req;

        // Its own needs come first so the list reads in build order.
        if (!req.built)
        {
            AddBuildable(def, 1);
        }
        plan.buildings.Add(req);
    }

    private static bool ColonyHas(ThingDef def)
    {
        return Find.Maps.Any(m => m.IsPlayerHome && m.listerBuildings.ColonistsHaveBuilding(def));
    }

    // ---------------------------------------------------------------- research

    private void AddResearch(ResearchProjectDef research)
    {
        if (research == null || !visitedResearch.Add(research))
        {
            return;
        }

        if (research.prerequisites != null)
        {
            foreach (ResearchProjectDef pre in research.prerequisites)
            {
                AddResearch(pre);
            }
        }
        if (research.hiddenPrerequisites != null)
        {
            foreach (ResearchProjectDef pre in research.hiddenPrerequisites)
            {
                AddResearch(pre);
            }
        }

        // Added after its prerequisites, so the list is in research order.
        plan.research.Add(research);

        // Benches only matter for research that still has to be done.
        if (research.IsFinished)
        {
            return;
        }
        string reason = "GoalPlanner_ReasonResearch".Translate(research.LabelCap);
        if (research.requiredResearchBuilding != null)
        {
            AddPrerequisiteBuilding(research.requiredResearchBuilding, reason);
        }
        if (research.requiredResearchFacilities != null)
        {
            foreach (ThingDef facility in research.requiredResearchFacilities)
            {
                AddPrerequisiteBuilding(facility, reason);
            }
        }
    }

    // ---------------------------------------------------------------- fuel

    private void AddFuel(ThingDef def, int count)
    {
        var refuelable = def.GetCompProperties<CompProperties_Refuelable>();
        ThingDef fuelDef = refuelable?.fuelFilter?.AnyAllowedDef;
        if (fuelDef == null)
        {
            return;
        }

        // Same formula the game uses for "fuel to fully refuel" from empty.
        int perBuilding = Mathf.CeilToInt(refuelable.fuelCapacity / refuelable.FuelMultiplierCurrentDifficulty);
        plan.fuel.Add(new FuelRequirement { fuelDef = fuelDef, count = perBuilding * count });
    }

    // ---------------------------------------------------------------- crafting

    /// <summary>
    /// Replaces craftable intermediates (components, advanced components...) with what they're made from,
    /// and adds the workbench and research needed to craft them.
    /// </summary>
    private void ExpandCrafting()
    {
        for (int step = 0; step < MaxCraftSteps; step++)
        {
            ThingDef item = plan.materials.Keys.FirstOrDefault(d => GetCraftRecipe(d) != null);
            if (item == null)
            {
                return;
            }

            int needed = plan.materials[item];
            plan.materials.Remove(item);
            RecipeDef recipe = GetCraftRecipe(item);

            int perCraft = Mathf.Max(1, recipe.products[0].count);
            int crafts = Mathf.CeilToInt(needed / (float)perCraft);

            foreach (IngredientCount ingredient in recipe.ingredients)
            {
                if (ingredient.IsFixedIngredient)
                {
                    ThingDef input = ingredient.FixedIngredient;
                    plan.AddMaterial(input, ingredient.CountRequiredOfFor(input, recipe) * crafts);
                }
                else
                {
                    string label = ingredient.filter.Summary;
                    plan.otherIngredients.TryGetValue(label, out float current);
                    plan.otherIngredients[label] = current + ingredient.GetBaseCount() * crafts;
                }
            }

            ThingDef workbench = PickWorkbench(recipe);
            if (workbench != null)
            {
                AddPrerequisiteBuilding(workbench, "GoalPlanner_ReasonCraft".Translate(item.LabelCap));
            }
            AddResearch(recipe.researchPrerequisite);
            if (recipe.researchPrerequisites != null)
            {
                foreach (ResearchProjectDef research in recipe.researchPrerequisites)
                {
                    AddResearch(research);
                }
            }

            CraftRequirement existing = plan.crafts.Find(c => c.def == item);
            if (existing != null)
            {
                existing.count += needed;
            }
            else
            {
                plan.crafts.Add(new CraftRequirement { def = item, count = needed, recipe = recipe, workbench = workbench });
            }
        }

        Log.Warning("[Goal Planner] Stopped expanding crafted materials after " + MaxCraftSteps + " steps.");
    }

    /// <summary>
    /// The "make X" recipe for an item, or null if it isn't something we should break down.
    /// Raw materials (steel, plasteel...) are never expanded, so smelting recipes don't turn steel into slag.
    /// </summary>
    private static RecipeDef GetCraftRecipe(ThingDef def)
    {
        if (def.IsStuff || def.recipeMaker == null)
        {
            return null;
        }

        craftRecipeCache ??= new Dictionary<ThingDef, RecipeDef>();
        if (!craftRecipeCache.TryGetValue(def, out RecipeDef recipe))
        {
            recipe = DefDatabase<RecipeDef>.AllDefs.FirstOrDefault(r =>
                r.products != null && r.products.Count == 1 && r.products[0].thingDef == def
                && !r.ingredients.NullOrEmpty());
            craftRecipeCache[def] = recipe;
        }
        return recipe;
    }

    /// <summary>Prefers a workbench the colony already has.</summary>
    private static ThingDef PickWorkbench(RecipeDef recipe)
    {
        List<ThingDef> users = recipe.AllRecipeUsers.ToList();
        return users.FirstOrDefault(ColonyHas) ?? users.FirstOrDefault();
    }

    /// <summary>True when there is no recipe that makes this item and it isn't a basic material.</summary>
    public static bool IsUncraftable(ThingDef def)
    {
        if (def.IsStuff || def.recipeMaker != null)
        {
            return false;
        }
        // Called every frame while the tab is open, so collect all recipe products once.
        craftableCache ??= DefDatabase<RecipeDef>.AllDefs
            .Where(r => r.products != null)
            .SelectMany(r => r.products)
            .Select(p => p.thingDef)
            .Where(d => d != null)
            .ToHashSet();
        return !craftableCache.Contains(def);
    }

    private static HashSet<ThingDef> craftableCache;

    /// <summary>How many of an item the player has on their home maps.</summary>
    public static int CountInColony(ThingDef def)
    {
        int total = 0;
        foreach (Map map in Find.Maps)
        {
            if (!map.IsPlayerHome)
            {
                continue;
            }
            if (def.CountAsResource)
            {
                // GetCount logs an error for defs it hasn't seen yet, so look the value up directly.
                map.resourceCounter.AllCountedAmounts.TryGetValue(def, out int count);
                total += count;
            }
            else
            {
                foreach (Thing thing in map.listerThings.ThingsOfDef(def))
                {
                    if (!thing.IsForbidden(Faction.OfPlayer))
                    {
                        total += thing.stackCount;
                    }
                }
            }
        }
        return total;
    }
}
