using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PATHNC.MOLD_WIZARD
{
    public  class MOLD_WIZARD_REST_MILL
    {


          public static  void Run(string[] args)

        {
            MOLDRESTMILL_CUTTER_D25_R8.Run(args);
            MOLDRESTMILL_CUTTER_D16_R8.Run(args);
            MOLD_CORNER_RELIEF.Run(args);
            MOLDRESTMILL_ENDMILL_D10MM.Run(args);




        }

    }
}
