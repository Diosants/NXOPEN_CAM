
using NXOpen;
using System;


public class M10_SOCKET_HEAD
{
    public static void Run(string[] args)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        Part displayPart = theSession.Parts.Display;
        UI theUI =UI.GetUI();

        //   Menu: Insert->Operation...


        NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
        NXOpen.CAM.Method method1 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("DRILL_METHOD"));
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));
        NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("M10_SOCKET_HEAD"));

        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "hole_making", "SPOT_DRILLING", NXOpen.CAM.OperationCollection.UseDefaultName.False, "CENTER-DRILL", "Spot Drilling");



        NXOpen.CAM.HoleDrilling holeDrilling1 = ((NXOpen.CAM.HoleDrilling)operation1);
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder1;
        holeDrillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling1);


        NXOpen.NXObject nXObject1;
        nXObject1 = holeDrillingBuilder1.Commit();

        // ----------------------------------------------
        //   Dialog Begin New Tool
        // ----------------------------------------------
        NXOpen.CAM.NCGroup nCGroup3 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("GENERIC_MACHINE"));
        NXOpen.CAM.NCGroup nCGroup4;
        nCGroup4 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "hole_making", "CENTERDRILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.True, "CENTERDRILL", "Centerdrill");

        NXOpen.CAM.CAMObject[] objectsToBeMoved1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)nCGroup4);
        objectsToBeMoved1[0] = tool1;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved1, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);


        NXOpen.CAM.DrillCenterBellToolBuilder drillCenterBellToolBuilder1;
        drillCenterBellToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateDrillCenterBellToolBuilder(tool1);


        // ----------------------------------------------
        //   Dialog Begin Centerdrill
        // ----------------------------------------------
        drillCenterBellToolBuilder1.TlNumberBuilder.Value = 10;

        drillCenterBellToolBuilder1.TlAdjRegBuilder.Value = 10;

        drillCenterBellToolBuilder1.UseTaperedShank = true;

        drillCenterBellToolBuilder1.ShankSectionBuilder.Delete(0);

        drillCenterBellToolBuilder1.ShankSectionBuilder.Modify(0, 50.0, 37.5, -26.869813087499079, 0.0);

        drillCenterBellToolBuilder1.ShankSectionBuilder.Modify(0, 50.0, 50.0, -20.806791012711241, 0.0);

        drillCenterBellToolBuilder1.ShankSectionBuilder.Modify(0, 50.0, 50.0, 0.0, 0.0);

        NXOpen.NXObject nXObject2;
        nXObject2 = drillCenterBellToolBuilder1.Commit();
        drillCenterBellToolBuilder1.Destroy();


        theSession.CAMSession.Utils.SetInspectionIntent(false);

        NXOpen.CAM.CAMObject[] objectsToBeMoved2 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling2 = ((NXOpen.CAM.HoleDrilling)nXObject1);
        objectsToBeMoved2[0] = holeDrilling2;
        NXOpen.CAM.Tool tool2 = ((NXOpen.CAM.Tool)nXObject2);
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved2, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.CAMObject[] objectsToBeMoved3 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved3[0] = holeDrilling2;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved3, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);


        holeDrillingBuilder1.Destroy();

        ///  CONTINUAR DAQUI

        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder2;
        holeDrillingBuilder2 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling2);
        holeDrillingBuilder2.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;
        holeDrillingBuilder2.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;
        holeDrillingBuilder2.CycleTable.MotionOutput = NXOpen.CAM.Cycle.MotionOutputTypes.SingleMoves;
        holeDrillingBuilder2.NonCuttingBuilder.TransferClearance.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
        holeDrillingBuilder2.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;
        holeDrillingBuilder2.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;
        holeDrillingBuilder2.FeedsBuilder.SpindleRpmBuilder.Value = 1500.0;
        holeDrillingBuilder2.FeedsBuilder.FeedCutBuilder.Value = 150.0;
        holeDrillingBuilder2.CuttingParameters.TopOffset.Distance = 1.0;
        holeDrillingBuilder2.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;

        //  update after modeling
        NXOpen.Plane plane1 = Getplane(workPart, 0, 0, 0, 0, 0, 1);

        NXOpen.NXObject nXObject3;
        nXObject3 = holeDrillingBuilder2.Commit();

        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling3 = ((NXOpen.CAM.HoleDrilling)nXObject3);
        objects1[0] = holeDrilling3;
        workPart.CAMSetup.GenerateToolPath(objects1);

        holeDrillingBuilder2.Destroy();



        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder3;
        holeDrillingBuilder3 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling3);
        holeDrillingBuilder3.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;
        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters3;
        holeMachiningCutParameters3 = holeDrillingBuilder3.CuttingParameters;
        NXOpen.Plane plane2 = Getplane(workPart, 0, 0, 0, 0, 0, 1);

        NXOpen.NXObject nXObject4;
        nXObject4 = holeDrillingBuilder3.Commit();

        holeDrillingBuilder3.Destroy();

        //  OPERATION TWO  DRILLING
        NXOpen.CAM.Operation operation2;
        operation2 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "hole_making", "DRILLING", NXOpen.CAM.OperationCollection.UseDefaultName.False, "DRILL-D11MM", "Drilling");


        NXOpen.CAM.HoleDrilling holeDrilling4 = ((NXOpen.CAM.HoleDrilling)operation2);
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder4;
        holeDrillingBuilder4 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling4);
        holeDrillingBuilder4.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters4;
        holeMachiningCutParameters4 = holeDrillingBuilder4.CuttingParameters;
        NXOpen.Plane plane3 = Getplane(workPart, 0, 0, 0, 0, 0, 1);


        NXOpen.NXObject nXObject5;
        nXObject5 = holeDrillingBuilder4.Commit();

        NXOpen.CAM.NCGroup nCGroup5;
        nCGroup5 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "hole_making", "SPOT_DRILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "HSS-DRILL-11MM", "Spot Drill");
        NXOpen.CAM.CAMObject[] objectsToBeMoved4 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool3 = ((NXOpen.CAM.Tool)nCGroup5);
        objectsToBeMoved4[0] = tool3;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved4, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);


        //  SPOTDRILL BUILDER
        NXOpen.CAM.DrillSpotdrillToolBuilder drillSpotdrillToolBuilder1;
        drillSpotdrillToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateDrillSpotdrillToolBuilder(tool3);
        drillSpotdrillToolBuilder1.TlDiameterBuilder.Value = 11;
        drillSpotdrillToolBuilder1.TlNumberBuilder.Value = 11;
        drillSpotdrillToolBuilder1.TlAdjRegBuilder.Value = 11;
        drillSpotdrillToolBuilder1.TlHeightBuilder.Value = 60.0;
        drillSpotdrillToolBuilder1.UseTaperedShank = true;
        drillSpotdrillToolBuilder1.ShankSectionBuilder.Delete(0);

        NXOpen.NXObject nXObject6;
        nXObject6 = drillSpotdrillToolBuilder1.Commit();
        drillSpotdrillToolBuilder1.Destroy();

        NXOpen.CAM.CAMObject[] objectsToBeMoved5 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling5 = ((NXOpen.CAM.HoleDrilling)nXObject5);
        objectsToBeMoved5[0] = holeDrilling5;
        NXOpen.CAM.Tool tool4 = ((NXOpen.CAM.Tool)nXObject6);
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved5, tool4, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.CAMObject[] objectsToBeMoved6 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved6[0] = holeDrilling5;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved6, tool4, NXOpen.CAM.CAMSetup.Paste.Inside);


        holeDrillingBuilder4.Destroy();


        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder5;
        holeDrillingBuilder5 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling5);
        holeDrillingBuilder5.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters5;
        holeMachiningCutParameters5 = holeDrillingBuilder5.CuttingParameters;
        NXOpen.Plane plane4 = Getplane(workPart, 0, 0, 0, 0, 0, 1);


        NXOpen.CAM.FBM.FeatureGeometry featureGeometry100;
        featureGeometry100 = holeDrillingBuilder5.GetFeatureGeometry();

        NXOpen.CAM.FBM.MachiningFeatureGeometry machiningFeatureGeometry1 = ((NXOpen.CAM.FBM.MachiningFeatureGeometry)featureGeometry100);
        NXOpen.CAM.GeometrySetList geometrySetList1;
        geometrySetList1 = machiningFeatureGeometry1.GeometryList;



        NXOpen.TaggedObject taggedObject1;
        taggedObject1 = geometrySetList1.FindItem(0);

        NXOpen.CAM.FBM.FeatureSet featureSet1 = ((NXOpen.CAM.FBM.FeatureSet)taggedObject1);

        machiningFeatureGeometry1.UseModelDepth = true;




        holeDrillingBuilder5.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;
        holeDrillingBuilder5.CycleTable.MotionOutput = NXOpen.CAM.Cycle.MotionOutputTypes.SingleMoves;  //  DEFINES EITHER SINGLE MOVES OR MACHINE CICLE
        holeDrillingBuilder5.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;
        holeDrillingBuilder5.CycleTable.CycleType = "Drilling with chip removal";
        holeDrillingBuilder5.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.Constant;
        holeDrillingBuilder5.CycleTable.StepClearance = 1.0;
        holeDrillingBuilder5.CycleTable.AxialStepover.DistanceBuilder.Value = 1.0;

        NXOpen.Plane plane5 = Getplane(workPart, 0, 0, 0, 0, 0, 1);


        NXOpen.NXObject nXObject7;
        nXObject7 = holeDrillingBuilder5.Commit();
        holeDrillingBuilder5.Destroy();

        NXOpen.CAM.HoleDrilling holeDrilling6 = ((NXOpen.CAM.HoleDrilling)nXObject7);
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder6;
        holeDrillingBuilder6 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling6);


        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters7;
        holeMachiningCutParameters7 = holeDrillingBuilder6.CuttingParameters;
        NXOpen.Plane plane6 = Getplane(workPart, 0, 0, 0, 0, 0, 1);
        holeDrillingBuilder6.FeedsBuilder.SurfaceSpeedBuilder.Value = 25.0;
        NXOpen.Plane plane7 = Getplane(workPart, 0, 0, 0, 0, 0, 1);
        holeDrillingBuilder6.FeedsBuilder.RecalculateData(NXOpen.CAM.FeedsBuilder.RecalculateBasedOn.SurfaceSpeed);
        NXOpen.Plane plane8 = Getplane(workPart, 0, 0, 0, 0, 0, 1);
        holeDrillingBuilder6.FeedsBuilder.FeedPerToothBuilder.Value = 0.10000000000000001;
        NXOpen.Plane plane9 = Getplane(workPart, 0, 0, 0, 0, 0, 1);
        holeDrillingBuilder6.FeedsBuilder.RecalculateData(NXOpen.CAM.FeedsBuilder.RecalculateBasedOn.FeedPerTooth);
        NXOpen.Plane plane10 = Getplane(workPart, 0, 0, 0, 0, 0, 1);

        holeDrillingBuilder6.NonCuttingBuilder.TransferClearance.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        holeDrillingBuilder6.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;

        holeDrillingBuilder6.NonCuttingBuilder.TransferBetweenRegions.Type = NXOpen.CAM.NcmTransfer.TransferTypes.ShortestToClearance;

        NXOpen.NXObject nXObject8;
        nXObject8 = holeDrillingBuilder6.Commit();

        NXOpen.CAM.CAMObject[] objects2 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling7 = ((NXOpen.CAM.HoleDrilling)nXObject8);
        objects2[0] = holeDrilling7;
        workPart.CAMSetup.GenerateToolPath(objects2);

        holeDrillingBuilder6.Destroy();


        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder7;
        holeDrillingBuilder7 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling7);
        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters8;
        holeMachiningCutParameters8 = holeDrillingBuilder7.CuttingParameters;
        NXOpen.Plane plane11 = Getplane(workPart, 0, 0, 0, 0, 0, 1);

        NXOpen.NXObject nXObject9;
        nXObject9 = holeDrillingBuilder7.Commit();
        holeDrillingBuilder7.Destroy();



        NXOpen.CAM.Method method2 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("METHOD"));
        NXOpen.CAM.Operation operation3;
        operation3 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method2, nCGroup2, featureGeometry1, "hole_making", "HOLE_MILLING", NXOpen.CAM.OperationCollection.UseDefaultName.False, "HOLE_MILL-D18MM", "Hole Milling");
        ;

        NXOpen.CAM.CylinderMilling cylinderMilling1 = ((NXOpen.CAM.CylinderMilling)operation3);
        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder1;
        cylinderMillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling1);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters9;
        holeMachiningCutParameters9 = cylinderMillingBuilder1.CuttingParameters;

        NXOpen.Plane plane12 = Getplane(workPart, 0, 0, 0, 0, 0, 1);


        NXOpen.NXObject nXObject10;
        nXObject10 = cylinderMillingBuilder1.Commit();

        NXOpen.CAM.NCGroup nCGroup6;
        nCGroup6 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "mill_planar", "MILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "ENDMILL-D8MM", "Mill");

        NXOpen.CAM.CAMObject[] objectsToBeMoved7 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool5 = ((NXOpen.CAM.Tool)nCGroup6);
        objectsToBeMoved7[0] = tool5;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved7, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);


        // TOOL  BUILDER

        NXOpen.CAM.MillToolBuilder millToolBuilder1;
        millToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool5);

        millToolBuilder1.TlDiameterBuilder.Value = 8;

        millToolBuilder1.TlNumberBuilder.Value = 8;

        millToolBuilder1.TlAdjRegBuilder.Value = 8;

        millToolBuilder1.TlCutcomRegBuilder.Value = 8;

        millToolBuilder1.TlHeightBuilder.Value = 50.0;

        millToolBuilder1.UseTaperedShank = true;

        NXOpen.NXObject nXObject11;
        nXObject11 = millToolBuilder1.Commit();
        millToolBuilder1.Destroy();



        NXOpen.CAM.CAMObject[] objectsToBeMoved8 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CylinderMilling cylinderMilling2 = ((NXOpen.CAM.CylinderMilling)nXObject10);
        objectsToBeMoved8[0] = cylinderMilling2;
        NXOpen.CAM.Tool tool6 = ((NXOpen.CAM.Tool)nXObject11);
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved8, tool6, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.CAMObject[] objectsToBeMoved9 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved9[0] = cylinderMilling2;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved9, tool6, NXOpen.CAM.CAMSetup.Paste.Inside);

        cylinderMillingBuilder1.Destroy();


        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder2;
        cylinderMillingBuilder2 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling2);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters10;
        holeMachiningCutParameters10 = cylinderMillingBuilder2.CuttingParameters;

        cylinderMillingBuilder2.FeedsBuilder.SurfaceSpeedBuilder.Value = 60.0;
        cylinderMillingBuilder2.FeedsBuilder.RecalculateData(NXOpen.CAM.FeedsBuilder.RecalculateBasedOn.SurfaceSpeed);
        cylinderMillingBuilder2.FeedsBuilder.FeedCutBuilder.Value = 600.0;
        NXOpen.Plane plane13 = Getplane(workPart, 0, 0, 0, 0, 0, 1);

        NXOpen.CAM.InheritableToolDepBuilder inheritableToolDepBuilder1;
        inheritableToolDepBuilder1 = cylinderMillingBuilder2.AxialDistance;

        inheritableToolDepBuilder1.Value = 100.0;

        cylinderMillingBuilder2.AxialStepover.DistanceBuilder.Value = 100.0; //  AXIAL  DISTANCE

        cylinderMillingBuilder2.RadialStepover.DistanceBuilder.Value = 10.0;  // RADIAL DISTANCE

        cylinderMillingBuilder2.CuttingParameters.TopOffset.Distance = 1.0; // TOP OFFSET

        cylinderMillingBuilder2.NonCuttingBuilder.Engage.EngRetType = NXOpen.CAM.NcmHoleMachiningEngRet.EngRetTypes.Linear;

        cylinderMillingBuilder2.NonCuttingBuilder.TransferClearance.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        cylinderMillingBuilder2.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;
        cylinderMillingBuilder2.CutPattern = NXOpen.CAM.CylinderMillingBuilder.CutPatternTypes.Spiral;

        NXOpen.NXObject nXObject12;
        nXObject12 = cylinderMillingBuilder2.Commit();

        NXOpen.CAM.CAMObject[] objects3 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CylinderMilling cylinderMilling3 = ((NXOpen.CAM.CylinderMilling)nXObject12);
        objects3[0] = cylinderMilling3;
        workPart.CAMSetup.GenerateToolPath(objects3);

        cylinderMillingBuilder2.Destroy();


        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder3;
        cylinderMillingBuilder3 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling3);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters11;
        holeMachiningCutParameters11 = cylinderMillingBuilder3.CuttingParameters;
        NXOpen.Plane plane14 = Getplane(workPart, 0, 0, 0, 0, 0, 1);


        cylinderMillingBuilder3.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.PercentFluteLength;

        NXOpen.NXObject nXObject13;
        nXObject13 = cylinderMillingBuilder3.Commit();

        NXOpen.CAM.CAMObject[] objects4 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CylinderMilling cylinderMilling4 = ((NXOpen.CAM.CylinderMilling)nXObject13);
        objects4[0] = cylinderMilling4;
        workPart.CAMSetup.GenerateToolPath(objects4);


        cylinderMillingBuilder3.Destroy();

        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder4;
        cylinderMillingBuilder4 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling4);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters12;
        holeMachiningCutParameters12 = cylinderMillingBuilder4.CuttingParameters;
        NXOpen.Plane plane15 = Getplane(workPart, 0, 0, 0, 0, 0, 1);
        NXOpen.CAM.CylinderMillingCutParameters cylinderMillingCutParameters4 = ((NXOpen.CAM.CylinderMillingCutParameters)holeMachiningCutParameters12);
        NXOpen.CAM.VerticalPosition verticalPosition18;
        verticalPosition18 = cylinderMillingCutParameters4.BottomOffset;


        NXOpen.NXObject nXObject14;
        nXObject14 = cylinderMillingBuilder4.Commit();
        cylinderMillingBuilder4.Destroy();


        NXOpen.CAM.Method method3 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("MILL_FINISH"));
        NXOpen.CAM.Operation operation4;
        operation4 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method3, nCGroup2, featureGeometry1, "hole_making", "HOLE_CHAMFER_MILLING", NXOpen.CAM.OperationCollection.UseDefaultName.False, "HOLE_CHAMFER_D19", "Hole Chamfer Milling");


        NXOpen.CAM.ChamferMilling chamferMilling1 = ((NXOpen.CAM.ChamferMilling)operation4);
        NXOpen.CAM.ChamferMillingBuilder chamferMillingBuilder1;
        chamferMillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateChamferMillingBuilder(chamferMilling1);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters13;
        holeMachiningCutParameters13 = chamferMillingBuilder1.CuttingParameters;
        NXOpen.Plane plane16 = Getplane(workPart, 0, 0, 0, 0, 0, 1);
        chamferMillingBuilder1.ChamferReference = NXOpen.CAM.ChamferMillingBuilder.ChamferReferenceType.MinDiameter;


        NXOpen.NXObject nXObject15;
        nXObject15 = chamferMillingBuilder1.Commit();

        NXOpen.CAM.NCGroup nCGroup7;
        nCGroup7 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "hole_making", "CHAMFER_MILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "CHAMFER_D8MM", "Chamfer Mill");

        NXOpen.CAM.CAMObject[] objectsToBeMoved10 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool7 = ((NXOpen.CAM.Tool)nCGroup7);
        objectsToBeMoved10[0] = tool7;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved10, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);


        NXOpen.CAM.MillToolBuilder millToolBuilder2;
        millToolBuilder2 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool7);

        millToolBuilder2.TlDiameterBuilder.Value = 8.0;


        millToolBuilder2.TlNumberBuilder.Value = 15;

        millToolBuilder2.TlAdjRegBuilder.Value = 15;

        millToolBuilder2.TlCutcomRegBuilder.Value = 15;

        millToolBuilder2.UseTaperedShank = true;


        NXOpen.NXObject nXObject16;
        nXObject16 = millToolBuilder2.Commit();
        millToolBuilder2.Destroy();

        NXOpen.CAM.CAMObject[] objectsToBeMoved11 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.ChamferMilling chamferMilling2 = ((NXOpen.CAM.ChamferMilling)nXObject15);
        objectsToBeMoved11[0] = chamferMilling2;
        NXOpen.CAM.Tool tool8 = ((NXOpen.CAM.Tool)nXObject16);
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved11, tool8, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.CAMObject[] objectsToBeMoved12 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved12[0] = chamferMilling2;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved12, tool8, NXOpen.CAM.CAMSetup.Paste.Inside);


        chamferMillingBuilder1.Destroy();


        NXOpen.CAM.ChamferMillingBuilder chamferMillingBuilder2;
        chamferMillingBuilder2 = workPart.CAMSetup.CAMOperationCollection.CreateChamferMillingBuilder(chamferMilling2);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters14;
        holeMachiningCutParameters14 = chamferMillingBuilder2.CuttingParameters;
        chamferMillingBuilder2.ChamferReference = NXOpen.CAM.ChamferMillingBuilder.ChamferReferenceType.MinDiameter;
        chamferMillingBuilder2.FeedsBuilder.SpindleRpmBuilder.Value = 6000.0;
        chamferMillingBuilder2.FeedsBuilder.FeedCutBuilder.Value = 800.0;
        chamferMillingBuilder2.CuttingParameters.TopOffset.Distance = 1.0;
        chamferMillingBuilder2.NonCuttingBuilder.TransferClearance.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
        chamferMillingBuilder2.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;
        chamferMillingBuilder2.ChamferReference = NXOpen.CAM.ChamferMillingBuilder.ChamferReferenceType.MinDiameter;
        chamferMillingBuilder2.NonCuttingBuilder.InitialType = NXOpen.CAM.NcmHoleMachining.InitialTypes.ClearanceShortestDistance;
        NXOpen.Plane plane17 = Getplane(workPart, 0, 0, 0, 0, 0, 1);

        chamferMillingBuilder2.DrivePoint = "SYS_OD_CHAMFER";
        chamferMillingBuilder2.CollisionCheck = false;

        chamferMillingBuilder2.GougeChecking = false;

        chamferMillingBuilder2.NonCuttingBuilder.CollisionCheck = false;

        chamferMillingBuilder2.ChamferReference = NXOpen.CAM.ChamferMillingBuilder.ChamferReferenceType.MinDiameter;

        NXOpen.NXObject nXObject17;
        nXObject17 = chamferMillingBuilder2.Commit();

        NXOpen.CAM.CAMObject[] objects5 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.ChamferMilling chamferMilling3 = ((NXOpen.CAM.ChamferMilling)nXObject17);
        objects5[0] = chamferMilling3;
        workPart.CAMSetup.GenerateToolPath(objects5);
        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters15;
        holeMachiningCutParameters15 = chamferMillingBuilder2.CuttingParameters;
        NXOpen.Plane plane18 = Getplane(workPart, 0, 0, 0, 0, 0, 1);

        chamferMillingBuilder2.Commit();
        chamferMillingBuilder2.Destroy();
        ;


    }

    //  FUNCAO CRIAR PLANO
    public static NXOpen.Plane Getplane(Part workPart, double x, double y, double z, double nx, double ny, double nz)
    {
        NXOpen.Point3d origen = new NXOpen.Point3d(x, y, z);
        NXOpen.Vector3d normal = new NXOpen.Vector3d(nx, ny, nz);
        NXOpen.Plane plane = workPart.Planes.CreatePlane(origen, normal, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);
        return plane;
    }
    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
}
