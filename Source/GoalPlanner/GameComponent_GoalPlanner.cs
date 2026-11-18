using System.Collections.Generic;
using Verse;

namespace GoalPlanner;

/// <summary>Stores the player's goals in the save file.</summary>
public class GameComponent_GoalPlanner : GameComponent
{
    public List<Goal> goals = new();

    public static GameComponent_GoalPlanner Instance => Current.Game?.GetComponent<GameComponent_GoalPlanner>();

    // RimWorld creates game components through this constructor.
    public GameComponent_GoalPlanner(Game game)
    {
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref goals, "goals", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            goals ??= new List<Goal>();
            // Drop goals whose def no longer exists (e.g. a mod was removed).
            goals.RemoveAll(g => g?.target == null);
        }
    }
}
