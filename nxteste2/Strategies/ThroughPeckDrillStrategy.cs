using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class ThroughPeckDrillStrategy : IMachiningStrategy
    {
        public string FeatureName =>
            "THROUGH_PECK_DRILL";
        public void Execute()
        {
            THROUGH_PECK_DRILL.Run(null);
            {
            }

        }

    }
}
