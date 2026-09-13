using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace PATHNC.FBM_MOLD_COUNTERBORE_MACHINING
{
    public class WIZARD_CBORE_MOLD
    {

          public static void Run(string[] args)

        {
            USINAGEM_FIGURA_COLUNA_OP_01_DIAM_42_48_TMAX_D30MM.Run(args);
            USINAGEM_FIGURA_COLUNA_OP_02_DESB_DIAM_42_CAB_25.Run(args);
            USINAGEM_FIGURA_COLUNA_OP_03_DESB_DIAM_48_CAB_25.Run(args);
            USINAGEM_FIGURA_COLUNA_OP_04_ACAB_DIAM_48_TOPO10.Run(args);
            USINAGEM_FIGURA_COLUNA_OP_05_MANDRILHAMENTO_DIAM_42.Run(args);




        }
    }
}
