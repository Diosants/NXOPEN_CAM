// NX 2406
// TEST_IMPORT_MORSA_C_BASE_01.cs
//
// JOURNAL DE TESTE - importa o MORSA-C-BASE-01.prt na montagem atual (SEM
// alinhamento nenhum) - pra voce confirmar rapidinho, direto no NX (Tools >
// Journal > Play Journal, sem precisar compilar o Visual Studio), que o
// arquivo e achado no lugar certo e e adicionado na montagem, antes de
// confiar no alinhamento completo do LoadAndAlignCenteredVise_Auto.cs (que
// ja usa esse mesmo nome de arquivo, mas SO funciona rodando de dentro do
// app compilado - ver aviso abaixo).
//
// AUTOCONTIDO DE PROPOSITO (sem depender de nenhum outro .cs do projeto,
// nem de FaceFinderHelpers): quando voce roda "Play Journal" direto no NX
// apontando pra um arquivo .cs, o NX compila so ESSE arquivo sozinho - ele
// NAO enxerga as outras classes do projeto Visual Studio (LoadAndAlignVise_
// Auto.cs, ImportViseComponent.cs, etc. so funcionam through o botao do
// app, que ja esta tudo compilado junto num unico .dll). Por isso este
// journal de teste tem sua propria copia minima da logica de carregar +
// adicionar componente, sem alinhamento.
//
// CAMINHO - CORRIGIDO (28/08): o arquivo NAO esta na pasta Resources\ do
// projeto (fonte) - ele so existe em bin\x64\Debug\Resources\, ou seja, foi
// colocado direto na pasta de saida do build, nao na pasta de origem do
// projeto. Apontei o FULL_PATH abaixo pra esse lugar onde ele REALMENTE
// esta hoje. ATENCAO: como esse arquivo nao esta na pasta Resources\ de
// origem (a que fica dentro do projeto no Visual Studio, com "Copy to
// Output Directory"), um "Clean Solution" ou apagar a pasta bin\ manualmente
// PODE APAGAR esse .prt tambem, ja que ele nao e rastreado como arquivo de
// origem do projeto. Recomendo copiar o MORSA-C-BASE-01.prt tambem pra
// dentro de Resources\ (a pasta do projeto, nao a do bin) e adicionar como
// "Existing Item" com "Copy to Output Directory" = "Copy if newer" - assim
// ele fica seguro e o LoadAndAlignCenteredVise_Auto.cs (que calcula o
// caminho a partir da pasta do .exe compilado) continua funcionando igual.
// Troque a constante FULL_PATH abaixo se voce mover o projeto de lugar.
//
// ARQUIVOS "IRMAOS" - NOVO (28/08): o MORSA-C-BASE-01.prt e a MONTAGEM PAI
// e referencia internamente os 5 arquivos abaixo (confirmado por voce). O
// NX so resolve essas referencias se esses 5 arquivos JA ESTIVEREM
// carregados na sessao ANTES de abrir/adicionar o MORSA-C-BASE-01.prt - por
// isso este journal agora carrega os 5 primeiro (theSession.Parts.Open),
// e SO DEPOIS abre e adiciona o MORSA-C-BASE-01.prt. Eles estao na MESMA
// pasta que o MORSA-C-BASE-01.prt (bin\x64\Debug\Resources\) - mesmo risco
// de "Clean Solution" apagar eles, ja documentado acima; vale copiar os 6
// juntos pra Resources\ (pasta de origem do projeto) quando der.
using System;
using System.Collections.Generic;
using System.IO;
using NXOpen;
using NXOpen.Assemblies;

public class TEST_IMPORT_MORSA_C_BASE_01
{
    private const string COMPONENT_FILE_NAME = "MORSA-C-BASE-01";
    private const string FULL_PATH = @"C:\Users\dcard\OneDrive\Desktop\WINDOWS FORM PROJECTS\nxteste2\nxteste2\bin\x64\Debug\Resources\MORSA-C-BASE-01.prt";

    // Arquivos irmaos referenciados internamente pelo MORSA-C-BASE-01.prt -
    // tem que estar carregados na sessao ANTES dele ser aberto/adicionado.
    // Mesma pasta do MORSA-C-BASE-01.prt.
    private static readonly string[] SIBLING_FILE_NAMES = new string[]
    {
        "01_sldprt",
        "02_R_sldprt",
        "03_L_sldprt",
        "04_sldprt",
        "celist_sldprt",
    };

    public static void Run(string[] args)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        ListingWindow lw = theSession.ListingWindow;
        lw.Open();

        if (workPart == null)
        {
            lw.WriteLine("ERRO: nenhuma peca aberta no NX.");
            return;
        }

        lw.WriteLine("Testando import de: " + FULL_PATH);

        if (!File.Exists(FULL_PATH))
        {
            lw.WriteLine("ERRO: arquivo NAO encontrado nesse caminho. Confira se o .prt esta mesmo em Resources\\MORSA-C-BASE-01.prt dentro da pasta do projeto (nao da pasta bin\\...\\Debug), e se o nome bate exatamente (maiusculas/minusculas e hifens).");
            return;
        }
        lw.WriteLine("Arquivo encontrado.");

