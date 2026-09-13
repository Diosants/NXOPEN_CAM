using FBM_MACHINING_PLANAR_SURFACE;
using NX_3_PLUS_TWO_TOOLPATHS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PATHNC.FBM_SLOT_PARTIAL_RECTANGULAR
{
    public class WIZARD_SLOT_PARTIAL_RECTANGULAR
    {
     
          public static void Run(string[] args)
        {


            FBM_SLOT_PARTIAL_RECTANGULAR_ROUGH.Run(args);
            FBM_SLOT_PARTIAL_RECTANGULAR_WALL_FINISH.Run(args);
            FBM_SLOT_PARTIAL_RECTANGULAR_FLOOR_FINISH.Run(args);


        }
    }
}
