using Verse;

namespace GoalPlanner;

/// <summary>A single thing the player wants to build, e.g. "1x shuttle".</summary>
public class Goal : IExposable
{
    public ThingDef target;
    public int count = 1;

    public Goal()
    {
    }

    public Goal(ThingDef target, int count = 1)
    {
        this.target = target;
        this.count = count;
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref target, "target");
        Scribe_Values.Look(ref count, "count", 1);
    }
}
