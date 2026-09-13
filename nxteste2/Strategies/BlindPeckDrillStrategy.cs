using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class BlindPeckDrillStrategy : IMachiningStrategy
    {
        public string FeatureName =>
            "BLIND_PECK_DRILL";

        public void Execute()
        {
            BLIND_PECK_DRILL.Run(null);


        }

    }
}
