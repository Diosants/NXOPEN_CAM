using System;
using NXOpen;
using NXOpen.BlockStyler;
using NXOpen.CAM;
using NXOpen.Motion;
using NXOpen.UF;


public class DesbasteInterativo
{
    //class members
    private static Session theSession = null;
    private static UI theUI = null;
    private string theDlxFileName;
    private NXOpen.BlockStyler.BlockDialog theDialog;
    private NXOpen.BlockStyler.Wizard wizard;// Block type: Wizard
    private NXOpen.BlockStyler.Group wizardStep;
    private NXOpen.BlockStyler.Label label0;// Block type: Label
    private NXOpen.BlockStyler.DoubleBlock doubleDiameter;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleLenght;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubletipRadius;// Block type: Double
    private NXOpen.BlockStyler.IntegerBlock integerToolNumber;// Block type: Integer
    private NXOpen.BlockStyler.Group wizardStep1;
    private NXOpen.BlockStyler.DoubleBlock doubleCutDepth;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleSurfaceSpeed;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleFeedRate;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleClearanceDistance;// Block type: Double
    private NXOpen.BlockStyler.Group wizardStep2;
    private NXOpen.BlockStyler.DoubleBlock doublePartStock;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleFloorStock;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleBlankStock;// Block type: Double

