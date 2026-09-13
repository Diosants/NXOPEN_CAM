// NX 2406
// Journal created by dcard on Sun Jul  5 12:28:38 2026 Eastern Summer Time
//
using System;
using System.Collections.Generic;
using NXOpen;
using NXOpen.CAM;

public class CopiaFindHoles
{
    public static void Run(string[] args)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        Part displayPart = theSession.Parts.Display;

        FeatureGeometry featureGeometry1 = ((FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));
        NCGroup nCGroup1;
        nCGroup1 = workPart.CAMSetup.CAMGroupCollection.CreateGeometryWithUserName(featureGeometry1, "hole_making", "HOLE_BOSS_GEOM", NCGroupCollection.UseDefaultName.False, "M12", "Hole Boss Geom");


        FeatureGeometry featureGeometry2 = ((FeatureGeometry)nCGroup1);
        HoleBossGeometry holeBossGeometry1;
        holeBossGeometry1 = workPart.CAMSetup.CAMGroupCollection.CreateHoleBossGeometryBuilder(featureGeometry2);

        GeometrySetList geometrySetList1;
        geometrySetList1 = holeBossGeometry1.FeatureGeometry.GeometryList;


        // O FeatureSet agora é criado dentro do loop, um por furo (ver mais abaixo).



        PartLoadStatus partLoadStatus1;
        partLoadStatus1 = workPart.LoadThisPartFully();

        partLoadStatus1.Dispose();

        // ---------------------------------------------------------------
        // FILTRO POR ATRIBUTO (substitui o FindObject fixo pelo nome)
        // ---------------------------------------------------------------
        double diametroAlvo = 12.10;
        double tolerancia = 0.05;

        // Nomes de atributo a checar. Furos com múltiplos diâmetros (contra-furo,
        // chanfro, rosca em degrau, etc.) guardam cada diâmetro num atributo separado.
        string[] nomesDiametro = new string[] { "Diameter", "Diameter_1", "Diameter_2" };

        List<NXObject> encontradas = new List<NXObject>();

        theSession.ListingWindow.Open();

        foreach (CAMFeature feature in workPart.CAMFeatures)
        {
            // CAMFeature NAO usa o sistema genérico de atributos do NXObject.
            // "Diameter", "Depth", "TIP_ANGLE" etc. ficam em feature.Attributes,
            // que é uma coleção de NXOpen.CAM.CAMAttribute.
            bool jaAdicionada = false;

            foreach (CAMAttribute attr in feature.Attributes)
            {
                foreach (string nome in nomesDiametro)
                {
                    if (string.Equals(attr.Name, nome, StringComparison.OrdinalIgnoreCase))
                    {
                        double diametro = attr.GetDoubleValue();

                        theSession.ListingWindow.WriteLine(
                            "Feature: " + feature.Name + " | " + attr.Name + " = " + diametro);

                        if (!jaAdicionada && Math.Abs(diametro - diametroAlvo) < tolerancia)
                        {
                            encontradas.Add(feature);
                            jaAdicionada = true; // evita adicionar a mesma feature 2x se mais de 1 atributo bater
                        }

                        break; // já tratou este atributo, vai para o próximo attr da feature
                    }
                }
            }
        }

        if (encontradas.Count == 0)
        {
            theSession.ListingWindow.Open();
            theSession.ListingWindow.WriteLine("Nenhuma feature encontrada com Diameter = " + diametroAlvo);
        }
        else
        {
            theSession.ListingWindow.WriteLine(
                "Total de furos encontrados com Diameter = " + diametroAlvo + " -> " + encontradas.Count);

            // CreateFeature cria APENAS 1 feature por chamada — o array de entrada é a
            // geometria (faces) que define ESSA feature, não uma lista de features distintas.
            // Por isso, para criar uma feature para CADA furo encontrado, chamamos
            // CreateFeature uma vez por furo, dentro do loop.
            int totalCriadas = 0;
            int totalErros = 0;

            CAMFeature nullNXOpen_CAM_CAMFeature = null;

            foreach (NXObject furo in encontradas)
            {
                try
                {
                    // Cria um FeatureSet NOVO para este furo (reaproveitar o mesmo FeatureSet
                    // para vários CreateFeature fazia a chamada seguinte substituir a anterior).
                    NXOpen.CAM.FBM.FeatureSet featureSetIndividual;
                    featureSetIndividual = holeBossGeometry1.FeatureGeometry.AddFeatureSet(nullNXOpen_CAM_CAMFeature, "NXHOLE");

                    featureSetIndividual.AngleToleranceEdges = 0.0;
                    featureSetIndividual.Intol = 0.0;
                    featureSetIndividual.Outtol = 0.0;

                    NXObject[] umFuro = new NXObject[] { furo };

                    NXOpen.CAM.FBM.Feature featureIndividual;
                    featureIndividual = featureSetIndividual.CreateFeature(umFuro);

                    totalCriadas++;
                }
                catch (Exception ex)
                {
                    totalErros++;
                    theSession.ListingWindow.WriteLine(
                        "ERRO ao criar feature para " + furo.JournalIdentifier + " -> " + ex.Message);
                }
            }

            theSession.ListingWindow.WriteLine("Total de features criadas: " + totalCriadas);
            theSession.ListingWindow.WriteLine("Total de erros: " + totalErros);

            NXObject nXObject1;
            nXObject1 = holeBossGeometry1.Commit();
        }

        holeBossGeometry1.Destroy();

    }
    public static int GetUnloadOption(string dummy) { return (int)Session.LibraryUnloadOption.Immediately; }
}
