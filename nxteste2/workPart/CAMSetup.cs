// NX 2406
// Journal created by STELMEC on Thu Jun 11 08:59:21 2026 Hora oficial do Brasil
//
using NXOpen.CAM;

namespace workPart
{
    internal class CAMSetup
    {
        internal class CAMOperationCollection
        {
            internal class CreateHoleDrillingBuilder : HoleDrillingBuilder
            {
                private HoleDrilling op;

                public CreateHoleDrillingBuilder(HoleDrilling op)
                {
                    this.op = op;
                }
            }
        }
    }
}