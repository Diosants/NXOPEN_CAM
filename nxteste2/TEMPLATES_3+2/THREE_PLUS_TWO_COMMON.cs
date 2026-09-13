// NX 2406
// THREE_PLUS_TWO_COMMON.cs
//
// Nucleo compartilhado do pipeline 3+2 Axis, usado pelas 3 classes
// independentes THREE_PLUS_TWO_ROUGH.cs / THREE_PLUS_TWO_REST_MILL.cs /
// THREE_PLUS_TWO_SEMI_FINISH.cs (uma por estagio, a pedido do usuario -
// antes eram 3 metodos dentro de uma unica classe THREE_PLUS_TWO_PIPELINE,
// que continua existindo so como ORQUESTRADOR fino chamando as 3 em
// sequencia, ja que ainda e referenciada pelo card "3+2 Axis Example" da
// pagina Templates).
//
// NAO cole nenhum dos tipos/metodos daqui de novo em outro arquivo - vai dar
// erro de definicao duplicada (mesmo padrao de aviso do FaceFinderHelpers.cs
// em VISES_FIXTURES/LoadAndAlignVise_Auto.cs).
//
// Contem: tabelas de material/ferramenta, deteccao dinamica de direcao de
// acesso (bounding box/normais das faces), preparo de WCS+reconhecimento de
// features+agrupamento por setup (com a checagem idempotente
// EnsureSetupPrepared - ver aviso abaixo sobre CAMGroupCollection.
// FindObject), e utilidades geometricas (bbox, produto vetorial/escalar,
// "abertura local"/footprint da ferramenta de desbaste).
using System;
using System.Collections.Generic;
using System.Globalization;
using NXOpen;
using NXOpen.CAM;
using NXOpen.Assemblies;

public class THREE_PLUS_TWO_COMMON
{
    public const double NORMAL_GROUP_DOT_THRESHOLD = 0.98; // ~11.5 graus de tolerância
    public const double TOOL_FOOTPRINT_SAFETY_FACTOR = 0.6; // ferramenta de desbaste não pode passar de 60% da abertura local calculada

    public static readonly string[] BLANK_NAME_HINTS = new string[] { "BLANK", "TARUGO", "STOCK", "BRUTO", "IPW" };

    // ── Tipos de feature reconhecidos (lista ampla - ex-TOP_RECOGNIZE_FEATURE.cs) ──
    public static readonly string[] AllFeatureTypes = new string[]
    {
        "STEP1POCKET", "CORNER_NOTCH_STRAIGHT", "SLOT_PARTIAL_RECTANGULAR", "SLOT_PARTIAL_U_SHAPED",
        "STEP1POCKET_THREAD", "STEP2POCKET_THREAD", "STEP2HOLE_THREAD", "STEP1HOLE_THREAD", "STEP1HOLE",
        "POCKET_ROUND_TAPERED", "STEP2POCKET", "STEP2HOLE", "COUNTER_BORE_HOLE", "COUNTER_SUNK_HOLE",
        "POCKET_RECTANGULAR_STRAIGHT", "BOSS_RECTANGULAR_STRAIGHT", "BOSS_ROUND_STRAIGHT",
        "BOSS_ROUND_STRAIGHT_THREAD", "CORNER_NOTCH_RECTANGULAR", "CORNER_NOTCH_ROUND_CONCAVE",
        "CORNER_NOTCH_U_SHAPED", "GROOVE_AX_CIRCULAR_RECT", "GROOVE_INS_RAD_RECT",
        "HOLE_ROUND_INTERRUPTED_STRAIGHT", "HOLE_ROUND_TAPERED", "HOLE_OBROUND_CURVED_STRAIGHT",
        "HOLE_FREE_SHAPED_STRAIGHT", "HOLE_OBROUND_STRAIGHT", "HOLE_RECTANGULAR_STRAIGHT",
        "POCKET_CLOSED", "POCKET_OBROUND_CURVED_STRAIGHT", "POCKET_FREE_SHAPED_STRAIGHT", "POCKET_OPEN",
        "POCKET_OBROUND_STRAIGHT", "SIDE_NOTCH_RECTANGULAR", "SIDE_NOTCH_ROUND_CONCAVE",
        "SIDE_NOTCH_U_SHAPED", "SLOT_90_DEGREE", "SLOT_DOVE_TAIL", "SLOT_OBROUND", "SLOT_PARTIAL_OBROUND",
        "SLOT_PARTIAL_ROUND", "SLOT_RECTANGULAR", "SLOT_ROUND", "SLOT_T_SHAPED", "SLOT_U_SHAPED",
        "SLOT_UPSIDE_DOWN_DOVE_TAIL", "SLOT_V_SHAPED", "STEP3POCKET", "STEP3HOLE", "STEP3HOLE1",
        "STEP4POCKET", "STEP4HOLE", "STEP4HOLE1", "STEP5POCKET", "STEP5HOLE", "STEP5HOLE1", "STEP5HOLE2",
        "STEP6POCKET", "STEP6HOLE", "STEP6HOLE1", "STEP6HOLE2", "STEP3POCKET_THREAD", "STEP3HOLE_THREAD",
        "STEP3HOLE1_THREAD", "STEP4POCKET_THREAD", "STEP4HOLE_THREAD", "STEP4HOLE1_THREAD",
        "STEP5POCKET_THREAD", "STEP5HOLE_THREAD", "STEP5HOLE1_THREAD", "STEP5HOLE2_THREAD",
        "STEP6POCKET_THREAD", "STEP6HOLE_THREAD", "STEP6HOLE1_THREAD", "STEP6HOLE2_THREAD",
        "SURFACE_PLANAR", "SURFACE_PLANAR_RECTANGULAR", "SURFACE_PLANAR_ROUND", "TURNING_GROOVE_FACE",
        "TURNING_GROOVE_ID", "TURNING_GROOVE_OD", "WEDM_FREE_SHAPED_STRAIGHT", "WEDM_OBROUND_STRAIGHT",
        "WEDM_RECTANGULAR_STRAIGHT", "WEDM_ROUND_STRAIGHT",
    };

