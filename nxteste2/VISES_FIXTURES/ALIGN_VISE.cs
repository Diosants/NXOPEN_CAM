
using System;
using System.Collections.Generic;
using NXOpen;
using NXOpen.Assemblies;
using NXOpen.Positioning;

public class ALIGN_VISE
{
    // =====================================================================
    // CONFIGURACAO - ajuste aqui, sem precisar mexer no meio do codigo
    // =====================================================================

    // Nomes dos componentes/features na montagem (mudam se a estrutura
    // do assembly mudar, mas normalmente ficam fixos entre pecas)
    private const string ASSEMBLY_COMPONENT_NAME = "day_one_setup_vice_assmbly";
    private const string JAW2_COMPONENT_NAME = "day_one_setup_vice_jaw_2";
    private const string JAW1_COMPONENT_NAME = "day_one_setup_vice_jaw_1";
    private const string FIXTURE_FEATURE_NAME = "EXTRUDE(2)";

    // Tolerancia angular (graus) usada para considerar uma face "alinhada"
    // a um eixo X/Y/Z nas buscas geometricas
    private const double FACE_ANGLE_TOLERANCE_DEG = 5.0;

    // Folga de cada lado do mordente (jaw_2 e jaw_1) em relacao a placa
    // fixa, para acomodar a sobra de materia-prima bruta em relacao a
    // peca nominal. Ajuste este valor se a sobra de material mudar
    // (ex: se a materia-prima bruta for X mm maior no total, use X/2 aqui).
    private const string JAW_CLEARANCE_MM = "2.5";

    // Deslocamento em Z (profundidade) da peca em relacao a face de
    // referencia da placa fixa (STEP 3 - Distance). Sinal e magnitude sao
    // especificos de cada peca/materia-prima - reajuste aqui se mudar.
    private const string Z_DEPTH_OFFSET_MM = "-5";

    // Nome da expressao NX gerada ao criar o constraint de Distance do
    // STEP 3. Isso e um ID AUTO-GERADO PELA SESSAO NX - pode mudar entre
    // sessoes/pecas. Se o script falhar aqui dizendo que a expressao nao
    // existe, rode o constraint de Distance manualmente uma vez, veja o
    // nome correto no Expression Editor do NX, e atualize esta constante.
    private const string DISTANCE_EXPRESSION_NAME = "p3255";

    public static void Run(string[] args)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;

        // -----------------------------------------------------------
        // 1) Localizar componentes
        // -----------------------------------------------------------
        Component asmComponent = FindComponent(workPart.ComponentAssembly.RootComponent, ASSEMBLY_COMPONENT_NAME);
        if (asmComponent == null)
        {
            theSession.ListingWindow.WriteLine("ERRO: componente 'day_one_setup_vice_assmbly' nao encontrado.");
            return;
        }

        Component jaw2Component = FindComponent(asmComponent, JAW2_COMPONENT_NAME);
        Component jaw1Component = FindComponent(asmComponent, JAW1_COMPONENT_NAME);

        if (jaw2Component == null || jaw1Component == null)
        {
            theSession.ListingWindow.WriteLine("ERRO: jaw_1 ou jaw_2 nao encontrado dentro de 'day_one_setup_vice_assmbly'.");
            return;
        }

        NXOpen.Features.Feature fixtureFeature;
        try
        {
            fixtureFeature = workPart.Features.FindObject(FIXTURE_FEATURE_NAME);
        }
        catch (NXException)
        {
            theSession.ListingWindow.WriteLine("ERRO: feature 'EXTRUDE(2)' (placa fixa) nao encontrada.");
            return;
        }

