using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PATHNC.MOLD_WIZARD
{
    public class MOLD_WIZARD_FINISH
    {

        public static void Run(string[] args)

        {

            MOLD_FINISH_FLOOR_WALL.Run(args);
            MOLDFINISH_PROFILE.Run(args);

        }

    }
}
