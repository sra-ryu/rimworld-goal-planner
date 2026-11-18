using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace GoalPlanner;

/// <summary>The "Goals" tab on the bottom bar: goal list on the left, plan for the selected goal on the right.</summary>
public class MainTabWindow_GoalPlanner : MainTabWindow
{
    private const float HeaderHeight = 45f;
    private const float ListWidth = 340f;
    private const float RowHeight = 40f;
    private const float IconSize = 32f;
    private const float SmallButtonSize = 24f;

    private readonly GoalDetailPanel detailPanel = new();
    private Vector2 scrollPosition;
    private Goal selected;

    public override Vector2 RequestedTabSize => new(1000f, 680f);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 160f, 35f), "GoalPlanner_Title".Translate());
        Text.Font = GameFont.Small;

        GameComponent_GoalPlanner comp = GameComponent_GoalPlanner.Instance;
        if (comp == null)
        {
            return;
        }

        Rect body = new(inRect.x, inRect.y + HeaderHeight, inRect.width, inRect.height - HeaderHeight);
        Rect listRect = new(body.x, body.y, ListWidth, body.height);
        Rect detailRect = new(listRect.xMax + 10f, body.y, body.width - ListWidth - 10f, body.height);

        if (Widgets.ButtonText(new Rect(listRect.x, inRect.y, ListWidth, 32f), "GoalPlanner_AddGoal".Translate()))
        {
            Find.WindowStack.Add(new Dialog_AddGoal(goal => selected = goal));
        }

        List<Goal> goals = comp.goals;
        if (goals.Count == 0)
        {
            Widgets.Label(listRect, "GoalPlanner_NoGoals".Translate());
            return;
        }

        if (selected == null || !goals.Contains(selected))
        {
            selected = goals[0];
        }

        DrawGoalList(listRect, comp);

        if (selected != null)
        {
            detailPanel.Draw(detailRect, selected, comp);
        }
    }

    private void DrawGoalList(Rect rect, GameComponent_GoalPlanner comp)
    {
        List<Goal> goals = comp.goals;
        Rect viewRect = new(0f, 0f, rect.width - 16f, goals.Count * RowHeight);
        Goal toRemove = null;

        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
        for (int i = 0; i < goals.Count; i++)
        {
            Rect row = new(0f, i * RowHeight, viewRect.width, RowHeight);
            if (DrawGoalRow(row, goals[i], i))
            {
                toRemove = goals[i];
            }
        }
        Widgets.EndScrollView();

        // Remove after drawing so the list isn't modified mid-loop.
        if (toRemove != null)
        {
            comp.RemoveGoal(toRemove);
        }
    }

    /// <returns>True when the remove button was clicked.</returns>
    private bool DrawGoalRow(Rect row, Goal goal, int index)
    {
        if (goal == selected)
        {
            Widgets.DrawHighlightSelected(row);
        }
        else if (index % 2 == 1)
        {
            Widgets.DrawLightHighlight(row);
        }
        Widgets.DrawHighlightIfMouseover(row);

        float midY = row.y + row.height / 2f;

        // Right side, right to left: remove, +, count, -
        Rect removeRect = new(row.xMax - SmallButtonSize - 4f, midY - SmallButtonSize / 2f, SmallButtonSize, SmallButtonSize);
        bool remove = Widgets.ButtonImage(removeRect, TexButton.Delete, true, "GoalPlanner_Remove".Translate());

        Rect plusRect = new(removeRect.x - SmallButtonSize - 12f, removeRect.y, SmallButtonSize, SmallButtonSize);
        Rect countRect = new(plusRect.x - 40f, row.y, 40f, row.height);
        Rect minusRect = new(countRect.x - SmallButtonSize, removeRect.y, SmallButtonSize, SmallButtonSize);

        if (Widgets.ButtonImage(minusRect, TexButton.Minus) && goal.count > 1)
        {
            goal.count--;
        }
        if (Widgets.ButtonImage(plusRect, TexButton.Plus))
        {
            goal.count++;
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(countRect, goal.count.ToString());

        // Left side: icon and name; clicking selects the goal.
        Rect iconRect = new(row.x + 4f, midY - IconSize / 2f, IconSize, IconSize);
        Widgets.DefIcon(iconRect, goal.target);

        Text.Anchor = TextAnchor.MiddleLeft;
        Rect labelRect = new(iconRect.xMax + 8f, row.y, minusRect.x - iconRect.xMax - 12f, row.height);
        Widgets.Label(labelRect, goal.target.LabelCap.Truncate(labelRect.width));
        Text.Anchor = TextAnchor.UpperLeft;

        if (Widgets.ButtonInvisible(new Rect(row.x, row.y, minusRect.x - row.x, row.height)))
        {
            selected = goal;
        }

        return remove;
    }
}
