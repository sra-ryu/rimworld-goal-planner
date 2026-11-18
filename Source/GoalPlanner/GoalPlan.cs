using System.Collections.Generic;
using RimWorld;
using Verse;

namespace GoalPlanner;

/// <summary>Everything a goal needs, as computed by <see cref="PlanCalculator"/>.</summary>
public class GoalPlan
{
    /// <summary>Research projects in the order they can be researched (prerequisites first).</summary>
    public readonly List<ResearchProjectDef> research = new();

    /// <summary>Buildings needed along the way (research benches, facilities, workbenches...).</summary>
    public readonly List<BuildingRequirement> buildings = new();

    /// <summary>Total materials, including the cost of prerequisite buildings that aren't built yet.</summary>
    public readonly Dictionary<ThingDef, int> materials = new();

    /// <summary>"Any stuff" costs, e.g. 50x metallic/woody/stony.</summary>
    public readonly List<StuffRequirement> stuff = new();

    /// <summary>Ingredients that accept several things, e.g. "any meat". Only appears when crafting is expanded.</summary>
    public readonly Dictionary<string, float> otherIngredients = new();

    /// <summary>Intermediate items that have to be crafted, with how many (only when crafting is expanded).</summary>
    public readonly List<CraftRequirement> crafts = new();

    /// <summary>Fuel needed to fill the goal building completely.</summary>
    public readonly List<FuelRequirement> fuel = new();

    /// <summary>Highest construction skill required among the things to build.</summary>
    public int constructionSkill;

    public void AddMaterial(ThingDef def, int count)
    {
        if (def == null || count <= 0)
        {
            return;
        }
        materials.TryGetValue(def, out int current);
        materials[def] = current + count;
    }
}

public class BuildingRequirement
{
    public ThingDef def;
    public bool built;
    public readonly List<string> reasons = new();
}

public class StuffRequirement
{
    public List<StuffCategoryDef> categories;
    public int count;
}

public class CraftRequirement
{
    public ThingDef def;
    public int count;
    public RecipeDef recipe;
    public ThingDef workbench;
}

public class FuelRequirement
{
    public ThingDef fuelDef;
    public int count;
}
