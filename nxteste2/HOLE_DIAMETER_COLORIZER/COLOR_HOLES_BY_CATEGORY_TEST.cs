using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NXOpen;
using NXOpen.Assemblies;
using NXOpen.UF;

// ══════════════════════════════════════════════════════════════════════
// Classe de TESTE (v3) — classifica furos por CATEGORIA usando um filtro
// GEOMÉTRICO antes de olhar qualquer tabela de diâmetro, pra evitar a
// confusão relatada: furo de rosca caindo na categoria de counterbore
// (soquete/coluna de molde) e vice-versa só porque um número de diâmetro
// coincide entre as tabelas.
//
// COMO FUNCIONA O FILTRO (furo em degrau):
//   1. Toda face cilíndrica vira um "CylFaceInfo": diâmetro + eixo
//      (ponto + direção), pego via UF_MODL_ask_face_data.
//   2. Faces são agrupadas em "furos" (HoleGroup) por COAXIALIDADE: só
//      entram no mesmo furo faces cujo eixo é a MESMA reta (mesma direção
//      e mesma linha, dentro de tolerância), e não qualquer face com
//      diâmetro parecido.
//   3. Dentro de cada furo, olha quantos diâmetros DISTINTOS existem:
//        - 1 diâmetro só  -> furo SIMPLES (sem degrau) -> compara contra
//          a tabela de valor ÚNICO (rosca / alargador H7 / coluna de
//          molde tratada como furo solto - ver nota abaixo).
//        - 2+ diâmetros   -> furo EM DEGRAU (tem passante + cabeça/
//          contra-furo coaxiais) -> compara o PAR (passante, cabeça)
//          inteiro com a tabela de pares (soquete allen / coluna de
//          molde quando ela aparecer como degrau de verdade).
//
// Isso elimina estruturalmente a colisão ENTRE furo simples e furo em
// degrau (um nunca é comparado com a tabela do outro). Mas dentro de cada
// grupo (simples ou degrau) ainda pode haver colisão NUMÉRICA entre
// categorias do mesmo grupo (ex.: broca de M14 = 12.0mm é o mesmo valor
// do alargador H7 de 12mm) - pra isso, o casamento de valor único e o de
// par ambos detectam EMPATE entre categorias diferentes e marcam
// AMBÍGUO em vez de chutar uma das duas.
//
// NOTA sobre coluna de molde: nos testes reais, o par 42/48 não apareceu
// coaxial (são furos separados - provavelmente furo do pino-guia numa
// placa e furo da bucha em outra). Por isso a coluna de molde agora
// entra nas DUAS tabelas: na de valor único (42 sozinho, 48 sozinho -
// cobre o caso real observado) E na de par (42+48 juntos - cobre o caso
// de um furo em degrau de verdade, se aparecer em outra peça).
//
//   AMARELO -> furo simples que bate com broca de macho ISO 724 (passo
//              grosso), M4 até M24.
//   VERDE   -> furo simples que bate com alargador H7 (medidas redondas
//              mais usadas: 8, 10, 12, 14, 16, 18mm).
//   CYAN    -> furo simples OU em degrau que bate com coluna de molde
//              (guide pillar/leader pin) - passante 42 / cabeça 48.
//   ROXO    -> furo em degrau cujo par (passante/cabeça) bate com um
//              soquete allen / DIN 912, calibrado com valores reais
//              medidos na peça (ver comentário em BuildSteppedReferences).
//   VERMELHO -> AMBÍGUO: o furo bate com MAIS DE UMA categoria ao mesmo
//              tempo (ex.: 12.0mm é tanto broca de M14 quanto alargador
//              H7 de 12mm - ver aviso no chat).
//   MARROM  -> não bateu com nada (furo normal, sem classificação
//              conhecida).
//
// PONTO DE ATENÇÃO (o único trecho que depende de uma API de nível mais
// baixo, UFSession.Modl.AskFaceData): se a assinatura não bater com a
// versão do NXOpen de vocês, o compilador vai apontar exatamente essa
// linha em TryGetCylAxis() - me manda o erro que eu ajusto.
// ══════════════════════════════════════════════════════════════════════
public class COLOR_HOLES_BY_CATEGORY_TEST
{
    // Tolerância pra agrupar faces com o MESMO diâmetro dentro de um furo
    // (ruído de modelagem/ponto flutuante) - bem apertada de propósito.
    private const double GROUP_TOLERANCE = 0.02; // mm

