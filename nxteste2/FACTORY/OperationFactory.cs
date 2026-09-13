using NXOpen.CAM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NXOpen;
namespace FBM_CORE
{
    public  static  class OperationFactory
    {


        public static HoleDrilling  CreateHoleOperation(Part workPart, NCGroup program, Method method, CAMObject tool, CAMObject geometry, string operationType, string operationName )
        {


            return (HoleDrilling)
                workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(program, method, (NCGroup)tool, (NCGroup)geometry, "hole_making", operationType, OperationCollection.UseDefaultName.False, operationName, operationName);
        }
    }
}
