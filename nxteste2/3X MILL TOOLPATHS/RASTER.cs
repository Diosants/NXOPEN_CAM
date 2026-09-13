
using NXOpen;
using System;

public class RASTER
{
    public static void Run(string[] args)
    {
       

        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;

        NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
        NXOpen.CAM.Method method1 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("MILL_FINISH"));
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));
        NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "mill_contour", "AREA_MILL", NXOpen.CAM.OperationCollection.UseDefaultName.False, "RASTER", "Area Mill");

     
        NXOpen.CAM.SurfaceContour surfaceContour1 = ((NXOpen.CAM.SurfaceContour)operation1);
        NXOpen.CAM.SurfaceContourBuilder surfaceContourBuilder1;
        surfaceContourBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateSurfaceContourBuilder(surfaceContour1);

        try
        {

            surfaceContourBuilder1.DmareaMillingBuilder.SteepCutting.DepthPerCut.StepoverType = NXOpen.CAM.StepoverBuilder.StepoverTypes.Constant;

        // Additional area milling settings consolidated into the first SurfaceContourBuilder
        surfaceContourBuilder1.DmareaMillingBuilder.AmSteepOption = NXOpen.CAM.DmAmBuilder.SteepOptTypes.NonSteepNonDirectional;
        surfaceContourBuilder1.DmareaMillingBuilder.StepoverBuilder.DistanceBuilder.Intent = NXOpen.CAM.ParamValueIntent.PartUnits;
        surfaceContourBuilder1.DmareaMillingBuilder.StepoverBuilder.DistanceBuilder.Value = 0.25;
        surfaceContourBuilder1.DmareaMillingBuilder.CutPatternBuilder.CutPattern = NXOpen.CAM.CutPatternBuilder.Types.ZigZag;

        NXOpen.CAM.CutAngle cutAngle1;
        cutAngle1 = surfaceContourBuilder1.DmareaMillingBuilder.NonSteepCutting.CutAngleBuilder;
        cutAngle1.Type = NXOpen.CAM.CutAngle.Types.Specify;
        cutAngle1.Value = 90.0;

        // ----------------------------------------------
        //   Cut Area selection (moved to the initial builder)
        // ----------------------------------------------
        surfaceContourBuilder1.CutAreaGeometry.InitializeData(false);

        NXOpen.CAM.GeometrySetList geometrySetList1;
        geometrySetList1 = surfaceContourBuilder1.CutAreaGeometry.GeometryList;

        NXOpen.TaggedObject taggedObject1;
        taggedObject1 = geometrySetList1.FindItem(0);

        NXOpen.CAM.GeometrySet geometrySet1 = ((NXOpen.CAM.GeometrySet)taggedObject1);

        //  COLOCAR A FUNCAO DE SELECIONAR A FACE AQUI

          var  faces  =  SelectAnyFace();
        if (faces == null || faces.Length == 0)
        {
            UI.GetUI().NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error,
                "No faces selected. Operation cancelled.");
            surfaceContourBuilder1.Destroy();
            return;
        }



        // Apply selected faces to geometry collector
        var geomList = surfaceContourBuilder1.CutAreaGeometry.GeometryList;
        var geomSet = (NXOpen.CAM.GeometrySet)geomList.FindItem(0);

        var ruleOptions = workPart.ScRuleFactory.CreateRuleOptions();
        ruleOptions.SetSelectedFromInactive(false);

        var faceRule = workPart.ScRuleFactory.CreateRuleFaceDumb(faces, ruleOptions);
        ruleOptions.Dispose();

        var scCollector = geomSet.ScCollector;
        scCollector.ReplaceRules(new NXOpen.SelectionIntentRule[] { faceRule }, false);
        // Commit will be executed once after all configuration (see below)
    

        // Feeds and non-cutting settings
        surfaceContourBuilder1.FeedsBuilder.SpindleRpmBuilder.Value = 8000.0;
        surfaceContourBuilder1.FeedsBuilder.FeedCutBuilder.Value = 1000.0;
        surfaceContourBuilder1.NonCuttingBuilder.TransferCommonClearanceBuilder.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
        surfaceContourBuilder1.NonCuttingBuilder.TransferCommonClearanceBuilder.SafeDistance = 50.0;

        NXOpen.NXObject nXObject1;
        nXObject1 = surfaceContourBuilder1.Commit();

        //  CRIAR FERRAMENTA



        NXOpen.CAM.NCGroup nCGroup3 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("GENERIC_MACHINE"));
        NXOpen.CAM.NCGroup nCGroup4;
        nCGroup4 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "mill_contour", "BALL_MILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.True, "BALL_MILL", "Ball Mill");

        NXOpen.CAM.CAMObject[] objectsToBeMoved1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)nCGroup4);
        objectsToBeMoved1[0] = tool1;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved1, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.MillToolBuilder millToolBuilder1;
        millToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool1);


        millToolBuilder1.TlDiameterBuilder.Value = 10.0;

        millToolBuilder1.TlNumberBuilder.Value = 10;

        millToolBuilder1.TlAdjRegBuilder.Value = 10;

        millToolBuilder1.TlCutcomRegBuilder.Value = 10;

        NXOpen.NXObject nXObject2;
        nXObject2 = millToolBuilder1.Commit();
        millToolBuilder1.Destroy();


        theSession.CAMSession.Utils.SetInspectionIntent(false);


        NXOpen.CAM.CAMObject[] objectsToBeMoved2 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.SurfaceContour surfaceContour2 = ((NXOpen.CAM.SurfaceContour)nXObject1);
        objectsToBeMoved2[0] = surfaceContour2;
        NXOpen.CAM.Tool tool2 = ((NXOpen.CAM.Tool)nXObject2);
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved2, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);

        // Generate toolpath for the single consolidated operation
        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.SurfaceContour surfaceContour4 = ((NXOpen.CAM.SurfaceContour)nXObject1);
        objects1[0] = surfaceContour4;
        workPart.CAMSetup.GenerateToolPath(objects1);

        }
        catch (Exception ex)
        {
            try { UI.GetUI().NXMessageBox.Show("RASTER", NXOpen.NXMessageBox.DialogType.Error, ex.Message); } catch { }
        }
        finally
        {
            try { if (surfaceContourBuilder1 != null) surfaceContourBuilder1.Destroy(); } catch { }
        }

    }

    static Face[] SelectAnyFace()
    {
        Session theSession = Session.GetSession();
        UI theUI = UI.GetUI();

        theSession.ListingWindow.Open();
        theSession.ListingWindow.WriteLine(
            "Select Surfaces to be Machined (press OK when done)");

        Selection.MaskTriple[] mask = new Selection.MaskTriple[1];

        Selection.MaskTriple mask1;

        mask1.Type =
            NXOpen.UF.UFConstants.UF_solid_type;

        mask1.Subtype =
            NXOpen.UF.UFConstants.UF_solid_face_subtype;

        mask1.SolidBodySubtype =
            NXOpen.UF.UFConstants.UF_UI_SEL_FEATURE_ANY_FACE;

        mask[0] = mask1;

        TaggedObject[] objs;

        //=========================================================
        // MULTI SELECTION
        //=========================================================

        Selection.Response resp =
            theUI.SelectionManager.SelectTaggedObjects(
                "Select faces",
                "Faces",
                Selection.SelectionScope.WorkPart,
                Selection.SelectionAction.ClearAndEnableSpecific,
                false,
                false,
                mask,
                out objs
            );

        //=========================================================
        // CANCEL
        //=========================================================

        if (resp == Selection.Response.Cancel ||
            objs == null ||
            objs.Length == 0)
        {
            return new Face[0];
        }

        //=========================================================
        // CONVERT PARA FACE[]
        //=========================================================

        Face[] faces = new Face[objs.Length];

        for (int i = 0; i < objs.Length; i++)
        {
            faces[i] = (Face)objs[i];
        }

        return faces;
    }




    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
}
