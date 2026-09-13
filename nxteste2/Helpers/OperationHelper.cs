using NXOpen.CAM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FBM_MACHINING_HELPERS
{
     public static  class OperationHelper
    {

        public static void DisableGougeCheck(
            
            HoleDrillingBuilder builder)
        {

            builder.CollisionCheck = false;
            builder.GougeChecking = false;
            builder.NonCuttingBuilder.CollisionCheck = false;

        }


        public static void EnableGougeCheck(
            
            HoleDrillingBuilder builder1)
        {

            builder1.CollisionCheck = true;
            builder1.GougeChecking = true;
            builder1.NonCuttingBuilder.CollisionCheck = true;
        }


    }
}
