
using FBM_MACHINING_PLANAR_SURFACE;
using NX_3_PLUS_TWO_TOOLPATHS;
using NXOpen;
using NXOpen.CAM;
using System;
using System.Collections.Generic;
using System.Globalization;

public class THREE_PLUS_TWO_SEMI_FINISH
{
    public static void Run(string[] args)

    {



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



    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
