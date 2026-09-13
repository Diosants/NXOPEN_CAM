using FBM_MACHINING.Strategies;
using FBM_MACHINING_PLANAR_RECTANGULAR;


public class PlanarFaceStrategy : IMachiningStrategy
    {

        public string FeatureName => "RECTANGULAR_SURFACE";

        public void Execute()
        {
        FBM_RECTANGULAR_PLANAR_ROUGH.Run(null);
        FBM_RECTANGULAR_PLANAR_FINISH.Run(null);



    }
    }