    // Tolerância pra casar um furo SIMPLES (valor único) com a tabela de
    // rosca/alargador/coluna solta.
    private const double SIMPLE_MATCH_TOLERANCE = 0.15; // mm

    // Tolerância POR DIMENSÃO pra casar um furo EM DEGRAU (passante E
    // cabeça, as duas ao mesmo tempo) com um par de referência. Um pouco
    // mais folgada que a de rosca porque aqui as DUAS dimensões precisam
    // bater juntas - o risco de confundir categoria é muito menor mesmo
    // com uma tolerância um pouco maior em cada uma.
    private const double PAIR_MATCH_TOLERANCE = 0.20; // mm por dimensão

    // Tolerância angular pra considerar dois eixos "paralelos" (1 - |dot|).
    private const double AXIS_PARALLEL_TOL = 1e-3;
    // Distância perpendicular máxima entre duas retas de eixo pra
    // considerar que é a MESMA reta (mesmo furo).
    private const double AXIS_LINE_TOL = 0.05; // mm
    // Limite de sanidade: não funde faces coaxiais que estão absurdamente
    // longe uma da outra ao longo do eixo (furos diferentes por acaso
    // alinhados). Ajustável se a peça tiver placas muito espessas.
    private const double AXIS_MAX_GAP = 300.0; // mm

    // ── estruturas de face/furo ──
    private class CylFaceInfo
    {
        public DisplayableObject DisplayFace;
        public double Diameter;
        public double[] AxisPoint;
        public double[] AxisDir;
        public string Frame; // nome do componente (ou "workPart") de onde essa face veio - só pra log/diagnóstico
    }

    private class HoleGroup
    {
        public List<CylFaceInfo> Members = new List<CylFaceInfo>();
        public List<double> DistinctDiameters = new List<double>();
    }

    // ── tabelas de referência ──
    // Furo SIMPLES (valor único) - rosca, alargador H7 ou coluna de
    // molde tratada como furo solto.
    private class SimpleRef
    {
        public double Diameter;
        public string Category; // "rosca" | "alargador" | "coluna_molde"
        public string Label;
        public string ColorName;
    }

    private class SimpleMatchResult
    {
        public SimpleRef Best;
        public bool Ambiguous;
        public List<SimpleRef> Tied;
    }

    private class SteppedRef
    {
        public double ThroughDiameter;
        public double HeadDiameter;
        public string Category; // "soquete" | "coluna_molde"
        public string Label;
        public string ColorName;
    }

    private class StepMatchResult
    {
        public SteppedRef Best;
        public bool Ambiguous;
        public List<SteppedRef> Tied;
    }

    private class NamedColor
    {
        public string Name;
        public byte R, G, B;
    }

    private static readonly NamedColor[] Palette =
    {
        new NamedColor { Name = "cyan",   R = 34,  G = 211, B = 238 },
        new NamedColor { Name = "brown",  R = 139, G = 69,  B = 19  }, // furo normal (sem match)
        new NamedColor { Name = "purple", R = 139, G = 92,  B = 246 },
        new NamedColor { Name = "yellow", R = 250, G = 204, B = 21  },
        new NamedColor { Name = "red",    R = 239, G = 68,  B = 68  },
        new NamedColor { Name = "green",  R = 34,  G = 197, B = 94  }, // alargador H7
    };

