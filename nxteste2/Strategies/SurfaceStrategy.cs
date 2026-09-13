using   FBM_MACHINING.Strategies;
using FBM_MACHINING_PLANAR_RECTANGULAR;

namespace FBM_MACHINING_PLANAR_SURFACES
{
     public  class SurfaceStrategy : IMachiningStrategy
    {

        public string FeatureName => "SURFACE_PLANAR_RECTANGULAR";

        public void Execute()
        {


            FBM_RECTANGULAR_PLANAR_ROUGH.Run(null);
            FBM_RECTANGULAR_PLANAR_FINISH.Run(null);


        }
    }
}
