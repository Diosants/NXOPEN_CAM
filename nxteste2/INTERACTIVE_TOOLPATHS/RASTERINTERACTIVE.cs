
using NXOpen;
using NXOpen.BlockStyler;
using System;

public class RASTERINTERACTIVE
{
    //class members
    private static Session theSession = null;
    private static UI theUI = null;
    private string theDlxFileName;
    private NXOpen.BlockStyler.BlockDialog theDialog;
    private NXOpen.BlockStyler.Group group0;
    private NXOpen.BlockStyler.Label label0;// Block type: Label
    private NXOpen.BlockStyler.Separator separator0;// Block type: Separator
    private NXOpen.BlockStyler.DoubleBlock doubleToolDiameter;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleCornerRadius;// Block type: Double
    private NXOpen.BlockStyler.IntegerBlock integerToolNumber;// Block type: Integer
    private NXOpen.BlockStyler.DoubleBlock doubleRPM;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleFeedRate;// Block type: Double
    private NXOpen.BlockStyler.Separator separator01;// Block type: Separator
    private NXOpen.BlockStyler.Label label01;// Block type: Label
    private NXOpen.BlockStyler.DoubleBlock doubleStepover;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleCutAngle;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleSafeDistance;// Block type: Double

    //------------------------------------------------------------------------------
    //Constructor for NX Styler class
    //------------------------------------------------------------------------------
    public RASTERINTERACTIVE()
    {
        try
        {
            theSession = Session.GetSession();
            theUI = UI.GetUI();
            theDlxFileName = "C:\\Users\\dcard\\OneDrive\\Desktop\\NXUltimate\\Block-Style\\Raster.dlx";
            theDialog = theUI.CreateDialog(theDlxFileName);
            theDialog.AddApplyHandler(new NXOpen.BlockStyler.BlockDialog.Apply(apply_cb));
            theDialog.AddOkHandler(new NXOpen.BlockStyler.BlockDialog.Ok(ok_cb));
            theDialog.AddUpdateHandler(new NXOpen.BlockStyler.BlockDialog.Update(update_cb));
            theDialog.AddInitializeHandler(new NXOpen.BlockStyler.BlockDialog.Initialize(initialize_cb));
            theDialog.AddDialogShownHandler(new NXOpen.BlockStyler.BlockDialog.DialogShown(dialogShown_cb));
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            throw ex;
        }
    }

    // Strategy pattern support types
    public interface IPathStrategy
    {
        void Apply(NXOpen.CAM.SurfaceContourBuilder builder, RasterParameters p);
    }

    public class RasterParameters
    {
        public double Stepover { get; set; }
        public double CutAngle { get; set; }
        public double SafeDistance { get; set; }
        public double RPM { get; set; }
        public double FeedRate { get; set; }
        public double ToolDiameter { get; set; }
        public int ToolNumber { get; set; }
    }

    public class ZigZagPathStrategy : IPathStrategy
    {
        public void Apply(NXOpen.CAM.SurfaceContourBuilder builder, RasterParameters p)
        {
            try
            {
                builder.DmareaMillingBuilder.StepoverBuilder.DistanceBuilder.Value = p.Stepover;
                builder.DmareaMillingBuilder.CutPatternBuilder.CutPattern = NXOpen.CAM.CutPatternBuilder.Types.ZigZag;

                var cutAngle = builder.DmareaMillingBuilder.NonSteepCutting.CutAngleBuilder;
                cutAngle.Type = NXOpen.CAM.CutAngle.Types.Specify;
                cutAngle.Value = p.CutAngle;
            }
            catch (Exception ex)
            {
                try { UI.GetUI().NXMessageBox.Show("Strategy", NXOpen.NXMessageBox.DialogType.Error, ex.Message); } catch { }
            }
        }
    }

