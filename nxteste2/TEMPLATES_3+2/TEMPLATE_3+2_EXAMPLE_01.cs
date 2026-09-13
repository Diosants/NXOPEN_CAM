using FBM_MACHINING_PLANAR_SURFACE;
using NX_3_PLUS_TWO_CREATE_FEATURE_GROUP;
using NX_3_PLUS_TWO_RECOGNIZE_FEATURES;
using NX_3_PLUS_TWO_TOOLPATHS;
using NX_3_PLUS_TWO_WCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace  NX_3_PLUS_TWO_TOOLPATHS
{
      public  class TEMPLATE_3_2_EXAMPLE_01
    {

        public void Execute()
        {
            //    WCS
            TOP_VIEW.Run(null);
            FRONT_VIEW.Run(null);
            BACK_VIEW.Run(null);
            RIGHT_VIEW.Run(null);
            LEFT_VIEW.Run(null);


            //  RECOGNIZE FEATURES

            TOP_RECOGNIZE_FEATURE.Run(null);
            FRONT_RECOGNIZE_FEATURE.Run(null);
            BACK_RECOGNIZE_FEATURE.Run(null);
            RIGHT_RECOGNIZE_FEATURE.Run(null);
            LEFT_RECOGNIZE_FEATURE.Run(null);


            //   CREATE FEATURES

            CREATE_TOP_FEATURE_GROUP.Run(null);
            CREATE_FRONT_FEATURE_GROUP.Run(null);
            CREATE_BACK_FEATURE_GROUP.Run(null);
            CREATE_RIGHT_FEATURE_GROUP.Run(null);
            CREATE_LEFT_FEATURE_GROUP.Run(null);



            //   ROUGH STRATEGIES
            CAVITY_MILL_THREE_PLUS_TWO_TOP.Run(null);
            CAVITY_MILL_THREE_PLUS_TWO_FRONT.Run(null);
            CAVITY_MILL_THREE_PLUS_TWO_BACK.Run(null);
            CAVITY_MILL_THREE_PLUS_TWO_RIGHT.Run(null);
            CAVITY_MILL_THREE_PLUS_TWO_LEFT.Run(null);

            //   REST MILL  CAB 16 STRATEGIES

            REST_MILL_CUTTER_16MM_TOP.Run(null);
            REST_MILL_CUTTER_16MM_FRONT.Run(null);
            REST_MILL_CUTTER_16MM_BACK.Run(null);
            REST_MILL_CUTTER_16MM_RIGHT.Run(null);
            REST_MILL_CUTTER_16MM_LEFT.Run(null);

            //  REST MIL  TOPO 8  STRATEGIES

            REST_MILL_ENDMILL_8MM_TOP.Run(null);
            REST_MILL_ENDMILL_8MM_FRONT.Run(null);
            REST_MILL_ENDMILL_8MM_BACK.Run(null);
            REST_MILL_ENDMILL_8MM_RIGHT.Run(null);
            REST_MILL_ENDMILL_8MM_LEFT.Run(null);

            //  SEMI FINISH

            TOP_5X_ZLEVEL_SEMI_FINISH.Run(null);
            FRONT_5X_ZLEVEL_SEMI_FINISH.Run(null);
            BACK_5X_ZLEVEL_SEMI_FINISH.Run(null);
            RIGHT_5X_ZLEVEL_SEMI_FINISH.Run(null);
            LEFT_5X_ZLEVEL_SEMI_FINISH.Run(null);

            //    PLANAR  SURFACES FINISH


            TOP_PLANAR_SURFACE_FINISH_PLANAR.Run(null);
            FRONT_PLANAR_SURFACE_FINISH_PLANAR.Run(null);
            BACK_PLANAR_SURFACE_FINISH_PLANAR.Run(null);
            RIGHT_PLANAR_SURFACE_FINISH_PLANAR.Run(null);
            LEFT_PLANAR_SURFACE_FINISH_PLANAR.Run(null);






        }
    }
}
