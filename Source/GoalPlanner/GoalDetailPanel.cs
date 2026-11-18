using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace GoalPlanner;

/// <summary>Right-hand panel of the Goals tab: draws the plan for the selected goal.</summary>
public class GoalDetailPanel
{
    private const float RowHeight = 28f;
    private const float IconSize = 24f;
    private const float StatusWidth = 170f;
    // Have/built/research status changes while the game runs, so recalculate now and then.
    private const long RefreshIntervalMs = 1000;

    private static readonly Color GoodColor = new(0.45f, 0.85f, 0.45f);
    private static readonly Color BadColor = new(0.95f, 0.45f, 0.4f);
    private static readonly Color WarnColor = new(1f, 0.8f, 0.35f);

    private Vector2 scrollPosition;
    private float contentHeight;

    private GoalPlan cachedPlan;
    private Goal cachedGoal;
    private int cachedCount;
    private bool cachedExpand;
    private long cachedAt;

    public void Draw(Rect rect, Goal goal, GameComponent_GoalPlanner comp)
    {
        Widgets.DrawMenuSection(rect);
        rect = rect.ContractedBy(10f);

        GoalPlan plan = GetPlan(goal, comp.expandCrafting);

        // Header: icon, name x count, info card
        Rect iconRect = new(rect.x, rect.y, 48f, 48f);
        Widgets.DefIcon(iconRect, goal.target);
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(iconRect.xMax + 10f, rect.y, rect.width - 100f, 48f), $"{goal.target.LabelCap} ×{goal.count}");
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        Widgets.InfoCardButton(new Rect(rect.xMax - 24f, rect.y + 12f, 24f, 24f), goal.target);

        float y = rect.y + 54f;
        if (plan.constructionSkill > 0)
        {
            Widgets.Label(new Rect(rect.x, y, rect.width, 24f), "GoalPlanner_ConstructionSkill".Translate(plan.constructionSkill));
            y += 26f;
        }

        Rect toggleRect = new(rect.x, y, Mathf.Min(rect.width, 420f), 24f);
        bool expand = comp.expandCrafting;
        Widgets.CheckboxLabeled(toggleRect, "GoalPlanner_ExpandCrafting".Translate(), ref expand);
        TooltipHandler.TipRegion(toggleRect, "GoalPlanner_ExpandCraftingDesc".Translate());
        comp.expandCrafting = expand;
        y += 30f;

