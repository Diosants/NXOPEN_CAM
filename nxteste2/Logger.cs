using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FBM_MACHINING_HELPERS
{
      public static  class Logger
    {

        public static Action<string> LogAction { get; set; }
        public static Action<int> ProgressAction;


        public static void Write( string nessage)
        {

            LogAction?.Invoke(nessage);
        }

            public static void ReportProgress(int percentage)
            {
                ProgressAction?.Invoke(percentage);
        }
    }
}
