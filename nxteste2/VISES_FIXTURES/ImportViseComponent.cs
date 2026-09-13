// NX 2406
// ImportViseComponent.cs
//
// Importa QUALQUER .prt de morsa/fixture pra montagem atual, a partir de um
// caminho escolhido em tempo de execucao (ver botao "Import Vise" em
// Form1.cs) - ao contrario do LoadAndAlignVise_Auto.cs / GetVise.cs (que so
// sabem carregar o "day_one_setup_vice_assmbly" fixo, com caminho travado
// em codigo), este aceita qualquer arquivo, pensado especificamente pra
// morsa nova (centered/self-centering vise) que ainda vai ser inserida no
// projeto.
//
// IMPORTANTE - escopo desta classe: SO importa/insere o componente na
// montagem (mesma logica de AddComponentBuilder do GetVise.cs/
// LoadAndAlignVise_Auto.cs), sem rodar NENHUM alinhamento automatico. A
// logica de alinhamento do LoadAndAlignVise_Auto.cs (Center22 + Distance x3)
// e ESPECIFICA da topologia jaw_1/jaw_2 de um mordente fixo + um mordente
// movel - uma morsa self-centering de verdade tem os DOIS mordentes se
// movendo simetricamente (geralmente via engrenagem/cremalheira), entao
// precisa de constraints diferentes (tipicamente um par de Distance
// simetricos a partir do centro, ou um constraint dedicado de simetria, em
// vez do Center22 acoplado a um unico mordente fixo). Quando o .prt real da
// morsa nova estiver no projeto, me chama que eu adapto o alinhamento pra
// essa topologia - por enquanto, depois de importar, posicione manualmente
// ou use os constraints de montagem do NX.
using System;
using System.IO;
using NXOpen;
using NXOpen.Assemblies;

public class ImportViseComponent
{
    /// <summary>
    /// Importa (carrega do disco se necessario, e adiciona na montagem
    /// atual, na origem do work part) o componente cujo caminho completo
    /// vem em args[0]. Nao duplica se ja houver um componente com o mesmo
    /// nome (baseado no nome do arquivo, sem extensao) na arvore da
    /// montagem. Chame via: ImportViseComponent.Run(new[] { fullPath });
    /// </summary>
    public static void Run(string[] args)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        ListingWindow lw = theSession.ListingWindow;

        if (workPart == null)
        {
            LogMessage(lw, "ERRO: nenhuma peca aberta no NX.");
            return;
        }
        if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            LogMessage(lw, "ERRO: nenhum arquivo .prt informado.");
            return;
        }

        string fullPath = args[0];
        if (!File.Exists(fullPath))
        {
            LogMessage(lw, "ERRO: arquivo nao encontrado: " + fullPath);
            return;
        }
        string componentPartName = Path.GetFileNameWithoutExtension(fullPath);

        // Ja esta na montagem com esse nome? Nao duplica - so avisa.
        Component existing = FindComponent(workPart.ComponentAssembly.RootComponent, componentPartName);
        if (existing != null)
        {
            LogMessage(lw, "Componente '" + componentPartName + "' ja esta na montagem - nada foi importado de novo.");
            return;
        }

        Session.UndoMarkId markId = theSession.SetUndoMark(Session.MarkVisibility.Visible, "Import Vise Component");

        Part sourcePart = FindLoadedPart(theSession, componentPartName);
        if (sourcePart == null)
        {
            PartLoadStatus loadStatus;
            try
            {
                sourcePart = theSession.Parts.Open(fullPath, out loadStatus);
            }
            catch (NXException ex)
            {
                LogMessage(lw, "ERRO ao carregar '" + fullPath + "': " + ex.Message);
                return;
            }
            if (loadStatus != null)
            {
                if (loadStatus.NumberUnloadedParts > 0)
                    LogMessage(lw, "Aviso: " + loadStatus.NumberUnloadedParts.ToString() + " sub-peca(s) nao carregada(s) ao abrir " + componentPartName + ".");
                loadStatus.Dispose();
            }
            if (sourcePart == null)
            {
                LogMessage(lw, "ERRO: falha ao carregar a peca '" + fullPath + "'.");
                return;
            }
        }
        else
        {
            LogMessage(lw, "Peca '" + componentPartName + "' ja estava carregada na sessao - reaproveitando.");
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
            addComponentBuilder.ComponentName = componentPartName.ToUpper();
            addComponentBuilder.SetCamComponentType(AddComponentBuilder.CamComponentType.Target);

            BasePart[] partsToAdd = new BasePart[] { sourcePart };
            addComponentBuilder.SetPartsToAdd(partsToAdd);

            NXObject result = addComponentBuilder.Commit();
            ErrorList errors = addComponentBuilder.GetOperationFailures();
            errors.Dispose();

            if (result == null)
            {
                LogMessage(lw, "Falha ao adicionar o componente '" + componentPartName + "' na montagem.");
                return;
            }
            LogMessage(lw, "Componente '" + componentPartName + "' importado e adicionado a montagem com sucesso (na origem do work part - reposicione/alinhe manualmente por enquanto).");
        }
        finally
        {
            addComponentBuilder.ResetPartsToAdd();
            addComponentBuilder.Destroy();
        }

        try
        {
            theSession.UpdateManager.DoUpdate(markId);
        }
        catch (NXException ex)
        {
            LogMessage(lw, "Aviso: falha ao forcar UpdateManager.DoUpdate: " + ex.Message);
        }
    }

    private static void LogMessage(ListingWindow lw, string message)
    {
        if (!lw.IsOpen)
            lw.Open();
        lw.WriteLine(message);
    }

    private static Part FindLoadedPart(Session theSession, string partName)
    {
        foreach (Part p in theSession.Parts)
        {
            string leafName = Path.GetFileNameWithoutExtension(p.FullPath);
            if (string.Equals(leafName, partName, StringComparison.OrdinalIgnoreCase))
                return p;
        }
        return null;
    }

    private static Component FindComponent(Component parent, string partialName)
    {
        if (parent == null) return null;
        foreach (Component child in parent.GetChildren())
        {
            if (child.Name.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0)
                return child;
            Component found = FindComponent(child, partialName);
            if (found != null) return found;
        }
        return null;
    }

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
