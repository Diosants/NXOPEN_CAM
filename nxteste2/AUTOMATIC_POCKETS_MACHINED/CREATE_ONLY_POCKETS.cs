// NX 2406
// Journal created by STELMEC on Thu May 28 12:29:01 2026 Hora oficial do Brasil
//
using System;
using NXOpen;

public class CREATE_ONLY_POCKETS
{
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;
        UI theUI = UI.GetUI();



        NXOpen.CAM.GroupFeatures groupFeatures1;
        groupFeatures1 = workPart.CAMSetup.CAMGroupCollection.CreateGroupFeatures();

        groupFeatures1.GeometryLocation = "Automatic";

        groupFeatures1.FeaturesToGroupType = NXOpen.CAM.GroupFeatures.FeaturesToGroupTypes.SpecifyFeatures;

        groupFeatures1.FeaturesToGroupType = NXOpen.CAM.GroupFeatures.FeaturesToGroupTypes.All;


        NXOpen.Point3d origin1 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d vector1 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Direction direction1;
        direction1 = workPart.Directions.CreateDirection(origin1, vector1, NXOpen.SmartObject.UpdateOption.AfterModeling);
        string[] featureTypes1 = new string[3];
        featureTypes1[0] = "FG_POCKET_OBROUND_STRAIGHT";
        featureTypes1[1] = "FG_POCKET_RECTANGULAR_STRAIGHT";
        featureTypes1[2] = "FG_SLOT_PARTIAL_RECTANGULAR";

        groupFeatures1.SetFeatureTypes(featureTypes1);

        NXOpen.Direction[] vecdirectiontags1 = new NXOpen.Direction[1];
        vecdirectiontags1[0] = direction1;
        groupFeatures1.SetMachiningAccessDirections(vecdirectiontags1, 9.9999999999999995e-07);

        groupFeatures1.CreateFeatureGroups();


        NXOpen.NXObject nXObject1;
        nXObject1 = groupFeatures1.Commit();

        groupFeatures1.Destroy();



    }
    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
}
