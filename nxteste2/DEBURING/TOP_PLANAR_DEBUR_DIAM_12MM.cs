
using System;
using NXOpen;


namespace NX_3_PLUS_TWO_TOOLPATHS
{



    public class TOP_PLANAR_DEBUR_DIAM_12MM
    {
        public static void Run(string[] args)
        {
            NXOpen.Session theSession = NXOpen.Session.GetSession();
            NXOpen.Part workPart = theSession.Parts.Work;
            NXOpen.Part displayPart = theSession.Parts.Display;

            // BEGINNING OF JOURNAL
            NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NC_PROGRAM"));
            NXOpen.CAM.Method method1 = ((NXOpen.CAM.Method)workPart.CAMSetup.CAMGroupCollection.FindObject("MILL_FINISH"));
            NXOpen.CAM.NCGroup nCGroup2 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("NONE"));
            NXOpen.CAM.FeatureGeometry featureGeometry1 = ((NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
            NXOpen.CAM.Operation operation1;
            operation1 = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(nCGroup1, method1, nCGroup2, featureGeometry1, "mill_planar", "PLANAR_DEBURRING", NXOpen.CAM.OperationCollection.UseDefaultName.True, "PLANAR_DEBURRING", "Planar Deburring");


            NXOpen.CAM.SurfaceContour surfaceContour1 = ((NXOpen.CAM.SurfaceContour)operation1);
            NXOpen.CAM.EdgeChamferBuilder edgeChamferBuilder1;
            edgeChamferBuilder1 = workPart.CAMSetup.CAMOperationCollection.CreateEdgeChamferBuilder(surfaceContour1);

            NXOpen.NXObject nXObject1;
            nXObject1 = edgeChamferBuilder1.Commit();

            // GETTING THE TOOL
            NXOpen.CAM.NCGroup nCGroup3 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("GENERIC_MACHINE"));
            NXOpen.CAM.NCGroup nCGroup4;
            nCGroup4 = workPart.CAMSetup.CAMGroupCollection.CreateToolWithUserName(nCGroup3, "mill_planar", "CHAMFER_MILL", NXOpen.CAM.NCGroupCollection.UseDefaultName.False, "CHAMFERD12MM", "Chamfer Mill");

            NXOpen.CAM.CAMObject[] objectsToBeMoved1 = new NXOpen.CAM.CAMObject[1];
            NXOpen.CAM.Tool tool1 = ((NXOpen.CAM.Tool)nCGroup4);
            objectsToBeMoved1[0] = tool1;
            workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved1, nCGroup3, NXOpen.CAM.CAMSetup.Paste.Inside);


            NXOpen.CAM.MillToolBuilder millToolBuilder1;
            millToolBuilder1 = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool1);


            millToolBuilder1.TlDiameterBuilder.Value = 12.0;


            millToolBuilder1.TlNumberBuilder.Value = 15;

            millToolBuilder1.TlAdjRegBuilder.Value = 15;

            millToolBuilder1.TlCutcomRegBuilder.Value = 15;

            millToolBuilder1.UseTaperedShank = true;


            NXOpen.NXObject nXObject2;
            nXObject2 = millToolBuilder1.Commit();


            millToolBuilder1.Destroy();




            NXOpen.CAM.CAMObject[] objectsToBeMoved2 = new NXOpen.CAM.CAMObject[1];
            NXOpen.CAM.SurfaceContour surfaceContour2 = ((NXOpen.CAM.SurfaceContour)nXObject1);
            objectsToBeMoved2[0] = surfaceContour2;
            NXOpen.CAM.Tool tool2 = ((NXOpen.CAM.Tool)nXObject2);
            workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved2, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);

            NXOpen.CAM.CAMObject[] objectsToBeMoved3 = new NXOpen.CAM.CAMObject[1];
            objectsToBeMoved3[0] = surfaceContour2;
            workPart.CAMSetup.MoveObjects(NXOpen.CAM.CAMSetup.View.MachineTool, objectsToBeMoved3, tool2, NXOpen.CAM.CAMSetup.Paste.Inside);


            NXOpen.CAM.EdgeChamferBuilder edgeChamferBuilder2;
            edgeChamferBuilder2 = workPart.CAMSetup.CAMOperationCollection.CreateEdgeChamferBuilder(surfaceContour2);


            edgeChamferBuilder2.FeedsBuilder.SpindleRpmBuilder.Value = 4000.0;

            edgeChamferBuilder2.FeedsBuilder.FeedCutBuilder.Value = 800.0;


            NXOpen.NXObject nXObject3;
            nXObject3 = edgeChamferBuilder2.Commit();

            NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
            NXOpen.CAM.SurfaceContour surfaceContour3 = ((NXOpen.CAM.SurfaceContour)nXObject3);
            objects1[0] = surfaceContour3;
            workPart.CAMSetup.GenerateToolPath(objects1);


            edgeChamferBuilder2.Destroy();



        }
        public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
    }

}