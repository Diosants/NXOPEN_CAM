using System;
using NXOpen;
using NXOpen.BlockStyler;
using NXOpen.UF;

public class ZLEVELINTERACTIVE
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
    private NXOpen.BlockStyler.DoubleBlock doubleTipRadius;// Block type: Double
    private NXOpen.BlockStyler.IntegerBlock integerToolNumber;// Block type: Integer
    private NXOpen.BlockStyler.DoubleBlock doubleSurfaceSpeed;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleFeedRate;// Block type: Double
    private NXOpen.BlockStyler.Separator separator01;// Block type: Separator
    private NXOpen.BlockStyler.Label label01;// Block type: Label
    private NXOpen.BlockStyler.Separator separator02;// Block type: Separator
    private NXOpen.BlockStyler.DoubleBlock doubleDepth_per_cut;// Block type: Double



    public ZLEVELINTERACTIVE()
    {
        try
        {
            theSession = Session.GetSession();
            theUI = UI.GetUI();
            theDlxFileName = "C:\\Users\\dcard\\OneDrive\\Desktop\\NXUltimate\\Block-Style\\ZlevelInteractive.dlx";
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

    public static void Run(string[] args)
    {
        ZLEVELINTERACTIVE theZLEVELINTERACTIVE = null;
        try
        {
            theZLEVELINTERACTIVE = new ZLEVELINTERACTIVE();
            // The following method shows the dialog immediately
            theZLEVELINTERACTIVE.Launch();

            NXOpen.UIStyler.DialogResponse response;

        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        finally
        {
            if (theZLEVELINTERACTIVE != null)
                theZLEVELINTERACTIVE.Dispose();
            theZLEVELINTERACTIVE = null;
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
            doubleTipRadius = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleTipRadius");
            integerToolNumber = (NXOpen.BlockStyler.IntegerBlock)theDialog.TopBlock.FindBlock("integerToolNumber");
            doubleSurfaceSpeed = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleSurfaceSpeed");
            doubleFeedRate = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleFeedRate");
            separator01 = (NXOpen.BlockStyler.Separator)theDialog.TopBlock.FindBlock("separator01");
            label01 = (NXOpen.BlockStyler.Label)theDialog.TopBlock.FindBlock("label01");
            separator02 = (NXOpen.BlockStyler.Separator)theDialog.TopBlock.FindBlock("separator02");
            doubleDepth_per_cut = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleDepth_per_cut");
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

    //------------------------------------------------------------------------------
    //Callback Name: update_cb
    //------------------------------------------------------------------------------
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
            else if (block == doubleTipRadius)
            {
                //---------Enter your code here-----------
            }
            else if (block == integerToolNumber)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleSurfaceSpeed)
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
            else if (block == separator02)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleDepth_per_cut)
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


    //  Insira o seu codigo aqui,  FDP

    public void ZlevelProfile()
    {
        try
        {
            // Read UI values
            double uiToolDiameter = doubleToolDiameter.Value;
            double uiTipRadius = doubleTipRadius.Value;
            int uiToolNumber = integerToolNumber.Value;
            double uiSurfaceSpeed = doubleSurfaceSpeed.Value;
            double uiFeedRate = doubleFeedRate.Value;
            double uiDepthPerCut = doubleDepth_per_cut.Value;

            if (uiToolDiameter <= 0)
            {
                UI.GetUI().NXMessageBox.Show("Tool Error", NXMessageBox.DialogType.Error,
                    "Tool DIameter must be  greater than zero");
                return;
            }

            if (uiDepthPerCut <= 0)

            {
                UI.GetUI().NXMessageBox.Show("Cutting Parameters Error", NXMessageBox.DialogType.Error,
                    "Depth per cut must be greater than zero");
                return;
            }



            var theSession = Session.GetSession();
            var workPart = theSession.Parts.Work;

            // Create operation
            var ncProgramGroup = (NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("PROGRAM");
            var method = (NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("MILL_FINISH");
            var noneGroup = (NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("PROGRAM");
            var workpiece = (NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE");

            var operation = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
                ncProgramGroup, method, noneGroup, workpiece,
                "mill_contour", "ZLEVEL_PROFILE_STEEP",
                NXOpen.CAM.OperationCollection.UseDefaultName.True,
                "ZLEVEL_PROFILE_STEEP_1", "Zlevel Profile Steep 1");

            var zLevelOp = (NXOpen.CAM.ZLevelMilling)operation;
            var zLevelBuilder = workPart.CAMSetup.CAMOperationCollection.CreateZlevelMillingBuilder(zLevelOp);

            // Initialize and collect faces
            zLevelBuilder.CutAreaGeometry.InitializeData(false);

            var faces = SelectAnyFace();
            if (faces == null || faces.Length == 0)
            {
                UI.GetUI().NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error,
                    "No faces selected. Operation cancelled.");
                zLevelBuilder.Destroy();
                return;
            }

            // Apply selected faces to geometry collector
            var geomList = zLevelBuilder.CutAreaGeometry.GeometryList;
            var geomSet = (NXOpen.CAM.GeometrySet)geomList.FindItem(0);

            var ruleOptions = workPart.ScRuleFactory.CreateRuleOptions();
            ruleOptions.SetSelectedFromInactive(true);

            var faceRule = workPart.ScRuleFactory.CreateRuleFaceDumb(faces, ruleOptions);
            ruleOptions.Dispose();

            var scCollector = geomSet.ScCollector;
            scCollector.ReplaceRules(new NXOpen.SelectionIntentRule[] { faceRule }, false);

            var committedOpObject = zLevelBuilder.Commit();
            // keep builder around until destroy (we'll destroy below)
            zLevelBuilder.Destroy();

            // TOOL CREATION (creates a new tool with a name containing the diameter)
            var machineGroup = (NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("GENERIC_MACHINE");

            NXOpen.CAM.NCGroup toolGroup = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(
                machineGroup,
                "mill_contour",
                "MILL",
                NXOpen.CAM.NCGroupCollection.UseDefaultName.False,
                "Tool_Diam_" + uiToolDiameter + "MM",
                "Mill");

            var tool = (NXOpen.CAM.Tool)toolGroup;

            // Move tool into machine tool group view
            workPart.CAMSetup.MoveObjects(
                NXOpen.CAM.CAMSetup.View.MachineTool,
                new NXOpen.CAM.CAMObject[] { tool },
                machineGroup,
                NXOpen.CAM.CAMSetup.Paste.Inside);

            // Configure mill tool
            var millBuilder = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool);
            millBuilder.TlDiameterBuilder.Value = uiToolDiameter;
            millBuilder.TlCor1RadBuilder.Value = uiTipRadius;
            millBuilder.TlNumberBuilder.Value = uiToolNumber;
            millBuilder.TlAdjRegBuilder.Value = uiToolNumber;
            millBuilder.TlCutcomRegBuilder.Value = uiToolNumber;

            var toolNxObject = millBuilder.Commit();
            millBuilder.Destroy();

            // Associate operation with tool
            theSession.CAMSession.Utils.SetInspectionIntent(false);

            var zLevelOpCommitted = (NXOpen.CAM.ZLevelMilling)committedOpObject;
            var camTool = (NXOpen.CAM.Tool)toolNxObject;

            workPart.CAMSetup.MoveObjects(
                NXOpen.CAM.CAMSetup.View.MachineTool,
                new NXOpen.CAM.CAMObject[] { zLevelOpCommitted },
                camTool,
                NXOpen.CAM.CAMSetup.Paste.Inside);

            // Create a builder for the operation we just associated with the tool
            var zLevelBuilder2 = workPart.CAMSetup.CAMOperationCollection.CreateZlevelMillingBuilder(zLevelOpCommitted);

            zLevelBuilder2.CutLevel.InitializeData();

            // Configure cut parameters
            zLevelBuilder2.CutParameters.SteepContainment.Type = NXOpen.CAM.SteepContainment.Types.None;

            zLevelBuilder2.CutLevel.GlobalDepthPerCut.DistanceBuilder.Intent = NXOpen.CAM.ParamValueIntent.PartUnits;
            zLevelBuilder2.CutLevel.ApplyGlobalDepthPerCut();
            zLevelBuilder2.CutLevel.GlobalDepthPerCut.DistanceBuilder.Value = uiDepthPerCut;
            zLevelBuilder2.CutLevel.ApplyGlobalDepthPerCut();

            zLevelBuilder2.CutParameters.CutDirection.Type = NXOpen.CAM.CutDirection.Types.Mixed;
            zLevelBuilder2.CutParameters.CutOrder = NXOpen.CAM.CutParametersCutOrderTypes.DepthFirstAlways;
            zLevelBuilder2.CutParameters.RollToolOverEdges = true;
            zLevelBuilder2.CutParameters.LevelToLevel.Type = NXOpen.CAM.LevelToLevel.Types.DirectOnPart;
        

            zLevelBuilder2.FeedsBuilder.SurfaceSpeedBuilder.Value = uiSurfaceSpeed;
            zLevelBuilder2.FeedsBuilder.FeedCutBuilder.Value = uiFeedRate;

            zLevelBuilder2.NonCuttingBuilder.ClearanceBuilder.AxisObject = null;
            zLevelBuilder2.NonCuttingBuilder.EngageClosedAreaBuilder.EngRetType = NXOpen.CAM.NcmPlanarEngRetBuilder.EngRetTypes.None;
            zLevelBuilder2.NonCuttingBuilder.EngageOpenAreaBuilder.EngRetType = NXOpen.CAM.NcmPlanarEngRetBuilder.EngRetTypes.None;

            zLevelBuilder2.NonCuttingBuilder.ClearanceBuilder.ClearanceType = NXOpen.CAM.NcmClearanceBuilder.ClearanceTypes.Automatic;
            zLevelBuilder2.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50.0;

            zLevelBuilder2.NonCuttingBuilder.TransferBetweenRegionsBuilder.Type = NXOpen.CAM.NcmTransfer.TransferTypes.PrevPlane;
            zLevelBuilder2.NonCuttingBuilder.TransferBetweenRegionsBuilder.SafeDistanceBuilder.Value = 0.5;

            zLevelBuilder2.NonCuttingBuilder.TransferWithinLevelsType = NXOpen.CAM.NcmPlanarBuilder.TransferWithinLevelsTypes.PrevPlane;
            zLevelBuilder2.NonCuttingBuilder.TransferWithinLevelsSafeDistanceBuilder.Value = 0.0;

            var committedZLevel2 = zLevelBuilder2.Commit();

            // Generate toolpath
            workPart.CAMSetup.GenerateToolPath(new NXOpen.CAM.CAMObject[] { (NXOpen.CAM.ZLevelMilling)committedZLevel2 });

            zLevelBuilder2.Destroy();

            // Finalize: create builder for committed operation to initialize data as original code did
            var zLevelBuilder3 = workPart.CAMSetup.CAMOperationCollection.CreateZlevelMillingBuilder((NXOpen.CAM.ZLevelMilling)committedZLevel2);
            zLevelBuilder3.CutLevel.InitializeData();
            zLevelBuilder3.Commit();
            zLevelBuilder3.Destroy();
        }
        catch (Exception ex)
        {
            UI.GetUI().NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
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



    static Body selectBody()
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









    public int ok_cb()
    {
        int errorCode = 0;
        try
        {
            errorCode = apply_cb();
            ZlevelProfile();
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            errorCode = 1;
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        return errorCode;
    }

    //------------------------------------------------------------------------------
    //Function Name: GetBlockProperties
    //Returns the propertylist of the specified BlockID
    //------------------------------------------------------------------------------
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
