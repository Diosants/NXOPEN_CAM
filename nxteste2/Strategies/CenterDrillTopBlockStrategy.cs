using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class CenterDrillTopBlockStrategy: IMachiningStrategy
    {
        public string FeatureName =>
            "CENTER_DRILL_TOP_BLOCK";
        public void Execute()
        {
            FG_CENTER_DRILL_ALL_HOLES.Run(null);

        }
    }
}
