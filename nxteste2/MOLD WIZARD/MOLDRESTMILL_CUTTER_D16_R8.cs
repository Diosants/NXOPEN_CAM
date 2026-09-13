// NX 2406
// Journal created by STELMEC on Thu Jul 30 11:20:14 2026 Hora oficial do Brasil
//
using System;
using NXOpen;

public class MOLDRESTMILL_CUTTER_D16_R8
{
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;


        NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));
        NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("CUTTER_D16_R.8"));
        NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, nCGroup2, tool1, featureGeometry1, "mill_contour", "REST_MILLING", NXOpen.CAM.OperationCollection.UseDefaultName.True, "REST_MILLING", "Rest Milling");


        NXOpen.CAM.CavityMilling cavityMilling1 = ((NXOpen.CAM.CavityMilling)operation1);
        NXOpen.CAM.CavityMillingBuilder cavityMillingBuilder1;
        cavityMillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateCavityMillingBuilder(cavityMilling1);


        cavityMillingBuilder1.CutPattern.CutPattern = NXOpen.CAM.CutPatternBuilder.Types.FollowPeriphery;

        cavityMillingBuilder1.CutLevel.GlobalDepthPerCut.DistanceBuilder.Value = 2.0;
        cavityMillingBuilder1.CutParameters.PartStock.Value = 1.0;
        cavityMillingBuilder1.CutParameters.FloorStock.Value = 0.5;

        cavityMillingBuilder1.CutLevel.ApplyGlobalDepthPerCut();

        cavityMillingBuilder1.CutParameters.TrimControl = NXOpen.CAM.CutParametersTrimControlTypes.Silhoutte;

        cavityMillingBuilder1.CutParameters.CutOrder = NXOpen.CAM.CutParametersCutOrderTypes.DepthFirst;

        cavityMillingBuilder1.FeedsBuilder.SpindleRpmBuilder.Value = 4000.0;
        cavityMillingBuilder1.FeedsBuilder.FeedCutBuilder.Value = 5000;


        cavityMillingBuilder1.CutParameters.CutBelowOverhangingBlank = false;

        cavityMillingBuilder1.CutParameters.SmallAreaAvoidance.SmallAreaStatus = NXOpen.CAM.SmallAreaAvoidance.StatusTypes.Ignore;


        cavityMillingBuilder1.NonCuttingBuilder.EngageClosedAreaBuilder.EngRetType = NXOpen.CAM.NcmPlanarEngRetBuilder.EngRetTypes.RampOnShape;


        cavityMillingBuilder1.NonCuttingBuilder.RetractAreaBuilder.EngRetType = NXOpen.CAM.NcmPlanarEngRetBuilder.EngRetTypes.None;


        cavityMillingBuilder1.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        cavityMillingBuilder1.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;

        cavityMillingBuilder1.NonCuttingBuilder.TransferBetweenRegionsBuilder.Type = NXOpen.CAM.NcmTransfer.TransferTypes.PrevPlane;

        cavityMillingBuilder1.NonCuttingBuilder.TransferBetweenRegionsBuilder.SafeDistanceBuilder.Value = 1.0;

        cavityMillingBuilder1.NonCuttingBuilder.TransferWithinLevelsType = NXOpen.CAM.NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;

        cavityMillingBuilder1.NonCuttingBuilder.TransferWithinLevelsSafeDistanceBuilder.Value = 0.0;

        NXOpen.NXObject nXObject1;
        nXObject1 = cavityMillingBuilder1.Commit();

        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CavityMilling cavityMilling2 = ((NXOpen.CAM.CavityMilling)nXObject1);
        objects1[0] = cavityMilling2;
        workPart.CAMSetup.GenerateToolPath(objects1);


        cavityMillingBuilder1.Destroy();



    }
    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
}
