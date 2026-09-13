using FBM_MACHINING.Strategies;

namespace FBM_MACHINING_PARCIAL_POCKET
{
    public class OpenPocketStrategy : IMachiningStrategy
    {
        public string FeatureName =>
            "FG_SLOT_PARTIAL_RECTANGULAR";


        public void Execute()
        {
            FBM_MACHINING_OPEN_POCKET_USINAGEM_FIGURA_PARCIAL_RECTANGULAR_POCKET_CAB16.Execute();

            USUSINAGEM_FIGURA_PARCIAL_RECTANGULAR_POCKET_OP3_ACAB_AREA_PLANA_TOPO10.Execute();

            USINAGEM_FIGURA_PARCIAL_RECTANGULAR_POCKET_OP3_ACAB_PERFIL_TOPO10.Execute();
        }
    }
}