using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class CSinkStrategy : IMachiningStrategy 
    {

        public string FeatureName =>
            "CSINK";
        public void Execute()
        {
            CSINK.Run(null);
        }
    }
}