        // -----------------------------------------------------------
        // 2) Coletar faces planas dos 3 corpos envolvidos
        // -----------------------------------------------------------
        List<FaceFinderHelpers.PlanarFaceInfo> jaw2Faces = FaceFinderHelpers.GetPlanarFaces(FaceFinderHelpers.GetOccurrenceBody(jaw2Component));
        List<FaceFinderHelpers.PlanarFaceInfo> jaw1Faces = FaceFinderHelpers.GetPlanarFaces(FaceFinderHelpers.GetOccurrenceBody(jaw1Component));
        List<FaceFinderHelpers.PlanarFaceInfo> fixtureFaces = FaceFinderHelpers.GetPlanarFacesFromFeature(fixtureFeature);

        // -----------------------------------------------------------
        // 3) STEP 1 - Center22 (deteccao geometrica)
        // -----------------------------------------------------------
        int centeringAxis;
        Face jaw2Neg, jaw2Pos;
        bool jaw2PairOk = FaceFinderHelpers.TryDetectBestCenteringPair(jaw2Faces, FACE_ANGLE_TOLERANCE_DEG, out centeringAxis, out jaw2Neg, out jaw2Pos);
        if (!jaw2PairOk)
        {
            theSession.ListingWindow.WriteLine("ERRO: nao foi possivel detectar o par de centralizacao do jaw_2.");
            return;
        }

        Face fixtureNeg, fixturePos;
        double fixtureMatchedArea;
        // IMPORTANTE: procurar o par da placa fixa NO MESMO EIXO do jaw_2,
        // nao o "maior par" independente - senao pode pegar o par errado
        // (ex: topo/fundo do bloco em vez das laterais).
        bool fixturePairOk = FaceFinderHelpers.TryFindSymmetricFacePair(fixtureFaces, centeringAxis, FACE_ANGLE_TOLERANCE_DEG, out fixtureNeg, out fixturePos, out fixtureMatchedArea);
        if (!fixturePairOk)
        {
            theSession.ListingWindow.WriteLine("ERRO: nao foi possivel encontrar o par da placa fixa no eixo detectado do jaw_2.");
            return;
        }

        RunConstraintBlock(workPart, (positioner, network) =>
        {
            Constraint constraint = positioner.CreateConstraint(true);
            ComponentConstraint cc = (ComponentConstraint)constraint;
            cc.ConstraintType = Constraint.Type.Center22;

            ConstraintReference r1 = cc.CreateConstraintReference(asmComponent, jaw2Neg, false, false, false);
            ConstraintReference r2 = cc.CreateConstraintReference(asmComponent, jaw2Pos, false, false, false);

            ConstraintReference r3 = cc.CreateConstraintReference(workPart.ComponentAssembly, fixtureNeg, false, false, false);
            r3.SetFixHint(true);
            ConstraintReference r4 = cc.CreateConstraintReference(workPart.ComponentAssembly, fixturePos, false, false, false);
            r4.SetFixHint(true);
        });

        // -----------------------------------------------------------
        // 4) STEP 2 - Libera o override de posicao do jaw_1
        // -----------------------------------------------------------
        jaw1Component.EstablishPositionOverride(null);