    public static void Run(string[] args)
    {
        Session theSession = Session.GetSession();
        UFSession ufSession = UFSession.GetUFSession();
        Part workPart = theSession.Parts.Work;
        theSession.ListingWindow.Open();

        if (workPart == null)
        {
            theSession.ListingWindow.WriteLine("Nenhum work part aberto.");
            return;
        }

        try
        {
            List<SimpleRef> simpleTable = BuildSimpleReferences();
            List<SteppedRef> steppedTable = BuildSteppedReferences();

            // ── Resolve as cores nomeadas pro índice real deste arquivo ──
            Dictionary<string, int> colorIndex = new Dictionary<string, int>();
            foreach (NamedColor named in Palette)
            {
                double[] rgb = new double[3] { named.R / 255.0, named.G / 255.0, named.B / 255.0 };
                int idx;
                ufSession.Disp.AskClosestColor(
                    UFConstants.UF_DISP_rgb_model, rgb,
                    UFConstants.UF_DISP_CCM_EUCLIDEAN_DISTANCE, out idx);
                colorIndex[named.Name] = idx;
                theSession.ListingWindow.WriteLine("Cor '" + named.Name + "' -> índice " + idx + " neste arquivo.");
            }

            // ── Coleta faces cilíndricas POR FRAME (cada componente, ou o
            // workPart direto se não for assembly) e agrupa por eixo DENTRO
            // de cada frame - não funde furos de componentes diferentes,
            // já que cada Prototype tem seu próprio sistema de coordenadas
            // local. ──
            var allHoles = new List<HoleGroup>();

            Component root = workPart.ComponentAssembly.RootComponent;
            if (root != null)
            {
                List<Component> allComponents = new List<Component>();
                CollectComponentsRecursive(root, allComponents);
                theSession.ListingWindow.WriteLine("Assembly - componentes encontrados: " + allComponents.Count);

                foreach (Component comp in allComponents)
                {
                    Part protoPart = comp.Prototype as Part;
                    if (protoPart == null) continue;

                    string frameName = string.IsNullOrEmpty(comp.Name) ? ("componente sem nome (tag " + comp.Tag + ")") : comp.Name;

                    var frameFaces = new List<CylFaceInfo>();
                    foreach (Body body in protoPart.Bodies)
                        foreach (Face face in body.GetFaces())
                        {
                            CylFaceInfo info;
                            DisplayableObject displayFace = (comp.FindOccurrence(face) as Face) ?? (DisplayableObject)face;
                            if (TryGetCylFaceInfo(theSession, ufSession, face, displayFace, out info))
                            {
                                info.Frame = frameName;
                                frameFaces.Add(info);
                            }
                        }

                    theSession.ListingWindow.WriteLine("  Componente '" + frameName + "': " + frameFaces.Count + " face(s) cilíndrica(s).");
                    allHoles.AddRange(GroupHolesByAxis(frameFaces));
                }
            }
            else
            {
                theSession.ListingWindow.WriteLine("Não é assembly - varrendo workPart.Bodies direto.");
                var frameFaces = new List<CylFaceInfo>();
                foreach (Body body in workPart.Bodies)
                    foreach (Face face in body.GetFaces())
                    {
                        CylFaceInfo info;
                        if (TryGetCylFaceInfo(theSession, ufSession, face, face, out info))
                        {
                            info.Frame = "workPart";
                            frameFaces.Add(info);
                        }
                    }
                allHoles.AddRange(GroupHolesByAxis(frameFaces));
            }

            theSession.ListingWindow.WriteLine("Furos (grupos coaxiais) encontrados: " + allHoles.Count);

            if (allHoles.Count == 0)
            {
                theSession.ListingWindow.WriteLine("Nada pra colorir.");
                return;
            }

            // ── Classifica e colore cada furo ──
            Session.UndoMarkId markId = theSession.SetUndoMark(
                Session.MarkVisibility.Invisible, "Classificar e colorir furos (teste v2 - filtro geometrico)");

            int countRosca = 0, countAlargador = 0, countSoquete = 0, countColuna = 0, countAmbiguo = 0, countNormal = 0;
            theSession.ListingWindow.WriteLine("--- Classificação ---");

            foreach (HoleGroup hole in allHoles)
            {
                string colorName;
                string descricao;
                string tipo;

                if (hole.DistinctDiameters.Count <= 1)
                {
                    tipo = "SIMPLES";
                    double d = hole.DistinctDiameters.Count == 1 ? hole.DistinctDiameters[0] : hole.Members[0].Diameter;
                    SimpleMatchResult match = FindClosestSimple(simpleTable, d, SIMPLE_MATCH_TOLERANCE);
                    if (match == null)
                    {
                        colorName = "brown";
                        descricao = "furo normal (sem degrau, sem match)";
                        countNormal++;
                    }
                    else if (match.Ambiguous)
                    {
                        colorName = "red";
                        descricao = "AMBÍGUO (furo simples) - bate com: " + string.Join(" / ", match.Tied.Select(t => t.Label));
                        countAmbiguo++;
                    }
                    else
                    {
                        colorName = match.Best.ColorName;
                        descricao = match.Best.Label;
                        if (match.Best.Category == "rosca") countRosca++;
                        else if (match.Best.Category == "alargador") countAlargador++;
                        else if (match.Best.Category == "coluna_molde") countColuna++;
                    }
                }
                else
                {
                    tipo = "DEGRAU";
                    double throughD = hole.DistinctDiameters.First();
                    double headD = hole.DistinctDiameters.Last();
                    string multiNote = hole.DistinctDiameters.Count > 2
                        ? " [furo com " + hole.DistinctDiameters.Count + " diâmetros - considerando só o menor e o maior]"
                        : "";

                    StepMatchResult match = FindClosestStepped(steppedTable, throughD, headD, PAIR_MATCH_TOLERANCE);
                    if (match == null)
                    {
                        colorName = "brown";
                        descricao = "furo em degrau sem match na tabela (passante " + throughD.ToString("F2", CultureInfo.InvariantCulture)
                            + " / cabeça " + headD.ToString("F2", CultureInfo.InvariantCulture) + ")" + multiNote;
                        countNormal++;
                    }
                    else if (match.Ambiguous)
                    {
                        colorName = "red";
                        descricao = "AMBÍGUO (degrau) - bate com: " + string.Join(" / ", match.Tied.Select(t => t.Label)) + multiNote;
                        countAmbiguo++;
                    }
                    else
                    {
                        colorName = match.Best.ColorName;
                        descricao = match.Best.Label + multiNote;
                        if (match.Best.Category == "soquete") countSoquete++;
                        else if (match.Best.Category == "coluna_molde") countColuna++;
                    }
                }

                DisplayableObject[] faces = hole.Members.Select(m => m.DisplayFace).Distinct().ToArray();

                DisplayModification dispMod = theSession.DisplayManager.NewDisplayModification();
                dispMod.ApplyToAllFaces = true;
                dispMod.ApplyToOwningParts = false;
                dispMod.NewColor = colorIndex[colorName];
                dispMod.CurveEndPointColorOption = DisplayModification.CurveEndPointColorOptions.NoChange;
                dispMod.Apply(faces);
                dispMod.Dispose();

                string diametrosStr = string.Join("/", hole.DistinctDiameters.Select(d => d.ToString("F3", CultureInfo.InvariantCulture)));
                string frameStr = hole.Members[0].Frame ?? "?";
                theSession.ListingWindow.WriteLine(string.Format(
                    "  [{0}] D{1} mm | comp={2} | {3} face(s) | {4} ({5}) -> {6}",
                    tipo, diametrosStr, frameStr, faces.Length, colorName, colorIndex[colorName], descricao));
            }

            theSession.UpdateManager.DoUpdate(markId);
            theSession.CleanUpFacetedFacesAndEdges();

            theSession.ListingWindow.WriteLine("--- Resumo ---");
            theSession.ListingWindow.WriteLine("Rosca (amarelo): " + countRosca);
            theSession.ListingWindow.WriteLine("Alargador H7 (verde): " + countAlargador);
            theSession.ListingWindow.WriteLine("Soquete (roxo): " + countSoquete);
            theSession.ListingWindow.WriteLine("Coluna de molde (cyan): " + countColuna);
            theSession.ListingWindow.WriteLine("Ambíguo (vermelho): " + countAmbiguo);
            theSession.ListingWindow.WriteLine("Normal (marrom): " + countNormal);
        }
        catch (Exception ex)
        {
            theSession.ListingWindow.WriteLine("ERRO:");
            theSession.ListingWindow.WriteLine(ex.ToString());
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // TABELAS DE REFERÊNCIA
    // ══════════════════════════════════════════════════════════════════
    private static List<SimpleRef> BuildSimpleReferences()
    {
        var list = new List<SimpleRef>();

        // ── ROSCA - broca de macho, passo GROSSO (ISO 724) ──
        // Fonte: Fractory ISO Metric Tap Drill Chart.
        AddThread(list, "M4", 0.7, 3.3);
        AddThread(list, "M5", 0.8, 4.2);
        AddThread(list, "M6", 1.0, 5.0);
        AddThread(list, "M8", 1.25, 6.8);
        AddThread(list, "M10", 1.5, 8.5);
        AddThread(list, "M12", 1.75, 10.2);
        AddThread(list, "M14", 2.0, 12.0);
        AddThread(list, "M16", 2.0, 14.0);
        AddThread(list, "M18", 2.5, 15.5);
        AddThread(list, "M20", 2.5, 17.5);
        AddThread(list, "M22", 2.5, 19.5);
        AddThread(list, "M24", 3.0, 21.0);

        // ── ALARGADOR H7 - medidas redondas mais usadas na oficina ──
        // Só as que você pediu (8 a 18mm). Adicione mais com AddReamer se
        // usar outras medidas de alargador.
        AddReamer(list, 8.0);
        AddReamer(list, 10.0);
        AddReamer(list, 12.0);
        AddReamer(list, 14.0);
        AddReamer(list, 16.0);
        AddReamer(list, 18.0);

        // ── COLUNA DE MOLDE, como furo SOLTO (passante e cabeça/bucha
        // como furos separados, não um único furo em degrau) - é o caso
        // que apareceu de verdade nos testes: 42mm e 48mm nunca vieram
        // coaxiais um com o outro. ──
        AddSimple(list, 42.0, "coluna_molde", "Coluna de molde (passante 42mm)", "cyan");
        AddSimple(list, 48.0, "coluna_molde", "Coluna de molde (cabeça/bucha 48mm)", "cyan");

        return list;
    }

    // ATENÇÃO - COLISÕES NUMÉRICAS DETECTADAS entre rosca e alargador
    // (mesmo furo simples, categorias diferentes, mesmo diâmetro):
    //   12.0mm = broca de M14  E  alargador H7 12mm
    //   14.0mm = broca de M16  E  alargador H7 14mm
    // Esses dois furos vão aparecer como AMBÍGUO/vermelho (nenhum jeito
    // de saber só pelo diâmetro se é rosca ou alargador - não tem
    // diferença geométrica entre os dois, os dois são furo reto de
    // diâmetro único). Se no seu processo M14/M16 é raro e na prática
    // isso é sempre alargador (ou sempre rosca), me avisa que eu tiro a
    // ambiguidade e fixo uma prioridade em vez de marcar vermelho.
    private static void AddThread(List<SimpleRef> list, string size, double pitch, double drillDiameter)
    {
        AddSimple(list, drillDiameter, "rosca",
            size + " (broca de macho, passo " + pitch.ToString("0.##", CultureInfo.InvariantCulture) + ")",
            "yellow");
    }

    private static void AddReamer(List<SimpleRef> list, double diameter)
    {
        AddSimple(list, diameter, "alargador",
            "Alargador H7 " + diameter.ToString("0.##", CultureInfo.InvariantCulture) + "mm",
            "green");
    }

    private static void AddSimple(List<SimpleRef> list, double diameter, string category, string label, string colorName)
    {
        list.Add(new SimpleRef { Diameter = diameter, Category = category, Label = label, ColorName = colorName });
    }

    private static List<SteppedRef> BuildSteppedReferences()
    {
        var list = new List<SteppedRef>();

        // ── SOQUETES (parafuso allen, DIN 912 / ISO 4762) ──
        // Cada tamanho é UM par (passante, cabeça/contra-furo) - os dois
        // têm que bater juntos pra classificar.
        //
        // RECALIBRADO a partir de furos em degrau REAIS medidos numa peça
        // de vocês (rodada de teste de 22/08) - os valores de catálogo
        // genérico (AmesWeb/CarbideDepot) que eu tinha usado antes NÃO
        // batiam com a prática de vocês (zero soquetes reconhecidos).
        // O passante bateu quase exato com a série "medium fit" da ISO
        // 273; a cabeça/contra-furo é uma folga própria da oficina, maior
        // que a de catálogo - por isso os valores abaixo vêm da peça
        // real, não de tabela publicada.
        //
        // M14, M18 e M22 NÃO apareceram como furo em degrau nessa peça de
        // teste (só como furo simples de rosca) - se aparecerem em outra
        // peça, me manda o par exato do log (passante/cabeça) que eu
        // adiciono aqui do mesmo jeito.
        AddSocket(list, "M4", 4.5, 8.0);
        AddSocket(list, "M5", 5.5, 10.0);
        AddSocket(list, "M6 (passagem 6.5)", 6.5, 11.0);
        AddSocket(list, "M6 (passagem 6.6)", 6.6, 11.0);
        AddSocket(list, "M8 (passagem 8.5)", 8.5, 14.0);
        AddSocket(list, "M8 (passagem 9.0)", 9.0, 15.0);
        AddSocket(list, "M10", 11.0, 18.0);
        AddSocket(list, "M12", 13.5, 20.0);
        AddSocket(list, "M16", 17.5, 26.0);
        AddSocket(list, "M20", 22.0, 33.0);
        AddSocket(list, "M24", 26.0, 40.0);
        // M30 não estava no pedido original (M4-M24), mas apareceu na
        // peça real com o mesmo padrão consistente - incluí; me avisa se
        // não fizer sentido pro seu processo e eu tiro.
        AddSocket(list, "M30", 33.0, 50.0);

        // ── COLUNA DE MOLDE (guide pillar / leader pin) ──
        // Só o par confirmado por você (sem tabela pública universal -
        // específico de catálogo de mold base). Adicione mais linhas
        // aqui se usar outras colunas: AddMoldColumn(list, "nome", passante, cabeça);
        AddMoldColumn(list, "Coluna de molde", 42.0, 48.0);

        return list;
    }

    private static void AddSocket(List<SteppedRef> list, string size, double throughDiameter, double headDiameter, bool estimado = false)
    {
        string tag = estimado ? " [ESTIMADO]" : "";
        list.Add(new SteppedRef
        {
            ThroughDiameter = throughDiameter,
            HeadDiameter = headDiameter,
            Category = "soquete",
            Label = size + " soquete allen (passante " + throughDiameter.ToString("0.##", CultureInfo.InvariantCulture)
                + " / cabeça " + headDiameter.ToString("0.##", CultureInfo.InvariantCulture) + ")" + tag,
            ColorName = "purple",
        });
    }

    private static void AddMoldColumn(List<SteppedRef> list, string label, double throughDiameter, double headDiameter)
    {
        list.Add(new SteppedRef
        {
            ThroughDiameter = throughDiameter,
            HeadDiameter = headDiameter,
            Category = "coluna_molde",
            Label = label + " (passante " + throughDiameter.ToString("0.##", CultureInfo.InvariantCulture)
                + " / cabeça " + headDiameter.ToString("0.##", CultureInfo.InvariantCulture) + ")",
            ColorName = "cyan",
        });
    }

    // Casa um furo SIMPLES (valor único) com a tabela de rosca/alargador/
    // coluna solta. Se dois candidatos de categorias DIFERENTES empatarem
    // no menor erro, marca Ambiguous = true (mesmo mecanismo do casamento
    // de par, só que pra um valor só).
    private static SimpleMatchResult FindClosestSimple(List<SimpleRef> table, double diameter, double tolerance)
    {
        double bestDiff = double.MaxValue;
        var candidates = new List<SimpleRef>();
        foreach (SimpleRef r in table)
        {
            double diff = Math.Abs(r.Diameter - diameter);
            if (diff > tolerance)
                continue;

            if (diff < bestDiff - 1e-6)
            {
                bestDiff = diff;
                candidates.Clear();
                candidates.Add(r);
            }
            else if (Math.Abs(diff - bestDiff) <= 1e-6)
            {
                candidates.Add(r);
            }
        }
        if (candidates.Count == 0)
            return null;

        bool ambiguous = candidates.Select(c => c.Category).Distinct().Count() > 1;
        return new SimpleMatchResult { Best = candidates[0], Ambiguous = ambiguous, Tied = candidates };
    }

    // Casa o PAR (passante, cabeça) de um furo em degrau com a tabela.
    // Se dois candidatos de categorias DIFERENTES empatarem no menor
    // erro combinado, marca Ambiguous = true.
    private static StepMatchResult FindClosestStepped(List<SteppedRef> table, double throughD, double headD, double perDimTolerance)
    {
        double bestScore = double.MaxValue;
        var candidates = new List<SteppedRef>();
        foreach (SteppedRef r in table)
        {
            double dT = Math.Abs(r.ThroughDiameter - throughD);
            double dH = Math.Abs(r.HeadDiameter - headD);
            if (dT > perDimTolerance || dH > perDimTolerance)
                continue;

            double score = dT + dH;
            if (score < bestScore - 1e-6)
            {
                bestScore = score;
                candidates.Clear();
                candidates.Add(r);
            }
            else if (Math.Abs(score - bestScore) <= 1e-6)
            {
                candidates.Add(r);
            }
        }
        if (candidates.Count == 0)
            return null;

        bool ambiguous = candidates.Select(c => c.Category).Distinct().Count() > 1;
        return new StepMatchResult { Best = candidates[0], Ambiguous = ambiguous, Tied = candidates };
    }

    // ══════════════════════════════════════════════════════════════════
    // AGRUPAMENTO POR EIXO (o filtro geométrico em si)
    // ══════════════════════════════════════════════════════════════════
    private static List<HoleGroup> GroupHolesByAxis(List<CylFaceInfo> faces)
    {
        var holes = new List<HoleGroup>();
        foreach (CylFaceInfo f in faces)
        {
            HoleGroup target = null;
            foreach (HoleGroup h in holes)
            {
                if (h.Members.Any(m => IsCoaxial(m, f)))
                {
                    target = h;
                    break;
                }
            }
            if (target == null)
            {
                target = new HoleGroup();
                holes.Add(target);
            }
            target.Members.Add(f);
        }

        foreach (HoleGroup h in holes)
        {
            foreach (CylFaceInfo f in h.Members)
            {
                if (!h.DistinctDiameters.Any(d => Math.Abs(d - f.Diameter) <= GROUP_TOLERANCE))
                    h.DistinctDiameters.Add(f.Diameter);
            }
            h.DistinctDiameters.Sort();
        }
        return holes;
    }

    private static bool IsCoaxial(CylFaceInfo a, CylFaceInfo b)
    {
        double dot = a.AxisDir[0] * b.AxisDir[0] + a.AxisDir[1] * b.AxisDir[1] + a.AxisDir[2] * b.AxisDir[2];
        if (1.0 - Math.Abs(dot) > AXIS_PARALLEL_TOL)
            return false;

        double[] v = { b.AxisPoint[0] - a.AxisPoint[0], b.AxisPoint[1] - a.AxisPoint[1], b.AxisPoint[2] - a.AxisPoint[2] };
        double along = v[0] * a.AxisDir[0] + v[1] * a.AxisDir[1] + v[2] * a.AxisDir[2];
        double[] perp = { v[0] - along * a.AxisDir[0], v[1] - along * a.AxisDir[1], v[2] - along * a.AxisDir[2] };
        double perpDist = Math.Sqrt(perp[0] * perp[0] + perp[1] * perp[1] + perp[2] * perp[2]);

        if (perpDist > AXIS_LINE_TOL) return false;
        if (Math.Abs(along) > AXIS_MAX_GAP) return false;
        return true;
    }

    // ══════════════════════════════════════════════════════════════════
    // COLETA DE FACE (diâmetro + eixo)
    // ══════════════════════════════════════════════════════════════════
    private static bool TryGetCylFaceInfo(Session theSession, UFSession ufSession, Face face, DisplayableObject displayFace, out CylFaceInfo info)
    {
        info = null;
        if (face.SolidFaceType != Face.FaceType.Cylindrical)
            return false;

        double diameter;
        if (!TryGetCylindricalDiameter(theSession, face, out diameter))
            return false;

        double[] axisPoint;
        double[] axisDir;
        if (!TryGetCylAxis(ufSession, face, out axisPoint, out axisDir))
            return false;

        info = new CylFaceInfo
        {
            DisplayFace = displayFace,
            Diameter = diameter,
            AxisPoint = axisPoint,
            AxisDir = axisDir,
        };
        return true;
    }

    // Eixo do cilindro via UF_MODL_ask_face_data. O parâmetro rad_data
    // (7º) é um double ESCALAR ('out double'), não array - só é
    // significativo pra face tórica (raio menor do toro); pra face
    // cilíndrica vem sem uso, a gente ignora.
    private static bool TryGetCylAxis(UFSession ufSession, Face face, out double[] axisPoint, out double[] axisDir)
    {
        axisPoint = null;
        axisDir = null;
        try
        {
            int faceType, normalDir;
            double radius;
            double[] point = new double[3];
            double[] dir = new double[3];
            double[] box = new double[6];
            double radData;

            ufSession.Modl.AskFaceData(face.Tag, out faceType, point, dir, box, out radius, out radData, out normalDir);

            double len = Math.Sqrt(dir[0] * dir[0] + dir[1] * dir[1] + dir[2] * dir[2]);
            if (len < 1e-9)
                return false;

            axisPoint = point;
            axisDir = new double[] { dir[0] / len, dir[1] / len, dir[2] / len };
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetCylindricalDiameter(Session theSession, Face face, out double diameter)
    {
        diameter = 0.0;
        if (face.SolidFaceType != Face.FaceType.Cylindrical)
            return false;

        ISurface[] faces = new ISurface[] { face };
        double area, perimeter, radius, minRadiusOfCurvature, areaErrorEstimate;
        Point3d cog, anchorPoint;
        bool isApproximate;

        theSession.Measurement.GetFaceProperties(
            faces, 0.99, Measurement.AlternateFace.Radius, true,
            out area, out perimeter, out radius, out cog,
            out minRadiusOfCurvature, out areaErrorEstimate, out anchorPoint, out isApproximate);

        diameter = radius * 2.0;
        return true;
    }

    private static void CollectComponentsRecursive(Component comp, List<Component> result)
    {
        if (comp == null) return;
        result.Add(comp);
        Component[] children = comp.GetChildren();
        if (children == null) return;
        foreach (Component child in children)
            CollectComponentsRecursive(child, result);
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
