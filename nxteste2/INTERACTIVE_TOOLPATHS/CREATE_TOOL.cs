using System;
using NXOpen;
using NXOpen.BlockStyler;


//------------------------------------------------------------------------------
//Represents Block Styler application class
//------------------------------------------------------------------------------
public class CREATE_TOOL
{
    //class members
    private static Session theSession = null;
    private static UI theUI = null;
    private string theDlxFileName;
    private NXOpen.BlockStyler.BlockDialog theDialog;
    private NXOpen.BlockStyler.Group group0;
    private NXOpen.BlockStyler.Label labelGetTool;// Block type: Label
    private NXOpen.BlockStyler.StringBlock stringName;// Block type: String
    private NXOpen.BlockStyler.Enumeration enumType;// Block type: Enumeration
    private NXOpen.BlockStyler.DoubleBlock doubleDiameter;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleLowerRadius;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleLenght;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleFluteLenght;// Block type: Double
    private NXOpen.BlockStyler.IntegerBlock integerFlutes;// Block type: Integer
    private NXOpen.BlockStyler.IntegerBlock integerNumber;// Block type: Integer
    private NXOpen.BlockStyler.Separator separator0;// Block type: Separator
    private NXOpen.BlockStyler.Label labelShank;// Block type: Label
    private NXOpen.BlockStyler.DoubleBlock doubleDefineSHank;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleLowerDiameter;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleUpperDiameter;// Block type: Double
    private NXOpen.BlockStyler.DoubleBlock doubleShankLenght;// Block type: Double

