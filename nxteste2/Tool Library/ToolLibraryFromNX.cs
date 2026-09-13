using NXOpen;
using NXOpen.CAM;

namespace TOOL_LIBRARY_FROM_NX
{
    public class ToolLibraryFromNX
    {
        public static void Run(string[] args)
        {
            Session theSession = Session.GetSession();
            Part workPart = theSession.Parts.Work;
            UI theUI = UI.GetUI();

            theUI.NXMessageBox.Show("Tool List Refactored", NXMessageBox.DialogType.Information,
                "This is a refactored version of the tool list code.");

            NCGroup ncGroup1 = (NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("GENERIC_MACHINE");

            if (ncGroup1 == null)
            {
                theUI.NXMessageBox.Show("ERROR", NXMessageBox.DialogType.Error,
                    "UNABLE TO FIND GENERIC_MACHINE");
                return;
            }

            // ========================
            // FACEMILL
            // ========================
            LoadTool(workPart, ncGroup1, "ugt0212_001", "FACEMILL_D100MM");
            LoadTool(workPart, ncGroup1, "ugt0212_003", "FACEMILL_D80MM");
            LoadTool(workPart, ncGroup1, "NXT0212_002", "FACEMILL_D64MM");
            // ========================
            // CUTTERS
            // ========================
            LoadTool(workPart, ncGroup1, "ugt0202_031", "CUTTER_D50_R1");
            LoadTool(workPart, ncGroup1, "ugt0202_001", "CUTTER_D40_R1");
            LoadTool(workPart, ncGroup1, "ugt0202_008", "CUTTER_D25_R.8");
            LoadTool(workPart, ncGroup1, "ugt0202_009", "CUTTER_D20_R.8");
            LoadTool(workPart, ncGroup1, "ugt0202_005", "CUTTER_D16_R.8");

            // ========================
            // ENDMILLS
            // ========================
            LoadTool(workPart, ncGroup1, "ugt0201_100", "ENDMILL_D18MM");
            LoadTool(workPart, ncGroup1, "NXT0201_012", "ENDMILL_D16MM");
            LoadTool(workPart, ncGroup1, "ugt0201_103", "ENDMILL_D14MM");
 
            LoadTool(workPart, ncGroup1, "NXT0201_005", "ENDMILL_D12MM");
            LoadTool(workPart, ncGroup1, "NXT0201_004", "ENDMILL_D10MM");
            LoadTool(workPart, ncGroup1, "NXT0201_003", "ENDMILL_D8MM");
            LoadTool(workPart, ncGroup1, "NXT0201_008", "ENDMILL_D6MM");
            LoadTool(workPart, ncGroup1, "NXT0201_001", "ENDMILL_D4MM");
            LoadTool(workPart, ncGroup1, "ugt0201_001", "ENDMILL_D2MM");

            // ========================
            // BALL MILLS
            // ========================
            LoadTool(workPart, ncGroup1, "ugt0203_064", "BMD25MM");
            LoadTool(workPart, ncGroup1, "ugt0203_063", "BMD18MM");
            LoadTool(workPart, ncGroup1, "ugt0203_062", "BMD16MM");
            LoadTool(workPart, ncGroup1, "ugt0203_061", "BMD14MM");


          

            LoadTool(workPart, ncGroup1, "ugt0203_005", "BMD12MM");


            LoadTool(workPart, ncGroup1, "ugt0203_004", "BMD10MM");
            LoadTool(workPart, ncGroup1, "ugt0203_003", "BMD8MM");
            LoadTool(workPart, ncGroup1, "ugt0203_001", "BMD6MM");
            LoadTool(workPart, ncGroup1, "NXT0203_001", "BMD4MM");

            // ========================
            // DRILLING
            // ========================
            LoadTool(workPart, ncGroup1, "ugt0321_004", "CENTER_DRILL");

            // HSS DRILLS


            LoadTool(workPart, ncGroup1, "ugt0301_383", "HSS_DRILL_D33");


            LoadTool(workPart, ncGroup1, "ugt0301_052", "HSS_DRILL_D21.5");
            LoadTool(workPart, ncGroup1, "ugt0301_053", "HSS_DRILL_D22");
            LoadTool(workPart, ncGroup1, "ugt0301_054", "HSS_DRILL_D22.5");
            LoadTool(workPart, ncGroup1, "ugt0301_055", "HSS_DRILL_D23");
            LoadTool(workPart, ncGroup1, "ugt0301_056", "HSS_DRILL_D23.5");
            LoadTool(workPart, ncGroup1, "ugt0301_057", "HSS_DRILL_D24");
            LoadTool(workPart, ncGroup1, "ugt0301_058", "HSS_DRILL_D24.5");
            LoadTool(workPart, ncGroup1, "ugt0301_059", "HSS_DRILL_D25");
            LoadTool(workPart, ncGroup1, "ugt0301_130", "HSS_DRILL_D25.75");
            LoadTool(workPart, ncGroup1, "ugt0301_387", "HSS_DRILL_D26");
            LoadTool(workPart, ncGroup1, "ugt0301_131", "HSS_DRILL_D26.75");
            LoadTool(workPart, ncGroup1, "ugt0301_132", "HSS_DRILL_D27.75");
            LoadTool(workPart, ncGroup1, "ugt0301_133", "HSS_DRILL_D28.75");
            LoadTool(workPart, ncGroup1, "ugt0301_134", "HSS_DRILL_D29.75");
            LoadTool(workPart, ncGroup1, "ugt0301_051", "HSS_DRILL_D21");
            LoadTool(workPart, ncGroup1, "ugt0301_050", "HSS_DRILL_D20.5");
            LoadTool(workPart, ncGroup1, "ugt0301_007", "HSS_DRILL_D20");

            // BROCAS 19 MM
            LoadTool(workPart, ncGroup1, "NXT0301_121", "HSS_DRILL_D19.8");
            LoadTool(workPart, ncGroup1, "ugt0301_049", "HSS_DRILL_D19.5");
            LoadTool(workPart, ncGroup1, "ugt0301_048", "HSS_DRILL_D19");

            //   BROCAS 18 MM

            LoadTool(workPart, ncGroup1, "NXT0301_117", "HSS_DRILL_D18");
            LoadTool(workPart, ncGroup1, "NXT0301_118", "HSS_DRILL_D18.5");
            LoadTool(workPart, ncGroup1, "NXT0301_119", "HSS_DRILL_D18.8");


            //   BROCAS 17 MM

            LoadTool(workPart, ncGroup1, "NXT0301_114", "HSS_DRILL_D17");
            LoadTool(workPart, ncGroup1, "NXT0301_115", "HSS_DRILL_D17.5");
            LoadTool(workPart, ncGroup1, "NXT0301_116", "HSS_DRILL_D17.8");


            //   BROCAS 16  MM

            LoadTool(workPart, ncGroup1, "NXT0301_110", "HSS_DRILL_D16");
            LoadTool(workPart, ncGroup1, "NXT0301_111", "HSS_DRILL_D16.1");
            LoadTool(workPart, ncGroup1, "NXT0301_112", "HSS_DRILL_D16.5");
            LoadTool(workPart, ncGroup1, "NXT0301_113", "HSS_DRILL_D16.8");

            //   BROCAS 15  MM

            LoadTool(workPart, ncGroup1, "NXT0301_107", "HSS_DRILL_D15");
            LoadTool(workPart, ncGroup1, "NXT0301_108", "HSS_DRILL_D15.5");
            LoadTool(workPart, ncGroup1, "NXT0301_109", "HSS_DRILL_D15.8");

            //   BROCAS 14  MM


            LoadTool(workPart, ncGroup1, "NXT0301_104", "HSS_DRILL_D14");
            LoadTool(workPart, ncGroup1, "NXT0301_105", "HSS_DRILL_D14.25");
            LoadTool(workPart, ncGroup1, "NXT0301_106", "HSS_DRILL_D14.5");


            //   BROCAS 13 MM

            LoadTool(workPart, ncGroup1, "NXT0301_101", "HSS_DRILL_D13");
            LoadTool(workPart, ncGroup1, "NXT0301_102", "HSS_DRILL_D13.5");
            LoadTool(workPart, ncGroup1, "NXT0301_103", "HSS_DRILL_D13.8");



            // BROCAS 12 MM
            LoadTool(workPart, ncGroup1, "NXT0301_091", "HSS_DRILL_D12");
            LoadTool(workPart, ncGroup1, "NXT0301_092", "HSS_DRILL_D12.1");
            LoadTool(workPart, ncGroup1, "NXT0301_093", "HSS_DRILL_D12.2");
            LoadTool(workPart, ncGroup1, "NXT0301_096", "HSS_DRILL_D12.5");
            LoadTool(workPart, ncGroup1, "NXT0301_097", "HSS_DRILL_D12.6");
            LoadTool(workPart, ncGroup1, "NXT0301_098", "HSS_DRILL_D12.7");
            LoadTool(workPart, ncGroup1, "NXT0301_099", "HSS_DRILL_D12.8");


            //  BROCAS 11 MM
            LoadTool(workPart, ncGroup1, "NXT0301_081", "HSS_DRILL_D11");
            LoadTool(workPart, ncGroup1, "NXT0301_082", "HSS_DRILL_D11.1");
            LoadTool(workPart, ncGroup1, "NXT0301_083", "HSS_DRILL_D11.2");
            LoadTool(workPart, ncGroup1, "NXT0301_084", "HSS_DRILL_D11.3");
            LoadTool(workPart, ncGroup1, "NXT0301_086", "HSS_DRILL_D11.5");
            LoadTool(workPart, ncGroup1, "NXT0301_087", "HSS_DRILL_D11.6");
            LoadTool(workPart, ncGroup1, "NXT0301_088", "HSS_DRILL_D11.7");
            LoadTool(workPart, ncGroup1, "NXT0301_089", "HSS_DRILL_D11.8");


            //  BROCAS DIAM 10 MM
            LoadTool(workPart, ncGroup1, "ugt0301_033", "HSS_DRILL_D10.5");
            LoadTool(workPart, ncGroup1, "NXT0301_079", "HSS_DRILL_D10.8");
            LoadTool(workPart, ncGroup1, "NXT0301_077", "HSS_DRILL_D10.6");
            LoadTool(workPart, ncGroup1, "NXT0301_074", "HSS_DRILL_D10.3");
            LoadTool(workPart, ncGroup1, "NXT0301_073", "HSS_DRILL_D10.2");
            LoadTool(workPart, ncGroup1, "ugt0301_032", "HSS_DRILL_D10");

            LoadTool(workPart, ncGroup1, "NXT0302_036", "HSS_DRILL_D42");
        
            LoadTool(workPart, ncGroup1, "NXT0302_038", "HSS_DRILL_D50");
       



            //  BROCAS   DIAM 9 MM

            LoadTool(workPart, ncGroup1, "ugt0301_030", "HSS_DRILL_D9");
            LoadTool(workPart, ncGroup1, "ugt0301_031", "HSS_DRILL_D9.5");
            LoadTool(workPart, ncGroup1, "ugt0301_206", "HSS_DRILL_D9.1");
            LoadTool(workPart, ncGroup1, "ugt0301_207", "HSS_DRILL_D9.2");
            LoadTool(workPart, ncGroup1, "ugt0301_208", "HSS_DRILL_D9.3");
            LoadTool(workPart, ncGroup1, "ugt0301_209", "HSS_DRILL_D9.4");
            LoadTool(workPart, ncGroup1, "ugt0301_210", "HSS_DRILL_D9.6");
            LoadTool(workPart, ncGroup1, "ugt0301_211", "HSS_DRILL_D9.7");
            LoadTool(workPart, ncGroup1, "ugt0301_212", "HSS_DRILL_D9.8");
            LoadTool(workPart, ncGroup1, "ugt0301_213", "HSS_DRILL_D9.9");



            //  BROCAS   DIAM 8MM
            LoadTool(workPart, ncGroup1, "ugt0301_029", "HSS_DRILL_D8.5");
            LoadTool(workPart, ncGroup1, "ugt0301_028", "HSS_DRILL_D8");
            LoadTool(workPart, ncGroup1, "ugt0301_199", "HSS_DRILL_D8.2");
            LoadTool(workPart, ncGroup1, "ugt0301_200", "HSS_DRILL_D8.3");
            LoadTool(workPart, ncGroup1, "ugt0301_201", "HSS_DRILL_D8.4");
            LoadTool(workPart, ncGroup1, "ugt0301_202", "HSS_DRILL_D8.6");
            LoadTool(workPart, ncGroup1, "ugt0301_203", "HSS_DRILL_D8.7");
            LoadTool(workPart, ncGroup1, "ugt0301_204", "HSS_DRILL_D8.8");
            LoadTool(workPart, ncGroup1, "ugt0301_205", "HSS_DRILL_D8.9");



            //  BROCAS DIAM 7 MM
            LoadTool(workPart, ncGroup1, "ugt0301_026", "HSS_DRILL_D7.5");
            LoadTool(workPart, ncGroup1, "ugt0301_027", "HSS_DRILL_D7");
            LoadTool(workPart, ncGroup1, "ugt0301_191", "HSS_DRILL_D7.1");
            LoadTool(workPart, ncGroup1, "ugt0301_192", "HSS_DRILL_D7.2");
            LoadTool(workPart, ncGroup1, "ugt0301_193", "HSS_DRILL_D7.3");
            LoadTool(workPart, ncGroup1, "ugt0301_194", "HSS_DRILL_D7.4");
            LoadTool(workPart, ncGroup1, "ugt0301_195", "HSS_DRILL_D7.6");
            LoadTool(workPart, ncGroup1, "ugt0301_196", "HSS_DRILL_D7.7");
            LoadTool(workPart, ncGroup1, "ugt0301_197", "HSS_DRILL_D7.8");
            LoadTool(workPart, ncGroup1, "ugt0301_198", "HSS_DRILL_D7.9");






            //  BROCAS DIAM 6 MM
            LoadTool(workPart, ncGroup1, "ugt0301_021", "HSS_DRILL_D6.5");
            LoadTool(workPart, ncGroup1, "ugt0301_025", "HSS_DRILL_D6");
            LoadTool(workPart, ncGroup1, "ugt0301_183", "HSS_DRILL_D6.1");
            LoadTool(workPart, ncGroup1, "ugt0301_184", "HSS_DRILL_D6.2");
            LoadTool(workPart, ncGroup1, "ugt0301_185", "HSS_DRILL_D6.3");
            LoadTool(workPart, ncGroup1, "ugt0301_186", "HSS_DRILL_D6.4");
            LoadTool(workPart, ncGroup1, "ugt0301_187", "HSS_DRILL_D6.6");
            LoadTool(workPart, ncGroup1, "ugt0301_188", "HSS_DRILL_D6.7");
            LoadTool(workPart, ncGroup1, "ugt0301_189", "HSS_DRILL_D6.8");
            LoadTool(workPart, ncGroup1, "ugt0301_190", "HSS_DRILL_D6.9");



            // BROCAS DIAM 5 MM

            LoadTool(workPart, ncGroup1, "ugt0301_024", "HSS_DRILL_D5.5");
            LoadTool(workPart, ncGroup1, "ugt0301_023", "HSS_DRILL_D5");
            LoadTool(workPart, ncGroup1, "ugt0301_175", "HSS_DRILL_D5.1");
            LoadTool(workPart, ncGroup1, "ugt0301_176", "HSS_DRILL_D5.2");
            LoadTool(workPart, ncGroup1, "ugt0301_177", "HSS_DRILL_D5.3");
            LoadTool(workPart, ncGroup1, "ugt0301_178", "HSS_DRILL_D5.4");
            LoadTool(workPart, ncGroup1, "ugt0301_179", "HSS_DRILL_D5.6");
            LoadTool(workPart, ncGroup1, "ugt0301_180", "HSS_DRILL_D5.7");
            LoadTool(workPart, ncGroup1, "ugt0301_181", "HSS_DRILL_D5.8");
            LoadTool(workPart, ncGroup1, "ugt0301_182", "HSS_DRILL_D5.9");

            // BROCAS  DIAM 4 MM

            LoadTool(workPart, ncGroup1, "ugt0301_022", "HSS_DRILL_D4.5");
            LoadTool(workPart, ncGroup1, "ugt0301_002", "HSS_DRILL_D4");
            LoadTool(workPart, ncGroup1, "ugt0301_167", "HSS_DRILL_D4.1");
            LoadTool(workPart, ncGroup1, "ugt0301_168", "HSS_DRILL_D4.2");
            LoadTool(workPart, ncGroup1, "ugt0301_169", "HSS_DRILL_D4.3");
            LoadTool(workPart, ncGroup1, "ugt0301_170", "HSS_DRILL_D4.4");
            LoadTool(workPart, ncGroup1, "ugt0301_171", "HSS_DRILL_D4.6");
            LoadTool(workPart, ncGroup1, "ugt0301_172", "HSS_DRILL_D4.7");
            LoadTool(workPart, ncGroup1, "ugt0301_173", "HSS_DRILL_D4.8");
            LoadTool(workPart, ncGroup1, "ugt0301_174", "HSS_DRILL_D4.9");




            // BROCAS  DIAM 3 MM

            LoadTool(workPart, ncGroup1, "ugt0301_020", "HSS_DRILL_D3.5");
            LoadTool(workPart, ncGroup1, "ugt0301_019", "HSS_DRILL_D3");
            LoadTool(workPart, ncGroup1, "ugt0301_159", "HSS_DRILL_D3.1");
            LoadTool(workPart, ncGroup1, "ugt0301_160", "HSS_DRILL_D3.2");
            LoadTool(workPart, ncGroup1, "ugt0301_161", "HSS_DRILL_D3.3");
            LoadTool(workPart, ncGroup1, "ugt0301_162", "HSS_DRILL_D3.4");
            LoadTool(workPart, ncGroup1, "ugt0301_163", "HSS_DRILL_D3.6");
            LoadTool(workPart, ncGroup1, "ugt0301_164", "HSS_DRILL_D3.7");
            LoadTool(workPart, ncGroup1, "ugt0301_165", "HSS_DRILL_D3.8");
            LoadTool(workPart, ncGroup1, "ugt0301_166", "HSS_DRILL_D3.9");





            //  BROCAS DIAM 2 MM
            LoadTool(workPart, ncGroup1, "ugt0301_018", "HSS_DRILL_D2.5");
            LoadTool(workPart, ncGroup1, "ugt0301_017", "HSS_DRILL_D2");
            LoadTool(workPart, ncGroup1, "ugt0301_151", "HSS_DRILL_D2.1");
            LoadTool(workPart, ncGroup1, "ugt0301_152", "HSS_DRILL_D2.2");
            LoadTool(workPart, ncGroup1, "ugt0301_153", "HSS_DRILL_D2.3");
            LoadTool(workPart, ncGroup1, "ugt0301_154", "HSS_DRILL_D2.4");
            LoadTool(workPart, ncGroup1, "ugt0301_155", "HSS_DRILL_D2.6");
            LoadTool(workPart, ncGroup1, "ugt0301_156", "HSS_DRILL_D2.7");
            LoadTool(workPart, ncGroup1, "ugt0301_157", "HSS_DRILL_D2.8");
            LoadTool(workPart, ncGroup1, "ugt0301_158", "HSS_DRILL_D2.9");

       
            LoadTool(workPart, ncGroup1, "ugt0301_143", "HSS_DRILL_D1.1");
            LoadTool(workPart, ncGroup1, "ugt0301_144", "HSS_DRILL_D1.2");
            LoadTool(workPart, ncGroup1, "ugt0301_145", "HSS_DRILL_D1.3");
            LoadTool(workPart, ncGroup1, "ugt0301_146", "HSS_DRILL_D1.4");
            LoadTool(workPart, ncGroup1, "ugt0301_147", "HSS_DRILL_D1.6");
            LoadTool(workPart, ncGroup1, "ugt0301_148", "HSS_DRILL_D1.7");
            LoadTool(workPart, ncGroup1, "ugt0301_149", "HSS_DRILL_D1.8");
            LoadTool(workPart, ncGroup1, "ugt0301_150", "HSS_DRILL_D1.9");



            LoadTool(workPart, ncGroup1, "ugt0333_007", "BORE_BAR_DIAM_40");





            // ======================== UGT0333_007
            // REAMERS
            // ========================
            LoadTool(workPart, ncGroup1, "NXT0341_002", "REAMER_D6MM");
            LoadTool(workPart, ncGroup1, "NXT0341_004", "REAMER_D8MM");
            LoadTool(workPart, ncGroup1, "NXT0341_006", "REAMER_D10MM");
            LoadTool(workPart, ncGroup1, "NXT0341_008", "REAMER_D12MM");
            LoadTool(workPart, ncGroup1, "NXT0341_009", "REAMER_D16MM");
            LoadTool(workPart, ncGroup1, "NXT0341_010", "REAMER_D18MM");

            // ========================
            // CHAMFER
            // ========================
            LoadTool(workPart, ncGroup1, "NXT0205_001", "CHAMFERER_D12MM");
            LoadTool(workPart, ncGroup1, "NXT0205_002", "CHAMFERER_D10MM");
            LoadTool(workPart, ncGroup1, "ugt0205_001", "CHAMFERER_D20MM");
            LoadTool(workPart, ncGroup1, "ugt0205_002", "CHAMFERER_D15MM");


            // ========================
            // TAP
            // ========================
            LoadTool(workPart, ncGroup1, "ugt0371_023", "TAP_M24X2.0");
            LoadTool(workPart, ncGroup1, "ugt0371_022", "TAP_M24X3.0");
            LoadTool(workPart, ncGroup1, "ugt0371_021", "TAP_M22X2.5");
            LoadTool(workPart, ncGroup1, "ugt0371_020", "TAP_M20X1.5");
            LoadTool(workPart, ncGroup1, "ugt0371_019", "TAP_M18X2.5");
            LoadTool(workPart, ncGroup1, "ugt0371_018", "TAP_M12X1.25");
            LoadTool(workPart, ncGroup1, "ugt0371_016", "TAP_M20X2.5");
            LoadTool(workPart, ncGroup1, "ugt0371_015", "TAP_M18X1.5");
            LoadTool(workPart, ncGroup1, "ugt0371_014", "TAP_M16X2.0");
            LoadTool(workPart, ncGroup1, "ugt0371_013", "TAP_M16X1.5");
            LoadTool(workPart, ncGroup1, "ugt0371_012", "TAP_M14X2.0");
            LoadTool(workPart, ncGroup1, "ugt0371_011", "TAP_M14X1.5");
            LoadTool(workPart, ncGroup1, "ugt0371_010", "TAP_M12X1.75");
            LoadTool(workPart, ncGroup1, "ugt0371_009", "TAP_M10X1.25");
            LoadTool(workPart, ncGroup1, "ugt0371_008", "TAP_M8X1.0");
            LoadTool(workPart, ncGroup1, "ugt0371_006", "TAP_M6X1.0");
            LoadTool(workPart, ncGroup1, "ugt0371_005", "TAP_M5X0.8");
            LoadTool(workPart, ncGroup1, "ugt0371_004", "TAP_M4X0.7");
            LoadTool(workPart, ncGroup1, "ugt0371_001", "TAP_M10X1.5");

            //  COUNTERBORE

            LoadTool(workPart, ncGroup1, "ugt0351_011", "Cbore_8MM");
            LoadTool(workPart, ncGroup1, "ugt0351_012", "Cbore_10MM");
            LoadTool(workPart, ncGroup1, "ugt0351_022", "Cbore_11");
            LoadTool(workPart, ncGroup1, "ugt0351_013", "Cbore_11.5MM");
            LoadTool(workPart, ncGroup1, "ugt0351_024", "Cbore_15MM");
            LoadTool(workPart, ncGroup1, "ugt0351_026", "Cbore_18MM");
            LoadTool(workPart, ncGroup1, "ugt0351_028", "Cbore_20MM");
            LoadTool(workPart, ncGroup1, "ugt0351_041", "Cbore_24MM");
            LoadTool(workPart, ncGroup1, "ugt0351_042", "Cbore_26MM");
            LoadTool(workPart, ncGroup1, "ugt0351_079", "Cbore_30MM");
            LoadTool(workPart, ncGroup1, "ugt0351_091", "Cbore_33MM");
            LoadTool(workPart, ncGroup1, "ugt0351_098", "Cbore_36MM");
            LoadTool(workPart, ncGroup1, "ugt0351_110", "Cbore_40MM");
            LoadTool(workPart, ncGroup1, "ugt0351_130", "Cbore_40.5MM");
            LoadTool(workPart, ncGroup1, "ugt0351_131", "Cbore_45MM");
            LoadTool(workPart, ncGroup1, "ugt0351_132", "Cbore_46MM");
            LoadTool(workPart, ncGroup1, "ugt0351_133", "Cbore_48MM");
            LoadTool(workPart, ncGroup1, "ugt0351_134", "Cbore_54MM");
            LoadTool(workPart, ncGroup1, "ugt0351_135", "Cbore_61MM");
            LoadTool(workPart, ncGroup1, "ugt0351_136", "Cbore_64MM");
            LoadTool(workPart, ncGroup1, "ugt0351_137", "Cbore_73MM");

            // COUNTERSINK
            LoadTool(workPart, ncGroup1, "ugt0361_014", "COUNTERSINK");


            theUI.NXMessageBox.Show("TOOL LIBRARY", NXMessageBox.DialogType.Information,
                "MASTER TOOLS LOADED SUCCESSFULLY");

          
            // UPDATING SOME TOOLS IN THE LIBRARY
            NXOpen.CAM.Tool emd18mm = (NXOpen.CAM.Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("ENDMILL_D18MM");

            NXOpen.CAM.MillToolBuilder millToolBuilder1;
            millToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(emd18mm);
     

            millToolBuilder1.TlNumberBuilder.Value = 18;

            millToolBuilder1.TlAdjRegBuilder.Value = 18;

            millToolBuilder1.TlCutcomRegBuilder.Value = 18;

            millToolBuilder1.TlCor1RadBuilder.Value = 0.10000000000000001;

            millToolBuilder1.TlHeightBuilder.Value = 100.0;

            millToolBuilder1.TlFluteLnBuilder.Value = 70.0;
            NXObject nXObject1;
            nXObject1 = millToolBuilder1.Commit();
            millToolBuilder1.Destroy();









            // UPDATING  CUTTER DIAM 40MM
            //========================
            //=======================

            Tool cutterD40mm = (Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("CUTTER_D40_R1");
            MillToolBuilder millToolBuilder;
            millToolBuilder = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(cutterD40mm);
        

            millToolBuilder.TlDiameterBuilder.Value = 40.0;
            millToolBuilder.TlNumberBuilder.Value = 22;
            millToolBuilder.TlAdjRegBuilder.Value =22;
            millToolBuilder.TlCutcomRegBuilder.Value = 22;
            millToolBuilder.TlCor1RadBuilder.Value = 0.80000000000000004;
            millToolBuilder.TlHeightBuilder.Value = 150;
            millToolBuilder.TlFluteLnBuilder.Value = 30;

            NXObject nXObject10;
            nXObject1 = millToolBuilder.Commit();
            millToolBuilder.Destroy();




     



            //   ================
            // UPDATING CUTTER DIAM 25MM
            //   ================

            Session.UndoMarkId markId2;
            markId2 = theSession.SetUndoMark(Session.MarkVisibility.Visible, "Updating Tools");
            Tool cutterD25mm = (Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("CUTTER_D25_R.8");
            MillToolBuilder millToolBuilder2;
            millToolBuilder2 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(cutterD25mm);
            theSession.SetUndoMarkName(markId2, "Updating Cutter D25MM");

            millToolBuilder2.TlDiameterBuilder.Value = 25.0;
            millToolBuilder2.TlNumberBuilder.Value = 3;
            millToolBuilder2.TlAdjRegBuilder.Value = 3;
            millToolBuilder2.TlCutcomRegBuilder.Value = 3;
            millToolBuilder2.TlCor1RadBuilder.Value = 0.80000000000000004;
            millToolBuilder2.TlHeightBuilder.Value = 100.0;
            millToolBuilder2.TlFluteLnBuilder.Value = 70.0;

            NXObject nXObject2;
            nXObject2 = millToolBuilder2.Commit();
            millToolBuilder2.Destroy();



            //   ================
            // UPDATING CUTTER DIAM 20MM
            //   ================

            Session.UndoMarkId markId3;
            markId3 = theSession.SetUndoMark(Session.MarkVisibility.Visible, "Updating Tools");
            Tool cutterD20mm = (Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("CUTTER_D20_R.8");
            MillToolBuilder millToolBuilder3;
            millToolBuilder3 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(cutterD20mm);
            theSession.SetUndoMarkName(markId3, "Updating Cutter D20MM");

            millToolBuilder3.TlDiameterBuilder.Value = 20.0;
            millToolBuilder3.TlNumberBuilder.Value = 4;
            millToolBuilder3.TlAdjRegBuilder.Value = 4;
            millToolBuilder3.TlCutcomRegBuilder.Value = 4;
            millToolBuilder3.TlCor1RadBuilder.Value = 0.80000000000000004;
            millToolBuilder3.TlHeightBuilder.Value = 100.0;
            millToolBuilder3.TlFluteLnBuilder.Value = 70.0;

            NXObject nXObject3;
            nXObject3 = millToolBuilder3.Commit();
            millToolBuilder3.Destroy();


            //   ================
            // UPDATING CUTTER DIAM 16MM
            //   ================

            Session.UndoMarkId markId4;
            markId4 = theSession.SetUndoMark(Session.MarkVisibility.Visible, "Updating Tools");
            Tool cutterD16mm = (Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("CUTTER_D16_R.8");
            MillToolBuilder millToolBuilder4;
            millToolBuilder4 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(cutterD16mm);
            theSession.SetUndoMarkName(markId4, "Updating Cutter D16MM");

            millToolBuilder4.TlDiameterBuilder.Value = 16.0;
            millToolBuilder4.TlNumberBuilder.Value = 5;
            millToolBuilder4.TlAdjRegBuilder.Value = 5;
            millToolBuilder4.TlCutcomRegBuilder.Value = 5;
            millToolBuilder4.TlCor1RadBuilder.Value = 0.80000000000000004;
            millToolBuilder4.TlHeightBuilder.Value = 100.0;
            millToolBuilder4.TlFluteLnBuilder.Value = 70.0;

            NXObject nXObject4;
            nXObject4 = millToolBuilder4.Commit();
            millToolBuilder4.Destroy();

            //   UPDATING ENDMILL DIAM 12MM

            Session.UndoMarkId markId5;
            markId5 = theSession.SetUndoMark(Session.MarkVisibility.Visible, "Updating Tools");

            Tool endMillD12mm = (Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("ENDMILL_D12MM");
            MillToolBuilder millToolBuilder5;
            millToolBuilder5 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(endMillD12mm);
            theSession.SetUndoMarkName(markId5, "Updating Endmill D12MM");

            millToolBuilder5.TlDiameterBuilder.Value = 12.0;
            millToolBuilder5.TlNumberBuilder.Value = 9;
            millToolBuilder5.TlAdjRegBuilder.Value = 9;
            millToolBuilder5.TlCutcomRegBuilder.Value = 9;
            millToolBuilder5.TlHeightBuilder.Value = 80;
            millToolBuilder5.TlFluteLnBuilder.Value = 50;

            NXObject nXObject5;
            nXObject5 = millToolBuilder5.Commit();
            millToolBuilder5.Destroy();



            //   UPDATING ENDMILL DIAM 10MM

            Session.UndoMarkId markId6;
            markId6 = theSession.SetUndoMark(Session.MarkVisibility.Visible, "Updating Tools");

            Tool endMillD10mm = (Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("ENDMILL_D10MM");
            MillToolBuilder millToolBuilder6;
            millToolBuilder6 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(endMillD10mm);
            theSession.SetUndoMarkName(markId6, "Updating Endmill D10MM");

            millToolBuilder6.TlDiameterBuilder.Value = 10.0;
            millToolBuilder6.TlNumberBuilder.Value = 7;
            millToolBuilder6.TlAdjRegBuilder.Value = 7;
            millToolBuilder6.TlCutcomRegBuilder.Value = 7;
            millToolBuilder6.TlHeightBuilder.Value = 80;
            millToolBuilder6.TlFluteLnBuilder.Value = 50;

            NXObject nXObject6;
            nXObject6 = millToolBuilder6.Commit();
            millToolBuilder6.Destroy();


            //   UPDATING ENDMILL DIAM 8MM

            Session.UndoMarkId markId7;
            markId7 = theSession.SetUndoMark(Session.MarkVisibility.Visible, "Updating Tools");

            Tool endMillD8mm = (Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("ENDMILL_D8MM");
            MillToolBuilder millToolBuilder7;
            millToolBuilder7 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(endMillD8mm);
            theSession.SetUndoMarkName(markId7, "Updating Endmill D8MM");

            millToolBuilder7.TlDiameterBuilder.Value = 8.0;
            millToolBuilder7.TlNumberBuilder.Value = 8;
            millToolBuilder7.TlAdjRegBuilder.Value = 8;
            millToolBuilder7.TlCutcomRegBuilder.Value = 8;
            millToolBuilder7.TlHeightBuilder.Value = 60;
            millToolBuilder7.TlFluteLnBuilder.Value = 40;

            NXObject nXObject7;
            nXObject7 = millToolBuilder7.Commit();
            millToolBuilder7.Destroy();


            //   UPDATING FACEMILL DIAM 64MM

            Session.UndoMarkId markId8;
            markId8 = theSession.SetUndoMark(Session.MarkVisibility.Visible, "Updating Tools");

            Tool faceMillD64mm = (Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("FACEMILL_D64MM");
            MillToolBuilder millToolBuilder8;
            millToolBuilder8 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(faceMillD64mm);
            theSession.SetUndoMarkName(markId8, "Updating  FACEMILL DIAM 64MM");

            millToolBuilder8.TlDiameterBuilder.Value = 64;
            millToolBuilder8.TlNumberBuilder.Value = 2;
            millToolBuilder8.TlAdjRegBuilder.Value = 2;
            millToolBuilder8.TlCutcomRegBuilder.Value = 2;
            millToolBuilder8.ChamferLengthBuilder.Value = 5;

            NXObject nXObject8;
            nXObject8 = millToolBuilder8.Commit();
            millToolBuilder8.Destroy();




            //  alterand  comprmento ball mill



            Tool ballMillD12mm = (Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("BMD12MM");
            MillToolBuilder ballMillToolBuilder;
            ballMillToolBuilder = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(ballMillD12mm);


            ballMillToolBuilder.TlDiameterBuilder.Value = 12;
            ballMillToolBuilder.TlNumberBuilder.Value = 18;
            ballMillToolBuilder.TlAdjRegBuilder.Value = 18;
            ballMillToolBuilder.TlCutcomRegBuilder.Value = 18;
            ballMillToolBuilder.TlCor1RadBuilder.Value = 6;
            ballMillToolBuilder.TlHeightBuilder.Value = 120;
            ballMillToolBuilder.TlFluteLnBuilder.Value = 30;

            NXObject nXObject200;
            nXObject200 = ballMillToolBuilder.Commit();
            ballMillToolBuilder.Destroy();


            //  ball mill  diam 10 mm

            Tool ballMillDiam10 =  (Tool)workPart.CAMSetup.CAMGroupCollection.FindObject("BMD10MM");
            MillToolBuilder ballMillToolBuilder2;
            ballMillToolBuilder2 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(ballMillDiam10);

            ballMillToolBuilder.TlDiameterBuilder.Value = 12;
            ballMillToolBuilder.TlNumberBuilder.Value = 18;
            ballMillToolBuilder.TlAdjRegBuilder.Value = 18;
            ballMillToolBuilder.TlCutcomRegBuilder.Value = 18;
            ballMillToolBuilder.TlCor1RadBuilder.Value = 6;
            ballMillToolBuilder.TlHeightBuilder.Value = 120;
            ballMillToolBuilder.TlFluteLnBuilder.Value = 30;

            NXObject nXObject210;
            nXObject210 = ballMillToolBuilder2.Commit();
            ballMillToolBuilder2.Destroy();




        }



        // ========================
        // 🔥 MÉTODO CENTRAL
        // ========================
        private static void LoadTool(Part workPart, NCGroup group, string toolId, string newName)
        {
            CAMObject camObject;
            bool success;

            Tool tool = workPart.CAMSetup.RetrieveTool(toolId, group, out camObject, out success);

            if (success && tool != null)
            {
                tool.SetName(newName);
            }
        }

        public static void Execute()
        {
            string[] args = new string[0];
            Run(args);
        }

        public static int GetUnloadOption(string dummy)
        {
            return (int)Session.LibraryUnloadOption.Immediately;
        }
    }
}