        // -----------------------------------------------------------
        // PASSO 1 - carregar os 5 arquivos irmaos ANTES do pai, pra o NX
        // conseguir resolver as referencias internas do MORSA-C-BASE-01.prt.
        // Nao e fatal se algum falhar (so avisa) - mas se algum falhar, o
        // resultado final pode sair incompleto de novo.
        // -----------------------------------------------------------
        string resourcesDir = Path.GetDirectoryName(FULL_PATH);
        int siblingWarnings = 0;
        lw.WriteLine("Carregando os " + SIBLING_FILE_NAMES.Length.ToString() + " arquivo(s) irmao(s) primeiro...");
        foreach (string siblingName in SIBLING_FILE_NAMES)
        {
            bool ok = LoadSiblingFile(theSession, lw, resourcesDir, siblingName);
            if (!ok)
            {
                siblingWarnings++;
            }
        }
        if (siblingWarnings > 0)
        {
            lw.WriteLine("AVISO: " + siblingWarnings.ToString() + " de " + SIBLING_FILE_NAMES.Length.ToString() + " arquivo(s) irmao(s) nao carregaram - o MORSA-C-BASE-01 pode sair incompleto mesmo assim. Veja os erros acima.");
        }
        else
        {
            lw.WriteLine("Todos os " + SIBLING_FILE_NAMES.Length.ToString() + " arquivo(s) irmao(s) carregados com sucesso (ou ja estavam carregados).");
        }

        // Ja esta na montagem? Se estiver, provavelmente foi adicionado
        // ANTES dos irmaos estarem carregados (teste anterior) - por isso
        // pode estar incompleto. Nao mexe automaticamente nele (mais
        // seguro): peco pra voce apagar manualmente no Assembly Navigator
        // e rodar de novo.
        Component existing = FindComponent(workPart.ComponentAssembly.RootComponent, COMPONENT_FILE_NAME);
        if (existing != null)
        {
            lw.WriteLine("Componente '" + existing.Name + "' ja esta na montagem - nada foi importado de novo.");
            lw.WriteLine("Se esse componente foi adicionado ANTES desse teste (antes dos arquivos irmaos serem carregados), ele pode estar incompleto/quebrado.");
            lw.WriteLine("Recomendacao: apague esse componente manualmente no Assembly Navigator do NX e rode este journal de novo, agora que os irmaos ja sao carregados primeiro.");
            return;
        }

        Part sourcePart = FindLoadedPart(theSession, COMPONENT_FILE_NAME);
        if (sourcePart == null)
        {
            PartLoadStatus loadStatus;
            try
            {
                sourcePart = theSession.Parts.Open(FULL_PATH, out loadStatus);
            }
            catch (NXException ex)
            {
                lw.WriteLine("ERRO ao carregar '" + FULL_PATH + "': " + ex.Message);
                return;
            }
            if (loadStatus != null)
            {
                if (loadStatus.NumberUnloadedParts > 0)
                {
                    lw.WriteLine("Aviso: " + loadStatus.NumberUnloadedParts.ToString() + " sub-peca(s) nao carregada(s) ao abrir " + COMPONENT_FILE_NAME + ".");
                }
                loadStatus.Dispose();
            }
            if (sourcePart == null)
            {
                lw.WriteLine("ERRO: falha ao carregar a peca '" + FULL_PATH + "'.");
                return;
            }
            lw.WriteLine("Peca carregada do disco com sucesso.");
        }
        else
        {
            lw.WriteLine("Peca ja estava carregada na sessao - reaproveitando.");
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
            addComponentBuilder.SetCamComponentType(AddComponentBuilder.CamComponentType.Target);

            BasePart[] partsToAdd = new BasePart[] { sourcePart };
            addComponentBuilder.SetPartsToAdd(partsToAdd);

            NXObject result = addComponentBuilder.Commit();
            ErrorList errors = addComponentBuilder.GetOperationFailures();
            errors.Dispose();

            if (result == null)
            {
                lw.WriteLine("ERRO: falha ao adicionar o componente '" + COMPONENT_FILE_NAME + "' na montagem.");
                return;
            }

            Component added = result as Component;
            lw.WriteLine("SUCESSO: componente importado e adicionado na montagem" + (added != null ? " como '" + added.Name + "'" : "") + " (na origem do work part - sem alinhamento, e so esse o teste).");
        }
        finally
        {
            addComponentBuilder.ResetPartsToAdd();
            addComponentBuilder.Destroy();
        }
    }

    /// <summary>
    /// Carrega um arquivo irmao (referenciado internamente pelo
    /// MORSA-C-BASE-01.prt) na sessao do NX, se ainda nao estiver
    /// carregado. So carrega (Session.Parts.Open) - nao adiciona na
    /// montagem, porque quem faz isso e o proprio NX ao resolver as
    /// referencias internas do assembly pai. Retorna true se ja estava (ou
    /// ficou) carregado com sucesso.
    /// </summary>
    private static bool LoadSiblingFile(Session theSession, ListingWindow lw, string resourcesDir, string siblingName)
    {
        Part already = FindLoadedPart(theSession, siblingName);
        if (already != null)
        {
            lw.WriteLine("  - " + siblingName + ": ja estava carregado na sessao.");
            return true;
        }

        string siblingPath = Path.Combine(resourcesDir, siblingName + ".prt");
        if (!File.Exists(siblingPath))
        {
            lw.WriteLine("  - " + siblingName + ": ERRO - arquivo nao encontrado em " + siblingPath);
            return false;
        }

        PartLoadStatus loadStatus;
        try
        {
            Part loaded = theSession.Parts.Open(siblingPath, out loadStatus);
            if (loadStatus != null)
            {
                loadStatus.Dispose();
            }
            if (loaded == null)
            {
                lw.WriteLine("  - " + siblingName + ": ERRO - falha ao carregar (retornou nulo).");
                return false;
            }
            lw.WriteLine("  - " + siblingName + ": carregado com sucesso.");
            return true;
        }
        catch (NXException ex)
        {
            lw.WriteLine("  - " + siblingName + ": ERRO ao carregar - " + ex.Message);
            return false;
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

    public static int GetUnloadOption(string dummy)
    {
        return (int)Session.LibraryUnloadOption.Immediately;
    }
}