        // -----------------------------------------------------------
        // 5) STEP 3 - Distance (AINDA HARDCODED - ver nota no topo do arquivo)
        //
        //    ATENCAO: "p3255" e o nome de expressao gravado nesta sessao.
        //    Se o solve falhar dizendo que a expressao nao existe, troque
        //    pelo nome correto (visivel no Expression Editor do NX logo
        //    apos rodar o constraint de Distance manualmente uma vez).
        // -----------------------------------------------------------
        {
            ComponentPositioner positioner3 = workPart.ComponentAssembly.Positioner;
            Network network3 = positioner3.EstablishNetwork();
            positioner3.BeginAssemblyConstraints();

            ComponentNetwork componentNetwork3 = (ComponentNetwork)network3;
            componentNetwork3.NetworkArrangementsMode = ComponentNetwork.ArrangementsMode.Existing;
            componentNetwork3.DisplayComponent = null;
            componentNetwork3.MoveObjectsState = true;

            Constraint constraint2 = positioner3.CreateConstraint(true);
            ComponentConstraint componentConstraint2 = (ComponentConstraint)constraint2;
            componentConstraint2.ConstraintType = Constraint.Type.Distance;

            Face fixtureFaceBase = (Face)SafeFindGeomFromFeature(fixtureFeature, "FACE 130 {(0,0,-60) EXTRUDE(2)}");
            ConstraintReference r5 = componentConstraint2.CreateConstraintReference(workPart.ComponentAssembly, fixtureFaceBase, false, false, false);

            Face jaw2FaceRef = (Face)SafeFindGeom(jaw2Component,
                "PROTO#.Features|UNPARAMETERIZED_FEATURE(1)|FACE 4 {(-83.8499999999994,205.0000000000002,-0.000000000001) UNPARAMETERIZED_FEATURE(1)}");
            ConstraintReference r6 = componentConstraint2.CreateConstraintReference(asmComponent, jaw2FaceRef, false, false, false);
            r6.SetFixHint(true);

            componentConstraint2.SetExpression("0");
            componentConstraint2.SetExpression("235");

            componentNetwork3.Solve();
            componentNetwork3.Solve();

            componentNetwork3.AddConstraint(componentConstraint2);

            Expression distanceExpression = (Expression)workPart.Expressions.FindObject(DISTANCE_EXPRESSION_NAME);
            distanceExpression.RightHandSide = Z_DEPTH_OFFSET_MM;

            componentNetwork3.Solve();
            componentNetwork3.Solve();
            componentNetwork3.Solve();

            positioner3.PrimaryArrangement = null;
            positioner3.ClearNetwork();
            positioner3.EndAssemblyConstraints();
        }

        // -----------------------------------------------------------
        // 6) STEP 4 e 5 - Detecta o eixo de aperto cruzando jaw_2 e jaw_1
        //    ao mesmo tempo (regra fisica: lados opostos, faces diferentes)
        // -----------------------------------------------------------
        int clampAxis;
        Face jaw2TouchFace, fixtureTouchFace1, jaw1TouchFace, fixtureTouchFace2;
        bool clampOk = FaceFinderHelpers.TryDetectClampingAxis(jaw2Faces, jaw1Faces, fixtureFaces, FACE_ANGLE_TOLERANCE_DEG, centeringAxis,
            out clampAxis, out jaw2TouchFace, out fixtureTouchFace1, out jaw1TouchFace, out fixtureTouchFace2);
        if (!clampOk)
        {
            theSession.ListingWindow.WriteLine("ERRO: nao foi possivel detectar o eixo de aperto (jaw_2/jaw_1 x fixture).");
            return;
        }

        // STEP 4 - jaw_2 <-> placa fixa, com folga de 2.5mm (nao encosto
        // flush). A materia-prima bruta e 5mm maior no total que a peca
        // nominal (2.5mm de sobra por lado), entao os mordentes devem
        // parar 2.5mm antes do encosto total, nao em Touch puro (0mm).
        RunConstraintBlock(workPart, (positioner, network) =>
        {
            Constraint constraint = positioner.CreateConstraint(true);
            ComponentConstraint cc = (ComponentConstraint)constraint;
            cc.ConstraintType = Constraint.Type.Distance;

            ConstraintReference r1 = cc.CreateConstraintReference(workPart.ComponentAssembly, fixtureTouchFace1, false, false, false);

            ConstraintReference r2 = cc.CreateConstraintReference(asmComponent, jaw2TouchFace, false, false, false);
            r2.SetFixHint(true);

            cc.SetExpression(JAW_CLEARANCE_MM);
        });

