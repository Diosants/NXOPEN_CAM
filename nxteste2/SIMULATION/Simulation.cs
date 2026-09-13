// NX 2406
// Journal created by dcard on Tue Jun  9 19:33:09 2026 Eastern Summer Time
//
using System;
using NXOpen;


namespace NXOPEN_CHECKS
{



    public class GougeCheck
    {
        public static void Run(string[] args)
        {
            NXOpen.Session theSession = NXOpen.Session.GetSession();
            NXOpen.Part workPart = theSession.Parts.Work;
            NXOpen.Part displayPart = theSession.Parts.Display;
            NXOpen.CAM.NCGroup nCGroup1 = ((NXOpen.CAM.NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject("PROGRAM"));
            theSession.CAMSession.PathDisplay.ShowToolPath(nCGroup1);

            NXOpen.CAM.CAMObject[] objects1 = new NXOpen.CAM.CAMObject[1];
            objects1[0] = nCGroup1;
            NXOpen.CAM.GougeCheckBuilder gougeCheckBuilder1;
            gougeCheckBuilder1 = workPart.CAMSetup.CreateGougeCheckBuilder(objects1);


            NXOpen.NXObject nXObject1;
            nXObject1 = gougeCheckBuilder1.Commit();


            gougeCheckBuilder1.Destroy();

            theSession.CAMSession.PathDisplay.HideToolPath(nCGroup1);


        }
        public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
    }
}
