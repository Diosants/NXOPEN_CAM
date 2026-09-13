// NX 2406
// Journal created by STELMEC on Thu Jul 30 13:53:40 2026 Hora oficial do Brasil
//
using System;
using NXOpen;

public class MOLD_FINISH_FLOOR_WALL
{
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;


        NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));
        NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("ENDMILL_D10MM"));
        NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, nCGroup2, tool1, featureGeometry1, "mill_contour", "ZLEVEL_PROFILE_STEEP", NXOpen.CAM.OperationCollection.UseDefaultName.True, "ZLEVEL_PROFILE_STEEP", "Zlevel Profile Steep");


        NXOpen.CAM.ZLevelMilling zLevelMilling1 = ((NXOpen.CAM.ZLevelMilling)operation1);
        NXOpen.CAM.ZLevelMillingBuilder zLevelMillingBuilder1;
        zLevelMillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateZlevelMillingBuilder(zLevelMilling1);


        zLevelMillingBuilder1.CutParameters.SteepContainment.Type = NXOpen.CAM.SteepContainment.Types.None;

        zLevelMillingBuilder1.CutLevel.GlobalDepthPerCut.DistanceBuilder.Intent = NXOpen.CAM.ParamValueIntent.PartUnits;

        zLevelMillingBuilder1.CutLevel.ApplyGlobalDepthPerCut();

        zLevelMillingBuilder1.CutLevel.GlobalDepthPerCut.DistanceBuilder.Value = 1;

        zLevelMillingBuilder1.CutLevel.ApplyGlobalDepthPerCut();


        zLevelMillingBuilder1.CutParameters.TrimControl = NXOpen.CAM.CutParametersTrimControlTypes.Silhoutte;   ///  ALTERAR PARA CONTORNO  TOTAL

        zLevelMillingBuilder1.CutParameters.FloorSameAsPartStock = false;

        zLevelMillingBuilder1.FeedsBuilder.SpindleRpmBuilder.Value = 6000.0;
        zLevelMillingBuilder1.FeedsBuilder.FeedCutBuilder.Value = 5000;


        zLevelMillingBuilder1.CutParameters.CutDirection.Type = NXOpen.CAM.CutDirection.Types.Mixed;

        zLevelMillingBuilder1.CutParameters.CutOrder = NXOpen.CAM.CutParametersCutOrderTypes.DepthFirstAlways;

        zLevelMillingBuilder1.CutParameters.LevelToLevel.Type = NXOpen.CAM.LevelToLevel.Types.DirectOnPart;

        zLevelMillingBuilder1.NonCuttingBuilder.EngageClosedAreaBuilder.EngRetType = NXOpen.CAM.NcmPlanarEngRetBuilder.EngRetTypes.RampOnShape;

        zLevelMillingBuilder1.NonCuttingBuilder.EngageClosedAreaBuilder.HeightBuilder.Value = 1.0;
        zLevelMillingBuilder1.CutParameters.CutBetweenLevels = true;

        zLevelMillingBuilder1.CutParameters.Stepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.PercentToolFlat;

        zLevelMillingBuilder1.CutParameters.Stepover.PercentToolFlatBuilder.Value = 60.0;


        zLevelMillingBuilder1.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        zLevelMillingBuilder1.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;

        zLevelMillingBuilder1.NonCuttingBuilder.TransferWithinLevelsType = NXOpen.CAM.NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;

        zLevelMillingBuilder1.NonCuttingBuilder.TransferWithinLevelsSafeDistanceBuilder.Value = 0.0;

        NXOpen.NXObject nXObject1;
        nXObject1 = zLevelMillingBuilder1.Commit();

        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.ZLevelMilling zLevelMilling2 = ((NXOpen.CAM.ZLevelMilling)nXObject1);
        objects1[0] = zLevelMilling2;
        workPart.CAMSetup.GenerateToolPath(objects1);


        zLevelMillingBuilder1.Destroy();





    }
    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
}
