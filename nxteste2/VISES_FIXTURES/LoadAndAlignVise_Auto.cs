// NX 2406
// LoadAndAlignVise.cs
// Classe reutilizavel (chamada via LoadAndAlignVise_Auto.Run(null) a
// partir da sua aplicacao) - carrega o componente da morsa (do disco, se
// necessario) e roda o alinhamento automatico completo.
//
// IMPORTANTE (projeto Visual Studio): este arquivo depende da classe
// FaceFinderHelpers, que deve estar em um arquivo PROPRIO no mesmo
// projeto (FaceFinderHelpers.cs) - NAO cole essa classe aqui de novo,
// senao da erro de definicao duplicada / ambiguidade.
//
// TUDO e detectado geometricamente, incluindo o STEP 3 (profundidade):
// como so existem 3 eixos, o eixo de profundidade e deduzido por
// eliminacao (3 - eixo_centralizacao - eixo_aperto), e as faces de
// referencia usam o mesmo criterio de "par oposto de maior area" ja
// validado no Center22 e no Touch/Distance dos mordentes.
//
// Se o resultado final sair na direcao errada em alguma peca, o mais
// facil e so inverter o SINAL das constantes JAW_CLEARANCE_MM ou
// Z_DEPTH_OFFSET_MM.
//
// -----------------------------------------------------------------------
// CORRECAO (ago/2026): o alinhamento so "aparecia" concluido depois de
// fechar a aplicacao que chama Run(). Causa raiz: o solve das
// constraints (ComponentNetwork.Solve) marca o modelo como "sujo" e
// enfileira a atualizacao/repintura no Update Manager da NX, mas nunca
// existia uma chamada explicita de Session.UpdateManager.DoUpdate(...)
// para forcar esse update a ser processado e exibido imediatamente -
// por isso ele so era descarregado quando a sessao/aplicacao fechava
// (fluxo que forca o flush de tudo que estava pendente). Toda macro
// gravada pela propria NX (Tools > Journal > Record) sempre fecha cada
// operacao com SetUndoMark + UpdateManager.DoUpdate; este arquivo agora
// segue o mesmo padrao. Alem disso, a ListingWindow so e aberta agora
// sob demanda (quando ha erro/aviso de verdade), entao numa execucao
// bem-sucedida nenhuma janela de log aparece.
// -----------------------------------------------------------------------
using System;
using System.IO;
using System.Collections.Generic;
using NXOpen;
using NXOpen.Assemblies;
using NXOpen.Positioning;
public class LoadAndAlignVise_Auto
{
    // =====================================================================
    // CONFIGURACAO - ajuste aqui, sem precisar mexer no meio do codigo
    // =====================================================================
    // --- Carregamento do componente ---
    private const string COMPONENT_PART_NAME = "day_one_setup_vice_assmbly";
    // Caminho RELATIVO do .prt dentro da pasta do projeto (subpasta +
    // nome do arquivo). O caminho completo e calculado em runtime a
    // partir da pasta onde o .dll/executavel esta rodando - assim
    // funciona em qualquer maquina, sem depender de "C:\..." fixo.
    // Coloque o .prt em: <pasta do projeto>\Resources\day_one_setup_vice_assmbly.prt
    // e marque "Copy to Output Directory" = "Copy if newer" nas
    // propriedades do arquivo no Visual Studio.
    private const string COMPONENT_RELATIVE_PATH = @"Resources\day_one_setup_vice_assmbly.prt";
    /// <summary>
    /// Calcula o caminho completo do .prt a partir da pasta onde o
    /// assembly (.dll) atual esta rodando, combinada com o caminho
    /// relativo configurado acima.
    /// </summary>
    private static string GetComponentFullPath()
    {
        string assemblyDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        return Path.Combine(assemblyDir, COMPONENT_RELATIVE_PATH);
    }
    // --- Alinhamento ---
    private const string ASSEMBLY_COMPONENT_NAME = "day_one_setup_vice_assmbly";
    private const string JAW2_COMPONENT_NAME = "day_one_setup_vice_jaw_2";
    private const string JAW1_COMPONENT_NAME = "day_one_setup_vice_jaw_1";
    private const double FACE_ANGLE_TOLERANCE_DEG = 5.0;
    // Folga de cada mordente em relacao a fixture. Separadas em duas
    // constantes porque cada lado pode precisar de sinal/magnitude
    // diferente dependendo de qual face foi escolhida como referencia
    // (jaw_2 = mordente fixo, jaw_1 = mordente movel).
    private const string JAW2_CLEARANCE_MM = "0";
    private const string JAW1_CLEARANCE_MM = "35";
    // Deslocamento em Z (profundidade) da peca em relacao a face de
    // referencia da placa fixa (STEP 3 - Distance).
    private const string Z_DEPTH_OFFSET_MM = "-5";
    /// <summary>
    /// Ponto de entrada para chamar a partir da aplicacao:
    /// LoadAndAlignVise_Auto.Run(null);
    /// </summary>
    public static void Run(string[] args)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        ListingWindow lw = theSession.ListingWindow;
        // Undo mark "guarda-chuva" de toda a operacao. E ele que
        // permite forcar, no final (ou em qualquer saida antecipada),
        // o processamento IMEDIATO do update pendente via
        // UpdateManager.DoUpdate - sem isso, o resultado do ultimo
        // Solve() so era exibido quando a aplicacao/sessao fechava.
        // Invisible = nao aparece na lista de Undo do usuario.
        Session.UndoMarkId markId = theSession.SetUndoMark(Session.MarkVisibility.Invisible, "LoadAndAlignVise_Auto");
        // -----------------------------------------------------------
        // PARTE 1 - Garantir que o componente da morsa esta na montagem
        // (carrega do disco se necessario, e nao duplica se ja estiver la)
        // -----------------------------------------------------------
        Component asmComponent = FindComponent(workPart.ComponentAssembly.RootComponent, ASSEMBLY_COMPONENT_NAME);
        if (asmComponent == null)
        {
            if (!AddViseComponent(theSession, workPart, lw))
            {
                FlushUpdate(theSession, markId, lw); // erro ja reportado dentro de AddViseComponent
                return;
            }
            asmComponent = FindComponent(workPart.ComponentAssembly.RootComponent, ASSEMBLY_COMPONENT_NAME);
            if (asmComponent == null)
            {
                LogMessage(lw, "ERRO: componente foi adicionado mas nao foi encontrado na arvore da montagem logo em seguida.");
                FlushUpdate(theSession, markId, lw);
                return;
            }
        }
        // -----------------------------------------------------------
        // PARTE 2 - Alinhamento automatico
        // -----------------------------------------------------------
        Component jaw2Component = FindComponent(asmComponent, JAW2_COMPONENT_NAME);
        Component jaw1Component = FindComponent(asmComponent, JAW1_COMPONENT_NAME);
        if (jaw2Component == null || jaw1Component == null)
        {
            LogMessage(lw, "ERRO: jaw_1 ou jaw_2 nao encontrado dentro de '" + ASSEMBLY_COMPONENT_NAME + "'.");
            FlushUpdate(theSession, markId, lw);
            return;
        }
        Body fixtureBody = FindFixtureBody(workPart, asmComponent, lw);
        if (fixtureBody == null)
        {
            FlushUpdate(theSession, markId, lw); // erro ja reportado dentro de FindFixtureBody
            return;
        }
        List<FaceFinderHelpers.PlanarFaceInfo> jaw2Faces = FaceFinderHelpers.GetPlanarFaces(FaceFinderHelpers.GetOccurrenceBody(jaw2Component));
        List<FaceFinderHelpers.PlanarFaceInfo> jaw1Faces = FaceFinderHelpers.GetPlanarFaces(FaceFinderHelpers.GetOccurrenceBody(jaw1Component));
        List<FaceFinderHelpers.PlanarFaceInfo> fixtureFaces = FaceFinderHelpers.GetPlanarFaces(fixtureBody);
        // --- STEP 1 - Center22 (deteccao geometrica) ---
        int centeringAxis;
        Face jaw2Neg, jaw2Pos;
        bool jaw2PairOk = FaceFinderHelpers.TryDetectBestCenteringPair(jaw2Faces, FACE_ANGLE_TOLERANCE_DEG, out centeringAxis, out jaw2Neg, out jaw2Pos);
        if (!jaw2PairOk)
        {
            LogMessage(lw, "ERRO: nao foi possivel detectar o par de centralizacao do jaw_2.");
            FlushUpdate(theSession, markId, lw);
            return;
        }
        Face fixtureNeg, fixturePos;
        double fixtureMatchedArea;
        bool fixturePairOk = FaceFinderHelpers.TryFindSymmetricFacePair(fixtureFaces, centeringAxis, FACE_ANGLE_TOLERANCE_DEG, out fixtureNeg, out fixturePos, out fixtureMatchedArea);
        if (!fixturePairOk)
        {
            LogMessage(lw, "ERRO: nao foi possivel encontrar o par da placa fixa no eixo detectado do jaw_2.");
            FlushUpdate(theSession, markId, lw);
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
        // --- STEP 2 - Libera o override de posicao do jaw_1 ---
        jaw1Component.EstablishPositionOverride(null);
        // --- STEP 4 e 5 (deteccao adiantada) - eixo de aperto, cruzando
        //    jaw_2 e jaw_1. Precisamos disso ANTES do STEP 3 porque o
        //    eixo de profundidade e deduzido por eliminacao (o eixo que
        //    sobra depois do de centralizacao e do de aperto).
        int clampAxis;
        Face jaw2TouchFace, fixtureTouchFace1, jaw1TouchFace, fixtureTouchFace2;
        bool clampOk = FaceFinderHelpers.TryDetectClampingAxis(jaw2Faces, jaw1Faces, fixtureFaces, FACE_ANGLE_TOLERANCE_DEG, centeringAxis,
            out clampAxis, out jaw2TouchFace, out fixtureTouchFace1, out jaw1TouchFace, out fixtureTouchFace2);
        if (!clampOk)
        {
            LogMessage(lw, "ERRO: nao foi possivel detectar o eixo de aperto (jaw_2/jaw_1 x fixture).");
            FlushUpdate(theSession, markId, lw);
            return;
        }
        // CORRECAO: o jaw1TouchFace detectado automaticamente estava
        // errado - troca para a OUTRA face do jaw_1 no mesmo eixo.
        {
            Face jaw1Neg, jaw1Pos;
            double jaw1Area;
            bool jaw1PairOk = FaceFinderHelpers.TryFindOpposingFacePair(jaw1Faces, clampAxis, FACE_ANGLE_TOLERANCE_DEG, out jaw1Neg, out jaw1Pos, out jaw1Area);
            if (jaw1PairOk)
            {
                jaw1TouchFace = (jaw1TouchFace == jaw1Neg) ? jaw1Pos : jaw1Neg;
            }
        }
        // --- STEP 3 - Distance (profundidade). STATIONARY OBJECT = face
        //    da peca (fixa). MOTION OBJECT = face do jaw_2 (a morsa
        //    inteira se move junto, ja que jaw_2 faz parte do
        //    asmComponent). O SetFixHint precisa estar na peca, nao no
        //    jaw - e isso que faz o solver de fato mover a morsa.
        {
            int depthAxis = 3 - centeringAxis - clampAxis;
            Face fixtureDepthNeg, fixtureDepthPos;
            double fixtureDepthArea;
            bool fixtureDepthOk = FaceFinderHelpers.TryFindOpposingFacePair(fixtureFaces, depthAxis, FACE_ANGLE_TOLERANCE_DEG, out fixtureDepthNeg, out fixtureDepthPos, out fixtureDepthArea);
            Face jaw2DepthNeg, jaw2DepthPos;
            double jaw2DepthArea;
            bool jaw2DepthOk = FaceFinderHelpers.TryFindOpposingFacePair(jaw2Faces, depthAxis, FACE_ANGLE_TOLERANCE_DEG, out jaw2DepthNeg, out jaw2DepthPos, out jaw2DepthArea);
            if (!fixtureDepthOk || !jaw2DepthOk)
            {
                LogMessage(lw, "ERRO: nao foi possivel detectar as faces de profundidade (eixo " + depthAxis.ToString() + ").");
                FlushUpdate(theSession, markId, lw);
                return;
            }
            // Lado Neg da peca / Pos do jaw_2 (calibrado nos testes)
            Face pieceFaceRef = fixtureDepthNeg;   // STATIONARY
            Face jaw2FaceRef = jaw2DepthPos;        // MOTION (topo da jaw)
            RunConstraintBlock(workPart, (positioner, network) =>
            {
                Constraint constraint = positioner.CreateConstraint(true);
                ComponentConstraint cc = (ComponentConstraint)constraint;
                cc.ConstraintType = Constraint.Type.Distance;
                // MOTION OBJECT primeiro (o que efetivamente se move)
                ConstraintReference r5 = cc.CreateConstraintReference(asmComponent, jaw2FaceRef, false, false, false);
                // STATIONARY OBJECT - fixo, a referencia nao se move
                ConstraintReference r6 = cc.CreateConstraintReference(workPart.ComponentAssembly, pieceFaceRef, false, false, false);
                r6.SetFixHint(true);
                cc.SetExpression(Z_DEPTH_OFFSET_MM);
            });
        }
        // STEP 4 - jaw_2 <-> placa fixa, com folga de JAW2_CLEARANCE_MM
        RunConstraintBlock(workPart, (positioner, network) =>
        {
            Constraint constraint = positioner.CreateConstraint(true);
            ComponentConstraint cc = (ComponentConstraint)constraint;
            cc.ConstraintType = Constraint.Type.Distance;
            ConstraintReference r1 = cc.CreateConstraintReference(workPart.ComponentAssembly, fixtureTouchFace1, false, false, false);
            ConstraintReference r2 = cc.CreateConstraintReference(asmComponent, jaw2TouchFace, false, false, false);
            r2.SetFixHint(true);
            cc.SetExpression(JAW2_CLEARANCE_MM);
        });
        // STEP 5 - jaw_1 <-> placa fixa, com folga de JAW1_CLEARANCE_MM
        // (este e o passo que move o mordente movel ate a peca)
        RunConstraintBlock(workPart, (positioner, network) =>
        {
            Constraint constraint = positioner.CreateConstraint(true);
            ComponentConstraint cc = (ComponentConstraint)constraint;
            cc.ConstraintType = Constraint.Type.Distance;
            ConstraintReference r1 = cc.CreateConstraintReference(jaw1Component, jaw1TouchFace, false, false, false);
            ConstraintReference r2 = cc.CreateConstraintReference(workPart.ComponentAssembly, fixtureTouchFace2, false, false, false);
            r2.SetFixHint(true);
            cc.SetExpression(JAW1_CLEARANCE_MM);
        });
        // -----------------------------------------------------------
        // PARTE 3 - Esconde a exibicao grafica das constraints de
        // montagem. So roda AQUI, depois de TODOS os 5 passos do
        // alinhamento terem terminado - se rodasse antes/no meio, os
        // constraints que a automacao ainda precisa criar/resolver
        // poderiam nao aparecer corretamente durante o solve (o
        // ComponentNetwork usa a exibicao de constraints internamente
        // enquanto resolve, entao desligar isso no meio do processo
        // pode causar erro ou comportamento inesperado no solver).
        // -----------------------------------------------------------
        ComponentPositioner assemblyPositioner = workPart.ComponentAssembly.Positioner;
        assemblyPositioner.DisplayConstraints = false;
        assemblyPositioner.DisplaySuppressedConstraints = false;
        workPart.ModelingViews.WorkView.Regenerate();
        // -----------------------------------------------------------
        // PARTE 4 - Forca a NX a processar e EXIBIR imediatamente tudo
        // que ficou pendente desde markId (os 5 solves de constraint +
        // a mudanca de DisplayConstraints). Sem isso, o update fica
        // "engatilhado" no Update Manager e so e mesmo processado
        // quando algo mais forca um flush geral (por exemplo, fechar a
        // aplicacao/sessao) - e era exatamente esse o motivo do
        // mordente movel so "terminar de chegar" depois de encerrar a
        // aplicacao.
        // -----------------------------------------------------------
        FlushUpdate(theSession, markId, lw);
    }
    // =====================================================================
    // Log / Update helpers
    // =====================================================================
    /// <summary>
    /// Escreve na Listing Window abrindo-a sob demanda (lazy). Assim,
    /// numa execucao bem-sucedida (sem nenhum ERRO/Aviso) a janela de
    /// log nunca chega a aparecer para o usuario.
    /// </summary>
    private static void LogMessage(ListingWindow lw, string message)
    {
        if (!lw.IsOpen)
        {
            lw.Open();
        }
        lw.WriteLine(message);
    }
    /// <summary>
    /// Forca o Update Manager a processar imediatamente tudo que foi
    /// alterado desde 'markId' (equivalente ao que a propria NX faz ao
    /// final de qualquer comando gravado em journal). Isso garante que
    /// o resultado do alinhamento (posicao final do mordente movel)
    /// seja exibido na tela assim que Run() termina, sem depender de
    /// nenhum evento externo (como fechar a aplicacao) para "destravar"
    /// a atualizacao pendente.
    /// </summary>
    private static void FlushUpdate(Session theSession, Session.UndoMarkId markId, ListingWindow lw)
    {
        try
        {
            int updateErrorCount = theSession.UpdateManager.DoUpdate(markId);
            if (updateErrorCount > 0)
            {
                LogMessage(lw, "Aviso: UpdateManager.DoUpdate retornou " + updateErrorCount.ToString() + " erro(s) ao finalizar a atualizacao do modelo.");
            }
        }
        catch (NXException ex)
        {
            LogMessage(lw, "Aviso: falha ao forcar UpdateManager.DoUpdate: " + ex.Message);
        }
    }
    // =====================================================================
    // Carregamento do componente
    // =====================================================================
    /// <summary>
    /// Localiza a peca da morsa ja carregada na sessao, ou carrega do
    /// disco (caminho calculado por GetComponentFullPath) e adiciona na
    /// montagem atual. Retorna true em caso de sucesso.
    /// </summary>
    private static bool AddViseComponent(Session theSession, Part workPart, ListingWindow lw)
    {
        Part sourcePart = FindLoadedPart(theSession, COMPONENT_PART_NAME);
        if (sourcePart == null)
        {
            string componentFullPath = GetComponentFullPath();
            if (!File.Exists(componentFullPath))
            {
                LogMessage(lw, "ERRO: peca '" + COMPONENT_PART_NAME + "' nao esta carregada na sessao " +
                    "e o arquivo nao foi encontrado em: " + componentFullPath);
                LogMessage(lw, "Confira se o .prt esta em '" + COMPONENT_RELATIVE_PATH + "' (relativo a pasta do .dll) e se 'Copy to Output Directory' esta marcado.");
                return false;
            }
            PartLoadStatus loadStatus;
            try
            {
                sourcePart = theSession.Parts.Open(componentFullPath, out loadStatus);
            }
            catch (NXException ex)
            {
                LogMessage(lw, "ERRO ao carregar '" + componentFullPath + "': " + ex.Message);
                return false;
            }
            if (loadStatus != null)
            {
                if (loadStatus.NumberUnloadedParts > 0)
                {
                    LogMessage(lw, "Aviso: " + loadStatus.NumberUnloadedParts.ToString() + " sub-peca(s) nao carregada(s) ao abrir " + COMPONENT_PART_NAME + ".");
                }
                loadStatus.Dispose();
            }
            if (sourcePart == null)
            {
                LogMessage(lw, "ERRO: falha ao carregar a peca '" + componentFullPath + "'.");
                return false;
            }
        }
        AddComponentBuilder addComponentBuilder = workPart.AssemblyManager.CreateAddComponentBuilder();
        try
        {
            addComponentBuilder.SetAllowMultipleAssemblyLocations(false);
            addComponentBuilder.SetComponentAnchor(null);
            addComponentBuilder.SetInitialLocationType(AddComponentBuilder.LocationType.WorkPartAbsolute);
            addComponentBuilder.SetCount(1);
            addComponentBuilder.Layer = -1;
            addComponentBuilder.SetUseReferenceSetAndApplyInitialLocation(false);
            addComponentBuilder.ReferenceSet = "Entire Part";
            addComponentBuilder.ComponentName = COMPONENT_PART_NAME.ToUpper();
            addComponentBuilder.SetCamComponentType(AddComponentBuilder.CamComponentType.Target);
            BasePart[] partsToAdd = new BasePart[1];
            partsToAdd[0] = sourcePart;
            addComponentBuilder.SetPartsToAdd(partsToAdd);
            NXObject result = addComponentBuilder.Commit();
            ErrorList errors = addComponentBuilder.GetOperationFailures();
            errors.Dispose();
            if (result == null)
            {
                LogMessage(lw, "Falha ao adicionar o componente '" + COMPONENT_PART_NAME + "' na montagem.");
                return false;
            }
            return true;
        }
        finally
        {
            addComponentBuilder.ResetPartsToAdd();
            addComponentBuilder.Destroy();
        }
    }
    private static Part FindLoadedPart(Session theSession, string partName)
    {
        foreach (Part p in theSession.Parts)
        {
            string leafName = Path.GetFileNameWithoutExtension(p.FullPath);
            if (string.Equals(leafName, partName, StringComparison.OrdinalIgnoreCase))
            {
                return p;
            }
        }
        return null;
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
    /// <summary>
    /// Localiza o Body da placa fixa/referencia. As pecas costumam ser
    /// IMPORTADAS (STEP/Parasolid/etc), entao a busca vai direto pela
    /// GEOMETRIA (bodies), sem depender de nome de feature ou historico
    /// de modelagem (que pecas importadas normalmente nao tem):
    ///   1) Body(s) direto(s) no workPart - se houver mais de um, o mais
    ///      simples (menos faces) e escolhido como referencia
    ///   2) Se nao houver nenhum body no workPart, procura um componente
    ///      IRMAO da morsa direto na raiz da montagem (peca importada
    ///      como componente separado, nao como body solto)
    /// </summary>
    private static Body FindFixtureBody(Part workPart, Component asmComponent, ListingWindow lw)
    {
        // 1) Body(s) direto(s) no workPart
        List<Body> topLevelBodies = new List<Body>();
        foreach (Body b in workPart.Bodies)
        {
            topLevelBodies.Add(b);
        }
        if (topLevelBodies.Count == 1)
        {
            return topLevelBodies[0];
        }
        else if (topLevelBodies.Count > 1)
        {
            // Desempate: a placa fixa/referencia costuma ser bem mais
            // simples (menos faces) do que a peca real usinada (que tem
            // furos, chanfros, roscas, etc - dezenas ou centenas de faces
            // a mais).
            Body simplest = null;
            int minFaceCount = int.MaxValue;
            foreach (Body b in topLevelBodies)
            {
                int count = b.GetFaces().Length;
                if (count < minFaceCount)
                {
                    minFaceCount = count;
                    simplest = b;
                }
            }
            LogMessage(lw, "Aviso: " + topLevelBodies.Count.ToString() + " bodies encontrados no workPart. " +
                "Usando automaticamente o corpo mais simples (menos faces = " + minFaceCount.ToString() + ") como placa fixa. " +
                "Se estiver errado, rode DebugWorkPartBodies.cs para conferir e ajuste o criterio se necessario.");
            return simplest;
        }
        // 2) Nenhum body no workPart - a peca a ser fixada pode entrar
        //    como um COMPONENTE separado, irmao da morsa, direto na raiz
        //    da montagem (comum em pecas importadas). Procura entre os
        //    componentes-filhos do root que NAO sao a morsa.
        Component root = workPart.ComponentAssembly.RootComponent;
        if (root != null)
        {
            List<Component> siblingCandidates = new List<Component>();
            foreach (Component child in root.GetChildren())
            {
                bool isViseAssembly = asmComponent != null && child.Tag == asmComponent.Tag;
                if (!isViseAssembly)
                {
                    siblingCandidates.Add(child);
                }
            }
            if (siblingCandidates.Count == 1)
            {
                Body occBody = FaceFinderHelpers.GetOccurrenceBody(siblingCandidates[0]);
                if (occBody != null)
                {
                    return occBody;
                }
                LogMessage(lw, "ERRO: componente '" + siblingCandidates[0].Name + "' encontrado, mas nao foi possivel obter seu Body de ocorrencia.");
                return null;
            }
            else if (siblingCandidates.Count > 1)
            {
                LogMessage(lw, "ERRO: ha " + siblingCandidates.Count.ToString() + " componentes na raiz alem da morsa - nao da pra escolher automaticamente qual e a peca a ser fixada.");
                foreach (Component c in siblingCandidates)
                {
                    LogMessage(lw, "  - " + c.Name);
                }
                return null;
            }
        }
        LogMessage(lw, "ERRO: nenhum body no workPart nem componente de referencia encontrado na raiz da montagem.");
        return null;
    }
    /// <summary>
    /// Localiza o sistema de coordenadas MCS_MILL (ja existente na
    /// configuracao CAM) - disponivel para uso futuro caso precise de
    /// uma referencia absoluta independente de qualquer componente.
    /// </summary>
    private static NXObject FindMcsMillCsys(Part workPart, ListingWindow lw)
    {
        try
        {
            foreach (CartesianCoordinateSystem csys in workPart.CoordinateSystems)
            {
                if (string.Equals(csys.Name, "MCS_MILL", StringComparison.OrdinalIgnoreCase))
                {
                    return csys;
                }
            }
        }
        catch (NXException ex)
        {
            LogMessage(lw, "Aviso: erro ao varrer workPart.CoordinateSystems: " + ex.Message);
        }
        try
        {
            NXOpen.CAM.CAMObject camObj = workPart.CAMSetup.CAMGroupCollection.FindObject("MCS_MILL");
            if (camObj is NXObject)
            {
                return (NXObject)camObj;
            }
        }
        catch (NXException ex)
        {
            LogMessage(lw, "Aviso: erro ao procurar MCS_MILL no CAMSetup: " + ex.Message);
        }
        LogMessage(lw, "ERRO: MCS_MILL nao encontrado (nem como CoordinateSystem, nem como grupo CAM).");
        return null;
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
}
