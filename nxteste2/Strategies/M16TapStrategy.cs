
using FBM_MACHINING.Strategies;

namespace FBM_DRILLINGS
{
    public class M16TapStrategy : IMachiningStrategy
    {
        public string FeatureName =>
            "M16";

        public void Execute()
        {
            M16.Run(null);
        }
    }
}       
 
