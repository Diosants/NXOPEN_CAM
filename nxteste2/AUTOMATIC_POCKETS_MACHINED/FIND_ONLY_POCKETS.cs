
using System;
using NXOpen;


namespace FBM_MACHINING_HELPERS
{
    public class FIND_ONLY_POCKETS
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

            string[] featureTypes1 = new string[3];
            featureTypes1[0] = "FG_POCKET_OBROUND_STRAIGHT";
            featureTypes1[1] = "FG_POCKET_RECTANGULAR_STRAIGHT";
            featureTypes1[2] = "FG_SLOT_PARTIAL_RECTANGULAR";
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
