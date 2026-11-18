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

    /// <summary>Materials for the goal itself (count included).</summary>
    public readonly MaterialGroup goalMaterials = new();

    /// <summary>Materials for prerequisite buildings that aren't built yet.</summary>
    public readonly MaterialGroup buildingMaterials = new();

    /// <summary>
    /// How much of each item has to be on hand, counting crafting ingredients
    /// (e.g. steel for the goal, its prerequisite buildings and the missing components).
    /// Only filled when <see cref="ShowOnHandTotals"/> is true.
    /// </summary>
    public readonly Dictionary<ThingDef, int> onHandTotals = new();

    /// <summary>Fuel needed to fill the goal building completely.</summary>
    public readonly List<FuelRequirement> fuel = new();

    /// <summary>Highest construction skill required among the things to build.</summary>
    public int constructionSkill;

    public bool HasCrafting => goalMaterials.HasCrafting || buildingMaterials.HasCrafting;

    /// <summary>A combined total is only useful when materials come from more than one place.</summary>
    public bool ShowOnHandTotals => HasCrafting || !buildingMaterials.IsEmpty;
}

/// <summary>One section of the materials list: direct costs, their tree and any-stuff costs.</summary>
public class MaterialGroup
{
    /// <summary>Direct material costs.</summary>
    public readonly Dictionary<ThingDef, int> counts = new();

    /// <summary>
    /// Materials in the order they were first added.
    /// Used for display so rows don't jump around when the goal count changes.
    /// </summary>
    public readonly List<ThingDef> order = new();

    /// <summary>
    /// The same materials as a tree: each one shows what's in stock, and when crafting is expanded,
    /// what it takes to craft the missing amount (which can contain further crafted items).
    /// </summary>
    public readonly List<MaterialNode> tree = new();

    /// <summary>"Any stuff" costs, e.g. 50x metallic/woody/stony.</summary>
    public readonly List<StuffRequirement> stuff = new();

    public bool HasCrafting => tree.Exists(n => n.craft != null);

    public bool IsEmpty => counts.Count == 0 && stuff.Count == 0;

    public void Add(ThingDef def, int count)
    {
        if (def == null || count <= 0)
        {
            return;
        }
        if (!counts.TryGetValue(def, out int current))
        {
            order.Add(def);
        }
        counts[def] = current + count;
    }
}

/// <summary>One line in the material tree.</summary>
public class MaterialNode
{
    /// <summary>Null for ingredients that accept several things (e.g. "any meat"); see <see cref="label"/>.</summary>
    public ThingDef def;
    public string label;
    public int need;

    /// <summary>Stock still unclaimed when this line was reached (stock used by earlier lines is already taken).</summary>
    public int stockBefore;

    /// <summary>How much of <see cref="need"/> is covered by stock.</summary>
    public int fromStock;

    public int Shortfall => need - fromStock;

    /// <summary>How the shortfall gets crafted; null if it isn't (enough stock, not craftable, or expansion is off).</summary>
    public CraftStep craft;
}

public class CraftStep
{
    public RecipeDef recipe;
    public ThingDef workbench;
    public int amount;
    public int times;
    public readonly List<MaterialNode> ingredients = new();
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

public class FuelRequirement
{
    public ThingDef fuelDef;
    public int count;
}
