using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FBM_MACHINING.Strategies;

namespace  FBM_DRILLINGS
{
    public  class M10TapStrategy : IMachiningStrategy
    {
        public string FeatureName =>
           "M10";
            

        public void Execute()
        {
            NXOpen.Session.GetSession()
                .ListingWindow.WriteLine("Executando M10");
            M10.Run(null);

        }

    }
}
