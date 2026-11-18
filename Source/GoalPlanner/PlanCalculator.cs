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
    // Crafting can add workbenches whose own cost changes the tree, so it is rebuilt until it settles.
    private const int MaxRebuilds = 10;
    // Guards against recipe loops (e.g. A is made from B and B from A).
    private const int MaxCraftDepth = 8;

    private static Dictionary<ThingDef, RecipeDef> craftRecipeCache;

    private readonly GoalPlan plan = new();
    private readonly bool expandCrafting;
    private readonly HashSet<ResearchProjectDef> visitedResearch = new();
    private readonly Dictionary<ThingDef, BuildingRequirement> buildingsByDef = new();
    private readonly Dictionary<ThingDef, int> unclaimedStock = new();

    private PlanCalculator(bool expandCrafting)
    {
        this.expandCrafting = expandCrafting;
    }

    public static GoalPlan Calculate(Goal goal, bool expandCrafting)
    {
        var calc = new PlanCalculator(expandCrafting);
        calc.AddBuildable(goal.target, goal.count);
        calc.AddFuel(goal.target, goal.count);

        for (int i = 0; i < MaxRebuilds; i++)
        {
            int buildingsBefore = calc.plan.buildings.Count;
            calc.BuildMaterialTree();
            if (calc.plan.buildings.Count == buildingsBefore)
            {
                break;
            }
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
    /// Builds <see cref="GoalPlan.materialTree"/>. Stock is handed out first to the direct materials,
    /// then to crafting ingredients level by level, so the same steel is never counted twice.
    /// </summary>
    private void BuildMaterialTree()
    {
        plan.materialTree.Clear();
        plan.onHandTotals.Clear();
        unclaimedStock.Clear();

        var level = new List<MaterialNode>();
        foreach (ThingDef def in plan.materialOrder)
        {
            MaterialNode node = MakeNode(def, plan.materials[def]);
            plan.materialTree.Add(node);
            level.Add(node);
        }

        for (int depth = 0; depth < MaxCraftDepth && level.Count > 0; depth++)
        {
            var next = new List<MaterialNode>();
            foreach (MaterialNode node in level)
            {
                if (expandCrafting && TryCraft(node))
                {
                    next.AddRange(node.craft.ingredients);
                }
            }
            level = next;
        }

        if (plan.HasCrafting)
        {
            CollectOnHandTotals(plan.materialTree);
        }
    }

    private MaterialNode MakeNode(ThingDef def, int need)
    {
        if (!unclaimedStock.TryGetValue(def, out int stock))
        {
            stock = CountInColony(def);
        }
        int take = Mathf.Min(stock, need);
        unclaimedStock[def] = stock - take;
        return new MaterialNode { def = def, label = def.LabelCap, need = need, stockBefore = stock, fromStock = take };
    }

    /// <summary>Adds a craft step for the node's shortfall. Returns false if nothing needs crafting.</summary>
    private bool TryCraft(MaterialNode node)
    {
        if (node.def == null || node.Shortfall <= 0)
        {
            return false;
        }
        RecipeDef recipe = GetCraftRecipe(node.def);
        if (recipe == null)
        {
            return false;
        }

        int perCraft = Mathf.Max(1, recipe.products[0].count);
        var step = new CraftStep
        {
            recipe = recipe,
            amount = node.Shortfall,
            times = Mathf.CeilToInt(node.Shortfall / (float)perCraft),
            workbench = PickWorkbench(recipe),
        };

        foreach (IngredientCount ingredient in recipe.ingredients)
        {
            if (ingredient.IsFixedIngredient)
            {
                ThingDef input = ingredient.FixedIngredient;
                step.ingredients.Add(MakeNode(input, ingredient.CountRequiredOfFor(input, recipe) * step.times));
            }
            else
            {
                int count = Mathf.CeilToInt(ingredient.GetBaseCount() * step.times);
                step.ingredients.Add(new MaterialNode { label = ingredient.filter.Summary.CapitalizeFirst(), need = count });
            }
        }
        node.craft = step;

        if (step.workbench != null)
        {
            AddPrerequisiteBuilding(step.workbench, "GoalPlanner_ReasonCraft".Translate(node.def.LabelCap));
        }
        AddResearch(recipe.researchPrerequisite);
        if (recipe.researchPrerequisites != null)
        {
            foreach (ResearchProjectDef research in recipe.researchPrerequisites)
            {
                AddResearch(research);
            }
        }
        return true;
    }

    /// <summary>Everything that has to come from stock: all needs minus what gets crafted.</summary>
    private void CollectOnHandTotals(List<MaterialNode> nodes)
    {
        foreach (MaterialNode node in nodes)
        {
            if (node.def != null)
            {
                int fromStockOrMissing = node.craft == null ? node.need : node.need - node.craft.amount;
                plan.onHandTotals.TryGetValue(node.def, out int current);
                plan.onHandTotals[node.def] = current + fromStockOrMissing;
            }
            if (node.craft != null)
            {
                CollectOnHandTotals(node.craft.ingredients);
            }
        }
    }

    /// <summary>
    /// The "make X" recipe for an item, or null if it isn't something we should break down.
    /// Components count as stuff and use hand-written recipes (Make_ComponentIndustrial), so neither
    /// IsStuff nor recipeMaker tells us anything here. Instead we look for a recipe whose only product
    /// is the item, and skip raw resources so "extract metal from slag" doesn't turn steel into slag.
    /// </summary>
    private static RecipeDef GetCraftRecipe(ThingDef def)
    {
        craftRecipeCache ??= new Dictionary<ThingDef, RecipeDef>();
        if (craftRecipeCache.TryGetValue(def, out RecipeDef recipe))
        {
            return recipe;
        }

        bool isRawResource = def.thingCategories != null && def.thingCategories.Contains(ThingCategoryDefOf.ResourcesRaw);
        if (!isRawResource)
        {
            // Prefer recipes with fixed ingredients (e.g. chemfuel from wood over "from any organics").
            recipe = DefDatabase<RecipeDef>.AllDefs
                .Where(r => r.products != null && r.products.Count == 1 && r.products[0].thingDef == def
                            && !r.ingredients.NullOrEmpty())
                .OrderByDescending(r => r.ingredients.All(i => i.IsFixedIngredient))
                .FirstOrDefault();
        }
        craftRecipeCache[def] = recipe;
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
