// NX 2406
// Journal created by dcard on Sat Feb 21 16:47:08 2026 Eastern Standard Time
//
using System;
using NXOpen;

public class WallRough
{
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;


        NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
        NXOpen.CAM.Method method1 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("MILL_ROUGH"));
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));
        NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "mill_planar", "WALL_PROFILING", NXOpen.CAM.OperationCollection.UseDefaultName.True, "WALL_PROFILING", "Wall Profiling");

        
        NXOpen.CAM.VolumeBased25DMillingOperation volumeBased25DMillingOperation1 = ((NXOpen.CAM.VolumeBased25DMillingOperation)operation1);
        NXOpen.CAM.VolumeBased25DMillingOperationBuilder volumeBased25DMillingOperationBuilder1;
        volumeBased25DMillingOperationBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateVolumeBased25dMillingOperationBuilder(volumeBased25DMillingOperation1);

        NXOpen.TaggedObject taggedObject1;
        taggedObject1 = volumeBased25DMillingOperationBuilder1.GetCustomizableItemBuilder("Non Cutting Moves Context: ");


        // ----------------------------------------------
        //   Dialog Begin 2D Profile Wall without Floor - [WALL_PROFILING]
        // ----------------------------------------------
        NXOpen.NXObject nXObject1;
        nXObject1 = volumeBased25DMillingOperationBuilder1.Commit();




        // ----------------------------------------------
        //   Dialog Begin New Tool
        // ----------------------------------------------
 
        NXOpen.CAM.NCGroup nCGroup4;
        nCGroup4 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup2, "mill_planar", "MILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "ENDMILLD12MM", "Mill");

        NXOpen.CAM.CAMObject[] objectsToBeMoved1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)nCGroup4);
        objectsToBeMoved1[0] = tool1;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved1, nCGroup2, NXOpen.CAM.CAMSetup.Paste.Inside);



        NXOpen.CAM.MillToolBuilder millToolBuilder1;
        millToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool1);



        // ----------------------------------------------
        //   Dialog Begin Milling Tool-5 Parameters
        // ----------------------------------------------
        millToolBuilder1.TlDiameterBuilder.Value = 12.0;

        millToolBuilder1.TlNumberBuilder.Value = 8;

        millToolBuilder1.TlAdjRegBuilder.Value = 8;

        millToolBuilder1.TlCutcomRegBuilder.Value = 8;


        NXOpen.NXObject nXObject2;
        nXObject2 = millToolBuilder1.Commit();

        millToolBuilder1.Destroy();


        NXOpen.CAM.CAMObject[] objectsToBeMoved2 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.VolumeBased25DMillingOperation volumeBased25DMillingOperation2 = ((NXOpen.CAM.VolumeBased25DMillingOperation)nXObject1);
        objectsToBeMoved2[0] = volumeBased25DMillingOperation2;
        NXOpen.CAM.Tool tool2 = ((NXOpen.CAM.Tool)nXObject2);
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved2, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.CAMObject[] objectsToBeMoved3 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved3[0] = volumeBased25DMillingOperation2;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved3, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);


        NXOpen.CAM.VolumeBased25DMillingOperationBuilder volumeBased25DMillingOperationBuilder2;
        volumeBased25DMillingOperationBuilder2 = workPart.CAMSetup.CAMOperationCollection.CreateVolumeBased25dMillingOperationBuilder(volumeBased25DMillingOperation2);

        //   Dialog Begin 2D Profile Wall without Floor - [WALL_PROFILING]
        // ----------------------------------------------
        NXOpen.NXObject nXObject3;
        nXObject3 = volumeBased25DMillingOperationBuilder2.Commit();



        NXOpen.CAM.MillToolBuilder millToolBuilder2;
        millToolBuilder2 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool2);



        // ----------------------------------------------
        //   Dialog Begin Milling Tool-5 Parameters
        // ----------------------------------------------
        millToolBuilder2.UseTaperedShank = true;


        NXOpen.CAM.VolumeBased25DMillingOperation volumeBased25DMillingOperation3 = ((NXOpen.CAM.VolumeBased25DMillingOperation)nXObject3);
        NXOpen.CAM.VolumeBased25DMillingOperationBuilder volumeBased25DMillingOperationBuilder3;
        volumeBased25DMillingOperationBuilder3 = workPart.CAMSetup.CAMOperationCollection.CreateVolumeBased25dMillingOperationBuilder(volumeBased25DMillingOperation3);

        // ----------------------------------------------
        //   Dialog Begin 2D Profile Wall without Floor - [WALL_PROFILING]
        // ----------------------------------------------
        volumeBased25DMillingOperationBuilder3.ZDepthOffset.Value = -6.0;

        volumeBased25DMillingOperationBuilder3.WallBlankThickness.Value = 2.5;

        volumeBased25DMillingOperationBuilder3.BndStepover.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.Multiple;

        NXOpen.CAM.MultipleStepoverBuilder multipleStepoverBuilder1;
        multipleStepoverBuilder1 = volumeBased25DMillingOperationBuilder3.BndStepover.MultipleBuilder;

        multipleStepoverBuilder1.Modify(0, 3, 0.5, 0);


        volumeBased25DMillingOperationBuilder3.FeedsBuilder.SpindleRpmBuilder.Value = 2500.0;

        NXOpen.Direction nullNXOpen_Direction = null;
        volumeBased25DMillingOperationBuilder3.NonCuttingBuilder.TransferAvoidanceFromBuilder.ToolAxis = nullNXOpen_Direction;

        volumeBased25DMillingOperationBuilder3.NonCuttingBuilder.TransferAvoidanceGohomeBuilder.ToolAxis = nullNXOpen_Direction;


        volumeBased25DMillingOperationBuilder3.NonCuttingBuilder.ClearanceBuilder.AxisObject = nullNXOpen_Direction;

        volumeBased25DMillingOperationBuilder3.FeedsBuilder.FeedCutBuilder.Value = 400.0;

        volumeBased25DMillingOperationBuilder3.NonCuttingBuilder.TransferAvoidanceFromBuilder.ToolAxis = nullNXOpen_Direction;

        volumeBased25DMillingOperationBuilder3.NonCuttingBuilder.TransferAvoidanceGohomeBuilder.ToolAxis = nullNXOpen_Direction;


        volumeBased25DMillingOperationBuilder3.NonCuttingBuilder.ClearanceBuilder.AxisObject = nullNXOpen_Direction;

  
        volumeBased25DMillingOperationBuilder3.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        volumeBased25DMillingOperationBuilder3.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;

        volumeBased25DMillingOperationBuilder3.NonCuttingBuilder.TransferBetweenRegionsBuilder.Type = NXOpen.CAM.NcmTransfer.TransferTypes.PrevPlane;

        volumeBased25DMillingOperationBuilder3.NonCuttingBuilder.TransferBetweenRegionsBuilder.SafeDistanceBuilder.Value = 0.5;

        volumeBased25DMillingOperationBuilder3.NonCuttingBuilder.TransferWithinLevelsType = NXOpen.CAM.NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;

        volumeBased25DMillingOperationBuilder3.NonCuttingBuilder.TransferWithinLevelsType = NXOpen.CAM.NcmPlanarBuilder.TransferWithinLevelsTypes.Direct;

        NXOpen.NXObject nXObject5;
        nXObject5 = volumeBased25DMillingOperationBuilder3.Commit();


        NXOpen.NXObject nXObject6;
        nXObject6 = volumeBased25DMillingOperationBuilder3.Commit();

  

        volumeBased25DMillingOperationBuilder3.Destroy();



        // facing




    }
    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }

}
