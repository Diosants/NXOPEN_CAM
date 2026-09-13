using FBM_MACHINING.Strategies;
using FBM_MACHINING_PLANAR_RECTANGULAR;
using NX_3_PLUS_TWO_TOOLPATHS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FBM_MACHINING_PLANAR_SURFACE
{
     public  class PlanarSurfaceStrategy : IMachiningStrategy
    {

         public string FeatureName =>
             "SURFACE_PLANAR";

        public void Execute()
        {
             //    SURFACE PLANAR
            FBM_PLANAR_SURFACE_ROUGH.Run(null);
            FBM_PLANAR_SURFACE_FLOOR_FINISH.Run(null);
            FBM_PLANAR_SURFACE_WALL_FINISH.Run(null);

                //  SURFACE PLANAR RECTANGULAR
                FBM_RECTANGULAR_PLANAR_ROUGH.Run(null);
                FBM_RECTANGULAR_PLANAR_FINISH.Run(null);

            //  SURFACE PLANAR ROUND  

            FBM_PLANAR_ROUND_ROUGH.Run(null);
            FBM_PLANAR_ROUND_FLOOR_FINISH.Run(null);
            FBM_PLANAR_ROUND_WALL_FINISH.Run(null);





        }
    }
}
