using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class M14TapStrategy : IMachiningStrategy
    {
        public string FeatureName =>
            "M14";

        public void Execute()
        {
            M14.Run(null);
        }
    }
}