    public class ParallelPathStrategy : IPathStrategy
    {
        public void Apply(NXOpen.CAM.SurfaceContourBuilder builder, RasterParameters p)
        {
            try
            {
                builder.DmareaMillingBuilder.StepoverBuilder.DistanceBuilder.Value = p.Stepover;
                // Fallback to ZigZag enum if a parallel/offset enum is not available in this context
                builder.DmareaMillingBuilder.CutPatternBuilder.CutPattern = NXOpen.CAM.CutPatternBuilder.Types.ZigZag;

                var cutAngle = builder.DmareaMillingBuilder.NonSteepCutting.CutAngleBuilder;
                cutAngle.Type = NXOpen.CAM.CutAngle.Types.Specify;
                // For a parallel style we might flip or offset the cut angle slightly
                cutAngle.Value = p.CutAngle + 0.0;
            }
            catch (Exception ex)
            {
                try { UI.GetUI().NXMessageBox.Show("Strategy", NXOpen.NXMessageBox.DialogType.Error, ex.Message); } catch { }
            }
        }
    }


    public static void Run(string[]args )
    {
        RASTERINTERACTIVE theRASTERINTERACTIVE = null;
        try
        {
            theRASTERINTERACTIVE = new RASTERINTERACTIVE ();
            // The following method shows the dialog immediately
            theRASTERINTERACTIVE.Launch();
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        finally
        {
            if (theRASTERINTERACTIVE != null)
                theRASTERINTERACTIVE.Dispose();
            theRASTERINTERACTIVE = null;
        }
    }

    public static int GetUnloadOption(string arg)
    {
        //return System.Convert.ToInt32(Session.LibraryUnloadOption.Explicitly);
        return System.Convert.ToInt32(Session.LibraryUnloadOption.Immediately);
        // return System.Convert.ToInt32(Session.LibraryUnloadOption.AtTermination);
    }


    public static void UnloadLibrary(string arg)
    {
        try
        {
            //---- Enter your code here -----
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
    }


    public NXOpen.BlockStyler.BlockDialog.DialogResponse Launch()
    {
        NXOpen.BlockStyler.BlockDialog.DialogResponse dialogResponse = NXOpen.BlockStyler.BlockDialog.DialogResponse.Invalid;
        try
        {
            dialogResponse = theDialog.Launch();
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        return dialogResponse;
    }


    public void Dispose()
    {
        if (theDialog != null)
        {
            theDialog.Dispose();
            theDialog = null;
        }
    }


    public void initialize_cb()
    {
        try
        {
            group0 = (NXOpen.BlockStyler.Group)theDialog.TopBlock.FindBlock("group0");
            label0 = (NXOpen.BlockStyler.Label)theDialog.TopBlock.FindBlock("label0");
            separator0 = (NXOpen.BlockStyler.Separator)theDialog.TopBlock.FindBlock("separator0");
            doubleToolDiameter = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleToolDiameter");
            doubleCornerRadius = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleCornerRadius");
            integerToolNumber = (NXOpen.BlockStyler.IntegerBlock)theDialog.TopBlock.FindBlock("integerToolNumber");
            doubleRPM = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleRPM");
            doubleFeedRate = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleFeedRate");
            separator01 = (NXOpen.BlockStyler.Separator)theDialog.TopBlock.FindBlock("separator01");
            label01 = (NXOpen.BlockStyler.Label)theDialog.TopBlock.FindBlock("label01");
            doubleStepover = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleStepover");
            doubleCutAngle = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleCutAngle");
            doubleSafeDistance = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleSafeDistance");
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
    }

    public void dialogShown_cb()
    {
        try
        {
            //---- Enter your callback code here -----
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
    }


    public int apply_cb()
    {
        int errorCode = 0;
        try
        {
            //---- Enter your callback code here -----
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            errorCode = 1;
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        return errorCode;
    }

    public int update_cb(NXOpen.BlockStyler.UIBlock block)
    {
        try
        {
            if (block == label0)
            {
                //---------Enter your code here-----------
            }
            else if (block == separator0)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleToolDiameter)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleCornerRadius)
            {
                //---------Enter your code here-----------
            }
            else if (block == integerToolNumber)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleRPM)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleFeedRate)
            {
                //---------Enter your code here-----------
            }
            else if (block == separator01)
            {
                //---------Enter your code here-----------
            }
            else if (block == label01)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleStepover)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleCutAngle)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleSafeDistance)
            {
                //---------Enter your code here-----------
            }
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        return 0;
    }

