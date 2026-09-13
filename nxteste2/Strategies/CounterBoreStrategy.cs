using FBM_MACHINING.Strategies;

public class MoldCounterboreStrategy : IMachiningStrategy
{
    public string FeatureName =>
        "FG_STEP2HOLE";

    public void Execute()
    {
        USINAGEM_FIGURA_COLUNA_OP_01_DIAM_42_48_TMAX_D30MM.Execute();
        USINAGEM_FIGURA_COLUNA_OP_02_DESB_DIAM_42_CAB_25.Run(null);
         USINAGEM_FIGURA_COLUNA_OP_03_DESB_DIAM_48_CAB_25.Run(null);
        USINAGEM_FIGURA_COLUNA_OP_04_ACAB_DIAM_48_TOPO10.Run(null);
        USINAGEM_FIGURA_COLUNA_OP_05_MANDRILHAMENTO_DIAM_42.Run(null);





    }
}