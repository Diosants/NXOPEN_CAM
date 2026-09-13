// NX 2406
// Journal created by STELMEC on Wed May 27 14:51:19 2026 Hora oficial do Brasil
//
using System;
using NXOpen;

public class RECOGNIZE_FEATURES
{
    public static void Run(string[] args)
    {
        NXOpen.Session theSession = NXOpen.Session.GetSession();
        NXOpen.Part workPart = theSession.Parts.Work;
        NXOpen.Part displayPart = theSession.Parts.Display;



        NXOpen.CAM.CAMObject nullNXOpen_CAM_CAMObject = null;
        NXOpen.CAM.FeatureRecognitionBuilder featureRecognitionBuilder1;
        featureRecognitionBuilder1 = workPart.CAMSetup.CreateFeatureRecognitionBuilder(nullNXOpen_CAM_CAMObject);

        NXOpen.CAM.ManualFeatureBuilder manualFeatureBuilder1;
        manualFeatureBuilder1 = featureRecognitionBuilder1.CreateManualFeatureBuilder();

        featureRecognitionBuilder1.AssignColor = false;

        featureRecognitionBuilder1.AddCadFeatureAttributes = false;

        featureRecognitionBuilder1.MapFeatures = false;


        NXOpen.Point3d origin1 = new NXOpen.Point3d(0.0, 0.0, 0.0);
        NXOpen.Vector3d vector1 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
        NXOpen.Direction direction1;
        direction1 = workPart.Directions.CreateDirection(origin1, vector1, NXOpen.SmartObject.UpdateOption.AfterModeling);

        featureRecognitionBuilder1.RecognitionType = NXOpen.CAM.FeatureRecognitionBuilder.RecognitionEnum.Parametric;

        // ANTES: true - com isso, o NX confia no NOME/TIPO da feature de
        // MODELAGEM original (a feature "Hole" criada lá no CAD, com o
        // sub-tipo que a pessoa escolheu na hora - ex.: "Counterbore") em
        // vez de reavaliar a geometria ATUAL do furo. Se um furo foi
        // modelado em algum momento como counterbore/rosca e depois editado
        // pra virar um furo cego simples (ou o histórico de modelagem
        // ficou "sujo"), o NX continua marcando ele como counterbore mesmo
        // ele hoje sendo um furo cego de diâmetro único - é exatamente o
        // sintoma relatado (furo reconhecido como "Counter Bore" mas com só
        // 1 diâmetro, sem segundo furo). Mudado pra false: o NX ignora o
        // nome/tipo herdado da modelagem e reclassifica com base só na
        // geometria atual (análise Parametric de verdade).
        featureRecognitionBuilder1.UseFeatureNameAsType = false;

        featureRecognitionBuilder1.IgnoreWarnings = false;

        NXOpen.Direction[] vecdirections1 = new NXOpen.Direction[1];
        vecdirections1[0] = direction1;
        featureRecognitionBuilder1.SetMachiningAccessDirection(vecdirections1, 9.9999999999999995e-07);

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

        //  WEDM FEATURES

        featureTypes1[23] = "WEDM_RECTANGULAR_STRAIGHT";

        // NOVO - faltavam ser reconhecidos (já usados em
        // FBM_ALL_FEATURES_MACHINED.cs, mas essa recognição ali é interna
        // e separada; este RECOGNIZE_FEATURES.cs é rodado à parte, então
        // precisa da mesma lista pra também achar esses dois tipos).
        featureTypes1[24] = "WEDM_OBROUND_STRAIGHT";
        featureTypes1[25] = "WEDM_FREE_SHAPED_STRAIGHT";



        featureRecognitionBuilder1.SetFeatureTypes(featureTypes1);

        featureRecognitionBuilder1.GeometrySearchType = NXOpen.CAM.FeatureRecognitionBuilder.GeometrySearch.Workpiece;

        NXOpen.CAM.CAMFeature[] features1;
        features1 = featureRecognitionBuilder1.FindFeatures();


        NXOpen.NXObject nXObject1;
        nXObject1 = featureRecognitionBuilder1.Commit();



        featureRecognitionBuilder1.Destroy();

        manualFeatureBuilder1.Destroy();



    }
    public static int GetUnloadOption(string dummy) { return (int)NXOpen.Session.LibraryUnloadOption.Immediately; }
}