    // ── Materiais (mesmo padrão/valores de FBM_ALL_FEATURES_MACHINED.cs - ver AVISO no cabeçalho original) ──
    public class MaterialCuttingData { public string Code; public string Name; public double EndmillRoughVc; }

    public static readonly MaterialCuttingData[] DefaultMaterialTable = new MaterialCuttingData[]
    {
        new MaterialCuttingData { Code = "1020", Name = "Aço Carbono 1020", EndmillRoughVc = 140.0 },
        new MaterialCuttingData { Code = "1045", Name = "Aço Carbono 1045", EndmillRoughVc = 100.0 },
        new MaterialCuttingData { Code = "P20", Name = "Aço P20 (pré-temperado)", EndmillRoughVc = 70.0 },
        new MaterialCuttingData { Code = "AL", Name = "Alumínio", EndmillRoughVc = 250.0 },
        new MaterialCuttingData { Code = "BRONZE", Name = "Bronze", EndmillRoughVc = 90.0 },
        new MaterialCuttingData { Code = "CU", Name = "Cobre", EndmillRoughVc = 100.0 },
        new MaterialCuttingData { Code = "NYLON", Name = "Nylon / Poliamida", EndmillRoughVc = 220.0 },
    };

    public class ToolSelection { public string ToolName; public double Diameter; public bool IsCutter; }

    public static readonly ToolSelection[] DefaultRoughToolTable = new ToolSelection[]
    {
        new ToolSelection { ToolName = "ENDMILL_D12MM", Diameter = 12.0, IsCutter = false },
        new ToolSelection { ToolName = "CUTTER_D16_R.8", Diameter = 16.0, IsCutter = true },
        new ToolSelection { ToolName = "CUTTER_D25_R.8", Diameter = 25.0, IsCutter = true },
        new ToolSelection { ToolName = "CUTTER_D40_R1", Diameter = 40.0, IsCutter = true },
    };

    public const double ENDMILL_ROUGH_FEED_PER_REV = 0.3;
    public const double CUTTER_ROUGH_FEED_PER_REV = 0.6;

