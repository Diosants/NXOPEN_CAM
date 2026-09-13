// NX 2406
// Journal created by dcard on Sun Jul  5 12:28:38 2026 Eastern Summer Time
//
using System;
using System.Collections.Generic;
using NXOpen;
using NXOpen.CAM;

public class FIND_ALL_HOLES_BY_DIAMETER
{
    public static void Run(string[] args)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        Part displayPart = theSession.Parts.Display;

        FeatureGeometry featureGeometryWorkpiece = ((FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE"));

        PartLoadStatus partLoadStatus1;
        partLoadStatus1 = workPart.LoadThisPartFully();
        partLoadStatus1.Dispose();

        theSession.ListingWindow.Open();

        // ---------------------------------------------------------------
        // TABELA DE FURO BASE PARA ROSCA METRICA (referencia ISO, ate M30)
        // Furo base = diametro de broca recomendado para aquela rosca.
        // ---------------------------------------------------------------
        double[] diametrosBase = new double[]
        {
        5.2,   // M6  (passo fino 0.75)
        6.8,   // M8  (passo grosso 1.25)
        8.5,   // M10 (passo grosso 1.5)
        10.2,  // M12 (passo grosso 1.75)
        12.0,  // M14
        14.0,  // M16
        15.5,  // M18
        17.5,  // M20
        19.5,  // M22
        21.0,  // M24
        26.5,  // M30
        };

        string[] designacoesRosca = new string[]
        {
        "M6",
        "M8",
        "M10",
        "M12",
        "M14",
        "M16",
        "M18",
        "M20",
        "M22",
        "M24",
        "M30",
        };

        double toleranciaRosca = 0.01; // margem para casar o diametro medido com o furo base da tabela

        // ---------------------------------------------------------------
        // TABELA DE SOCKET HEAD (counterbore) - diametro MAIOR onde a
        // cabeca do parafuso allen (DIN 912 / ISO 4762) fica alojada.
        // ---------------------------------------------------------------
        double[] diametrosSocketHead = new double[]
        {
        11.25, // M6
        15.00, // M8
        18.00, // M10
        20.00, // M12
        25.80, // M16
        28.50, // M18
        33.00  // M20
        };

        string[] designacoesSocketHead = new string[]
        {
        "M6_SOCKET_HEAD",
        "M8_SOCKET_HEAD",
        "M10_SOCKET_HEAD",
        "M12_SOCKET_HEAD",
        "M14_SOCKET_HEAD",
        "M16_SOCKET_HEAD",
        "M18_SOCKET_HEAD",
        "M20_SOCKET_HEAD"
        };

        double toleranciaSocketHead = 0.01; // counterbore tem mais folga de fabricacao que broca de rosca

        // ---------------------------------------------------------------
        // LISTA EXPLICITA DE TIPOS DE FURO (extraida da lista completa de
        // feature types possiveis no FBM). Qualquer CAMFeature cujo Type
        // bata EXATAMENTE com um destes e considerado furo.
        // ---------------------------------------------------------------
        string[] tiposDeFuro = new string[]
        {
            "STEP1POCKET",
            "STEP1HOLE",
            "STEP1HOLE_THREAD",
            "STEP2HOLE",
            "STEP2HOLE_THREAD",
            "STEP3HOLE",
            "STEP3HOLE1",
            "STEP3HOLE_THREAD",
            "STEP3HOLE1_THREAD",
            "STEP4HOLE",
            "STEP4HOLE1",
            "STEP4HOLE_THREAD",
            "STEP4HOLE1_THREAD",
            "STEP5HOLE",
            "STEP5HOLE1",
            "STEP5HOLE2",
            "STEP5HOLE_THREAD",
            "STEP5HOLE1_THREAD",
            "STEP5HOLE2_THREAD",
            "STEP6HOLE",
            "STEP6HOLE1",
            "STEP6HOLE2",
            "STEP6HOLE_THREAD",
            "STEP6HOLE1_THREAD",
            "STEP6HOLE2_THREAD",
            "STEP1POCKET_THREAD",
            "STEP2POCKET_THREAD",
            "STEP2HOLE_THREAD",
            "STEP1HOLE_THREAD",
            "COUNTER_BORE_HOLE",
            "COUNTER_SUNK_HOLE",
            "HOLE_ROUND_INTERRUPTED_STRAIGHT",
            "HOLE_ROUND_TAPERED",
            "HOLE_OBROUND_CURVED_STRAIGHT",
            "HOLE_FREE_SHAPED_STRAIGHT",
            "HOLE_OBROUND_STRAIGHT",
            "HOLE_RECTANGULAR_STRAIGHT"
        };

        // ---------------------------------------------------------------
        // 1) LEVANTAMENTO: classifica cada CAMFeature e agrupa pelo NOME resultante
        //
        // Regra pedida:
        //  - So processa a feature se o Type dela estiver na lista tiposDeFuro
        //    acima (cobre TODOS os tipos de furo conhecidos) - exclui POCKET,
        //    SLOT, BOSS, GROOVE, WEDM, SURFACE, NOTCH e qualquer outra coisa
        //    que nao seja furo.
        //  - Se a feature tiver Diameter_2 E ele bater com a tabela de ROSCA ou
        //    SOCKET HEAD -> usa Diameter_2 para classificar (rosca/socket head).
        //  - Caso contrario (nao tem Diameter_2, ou Diameter_2 nao bate em
        //    nenhuma tabela) -> usa o atributo Diameter puro (furo comum).
        //  - Diameter_1 e ignorado sempre, de proposito.
        // ---------------------------------------------------------------

        double tolerancia = 0.05;     // usado apenas para arredondar/nomear furos comuns
        int casasDecimais = 2;

        Dictionary<string, List<CAMFeature>> grupos = new Dictionary<string, List<CAMFeature>>();
        Dictionary<string, double> diametroDoGrupo = new Dictionary<string, double>();
        Dictionary<string, double> profundidadeDoGrupo = new Dictionary<string, double>();

        int totalFeatures = 0;
        int totalIgnoradasPorTipo = 0;
        int totalSemDiametro = 0;

        // Guarda os Types encontrados na peca que NAO bateram com a lista, para
        // voce conferir no final se falta algum tipo novo para adicionar acima.
        List<string> tiposNaoReconhecidosEncontrados = new List<string>();

        foreach (CAMFeature feature in workPart.CAMFeatures)
        {
            totalFeatures++;

            string tipoFeature = feature.Type;

            // FILTRO POR TIPO: so continua se o Type estiver na lista tiposDeFuro
            bool ehTipoDeFuro = false;

            if (!string.IsNullOrEmpty(tipoFeature))
            {
                foreach (string tipoConhecido in tiposDeFuro)
                {
                    if (string.Equals(tipoFeature, tipoConhecido, StringComparison.OrdinalIgnoreCase))
                    {
                        ehTipoDeFuro = true;
                        break;
                    }
                }
            }

            if (!ehTipoDeFuro)
            {
                totalIgnoradasPorTipo++;

                if (!string.IsNullOrEmpty(tipoFeature) && !tiposNaoReconhecidosEncontrados.Contains(tipoFeature))
                    tiposNaoReconhecidosEncontrados.Add(tipoFeature);

                continue; // nao e um tipo de furo (pocket, slot, boss, groove, wedm, surface...) -> ignora
            }

            double? diamD2 = null;
            double? diamD = null;
            double? diamD1 = null;
            double? diamQualquerOutro = null;
            string nomeAtributoQualquerOutro = null;
            double? profD = null;
            double? profD1 = null;

            foreach (CAMAttribute attr in feature.Attributes)
            {
                if (string.Equals(attr.Name, "Diameter_2", StringComparison.OrdinalIgnoreCase))
                {
                    diamD2 = attr.GetDoubleValue();
                }
                else if (string.Equals(attr.Name, "Diameter", StringComparison.OrdinalIgnoreCase))
                {
                    diamD = attr.GetDoubleValue();
                }
                else if (string.Equals(attr.Name, "Diameter_1", StringComparison.OrdinalIgnoreCase))
                {
                    diamD1 = attr.GetDoubleValue();
                }
                else if (string.Equals(attr.Name, "Depth", StringComparison.OrdinalIgnoreCase))
                {
                    profD = attr.GetDoubleValue();
                }
                else if (string.Equals(attr.Name, "Depth_1", StringComparison.OrdinalIgnoreCase))
                {
                    profD1 = attr.GetDoubleValue();
                }
                else if (attr.Type == CAMAttribute.ValueType.Double && !diamQualquerOutro.HasValue)
                {
                    // Guarda o primeiro atributo numerico "extra" encontrado (ex: Diameter_3,
                    // Diameter_4, ou qualquer outro nome usado por tipos de furo diferentes),
                    // para servir de fallback se nenhum dos tres de cima existir.
                    diamQualquerOutro = attr.GetDoubleValue();
                    nomeAtributoQualquerOutro = attr.Name;
                }
            }

            // Profundidade: prioriza Depth; usa Depth_1 como fallback se Depth nao existir
            double? profundidadeEncontrada = profD.HasValue ? profD : profD1;

            if (!diamD2.HasValue && !diamD.HasValue && !diamD1.HasValue && !diamQualquerOutro.HasValue)
            {
                totalSemDiametro++;
                theSession.ListingWindow.WriteLine(
                    "AVISO: feature '" + feature.Name + "' (Type=" + tipoFeature +
                    ") e do tipo furo mas nao tem NENHUM atributo numerico -> sera agrupada por Type.");
            }

            string nomeGrupo = null;
            double diametroUsado = 0.0;

            // 1) Tenta classificar pelo Diameter_2 (rosca ou socket head)
            if (diamD2.HasValue)
            {
                string designacao = BuscarDesignacao(diamD2.Value, diametrosBase, designacoesRosca, toleranciaRosca);

                if (designacao == null)
                    designacao = BuscarDesignacao(diamD2.Value, diametrosSocketHead, designacoesSocketHead, toleranciaSocketHead);

                if (designacao != null)
                {
                    nomeGrupo = designacao.Replace(",", "_").Replace(".", "_");
                    diametroUsado = diamD2.Value;
                }
            }

            // 2) Nao classificou pelo Diameter_2 -> usa Diameter puro (furo comum)
            if (nomeGrupo == null && diamD.HasValue)
            {
                diametroUsado = diamD.Value;
                double diamArred = Math.Round(diametroUsado, casasDecimais);
                nomeGrupo = "HOLE_DIAMETER" + diamArred.ToString("0.00").Replace(".", "_");
            }

            // 3) Ainda nao classificou -> usa Diameter_1 como fallback (furo comum tambem)
            if (nomeGrupo == null && diamD1.HasValue)
            {
                diametroUsado = diamD1.Value;
                double diamArred = Math.Round(diametroUsado, casasDecimais);
                nomeGrupo = "HOLE_DIAMETER" + diamArred.ToString("0.00").Replace(".", "_");
            }

            // 4) Ainda nao classificou -> usa qualquer outro atributo numerico que a
            //    feature tenha (ex: Diameter_3, Diameter_4, etc.)
            if (nomeGrupo == null && diamQualquerOutro.HasValue)
            {
                diametroUsado = diamQualquerOutro.Value;
                double diamArred = Math.Round(diametroUsado, casasDecimais);
                nomeGrupo = "HOLE_DIAMETER" + diamArred.ToString("0.00").Replace(".", "_");

                theSession.ListingWindow.WriteLine(
                    "  (feature '" + feature.Name + "' usou o atributo '" + nomeAtributoQualquerOutro +
                    "' como diametro, por nao ter Diameter/Diameter_1/Diameter_2)");
            }

            // 5) Nao tem NENHUM atributo numerico -> agrupa por Type, garantindo que
            //    a feature ainda assim seja incluida em alguma geometria.
            if (nomeGrupo == null)
            {
                diametroUsado = 0.0;
                string tipoSanitizado = (tipoFeature ?? "DESCONHECIDO").Replace(",", "_").Replace(".", "_");
                nomeGrupo = "HOLE_SEM_DIAMETRO_" + tipoSanitizado;
            }

            theSession.ListingWindow.WriteLine(
                "Feature: " + feature.Name + " (Type=" + tipoFeature + ") -> grupo '" + nomeGrupo +
                "' (diametro usado = " + diametroUsado +
                ", profundidade = " + (profundidadeEncontrada.HasValue ? profundidadeEncontrada.Value.ToString() : "N/A") + ")");

            if (!grupos.ContainsKey(nomeGrupo))
            {
                grupos[nomeGrupo] = new List<CAMFeature>();
                diametroDoGrupo[nomeGrupo] = diametroUsado;
                profundidadeDoGrupo[nomeGrupo] = profundidadeEncontrada.HasValue ? profundidadeEncontrada.Value : 0.0;
            }
            else if (profundidadeEncontrada.HasValue && profundidadeEncontrada.Value > profundidadeDoGrupo[nomeGrupo])
            {
                // Mantem a MAIOR profundidade do grupo (a operacao de furacao
                // precisa cobrir o furo mais fundo daquele diametro)
                profundidadeDoGrupo[nomeGrupo] = profundidadeEncontrada.Value;
            }

            grupos[nomeGrupo].Add(feature);
        }

        theSession.ListingWindow.WriteLine("");
        theSession.ListingWindow.WriteLine("Total de CAMFeatures na peca: " + totalFeatures);
        theSession.ListingWindow.WriteLine("Ignoradas por nao serem tipo furo (Type fora da lista): " + totalIgnoradasPorTipo);

        if (tiposNaoReconhecidosEncontrados.Count > 0)
        {
            theSession.ListingWindow.WriteLine("Types encontrados na peca que NAO estao na lista tiposDeFuro:");
            foreach (string tipoDesconhecido in tiposNaoReconhecidosEncontrados)
            {
                theSession.ListingWindow.WriteLine("  " + tipoDesconhecido);
            }
            theSession.ListingWindow.WriteLine("(Se algum desses acima for realmente um furo, adicione o nome no array tiposDeFuro.)");
        }

        theSession.ListingWindow.WriteLine("Ignoradas por nao terem diametro utilizavel: " + totalSemDiametro);
        theSession.ListingWindow.WriteLine("Grupos distintos encontrados: " + grupos.Count);
        foreach (KeyValuePair<string, List<CAMFeature>> g in grupos)
        {
            theSession.ListingWindow.WriteLine(
                "  " + g.Key + " -> " + g.Value.Count + " furo(s), profundidade maxima = " + profundidadeDoGrupo[g.Key]);
        }
        theSession.ListingWindow.WriteLine("");

        // ---------------------------------------------------------------
        // 2) CRIACAO: uma HoleBossGeometry por grupo, ja nomeada
        // ---------------------------------------------------------------
        int totalGeometriasCriadas = 0;

        foreach (KeyValuePair<string, List<CAMFeature>> grupo in grupos)
        {
            string nomeGrupo = grupo.Key;
            List<CAMFeature> furosDoGrupo = grupo.Value;
            double diametroRepresentativo = diametroDoGrupo[nomeGrupo];
            double profundidadeRepresentativa = profundidadeDoGrupo[nomeGrupo];

            try
            {
                NCGroup nCGroupAtual;
                nCGroupAtual = workPart.CAMSetup.CAMGroupCollection.CreateGeometryWithUserName(
                    featureGeometryWorkpiece,
                    "hole_making",
                    "HOLE_BOSS_GEOM",
                    NCGroupCollection.UseDefaultName.False,
                    nomeGrupo,
                    "Hole Boss Geom " + nomeGrupo);

                FeatureGeometry featureGeometryAtual = ((FeatureGeometry)nCGroupAtual);
                HoleBossGeometry holeBossGeometryAtual;
                holeBossGeometryAtual = workPart.CAMSetup.CAMGroupCollection.CreateHoleBossGeometryBuilder(featureGeometryAtual);

                GeometrySetList geometrySetListAtual;
                geometrySetListAtual = holeBossGeometryAtual.FeatureGeometry.GeometryList;

                CAMFeature nullNXOpen_CAM_CAMFeature = null;

                int criadasNesteGrupo = 0;
                int errosNesteGrupo = 0;

                foreach (CAMFeature furo in furosDoGrupo)
                {
                    try
                    {
                        NXOpen.CAM.FBM.FeatureSet featureSetIndividual;
                        featureSetIndividual = holeBossGeometryAtual.FeatureGeometry.AddFeatureSet(nullNXOpen_CAM_CAMFeature, "NXHOLE");

                        featureSetIndividual.AngleToleranceEdges = 0.0;
                        featureSetIndividual.Intol = 0.0;
                        featureSetIndividual.Outtol = 0.0;

                        NXObject[] umFuro = new NXObject[] { furo };

                        NXOpen.CAM.FBM.Feature featureIndividual;
                        featureIndividual = featureSetIndividual.CreateFeature(umFuro);

                        criadasNesteGrupo++;
                    }
                    catch (Exception exFuro)
                    {
                        errosNesteGrupo++;
                        theSession.ListingWindow.WriteLine(
                            "  ERRO ao criar feature para " + furo.JournalIdentifier + " -> " + exFuro.Message);
                    }
                }

                NXObject commitResult;
                commitResult = holeBossGeometryAtual.Commit();

                holeBossGeometryAtual.Destroy();

                theSession.ListingWindow.WriteLine(
                    "Geometria '" + nomeGrupo + "' criada (diametro representativo " + diametroRepresentativo +
                    ", profundidade representativa " + profundidadeRepresentativa + "): " +
                    criadasNesteGrupo + " furo(s), " + errosNesteGrupo + " erro(s).");

                totalGeometriasCriadas++;
            }
            catch (Exception exGrupo)
            {
                theSession.ListingWindow.WriteLine(
                    "ERRO ao criar geometria para grupo " + nomeGrupo + " -> " + exGrupo.Message);
            }
        }

        theSession.ListingWindow.WriteLine("");
        theSession.ListingWindow.WriteLine("Total de geometrias criadas: " + totalGeometriasCriadas);
    }

    // Procura, dentro de uma tabela (diametros[] / designacoes[]), a entrada mais
    // proxima do diametro informado, respeitando a tolerancia. Retorna null se
    // nao achar nenhuma dentro da tolerancia.
    private static string BuscarDesignacao(double diametro, double[] diametros, string[] designacoes, double toleranciaBusca)
    {
        string designacaoEncontrada = null;
        double menorDiferenca = double.MaxValue;

        for (int i = 0; i < diametros.Length; i++)
        {
            double diferenca = Math.Abs(diametros[i] - diametro);
            if (diferenca < toleranciaBusca && diferenca < menorDiferenca)
            {
                menorDiferenca = diferenca;
                designacaoEncontrada = designacoes[i];
            }
        }

        return designacaoEncontrada;
    }

    public static int GetUnloadOption(string dummy) { return (int)Session.LibraryUnloadOption.Immediately; }
}
