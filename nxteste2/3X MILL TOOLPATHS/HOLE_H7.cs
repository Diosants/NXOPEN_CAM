
using System;
using NXOpen;

public class HOLE_H7
{
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;

        //  INSERT OPERATION - HOLE-H7
        NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
        NXOpen.CAM.Method method1 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("DRILL_METHOD"));
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));
        NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("HOLE-MILLING-H7"));
        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "hole_making", "SPOT_DRILLING", NXOpen.CAM.OperationCollection.UseDefaultName.True, "SPOT_DRILLING", "Spot Drilling");

  

        NXOpen.CAM.HoleDrilling holeDrilling1 = ((NXOpen.CAM.HoleDrilling)operation1);
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder1;
        holeDrillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling1);

       

        holeDrillingBuilder1.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters1;
        holeMachiningCutParameters1 = holeDrillingBuilder1.CuttingParameters;

        NXOpen.Point3d origin1 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal1 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane1;
        plane1 = workPart.Planes.CreatePlane(origin1, normal1, NXOpen.SmartObject.UpdateOption.AfterModeling);

        holeDrillingBuilder1.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;

        holeDrillingBuilder1.NonCuttingBuilder.TransferClearance.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        holeDrillingBuilder1.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;

        NXOpen.NXObject nXObject1;
        nXObject1 = holeDrillingBuilder1.Commit();
        // GETTING CENTERDRILL TOOL
        NXOpen.CAM.NCGroup nCGroup3 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("GENERIC_MACHINE"));
        NXOpen.CAM.NCGroup nCGroup4;
        nCGroup4 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "hole_making", "CENTERDRILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.True, "CENTERDRILL", "Centerdrill");

        NXOpen.CAM.CAMObject[] objectsToBeMoved1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)nCGroup4);
        objectsToBeMoved1[0] = tool1;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved1, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);

        // BUILDER TO CREATE TOOL
        NXOpen.CAM.DrillCenterBellToolBuilder drillCenterBellToolBuilder1;
        drillCenterBellToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateDrillCenterBellToolBuilder(tool1);

        drillCenterBellToolBuilder1.TlNumberBuilder.Value = 10;

        drillCenterBellToolBuilder1.TlAdjRegBuilder.Value = 10;

        NXOpen.NXObject nXObject2;
        nXObject2 = drillCenterBellToolBuilder1.Commit();
        drillCenterBellToolBuilder1.Destroy();


        NXOpen.CAM.CAMObject[] objectsToBeMoved2 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling2 = ((NXOpen.CAM.HoleDrilling)nXObject1);
        objectsToBeMoved2[0] = holeDrilling2;
        NXOpen.CAM.Tool tool2 = ((NXOpen.CAM.Tool)nXObject2);
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved2, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.CAMObject[] objectsToBeMoved3 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved3[0] = holeDrilling2;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved3, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);

        // END OF BUILDER TO CREATE TOOL


        holeDrillingBuilder1.Destroy();

        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder2;
        holeDrillingBuilder2 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling2);

      

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters2;
        holeMachiningCutParameters2 = holeDrillingBuilder2.CuttingParameters;

        NXOpen.Point3d origin2 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal2 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane2;
        plane2 = workPart.Planes.CreatePlane(origin2, normal2, NXOpen.SmartObject.UpdateOption.AfterModeling);

        holeDrillingBuilder2.FeedsBuilder.SpindleRpmBuilder.Value = 1500.0;

        NXOpen.Point3d origin3 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal3 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane3;
        plane3 = workPart.Planes.CreatePlane(origin3, normal3, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane3.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        holeDrillingBuilder2.FeedsBuilder.FeedCutBuilder.Value = 150.0;

        NXOpen.Point3d origin4 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal4 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane4;
        plane4 = workPart.Planes.CreatePlane(origin4, normal4, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane4.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        NXOpen.NXObject nXObject3;
        nXObject3 = holeDrillingBuilder2.Commit();

        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling3 = ((NXOpen.CAM.HoleDrilling)nXObject3);
        objects1[0] = holeDrilling3;
        workPart.CAMSetup.GenerateToolPath(objects1);
        holeDrillingBuilder2.Destroy();

        //  END OF  CENTER DRILL OPERATION



           //  HOLE DRILLING BUILDER 3
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder3;
        holeDrillingBuilder3 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling3);

      

        NXOpen.Point3d origin5 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal5 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane5;
        plane5 = workPart.Planes.CreatePlane(origin5, normal5, NXOpen.SmartObject.UpdateOption.AfterModeling);

        NXOpen.NXObject nXObject4;
        nXObject4 = holeDrillingBuilder3.Commit();

        holeDrillingBuilder3.Destroy();

     
          // OPERATION 2 DRILLING
        NXOpen.CAM.Operation operation2;
        operation2 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "hole_making", "DRILLING", NXOpen.CAM.OperationCollection.UseDefaultName.True, "DRILLING", "Drilling");

        NXOpen.CAM.HoleDrilling holeDrilling4 = ((NXOpen.CAM.HoleDrilling)operation2);
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder4;
        holeDrillingBuilder4 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling4);

        NXOpen.NXObject nXObject5;
        nXObject5 = holeDrillingBuilder4.Commit();

         // GETTINF DRILLING TOOL DIAM 20MM
        NXOpen.CAM.NCGroup nCGroup5;
        nCGroup5 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "hole_making", "SPOT_DRILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "DRILL-D20MM", "Spot Drill");

        NXOpen.CAM.CAMObject[] objectsToBeMoved4 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool3 = ((NXOpen.CAM.Tool)nCGroup5);
        objectsToBeMoved4[0] = tool3;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved4, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);


        NXOpen.CAM.DrillSpotdrillToolBuilder drillSpotdrillToolBuilder1;
        drillSpotdrillToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateDrillSpotdrillToolBuilder(tool3);

        drillSpotdrillToolBuilder1.TlDiameterBuilder.Value = 20.0;

        drillSpotdrillToolBuilder1.TlNumberBuilder.Value = 11;

        drillSpotdrillToolBuilder1.TlAdjRegBuilder.Value = 11;

        drillSpotdrillToolBuilder1.UseTaperedShank = true;

        drillSpotdrillToolBuilder1.ShankSectionBuilder.Delete(0);

        drillSpotdrillToolBuilder1.ShankSectionBuilder.Modify(0, 50.0, 75.0, -13.13402230639633, 0.0);

        drillSpotdrillToolBuilder1.ShankSectionBuilder.Modify(0, 50.0, 50.0, -19.290046219188742, 0.0);

        drillSpotdrillToolBuilder1.ShankSectionBuilder.Modify(0, 50.0, 50.0, 0.0, 0.0);
        

        NXOpen.NXObject nXObject6;
        nXObject6 = drillSpotdrillToolBuilder1.Commit();
        drillSpotdrillToolBuilder1.Destroy();
        theSession.CAMSession.Utils.SetInspectionIntent(false);

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

        holeDrillingBuilder5.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.Constant;


        NXOpen.CAM.CycleTipRelease cycleTipRelease6;
        cycleTipRelease6 = holeDrillingBuilder5.CycleTable.TipRelease;

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters6;
        holeMachiningCutParameters6 = holeDrillingBuilder5.CuttingParameters;

        holeDrillingBuilder5.CycleTable.AxialStepover.DistanceBuilder.Value = 1.0;

        theSession.CAMSession.Utils.SetInspectionIntent(false);

        NXOpen.Point3d origin8 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal8 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane8;
        plane8 = workPart.Planes.CreatePlane(origin8, normal8, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane8.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        NXOpen.NXObject nXObject7;
        nXObject7 = holeDrillingBuilder5.Commit();

        holeDrillingBuilder5.Destroy();

    

        NXOpen.CAM.HoleDrilling holeDrilling6 = ((NXOpen.CAM.HoleDrilling)nXObject7);
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder6;
        holeDrillingBuilder6 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling6);

     
        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters7;
        holeMachiningCutParameters7 = holeDrillingBuilder6.CuttingParameters;

        NXOpen.Point3d origin9 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal9 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane9;
        plane9 = workPart.Planes.CreatePlane(origin9, normal9, NXOpen.SmartObject.UpdateOption.AfterModeling);

        holeDrillingBuilder6.FeedsBuilder.SurfaceSpeedBuilder.Value = 25.0;

        NXOpen.Point3d origin10 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal10 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane10;
        plane10 = workPart.Planes.CreatePlane(origin10, normal10, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane10.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        holeDrillingBuilder6.FeedsBuilder.RecalculateData(NXOpen.CAM.FeedsBuilder.RecalculateBasedOn.SurfaceSpeed);

        NXOpen.Point3d origin11 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal11 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane11;
        plane11 = workPart.Planes.CreatePlane(origin11, normal11, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane11.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        holeDrillingBuilder6.FeedsBuilder.FeedPerToothBuilder.Value = 0.1;

        NXOpen.Point3d origin12 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal12 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane12;
        plane12 = workPart.Planes.CreatePlane(origin12, normal12, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane12.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        holeDrillingBuilder6.FeedsBuilder.RecalculateData(NXOpen.CAM.FeedsBuilder.RecalculateBasedOn.FeedPerTooth);

        NXOpen.Point3d origin13 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal13 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane13;
        plane13 = workPart.Planes.CreatePlane(origin13, normal13, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane13.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        holeDrillingBuilder6.NonCuttingBuilder.TransferClearance.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        holeDrillingBuilder6.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;

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

        NXOpen.Point3d origin14 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal14 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane14;
        plane14 = workPart.Planes.CreatePlane(origin14, normal14, NXOpen.SmartObject.UpdateOption.AfterModeling);

        NXOpen.NXObject nXObject9;
        nXObject9 = holeDrillingBuilder7.Commit();

        holeDrillingBuilder7.Destroy();


        theSession.CAMSession.Utils.SetInspectionIntent(false);

    
        NXOpen.CAM.Method method2 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("METHOD"));
        NXOpen.CAM.Operation operation3;
        operation3 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method2, nCGroup2, featureGeometry1, "hole_making", "HOLE_MILLING", NXOpen.CAM.OperationCollection.UseDefaultName.False, "HOLE_MILLING-ROUGH", "Hole Milling");


        NXOpen.CAM.CylinderMilling cylinderMilling1 = ((NXOpen.CAM.CylinderMilling)operation3);
        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder1;
        cylinderMillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling1);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters9;
        holeMachiningCutParameters9 = cylinderMillingBuilder1.CuttingParameters;

        NXOpen.Point3d origin15 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal15 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane15;
        plane15 = workPart.Planes.CreatePlane(origin15, normal15, NXOpen.SmartObject.UpdateOption.AfterModeling);
        NXOpen.NXObject nXObject10;
        nXObject10 = cylinderMillingBuilder1.Commit();


        NXOpen.CAM.NCGroup nCGroup6;
        nCGroup6 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "mill_planar", "MILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "ENDMILLD12", "Mill");

        NXOpen.CAM.CAMObject[] objectsToBeMoved7 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool5 = ((NXOpen.CAM.Tool)nCGroup6);
        objectsToBeMoved7[0] = tool5;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved7, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.MillToolBuilder millToolBuilder1;
        millToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool5);

        millToolBuilder1.TlDiameterBuilder.Value = 8.0;

        millToolBuilder1.TlDiameterBuilder.Value = 12.0;

        millToolBuilder1.TlNumberBuilder.Value = 7;

        millToolBuilder1.TlAdjRegBuilder.Value = 7;

        millToolBuilder1.TlCutcomRegBuilder.Value = 7;

        millToolBuilder1.UseTaperedShank = true;


        NXOpen.NXObject nXObject11;
        nXObject11 = millToolBuilder1.Commit();
        millToolBuilder1.Destroy();
        theSession.CAMSession.Utils.SetInspectionIntent(false);

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

        NXOpen.Point3d origin16 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal16 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane16;
        plane16 = workPart.Planes.CreatePlane(origin16, normal16, NXOpen.SmartObject.UpdateOption.AfterModeling);
        cylinderMillingBuilder2.CleanupPasses = false;

        cylinderMillingBuilder2.CutPattern = NXOpen.CAM.CylinderMillingBuilder.CutPatternTypes.Circular;

        cylinderMillingBuilder2.CuttingParameters.PartStock.Value = 0.25;
        cylinderMillingBuilder2.FeedsBuilder.SpindleRpmBuilder.Value = 2500.0;

        NXOpen.Point3d origin17 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal17 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane17;
        plane17 = workPart.Planes.CreatePlane(origin17, normal17, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane17.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        cylinderMillingBuilder2.FeedsBuilder.FeedCutBuilder.Value = 500.0;

        NXOpen.Point3d origin18 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal18 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane18;
        plane18 = workPart.Planes.CreatePlane(origin18, normal18, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane18.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        cylinderMillingBuilder2.CuttingParameters.TopOffset.Distance = 0.5;

        cylinderMillingBuilder2.AxialStepover.DistanceBuilder.Intent = NXOpen.CAM.ParamValueIntent.PartUnits;

        cylinderMillingBuilder2.AxialStepover.DistanceBuilder.Intent = NXOpen.CAM.ParamValueIntent.ToolDep;

        cylinderMillingBuilder2.AxialStepover.DistanceBuilder.Value = 200.0;

        cylinderMillingBuilder2.RadialStepover.DistanceBuilder.Value = 10.0;

        cylinderMillingBuilder2.NonCuttingBuilder.TransferClearance.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        cylinderMillingBuilder2.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;

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

        NXOpen.Point3d origin19 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal19 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane19;
        plane19 = workPart.Planes.CreatePlane(origin19, normal19, NXOpen.SmartObject.UpdateOption.AfterModeling);

        NXOpen.NXObject nXObject13;
        nXObject13 = cylinderMillingBuilder3.Commit();

        cylinderMillingBuilder3.Destroy();


        theSession.CAMSession.Utils.SetInspectionIntent(false);

        NXOpen.CAM.Method method3 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("MILL_FINISH"));
        NXOpen.CAM.FeatureGeometry featureGeometry2 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
        NXOpen.CAM.Operation operation4;
        operation4 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method3, tool6, featureGeometry2, "hole_making", "HOLE_MILLING", NXOpen.CAM.OperationCollection.UseDefaultName.False, "HOLE_MILLING-SEMI-FINISH", "Hole Milling");


        NXOpen.CAM.CylinderMilling cylinderMilling4 = ((NXOpen.CAM.CylinderMilling)operation4);
        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder4;
        cylinderMillingBuilder4 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling4);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters12;
        holeMachiningCutParameters12 = cylinderMillingBuilder4.CuttingParameters;

        NXOpen.Point3d origin20 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal20 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane20;
        plane20 = workPart.Planes.CreatePlane(origin20, normal20, NXOpen.SmartObject.UpdateOption.AfterModeling);


        NXOpen.CAM.CylinderMillingCutParameters cylinderMillingCutParameters4 = ((NXOpen.CAM.CylinderMillingCutParameters)holeMachiningCutParameters12);
        NXOpen.CAM.VerticalPosition verticalPosition18;
        verticalPosition18 = cylinderMillingCutParameters4.BottomOffset;

      
        cylinderMillingBuilder4.CutPattern = NXOpen.CAM.CylinderMillingBuilder.CutPatternTypes.Circular;

        cylinderMillingBuilder4.CuttingParameters.PartStock.Value = 0.14999999999999999;

        NXOpen.NXObject nXObject14;
        nXObject14 = cylinderMillingBuilder4.Commit();

        NXOpen.CAM.CAMObject[] objectsToBeMoved10 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CylinderMilling cylinderMilling5 = ((NXOpen.CAM.CylinderMilling)nXObject14);
        objectsToBeMoved10[0] = cylinderMilling5;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.Geometry, objectsToBeMoved10, featureGeometry1, NXOpen.CAM.CAMSetup.Paste.Inside);

        cylinderMillingBuilder4.Destroy();


        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder5;
        cylinderMillingBuilder5 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling5);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters13;
        holeMachiningCutParameters13 = cylinderMillingBuilder5.CuttingParameters;

        NXOpen.Point3d origin21 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal21 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane21;
        plane21 = workPart.Planes.CreatePlane(origin21, normal21, NXOpen.SmartObject.UpdateOption.AfterModeling);


        NXOpen.CAM.CylinderMillingCutParameters cylinderMillingCutParameters5 = ((NXOpen.CAM.CylinderMillingCutParameters)holeMachiningCutParameters13);
        NXOpen.CAM.VerticalPosition verticalPosition19;
        verticalPosition19 = cylinderMillingCutParameters5.BottomOffset;

        cylinderMillingBuilder5.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.Number;

        cylinderMillingBuilder5.CleanupPasses = false;

        cylinderMillingBuilder5.FeedsBuilder.SpindleRpmBuilder.Value = 2500.0;

        NXOpen.Point3d origin22 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal22 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane22;
        plane22 = workPart.Planes.CreatePlane(origin22, normal22, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane22.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        cylinderMillingBuilder5.FeedsBuilder.FeedCutBuilder.Value = 500.0;

        NXOpen.Point3d origin23 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal23 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane23;
        plane23 = workPart.Planes.CreatePlane(origin23, normal23, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane23.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        cylinderMillingBuilder5.NonCuttingBuilder.TransferClearance.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        cylinderMillingBuilder5.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;

        NXOpen.NXObject nXObject15;
        nXObject15 = cylinderMillingBuilder5.Commit();

        NXOpen.CAM.CAMObject[] objects4 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CylinderMilling cylinderMilling6 = ((NXOpen.CAM.CylinderMilling)nXObject15);
        objects4[0] = cylinderMilling6;
        workPart.CAMSetup.GenerateToolPath(objects4);

        cylinderMillingBuilder5.Destroy();

     
        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder6;
        cylinderMillingBuilder6 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling6);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters14;
        holeMachiningCutParameters14 = cylinderMillingBuilder6.CuttingParameters;

        NXOpen.Point3d origin24 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal24 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane24;
        plane24 = workPart.Planes.CreatePlane(origin24, normal24, NXOpen.SmartObject.UpdateOption.AfterModeling);

  

        NXOpen.NXObject nXObject16;
        nXObject16 = cylinderMillingBuilder6.Commit();


        cylinderMillingBuilder6.Destroy();

      

        theSession.CAMSession.Utils.SetInspectionIntent(false);

        NXOpen.CAM.CylinderMilling cylinderMilling7 = ((NXOpen.CAM.CylinderMilling)nXObject13);
        theSession.CAMSession.PathDisplay.ShowToolPath(cylinderMilling7);

        theSession.CAMSession.PathDisplay.Jump(false);

        theSession.CAMSession.PathDisplay.HideToolPath(cylinderMilling7);

        NXOpen.CAM.CylinderMilling cylinderMilling8 = ((NXOpen.CAM.CylinderMilling)nXObject16);
        theSession.CAMSession.PathDisplay.ShowToolPath(cylinderMilling8);

        theSession.CAMSession.PathDisplay.Jump(false);

        NXOpen.CAM.CAMObject[] objectsToBeBuffered1 = new NXOpen.CAM.CAMObject[1];
        objectsToBeBuffered1[0] = cylinderMilling8;
        workPart.CAMSetup.BufferObjects(NXOpen.CAM.CAMSetup.View.ProgramOrder, objectsToBeBuffered1);

        NXOpen.CAM.CAMObject[] objectsToBeMoved11 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved11[0] = cylinderMilling8;
        NXOpen.CAM.CAMObject[] newObjects1;
        newObjects1 = workPart.CAMSetup.CopyObjects(NXOpen.CAM.CAMSetup.View.ProgramOrder, objectsToBeMoved11, cylinderMilling8, NXOpen.CAM.CAMSetup.Paste.After);

        theSession.CAMSession.PathDisplay.HideToolPath(cylinderMilling8);

        NXOpen.CAM.CylinderMilling cylinderMilling9 = ((NXOpen.CAM.CylinderMilling)newObjects1[0]);
        theSession.CAMSession.PathDisplay.ShowToolPath(cylinderMilling9);

        theSession.CAMSession.PathDisplay.Jump(false);

        theSession.CAMSession.PathDisplay.HideToolPath(cylinderMilling9);

        theSession.CAMSession.PathDisplay.ShowToolPath(cylinderMilling9);

        theSession.CAMSession.PathDisplay.Jump(false);


        cylinderMilling9.SetName("HOLE_MILLING--FINISH");

        theSession.CAMSession.PathDisplay.HideToolPath(cylinderMilling9);

        theSession.CAMSession.PathDisplay.ShowToolPath(cylinderMilling9);

        theSession.CAMSession.PathDisplay.Jump(false);

        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder7;
        cylinderMillingBuilder7 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling9);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters15;
        holeMachiningCutParameters15 = cylinderMillingBuilder7.CuttingParameters;

        NXOpen.Point3d origin25 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal25 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane25;
        plane25 = workPart.Planes.CreatePlane(origin25, normal25, NXOpen.SmartObject.UpdateOption.AfterModeling);

        NXOpen.CAM.CylinderMillingCutParameters cylinderMillingCutParameters7 = ((NXOpen.CAM.CylinderMillingCutParameters)holeMachiningCutParameters15);
        NXOpen.CAM.VerticalPosition verticalPosition21;
        verticalPosition21 = cylinderMillingCutParameters7.BottomOffset;

        cylinderMillingBuilder7.CuttingParameters.PartStock.Value = 0.050000000000000003;

        NXOpen.NXObject nXObject17;
        nXObject17 = cylinderMillingBuilder7.Commit();

        NXOpen.CAM.CAMObject[] objects5 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CylinderMilling cylinderMilling10 = ((NXOpen.CAM.CylinderMilling)nXObject17);
        objects5[0] = cylinderMilling10;
        workPart.CAMSetup.GenerateToolPath(objects5);


        cylinderMillingBuilder7.Destroy();

      

        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder8;
        cylinderMillingBuilder8 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling10);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters16;
        holeMachiningCutParameters16 = cylinderMillingBuilder8.CuttingParameters;

        NXOpen.Point3d origin26 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal26 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane26;
        plane26 = workPart.Planes.CreatePlane(origin26, normal26, NXOpen.SmartObject.UpdateOption.AfterModeling);

   

        NXOpen.NXObject nXObject18;
        nXObject18 = cylinderMillingBuilder8.Commit();



        cylinderMillingBuilder8.Destroy();

   

        theSession.CAMSession.Utils.SetInspectionIntent(false);

        NXOpen.CAM.CAMObject[] objectsToBeBuffered2 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CylinderMilling cylinderMilling11 = ((NXOpen.CAM.CylinderMilling)nXObject18);
        objectsToBeBuffered2[0] = cylinderMilling11;
        workPart.CAMSetup.BufferObjects(NXOpen.CAM.CAMSetup.View.ProgramOrder, objectsToBeBuffered2);

        NXOpen.CAM.CAMObject[] objectsToBeMoved12 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved12[0] = cylinderMilling11;
        NXOpen.CAM.CAMObject[] newObjects2;
        newObjects2 = workPart.CAMSetup.CopyObjects(NXOpen.CAM.CAMSetup.View.ProgramOrder, objectsToBeMoved12, cylinderMilling11, NXOpen.CAM.CAMSetup.Paste.After);

        theSession.CAMSession.PathDisplay.HideToolPath(cylinderMilling11);

        NXOpen.CAM.CylinderMilling cylinderMilling12 = ((NXOpen.CAM.CylinderMilling)newObjects2[0]);
        theSession.CAMSession.PathDisplay.ShowToolPath(cylinderMilling12);

        theSession.CAMSession.PathDisplay.Jump(false);


        cylinderMilling12.SetName("HOLE_MILLING--ADJUST-01");

        theSession.CAMSession.PathDisplay.HideToolPath(cylinderMilling12);

        theSession.CAMSession.PathDisplay.ShowToolPath(cylinderMilling12);

        theSession.CAMSession.PathDisplay.Jump(false);

        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder9;
        cylinderMillingBuilder9 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling12);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters17;
        holeMachiningCutParameters17 = cylinderMillingBuilder9.CuttingParameters;

        NXOpen.Point3d origin27 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal27 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane27;
        plane27 = workPart.Planes.CreatePlane(origin27, normal27, NXOpen.SmartObject.UpdateOption.AfterModeling);

        cylinderMillingBuilder9.CuttingParameters.PartStock.Value = 0.025000000000000001;

        NXOpen.NXObject nXObject19;
        nXObject19 = cylinderMillingBuilder9.Commit();

        NXOpen.CAM.CAMObject[] objects6 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CylinderMilling cylinderMilling13 = ((NXOpen.CAM.CylinderMilling)nXObject19);
        objects6[0] = cylinderMilling13;
        workPart.CAMSetup.GenerateToolPath(objects6);
        cylinderMillingBuilder9.Destroy();


        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder10;
        cylinderMillingBuilder10 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling13);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters18;
        holeMachiningCutParameters18 = cylinderMillingBuilder10.CuttingParameters;

        NXOpen.Point3d origin28 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal28 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane28;
        plane28 = workPart.Planes.CreatePlane(origin28, normal28, NXOpen.SmartObject.UpdateOption.AfterModeling);


        NXOpen.NXObject nXObject20;
        nXObject20 = cylinderMillingBuilder10.Commit();


        cylinderMillingBuilder10.Destroy();

        

        theSession.CAMSession.Utils.SetInspectionIntent(false);

        NXOpen.CAM.CAMObject[] objectsToBeBuffered3 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CylinderMilling cylinderMilling14 = ((NXOpen.CAM.CylinderMilling)nXObject20);
        objectsToBeBuffered3[0] = cylinderMilling14;
        workPart.CAMSetup.BufferObjects(NXOpen.CAM.CAMSetup.View.ProgramOrder, objectsToBeBuffered3);

        NXOpen.CAM.CAMObject[] objectsToBeMoved13 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved13[0] = cylinderMilling14;
        NXOpen.CAM.CAMObject[] newObjects3;
        newObjects3 = workPart.CAMSetup.CopyObjects(NXOpen.CAM.CAMSetup.View.ProgramOrder, objectsToBeMoved13, cylinderMilling14, NXOpen.CAM.CAMSetup.Paste.After);

        theSession.CAMSession.PathDisplay.HideToolPath(cylinderMilling14);

        NXOpen.CAM.CylinderMilling cylinderMilling15 = ((NXOpen.CAM.CylinderMilling)newObjects3[0]);
        theSession.CAMSession.PathDisplay.ShowToolPath(cylinderMilling15);

        theSession.CAMSession.PathDisplay.Jump(false);

  

        cylinderMilling15.SetName("HOLE_MILLING--ADJUST-02");

        theSession.CAMSession.PathDisplay.HideToolPath(cylinderMilling15);

        theSession.CAMSession.PathDisplay.ShowToolPath(cylinderMilling15);

        theSession.CAMSession.PathDisplay.Jump(false);


        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder11;
        cylinderMillingBuilder11 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling15);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters19;
        holeMachiningCutParameters19 = cylinderMillingBuilder11.CuttingParameters;

        NXOpen.Point3d origin29 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal29 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane29;
        plane29 = workPart.Planes.CreatePlane(origin29, normal29, NXOpen.SmartObject.UpdateOption.AfterModeling);


        NXOpen.CAM.CylinderMillingCutParameters cylinderMillingCutParameters11 = ((NXOpen.CAM.CylinderMillingCutParameters)holeMachiningCutParameters19);
        NXOpen.CAM.VerticalPosition verticalPosition25;
        verticalPosition25 = cylinderMillingCutParameters11.BottomOffset;

        cylinderMillingBuilder11.CuttingParameters.PartStock.Value = 0.014999999999999999;

        NXOpen.NXObject nXObject21;
        nXObject21 = cylinderMillingBuilder11.Commit();

        NXOpen.CAM.CAMObject[] objects7 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CylinderMilling cylinderMilling16 = ((NXOpen.CAM.CylinderMilling)nXObject21);
        objects7[0] = cylinderMilling16;
        workPart.CAMSetup.GenerateToolPath(objects7);

        cylinderMillingBuilder11.Destroy();

        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder12;
        cylinderMillingBuilder12 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling16);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters20;
        holeMachiningCutParameters20 = cylinderMillingBuilder12.CuttingParameters;

        NXOpen.Point3d origin30 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal30 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane30;
        plane30 = workPart.Planes.CreatePlane(origin30, normal30, NXOpen.SmartObject.UpdateOption.AfterModeling);

        NXOpen.CAM.CylinderMillingCutParameters cylinderMillingCutParameters12 = ((NXOpen.CAM.CylinderMillingCutParameters)holeMachiningCutParameters20);
        NXOpen.CAM.VerticalPosition verticalPosition26;
        verticalPosition26 = cylinderMillingCutParameters12.BottomOffset;

      
        NXOpen.NXObject nXObject22;
        nXObject22 = cylinderMillingBuilder12.Commit();
        cylinderMillingBuilder12.Destroy();


        theSession.CAMSession.Utils.SetInspectionIntent(false);

        NXOpen.CAM.CAMObject[] objectsToBeBuffered4 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CylinderMilling cylinderMilling17 = ((NXOpen.CAM.CylinderMilling)nXObject22);
        objectsToBeBuffered4[0] = cylinderMilling17;
        workPart.CAMSetup.BufferObjects(NXOpen.CAM.CAMSetup.View.ProgramOrder, objectsToBeBuffered4);

        NXOpen.CAM.CAMObject[] objectsToBeMoved14 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved14[0] = cylinderMilling17;
        NXOpen.CAM.CAMObject[] newObjects4;
        newObjects4 = workPart.CAMSetup.CopyObjects(NXOpen.CAM.CAMSetup.View.ProgramOrder, objectsToBeMoved14, cylinderMilling17, NXOpen.CAM.CAMSetup.Paste.After);

        theSession.CAMSession.PathDisplay.HideToolPath(cylinderMilling17);

        NXOpen.CAM.CylinderMilling cylinderMilling18 = ((NXOpen.CAM.CylinderMilling)newObjects4[0]);
        theSession.CAMSession.PathDisplay.ShowToolPath(cylinderMilling18);

        theSession.CAMSession.PathDisplay.Jump(false);


        cylinderMilling18.SetName("HOLE_MILLING--ADJUST-03");

        theSession.CAMSession.PathDisplay.HideToolPath(cylinderMilling18);

        theSession.CAMSession.PathDisplay.ShowToolPath(cylinderMilling18);

        theSession.CAMSession.PathDisplay.Jump(false);


        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder13;
        cylinderMillingBuilder13 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling18);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters21;
        holeMachiningCutParameters21 = cylinderMillingBuilder13.CuttingParameters;

        NXOpen.Point3d origin31 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal31 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane31;
        plane31 = workPart.Planes.CreatePlane(origin31, normal31, NXOpen.SmartObject.UpdateOption.AfterModeling);

        cylinderMillingBuilder13.CuttingParameters.PartStock.Value = 0.0;

        NXOpen.NXObject nXObject23;
        nXObject23 = cylinderMillingBuilder13.Commit();

        NXOpen.CAM.CAMObject[] objects8 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.CylinderMilling cylinderMilling19 = ((NXOpen.CAM.CylinderMilling)nXObject23);
        objects8[0] = cylinderMilling19;
        workPart.CAMSetup.GenerateToolPath(objects8);


        cylinderMillingBuilder13.Destroy();

      

        NXOpen.CAM.CylinderMillingBuilder cylinderMillingBuilder14;
        cylinderMillingBuilder14 = workPart.CAMSetup.CAMOperationCollection.CreateCylinderMillingBuilder(cylinderMilling19);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters22;
        holeMachiningCutParameters22 = cylinderMillingBuilder14.CuttingParameters;

        NXOpen.Point3d origin32 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal32 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane32;
        plane32 = workPart.Planes.CreatePlane(origin32, normal32, NXOpen.SmartObject.UpdateOption.AfterModeling);

        NXOpen.NXObject nXObject24;
        nXObject24 = cylinderMillingBuilder14.Commit();

        cylinderMillingBuilder14.Destroy();

        theSession.CAMSession.Utils.SetInspectionIntent(false);

        NXOpen.CAM.CylinderMilling cylinderMilling20 = ((NXOpen.CAM.CylinderMilling)nXObject24);
        theSession.CAMSession.PathDisplay.HideToolPath(cylinderMilling20);

        theSession.CAMSession.PathDisplay.ShowToolPath(cylinderMilling14);

        theSession.CAMSession.PathDisplay.Jump(false);

        theSession.CAMSession.PathDisplay.HideToolPath(cylinderMilling14);


    }
    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
}
