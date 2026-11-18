using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace GoalPlanner;

/// <summary>The "Goals" tab on the bottom bar.</summary>
public class MainTabWindow_GoalPlanner : MainTabWindow
{
    private const float HeaderHeight = 45f;
    private const float RowHeight = 40f;
    private const float IconSize = 32f;
    private const float SmallButtonSize = 24f;

    private Vector2 scrollPosition;

    public override Vector2 RequestedTabSize => new(800f, 600f);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 160f, 35f), "GoalPlanner_Title".Translate());
        Text.Font = GameFont.Small;

        if (Widgets.ButtonText(new Rect(inRect.xMax - 150f, inRect.y, 150f, 32f), "GoalPlanner_AddGoal".Translate()))
        {
            Find.WindowStack.Add(new Dialog_AddGoal());
        }

        Rect body = new(inRect.x, inRect.y + HeaderHeight, inRect.width, inRect.height - HeaderHeight);
        GameComponent_GoalPlanner comp = GameComponent_GoalPlanner.Instance;
        List<Goal> goals = comp?.goals;

        if (goals == null || goals.Count == 0)
        {
            Widgets.Label(body, "GoalPlanner_NoGoals".Translate());
            return;
        }

        Rect viewRect = new(0f, 0f, body.width - 16f, goals.Count * RowHeight);
        Goal toRemove = null;

        Widgets.BeginScrollView(body, ref scrollPosition, viewRect);
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
    private static bool DrawGoalRow(Rect row, Goal goal, int index)
    {
        if (index % 2 == 1)
        {
            Widgets.DrawLightHighlight(row);
        }
        Widgets.DrawHighlightIfMouseover(row);

        float midY = row.y + row.height / 2f;

        // Right side, right to left: remove, info, +, count, -
        Rect removeRect = new(row.xMax - SmallButtonSize - 4f, midY - SmallButtonSize / 2f, SmallButtonSize, SmallButtonSize);
        bool remove = Widgets.ButtonImage(removeRect, TexButton.Delete, true, "GoalPlanner_Remove".Translate());

        Rect infoRect = new(removeRect.x - SmallButtonSize - 8f, removeRect.y, SmallButtonSize, SmallButtonSize);
        Widgets.InfoCardButton(infoRect, goal.target);

        Rect plusRect = new(infoRect.x - SmallButtonSize - 16f, removeRect.y, SmallButtonSize, SmallButtonSize);
        Rect countRect = new(plusRect.x - 48f, row.y, 48f, row.height);
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

        // Left side: icon and name
        Rect iconRect = new(row.x + 4f, midY - IconSize / 2f, IconSize, IconSize);
        Widgets.DefIcon(iconRect, goal.target);

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(iconRect.xMax + 10f, row.y, minusRect.x - iconRect.xMax - 20f, row.height), goal.target.LabelCap);
        Text.Anchor = TextAnchor.UpperLeft;

        return remove;
    }
}