        Rect outRect = new(rect.x, y, rect.width, rect.yMax - y);
        Rect viewRect = new(0f, 0f, outRect.width - 16f, Mathf.Max(contentHeight, outRect.height));
        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        float curY = 0f;
        DrawMaterials(plan.goalMaterials, "GoalPlanner_Materials".Translate(), null, viewRect.width, ref curY);
        if (!plan.buildingMaterials.IsEmpty)
        {
            DrawMaterials(plan.buildingMaterials, "GoalPlanner_BuildingMaterials".Translate(),
                "GoalPlanner_BuildingStockDesc".Translate(), viewRect.width, ref curY);
        }
        DrawOnHandTotals(plan, viewRect.width, ref curY);
        DrawFuel(plan, viewRect.width, ref curY);
        DrawResearch(plan, viewRect.width, ref curY);
        DrawBuildings(plan, viewRect.width, ref curY);
        contentHeight = curY;
        Widgets.EndScrollView();
    }

    private GoalPlan GetPlan(Goal goal, bool expand)
    {
        long now = Environment.TickCount64;
        bool stale = cachedPlan == null || cachedGoal != goal || cachedCount != goal.count
                     || cachedExpand != expand || now - cachedAt > RefreshIntervalMs;
        if (stale)
        {
            cachedPlan = PlanCalculator.Calculate(goal, expand);
            cachedGoal = goal;
            cachedCount = goal.count;
            cachedExpand = expand;
            cachedAt = now;
        }
        return cachedPlan;
    }

    // ---------------------------------------------------------------- sections

    /// <param name="stockTip">Tooltip for top-level rows whose "have" is what's left after earlier sections.</param>
    private static void DrawMaterials(MaterialGroup group, string header, string stockTip, float width, ref float y)
    {
        Widgets.ListSeparator(ref y, width, header);

        if (group.tree.Count == 0 && group.stuff.Count == 0)
        {
            DrawNone(width, ref y);
            return;
        }

        int index = 0;
        foreach (MaterialNode node in group.tree)
        {
            DrawMaterialNode(node, 0, stockTip, width, ref y, ref index);
        }

        foreach (StuffRequirement stuff in group.stuff)
        {
            Rect row = NextRow(width, ref y, index++);
            string categories = stuff.categories.Select(c => c.label).ToCommaListOr();
            DrawIconLabel(row, null, "GoalPlanner_AnyStuff".Translate(categories));
            DrawStatus(row, "×" + stuff.count, Color.white, TextAnchor.MiddleRight);
        }
    }

    /// <summary>A material line, followed (indented) by how its shortfall gets crafted.</summary>
    private static void DrawMaterialNode(MaterialNode node, int depth, string stockTip, float width, ref float y, ref int index)
    {
        Rect row = NextRow(width, ref y, index++);
        Rect indented = Indent(row, depth);

        DrawIconLabel(indented, node.def, node.label);
        if (node.def == null)
        {
            // "Any meat" style ingredient: no single item to count.
            DrawStatus(row, "×" + node.need, Color.white, TextAnchor.MiddleRight);
            return;
        }

        DrawHaveNeed(row, node.stockBefore, node.need);
        if (depth > 0)
        {
            TooltipHandler.TipRegion(row, "GoalPlanner_StockLeftDesc".Translate());
        }
        else if (stockTip != null)
        {
            TooltipHandler.TipRegion(row, stockTip);
        }

        // Shown even when in stock: it is a reminder that more can only come from traders, quests or salvage.
        if (PlanCalculator.IsUncraftable(node.def))
        {
            Rect warnRect = new(indented.x + 260f, row.y, row.xMax - StatusWidth - indented.x - 260f, row.height);
            DrawStatus(warnRect, "GoalPlanner_Uncraftable".Translate(), WarnColor, TextAnchor.MiddleLeft);
            TooltipHandler.TipRegion(row, "GoalPlanner_UncraftableDesc".Translate());
        }

        if (node.craft == null)
        {
            return;
        }

        // "10 short, can be crafted at fabrication bench"
        CraftStep craft = node.craft;
        Rect craftRow = NextRow(width, ref y, index++);
        Rect craftIndented = Indent(craftRow, depth + 1);
        string text = craft.workbench != null
            ? "GoalPlanner_CraftShortfall".Translate(craft.amount, craft.workbench.LabelCap)
            : "GoalPlanner_Shortfall".Translate(craft.amount);
        GUI.color = WarnColor;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(craftIndented, text);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        foreach (MaterialNode ingredient in craft.ingredients)
        {
            DrawMaterialNode(ingredient, depth + 1, stockTip, width, ref y, ref index);
        }
    }

    /// <summary>Total to keep in stock across the goal, prerequisite buildings and crafting.</summary>
    private static void DrawOnHandTotals(GoalPlan plan, float width, ref float y)
    {
        if (!plan.ShowOnHandTotals)
        {
            return;
        }

        Widgets.ListSeparator(ref y, width, "GoalPlanner_OnHandTotals".Translate());
        int index = 0;
        foreach (KeyValuePair<ThingDef, int> entry in plan.onHandTotals.Where(e => e.Value > 0))
        {
            Rect row = NextRow(width, ref y, index++);
            DrawIconLabel(row, entry.Key, entry.Key.LabelCap);
            DrawHaveNeed(row, PlanCalculator.CountInColony(entry.Key), entry.Value);
        }
    }

    private static Rect Indent(Rect row, int depth)
    {
        float offset = depth * 24f;
        return new Rect(row.x + offset, row.y, row.width - offset, row.height);
    }

    private static void DrawFuel(GoalPlan plan, float width, ref float y)
    {
        if (plan.fuel.Count == 0)
        {
            return;
        }

        Widgets.ListSeparator(ref y, width, "GoalPlanner_Fuel".Translate());
        int index = 0;
        foreach (FuelRequirement fuel in plan.fuel)
        {
            Rect row = NextRow(width, ref y, index++);
            DrawIconLabel(row, fuel.fuelDef, fuel.fuelDef.LabelCap);
            DrawHaveNeed(row, PlanCalculator.CountInColony(fuel.fuelDef), fuel.count);
        }
    }

    private static void DrawResearch(GoalPlan plan, float width, ref float y)
    {
        List<ResearchProjectDef> remaining = plan.research.Where(r => !r.IsFinished).ToList();
        string header = remaining.Count == 0
            ? "GoalPlanner_ResearchAllDone".Translate()
            : "GoalPlanner_Research".Translate(remaining.Count);
        Widgets.ListSeparator(ref y, width, header);

        if (plan.research.Count == 0)
        {
            DrawNone(width, ref y);
            return;
        }

        int index = 0;
        foreach (ResearchProjectDef research in plan.research)
        {
            Rect row = NextRow(width, ref y, index++);
            bool done = research.IsFinished;
            GUI.color = done ? Color.gray : Color.white;
            DrawIconLabel(row, null, research.LabelCap);
            GUI.color = Color.white;

            if (done)
            {
                DrawStatus(row, "GoalPlanner_Done".Translate(), GoodColor, TextAnchor.MiddleRight);
            }
            else
            {
                float left = research.CostApparent * (1f - research.ProgressPercent);
                DrawStatus(row, "GoalPlanner_ResearchLeft".Translate(Mathf.CeilToInt(left)), BadColor, TextAnchor.MiddleRight);
            }
            TooltipHandler.TipRegion(row, research.description);
        }
    }

    private static void DrawBuildings(GoalPlan plan, float width, ref float y)
    {
        Widgets.ListSeparator(ref y, width, "GoalPlanner_Buildings".Translate());

        if (plan.buildings.Count == 0)
        {
            DrawNone(width, ref y);
            return;
        }

        int index = 0;
        foreach (BuildingRequirement building in plan.buildings)
        {
            Rect row = NextRow(width, ref y, index++);
            DrawIconLabel(row, building.def, building.def.LabelCap);

            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect reasonRect = new(row.x + 220f, row.y, row.width - 220f - StatusWidth, row.height);
            Widgets.Label(reasonRect, string.Join(", ", building.reasons).Truncate(reasonRect.width));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            if (building.built)
            {
                DrawStatus(row, "GoalPlanner_Built".Translate(), GoodColor, TextAnchor.MiddleRight);
            }
            else
            {
                DrawStatus(row, "GoalPlanner_NotBuilt".Translate(), BadColor, TextAnchor.MiddleRight);
            }
            TooltipHandler.TipRegion(row, string.Join("\n", building.reasons) + "\n\n" + CostSummary(building.def));
        }

        GUI.color = Color.gray;
        Text.Font = GameFont.Tiny;
        Widgets.Label(new Rect(0f, y, width, 20f), "GoalPlanner_BuildingsNote".Translate());
        Text.Font = GameFont.Small;
        GUI.color = Color.white;
        y += 22f;
    }

    // ---------------------------------------------------------------- helpers

    private static Rect NextRow(float width, ref float y, int index)
    {
        Rect row = new(0f, y, width, RowHeight);
        if (index % 2 == 1)
        {
            Widgets.DrawLightHighlight(row);
        }
        Widgets.DrawHighlightIfMouseover(row);
        y += RowHeight;
        return row;
    }

    private static void DrawNone(float width, ref float y)
    {
        GUI.color = Color.gray;
        Widgets.Label(new Rect(4f, y, width, 24f), "GoalPlanner_None".Translate());
        GUI.color = Color.white;
        y += 26f;
    }

    private static void DrawIconLabel(Rect row, ThingDef def, string label)
    {
        float x = row.x + 4f;
        if (def != null)
        {
            Widgets.DefIcon(new Rect(x, row.y + (row.height - IconSize) / 2f, IconSize, IconSize), def);
        }
        x += IconSize + 8f;

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(x, row.y, row.xMax - x - StatusWidth, row.height), label);
        Text.Anchor = TextAnchor.UpperLeft;
    }

    /// <summary>"have / need", green when there's enough.</summary>
    private static void DrawHaveNeed(Rect row, int have, int need)
    {
        Color color = have >= need ? GoodColor : BadColor;
        DrawStatus(row, "GoalPlanner_HaveNeed".Translate(have, need), color, TextAnchor.MiddleRight);
    }

    private static void DrawStatus(Rect row, string text, Color color, TextAnchor anchor)
    {
        Rect rect = anchor == TextAnchor.MiddleRight
            ? new Rect(row.xMax - StatusWidth - 4f, row.y, StatusWidth, row.height)
            : row;
        GUI.color = color;
        Text.Anchor = anchor;
        Widgets.Label(rect, text);
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;
    }

    private static string CostSummary(ThingDef def)
    {
        var parts = new List<string>();
        if (def.MadeFromStuff && def.CostStuffCount > 0)
        {
            parts.Add($"{def.CostStuffCount}x {def.stuffCategories.Select(c => c.label).ToCommaListOr()}");
        }
        if (def.CostList != null)
        {
            parts.AddRange(def.CostList.Select(c => $"{c.count}x {c.thingDef.label}"));
        }
        return "GoalPlanner_Cost".Translate(parts.Count > 0 ? string.Join(", ", parts) : "-");
    }
}
