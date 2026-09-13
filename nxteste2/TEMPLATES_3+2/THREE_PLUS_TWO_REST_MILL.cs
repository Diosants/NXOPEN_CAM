
using NX_3_PLUS_TWO_TOOLPATHS;
using NXOpen;
using NXOpen.CAM;
using System;
using System.Collections.Generic;

public class THREE_PLUS_TWO_REST_MILL
{
    public static void Run(string[] args)
    {




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



    }

  
    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
