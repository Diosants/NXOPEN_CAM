// NX 2406
// Journal created by dcard on Fri Feb 20 20:13:06 2026 Eastern Standard Time
//
using NXOpen;
using NXOpen.CAM;
using NXOpen.UF;
using System;

public class NXZLevelProfile
{
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;

        //   Menu: Insert->Operation...
        // ----------------------------------------------


        NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
        NXOpen.CAM.Method method1 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("METHOD"));
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));
        NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "mill_contour", "ZLEVEL_PROFILE_STEEP", NXOpen.CAM.OperationCollection.UseDefaultName.True, "ZLEVEL_PROFILE_STEEP_1", "Zlevel Profile Steep 1");



        NXOpen.CAM.ZLevelMilling zLevelMilling1 = ((NXOpen.CAM.ZLevelMilling)operation1);
        NXOpen.CAM.ZLevelMillingBuilder zLevelMillingBuilder1;
        zLevelMillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateZlevelMillingBuilder(zLevelMilling1);
        NXOpen.NXObject nXObject1;
        zLevelMillingBuilder1.CutAreaGeometry.InitializeData(false);

        // Selecionar face manualmente
        Face myface = SelectAnyFace();

        zLevelMillingBuilder1.CutAreaGeometry.InitializeData(false);

        NXOpen.CAM.GeometrySetList geometrySetList1;
        geometrySetList1 = zLevelMillingBuilder1.CutAreaGeometry.GeometryList;

        NXOpen.TaggedObject taggedObject1;
        taggedObject1 = geometrySetList1.FindItem(0);

        NXOpen.CAM.GeometrySet geometrySet1 = ((NXOpen.CAM.GeometrySet)taggedObject1);

        // Criar regra de seleção
        NXOpen.SelectionIntentRuleOptions options;
        options = workPart.ScRuleFactory.CreateRuleOptions();

        options.SetSelectedFromInactive(false);

        NXOpen.Face[] faces1 = new NXOpen.Face[1];
        faces1[0] = myface;

        NXOpen.FaceDumbRule rule;
        rule = workPart.ScRuleFactory.CreateRuleFaceDumb(faces1, options);

        options.Dispose();

        // Aplicar no collector
        NXOpen.ScCollector scCollector1 = geometrySet1.ScCollector;
        scCollector1.ReplaceRules(new NXOpen.SelectionIntentRule[] { rule }, false);

        nXObject1 = zLevelMillingBuilder1.Commit();
   


        // ----------------------------------------------
        //   Dialog Begin New Tool
        // ----------------------------------------------
        NXOpen.CAM.NCGroup nCGroup3 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("GENERIC_MACHINE"));
        NXOpen.CAM.NCGroup nCGroup4 = null;
        NXOpen.NXObject nXObject2 = null;

        // Verifica se a ferramenta já existe pelo nome (evita criar duplicada)
        NXOpen.TaggedObject foundTool = workPart.CAMSetup.CAMGroupCollection.FindObject("CAB16R.8");
        if (foundTool == null)
        {
            // Cria nova ferramenta
            nCGroup4 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "mill_contour", "MILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "CAB16R.8", "Mill");

            NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)nCGroup4);

            NXOpen.CAM.CAMObject[] objectsToBeMoved1 = new NXOpen.CAM.CAMObject[1];
            objectsToBeMoved1[0] = tool1;
            workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved1, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);

            //  Criando Ferramenta
            NXOpen.CAM.MillToolBuilder millToolBuilder1;
            millToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool1);

            // ----------------------------------------------
            //   Dialog Begin Milling Tool-5 Parameters
            // ----------------------------------------------

               
            millToolBuilder1.TlDiameterBuilder.Value = 16.0;
            millToolBuilder1.TlCor1RadBuilder.Value = 0.80000000000000004;
            millToolBuilder1.TlNumberBuilder.Value = 5;
            millToolBuilder1.TlAdjRegBuilder.Value = 5;
            millToolBuilder1.TlCutcomRegBuilder.Value = 5;

            nXObject2 = millToolBuilder1.Commit();
            millToolBuilder1.Destroy();
        }
        else
        {
            // Reusa a ferramenta existente
            nCGroup4 = (NXOpen.CAM.NCGroup)foundTool;
            nXObject2 = (NXOpen.NXObject)((NXOpen.CAM.Tool)nCGroup4);
        }

      


        theSession.CAMSession.Utils.SetInspectionIntent(false);

        NXOpen.CAM.CAMObject[] objectsToBeMoved2 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.ZLevelMilling zLevelMilling2 = ((NXOpen.CAM.ZLevelMilling)nXObject1);
        objectsToBeMoved2[0] = zLevelMilling2;
        NXOpen.CAM.Tool tool2 = ((NXOpen.CAM.Tool)nXObject2);
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved2, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.CAMObject[] objectsToBeMoved3 = new NXOpen.CAM.CAMObject[1];
        objectsToBeMoved3[0] = zLevelMilling2;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved3, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);


        NXOpen.CAM.ZLevelMillingBuilder zLevelMillingBuilder2;
        zLevelMillingBuilder2 = workPart.CAMSetup.CAMOperationCollection.CreateZlevelMillingBuilder(zLevelMilling2);



        bool isupdated2;
        isupdated2 = zLevelMillingBuilder2.CutLevel.InitializeData();

        zLevelMillingBuilder2.CutParameters.SteepContainment.Type = NXOpen.CAM.SteepContainment.Types.None;

        zLevelMillingBuilder2.CutLevel.GlobalDepthPerCut.DistanceBuilder.Intent = NXOpen.CAM.ParamValueIntent.PartUnits;

        zLevelMillingBuilder2.CutLevel.ApplyGlobalDepthPerCut();

        zLevelMillingBuilder2.CutLevel.GlobalDepthPerCut.DistanceBuilder.Value = 0.5;

        zLevelMillingBuilder2.CutLevel.ApplyGlobalDepthPerCut();


        //  Cut Direction

        zLevelMillingBuilder2.CutParameters.CutDirection.Type =  NXOpen.CAM.CutDirection.Types.Mixed;
        zLevelMillingBuilder2.CutParameters.CutOrder = NXOpen.CAM.CutParametersCutOrderTypes.DepthFirstAlways;

        zLevelMillingBuilder2.FeedsBuilder.SurfaceSpeedBuilder.Value = 200.0;

        NXOpen.Direction nullNXOpen_Direction = null;
        zLevelMillingBuilder2.NonCuttingBuilder.ClearanceBuilder.AxisObject = nullNXOpen_Direction;

        zLevelMillingBuilder2.FeedsBuilder.FeedCutBuilder.Value = 1500.0;


        // ----------------------------------------------
        zLevelMillingBuilder2.NonCuttingBuilder.EngageClosedAreaBuilder.EngRetType = NXOpen.CAM.NcmPlanarEngRetBuilder.EngRetTypes.SameAsEngage;

        // ----------------------------------------------
        //   Dialog Begin Zlevel Profile Steep - [ZLEVEL_PROFILE_STEEP_1]
        // ----------------------------------------------
        zLevelMillingBuilder2.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;

        zLevelMillingBuilder2.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;

        zLevelMillingBuilder2.NonCuttingBuilder.TransferBetweenRegionsBuilder.Type = NXOpen.CAM.NcmTransfer.TransferTypes.PrevPlane;

        zLevelMillingBuilder2.NonCuttingBuilder.TransferBetweenRegionsBuilder.SafeDistanceBuilder.Value = 0.5;

        zLevelMillingBuilder2.NonCuttingBuilder.TransferWithinLevelsType = NXOpen.CAM.NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;

        zLevelMillingBuilder2.NonCuttingBuilder.TransferWithinLevelsSafeDistanceBuilder.Value = 0.0;

        NXOpen.NXObject nXObject3;
        nXObject3 = zLevelMillingBuilder2.Commit();

        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.ZLevelMilling zLevelMilling3 = ((NXOpen.CAM.ZLevelMilling)nXObject3);
        objects1[0] = zLevelMilling3;
        workPart.CAMSetup.GenerateToolPath(objects1);


        zLevelMillingBuilder2.Destroy();


        NXOpen.CAM.ZLevelMillingBuilder zLevelMillingBuilder3;
        zLevelMillingBuilder3 = workPart.CAMSetup.CAMOperationCollection.CreateZlevelMillingBuilder(zLevelMilling3);



        bool isupdated3;
        isupdated3 = zLevelMillingBuilder3.CutLevel.InitializeData();


        NXOpen.NXObject nXObject4;
        nXObject4 = zLevelMillingBuilder3.Commit();

        zLevelMillingBuilder3.Destroy();



    }
    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }

    //  function to get any face to be machinned

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
