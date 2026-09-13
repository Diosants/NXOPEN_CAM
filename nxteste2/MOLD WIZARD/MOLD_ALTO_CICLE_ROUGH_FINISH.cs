using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PATHNC.MOLD_WIZARD
{
     public  class MOLD_ALTO_CICLE_ROUGH_FINISH
    {

           public static void Run(string[] args)

        {


            MOLDROUGHSTRATEGY.Run(null);
            MOLD_WIZARD_REST_MILL.Run(null);
            MOLD_WIZARD_FINISH.Run(null);


        }

    }
}
