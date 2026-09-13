
using NXOpen;
using NXOpen.CAM;
using NXOpen.UF;
using System;


namespace NXOPEN_3X_TOOLPATHS
{



    public class CAVITY_MILL_3X_MACHINE
    {
        public static void Run(string[] args)
        {
            NXOpen.Session theSession = NXOpen.Session.GetSession();
            NXOpen.Part workPart = theSession.Parts.Work;
            NXOpen.Part displayPart = theSession.Parts.Display;



            NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
            NXOpen.CAM.Method method1 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("METHOD"));
            NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));
            NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
            NXOpen.CAM.Operation operation1;
            operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "mill_contour", "CAVITY_MILL", NXOpen.CAM.OperationCollection.UseDefaultName.True, "CAVITY_MILL", "Cavity Mill");



            NXOpen.CAM.CavityMilling cavityMilling1 = ((NXOpen.CAM.CavityMilling)operation1);
            NXOpen.CAM.CavityMillingBuilder cavityMillingBuilder1;
            cavityMillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateCavityMillingBuilder(cavityMilling1);


            NXOpen.NXObject nXObject1;
            nXObject1 = cavityMillingBuilder1.Commit();






            //  GETTING NEW TOOL FROM GENERIC_MACHINE
            NXOpen.CAM.NCGroup nCGroup3 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("GENERIC_MACHINE"));
            NXOpen.CAM.NCGroup nCGroup4;
            nCGroup4 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "mill_contour", "MILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "CAB25R1", "Mill");

            NXOpen.CAM.CAMObject[] objectsToBeMoved1 = new NXOpen.CAM.CAMObject[1];
            NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)nCGroup4);
            objectsToBeMoved1[0] = tool1;
            workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved1, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);



            NXOpen.CAM.MillToolBuilder millToolBuilder1;
            millToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool1);



            // ----------------------------------------------
            //   Dialog Begin Milling Tool-5 Parameters
            // ----------------------------------------------
            millToolBuilder1.TlDiameterBuilder.Value = 25.0;

            millToolBuilder1.TlCor1RadBuilder.Value = 0.80000000000000004;

            millToolBuilder1.TlNumberBuilder.Value = 2;

            millToolBuilder1.TlAdjRegBuilder.Value = 3;

            millToolBuilder1.TlNumberBuilder.Value = 3;

            millToolBuilder1.TlCutcomRegBuilder.Value = 3;

            millToolBuilder1.UseTaperedShank = true;


            NXOpen.NXObject nXObject2;
            nXObject2 = millToolBuilder1.Commit();



            millToolBuilder1.Destroy();



            theSession.CAMSession.Utils.SetInspectionIntent(false);

            NXOpen.CAM.CAMObject[] objectsToBeMoved2 = new NXOpen.CAM.CAMObject[1];
            NXOpen.CAM.CavityMilling cavityMilling2 = ((NXOpen.CAM.CavityMilling)nXObject1);
            objectsToBeMoved2[0] = cavityMilling2;
            NXOpen.CAM.Tool tool2 = ((NXOpen.CAM.Tool)nXObject2);
            workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved2, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);

            NXOpen.CAM.CAMObject[] objectsToBeMoved3 = new NXOpen.CAM.CAMObject[1];
            objectsToBeMoved3[0] = cavityMilling2;
            workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved3, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);

            cavityMillingBuilder1.Destroy();


            NXOpen.CAM.CavityMillingBuilder cavityMillingBuilder2;
            cavityMillingBuilder2 = workPart.CAMSetup.CAMOperationCollection.CreateCavityMillingBuilder(cavityMilling2);


            bool isupdated2;
            isupdated2 = cavityMillingBuilder2.CutLevel.InitializeData();


            Body myBody = SelectBody();
            cavityMillingBuilder2.CutAreaGeometry.InitializeData(false);
            NXOpen.CAM.GeometrySetList geometrySetList1 = cavityMillingBuilder2.CutAreaGeometry.GeometryList;

            TaggedObject taggedObject1 = geometrySetList1.FindItem(0);
            NXOpen.CAM.GeometrySet geometrySet1 = (NXOpen.CAM.GeometrySet)taggedObject1;

            SelectionIntentRuleOptions options = workPart.ScRuleFactory.CreateRuleOptions();
            options.SetSelectedFromInactive(false);

            NXOpen.Body[] bodies1 = new NXOpen.Body[1];
            bodies1[0] = myBody;
            NXOpen.BodyDumbRule rule = workPart.ScRuleFactory.CreateRuleBodyDumb(bodies1);
            options.Dispose();
            NXOpen.ScCollector scCollector1 = geometrySet1.ScCollector;
            scCollector1.ReplaceRules(new NXOpen.SelectionIntentRule[] { rule }, false);

            nXObject2 = cavityMillingBuilder2.Commit();





            // ----------------------------------------------
            //   Dialog Begin Cavity Mill - [CAVITY_MILL]
            // ----------------------------------------------
            cavityMillingBuilder2.CutPattern.CutPattern = NXOpen.CAM.CutPatternBuilder.Types.FollowPeriphery;

            cavityMillingBuilder2.CutLevel.GlobalDepthPerCut.DistanceBuilder.Value = 0.5;

            cavityMillingBuilder2.CutLevel.ApplyGlobalDepthPerCut();

            cavityMillingBuilder2.CutParameters.CutOrder = NXOpen.CAM.CutParametersCutOrderTypes.DepthFirst;

            cavityMillingBuilder2.WallCleanupType = NXOpen.CAM.MillOperationBuilder.WallCleanupTypes.None;

            // ----------------------------------------------
            //   Dialog Begin Cavity Mill - [CAVITY_MILL]
            // ----------------------------------------------
            cavityMillingBuilder2.CutParameters.FloorSameAsPartStock = false;

            cavityMillingBuilder2.CutParameters.PartStock.Value = 0.25;

            cavityMillingBuilder2.CutParameters.FloorStock.Value = 0.10000000000000001;

            cavityMillingBuilder2.CutParameters.BlankStock.Value = 5.0;

            // ----------------------------------------------
            //   Dialog Begin Cavity Mill - [CAVITY_MILL]
            // ----------------------------------------------
            // ----------------------------------------------
            //   Dialog Begin Cavity Mill - [CAVITY_MILL]
            // ----------------------------------------------
            cavityMillingBuilder2.FeedsBuilder.SurfaceSpeedBuilder.Value = 200.0;

            NXOpen.Direction nullNXOpen_Direction = null;
            cavityMillingBuilder2.NonCuttingBuilder.TransferAvoidanceFromBuilder.ToolAxis = nullNXOpen_Direction;

            cavityMillingBuilder2.NonCuttingBuilder.TransferAvoidanceGohomeBuilder.ToolAxis = nullNXOpen_Direction;


            cavityMillingBuilder2.NonCuttingBuilder.ClearanceBuilder.AxisObject = nullNXOpen_Direction;


            //   Dialog Begin Cavity Mill - [CAVITY_MILL]
            // ----------------------------------------------
            cavityMillingBuilder2.NonCuttingBuilder.EngageClosedAreaBuilder.EngRetType = NXOpen.CAM.NcmPlanarEngRetBuilder.EngRetTypes.RampOnShape;

            cavityMillingBuilder2.NonCuttingBuilder.EngageClosedAreaBuilder.HelicalRampAngleBuilder.Value = 3.0;

            cavityMillingBuilder2.NonCuttingBuilder.EngageClosedAreaBuilder.HeightBuilder.Value = 1.0;

            cavityMillingBuilder2.NonCuttingBuilder.EngageClosedAreaBuilder.MinRampLengthBuilder.Intent = NXOpen.CAM.ParamValueIntent.PartUnits;

            cavityMillingBuilder2.NonCuttingBuilder.EngageClosedAreaBuilder.MinRampLengthBuilder.Value = 0.5;

            cavityMillingBuilder2.NonCuttingBuilder.EngageOpenAreaBuilder.EngRetType = NXOpen.CAM.NcmPlanarEngRetBuilder.EngRetTypes.None;

            cavityMillingBuilder2.NonCuttingBuilder.TransferAvoidanceFromBuilder.ToolAxis = nullNXOpen_Direction;

            cavityMillingBuilder2.NonCuttingBuilder.TransferAvoidanceGohomeBuilder.ToolAxis = nullNXOpen_Direction;



            cavityMillingBuilder2.NonCuttingBuilder.ClearanceBuilder.AxisObject = nullNXOpen_Direction;

            // ----------------------------------------------
            //   Dialog Begin Cavity Mill - [CAVITY_MILL]
            // ----------------------------------------------
            cavityMillingBuilder2.NonCuttingBuilder.RetractAreaBuilder.EngRetType = NXOpen.CAM.NcmPlanarEngRetBuilder.EngRetTypes.None;

            cavityMillingBuilder2.NonCuttingBuilder.TransferAvoidanceFromBuilder.ToolAxis = nullNXOpen_Direction;

            cavityMillingBuilder2.NonCuttingBuilder.TransferAvoidanceGohomeBuilder.ToolAxis = nullNXOpen_Direction;



            cavityMillingBuilder2.NonCuttingBuilder.ClearanceBuilder.AxisObject = nullNXOpen_Direction;

            // ----------------------------------------------
            //   Dialog Begin Cavity Mill - [CAVITY_MILL]
            // ----------------------------------------------
            // ----------------------------------------------
            //   Dialog Begin Cavity Mill - [CAVITY_MILL]
            // ----------------------------------------------
            cavityMillingBuilder2.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

            cavityMillingBuilder2.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;

            cavityMillingBuilder2.NonCuttingBuilder.TransferBetweenRegionsBuilder.Type = NXOpen.CAM.NcmTransfer.TransferTypes.PrevPlane;

            cavityMillingBuilder2.NonCuttingBuilder.TransferBetweenRegionsBuilder.SafeDistanceBuilder.Value = 0.5;

            cavityMillingBuilder2.NonCuttingBuilder.TransferWithinLevelsType = NXOpen.CAM.NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;

            cavityMillingBuilder2.NonCuttingBuilder.TransferWithinLevelsSafeDistanceBuilder.Value = 0.0;

            NXOpen.NXObject nXObject3;
            nXObject3 = cavityMillingBuilder2.Commit();


            //  GENERATE TOOLPATH
            NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
            NXOpen.CAM.CavityMilling cavityMilling3 = ((NXOpen.CAM.CavityMilling)nXObject3);
            objects1[0] = cavityMilling3;
            workPart.CAMSetup.GenerateToolPath(objects1);


            NXOpen.CAM.CavityMillingBuilder cavityMillingBuilder3;
            cavityMillingBuilder3 = workPart.CAMSetup.CAMOperationCollection.CreateCavityMillingBuilder(cavityMilling3);

            bool isupdated3;
            isupdated3 = cavityMillingBuilder3.CutLevel.InitializeData();

            NXOpen.NXObject nXObject4;
            nXObject4 = cavityMillingBuilder3.Commit();


            cavityMillingBuilder3.Destroy();

        }

        static Body SelectBody()
        {


            Session theSession = Session.GetSession();
            UI theUI1 = UI.GetUI();
            theSession.ListingWindow.WriteLine("Select Body");

            Selection.MaskTriple mask = new Selection.MaskTriple(
                UFConstants.UF_solid_type, 0, UFConstants.BODY);

            TaggedObject obj;
            Point3d cursor;

            Selection.Response resp = theUI1.SelectionManager.SelectTaggedObject(
                 "Select Body", "Body",
                 Selection.SelectionScope.WorkPart,
                 Selection.SelectionAction.ClearAndEnableSpecific,
                 false, false,
                 new[] { mask }, out obj, out cursor);
            return (Body)obj;

        }
        public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }


    }

}

