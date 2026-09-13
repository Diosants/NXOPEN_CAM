
using System;
using NXOpen;
using NXOpen.CAM;

public class M12
{
    public static void Run(string[] args)
    {

        //  CABECALHO DO CÓDIGO - INICIALIZAÇÃO
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;
        UI theUI = UI.GetUI();


        //INICIA GRUPO/ NC, METODO, FERRAMENTA, WORKPIECE


        NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM")); // PASTA  PROGRAMA NC
        NXOpen.CAM.Method method1 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("DRILL_METHOD"));
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE")); //  FERRAMENTA
        NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "hole_making", "SPOT_DRILLING", NXOpen.CAM.OperationCollection.UseDefaultName.False, "CENTER-DRILL-M12", "Spot Drilling");

        //  START OPERATION ONE - CENTER DRILL
        NXOpen.CAM.HoleDrilling holeDrilling1 = ((NXOpen.CAM.HoleDrilling)operation1);
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder1;
        holeDrillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling1);

        NXOpen.NXObject nXObject1;
        nXObject1 = holeDrillingBuilder1.Commit();

        // GETTING  CENTER DRILL TOOL
        NXOpen.CAM.NCGroup nCGroup3 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("GENERIC_MACHINE"));
        NXOpen.CAM.NCGroup nCGroup4;
        nCGroup4 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "hole_making", "CENTERDRILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "CENTER-TAP-M12", "CENTER-TAP-M12");

        NXOpen.CAM.CAMObject[] objectsToBeMoved1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)nCGroup4);
        objectsToBeMoved1[0] = tool1;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved1, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.DrillCenterBellToolBuilder drillCenterBellToolBuilder1;
        drillCenterBellToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateDrillCenterBellToolBuilder(tool1);


        //    DADOS SHANK
        drillCenterBellToolBuilder1.UseTaperedShank = true;
        drillCenterBellToolBuilder1.TlNumberBuilder.Value = 10;
        drillCenterBellToolBuilder1.TlAdjRegBuilder.Value = 10;
        holeDrillingBuilder1.FeedsBuilder.SpindleRpmBuilder.Value = 1500.0;
        holeDrillingBuilder1.FeedsBuilder.FeedCutBuilder.Value = 150.0;
        holeDrillingBuilder1.NonCuttingBuilder.TransferClearance.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
        holeDrillingBuilder1.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;

        //   CRIAR  PONTO,VETOR E PLANO PARA ATUALIZAR OS DADOS  INSERIDOS
        NXOpen.Point3d origin100 = new NXOpen.Point3d(0, 0, 0);
        NXOpen.Vector3d normal100 = new NXOpen.Vector3d(1, 0, 0);
        NXOpen.Plane plane100;
        plane100 = workPart.Planes.CreatePlane(origin100, normal100, NXOpen.SmartObject.UpdateOption.WithinModeling);


        NXOpen.NXObject nXObject2;
        nXObject2 = drillCenterBellToolBuilder1.Commit();

        NXOpen.CAM.CAMObject[] objectsToBeMoved2 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling2 = ((NXOpen.CAM.HoleDrilling)nXObject1);
        objectsToBeMoved2[0] = holeDrilling2;
        NXOpen.CAM.Tool tool2 = ((NXOpen.CAM.Tool)nXObject2);

        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved2, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);
        NXOpen.CAM.CAMObject[] objectsToBeMoved3 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved3[0] = holeDrilling2;

        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved3, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);

        holeDrillingBuilder1.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;

        holeDrillingBuilder1.Destroy();

        //  END OF CREATING TOOL


        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder2;
        holeDrillingBuilder2 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling2);

        NXOpen.NXObject nXObject3;
        nXObject3 = holeDrillingBuilder2.Commit();

        NXOpen.CAM.CAMObject[] objectsToBeMoved4 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling3 = ((NXOpen.CAM.HoleDrilling)nXObject3);
        objectsToBeMoved4[0] = holeDrilling3;
        NXOpen.CAM.FeatureGeometry featureGeometry2 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("M12"));
        if (workPart.CAMSetup.CAMGroupCollection.FindObject("M12") == null)
        {

            theUI.NXMessageBox.Show("Error", NXOpen.NXMessageBox.DialogType.Error, "Feature Geometry M12 not found.");
            return;
        }
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.Geometry, objectsToBeMoved4, featureGeometry2, NXOpen.CAM.CAMSetup.Paste.Inside);


        NXOpen.CAM.CAMObject[] objects100 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling100 = ((NXOpen.CAM.HoleDrilling)nXObject3);
        objects100[0] = holeDrilling3;
        workPart.CAMSetup.GenerateToolPath(objects100);


        holeDrillingBuilder2.Destroy();



        // OPERATION TW0 - DRILL DIAM 5
        NXOpen.CAM.Operation operation2;
        operation2 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "hole_making", "DRILLING", NXOpen.CAM.OperationCollection.UseDefaultName.False, "DRILL-D10.2", "Drilling");


        NXOpen.CAM.HoleDrilling holeDrilling5 = ((NXOpen.CAM.HoleDrilling)operation2);
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder5;
        holeDrillingBuilder5 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling5);

        NXOpen.NXObject nXObject6;
        nXObject6 = holeDrillingBuilder5.Commit();


        // GETTING TOOL DIAM 10.2
        NXOpen.CAM.NCGroup nCGroup5;
        nCGroup5 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "hole_making", "STD_DRILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "HSS-D10.5", "Std Drill");

        NXOpen.CAM.CAMObject[] objectsToBeMoved5 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool3 = ((NXOpen.CAM.Tool)nCGroup5);
        objectsToBeMoved5[0] = tool3;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved5, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.DrillStdToolBuilder drillStdToolBuilder1;
        drillStdToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateDrillStdToolBuilder(tool3);

        drillStdToolBuilder1.TlDiameterBuilder.Value = 10.2;
        drillStdToolBuilder1.TlNumberBuilder.Value = 12;

        drillStdToolBuilder1.TlAdjRegBuilder.Value = 12;

        drillStdToolBuilder1.UseTaperedShank = true;

        drillStdToolBuilder1.ShankSectionBuilder.Modify(1, 50, 60, -17.5, 0);

        drillStdToolBuilder1.ShankSectionBuilder.Modify(1, 50.0, 50.0, -20.8, 0);

        drillStdToolBuilder1.ShankSectionBuilder.Modify(1, 50.0, 50.0, 0.0, 0.0);

        holeDrillingBuilder5.CycleTable.AxialStepover.DistanceBuilder.Value = 1.0;


        NXOpen.NXObject nXObject7;
        nXObject7 = drillStdToolBuilder1.Commit();

        drillStdToolBuilder1.Destroy();


        NXOpen.CAM.CAMObject[] objectsToBeMoved6 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling6 = ((NXOpen.CAM.HoleDrilling)nXObject6);
        objectsToBeMoved6[0] = holeDrilling6;
        NXOpen.CAM.Tool tool4 = ((NXOpen.CAM.Tool)nXObject7);
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved6, tool4, NXOpen.CAM.CAMSetup.Paste.Inside);


        holeDrillingBuilder5.Destroy();


        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder6;
        holeDrillingBuilder6 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling6);



        NXOpen.NXObject nXObject8;
        nXObject8 = holeDrillingBuilder6.Commit();

        holeDrillingBuilder6.Destroy();

        NXOpen.CAM.HoleDrilling holeDrilling7 = ((NXOpen.CAM.HoleDrilling)nXObject8);
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder7;
        holeDrillingBuilder7 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling7);

        NXOpen.NXObject nXObject9;
        nXObject9 = holeDrillingBuilder7.Commit();

        NXOpen.CAM.CAMObject[] objectsToBeMoved8 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling8 = ((NXOpen.CAM.HoleDrilling)nXObject9);
        objectsToBeMoved8[0] = holeDrilling8;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.Geometry, objectsToBeMoved8, featureGeometry2, NXOpen.CAM.CAMSetup.Paste.Inside);

        holeDrillingBuilder7.Destroy();


        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder8;
        holeDrillingBuilder8 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling8);


        holeDrillingBuilder8.FeedsBuilder.SurfaceSpeedBuilder.Value = 25.0;

        holeDrillingBuilder8.FeedsBuilder.FeedPerToothBuilder.Value = 0.1;

        holeDrillingBuilder8.FeedsBuilder.FeedPerToothBuilder.Value = 0.1;

        holeDrillingBuilder8.FeedsBuilder.RecalculateData(NXOpen.CAM.FeedsBuilder.RecalculateBasedOn.SurfaceSpeed);

        holeDrillingBuilder8.FeedsBuilder.RecalculateData(NXOpen.CAM.FeedsBuilder.RecalculateBasedOn.FeedPerTooth);

        holeDrillingBuilder8.NonCuttingBuilder.TransferClearance.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        holeDrillingBuilder8.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;

        holeDrillingBuilder8.NonCuttingBuilder.TransferBetweenRegions.Type = NXOpen.CAM.NcmTransfer.TransferTypes.ShortestToClearance;

        NXOpen.NXObject nXObject10;
        nXObject10 = holeDrillingBuilder8.Commit();

        NXOpen.CAM.CAMObject[] objects2 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling9 = ((NXOpen.CAM.HoleDrilling)nXObject10);
        objects2[0] = holeDrilling9;
        workPart.CAMSetup.GenerateToolPath(objects2);

        holeDrillingBuilder8.Destroy();

        // THIRD OPERATION- COUNTERSINK


        NXOpen.CAM.Operation operation3;
        operation3 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "hole_making", "COUNTERSINKING", NXOpen.CAM.OperationCollection.UseDefaultName.False, "DEBUR-M12", "Countersinking");

        NXOpen.CAM.HoleDrilling holeDrilling10 = ((NXOpen.CAM.HoleDrilling)operation3);
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder10;
        holeDrillingBuilder10 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling10);

        NXOpen.NXObject nXObject12;
        nXObject12 = holeDrillingBuilder10.Commit();

        // GETTING COUNTERSINK TOOL
        NXOpen.CAM.NCGroup nCGroup6;
        nCGroup6 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "hole_making", "COUNTER_SINK", NXOpen.CAM.NCGroupCollection.UseDefaultName.True, "CHAMFERER-D12", "Counter Sink");

        NXOpen.CAM.CAMObject[] objectsToBeMoved9 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool5 = ((NXOpen.CAM.Tool)nCGroup6);
        objectsToBeMoved9[0] = tool5;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved9, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);


        NXOpen.CAM.DrillCtskToolBuilder drillCtskToolBuilder1;
        drillCtskToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateDrillCtskToolBuilder(tool5);

        drillCtskToolBuilder1.TlNumberBuilder.Value = 15;

        drillCtskToolBuilder1.TlAdjRegBuilder.Value = 15;

        drillCtskToolBuilder1.UseTaperedShank = true;

        NXOpen.NXObject nXObject13;
        nXObject13 = drillCtskToolBuilder1.Commit();
        drillCtskToolBuilder1.Destroy();




        NXOpen.CAM.CAMObject[] objectsToBeMoved10 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling11 = ((NXOpen.CAM.HoleDrilling)nXObject12);
        objectsToBeMoved10[0] = holeDrilling11;
        NXOpen.CAM.Tool tool6 = ((NXOpen.CAM.Tool)nXObject13);
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved10, tool6, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.CAMObject[] objectsToBeMoved11 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved11[0] = holeDrilling11;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved11, tool6, NXOpen.CAM.CAMSetup.Paste.Inside);


        holeDrillingBuilder10.Destroy();

        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder11;
        holeDrillingBuilder11 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling11);




        NXOpen.NXObject nXObject14;
        nXObject14 = holeDrillingBuilder11.Commit();

        NXOpen.CAM.CAMObject[] objectsToBeMoved12 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling12 = ((NXOpen.CAM.HoleDrilling)nXObject14);
        objectsToBeMoved12[0] = holeDrilling12;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.Geometry, objectsToBeMoved12, featureGeometry2, NXOpen.CAM.CAMSetup.Paste.Inside);

        holeDrillingBuilder11.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;


        holeDrillingBuilder11.Destroy();

        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder12;
        holeDrillingBuilder12 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling12);

        NXOpen.CAM.HoleMachiningCutParameters holeMachiningCutParameters13;
        holeMachiningCutParameters13 = holeDrillingBuilder12.CuttingParameters;


        holeDrillingBuilder12.FeedsBuilder.SpindleRpmBuilder.Value = 1000.0;
        holeDrillingBuilder12.FeedsBuilder.FeedCutBuilder.Value = 100.0;
        holeDrillingBuilder12.NonCuttingBuilder.TransferClearance.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
        holeDrillingBuilder12.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;
        holeDrillingBuilder12.NonCuttingBuilder.TransferBetweenRegions.Type = NXOpen.CAM.NcmTransfer.TransferTypes.LowestSafeZ;

        NXOpen.NXObject nXObject15;
        nXObject15 = holeDrillingBuilder12.Commit();

        NXOpen.CAM.CAMObject[] objects3 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling13 = ((NXOpen.CAM.HoleDrilling)nXObject15);
        objects3[0] = holeDrilling13;
        workPart.CAMSetup.GenerateToolPath(objects3);


        holeDrillingBuilder12.Destroy();



        //  START TAPPING OPERATION
        NXOpen.CAM.Operation operation4;
        operation4 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry2, "hole_making", "TAPPING", NXOpen.CAM.OperationCollection.UseDefaultName.False, "TAP-M12", "TAP-M12");


        NXOpen.CAM.HoleDrilling holeDrilling14 = ((NXOpen.CAM.HoleDrilling)operation4);
        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder14;
        holeDrillingBuilder14 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling14);



        NXOpen.Point3d origin23 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal23 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane23;
        plane23 = workPart.Planes.CreatePlane(origin23, normal23, NXOpen.SmartObject.UpdateOption.AfterModeling);


        NXOpen.NXObject nXObject17;
        nXObject17 = holeDrillingBuilder14.Commit();

        // GETTING TAP TOOL
        NXOpen.CAM.NCGroup nCGroup7;
        nCGroup7 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "hole_making", "TAP", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "TAPPER-M12", "Tap");

        NXOpen.CAM.CAMObject[] objectsToBeMoved13 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool7 = ((NXOpen.CAM.Tool)nCGroup7);
        objectsToBeMoved13[0] = tool7;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved13, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);



        NXOpen.CAM.DrillTapToolBuilder drillTapToolBuilder1;
        drillTapToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateDrillTapToolBuilder(tool7);


        drillTapToolBuilder1.TlDiameterBuilder.Value = 12;

        drillTapToolBuilder1.TlNumberBuilder.Value = 14;

        drillTapToolBuilder1.TlAdjRegBuilder.Value = 14;

        drillTapToolBuilder1.UseTaperedShank = true;
        ;

        NXOpen.NXObject nXObject18;
        nXObject18 = drillTapToolBuilder1.Commit();


        drillTapToolBuilder1.Destroy();


        NXOpen.CAM.CAMObject[] objectsToBeMoved14 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling15 = ((NXOpen.CAM.HoleDrilling)nXObject17);
        objectsToBeMoved14[0] = holeDrilling15;
        NXOpen.CAM.Tool tool8 = ((NXOpen.CAM.Tool)nXObject18);
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved14, tool8, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.CAMObject[] objectsToBeMoved15 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved15[0] = holeDrilling15;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved15, tool8, NXOpen.CAM.CAMSetup.Paste.Inside);


        holeDrillingBuilder14.Destroy();

        NXOpen.CAM.HoleDrillingBuilder holeDrillingBuilder15;
        holeDrillingBuilder15 = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrilling15);


        NXOpen.Point3d origin24 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal24 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Plane plane24;
        plane24 = workPart.Planes.CreatePlane(origin24, normal24, NXOpen.SmartObject.UpdateOption.AfterModeling);

        holeDrillingBuilder15.CycleTable.AxialStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.None;

        holeDrillingBuilder15.FeedsBuilder.SpindleRpmBuilder.Value = 150.0;

        NXOpen.Point3d origin25 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal25 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane25;
        plane25 = workPart.Planes.CreatePlane(origin25, normal25, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane25.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);


        NXOpen.Point3d origin26 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d normal26 = new NXOpen.Vector3d(1.0, 0.0, 0.0);
        NXOpen.Plane plane26;
        plane26 = workPart.Planes.CreatePlane(origin26, normal26, NXOpen.SmartObject.UpdateOption.WithinModeling);

        plane26.SetUpdateOption(NXOpen.SmartObject.UpdateOption.AfterModeling);

        holeDrillingBuilder15.NonCuttingBuilder.TransferClearance.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        holeDrillingBuilder15.NonCuttingBuilder.TransferClearance.SafeDistance = 50.0;

        holeDrillingBuilder15.NonCuttingBuilder.TransferBetweenRegions.Type = NXOpen.CAM.NcmTransfer.TransferTypes.LowestSafeZ;

        NXOpen.NXObject nXObject19;
        nXObject19 = holeDrillingBuilder15.Commit();

        NXOpen.CAM.CAMObject[] objects4 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.HoleDrilling holeDrilling16 = ((NXOpen.CAM.HoleDrilling)nXObject19);
        objects4[0] = holeDrilling16;
        workPart.CAMSetup.GenerateToolPath(objects4);


        NXOpen.NXObject nXObject20;
        nXObject20 = holeDrillingBuilder15.Commit();
        holeDrillingBuilder15.Destroy();


    }
    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
}
