
using System;
using NXOpen;


namespace FBM_MACHINING_HELPERS
{
    public class FIND_ALL_FEATURES
    {
        public static void Run(string[] args)
        {
            NXOpen.Session theSession = NXOpen.Session.GetSession();
            NXOpen.Part workPart = theSession.Parts.Work;
            NXOpen.Part displayPart = theSession.Parts.Display;


            NXOpen.CAM.CAMObject nullNXOpen_CAM_CAMObject = null;
            NXOpen.CAM.FeatureRecognitionBuilder featureRecognitionBuilder1;
            featureRecognitionBuilder1 = workPart.CAMSetup.CreateFeatureRecognitionBuilder(nullNXOpen_CAM_CAMObject);

            NXOpen.CAM.ManualFeatureBuilder manualFeatureBuilder1;
            manualFeatureBuilder1 = featureRecognitionBuilder1.CreateManualFeatureBuilder();
            featureRecognitionBuilder1.MapFeatures = true;


            NXOpen.Point3d origin1 = new NXOpen.Point3d(0.0, 0.0, 0.0);
            NXOpen.Vector3d vector1 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
            NXOpen.Direction direction1;
            direction1 = workPart.Directions.CreateDirection(origin1, vector1, NXOpen.SmartObject.UpdateOption.AfterModeling);

            featureRecognitionBuilder1.RecognitionType = NXOpen.CAM.FeatureRecognitionBuilder.RecognitionEnum.Parametric;

            featureRecognitionBuilder1.UseFeatureNameAsType = true;

            featureRecognitionBuilder1.IgnoreWarnings = false;

            NXOpen.Direction[] vecdirections1 = new NXOpen.Direction[1];
            vecdirections1[0] = direction1;
            featureRecognitionBuilder1.SetMachiningAccessDirection(vecdirections1, 9.9999999999999995e-07);

            string[] featureTypes1 = new string[117];
            featureTypes1[0] = "STEP1POCKET";
            featureTypes1[1] = "CORNER_NOTCH_STRAIGHT";
            featureTypes1[2] = "SLOT_PARTIAL_RECTANGULAR";
            featureTypes1[3] = "SLOT_PARTIAL_U_SHAPED";
            featureTypes1[4] = "STEP1POCKET_THREAD";
            featureTypes1[5] = "STEP2POCKET_THREAD";
            featureTypes1[6] = "STEP2HOLE_THREAD";
            featureTypes1[7] = "STEP1HOLE_THREAD";
            featureTypes1[8] = "STEP1HOLE";
            featureTypes1[9] = "STEP1POCKET";
            featureTypes1[10] = "POCKET_ROUND_TAPERED";
            featureTypes1[11] = "STEP2POCKET";
            featureTypes1[12] = "STEP2HOLE";
            featureTypes1[13] = "COUNTER_BORE_HOLE";
            featureTypes1[14] = "COUNTER_SUNK_HOLE";
            featureTypes1[15] = "POCKET_RECTANGULAR_STRAIGHT";
            featureTypes1[16] = "STEP1POCKET_THREAD";
            featureTypes1[17] = "STEP2POCKET_THREAD";
            featureTypes1[18] = "STEP2HOLE_THREAD";
            featureTypes1[19] = "STEP1HOLE_THREAD";
            featureTypes1[20] = "STEP1HOLE";
            featureTypes1[21] = "STEP1POCKET";
            featureTypes1[22] = "STEP1HOLE";
            featureTypes1[23] = "STEP1POCKET";
            featureTypes1[24] = "STEP2POCKET";
            featureTypes1[25] = "STEP2HOLE";
            featureTypes1[26] = "STEP1POCKET_THREAD";
            featureTypes1[27] = "STEP2POCKET_THREAD";
            featureTypes1[28] = "STEP2HOLE_THREAD";
            featureTypes1[29] = "STEP1HOLE_THREAD";
            featureTypes1[30] = "STEP1HOLE";
            featureTypes1[31] = "STEP2HOLE";
            featureTypes1[32] = "STEP1HOLE";
            featureTypes1[33] = "BOSS_RECTANGULAR_STRAIGHT";
            featureTypes1[34] = "BOSS_ROUND_STRAIGHT";
            featureTypes1[35] = "BOSS_ROUND_STRAIGHT_THREAD";
            featureTypes1[36] = "CORNER_NOTCH_RECTANGULAR";
            featureTypes1[37] = "CORNER_NOTCH_ROUND_CONCAVE";
            featureTypes1[38] = "CORNER_NOTCH_STRAIGHT";
            featureTypes1[39] = "CORNER_NOTCH_U_SHAPED";
            featureTypes1[40] = "GROOVE_AX_CIRCULAR_RECT";
            featureTypes1[41] = "GROOVE_INS_RAD_RECT";
            featureTypes1[42] = "HOLE_ROUND_INTERRUPTED_STRAIGHT";
            featureTypes1[43] = "HOLE_ROUND_TAPERED";
            featureTypes1[44] = "HOLE_OBROUND_CURVED_STRAIGHT";
            featureTypes1[45] = "HOLE_FREE_SHAPED_STRAIGHT";
            featureTypes1[46] = "HOLE_OBROUND_STRAIGHT";
            featureTypes1[47] = "HOLE_RECTANGULAR_STRAIGHT";
            featureTypes1[48] = "POCKET_ROUND_TAPERED";
            featureTypes1[49] = "POCKET_CLOSED";
            featureTypes1[50] = "POCKET_OBROUND_CURVED_STRAIGHT";
            featureTypes1[51] = "POCKET_FREE_SHAPED_STRAIGHT";
            featureTypes1[52] = "POCKET_OPEN";
            featureTypes1[53] = "POCKET_OBROUND_STRAIGHT";
            featureTypes1[54] = "POCKET_RECTANGULAR_STRAIGHT";
            featureTypes1[55] = "SIDE_NOTCH_RECTANGULAR";
            featureTypes1[56] = "SIDE_NOTCH_ROUND_CONCAVE";
            featureTypes1[57] = "SIDE_NOTCH_U_SHAPED";
            featureTypes1[58] = "SLOT_90_DEGREE";
            featureTypes1[59] = "SLOT_DOVE_TAIL";
            featureTypes1[60] = "SLOT_OBROUND";
            featureTypes1[61] = "SLOT_PARTIAL_OBROUND";
            featureTypes1[62] = "SLOT_PARTIAL_RECTANGULAR";
            featureTypes1[63] = "SLOT_PARTIAL_ROUND";
            featureTypes1[64] = "SLOT_PARTIAL_U_SHAPED";
            featureTypes1[65] = "SLOT_RECTANGULAR";
            featureTypes1[66] = "SLOT_ROUND";
            featureTypes1[67] = "SLOT_T_SHAPED";
            featureTypes1[68] = "SLOT_U_SHAPED";
            featureTypes1[69] = "SLOT_UPSIDE_DOWN_DOVE_TAIL";
            featureTypes1[70] = "SLOT_V_SHAPED";
            featureTypes1[71] = "STEP1POCKET";
            featureTypes1[72] = "STEP2POCKET";
            featureTypes1[73] = "STEP2HOLE";
            featureTypes1[74] = "STEP3POCKET";
            featureTypes1[75] = "STEP3HOLE";
            featureTypes1[76] = "STEP3HOLE1";
            featureTypes1[77] = "STEP4POCKET";
            featureTypes1[78] = "STEP4HOLE";
            featureTypes1[79] = "STEP4HOLE1";
            featureTypes1[80] = "STEP5POCKET";
            featureTypes1[81] = "STEP5HOLE";
            featureTypes1[82] = "STEP5HOLE1";
            featureTypes1[83] = "STEP5HOLE2";
            featureTypes1[84] = "STEP6POCKET";
            featureTypes1[85] = "STEP6HOLE";
            featureTypes1[86] = "STEP6HOLE1";
            featureTypes1[87] = "STEP6HOLE2";
            featureTypes1[88] = "STEP1POCKET_THREAD";
            featureTypes1[89] = "STEP2POCKET_THREAD";
            featureTypes1[90] = "STEP2HOLE_THREAD";
            featureTypes1[91] = "STEP3POCKET_THREAD";
            featureTypes1[92] = "STEP3HOLE_THREAD";
            featureTypes1[93] = "STEP3HOLE1_THREAD";
            featureTypes1[94] = "STEP4POCKET_THREAD";
            featureTypes1[95] = "STEP4HOLE_THREAD";
            featureTypes1[96] = "STEP4HOLE1_THREAD";
            featureTypes1[97] = "STEP5POCKET_THREAD";
            featureTypes1[98] = "STEP5HOLE_THREAD";
            featureTypes1[99] = "STEP5HOLE1_THREAD";
            featureTypes1[100] = "STEP5HOLE2_THREAD";
            featureTypes1[101] = "STEP6POCKET_THREAD";
            featureTypes1[102] = "STEP6HOLE_THREAD";
            featureTypes1[103] = "STEP6HOLE1_THREAD";
            featureTypes1[104] = "STEP6HOLE2_THREAD";
            featureTypes1[105] = "STEP1HOLE_THREAD";
            featureTypes1[106] = "STEP1HOLE";
            featureTypes1[107] = "SURFACE_PLANAR";
            featureTypes1[108] = "SURFACE_PLANAR_RECTANGULAR";
            featureTypes1[109] = "SURFACE_PLANAR_ROUND";
            featureTypes1[110] = "TURNING_GROOVE_FACE";
            featureTypes1[111] = "TURNING_GROOVE_ID";
            featureTypes1[112] = "TURNING_GROOVE_OD";
            featureTypes1[113] = "WEDM_FREE_SHAPED_STRAIGHT";
            featureTypes1[114] = "WEDM_OBROUND_STRAIGHT";
            featureTypes1[115] = "WEDM_RECTANGULAR_STRAIGHT";
            featureTypes1[116] = "WEDM_ROUND_STRAIGHT";
            featureRecognitionBuilder1.SetFeatureTypes(featureTypes1);

            featureRecognitionBuilder1.GeometrySearchType = NXOpen.CAM.FeatureRecognitionBuilder.GeometrySearch.Workpiece;

            NXOpen.CAM.CAMFeature[] features1;
            features1 = featureRecognitionBuilder1.FindFeatures();


            NXOpen.NXObject nXObject1;
            nXObject1 = featureRecognitionBuilder1.Commit();


            featureRecognitionBuilder1.Destroy();

            manualFeatureBuilder1.Destroy();



        }
        public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
    }
}
