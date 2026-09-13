using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class CboreMillStrategy : IMachiningStrategy
    {
        public string FeatureName =>
            "CBORE_MILL";

        public void Execute()
        {
            Cbore_MILL.Run(null);
        }
    }
}
