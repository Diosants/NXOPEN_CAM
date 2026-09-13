using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class M8TapStrategy : IMachiningStrategy
    {
        public string FeatureName =>
            "M8";

        public void Execute()
        {
            M8.Run(null);
        }
    }
}
