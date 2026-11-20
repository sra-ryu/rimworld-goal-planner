using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace GoalPlanner;

/// <summary>Searchable list of everything the player can build, grouped by Architect category. Clicking a row adds it as a goal.</summary>
public class Dialog_AddGoal : Window
{
    private const float RowHeight = 32f;
    private const float CategoryHeight = 32f;
    private const float IconSize = 28f;

    private readonly List<ThingDef> candidates;
    private readonly Action<Goal> onAdded;
    private readonly QuickSearchWidget search = new();
    private Vector2 scrollPosition;

    /// <summary>Categories the player has opened. Everything starts collapsed.</summary>
    private readonly HashSet<DesignationCategoryDef> expanded = new();

    public override Vector2 InitialSize => new(520f, 640f);

    /// <summary>One line in the list: a category header (def == null) or a buildable.</summary>
    private readonly struct Entry
    {
        public readonly DesignationCategoryDef category;
        public readonly ThingDef def;
        public readonly int count; // header only: how many buildables the category has

        public Entry(DesignationCategoryDef category, ThingDef def, int count = 0)
        {
            this.category = category;
            this.def = def;
            this.count = count;
        }
    }

    public Dialog_AddGoal(Action<Goal> onAdded = null)
    {
        this.onAdded = onAdded;
        doCloseX = true;
        closeOnClickedOutside = true;
        absorbInputAroundWindow = true;

        candidates = DefDatabase<ThingDef>.AllDefs
            .Where(d => d.BuildableByPlayer)
            .OrderBy(d => d.designationCategory.order)
            .ThenBy(d => d.label)
            .ToList();
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), "GoalPlanner_ChooseGoal".Translate());
        Text.Font = GameFont.Small;

        search.OnGUI(new Rect(inRect.x, inRect.y + 40f, inRect.width, QuickSearchWidget.WidgetHeight));

        List<ThingDef> shown = candidates.Where(d => search.filter.Matches(d)).ToList();
        Rect outRect = new(inRect.x, inRect.y + 72f, inRect.width, inRect.height - 72f);

        if (shown.Count == 0)
        {
            Widgets.Label(outRect, "GoalPlanner_NoResults".Translate());
            return;
        }

        // While searching, every category with a match is opened so results are never hidden.
        bool searching = search.filter.Active;
        List<Entry> entries = BuildEntries(shown, searching);
        float totalHeight = entries.Sum(e => e.def != null ? RowHeight : CategoryHeight);
        Rect viewRect = new(0f, 0f, outRect.width - 16f, totalHeight);

        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        float y = 0f;
        int rowIndex = 0;
        foreach (Entry entry in entries)
        {
            float height = entry.def != null ? RowHeight : CategoryHeight;
            Rect row = new(0f, y, viewRect.width, height);
            y += height;

            // Only draw rows that are on screen; the list can hold hundreds of defs.
            bool visible = row.yMax >= scrollPosition.y && row.y <= scrollPosition.y + outRect.height;

            if (entry.def == null)
            {
                if (visible && DrawCategoryHeader(row, entry, searching || expanded.Contains(entry.category)) && !searching)
                {
                    if (!expanded.Remove(entry.category))
                    {
                        expanded.Add(entry.category);
                    }
                    // The entry list is stale after toggling; redraw next frame.
                    break;
                }
                continue;
            }

            if (visible && DrawRow(row, entry.def, rowIndex))
            {
                Goal goal = GameComponent_GoalPlanner.Instance?.AddGoal(entry.def);
                if (goal != null)
                {
                    onAdded?.Invoke(goal);
                }
                Close();
                break;
            }
            rowIndex++;
        }
        Widgets.EndScrollView();
    }

    /// <summary>
    /// Inserts a header before each Architect category. The candidates are already sorted by category order,
    /// so each category's buildables are contiguous.
    /// </summary>
    private List<Entry> BuildEntries(List<ThingDef> shown, bool searching)
    {
        var entries = new List<Entry>();
        foreach (var group in shown.GroupBy(d => d.designationCategory))
        {
            List<ThingDef> defs = group.ToList();
            entries.Add(new Entry(group.Key, null, defs.Count));
            if (!searching && !expanded.Contains(group.Key))
            {
                continue;
            }
            foreach (ThingDef def in defs)
            {
                entries.Add(new Entry(group.Key, def));
            }
        }
        return entries;
    }

    /// <returns>True when the header was clicked.</returns>
    private static bool DrawCategoryHeader(Rect rect, Entry entry, bool open)
    {
        Widgets.DrawHighlightIfMouseover(rect);

        Rect arrowRect = new(rect.x + 4f, rect.y + (rect.height - 18f) / 2f, 18f, 18f);
        GUI.DrawTexture(arrowRect, open ? TexButton.Collapse : TexButton.Reveal);

        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        string label = entry.category?.LabelCap ?? "GoalPlanner_OtherCategory".Translate();
        Widgets.Label(new Rect(arrowRect.xMax + 6f, rect.y, rect.width - arrowRect.xMax - 6f, rect.height), $"{label} ({entry.count})");
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
        Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width);

        return Widgets.ButtonInvisible(rect);
    }

    /// <returns>True when the row was clicked.</returns>
    private static bool DrawRow(Rect row, ThingDef def, int index)
    {
        if (index % 2 == 1)
        {
            Widgets.DrawLightHighlight(row);
        }
        Widgets.DrawHighlightIfMouseover(row);

        // Info card button is drawn first so it gets the click instead of the row.
        Rect infoRect = new(row.xMax - 24f, row.y + (RowHeight - 24f) / 2f, 24f, 24f);
        Widgets.InfoCardButton(infoRect, def);

        Rect iconRect = new(row.x + 4f, row.y + (RowHeight - IconSize) / 2f, IconSize, IconSize);
        Widgets.DefIcon(iconRect, def);

        Text.Anchor = TextAnchor.MiddleLeft;
        Rect labelRect = new(iconRect.xMax + 8f, row.y, row.width * 0.55f, RowHeight);
        Widgets.Label(labelRect, def.LabelCap);

        GUI.color = Color.gray;
        Rect categoryRect = new(labelRect.xMax, row.y, infoRect.x - labelRect.xMax - 4f, RowHeight);
        Widgets.Label(categoryRect, def.designationCategory.LabelCap);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        return Widgets.ButtonInvisible(new Rect(row.x, row.y, infoRect.x - row.x, RowHeight));
    }
}
