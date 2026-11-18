using System.Collections.Generic;
using Verse;

namespace GoalPlanner;

/// <summary>Stores the player's goals in the save file.</summary>
public class GameComponent_GoalPlanner : GameComponent
{
    public List<Goal> goals = new();

    /// <summary>Break crafted materials (components...) down into what they're made from.</summary>
    public bool expandCrafting;

    public static GameComponent_GoalPlanner Instance => Current.Game?.GetComponent<GameComponent_GoalPlanner>();

    // RimWorld creates game components through this constructor.
    public GameComponent_GoalPlanner(Game game)
    {
    }

    /// <summary>Adds a goal, or raises the count if the same target is already a goal.</summary>
    public Goal AddGoal(ThingDef target, int count = 1)
    {
        Goal existing = goals.Find(g => g.target == target);
        if (existing != null)
        {
            existing.count += count;
            return existing;
        }

        var goal = new Goal(target, count);
        goals.Add(goal);
        return goal;
    }

    public void RemoveGoal(Goal goal) => goals.Remove(goal);

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref goals, "goals", LookMode.Deep);
        Scribe_Values.Look(ref expandCrafting, "expandCrafting");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            goals ??= new List<Goal>();
            // Drop goals whose def no longer exists (e.g. a mod was removed).
            goals.RemoveAll(g => g?.target == null);
        }
    }
}