        // STEP 5 - jaw_1 <-> placa fixa, mesma folga de 2.5mm do lado
        // oposto (2.5mm + 2.5mm = 5mm de sobra total da materia-prima).
        RunConstraintBlock(workPart, (positioner, network) =>
        {
            Constraint constraint = positioner.CreateConstraint(true);
            ComponentConstraint cc = (ComponentConstraint)constraint;
            cc.ConstraintType = Constraint.Type.Distance;

            ConstraintReference r1 = cc.CreateConstraintReference(jaw1Component, jaw1TouchFace, false, false, false);

            ConstraintReference r2 = cc.CreateConstraintReference(workPart.ComponentAssembly, fixtureTouchFace2, false, false, false);
            r2.SetFixHint(true);

            cc.SetExpression(JAW_CLEARANCE_MM);
        });

        theSession.ListingWindow.WriteLine("Alinhamento automatico da morsa concluido.");
    }

    // =====================================================================
    // Helpers de constraint / componente
    // =====================================================================

    private static void RunConstraintBlock(Part workPart, Action<ComponentPositioner, ComponentNetwork> buildConstraints)
    {
        ComponentPositioner positioner = workPart.ComponentAssembly.Positioner;
        Network network = positioner.EstablishNetwork();
        positioner.BeginAssemblyConstraints();

        ComponentNetwork componentNetwork = (ComponentNetwork)network;
        componentNetwork.NetworkArrangementsMode = ComponentNetwork.ArrangementsMode.Existing;
        componentNetwork.DisplayComponent = null;
        componentNetwork.MoveObjectsState = true;

        buildConstraints(positioner, componentNetwork);

        componentNetwork.Solve();
        componentNetwork.Solve();

        positioner.PrimaryArrangement = null;
        positioner.ClearNetwork();
        positioner.EndAssemblyConstraints();
    }

    private static Component FindComponent(Component parent, string partialName)
    {
        if (parent == null)
        {
            return null;
        }
        foreach (Component child in parent.GetChildren())
        {
            if (child.Name.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child;
            }
            Component found = FindComponent(child, partialName);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static NXOpen.TaggedObject SafeFindGeom(NXObject owner, string journalIdentifier)
    {
        try
        {
            if (owner is Component)
            {
                return (NXOpen.TaggedObject)((Component)owner).FindObject(journalIdentifier);
            }
            return null;
        }
        catch (NXException)
        {
            return null;
        }
    }

    private static NXOpen.TaggedObject SafeFindGeomFromFeature(NXOpen.Features.Feature feature, string journalIdentifier)
    {
        try
        {
            return (NXOpen.TaggedObject)feature.FindObject(journalIdentifier);
        }
        catch (NXException)
        {
            return null;
        }
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}

// ===========================================================================
// FaceFinderHelpers - colado no mesmo arquivo (o player de journal do NX
// compila apenas o arquivo que voce roda).
// ===========================================================================
public static class FaceFinderHelpers
{
    public struct PlanarFaceInfo
    {
        public Face TheFace;
        public double[] Point;
        public double[] Normal;
        public double[] Box;
    }

    public static Body GetOccurrenceBody(Component component)
    {
        if (component == null)
        {
            return null;
        }

        Part protoPart = component.Prototype as Part;
        if (protoPart == null)
        {
            return null;
        }

        foreach (Body protoBody in protoPart.Bodies)
        {
            NXObject occ = component.FindOccurrence(protoBody) as NXObject;
            if (occ is Body)
            {
                return (Body)occ;
            }
        }
        return null;
    }

    public static List<PlanarFaceInfo> GetPlanarFaces(Body body)
    {
        List<PlanarFaceInfo> result = new List<PlanarFaceInfo>();
        if (body == null)
        {
            return result;
        }

        NXOpen.UF.UFSession theUFSession = NXOpen.UF.UFSession.GetUFSession();
        Face[] faces = body.GetFaces();

        for (int i = 0; i < faces.Length; i++)
        {
            Face f = faces[i];
            int type;
            double[] point = new double[3];
            double[] dir = new double[3];
            double[] box = new double[6];
            double radius;
            double radData;
            int normalDir;

            try
            {
                theUFSession.Modl.AskFaceData(f.Tag, out type, point, dir, box, out radius, out radData, out normalDir);
            }
            catch (NXException)
            {
                continue;
            }

            const int UF_MODL_PLANE_FACE_TYPE = 22;

            if (type == UF_MODL_PLANE_FACE_TYPE)
            {
                PlanarFaceInfo info = new PlanarFaceInfo();
                info.TheFace = f;
                info.Point = point;
                info.Normal = dir;
                info.Box = box;
                result.Add(info);
            }
        }
        return result;
    }

    public static List<PlanarFaceInfo> GetPlanarFacesFromFeature(NXOpen.Features.Feature feature)
    {
        List<PlanarFaceInfo> result = new List<PlanarFaceInfo>();
        if (feature == null)
        {
            return result;
        }

        Body[] bodies;
        try
        {
            bodies = feature.GetBodies();
        }
        catch (NXException)
        {
            return result;
        }

        foreach (Body b in bodies)
        {
            result.AddRange(GetPlanarFaces(b));
        }
        return result;
    }

    public static bool TryFindSymmetricFacePair(List<PlanarFaceInfo> faces, int axisIndex, double angleToleranceDeg,
        out Face negFace, out Face posFace, out double matchedArea)
    {
        negFace = null;
        posFace = null;
        matchedArea = 0;

        double cosTolerance = Math.Cos(angleToleranceDeg * Math.PI / 180.0);
        double bestNegArea = 0;
        double bestPosArea = 0;

        foreach (PlanarFaceInfo info in faces)
        {
            double n = info.Normal[axisIndex];
            if (Math.Abs(n) < cosTolerance)
            {
                continue;
            }

            double dx = info.Box[3] - info.Box[0];
            double dy = info.Box[4] - info.Box[1];
            double dz = info.Box[5] - info.Box[2];
            double area;
            if (axisIndex == 0) area = dy * dz;
            else if (axisIndex == 1) area = dx * dz;
            else area = dx * dy;

            if (n < 0 && area > bestNegArea)
            {
                bestNegArea = area;
                negFace = info.TheFace;
            }
            else if (n > 0 && area > bestPosArea)
            {
                bestPosArea = area;
                posFace = info.TheFace;
            }
        }

        if (negFace == null || posFace == null)
        {
            return false;
        }

        double ratio = Math.Min(bestNegArea, bestPosArea) / Math.Max(bestNegArea, bestPosArea);
        matchedArea = (bestNegArea + bestPosArea) / 2.0;
        return ratio >= 0.9;
    }

    public static bool TryDetectBestCenteringPair(List<PlanarFaceInfo> faces, double angleToleranceDeg,
        out int bestAxis, out Face negFace, out Face posFace)
    {
        bestAxis = -1;
        negFace = null;
        posFace = null;
        double bestArea = 0;

        for (int axis = 0; axis < 3; axis++)
        {
            Face candidateNeg, candidatePos;
            double candidateArea;
            bool ok = TryFindSymmetricFacePair(faces, axis, angleToleranceDeg, out candidateNeg, out candidatePos, out candidateArea);
            if (ok && candidateArea > bestArea)
            {
                bestArea = candidateArea;
                bestAxis = axis;
                negFace = candidateNeg;
                posFace = candidatePos;
            }
        }

        return bestAxis >= 0;
    }

    private static double ApproxArea(PlanarFaceInfo info, int axisIndex)
    {
        double dx = info.Box[3] - info.Box[0];
        double dy = info.Box[4] - info.Box[1];
        double dz = info.Box[5] - info.Box[2];
        if (axisIndex == 0) return dy * dz;
        if (axisIndex == 1) return dx * dz;
        return dx * dy;
    }

    /// <summary>
    /// Como TryFindClosestFacingPair, mas quando varias faces empatam no
    /// menor gap (comum quando ha furos/chanfros/pequenos recortes tambem
    /// tocando naquele ponto), desempata pela MAIOR AREA COMBINADA em vez
    /// de pegar a primeira encontrada. gapTolerance define o quanto um
    /// candidato pode ser "quase tao proximo quanto o melhor" para entrar
    /// no desempate por area (ex: 0.05 = 0.05mm).
    /// </summary>
    public static bool TryFindClosestFacingPair(List<PlanarFaceInfo> facesA, List<PlanarFaceInfo> facesB,
        int axisIndex, double angleToleranceDeg, double gapTolerance, out Face faceA, out Face faceB, out double gap)
    {
        faceA = null;
        faceB = null;
        gap = double.PositiveInfinity;

        double cosTolerance = Math.Cos(angleToleranceDeg * Math.PI / 180.0);

        // 1a passada: menor gap entre todos os candidatos validos
        double minGap = double.PositiveInfinity;
        foreach (PlanarFaceInfo a in facesA)
        {
            if (Math.Abs(a.Normal[axisIndex]) < cosTolerance) continue;
            foreach (PlanarFaceInfo b in facesB)
            {
                if (Math.Abs(b.Normal[axisIndex]) < cosTolerance) continue;
                if (Math.Sign(a.Normal[axisIndex]) == Math.Sign(b.Normal[axisIndex])) continue;

                double d = Math.Abs(a.Point[axisIndex] - b.Point[axisIndex]);
                if (d < minGap)
                {
                    minGap = d;
                }
            }
        }

        if (double.IsPositiveInfinity(minGap))
        {
            return false;
        }

        // 2a passada: entre os candidatos "quase tao proximos quanto o
        // melhor", escolhe o de maior area combinada (a face de encosto
        // de verdade, nao uma coincidencia geometrica pequena)
        double bestCombinedArea = -1;
        foreach (PlanarFaceInfo a in facesA)
        {
            if (Math.Abs(a.Normal[axisIndex]) < cosTolerance) continue;
            foreach (PlanarFaceInfo b in facesB)
            {
                if (Math.Abs(b.Normal[axisIndex]) < cosTolerance) continue;
                if (Math.Sign(a.Normal[axisIndex]) == Math.Sign(b.Normal[axisIndex])) continue;

                double d = Math.Abs(a.Point[axisIndex] - b.Point[axisIndex]);
                if (d <= minGap + gapTolerance)
                {
                    double combinedArea = ApproxArea(a, axisIndex) + ApproxArea(b, axisIndex);
                    if (combinedArea > bestCombinedArea)
                    {
                        bestCombinedArea = combinedArea;
                        faceA = a.TheFace;
                        faceB = b.TheFace;
                        gap = d;
                    }
                }
            }
        }

        return faceA != null && faceB != null;
    }

    /// <summary>
    /// Como TryFindClosestFacingPair, mas ignora completamente a distancia
    /// atual entre as faces - escolhe pela MAIOR AREA COMBINADA entre
    /// faces com normais opostas. Isso e robusto mesmo quando as pecas
    /// estao longe da posicao final (o Touch existe justamente para
    /// aproxima-las - nao faz sentido escolher a face pelo gap atual).
    /// </summary>
    public static bool TryFindLargestFacingPair(List<PlanarFaceInfo> facesA, List<PlanarFaceInfo> facesB,
        int axisIndex, double angleToleranceDeg, out Face faceA, out Face faceB, out double combinedArea)
    {
        faceA = null;
        faceB = null;
        combinedArea = -1;

        double cosTolerance = Math.Cos(angleToleranceDeg * Math.PI / 180.0);

        foreach (PlanarFaceInfo a in facesA)
        {
            if (Math.Abs(a.Normal[axisIndex]) < cosTolerance) continue;

            foreach (PlanarFaceInfo b in facesB)
            {
                if (Math.Abs(b.Normal[axisIndex]) < cosTolerance) continue;
                if (Math.Sign(a.Normal[axisIndex]) == Math.Sign(b.Normal[axisIndex])) continue;

                // as faces precisam OLHAR UMA PARA A OUTRA (ou ja estar
                // encostando, gap=0). So rejeita quando a direcao esta
                // CLARAMENTE oposta a normal - nao usar Math.Sign puro
                // aqui, porque quando as faces ja estao tocando a
                // diferenca de posicao e exatamente zero, e Math.Sign(0)
                // nao bate com Math.Sign(normal), rejeitando por engano
                // o par correto.
                double direction = b.Point[axisIndex] - a.Point[axisIndex];
                double facingDot = direction * a.Normal[axisIndex];
                if (facingDot < -1e-6)
                {
                    continue;
                }

                double area = ApproxArea(a, axisIndex) + ApproxArea(b, axisIndex);
                if (area > combinedArea)
                {
                    combinedArea = area;
                    faceA = a.TheFace;
                    faceB = b.TheFace;
                }
            }
        }
        return faceA != null && faceB != null;
    }

    /// <summary>
    /// Testa os eixos (exceto excludeAxis, se >= 0 - tipicamente o eixo
    /// ja usado pelo Center22, que fisicamente nao pode ser tambem o
    /// eixo de encosto) e retorna o par de maior area combinada entre
    /// dois corpos.
    /// </summary>
    private static PlanarFaceInfo FindInfoForFace(List<PlanarFaceInfo> list, Face target)
    {
        foreach (PlanarFaceInfo info in list)
        {
            if (info.TheFace == target)
            {
                return info;
            }
        }
        PlanarFaceInfo empty = new PlanarFaceInfo();
        empty.TheFace = null;
        return empty;
    }

    public static bool TryFindOpposingFacePair(List<PlanarFaceInfo> faces, int axisIndex, double angleToleranceDeg,
        out Face negFace, out Face posFace, out double combinedArea)
    {
        negFace = null;
        posFace = null;
        combinedArea = 0;

        double cosTolerance = Math.Cos(angleToleranceDeg * Math.PI / 180.0);
        double bestNegArea = 0;
        double bestPosArea = 0;

        foreach (PlanarFaceInfo info in faces)
        {
            double n = info.Normal[axisIndex];
            if (Math.Abs(n) < cosTolerance)
            {
                continue;
            }

            double area = ApproxArea(info, axisIndex);

            if (n < 0 && area > bestNegArea)
            {
                bestNegArea = area;
                negFace = info.TheFace;
            }
            else if (n > 0 && area > bestPosArea)
            {
                bestPosArea = area;
                posFace = info.TheFace;
            }
        }

        combinedArea = bestNegArea + bestPosArea;
        return negFace != null && posFace != null;
    }

    private static double AveragePosition(List<PlanarFaceInfo> faces, int axisIndex)
    {
        double sum = 0;
        int count = 0;
        foreach (PlanarFaceInfo f in faces)
        {
            sum += f.Point[axisIndex];
            count++;
        }
        return count > 0 ? sum / count : 0;
    }

    private static bool TryFindLargestFaceFacingTarget(List<PlanarFaceInfo> jawFaces, PlanarFaceInfo target,
        int axisIndex, double angleToleranceDeg, out Face jawFace, out double area)
    {
        jawFace = null;
        area = -1;

        double cosTolerance = Math.Cos(angleToleranceDeg * Math.PI / 180.0);
        double targetNormal = target.Normal[axisIndex];
        if (Math.Abs(targetNormal) < cosTolerance)
        {
            return false;
        }

        foreach (PlanarFaceInfo f in jawFaces)
        {
            if (Math.Abs(f.Normal[axisIndex]) < cosTolerance) continue;
            if (Math.Sign(f.Normal[axisIndex]) == Math.Sign(targetNormal)) continue;

            // Sem checagem de direcao aqui: ja estamos ancorados numa face
            // GRANDE e ja validada da fixture (target), entao "normal
            // oposta + maior area" e suficiente.
            double a = ApproxArea(f, axisIndex);
            if (a > area)
            {
                area = a;
                jawFace = f.TheFace;
            }
        }
        return jawFace != null;
    }

    /// <summary>
    /// Detecta o eixo de aperto ancorado no par simetrico da PROPRIA
    /// fixture (mesma logica ja validada no Center22), em vez de vasculhar
    /// a fixture inteira. Determina qual mordente fica do lado positivo e
    /// qual fica do lado negativo pela posicao media das faces de cada um,
    /// depois busca em cada mordente apenas a face que encosta na face
    /// correspondente (ja conhecida) da fixture.
    /// </summary>
    public static bool TryDetectClampingAxis(List<PlanarFaceInfo> jaw2Faces, List<PlanarFaceInfo> jaw1Faces,
        List<PlanarFaceInfo> fixtureFaces, double angleToleranceDeg, int excludeAxis,
        out int bestAxis, out Face jaw2Face, out Face fixtureFaceForJaw2, out Face jaw1Face, out Face fixtureFaceForJaw1)
    {
        bestAxis = -1;
        jaw2Face = null;
        fixtureFaceForJaw2 = null;
        jaw1Face = null;
        fixtureFaceForJaw1 = null;
        double bestScore = -1;

        for (int axis = 0; axis < 3; axis++)
        {
            if (axis == excludeAxis)
            {
                continue;
            }

            Face fNeg, fPos;
            double fixtureArea;
            bool fixOk = TryFindOpposingFacePair(fixtureFaces, axis, angleToleranceDeg, out fNeg, out fPos, out fixtureArea);
            if (!fixOk)
            {
                continue;
            }

            PlanarFaceInfo fNegInfo = FindInfoForFace(fixtureFaces, fNeg);
            PlanarFaceInfo fPosInfo = FindInfoForFace(fixtureFaces, fPos);

            double jaw2Avg = AveragePosition(jaw2Faces, axis);
            double jaw1Avg = AveragePosition(jaw1Faces, axis);
            bool jaw2IsPosSide = jaw2Avg > jaw1Avg;
            double separation = Math.Abs(jaw2Avg - jaw1Avg);

            List<PlanarFaceInfo> posSideFaces = jaw2IsPosSide ? jaw2Faces : jaw1Faces;
            List<PlanarFaceInfo> negSideFaces = jaw2IsPosSide ? jaw1Faces : jaw2Faces;

            Face posJawFace, negJawFace;
            double posArea, negArea;
            bool okPos = TryFindLargestFaceFacingTarget(posSideFaces, fPosInfo, axis, angleToleranceDeg, out posJawFace, out posArea);
            bool okNeg = TryFindLargestFaceFacingTarget(negSideFaces, fNegInfo, axis, angleToleranceDeg, out negJawFace, out negArea);

            if (!okPos || !okNeg)
            {
                continue;
            }

            // O CRITERIO CERTO para escolher o eixo de aperto e a SEPARACAO
            // entre os mordentes, nao a area das faces (um par pode ser
            // grande - ex: topo/fundo do bloco - sem ser o eixo real de
            // aperto). Os mordentes so ficam bem afastados um do outro no
            // eixo em que realmente se abrem/fecham.
            double score = separation;
            if (score > bestScore)
            {
                bestScore = score;
                bestAxis = axis;

                if (jaw2IsPosSide)
                {
                    jaw2Face = posJawFace;
                    fixtureFaceForJaw2 = fPos;
                    jaw1Face = negJawFace;
                    fixtureFaceForJaw1 = fNeg;
                }
                else
                {
                    jaw2Face = negJawFace;
                    fixtureFaceForJaw2 = fNeg;
                    jaw1Face = posJawFace;
                    fixtureFaceForJaw1 = fPos;
                }
            }
        }

        return bestAxis >= 0;
    }
}
