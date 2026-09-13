// NX 2406
// Journal created by dcard on Sun Jul 19 20:30:45 2026 Eastern Summer Time
//
using System;
using NXOpen;

public class CREATE_GROUP_FEATURES
{
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;
        NXOpen.Session.UndoMarkId markId1;
        markId1 = theSession.SetUndoMark(NXOpen.Session.MarkVisibility.Invisible, null);



        NXOpen.CAM.GroupFeatures groupFeatures1;
        groupFeatures1 = workPart.CAMSetup.CAMGroupCollection.CreateGroupFeatures();

        groupFeatures1.GeometryLocation = "Automatic";

        groupFeatures1.FeaturesToGroupType = NXOpen.CAM.GroupFeatures.FeaturesToGroupTypes.SpecifyFeatures;

        groupFeatures1.GeometryLocation = "WORKPIECE";


        groupFeatures1.FeaturesToGroupType = NXOpen.CAM.GroupFeatures.FeaturesToGroupTypes.All;

        NXOpen.Point3d origin1 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d vector1 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Direction direction1;
        direction1 = workPart.Directions.CreateDirection(origin1, vector1, NXOpen.SmartObject.UpdateOption.AfterModeling);

        string[] featureTypes1 = new string[84];


        featureTypes1[0] = "STEP1POCKET";
        featureTypes1[1] = "STEP1HOLE";
        featureTypes1[2] = "STEP2HOLE";
        featureTypes1[3] = "STEP1POCKET_THREAD";

        featureTypes1[4] = "SLOT_PARTIAL_RECTANGULAR";
        featureTypes1[5] = "POCKET_RECTANGULAR_STRAIGHT";
        featureTypes1[6] = "POCKET_OBROUND_STRAIGHT";
        featureTypes1[7] = "SLOT_RECTANGULAR";
        featureTypes1[8] = "SLOT_OBROUND";
        featureTypes1[9] = "SLOT_PARTIAL_OBROUND";
        featureTypes1[10] = "SLOT_PARTIAL_RECTANGULAR";
        featureTypes1[11] = "SLOT_PARTIAL_ROUND";

        featureTypes1[12] = "BOSS_RECTANGULAR_STRAIGHT";
        featureTypes1[13] = "BOSS_ROUND_STRAIGHT";
        featureTypes1[14] = "BOSS_ROUND_STRAIGHT_THREAD";

        featureTypes1[15] = "POCKET_ROUND_TAPERED";
        featureTypes1[16] = "POCKET_CLOSED";
        featureTypes1[17] = "POCKET_OBROUND_CURVED_STRAIGHT";
        featureTypes1[18] = "POCKET_FREE_SHAPED_STRAIGHT";
        featureTypes1[19] = "POCKET_OPEN";

        featureTypes1[20] = "SURFACE_PLANAR";
        featureTypes1[21] = "SURFACE_PLANAR_RECTANGULAR";
        featureTypes1[22] = "SURFACE_PLANAR_ROUND";

        // OBS: linha 23 antes tinha "FG_WEDM_RECTANGULAR_STRAIGHT" (nome de
        // GRUPO, com prefixo FG_, colado por engano nessa lista de TIPOS de
        // feature) - removido, era um tipo inválido que o NX nunca ia achar.
        featureTypes1[23] = "WEDM_RECTANGULAR_STRAIGHT";

        // NOVO - faltavam ser reconhecidos/agrupados (mesmos dois tipos
        // adicionados no RECOGNIZE_FEATURES.cs e no FBM_ALL_FEATURES_MACHINED.cs).
        featureTypes1[24] = "WEDM_OBROUND_STRAIGHT";
        featureTypes1[25] = "WEDM_FREE_SHAPED_STRAIGHT";

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
