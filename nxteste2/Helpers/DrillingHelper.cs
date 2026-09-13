using NXOpen.CAM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FBM_MACHINING_HELPERS
{
    public static  class DrillingHelper
    {

        public static void ApplyCounterSinkFeeds(HoleDrillingBuilder builder, double rpm, double feed, double safeDistance)
        {

            builder.FeedsBuilder.SpindleRpmBuilder.Value = rpm;
            builder.FeedsBuilder.FeedCutBuilder.Value = feed;

            builder.NonCuttingBuilder.TransferClearance.ClearanceType = NcmClearanceBuilder.ClearanceTypes.Automatic;
            builder.NonCuttingBuilder.TransferClearance.SafeDistance = safeDistance;

        }
    }
}
