using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class M20TapStrategy : IMachiningStrategy
    {
        public string FeatureName =>
            "M20";

        public void Execute()
        {
            M20.Run(null);
        }
    }
}