    //------------------------------------------------------------------------------
    //Constructor for NX Styler class
    //------------------------------------------------------------------------------
    public DesbasteInterativo()
    {
        try
        {
            theSession = Session.GetSession();
            theUI = UI.GetUI();
            theDlxFileName = "C:\\Users\\dcard\\OneDrive\\Desktop\\NXUltimate\\Block-Style\\DesbasteInterativo.dlx";
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

    public static void Run()
    {
        DesbasteInterativo theDesbasteInterativo = null;
        try
        {
            theDesbasteInterativo = new DesbasteInterativo();
            // The following method shows the dialog immediately
            theDesbasteInterativo.Launch();
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        finally
        {
            if (theDesbasteInterativo != null)
                theDesbasteInterativo.Dispose();
            theDesbasteInterativo = null;
        }
    }

    public static int GetUnloadOption(string arg)
    {
        //return System.Convert.ToInt32(Session.LibraryUnloadOption.Explicitly);
        return System.Convert.ToInt32(Session.LibraryUnloadOption.Immediately);
        // return System.Convert.ToInt32(Session.LibraryUnloadOption.AtTermination);
    }

    //------------------------------------------------------------------------------
    // Following method cleanup any housekeeping chores that may be needed.
    // This method is automatically called by NX.
    //------------------------------------------------------------------------------
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

    //------------------------------------------------------------------------------
    //This method launches the dialog to screen
    //------------------------------------------------------------------------------
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

    //------------------------------------------------------------------------------
    //Method Name: Dispose
    //------------------------------------------------------------------------------
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
            wizard = (NXOpen.BlockStyler.Wizard)theDialog.TopBlock.FindBlock("wizard");
            wizardStep = (NXOpen.BlockStyler.Group)theDialog.TopBlock.FindBlock("wizardStep");
            label0 = (NXOpen.BlockStyler.Label)theDialog.TopBlock.FindBlock("label0");
            doubleDiameter = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleDiameter");
            doubleLenght = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleLenght");
            doubletipRadius = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubletipRadius");
            integerToolNumber = (NXOpen.BlockStyler.IntegerBlock)theDialog.TopBlock.FindBlock("integerToolNumber");
            wizardStep1 = (NXOpen.BlockStyler.Group)theDialog.TopBlock.FindBlock("wizardStep1");
            doubleCutDepth = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleCutDepth");
            doubleSurfaceSpeed = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleSurfaceSpeed");
            doubleFeedRate = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleFeedRate");
            doubleClearanceDistance = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleClearanceDistance");
            wizardStep2 = (NXOpen.BlockStyler.Group)theDialog.TopBlock.FindBlock("wizardStep2");
            doublePartStock = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doublePartStock");
            doubleFloorStock = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleFloorStock");
            doubleBlankStock = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleBlankStock");

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

    //------------------------------------------------------------------------------
    //Callback Name: apply_cb
    //------------------------------------------------------------------------------
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
            else if (block == doubleDiameter)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleLenght)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubletipRadius)
            {
                //---------Enter your code here-----------
            }
            else if (block == integerToolNumber)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleCutDepth)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleSurfaceSpeed)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleClearanceDistance)
            {
                //---------Enter your code here-----------
            }
            else if (block == doublePartStock)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleFloorStock)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleBlankStock)
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

    //------------------------------------------------------------------------------
    //Callback Name: ok_cb
    //------------------------------------------------------------------------------


    //   Methodo  de Desbaste
    // Renamed to avoid conflict with class name (constructors must share the class name).
    public void RunDesbasteInterativo()
    {

        Session theSession =  Session.GetSession();
        Part workPart = theSession.Parts.Work;
        Part displayPart =  theSession.Parts.Display;


        double uiRoughTOolDIameter = doubleDiameter.Value;
        double uiRoughToolLenght = doubleLenght.Value;
        double uiRoughTipRadius = doubletipRadius.Value;

        int uiRoughToolNumber = integerToolNumber.Value;
        double uiRoughCutDepth = doubleCutDepth.Value;
        double uiRoughSurfaceSpeed = doubleSurfaceSpeed.Value;
        double uiRoughFeedRate = doubleFeedRate.Value;

        double uiRoughDistanceClearance = doubleClearanceDistance.Value;
        double uiRoughPartStock = doublePartStock.Value;
        double uiRoughFloorStock = doubleFloorStock.Value;
        double uiRoughBlankStock = doubleBlankStock.Value;



        NCGroup ncGroup1 = workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM");
        Method method1 =(Method) workPart.CAMSetup.CAMGroupCollection.FindObject("METHOD");
        NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));


        NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
        NXOpen.CAM.Operation operation1;
        operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(ncGroup1, method1, nCGroup2, featureGeometry1, "mill_contour", "CAVITY_MILL", NXOpen.CAM.OperationCollection.UseDefaultName.False, "RGH_TOOL_DIAM_"+uiRoughTOolDIameter, "Cavity Mill");



        NXOpen.CAM.CavityMilling cavityMilling1 = ((NXOpen.CAM.CavityMilling)operation1);
        NXOpen.CAM.CavityMillingBuilder cavityMillingBuilder1;
        cavityMillingBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateCavityMillingBuilder(cavityMilling1);


        NXOpen.NXObject nXObject1;
        nXObject1 = cavityMillingBuilder1.Commit();




        // DoubleBlock.Value is a non-nullable double. Avoid comparing to null.
        // If you want a default when the user hasn't set a value, compare against a sentinel
        // such as 0.0 or use double.IsNaN. Here we set a default diameter of 25.0 if current value is 0.
        if (double.IsNaN(uiRoughTOolDIameter) || uiRoughTOolDIameter == 0.0)
        {
            uiRoughTOolDIameter = 25.0;
            doubleDiameter.Value = uiRoughTOolDIameter;
        }

        if (double.IsNaN(uiRoughToolLenght) || uiRoughToolLenght == 0.0)
        {
            uiRoughToolLenght = 100;
            doubleLenght.Value = uiRoughToolLenght;
        }

        if (double.IsNaN(uiRoughTipRadius) || uiRoughTipRadius == 0.0)
        {
            uiRoughTipRadius = 1.0;
            doubletipRadius.Value = uiRoughTipRadius;
        }

    


        //  GETTING NEW TOOL FROM GENERIC_MACHINE
        NXOpen.CAM.NCGroup nCGroup3 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("GENERIC_MACHINE"));
        NXOpen.CAM.NCGroup nCGroup4;
        nCGroup4 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "mill_contour", "MILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "MILL_DIAM_"+uiRoughTOolDIameter, "Mill");

        NXOpen.CAM.CAMObject[] objectsToBeMoved1 = new NXOpen.CAM.CAMObject[1];
        NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)nCGroup4);
        objectsToBeMoved1[0] = tool1;
        workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved1, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);


        // CRIAR FERRAMENTA
        NXOpen.CAM.MillToolBuilder millToolBuilder1;
        millToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool1);
        millToolBuilder1.TlDiameterBuilder.Value = uiRoughTOolDIameter;

        millToolBuilder1.TlCor1RadBuilder.Value = uiRoughTipRadius;

        millToolBuilder1.TlNumberBuilder.Value = uiRoughToolNumber;

        millToolBuilder1.TlAdjRegBuilder.Value = uiRoughToolNumber;

        millToolBuilder1.TlCutcomRegBuilder.Value = uiRoughToolNumber;

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


        Body myBody =  SelectBody();
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

        cavityMillingBuilder2.CutLevel.GlobalDepthPerCut.DistanceBuilder.Value = uiRoughCutDepth;

        cavityMillingBuilder2.CutLevel.ApplyGlobalDepthPerCut();

        cavityMillingBuilder2.CutParameters.CutOrder = NXOpen.CAM.CutParametersCutOrderTypes.DepthFirst;

        cavityMillingBuilder2.WallCleanupType = NXOpen.CAM.MillOperationBuilder.WallCleanupTypes.None;

        // ----------------------------------------------
        //   Dialog Begin Cavity Mill - [CAVITY_MILL]
        // ----------------------------------------------
        cavityMillingBuilder2.CutParameters.FloorSameAsPartStock = false;

        cavityMillingBuilder2.CutParameters.PartStock.Value = uiRoughPartStock;

        cavityMillingBuilder2.CutParameters.FloorStock.Value = uiRoughFloorStock;

        cavityMillingBuilder2.CutParameters.BlankStock.Value  =  uiRoughBlankStock;

        // ----------------------------------------------
        //   Dialog Begin Cavity Mill - [CAVITY_MILL]
        // ----------------------------------------------
        // ----------------------------------------------
        //   Dialog Begin Cavity Mill - [CAVITY_MILL]
        // ----------------------------------------------
        cavityMillingBuilder2.FeedsBuilder.SpindleRpmBuilder.Value = uiRoughFeedRate;

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

        cavityMillingBuilder2.NonCuttingBuilder.ClearanceBuilder.SafeDistance = 50;

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

    static Body  SelectBody()
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
           

          RunDesbasteInterativo();
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
    //Wizard specific callbacks
    //------------------------------------------------------------------------------
    //public int stepNotifyPreCallback(Wizard wizard, int nextStep)
    //{
    //}

    //public void stepNotifyPostCallback(Wizard wizard, int previousStep)
    //{
    //}

    //public bool isStepOkayCallback(Wizard wizard, int step)
    //{
    //}

    //public void onSubNodeCallback(Wizard wizard, int step, int subNodeId, Wizard.SubNodeAction action)
    //{
    //}

    //public void onMenuCallback(Wizard wizard, Wizard.TaskNavigatorItem item, int step, int subNodeId)
    //{
    //}

    //public void onMenuSelectionCallback(Wizard wizard, Wizard.TaskNavigatorItem item, int step, int subNodeId, int commandIndex)
    //{
    //}


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
