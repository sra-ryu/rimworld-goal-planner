using RimWorld;
using UnityEngine;
using Verse;

namespace GoalPlanner;

/// <summary>The "Goals" tab on the bottom bar.</summary>
public class MainTabWindow_GoalPlanner : MainTabWindow
{
    public override Vector2 RequestedTabSize => new(800f, 600f);

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), "GoalPlanner_Title".Translate());
        Text.Font = GameFont.Small;

        Rect body = new(inRect.x, inRect.y + 45f, inRect.width, inRect.height - 45f);
        var goals = GameComponent_GoalPlanner.Instance?.goals;

        if (goals == null || goals.Count == 0)
        {
            Widgets.Label(body, "GoalPlanner_NoGoals".Translate());
            return;
        }

        float y = body.y;
        foreach (Goal goal in goals)
        {
            Widgets.Label(new Rect(body.x, y, body.width, 24f), $"{goal.count}x {goal.target.LabelCap}");
            y += 28f;
        }
    }
}
