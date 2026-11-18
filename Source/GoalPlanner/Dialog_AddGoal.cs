using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace GoalPlanner;

/// <summary>Searchable list of everything the player can build. Clicking a row adds it as a goal.</summary>
public class Dialog_AddGoal : Window
{
    private const float RowHeight = 32f;
    private const float IconSize = 28f;

    private readonly List<ThingDef> candidates;
    private readonly QuickSearchWidget search = new();
    private Vector2 scrollPosition;

    public override Vector2 InitialSize => new(520f, 640f);

    public Dialog_AddGoal()
    {
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

        Rect viewRect = new(0f, 0f, outRect.width - 16f, shown.Count * RowHeight);
        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        for (int i = 0; i < shown.Count; i++)
        {
            Rect row = new(0f, i * RowHeight, viewRect.width, RowHeight);
            // Only draw rows that are on screen; the list can hold hundreds of defs.
            if (row.yMax < scrollPosition.y || row.y > scrollPosition.y + outRect.height)
            {
                continue;
            }
            if (DrawRow(row, shown[i], i))
            {
                GameComponent_GoalPlanner.Instance?.AddGoal(shown[i]);
                Close();
                break;
            }
        }
        Widgets.EndScrollView();
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
