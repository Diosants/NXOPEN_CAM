using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PATHNC.COUNTERBORE_STANDARD
{
     public  class WIZARD_CTBORE_STANDARD
    {

        public static void Run(string[] args)
        {

            //  01 Operation -    TMAX DRILLING
            // 02 Operation -     DIAMETER 01 ROUGHING
            // 03 Operation -     DIAMETER 02 ROUGHING
            // 04 Operation -     DIAMETER 03 ROUGHING


            CTBORE_STANDARD_TMAX_DRILL.Run(args);
            CTBORE_STANDARD_DIAMETER_1_ROUGH.Run(args);
            CTBORE_STANDARD_DIAMETER_2_ROUGH.Run(args);
            CTBORE_STANDARD_DIAMETER_3_ROUGH.Run(args);
             //  FINISHING STRATEGIES
            CTBORE_STANDARD_DIAMETER_1_FINISH.Run(args);
            CTBORE_STANDARD_DIAMETER_2_FINISH.Run(args);
            CTBORE_STANDARD_DIAMETER_3_FINISH.Run(args);





        }
    }
}
