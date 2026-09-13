using FBM_MACHINING.Strategies;
using NXOpen.CAM;

namespace FBM_DRILLINGS
{
    public class ChamferMillingStrategy : IMachiningStrategy
    {
        public string FeatureName =>
            "CHAMFER_MILLING";
        public void Execute()
        {
            CHAMFER_MILLING.Run(null);

        }
    }
}
    
