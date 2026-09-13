
using NXOpen;
using NXOpen.CAM;
using NXOpen.UF;
using System;


public class CONTOUR_AREA
{
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;
        NXOpen.UI theUI = NXOpen.UI.GetUI();





        NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
        NXOpen.CAM.Method method1 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("MILL_FINISH"));
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));
        NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "mill_contour", "AREA_MILL", NXOpen.CAM.OperationCollection.UseDefaultName.True, "AREA_MILL", "Area Mill");

        NXOpen.CAM.SurfaceContour surfaceContour1 = ((NXOpen.CAM.SurfaceContour)operation1);
        NXOpen.CAM.SurfaceContourBuilder surfaceContourBuilder1;
        surfaceContourBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateSurfaceContourBuilder(surfaceContour1);


        NXOpen.NXObject nXObject1;
        nXObject1 = surfaceContourBuilder1.Commit();

        // ----------------------------------------------
        //   Dialog Begin New Tool
        // ----------------------------------------------
        NXOpen.CAM.NCGroup nCGroup3 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("5-AX_MILL_VERTICAL_AC-TABLE_POST_CONFIGURATOR_METRIC_INCH"));
        NXOpen.CAM.NCGroup nCGroup4;
        nCGroup4 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "mill_contour", "BALL_MILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "BMILLD12MM", "Ball Mill");

        NXOpen.CAM.CAMObject[] objectsToBeMoved1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)nCGroup4);
        objectsToBeMoved1[0] = tool1;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved1, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);
        NXOpen.CAM.MillToolBuilder millToolBuilder1;
        millToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool1);

        millToolBuilder1.TlDiameterBuilder.Value = 12.0;

        millToolBuilder1.TlNumberBuilder.Value = 14;

        millToolBuilder1.TlAdjRegBuilder.Value = 14;

        millToolBuilder1.TlCutcomRegBuilder.Value = 14;

        millToolBuilder1.UseTaperedShank = true;


        NXOpen.NXObject nXObject2;
        nXObject2 = millToolBuilder1.Commit();


        NXOpen.CAM.CAMObject[] objectsToBeMoved2 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.SurfaceContour surfaceContour2 = ((NXOpen.CAM.SurfaceContour)nXObject1);
        objectsToBeMoved2[0] = surfaceContour2;
        NXOpen.CAM.Tool tool2 = ((NXOpen.CAM.Tool)nXObject2);
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved2, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.CAMObject[] objectsToBeMoved3 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved3[0] = surfaceContour2;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved3, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);



        NXOpen.CAM.SurfaceContourBuilder surfaceContourBuilder2;
        surfaceContourBuilder2 = workPart.CAMSetup.CAMOperationCollection.CreateSurfaceContourBuilder(surfaceContour2);
        surfaceContourBuilder2.DmareaMillingBuilder.SteepCutting.DepthPerCut.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.Constant;
        surfaceContourBuilder2.DmareaMillingBuilder.AmSteepOption = NXOpen.CAM.DmAmBuilder.SteepOptTypes.NonSteepNonDirectional;
        surfaceContourBuilder2.DmareaMillingBuilder.CutPatternBuilder.CutPattern = NXOpen.CAM.CutPatternBuilder.Types.ZigZag;

        NXOpen.CAM.CutAngle cutAngle1;
        cutAngle1 = surfaceContourBuilder2.DmareaMillingBuilder.NonSteepCutting.CutAngleBuilder;

        cutAngle1.Type = NXOpen.CAM.CutAngle.Types.Specify;

        cutAngle1.Value = 90.0;

        cutAngle1.Value = 90.0;

        surfaceContourBuilder2.DmareaMillingBuilder.StepoverBuilder.DistanceBuilder.Intent = NXOpen.CAM.ParamValueIntent.PartUnits;

        surfaceContourBuilder2.DmareaMillingBuilder.StepoverBuilder.DistanceBuilder.Value = 0.5;

        surfaceContourBuilder2.FeedsBuilder.SurfaceSpeedBuilder.Value = 90.0;

        NXOpen.Direction nullNXOpen_Direction = null;
        surfaceContourBuilder2.NonCuttingBuilder.TransferAvoidanceFromBuilder.ToolAxis = nullNXOpen_Direction;

        surfaceContourBuilder2.NonCuttingBuilder.TransferAvoidanceGohomeBuilder.ToolAxis = nullNXOpen_Direction;


        surfaceContourBuilder2.NonCuttingBuilder.TransferInitialFinalBuilder.ApproachClearanceBuilder.AxisObject = nullNXOpen_Direction;

        NXOpen.Point3d origin45 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal45 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane45;
        plane45 = workPart.Planes.CreatePlane(origin45, normal45, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane45.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        surfaceContourBuilder2.NonCuttingBuilder.TransferInitialFinalBuilder.DepartureClearanceBuilder.AxisObject = nullNXOpen_Direction;
        surfaceContourBuilder2.FeedsBuilder.RecalculateData(NXOpen.CAM.FeedsBuilder.RecalculateBasedOn.SurfaceSpeed);
        surfaceContourBuilder2.NonCuttingBuilder.TransferAvoidanceFromBuilder.ToolAxis = nullNXOpen_Direction;
        surfaceContourBuilder2.NonCuttingBuilder.TransferAvoidanceGohomeBuilder.ToolAxis = nullNXOpen_Direction;
        surfaceContourBuilder2.NonCuttingBuilder.TransferInitialFinalBuilder.ApproachClearanceBuilder.AxisObject = nullNXOpen_Direction;
        surfaceContourBuilder2.NonCuttingBuilder.TransferInitialFinalBuilder.DepartureClearanceBuilder.AxisObject = nullNXOpen_Direction;
        surfaceContourBuilder2.FeedsBuilder.FeedCutBuilder.Value = 1000.0;
        surfaceContourBuilder2.NonCuttingBuilder.TransferAvoidanceFromBuilder.ToolAxis = nullNXOpen_Direction;
        surfaceContourBuilder2.NonCuttingBuilder.TransferAvoidanceGohomeBuilder.ToolAxis = nullNXOpen_Direction;
        surfaceContourBuilder2.NonCuttingBuilder.TransferInitialFinalBuilder.DepartureClearanceBuilder.AxisObject = nullNXOpen_Direction;

        NXOpen.CAM.ToolAxisFixed toolAxisFixed1;
        toolAxisFixed1 = surfaceContourBuilder2.ToolAxisFixed;

        toolAxisFixed1.ToolAxisType = NXOpen.CAM.ToolAxisFixed.Types.Fixed;

        NXOpen.Point3d origin16 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d vector1 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Direction direction1;
        direction1 = workPart.Directions.CreateDirection(origin16, vector1, NXOpen.SmartObject.UpdateOption.AfterModeling);

        toolAxisFixed1.Vector = direction1;
        toolAxisFixed1.Vector = nullNXOpen_Direction;

        NXOpen.SIM.KinematicConfigurator kinematicConfigurator1; ;
        kinematicConfigurator1 = workPart.KinematicConfigurator;
        NXOpen.Point3d origin17 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d vector2 = new NXOpen.Vector3d(-0.0, -1.0, -0.0);
        NXOpen.Direction direction2;
        direction2 = workPart.Directions.CreateDirection(origin17, vector2, NXOpen.SmartObject.UpdateOption.AfterModeling);

        toolAxisFixed1.Vector = direction2;

        NXOpen.SIM.KinematicConfigurator kinematicConfigurator3;
        kinematicConfigurator3 = workPart.KinematicConfigurator;

        NXOpen.SIM.KinematicConfigurator kinematicConfigurator4;
        kinematicConfigurator4 = workPart.KinematicConfigurator;




        //  DIALOG BEGIN CUT AREA
        // Selecionar a face
        Face myface = SelectAnyFace();

        NXOpen.TaggedObject taggedObject = surfaceContourBuilder2.GetCustomizableItemBuilder(" Specify - Cut Area");
        NXOpen.CAM.Geometry geometry = (NXOpen.CAM.Geometry)taggedObject;
        geometry.InitializeData(false);

        NXOpen.CAM.GeometrySetList geometrySetList = geometry.GeometryList;
        NXOpen.CAM.GeometrySet geometrySet = (NXOpen.CAM.GeometrySet)geometrySetList.FindItem(0);

        NXOpen.ScCollector scCollector = geometrySet.ScCollector;

        // ============================
        // FACE DUMB RULE
        // ============================

        NXOpen.SelectionIntentRuleOptions ruleOptions1 = workPart.ScRuleFactory.CreateRuleOptions();
        ruleOptions1.SetSelectedFromInactive(false);

        NXOpen.Face[] faces = new NXOpen.Face[1];
        faces[0] = myface;

        NXOpen.FaceDumbRule faceDumbRule = workPart.ScRuleFactory.CreateRuleFaceDumb(faces, ruleOptions1);

        ruleOptions1.Dispose();

        NXOpen.SelectionIntentRule[] rules1 = new NXOpen.SelectionIntentRule[1];
        rules1[0] = faceDumbRule;

        scCollector.ReplaceRules(rules1, false);

        // ============================
        // FACE TANGENT RULE
        // ============================

        NXOpen.SelectionIntentRuleOptions ruleOptions2 = workPart.ScRuleFactory.CreateRuleOptions();
        ruleOptions2.SetSelectedFromInactive(false);

        NXOpen.Face[] boundaryFaces = new NXOpen.Face[0];

        NXOpen.FaceTangentRule faceTangentRule = workPart.ScRuleFactory.CreateRuleFaceTangent(
            myface,
            boundaryFaces,
            20.0,
            ruleOptions2
        );

        ruleOptions2.Dispose();

        NXOpen.SelectionIntentRule[] rules2 = new NXOpen.SelectionIntentRule[1];
        rules2[0] = faceTangentRule;

        scCollector.ReplaceRules(rules2, false);

        // Commit da operação
        NXOpen.NXObject nXObject;
        nXObject = surfaceContourBuilder1.Commit();

        // END DIALOG CUT AREA



        // ----------------------------------------------  
        //   Dialog Begin Area Mill - [AREA_MILL]
        // ----------------------------------------------
        NXOpen.NXObject nXObject3;
        nXObject3 = surfaceContourBuilder2.Commit();

        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.SurfaceContour surfaceContour3 = ((NXOpen.CAM.SurfaceContour)nXObject3);
        objects1[0] = surfaceContour3;
        workPart.CAMSetup.GenerateToolPath(objects1);

        surfaceContourBuilder2.Destroy();


        NXOpen.CAM.SurfaceContourBuilder surfaceContourBuilder3;
        surfaceContourBuilder3 = workPart.CAMSetup.CAMOperationCollection.CreateSurfaceContourBuilder(surfaceContour3);


        surfaceContourBuilder3.DmareaMillingBuilder.SteepCutting.DepthPerCut.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.Constant;

        NXOpen.NXObject nXObject4;
        nXObject4 = surfaceContourBuilder3.Commit();

        surfaceContourBuilder3.Destroy();

        theUI.NXMessageBox.Show("Area Mill", NXOpen.NXMessageBox.DialogType.Information, "Please, select surface to be machinned and vector direction.");



    }
    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }


    static Face SelectAnyFace()
    {
        Session theSession = Session.GetSession();
        UI theUI = UI.GetUI();
        theSession.ListingWindow.WriteLine("Select Surface to be Machinned");

        Selection.MaskTriple mask = new Selection.MaskTriple(
            UFConstants.UF_solid_type, 0, UFConstants.UF_UI_SEL_FEATURE_ANY_FACE);

        TaggedObject obj;
        Point3d cursor;

        Selection.Response resp = theUI.SelectionManager.SelectTaggedObject(
            "Select face", " Face",
            Selection.SelectionScope.WorkPart,
            Selection.SelectionAction.ClearAndEnableSpecific,
            false, false,
            new[] { mask }, out obj, out cursor);

        return (Face)obj;
    }
}
