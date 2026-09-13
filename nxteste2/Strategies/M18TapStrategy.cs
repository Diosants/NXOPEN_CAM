using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class M18TapStrategy : IMachiningStrategy
    {
        public string FeatureName =>
            "M18";

        public void Execute()
        {
            M18.Run(null);
        }
    }
}