    //------------------------------------------------------------------------------
    //Constructor for NX Styler class
    //------------------------------------------------------------------------------
    public CREATE_TOOL()
    {
        try
        {
            theSession = Session.GetSession();
            theUI = UI.GetUI();
            theDlxFileName = "C:\\Users\\dcard\\OneDrive\\Desktop\\NXUltimate\\Block-Style\\criar_ferramenta_Shank.dlx";
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

    public static void Run(string[]args )
    {
        CREATE_TOOL theCREATE_TOOL = null;
        try
        {
            theCREATE_TOOL = new CREATE_TOOL();
            // The following method shows the dialog immediately
            theCREATE_TOOL  .Launch();
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
        finally
        {
            if (theCREATE_TOOL != null)
                theCREATE_TOOL.Dispose();
            theCREATE_TOOL = null;
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





            group0 = (NXOpen.BlockStyler.Group)theDialog.TopBlock.FindBlock("group0");

           
            labelGetTool = (NXOpen.BlockStyler.Label)theDialog.TopBlock.FindBlock("labelGetTool");
            stringName = (NXOpen.BlockStyler.StringBlock)theDialog.TopBlock.FindBlock("stringName");
            enumType = (NXOpen.BlockStyler.Enumeration)theDialog.TopBlock.FindBlock("enumType");
            doubleDiameter = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleDiameter");
            doubleLowerRadius = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleLowerRadius");
            doubleLenght = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleLenght");
            doubleFluteLenght = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleFluteLenght");
            integerFlutes = (NXOpen.BlockStyler.IntegerBlock)theDialog.TopBlock.FindBlock("integerFlutes");
            integerNumber = (NXOpen.BlockStyler.IntegerBlock)theDialog.TopBlock.FindBlock("integerNumber");
            separator0 = (NXOpen.BlockStyler.Separator)theDialog.TopBlock.FindBlock("separator0");
            labelShank = (NXOpen.BlockStyler.Label)theDialog.TopBlock.FindBlock("labelShank");
            doubleDefineSHank = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleDefineSHank");
            doubleLowerDiameter = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleLowerDiameter");
            doubleUpperDiameter = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleUpperDiameter");
            doubleShankLenght = (NXOpen.BlockStyler.DoubleBlock)theDialog.TopBlock.FindBlock("doubleShankLenght");

            PropertyList enumProps = enumType.GetProperties();

            string[] items = Enum.GetNames(typeof(ToolType));

            enumProps.SetStrings("Strings", items);

            enumType.ValueAsString = items[0];

            enumProps.Dispose();
        }
        catch (Exception ex)
        {
            //---- Enter your exception handling code here -----
            theUI.NXMessageBox.Show("Block Styler", NXMessageBox.DialogType.Error, ex.ToString());
        }
    }

    //------------------------------------------------------------------------------
    //Callback Name: dialogShown_cb
    //This callback is executed just before the dialog launch. Thus any value set 
    //here will take precedence and dialog will be launched showing that value. 
    //------------------------------------------------------------------------------
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
            if (block == labelGetTool)
            {
                //---------Enter your code here-----------
            }
            else if (block == stringName)
            {
                //---------Enter your code here-----------
            }
            else if (block == enumType)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleDiameter)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleLowerRadius)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleLenght)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleFluteLenght)
            {
                //---------Enter your code here-----------
            }
            else if (block == integerFlutes)
            {
                //---------Enter your code here-----------
            }
            else if (block == integerNumber)
            {
                //---------Enter your code here-----------
            }
            else if (block == separator0)
            {
                //---------Enter your code here-----------
            }
            else if (block == labelShank)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleDefineSHank)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleLowerDiameter)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleUpperDiameter)
            {
                //---------Enter your code here-----------
            }
            else if (block == doubleShankLenght)
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

    // Make the ToolType enum public so it can be used by public methods and UI mapping
    public enum ToolType { MILL, DRILL, REAMER, TAP, OTHER };


    public static void CriarFerramentaCompleta(string toolName, ToolType toolType, double toolDiameter, double corRadius, double toolLenght, double fluteLenght, int toolNumber)

    {

            Session theSession = Session.GetSession();
            Part workPart = theSession.Parts.Work;
            Part displayPart = theSession.Parts.Display;
            UI theUI =  UI.GetUI();


          NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("GENERIC_MACHINE"));
    NXOpen.CAM.NCGroup nCGroup2;
    nCGroup2 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup1, "mill_contour", "MILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, toolName, "Mill");

    
    NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)nCGroup2);
    NXOpen.CAM.MillToolBuilder millToolBuilder1;
    millToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool1);
    
       
    millToolBuilder1.TlDiameterBuilder.Value = toolDiameter;

        millToolBuilder1.TlCor1RadBuilder.Value = corRadius;
       //  millToolBuilder1.TlLowCorRadBuilder.Value = corRadius;

        millToolBuilder1.TlHeightBuilder.Value = toolLenght;

        millToolBuilder1.TlFluteLnBuilder.Value = fluteLenght;

        millToolBuilder1.TlNumberBuilder.Value = toolNumber;

        millToolBuilder1.TlAdjRegBuilder.Value = toolNumber;

        millToolBuilder1.TlCutcomRegBuilder.Value = toolNumber;
    
       millToolBuilder1.UseTaperedShank = true;
    
      int outputindex1;
    try
    {
      // Length must be greater than zero.
      outputindex1 = millToolBuilder1.HolderSectionBuilder.Add(0, 65.0, 0.0, -90.000000000000043, 0.0);
    }
    catch (NXException ex)
    {
      ex.AssertErrorCode(3776401);
    }
    
    int outputindex2;
    outputindex2 = millToolBuilder1.HolderSectionBuilder.Add(0, 65.0, 50.0, -33.023867555796663, 0.0);
    
    millToolBuilder1.HolderSectionBuilder.Modify(0, 65.0, 50.0, 0.0, 0.0); 
    NXOpen.NXObject nXObject1;
    nXObject1 = millToolBuilder1.Commit();
     
    millToolBuilder1.Destroy();
    
    theSession.CAMSession.Utils.SetInspectionIntent(false);

         


    }




    public int ok_cb()
    {
        int errorCode = 0;
        try
        {
            errorCode = apply_cb();





            // Map selection from the Block Styler enumeration to the ToolType enum
            ToolType selectedTool = ToolType.MILL;
            if (enumType != null)
            {
                string sel = enumType.ValueAsString;
                if (!string.IsNullOrEmpty(sel))
                {
                    // Try parse by name (case-insensitive) if .dlx item names match ToolType identifiers
                    if (!Enum.TryParse<ToolType>(sel, true, out selectedTool))
                    {
                        // Fallback: map by index among enum members
                        string[] members = enumType.GetEnumMembers();
                        if (members != null)
                        {
                            for (int i = 0; i < members.Length; i++)
                            {
                                if (string.Equals(members[i], sel, StringComparison.OrdinalIgnoreCase))
                                {
                                    Array vals = Enum.GetValues(typeof(ToolType));
                                    if (i >= 0 && i < vals.Length)
                                    {
                                        selectedTool = (ToolType)i;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            CriarFerramentaCompleta(stringName.Value, selectedTool, doubleDiameter.Value, doubleLowerRadius.Value, doubleLenght.Value, doubleFluteLenght.Value, integerNumber.Value);
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

