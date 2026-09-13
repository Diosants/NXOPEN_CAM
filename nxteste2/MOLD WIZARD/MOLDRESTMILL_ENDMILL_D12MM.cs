// NX 2406
// Journal created by STELMEC on Thu Jul 30 13:13:46 2026 Hora oficial do Brasil
//
using System;
using NXOpen;

public class MOLDRESTMILL_ENDMILL_D12MM_REF_TOOL_DIAMETER_25MM
{
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;


        NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));
        NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("ENDMILL_D12MM"));
        NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, nCGroup2, tool1, featureGeometry1, "mill_contour", "FLOW_MILL_REF_TOOL", NXOpen.CAM.OperationCollection.UseDefaultName.True, "FLOW_MILL_REF_TOOL_1", "Flow Mill Ref Tool 1");



        NXOpen.CAM.SurfaceContour surfaceContour1 = ((NXOpen.CAM.SurfaceContour)operation1);
        NXOpen.CAM.SurfaceContourBuilder surfaceContourBuilder1;
        surfaceContourBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateSurfaceContourBuilder(surfaceContour1);


        surfaceContourBuilder1.FlowBuilder.SteepCutting.DepthPerCut.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.Constant;

        NXOpen.NXObject nXObject1;
        nXObject1 = surfaceContourBuilder1.Commit();

        NXOpen.CAM.Tool tool2 = ((NXOpen.CAM.Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("CUTTER_D25_R.8"));
        surfaceContourBuilder1.ReferenceTool = tool2;


        surfaceContourBuilder1.Destroy();



        NXOpen.CAM.SurfaceContour surfaceContour2 = ((NXOpen.CAM.SurfaceContour)nXObject1);
        NXOpen.CAM.SurfaceContourBuilder surfaceContourBuilder2;
        surfaceContourBuilder2 = workPart.CAMSetup.CAMOperationCollection.CreateSurfaceContourBuilder(surfaceContour2);


        surfaceContourBuilder2.FlowBuilder.SteepCutting.DepthPerCut.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.Constant;

        surfaceContourBuilder2.FlowBuilder.NonSteepCutting.CutPattern.CutPattern = NXOpen.CAM.CutPatternBuilder.Types.None;

        surfaceContourBuilder2.FlowBuilder.SteepCutting.DepthPerCut.DistanceBuilder.Intent = NXOpen.CAM.ParamValueIntent.PartUnits;

        surfaceContourBuilder2.FlowBuilder.SteepCutting.DepthPerCut.DistanceBuilder.Value = 0.5;

        surfaceContourBuilder2.FeedsBuilder.SpindleRpmBuilder.Value = 4000.0;

        surfaceContourBuilder2.FeedsBuilder.FeedCutBuilder.Value = 2500.0;

        surfaceContourBuilder2.CutParameters.MaxCutStep.Value = 40.0;

        surfaceContourBuilder2.NonCuttingBuilder.TransferCommonClearanceBuilder.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        surfaceContourBuilder2.NonCuttingBuilder.TransferCommonClearanceBuilder.SafeDistance = 50.0;
        surfaceContourBuilder2.FlowBuilder.SteepCutting.Stepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.Constant;


        NXOpen.NXObject nXObject2;
        nXObject2 = surfaceContourBuilder2.Commit();

        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.SurfaceContour surfaceContour3 = ((NXOpen.CAM.SurfaceContour)nXObject2);
        objects1[0] = surfaceContour3;
        workPart.CAMSetup.GenerateToolPath(objects1);



        surfaceContourBuilder2.Destroy();





    }
    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
}