    // ── Ferramentas de rest mill (ex-REST_MILL_CUTTER_16MM_TOP.cs / REST_MILL_ENDMILL_8MM_TOP.cs) ──
    public static readonly ToolSelection RestMillCutterTool = new ToolSelection { ToolName = "CUTTER_D16_R.8", Diameter = 16.0, IsCutter = true };
    public static readonly ToolSelection RestMillEndmillTool = new ToolSelection { ToolName = "ENDMILL_D8MM", Diameter = 8.0, IsCutter = false };

    public class ThreePlus2Setup
    {
        public string Name;
        public double[] ToolAxis;
        public double FootprintMin; // menor dimensão da bounding box local (mm), projetada perpendicular ao eixo; 0 = desconhecido/sem limite
    }

    public class FaceInfo
    {
        public string FaceType;
        public double Area;
        public double[] Direction;
        public double[] Bbox; // xmin,ymin,zmin,xmax,ymax,zmax
    }

    private class DirGroup
    {
        public double[] Dir;
        public int PlanarCount;
        public int AxisCount;
        public double[] Bbox; // bbox mesclada das faces que caíram neste grupo
    }

    // ── Wrapper de execução por estágio (usado pelas 3 classes de estágio) ──
    // Abre/loga no ListingWindow igual ao RunWithMaterial original (uma única
    // chamada que fazia tudo); 'body' recebe workPart/ufs/W já prontos.
    public static void RunPipelineStage(string stageTitle, Action<Part, NXOpen.UF.UFSession, Action<string>> body)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        if (workPart == null) return;
        NXOpen.UF.UFSession ufs = NXOpen.UF.UFSession.GetUFSession();
        List<string> log = new List<string>();
        Action<string> W = s => log.Add(s);
        try
        {
            W("=== THREE_PLUS_TWO_PIPELINE - " + stageTitle + " ===");
            body(workPart, ufs, W);
            W(""); W("=== FIM - " + stageTitle + " ===");
        }
        catch (Exception ex) { W("ERRO:"); W(ex.ToString()); }
        finally
        {
            ListingWindow lw = theSession.ListingWindow;
            lw.Open();
            foreach (string line in log) lw.WriteLine(line);
        }
    }

    // Recomputa (barato - só geometria) a lista de setups/direções pra um
    // estágio que roda de forma independente dos outros (não guarda estado
    // entre botões diferentes).
    public static List<ThreePlus2Setup> CollectSetupsForStage(Part workPart, NXOpen.UF.UFSession ufs, Action<string> W)
    {
        List<FaceInfo> faces = CollectAllFaces(workPart, ufs, W);
        W("Faces coletadas (fora tarugo/blank): " + faces.Count + ".");
        List<ThreePlus2Setup> setups = GetDynamicSetups(faces, W);
        if (setups == null || setups.Count == 0)
        {
            W("Nenhuma direção detectada dinamicamente - usando as 5 direções cardeais padrão (TOP/FRONT/BACK/LEFT/RIGHT), igual ao sistema antigo.");
            setups = GetDefaultCardinalSetups();
        }
        return setups;
    }

    // Garante que o WCS/reconhecimento de features/agrupamento de um setup já
    // existem antes de criar operações de usinagem nele - permite rodar
    // "Rough" de novo (ou rodar "Rest Mill"/"Semi-Finish" antes do "Rough",
    // se o usuário quiser) sem tentar recriar um WCS/grupo que já existe.
    //
    // CORREÇÃO: CAMGroupCollection.FindObject NÃO retorna null quando o nome
    // não existe - ele lança NXException ("No object found with this name").
    // A checagem original assumia (incorretamente, copiando o padrão usado
    // em outros lugares deste arquivo pra nomes que praticamente sempre
    // existem, tipo "NC_PROGRAM"/"WORKPIECE") que bastava um "as NCGroup" e
    // comparar com null - só que pra um nome novo como "SETUP_01", que nunca
    // existiu, a exceção estourava na hora e era capturada pelo try/catch por
    // setup do chamador, fazendo TODOS os setups falharem com "No object
    // found with this name" na primeira execução do botão "Rough". Corrigido
    // envolvendo o FindObject no seu próprio try/catch (NXException).
    public static void EnsureSetupPrepared(Part workPart, ThreePlus2Setup setup, Action<string> W)
    {
        NCGroup existing = null;
        try { existing = workPart.CAMSetup.CAMGroupCollection.FindObject(setup.Name) as NCGroup; }
        catch (NXException) { existing = null; }
        if (existing != null)
        {
            W("   Setup '" + setup.Name + "' já tem WCS/reconhecimento/grupo de features (pulando recriação).");
            return;
        }
        CreateWcsView(workPart, setup, W);
        RecognizeFeatures(workPart, setup, W);
        CreateFeatureGroup(workPart, setup, W);
    }

    // ── Coleta de geometria (mesma abordagem do SURFACE_ACCESS_DIRECTION_CSYS.cs) ──
    public static List<FaceInfo> CollectAllFaces(Part workPart, NXOpen.UF.UFSession ufs, Action<string> W)
    {
        List<FaceInfo> resultado = new List<FaceInfo>();
        List<Face> facesParaMedir = new List<Face>();
        Component root = workPart.ComponentAssembly.RootComponent;
        if (root != null)
        {
            List<Component> allComponents = new List<Component>();
            CollectComponentsRecursive(root, allComponents);
            foreach (Component comp in allComponents)
            {
                if (IsBlankComponent(comp.Name)) continue;
                Part protoPart = comp.Prototype as Part;
                if (protoPart == null) continue;
                NXOpen.Body[] corpos;
                try { corpos = protoPart.Bodies.ToArray(); } catch { continue; }
                foreach (NXOpen.Body corpo in corpos)
                {
                    NXOpen.Face[] faces;
                    try { faces = corpo.GetFaces(); } catch { continue; }
                    if (faces == null) continue;
                    foreach (Face f in faces)
                    {
                        Face occFace = null;
                        try { occFace = comp.FindOccurrence(f) as Face; } catch { }
                        facesParaMedir.Add(occFace ?? f);
                    }
                }
            }
        }
        else
        {
            NXOpen.Body[] corpos = workPart.Bodies.ToArray();
            foreach (NXOpen.Body corpo in corpos)
            {
                NXOpen.Face[] faces;
                try { faces = corpo.GetFaces(); } catch { continue; }
                if (faces == null) continue;
                foreach (Face f in faces) facesParaMedir.Add(f);
            }
        }
        foreach (Face face in facesParaMedir)
        {
            try
            {
                FaceInfo info = new FaceInfo { FaceType = face.SolidFaceType.ToString() };
                try
                {
                    int faceTypeUf;
                    double[] facePt = new double[3];
                    double[] faceDir = new double[3];
                    double[] bbox = new double[6];
                    double faceRadius, faceRadData;
                    int normDirection;
                    ufs.Modl.AskFaceData(face.Tag, out faceTypeUf, facePt, faceDir, bbox, out faceRadius, out faceRadData, out normDirection);
                    info.Direction = faceDir;
                    info.Bbox = bbox;
                }
                catch { info.Direction = new double[] { 0, 0, 0 }; info.Bbox = null; }
                try
                {
                    ISurface[] superficies = new ISurface[] { face };
                    double area, perimetro, raioAlt, minRaio, erroArea;
                    Point3d centroMassa, pontoAncora;
                    bool aproximado;
                    NXOpen.Session.GetSession().Measurement.GetFaceProperties(
                        superficies, 0.99, Measurement.AlternateFace.Radius, true,
                        out area, out perimetro, out raioAlt, out centroMassa,
                        out minRaio, out erroArea, out pontoAncora, out aproximado);
                    info.Area = area;
                }
                catch { info.Area = 0; }
                resultado.Add(info);
            }
            catch { /* pula face problemática, segue */ }
        }
        return resultado;
    }

    public static bool IsBlankComponent(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        string upper = name.ToUpperInvariant();
        foreach (string hint in BLANK_NAME_HINTS) if (upper.Contains(hint)) return true;
        return false;
    }

    public static void CollectComponentsRecursive(Component comp, List<Component> result)
    {
        if (comp == null) return;
        result.Add(comp);
        Component[] children = comp.GetChildren();
        if (children == null) return;
        foreach (Component child in children) CollectComponentsRecursive(child, result);
    }

    public static List<ThreePlus2Setup> GetDynamicSetups(List<FaceInfo> faces, Action<string> W)
    {
        List<DirGroup> planarGroups = new List<DirGroup>();
        List<DirGroup> axisGroups = new List<DirGroup>();
        foreach (FaceInfo f in faces)
        {
            if (f.Direction == null) continue;
            double norma = Norm(f.Direction);
            bool isPlanar = f.FaceType == "Planar";
            bool isAxisType = f.FaceType == "Cylindrical" || f.FaceType == "Conical" || f.FaceType == "Toroidal" || f.FaceType == "SurfaceOfRevolution";
            if (norma < 0.5 || (!isPlanar && !isAxisType)) continue;
            double[] dirUnit = new double[] { f.Direction[0] / norma, f.Direction[1] / norma, f.Direction[2] / norma };
            if (isPlanar)
            {
                DirGroup achado = null;
                foreach (DirGroup g in planarGroups) if (Dot(dirUnit, g.Dir) >= NORMAL_GROUP_DOT_THRESHOLD) { achado = g; break; }
                if (achado == null) { achado = new DirGroup { Dir = dirUnit }; planarGroups.Add(achado); }
                achado.PlanarCount++;
                MergeBbox(ref achado.Bbox, f.Bbox);
            }
            else
            {
                DirGroup achado = null;
                foreach (DirGroup g in axisGroups) if (Math.Abs(Dot(dirUnit, g.Dir)) >= NORMAL_GROUP_DOT_THRESHOLD) { achado = g; break; }
                if (achado == null) { achado = new DirGroup { Dir = dirUnit }; axisGroups.Add(achado); }
                achado.AxisCount++;
                MergeBbox(ref achado.Bbox, f.Bbox);
            }
        }
        List<DirGroup> finais = new List<DirGroup>();
        bool[] axisUsado = new bool[axisGroups.Count];
        foreach (DirGroup pg in planarGroups)
        {
            DirGroup consolidado = new DirGroup { Dir = pg.Dir, PlanarCount = pg.PlanarCount, Bbox = pg.Bbox != null ? (double[])pg.Bbox.Clone() : null };
            for (int i = 0; i < axisGroups.Count; i++)
            {
                if (Math.Abs(Dot(pg.Dir, axisGroups[i].Dir)) >= NORMAL_GROUP_DOT_THRESHOLD)
                {
                    consolidado.AxisCount += axisGroups[i].AxisCount;
                    MergeBbox(ref consolidado.Bbox, axisGroups[i].Bbox);
                    axisUsado[i] = true;
                }
            }
            finais.Add(consolidado);
        }
        for (int i = 0; i < axisGroups.Count; i++) if (!axisUsado[i]) finais.Add(axisGroups[i]);
        finais.Sort((a, b) => (b.PlanarCount + b.AxisCount).CompareTo(a.PlanarCount + a.AxisCount));
        List<ThreePlus2Setup> setups = new List<ThreePlus2Setup>();
        int contador = 0;
        foreach (DirGroup g in finais)
        {
            contador++;
            double footprint = g.Bbox != null ? ComputeFootprintMin(g.Bbox, g.Dir) : 0.0;
            setups.Add(new ThreePlus2Setup { Name = "SETUP_" + contador.ToString("00"), ToolAxis = g.Dir, FootprintMin = footprint });
            W("Direção detectada #" + contador + ": " + FmtVec(g.Dir) + " (" + g.PlanarCount + " face(s) plana(s), " + g.AxisCount + " de eixo"
                + (footprint > 0 ? ", abertura local ~" + footprint.ToString("0.#", CultureInfo.InvariantCulture) + "mm" : "") + ").");
        }
        return setups;
    }

    public static List<ThreePlus2Setup> GetDefaultCardinalSetups()
    {
        return new List<ThreePlus2Setup>
        {
            new ThreePlus2Setup { Name = "TOP", ToolAxis = new double[] { 0.0, 0.0, 1.0 } },
            new ThreePlus2Setup { Name = "FRONT", ToolAxis = new double[] { 0.0, -1.0, 0.0 } },
            new ThreePlus2Setup { Name = "BACK", ToolAxis = new double[] { 0.0, 1.0, 0.0 } },
            new ThreePlus2Setup { Name = "RIGHT", ToolAxis = new double[] { 1.0, 0.0, 0.0 } },
            new ThreePlus2Setup { Name = "LEFT", ToolAxis = new double[] { -1.0, 0.0, 0.0 } },
        };
    }

    public static double[] ComputeOverallBbox(List<FaceInfo> faces)
    {
        bool achouAlguma = false;
        double[] bbox = new double[] { double.MaxValue, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue, double.MinValue };
        foreach (FaceInfo f in faces)
        {
            if (f.Bbox == null) continue;
            achouAlguma = true;
            if (f.Bbox[0] < bbox[0]) bbox[0] = f.Bbox[0];
            if (f.Bbox[1] < bbox[1]) bbox[1] = f.Bbox[1];
            if (f.Bbox[2] < bbox[2]) bbox[2] = f.Bbox[2];
            if (f.Bbox[3] > bbox[3]) bbox[3] = f.Bbox[3];
            if (f.Bbox[4] > bbox[4]) bbox[4] = f.Bbox[4];
            if (f.Bbox[5] > bbox[5]) bbox[5] = f.Bbox[5];
        }
        return achouAlguma ? bbox : null;
    }

    public static MaterialCuttingData GetMaterial(string code, Action<string> W)
    {
        MaterialCuttingData[] tabela = DefaultMaterialTable;
        try
        {
            List<ToolDatabase.MaterialRow> rows = ToolDatabase.LoadMaterials(W);
            if (rows != null && rows.Count > 0)
            {
                List<MaterialCuttingData> lista = new List<MaterialCuttingData>();
                foreach (ToolDatabase.MaterialRow r in rows) lista.Add(new MaterialCuttingData { Code = r.Code, Name = r.Name, EndmillRoughVc = r.EndmillRoughVc });
                tabela = lista.ToArray();
            }
        }
        catch (Exception ex) { W("AVISO: falha lendo materiais do banco (" + ex.Message + ") - usando tabela fixa interna."); }
        if (!string.IsNullOrEmpty(code))
        {
            foreach (MaterialCuttingData m in tabela) if (string.Equals(m.Code, code, StringComparison.OrdinalIgnoreCase)) return m;
            W("AVISO: material '" + code + "' não encontrado na tabela - usando '" + tabela[0].Name + "' como padrão.");
        }
        return tabela[0];
    }

    public static ToolSelection[] GetRoughToolTable(Action<string> W)
    {
        try
        {
            List<ToolDatabase.ToolRow> rows = ToolDatabase.LoadRoughTools(W);
            if (rows != null && rows.Count > 0)
            {
                List<ToolSelection> lista = new List<ToolSelection>();
                foreach (ToolDatabase.ToolRow r in rows) lista.Add(new ToolSelection { ToolName = r.ToolName, Diameter = r.Diameter, IsCutter = r.IsCutter });
                return lista.ToArray();
            }
        }
        catch (Exception ex) { W("AVISO: falha lendo ferramentas de desbaste do banco (" + ex.Message + ") - usando tabela fixa interna."); }
        return DefaultRoughToolTable;
    }

    public static Dictionary<string, Tool> BuildToolCache(Part workPart)
    {
        Dictionary<string, Tool> cache = new Dictionary<string, Tool>(StringComparer.OrdinalIgnoreCase);
        CAMObject[] objects = workPart.CAMSetup.CAMGroupCollection.ToArray();
        foreach (CAMObject obj in objects) { Tool t = obj as Tool; if (t != null && !cache.ContainsKey(t.Name)) cache[t.Name] = t; }
        return cache;
    }

    public static void CalculateMillingParameters(ToolSelection sel, MaterialCuttingData material, out double rpm, out double feed)
    {
        double vc = material.EndmillRoughVc;
        double feedPerRev = sel.IsCutter ? CUTTER_ROUGH_FEED_PER_REV : ENDMILL_ROUGH_FEED_PER_REV;
        rpm = (vc * 318.0) / sel.Diameter;
        feed = rpm * feedPerRev;
    }

    public static void CreateWcsView(Part workPart, ThreePlus2Setup setup, Action<string> W)
    {
        FeatureGeometry featureGeometry1 = (FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE");
        NCGroup nCGroup1 = workPart.CAMSetup.CAMGroupCollection.CreateGeometryWithUserName(
            featureGeometry1, "mill_planar", "MCS", NCGroupCollection.UseDefaultName.False, setup.Name, "MCS");
        OrientGeometry orientGeometry1 = (OrientGeometry)nCGroup1;
        MillOrientGeomBuilder builder = workPart.CAMSetup.CAMGroupCollection.CreateMillOrientGeomBuilder(orientGeometry1);
        builder.SetToolAxisMode(OrientGeomBuilder.ToolAxisModes.FixedAxis);
        Direction direction = workPart.Directions.CreateDirection(new Point3d(0.0, 0.0, 0.0), ToVector3d(setup.ToolAxis), SmartObject.UpdateOption.AfterModeling);
        builder.ToolAxisVector = direction;
        builder.Commit();
        builder.Destroy();
        W("   WCS/MCS '" + setup.Name + "' criado.");
    }

    public static void RecognizeFeatures(Part workPart, ThreePlus2Setup setup, Action<string> W)
    {
        CAMObject nullObj = null;
        FeatureRecognitionBuilder builder = workPart.CAMSetup.CreateFeatureRecognitionBuilder(nullObj);
        ManualFeatureBuilder manualBuilder = builder.CreateManualFeatureBuilder();
        builder.AssignColor = false;
        builder.AddCadFeatureAttributes = false;
        builder.MapFeatures = false;
        builder.RecognitionType = FeatureRecognitionBuilder.RecognitionEnum.Parametric;
        builder.UseFeatureNameAsType = true;
        builder.IgnoreWarnings = false;
        Direction direction = workPart.Directions.CreateDirection(new Point3d(0.0, 0.0, 0.0), ToVector3d(setup.ToolAxis), SmartObject.UpdateOption.AfterModeling);
        builder.SetMachiningAccessDirection(new Direction[] { direction }, 9.9999999999999995e-07);
        builder.SetFeatureTypes(AllFeatureTypes);
        builder.GeometrySearchType = FeatureRecognitionBuilder.GeometrySearch.Workpiece;
        builder.FindFeatures();
        builder.Commit();
        builder.Destroy();
        manualBuilder.Destroy();
        W("   Reconhecimento de features rodado para '" + setup.Name + "'.");
    }

    public static void CreateFeatureGroup(Part workPart, ThreePlus2Setup setup, Action<string> W)
    {
        GroupFeatures groupFeatures = workPart.CAMSetup.CAMGroupCollection.CreateGroupFeatures();
        groupFeatures.GeometryLocation = setup.Name;
        groupFeatures.FeaturesToGroupType = GroupFeatures.FeaturesToGroupTypes.All;

        Direction direction = workPart.Directions.CreateDirection(
            new Point3d(0.0, 0.0, 0.0), ToVector3d(setup.ToolAxis), SmartObject.UpdateOption.AfterModeling);

        groupFeatures.SetFeatureTypes(AllFeatureTypes);
        groupFeatures.SetMachiningAccessDirections(new Direction[] { direction }, 9.9999999999999995e-07);
        groupFeatures.CreateFeatureGroups();
        groupFeatures.Commit();
        groupFeatures.Destroy();

        RenameAllBaseNames(workPart, setup.Name, W);
    }

    public static void RenameAllBaseNames(Part workPart, string prefix, Action<string> W)
    {
        HashSet<string> jaVistos = new HashSet<string>();
        foreach (string featureType in AllFeatureTypes)
        {
            string baseName = "FG_" + featureType;
            if (!jaVistos.Add(baseName)) continue;
            RenameAllOccurrences(workPart, baseName, prefix);
        }
    }

    public static void RenameAllOccurrences(Part workPart, string baseName, string prefix)
    {
        List<FeatureGeometryGroup> found = new List<FeatureGeometryGroup>();
        FeatureGeometryGroup first = TryFindFeatureGroup(workPart, baseName);
        if (first != null) found.Add(first);

        int idx = 1;
        while (true)
        {
            FeatureGeometryGroup next = TryFindFeatureGroup(workPart, baseName + "_" + idx);
            if (next == null) break;
            found.Add(next);
            idx++;
        }
        if (found.Count == 0) return;

        for (int i = 0; i < found.Count; i++)
        {
            string newName = i == 0 ? (prefix + "_" + baseName) : (prefix + "_" + baseName + "_" + i);
            found[i].SetName(newName);
        }
    }

    public static FeatureGeometryGroup TryFindFeatureGroup(Part workPart, string name)
    {
        try { return (FeatureGeometryGroup)workPart.CAMSetup.CAMGroupCollection.FindObject(name); }
        catch { return null; }
    }

    // =========================================================================
    //  Utilidades geométricas
    // =========================================================================
    public static double[][] GetBboxCorners(double[] bbox)
    {
        return new double[][]
        {
            new double[]{bbox[0],bbox[1],bbox[2]}, new double[]{bbox[3],bbox[1],bbox[2]},
            new double[]{bbox[0],bbox[4],bbox[2]}, new double[]{bbox[3],bbox[4],bbox[2]},
            new double[]{bbox[0],bbox[1],bbox[5]}, new double[]{bbox[3],bbox[1],bbox[5]},
            new double[]{bbox[0],bbox[4],bbox[5]}, new double[]{bbox[3],bbox[4],bbox[5]},
        };
    }

    public static double ComputeDepthAlongAxis(double[] bbox, double[] axis)
    {
        double min = double.MaxValue, max = double.MinValue;
        foreach (double[] c in GetBboxCorners(bbox))
        {
            double proj = Dot(c, axis);
            if (proj < min) min = proj;
            if (proj > max) max = proj;
        }
        return max - min;
    }

    public static void MergeBbox(ref double[] target, double[] src)
    {
        if (src == null) return;
        if (target == null) { target = new double[] { src[0], src[1], src[2], src[3], src[4], src[5] }; return; }
        if (src[0] < target[0]) target[0] = src[0];
        if (src[1] < target[1]) target[1] = src[1];
        if (src[2] < target[2]) target[2] = src[2];
        if (src[3] > target[3]) target[3] = src[3];
        if (src[4] > target[4]) target[4] = src[4];
        if (src[5] > target[5]) target[5] = src[5];
    }

    // Base ortonormal (x,y) perpendicular a um eixo Z dado - mesma construção usada no
    // SURFACE_ACCESS_DIRECTION_CSYS.cs pra orientar os CSYS de acesso.
    public static void BuildPlaneBasis(double[] zAxis, out double[] xAxis, out double[] yAxis)
    {
        double[] referencia = Math.Abs(zAxis[2]) < 0.9 ? new double[] { 0, 0, 1 } : new double[] { 1, 0, 0 };
        xAxis = Normalize(Cross(referencia, zAxis));
        yAxis = Cross(zAxis, xAxis);
    }

    public static double[] Cross(double[] a, double[] b)
    {
        return new double[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
    }

    public static double[] Normalize(double[] v)
    {
        double n = Norm(v);
        if (n < 1e-9) return new double[] { 0, 0, 0 };
        return new double[] { v[0] / n, v[1] / n, v[2] / n };
    }

    // Menor dimensão (mm) da bounding box de um grupo de faces, projetada no plano
    // perpendicular ao eixo de acesso - usada como estimativa de "abertura local" pra
    // limitar o diâmetro da ferramenta de desbaste (ver TOOL_FOOTPRINT_SAFETY_FACTOR).
    public static double ComputeFootprintMin(double[] bbox, double[] axis)
    {
        double[] xAxis, yAxis;
        BuildPlaneBasis(axis, out xAxis, out yAxis);
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (double[] c in GetBboxCorners(bbox))
        {
            double px = Dot(c, xAxis);
            double py = Dot(c, yAxis);
            if (px < minX) minX = px;
            if (px > maxX) maxX = px;
            if (py < minY) minY = py;
            if (py > maxY) maxY = py;
        }
        return Math.Min(maxX - minX, maxY - minY);
    }

    public static double Norm(double[] v) { return Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]); }
    public static double Dot(double[] a, double[] b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
    public static Vector3d ToVector3d(double[] v) { return new Vector3d(v[0], v[1], v[2]); }
    public static string FmtVec(double[] v)
    {
        return "(" + v[0].ToString("0.##", CultureInfo.InvariantCulture) + ", "
            + v[1].ToString("0.##", CultureInfo.InvariantCulture) + ", "
            + v[2].ToString("0.##", CultureInfo.InvariantCulture) + ")";
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
