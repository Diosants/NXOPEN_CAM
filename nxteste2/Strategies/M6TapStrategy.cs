using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class M6TapStrategy : IMachiningStrategy
    {
        public string FeatureName =>
            "M6";

        public void Execute()
        {
            M6.Run(null);
        }
    }
}