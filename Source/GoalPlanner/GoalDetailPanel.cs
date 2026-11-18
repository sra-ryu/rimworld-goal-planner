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
        DrawMaterials(plan, viewRect.width, ref curY);
        DrawFuel(plan, viewRect.width, ref curY);
        DrawResearch(plan, viewRect.width, ref curY);
        DrawBuildings(plan, viewRect.width, ref curY);
        DrawCrafts(plan, viewRect.width, ref curY);
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

    private static void DrawMaterials(GoalPlan plan, float width, ref float y)
    {
        Widgets.ListSeparator(ref y, width, "GoalPlanner_Materials".Translate());

        if (plan.materials.Count == 0 && plan.stuff.Count == 0 && plan.otherIngredients.Count == 0)
        {
            DrawNone(width, ref y);
            return;
        }

        int index = 0;
        foreach (KeyValuePair<ThingDef, int> entry in plan.materials.OrderByDescending(e => e.Value))
        {
            ThingDef def = entry.Key;
            Rect row = NextRow(width, ref y, index++);
            DrawIconLabel(row, def, def.LabelCap);
            DrawHaveNeed(row, PlanCalculator.CountInColony(def), entry.Value);

            if (PlanCalculator.IsUncraftable(def))
            {
                Rect warnRect = new(row.x + 300f, row.y, row.width - 300f - StatusWidth, row.height);
                DrawStatus(warnRect, "GoalPlanner_Uncraftable".Translate(), WarnColor, TextAnchor.MiddleLeft);
                TooltipHandler.TipRegion(row, "GoalPlanner_UncraftableDesc".Translate());
            }
        }

        foreach (StuffRequirement stuff in plan.stuff)
        {
            Rect row = NextRow(width, ref y, index++);
            string categories = stuff.categories.Select(c => c.label).ToCommaListOr();
            DrawIconLabel(row, null, "GoalPlanner_AnyStuff".Translate(categories));
            DrawStatus(row, "×" + stuff.count, Color.white, TextAnchor.MiddleRight);
        }

        foreach (KeyValuePair<string, float> other in plan.otherIngredients)
        {
            Rect row = NextRow(width, ref y, index++);
            DrawIconLabel(row, null, other.Key.CapitalizeFirst());
            DrawStatus(row, "×" + other.Value.ToStringDecimalIfSmall(), Color.white, TextAnchor.MiddleRight);
        }
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

    private static void DrawCrafts(GoalPlan plan, float width, ref float y)
    {
        if (plan.crafts.Count == 0)
        {
            return;
        }

        Widgets.ListSeparator(ref y, width, "GoalPlanner_Crafts".Translate());
        int index = 0;
        foreach (CraftRequirement craft in plan.crafts)
        {
            Rect row = NextRow(width, ref y, index++);
            DrawIconLabel(row, craft.def, $"{craft.def.LabelCap} ×{craft.count}");
            if (craft.workbench != null)
            {
                DrawStatus(row, craft.workbench.LabelCap, Color.gray, TextAnchor.MiddleRight);
            }
        }
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
        Widgets.Label(new Rect(x, row.y, row.width - x - StatusWidth, row.height), label);
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
