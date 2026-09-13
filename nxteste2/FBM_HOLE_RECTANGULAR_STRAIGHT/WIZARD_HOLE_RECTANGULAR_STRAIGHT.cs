using FBM_MACHINING_PARCIAL_POCKET;
using NXOpen.UF;
using PATHNC.FBM_SLOT_PARTIAL_RECTANGULAR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PATHNC.FBM_HOLE_RECTANGULAR_STRAIGHT
{
    public class WIZARD_HOLE_RECTANGULAR_STRAIGHT
    {


        public static void Run(string[] args)

        {
            FBM_HOLE_RECTANGULAR_STRAIGHT_ROUGH_CAB16.Run(args);
            FBM_HOLE_RECTANGULAR_STRAIGHT_FINISH_WALL_TOPO_10.Run(args);

            //    SLOT PARTIAL  RECTANGULAR

            WIZARD_SLOT_PARTIAL_RECTANGULAR.Run(args);





        }
    }
}
