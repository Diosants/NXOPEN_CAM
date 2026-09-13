
using NXOpen;
using NXOpen.CAM;
using NXOpen.Features;
using NXOpen.Features.AECDesign;
using NXOpen.UF;
using System;

public class FINISHINGFACE
{


    public static void Run(string[] args)
    {
        Session theSession = Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;
        UI theUI = UI.GetUI();

        NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
        NXOpen.CAM.Method method1 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("METHOD"));
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));
        NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "mill_planar", "FACE_MILL_ZIGZAG", NXOpen.CAM.OperationCollection.UseDefaultName.True, "FACE_MILL_ZIGZAG", "Face Mill Zigzag");
        theSession.CAMSession.PathDisplay.ShowToolPath(operation1);
        theSession.CAMSession.PathDisplay.Jump(false);


        NXOpen.CAM.SurfaceContour surfaceContour1 = ((NXOpen.CAM.SurfaceContour)operation1);
        NXOpen.CAM.FacingZigZagBuilder facingZigZagBuilder1;
        facingZigZagBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateFacingZigZagBuilder(surfaceContour1);

        NXOpen.NXObject nXObject1;
        nXObject1 = facingZigZagBuilder1.Commit();


        //  DIALOG BEGIN CUT AREA
        Face myface = SelectAnyFace();

        NXOpen.TaggedObject taggedObject27;
        taggedObject27 = facingZigZagBuilder1.GetCustomizableItemBuilder("Geometry - Cut Area");

        NXOpen.CAM.Geometry geometry1 = ((NXOpen.CAM.Geometry)taggedObject27);
        geometry1.InitializeData(false);

        NXOpen.CAM.GeometrySetList geometrySetList1 = geometry1.GeometryList;
        NXOpen.CAM.GeometrySet geometrySet1 = (NXOpen.CAM.GeometrySet)geometrySetList1.FindItem(0);

        NXOpen.SelectionIntentRuleOptions options = workPart.ScRuleFactory.CreateRuleOptions();
        options.SetSelectedFromInactive(false);

        NXOpen.Face[] faces1 = new NXOpen.Face[1];
        faces1[0] = myface;

        NXOpen.FaceDumbRule rule = workPart.ScRuleFactory.CreateRuleFaceDumb(faces1, options);

        options.Dispose();

        NXOpen.ScCollector scCollector1 = geometrySet1.ScCollector;
        scCollector1.ReplaceRules(new NXOpen.SelectionIntentRule[] { rule }, false);
        // END DIALOG CUT AREA



        //    LOAD TOOL
        NXOpen.CAM.Tool tool1;
        bool success1;
        tool1 = workPart.CAMSetup.RetrieveTool("ugt0212_002", out success1);

        NXOpen.CAM.CAMObject[] objectsToBeMoved1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.SurfaceContour surfaceContour2 = ((NXOpen.CAM.SurfaceContour)nXObject1);
        objectsToBeMoved1[0] = surfaceContour2;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved1, tool1, NXOpen.CAM.CAMSetup.Paste.Inside);

        NXOpen.CAM.MillToolBuilder millToolBuilder1;
        millToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool1);


        millToolBuilder1.TlNumberBuilder.Value = 2; // NUMERO DA FERRAMENTA

        millToolBuilder1.TlAdjRegBuilder.Value = 2; // CORRETOR DA FERRAMENTA

        millToolBuilder1.TlCutcomRegBuilder.Value = 2;//  COMPENSACAO

        NXOpen.NXObject nXObject3;
        nXObject3 = millToolBuilder1.Commit();


        millToolBuilder1.Destroy();

        ///////   END LOAD TOOL

        theSession.CAMSession.Utils.SetInspectionIntent(false);


        TaggedObject taggedObject001;
        taggedObject001 = facingZigZagBuilder1.GetCustomizableItemBuilder("Cut Angle");
        facingZigZagBuilder1.CutParameters.CutAngle.Value = 45.0;

        RotateAngleBuilder rotateAngleBuilder1 = (RotateAngleBuilder)taggedObject001;

        NXOpen.TaggedObject taggedObject1;
        taggedObject1 = facingZigZagBuilder1.GetCustomizableItemBuilder("Floor Blank Thickness");

        NXOpen.CAM.LevelPlaneBuilder levelPlaneBuilder1 = ((NXOpen.CAM.LevelPlaneBuilder)taggedObject1);
        levelPlaneBuilder1.PlaneDist = 0.5;

        TaggedObject taggedObject100;
        taggedObject100 = facingZigZagBuilder1.GetCustomizableItemBuilder("Floor Stock");
        ElementDoubleBuilder elementDoubleBuilder1 = (ElementDoubleBuilder)taggedObject100;
        elementDoubleBuilder1.Value = 0.0;


        NXOpen.TaggedObject taggedObject2;
        taggedObject2 = facingZigZagBuilder1.GetCustomizableItemBuilder("Cut Level Distance");

        NXOpen.CAM.InheritableToolDepBuilder inheritableToolDepBuilder1 = ((NXOpen.CAM.InheritableToolDepBuilder)taggedObject2);
        inheritableToolDepBuilder1.Intent = NXOpen.CAM.ParamValueIntent.PartUnits;

        NXOpen.TaggedObject taggedObject3;
        taggedObject3 = facingZigZagBuilder1.GetCustomizableItemBuilder("Cut Level Distance");

        NXOpen.CAM.InheritableToolDepBuilder inheritableToolDepBuilder2 = ((NXOpen.CAM.InheritableToolDepBuilder)taggedObject3);
        NXOpen.TaggedObject taggedObject4;
        taggedObject4 = facingZigZagBuilder1.GetCustomizableItemBuilder("Cut Level Distance");

        NXOpen.CAM.InheritableToolDepBuilder inheritableToolDepBuilder3 = ((NXOpen.CAM.InheritableToolDepBuilder)taggedObject4);
        inheritableToolDepBuilder3.Value = 0.5;

        NXOpen.TaggedObject taggedObject5;
        taggedObject5 = facingZigZagBuilder1.GetCustomizableItemBuilder("Cut Level Distance");

        NXOpen.CAM.InheritableToolDepBuilder inheritableToolDepBuilder4 = ((NXOpen.CAM.InheritableToolDepBuilder)taggedObject5);

        facingZigZagBuilder1.FeedsBuilder.SpindleRpmBuilder.Value = 1200.0;
        facingZigZagBuilder1.FeedsBuilder.FeedCutBuilder.Value = 600;
        facingZigZagBuilder1.FeedsBuilder.RecalculateData(FeedsBuilder.RecalculateBasedOn.CutFeedRate);

        //  GENERATE TOOLPATH
        NXOpen.NXObject nXObject4;
        nXObject4 = facingZigZagBuilder1.Commit();
        NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.SurfaceContour surfaceContour3 = ((NXOpen.CAM.SurfaceContour)nXObject4);
        objects1[0] = surfaceContour3;
        workPart.CAMSetup.GenerateToolPath(objects1);
        facingZigZagBuilder1.Destroy();


    }
    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }

    //  FUNCTION SELECT FACE

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
