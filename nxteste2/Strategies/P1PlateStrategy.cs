
using FBM_MACHINING.Strategies;
using FBM_MACHINING_PLANAR_SURFACE;
using FBM_MACHINING_RECTANGULAR_POCKET;


public  class P1PlateStrategy : IMachiningStrategy
{
      public string FeatureName => "P1_PLATE";
        public void Execute()
        {


        //  planar surface Strategy
        USINAGEM_FIGURA_PLANAR_SURFACE_OP_01_DESBASTE.Run(null);
        //USINAGEM_FIGURA_PLANAR_SURFACE_OP_02_ACAB_PLANO.Run(null);
        FBM_SLOT_PARTIAL_RECTANGULAR_WALL_FINISH.Run(null);


        //  open pocket Strategy
       // FBM_MACHINING_OPEN_POCKET_USINAGEM_FIGURA_PARCIAL_RECTANGULAR_POCKET_CAB16.Execute();
      //  USUSINAGEM_FIGURA_PARCIAL_RECTANGULAR_POCKET_OP3_ACAB_AREA_PLANA_TOPO10.Execute();
      //  USINAGEM_FIGURA_PARCIAL_RECTANGULAR_POCKET_OP3_ACAB_PERFIL_TOPO10.Execute();

        //  closed pocket Strategy

      


        //  counterbore Strategy

        USINAGEM_FIGURA_COLUNA_OP_01_DIAM_42_48_TMAX_D30MM.Execute();
        USINAGEM_FIGURA_COLUNA_OP_02_DESB_DIAM_42_CAB_25.Run(null);
        USINAGEM_FIGURA_COLUNA_OP_03_DESB_DIAM_48_CAB_25.Run(null);
        USINAGEM_FIGURA_COLUNA_OP_04_ACAB_DIAM_48_TOPO10.Run(null);
        USINAGEM_FIGURA_COLUNA_OP_05_MANDRILHAMENTO_DIAM_42.Run(null);



    }
}