    public void RasterFinishng()
    {

        //   UI VARIABLES

        double RasterToolDiameter = doubleToolDiameter.Value;
        double RasterCornerRadius = doubleCornerRadius.Value;
        int RasterToolNumber = integerToolNumber.Value;
        double RasterRPM = doubleRPM.Value;
        double RasterFeedRate = doubleFeedRate.Value;
        double RasterStepover = doubleStepover.Value;
        double RasterCutAngle = doubleCutAngle.Value;
        double RasterSafeDistance = doubleSafeDistance.Value;

        if (RasterToolDiameter <= 0)
        {
            UI.GetUI().NXMessageBox.Show("Tool Error", NXMessageBox.DialogType.Error, "Tool Diameter must be greater than zero");

            return;
        }

        if (RasterStepover <= 0)
        {


            UI.GetUI().NXMessageBox.Show("Stepover Error", NXMessageBox.DialogType.Error, "Stepover must be greater than zero"); return;
        }



        //

        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;

        NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("PROGRAM"));
        NXOpen.CAM.Method method1 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("MILL_FINISH"));
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("PROGRAM"));
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

            // Create a parameter object and choose a path strategy
            RasterParameters rp = new RasterParameters()
            {
                Stepover = RasterStepover,
                CutAngle = RasterCutAngle,
                SafeDistance = RasterSafeDistance,
                RPM = RasterRPM,
                FeedRate = RasterFeedRate,
                ToolDiameter = RasterToolDiameter,
                ToolNumber = RasterToolNumber
            };

            // Strategy selection: default to ZigZag. You can extend this selection to use a UI control.
            IPathStrategy strategy = new ZigZagPathStrategy();
            strategy.Apply(surfaceContourBuilder1, rp);

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

            var faces = SelectAnyFace();
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
            ruleOptions.SetSelectedFromInactive(true);

            var faceRule = workPart.ScRuleFactory.CreateRuleFaceDumb(faces, ruleOptions);
            ruleOptions.Dispose();

            var scCollector = geomSet.ScCollector;
            scCollector.ReplaceRules(new NXOpen.SelectionIntentRule[] { faceRule }, false);
            // Commit will be executed once after all configuration (see below)


            // Feeds and non-cutting settings
            surfaceContourBuilder1.FeedsBuilder.SpindleRpmBuilder.Value = RasterRPM;
            surfaceContourBuilder1.FeedsBuilder.FeedCutBuilder.Value = RasterFeedRate;
            surfaceContourBuilder1.NonCuttingBuilder.TransferCommonClearanceBuilder.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
            surfaceContourBuilder1.NonCuttingBuilder.TransferCommonClearanceBuilder.SafeDistance = RasterSafeDistance;

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


            millToolBuilder1.TlDiameterBuilder.Value = RasterToolDiameter;

            millToolBuilder1.TlNumberBuilder.Value = RasterToolNumber;

            millToolBuilder1.TlAdjRegBuilder.Value = RasterToolNumber; // Assuming the adjust register is the same as the tool number for simplicity

            millToolBuilder1.TlCutcomRegBuilder.Value = RasterToolNumber; // Assuming the cutcom register is the same as the tool number for simplicity

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









    public int ok_cb()
    {
        int errorCode = 0;
        try
        {
            errorCode = apply_cb();
            RasterFinishng();
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            errorCode = 1;
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        return errorCode;
    }


    public PropertyList GetBlockProperties(string blockID)
    {
        PropertyList plist = null;
        try
        {
            plist = theDialog.GetBlockProperties(blockID);
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        return plist;
    }

}
