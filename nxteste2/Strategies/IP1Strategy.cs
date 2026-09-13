using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FBM_MACHINING.MOLD_PLATES
{
    public  interface IP1Strategy
    {

        string featureName { get; }

        void execute();
    }

  
    }
