// ============================================================================
// NXCADAutomation - GERAR_SHOP_DOC_HTML.cs
// CADCAMWise - Setup Sheet automatico para NX CAM
// ============================================================================
//
// O QUE ESSE JOURNAL FAZ:
// Gera uma "Folha de Processo" (Setup Sheet) completa em HTML, a partir
// dos dados de CAM da peca aberta no NX: ferramentas usadas (com desenho
// tecnico 3D-like), tempos de operacao, dimensoes do bloco/materia-prima
// e da peca final, vistas isometricas (com fixacao e origem WCS), e
// imagens de progresso de usinagem (IPW) por operacao. O HTML gerado e
// um arquivo unico, autocontido (sem dependencia de imagem externa),
// com campos editaveis (contenteditable) pro programador preencher
// antes de imprimir, e layout pronto pra impressao em A4 paisagem (5+
// paginas com quebra automatica).
//
// ----------------------------------------------------------------------
// PRE-REQUISITOS / PREMISSAS (o script pode falhar ou dar dados
// incompletos se isso nao for verdade na sua peca):
// ----------------------------------------------------------------------
//   1. A peca precisa ter um CAMSetup valido (modulo de CAM licenciado
//      no NX que esta rodando o journal).
//   2. Existe um grupo de geometria chamado exatamente "WORKPIECE" (usado
//      pra ler as dimensoes do bloco/Blank). Se o nome for diferente, a
//      extracao de dimensoes do bloco falha silenciosamente (aviso no
//      log, mas o resto do documento continua funcionando).
//   3. O bloco (materia-prima) e a peca final sao 2 CORPOS SOLIDOS
//      SEPARADOS no arquivo (nao um so corpo fazendo as duas funcoes).
//      A peca e identificada por eliminacao: o corpo cujas dimensoes
//      NAO batem com as do bloco.
//   4. Ferramentas do tipo BROCA (drill) que nao expoe todos os dados
//      via API dependem de CONVENCAO DE NOME pro diametro (ex:
//      "HSS_DRILL_D25", "DRILL_DIAM_16MM", "CBORE_24MM" - numero no
//      nome, com ou sem sufixo "MM"). Ferramentas sem numero no nome
//      (ex: "CENTER_DRILL" sozinho) ficam com diametro/comprimento
//      estimados de forma generica.
//   5. Fixacao (morsa/grampos) precisa estar visivel na tela ao rodar,
//      pra aparecer nas vistas de "Configuracao".
//
// ----------------------------------------------------------------------
// APIs REAIS CONFIRMADAS (via journal gravado pelo usuario ou
// documentacao oficial testada - nao sao chutes; cada uma foi validada
// rodando sem erro numa peca real). Uteis pra reaproveitar em outros
// projetos de automacao NXOpen:
// ----------------------------------------------------------------------
//   - operacao.GetToolpathCuttingTime() / GetToolpathTime()
//     ATENCAO: a documentacao oficial diz "retorna em segundos", mas
//     testes reais confirmaram que retorna em MINUTOS (fator 60x
//     confirmado comparando com Information->Object). Corrigido no
//     codigo multiplicando por 60.
//
//   - operacao.GetParent(CAMSetup.View.MachineTool) -> NCGroup
//     Retorna o grupo pai na visualizacao de ferramentas. Se for do
//     tipo NXOpen.CAM.Tool, e a ferramenta real da operacao.
//
//   - workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(tool)
//     -> MillToolBuilder, com:
//       .TlDiameterBuilder.Value      (diametro)
//       .TlCor1RadBuilder.Value       (raio de canto)
//       .TlFluteLnBuilder.Value       (comprimento de corte/flute)
//       .TlHeightBuilder.Value        (comprimento total)
//       .TlNumFlutesBuilder.Value     (numero de flautas)
//       .TlNumberBuilder.Value        (numero da ferramenta)
//       .TlTaperAngBuilder.Value      (angulo de cone)
//       .TlTipAngBuilder.Value        (angulo de ponta - NAO existe em
//                                      DrillStdToolBuilder, so em Mill)
//       .HolderSectionBuilder.GetSection(i) +
//         .GetAllParameters(secao, out lowerD, out comp, out taperAng,
//                            out upperD, out cornerRad)
//         -> secoes reais do porta-ferramenta (holder), quantas
//            existirem (indice 0, 1, 2...)
//
//   - Pra ferramentas tipo BROCA (o MillToolBuilder acima falha ou nao
//     expoe as propriedades), existem builders especificos, com os
//     MESMOS nomes de propriedade do MillToolBuilder (TlNumberBuilder
//     etc), so mudando o nome da classe/metodo de criacao:
//       CreateDrillStdToolBuilder(tool)        -> DrillStdToolBuilder
//       CreateDrillSpotdrillToolBuilder(tool)  -> DrillSpotdrillToolBuilder
//       CreateDrillCounterboreToolBuilder(tool)-> DrillCounterboreToolBuilder
//     (existe tambem um builder pra "Boring Bar" - o erro de cast
//     mostrou o tipo real da ferramenta como "NXOpen.CAM.
//     DrillBoringBarTool", mas o nome exato do BUILDER pra esse tipo
//     ainda NAO foi confirmado - precisaria de outro journal)
//
//   - workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE")
//     -> NXOpen.CAM.FeatureGeometry, usado com:
//     CreateMillGeomBuilder(workpiece).BlankGeometry.BlockLength /
//     .BlockWidth / .BlockHeight
//     -> dimensoes do bloco/materia-prima, quando definido como bloco
//        parametrico (nao um corpo solido separado)
//
//   - theSession.CAMSession.PathDisplay.ShowToolPath(operacao) /
//     .HideToolPath(operacao) -> mostra/esconde a LINHA do toolpath de
//     uma operacao especifica na tela (nao mostra a ferramenta solida)
//
//   - workPart.CAMSetup.Show3dWorkpiece(new CAMObject[]{operacao}) /
//     .Delete3dWorkpieces(...) -> mostra/apaga o IPW (In-Process
//     Workpiece) - o estagio REAL do material apos aquela operacao.
//     Funciona bem, mas tem custo computacional (deixa a geracao mais
//     lenta com muitas Operationen).
//
//   - theSession.DisplayManager.BlankObjects(objetos) /
//     .UnblankObjects(objetos) -> esconde/mostra objetos (Body,
//     Component) na tela. Objetos precisam ser DisplayableObject[].
//     CUIDADO: esconder TODOS os componentes de montagem pode esconder
//     a propria peca/bloco se eles estiverem dentro de um componente -
//     ja tentamos isso e quebrou (imagem em branco). So escondemos
//     Body por enquanto, nao Component.
//
//   - UFSession.GetUFSession().Disp.CreateImage(caminho, formato, cor)
//     -> tira "print" da tela atual e salva como arquivo de imagem.
//     API confirmada e funcionando (aparece como "Deprecated" na
//     assinatura, mas isso e so aviso, nao impede compilar/rodar).
//
//   - workPart.WCS.Visibility = true/false -> controla visibilidade do
//     triedro de origem (WCS) na tela.
//
// ----------------------------------------------------------------------
// TENTATIVAS QUE NAO FUNCIONARAM (documentado pra nao repetir o erro):
// ----------------------------------------------------------------------
//   - Mostrar a ferramenta SOLIDA (nao so a linha) durante ShowToolPath:
//     tentamos NXOpen.CAM.OperationDisplayOptionsBuilder (nao existe do
//     jeito que a documentacao sugeria), depois
//     PathDisplay.SetToolDisplayType(SolidWithHolder) sozinho (sem
//     efeito), depois SetToolDisplayType + PathDisplay.Play(true) +
//     pausa de 300ms (ainda sem efeito). Hipotese: Play() so renderiza
//     de verdade numa sessao interativa (com loop de eventos de tela
//     ativo), nao funcionando via journal/batch. Sem solucao encontrada
//     ate agora.
//
//   - CAMSetup.MinToolLen(objects) pra pegar o comprimento minimo de
//     ferramenta calculado pelo NX: confirmado via documentacao oficial
//     que o metodo retorna VOID - so dispara a exibicao na tela (like
//     Information window), sem devolver o valor pro codigo. Sem uso
//     programatico possivel por esse caminho.
//
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NXOpen;
using NXOpen.CAM;
using NXOpen.UF;

namespace NXCADAutomation.ShopDocumentation
{
    public class SHOP_DOCUMENTATION_DE
    {
        // ================================================================
        // >>> PERSONALIZACAO POR KUNDE - EDITE AQUI <<<
        //
        // Essas constantes controlam a marca/identidade visual do Setup
        // Sheet gerado. Mude aqui pra adaptar pra cada cliente/oficina,
        // sem precisar mexer no resto do codigo.
        // ================================================================

        // Nome da marca, exibido no logo do cabecalho (o texto e
        // dividido em duas partes com cores diferentes - ex: "CADCAM" +
        // "Wise", ou troque pra "STELMEC" + "" se preferir uma palavra so)
        private const string MARCA_PARTE1 = "CADCAM";
        private const string MARCA_PARTE2 = "Wise";

        // Cor de destaque usada em titulos, bordas e a segunda parte do
        // logo (formato hexadecimal, ex: '#4db8e8' = azul)
        private const string COR_DESTAQUE = "#4db8e8";

        // Titulo principal do documento (aparece no topo, ao lado do logo)
        private const string TITULO_DOCUMENTO = "PROZESSBLATT";
        private static object operacoesComImagemIPW;

        private class SecaoHolder
        {
            public double DiametroInferior;
            public double DiametroSuperior;
            public double Comprimento;
            public double AnguloCone;
            public double RaioCanto;
        }

        private class DadosFerramenta
        {
            public string Nome;
            public double Diametro = -1;
            public double RaioCanto = -1;
            public double ComprimentoCorte = -1;
            public double ComprimentoTotal = -1;
            public int NumeroFlautas = -1;
            public int NumeroFerramenta = -1;
            public double AnguloCone = -1;
            public double AnguloPonta = -1;
            public bool EhBroca = false;
            public List<SecaoHolder> SecoesHolder = new List<SecaoHolder>();
            public double TempoCorteSegundos = 0.0;
            public double TempoTotalSegundos = 0.0;
            public List<string> Operacoes = new List<string>();
        }

        private class OperacaoOrdenada
        {
            public int Sequencia;
            public string NomeOperacao;
            public string NomeFerramenta;
            public double TempoCorteSegundos;
            public double TempoTotalSegundos;
            public string ImagemBase64;
            public string ImagemIPW;
        }

        public static void Run(string[] args)
        {
            Session theSession = Session.GetSession();
            Part workPart = theSession.Parts.Work;

            theSession.ListingWindow.Open();
            theSession.ListingWindow.WriteLine("=== Gerando Setup Sheet - Tool List (HTML) ===");

            if (workPart.CAMSetup == null)
            {
                theSession.ListingWindow.WriteLine("Esta peca nao tem setup de CAM.");
                return;
            }

            // ----------------------------------------------------------------
            // Ativa a exibicao da FERRAMENTA SOLIDA (com porta-ferramenta)
            // durante o ShowToolPath - API confirmada via journal do
            // usuario. Precisa ser configurado uma vez so, antes do loop
            // de captura - a partir daqui, toda chamada ShowToolPath
            // desenha a ferramenta de verdade junto com o caminho.
            // ----------------------------------------------------------------
            try
            {
                theSession.CAMSession.PathDisplay.SetToolDisplayType(NXOpen.CAM.PathDisplay.ToolDisplayType.SolidWithHolder);
            }
            catch (Exception exToolDisp)
            {
                theSession.ListingWindow.WriteLine(
                    string.Format("  [AVISO] Nao foi possivel ativar exibicao solida da ferramenta: {0}", exToolDisp.Message));
            }

            // ----------------------------------------------------------------
            // Orienta a view para ISOMETRICA uma unica vez, no comeco, e
            // MANTEM assim durante toda a geracao (imagem principal +
            // cada operacao) - assim todas as estrategias ficam na mesma
            // perspectiva, facilitando comparar uma com a outra. A vista
            // original do usuario e restaurada so no final de tudo, via
            // UndoMark.
            // ----------------------------------------------------------------
            Session.UndoMarkId markRestaurarView = theSession.SetUndoMark(
                Session.MarkVisibility.Invisible, "Restaurar Vista Original");

            string imagemPrincipal = "";
            try
            {
                workPart.ModelingViews.WorkView.Orient(View.Canned.Isometric, View.ScaleAdjustment.Fit);
                imagemPrincipal = CapturarViewAtualBase64(theSession, workPart);
            }
            catch (Exception exIso)
            {
                theSession.ListingWindow.WriteLine(
                    string.Format("  [AVISO] Nao foi possivel orientar/capturar vista isometrica: {0}", exIso.Message));
            }

            // ----------------------------------------------------------------
            // Segunda captura: mesma vista, mas com o WCS (tripe X/Y/Z)
            // FORCADO visivel - simbolo universal da origem de trabalho
            // (equivalente ao G54). Restaura a visibilidade original do
            // WCS depois, sem alterar a preferencia do usuario.
            //
            // NOTA: tentamos esconder os componentes de montagem (morsa)
            // pra essa captura, mas isso quebrou a imagem (ficou em
            // branco) - provavelmente a peca/bloco estao DENTRO de
            // componentes, entao esconder "todos os componentes" escondia
            // tudo. Revertido - a imagem mostra a cena completa (com a
            // morsa), e o simbolo de origem fica como referencia central
            // aproximada.
            // ----------------------------------------------------------------
            string imagemOrigemWCS = "";
            bool wcsVisivelOriginal = false;
            try
            {
                wcsVisivelOriginal = workPart.WCS.Visibility;
                workPart.WCS.Visibility = true;
                // Vista de TOPO (nao isometrica) - projecao ortografica
                // direta do plano X/Y, mais adequada pra mostrar a
                // origem de trabalho.
                workPart.ModelingViews.WorkView.Orient(View.Canned.Top, View.ScaleAdjustment.Fit);
                imagemOrigemWCS = CapturarViewAtualBase64(theSession, workPart);
            }
            catch (Exception exWcs)
            {
                theSession.ListingWindow.WriteLine(
                    string.Format("  [AVISO] Nao foi possivel capturar origem WCS: {0}", exWcs.Message));
            }
            finally
            {
                try { workPart.WCS.Visibility = wcsVisivelOriginal; } catch { }
                // Reorienta de volta pra ISOMETRICA - as proximas capturas
                // (cada operacao) dependem dessa orientacao persistente.
                try { workPart.ModelingViews.WorkView.Orient(View.Canned.Isometric, View.ScaleAdjustment.Fit); } catch { }
            }

            // ----------------------------------------------------------------
            // 1) Percorre as Operationen, agrupando tudo por FERRAMENTA
            // ----------------------------------------------------------------
            Dictionary<string, DadosFerramenta> ferramentas = new Dictionary<string, DadosFerramenta>();
            List<OperacaoOrdenada> OperationenOrdenadas = new List<OperacaoOrdenada>();
            int totalOperacoes = 0;
            double tempoCorteTotalGeral = 0.0;
            double tempoTotalGeral = 0.0;

            foreach (CAMObject camObj in workPart.CAMSetup.CAMOperationCollection)
            {
                NXOpen.CAM.Operation operacao = camObj as NXOpen.CAM.Operation;
                if (operacao == null) continue;

                totalOperacoes++;

                double tempoCorteSegundos = 0.0;
                try
                {
                    // CORRIGIDO: a documentacao oficial diz "segundos", mas
                    // testes reais confirmaram fator 60x (comparado com o
                    // "Cutting Time" mostrado em Information->Object) - a
                    // API na verdade retorna MINUTOS. Multiplicamos por 60
                    // pra converter pra segundos de verdade.
                    tempoCorteSegundos = operacao.GetToolpathCuttingTime() * 60.0;
                }
                catch (Exception exTc)
                {
                    theSession.ListingWindow.WriteLine(
                        string.Format("  [AVISO TEMPO] '{0}': falha ao ler tempo de corte - {1}", operacao.Name, exTc.Message));
                }

                double tempoTotalSegundos = 0.0;
                try
                {
                    // CORRIGIDO: mesmo fator 60x (minutos, nao segundos)
                    tempoTotalSegundos = operacao.GetToolpathTime() * 60.0;
                }
                catch (Exception exT)
                {
                    theSession.ListingWindow.WriteLine(
                        string.Format("  [AVISO TEMPO] '{0}': falha ao ler tempo total - {1}", operacao.Name, exT.Message));
                }

                // Diagnostico: mostra o valor bruto lido de cada operacao,
                // pra confirmar se os numeros fazem sentido individualmente
                theSession.ListingWindow.WriteLine(
                    string.Format("  [DEBUG] {0}: corte={1:F2}s | total={2:F2}s",
                    operacao.Name, tempoCorteSegundos, tempoTotalSegundos));

                tempoCorteTotalGeral += tempoCorteSegundos;
                tempoTotalGeral += tempoTotalSegundos;

                string nomeFerrParaOrdem = "-";

                try
                {
                    NCGroup grupoFerramenta = operacao.GetParent(CAMSetup.View.MachineTool);

                    if (grupoFerramenta is NXOpen.CAM.Tool)
                    {
                        NXOpen.CAM.Tool ferramenta = (NXOpen.CAM.Tool)grupoFerramenta;
                        string nomeFerr = ferramenta.Name;
                        nomeFerrParaOrdem = nomeFerr;

                        if (!ferramentas.ContainsKey(nomeFerr))
                        {
                            DadosFerramenta dadosFerr = new DadosFerramenta();
                            dadosFerr.Nome = nomeFerr;

                            MillToolBuilder millBuilder = null;
                            try
                            {
                                millBuilder = workPart.CAMSetup.CAMGroupCollection.CreateMillToolBuilder(ferramenta);

                                try { dadosFerr.Diametro = millBuilder.TlDiameterBuilder.Value; } catch { }
                                try { dadosFerr.RaioCanto = millBuilder.TlCor1RadBuilder.Value; } catch { }
                                try { dadosFerr.ComprimentoCorte = millBuilder.TlFluteLnBuilder.Value; } catch { }
                                try { dadosFerr.ComprimentoTotal = millBuilder.TlHeightBuilder.Value; } catch { }
                                try { dadosFerr.NumeroFlautas = (int)millBuilder.TlNumFlutesBuilder.Value; } catch { }
                                try { dadosFerr.NumeroFerramenta = (int)millBuilder.TlNumberBuilder.Value; } catch { }
                                try { dadosFerr.AnguloCone = millBuilder.TlTaperAngBuilder.Value; } catch { }
                                try { dadosFerr.AnguloPonta = millBuilder.TlTipAngBuilder.Value; } catch { }

                                // Seco es reais do porta-ferramenta (API
                                // confirmada: MillingToolBuilder.
                                // HolderSectionBuilder -> GetSection(i) ->
                                // GetAllParameters(...))
                                try
                                {
                                    NXOpen.CAM.HolderSectionBuilder holderSecBuilder = millBuilder.HolderSectionBuilder;
                                    int idxSecao = 0;
                                    while (idxSecao < 10)
                                    {
                                        NXObject secaoObj;
                                        try { secaoObj = holderSecBuilder.GetSection(idxSecao); }
                                        catch { break; }
                                        if (secaoObj == null) break;

                                        double lowerD, comp, taperAng, upperD, cornerRad;
                                        holderSecBuilder.GetAllParameters(secaoObj, out lowerD, out comp, out taperAng, out upperD, out cornerRad);

                                        dadosFerr.SecoesHolder.Add(new SecaoHolder
                                        {
                                            DiametroInferior = lowerD,
                                            DiametroSuperior = upperD,
                                            Comprimento = comp,
                                            AnguloCone = taperAng,
                                            RaioCanto = cornerRad
                                        });

                                        idxSecao++;
                                    }
                                }
                                catch { }
                            }
                            catch
                            {
                                // Ferramenta nao e do tipo Mill (ex: broca -
                                // DrillStdTool, DrillCenterBellTool). Tenta
                                // primeiro o DrillStdToolBuilder (API real,
                                // confirmada por journal do usuario) - se
                                // conseguir dados reais, usa eles. Se nao,
                                // cai no fallback por nome via regex.
                                bool conseguiuViaDrillBuilder = TentarExtrairViaDrillStdToolBuilder(theSession, workPart, ferramenta, dadosFerr);

                                if (conseguiuViaDrillBuilder)
                                {
                                    dadosFerr.EhBroca = true;
                                    theSession.ListingWindow.WriteLine(
                                        string.Format("  [INFO] '{0}': dados REAIS extraidos via DrillStdToolBuilder.", nomeFerr));
                                }
                                else
                                {
                                    double diamEstimado = TentarExtrairDiametroDoNome(nomeFerr);
                                    if (diamEstimado > 0)
                                    {
                                        dadosFerr.Diametro = diamEstimado;
                                        dadosFerr.ComprimentoTotal = diamEstimado * 8.0;   // proporcao tipica de broca
                                        dadosFerr.ComprimentoCorte = diamEstimado * 4.0;
                                        dadosFerr.RaioCanto = 0.0; // brocas geralmente nao tem raio de canto
                                        dadosFerr.EhBroca = true;
                                        if (dadosFerr.AnguloPonta <= 0) dadosFerr.AnguloPonta = 118.0; // padrao da industria, se nao veio real

                                        theSession.ListingWindow.WriteLine(
                                            string.Format("  [INFO] '{0}': broca detectada, diametro {1}mm extraido do nome (demais dimensoes ESTIMADAS, nao confirmadas via API).", nomeFerr, diamEstimado));
                                    }
                                    else
                                    {
                                        theSession.ListingWindow.WriteLine(
                                            string.Format("  [AVISO] '{0}' nao e fresa e nao tem diametro no nome - usando valores genericos.", nomeFerr));
                                    }
                                }
                            }
                            finally
                            {
                                if (millBuilder != null) millBuilder.Destroy();
                            }

                            // FALLBACK: se o MillToolBuilder "funcionou"
                            // (nao caiu no catch acima) mas as leituras
                            // individuais falharam silenciosamente
                            // (ferramenta e tecnicamente aceita pelo
                            // builder mas nao expoe essas propriedades -
                            // comum em brocas), o Diametro continua -1.
                            // Nesse caso, aplica o MESMO fallback via
                            // regex do nome que usamos no catch externo.
                            if (dadosFerr.Diametro <= 0)
                            {
                                bool conseguiuViaDrillBuilder2 = TentarExtrairViaDrillStdToolBuilder(theSession, workPart, ferramenta, dadosFerr);

                                if (conseguiuViaDrillBuilder2)
                                {
                                    theSession.ListingWindow.WriteLine(
                                        string.Format("  [INFO] '{0}': MillToolBuilder nao expos propriedades, mas DrillStdToolBuilder deu dados REAIS.", nomeFerr));
                                }
                                else
                                {
                                    double diamEstimado2 = TentarExtrairDiametroDoNome(nomeFerr);
                                    if (diamEstimado2 > 0)
                                    {
                                        dadosFerr.Diametro = diamEstimado2;
                                        dadosFerr.ComprimentoTotal = diamEstimado2 * 8.0;
                                        dadosFerr.ComprimentoCorte = diamEstimado2 * 4.0;
                                        dadosFerr.RaioCanto = 0.0;

                                        theSession.ListingWindow.WriteLine(
                                            string.Format("  [INFO] '{0}': MillToolBuilder aceitou mas nao expos propriedades - diametro {1}mm extraido do nome (demais dimensoes ESTIMADAS).", nomeFerr, diamEstimado2));
                                    }
                                    else
                                    {
                                        theSession.ListingWindow.WriteLine(
                                            string.Format("  [AVISO] '{0}': MillToolBuilder aceitou mas nao expos propriedades, e nao tem diametro no nome - dados incompletos.", nomeFerr));
                                    }
                                }
                            }

                            // Deteccao ROBUSTA de broca: pelo nome conter
                            // "DRILL", independente de qual caminho de
                            // extracao de dados funcionou (as vezes uma
                            // broca passa pelo MillToolBuilder com
                            // sucesso, entao o fallback acima nao seria
                            // acionado - mas o desenho ainda precisa ser
                            // de broca, nao de fresa).
                            if (nomeFerr.ToUpper().Contains("DRILL"))
                            {
                                dadosFerr.EhBroca = true;
                                if (dadosFerr.AnguloPonta <= 0)
                                {
                                    dadosFerr.AnguloPonta = 118.0; // padrao da industria, nao confirmado via API
                                }

                                // Se o numero da ferramenta ainda nao foi
                                // encontrado (independente do diametro ja
                                // ter vindo por outro caminho), tenta mais
                                // uma vez so pra isso via DrillStdToolBuilder.
                                if (dadosFerr.NumeroFerramenta < 0)
                                {
                                    TentarExtrairViaDrillStdToolBuilder(theSession, workPart, ferramenta, dadosFerr);
                                }
                            }

                            ferramentas[nomeFerr] = dadosFerr;
                        }

                        ferramentas[nomeFerr].TempoCorteSegundos += tempoCorteSegundos;
                        ferramentas[nomeFerr].TempoTotalSegundos += tempoTotalSegundos;
                        ferramentas[nomeFerr].Operacoes.Add(operacao.Name);
                    }
                }
                catch (Exception ex)
                {
                    theSession.ListingWindow.WriteLine(
                        string.Format("  [AVISO] '{0}': {1}", operacao.Name, ex.Message));
                }

                // Captura uma imagem do toolpath dessa operacao especifica:
                // mostra so o toolpath dela, tira a foto, esconde de novo.
                // Reaproveita ShowToolPath/HideToolPath (confirmado no
                // journal do usuario de Postprocess) + a captura de tela
                // ja validada (CapturarViewAtualBase64).
                //
                // NOTA: tentamos mostrar a ferramenta solida durante essa
                // captura (SetToolDisplayType + Play(true) + pausa) mas
                // nao funcionou mesmo com API confirmada via journal -
                // provavelmente Play() so renderiza de verdade numa
                // sessao interativa, nao rodando via journal/batch.
                // Revertido pra manter a velocidade, sem o beneficio.
                string imagemOperacao = "";
                try
                {
                    theSession.CAMSession.PathDisplay.ShowToolPath(operacao);
                    imagemOperacao = CapturarViewAtualBase64(theSession, workPart);
                    theSession.CAMSession.PathDisplay.HideToolPath(operacao);
                }
                catch (Exception exImg)
                {
                    theSession.ListingWindow.WriteLine(
                        string.Format("  [AVISO] Nao foi possivel capturar imagem de '{0}': {1}", operacao.Name, exImg.Message));
                }

                // ------------------------------------------------------
                // IPW (In-Process Workpiece): mostra o estagio REAL do
                // material apos essa operacao (nao so a linha do
                // toolpath) - API descoberta via journal do usuario
                // (Show3dWorkpiece). Usa Delete3dWorkpieces depois pra
                // limpar o corpo temporario criado, sem acumular entre
                // Operationen.
                // ------------------------------------------------------
                string imagemIPW = "";
                try
                {
                    NXOpen.CAM.CAMObject[] objetoIPW = new NXOpen.CAM.CAMObject[] { operacao };
                    workPart.CAMSetup.Show3dWorkpiece(objetoIPW);
                    imagemIPW = CapturarViewAtualBase64(theSession, workPart);

                    try { workPart.CAMSetup.Delete3dWorkpieces(objetoIPW); }
                    catch { }
                }
                catch (Exception exIpw)
                {
                    theSession.ListingWindow.WriteLine(
                        string.Format("  [AVISO] Nao foi possivel capturar IPW de '{0}': {1}", operacao.Name, exIpw.Message));
                }

                // Registra na lista ordenada (ordem de usinagem = ordem
                // real da colecao de Operationen do CAM), independente de
                // ter conseguido achar a ferramenta ou nao.
                OperationenOrdenadas.Add(new OperacaoOrdenada
                {
                    Sequencia = totalOperacoes,
                    NomeOperacao = operacao.Name,
                    NomeFerramenta = nomeFerrParaOrdem,
                    TempoCorteSegundos = tempoCorteSegundos,
                    TempoTotalSegundos = tempoTotalSegundos,
                    ImagemBase64 = imagemOperacao,
                    ImagemIPW = imagemIPW
                });
            }

            theSession.ListingWindow.WriteLine(
                string.Format("Operacoes: {0} | Ferramentas unicas: {1}", totalOperacoes, ferramentas.Count));

            // Restaura a vista original do usuario - todas as capturas
            // (principal + cada operacao) ja foram feitas na isometrica.
            try { theSession.UndoToMark(markRestaurarView, null); } catch { }
            try { theSession.DeleteUndoMark(markRestaurarView, null); } catch { }

            // ----------------------------------------------------------------
            // 2) Monta o HTML
            // ----------------------------------------------------------------
            string nomePeca = System.IO.Path.GetFileNameWithoutExtension(workPart.FullPath);

            // Programador: usuario logado no Windows (automatico)
            string programador = Environment.UserName;

            string dataGeracao = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            StringBuilder html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang='pt-BR'>");
            html.AppendLine("<head>");
            html.AppendLine("<meta charset='UTF-8'>");
            html.AppendLine("<title>Tool List - " + nomePeca + "</title>");
            html.AppendLine("<style>");
            html.AppendLine("* { box-sizing: border-box; }");
            html.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; background:#1e1e1e; color:#eee; margin:25px; }");
            html.AppendLine(".pagina-impressao { }");
            html.AppendLine(".vista-peca-larga { width:100%; margin-top:15px; }");
            html.AppendLine(".vista-peca-larga svg { height:280px; }");
            html.AppendLine(".setup-instrucoes-campo-topo { width:100%; margin-bottom:15px; }");
            html.AppendLine(".campo-instrucoes-topo { min-height:70px; max-height:90px; }");
            html.AppendLine(".setup-instrucoes-imagens-lado-a-lado { display:flex; gap:15px; }");
            html.AppendLine(".setup-instrucoes-imagem-horizontal { flex:1; min-width:0; }");
            html.AppendLine(".setup-instrucoes-imagem-horizontal img { width:100%; height:300px; object-fit:fill; display:block; }");
            html.AppendLine(".cabecalho { display:flex; align-items:center; gap:20px; border-bottom:2px solid " + COR_DESTAQUE + "; padding-bottom:12px; margin-bottom:10px; }");
            html.AppendLine(".cabecalho-titulo h1 { margin:0; }");
            html.AppendLine(".duas-colunas-topo { display:flex; gap:20px; margin-bottom:18px; align-items:flex-start; }");
            html.AppendLine(".coluna-esquerda-topo { flex:1; min-width:280px; }");
            html.AppendLine(".coluna-direita-topo { flex:1; min-width:280px; }");
            html.AppendLine("table.tabela-info { width:100%; border-collapse:collapse; }");
            html.AppendLine("table.tabela-info td { border:1px solid #3a3a3a; padding:7px 10px; font-size:12.5px; }");
            html.AppendLine("td.td-rotulo-info { background:#333; color:" + COR_DESTAQUE + "; font-weight:bold; width:45%; }");
            html.AppendLine("td.td-valor-info { background:#242424; color:#fff; }");
            html.AppendLine("td.td-valor-info:focus { outline:2px solid " + COR_DESTAQUE + "; background:#2a2a2a; }");
            html.AppendLine(".titulo-tabela-dim { font-weight:bold; color:" + COR_DESTAQUE + "; font-size:12px; margin-bottom:4px; text-align:center; background:#333; padding:5px; border:1px solid #3a3a3a; }");
            html.AppendLine(".setup-instrucoes-container { display:flex; gap:15px; margin-bottom:20px; align-items:stretch; }");
            html.AppendLine(".setup-instrucoes-imagem { flex:1; min-width:300px; margin-bottom:0; }");
            html.AppendLine(".setup-instrucoes-campo { flex:1; min-width:280px; }");
            html.AppendLine(".campo-instrucoes { background:#242424; border:1px solid #3a3a3a; padding:12px; min-height:200px; color:#fff; font-size:13px; white-space:pre-line; line-height:1.6; }");
            html.AppendLine(".campo-instrucoes:focus { outline:2px solid " + COR_DESTAQUE + "; background:#2a2a2a; }");
            html.AppendLine(".vistas-container-grande { display:flex; gap:15px; margin-bottom:15px; flex-wrap:wrap; }");
            html.AppendLine(".vista-grande { flex:1; min-width:320px; background:#000; border:1px solid #3a3a3a; padding:10px; text-align:center; }");
            html.AppendLine(".vista-grande img { width:100%; max-height:300px; object-fit:contain; cursor:pointer; }");
            html.AppendLine(".vista-grande .vista-titulo { color:" + COR_DESTAQUE + "; font-size:12px; margin-bottom:6px; }");
            html.AppendLine(".ferramenta-detalhada-card { flex:1; min-width:280px; background:#181818; border:1px solid #3a3a3a; padding:10px; }");
            html.AppendLine(".ferramenta-vertical-wrapper { width:100%; height:340px; position:relative; margin:0 auto 10px auto; overflow:hidden; }");
            html.AppendLine(".ferramenta-vertical-wrapper .rotar-90 { width:340px; height:220px; position:absolute; top:50%; left:50%; transform:translate(-50%, -50%) rotate(-90deg); }");
            html.AppendLine("table.tabela-ferramenta-detalhe { width:100%; border-collapse:collapse; font-size:12px; }");
            html.AppendLine("table.tabela-ferramenta-detalhe td { border:1px solid #3a3a3a; padding:5px 8px; }");
            html.AppendLine("td.td-rotulo-fd { background:#242424; color:#ccc; }");
            html.AppendLine("td.td-valor-fd { background:#242424; color:" + COR_DESTAQUE + "; font-weight:bold; }");
            html.AppendLine("td.td-valor-fd:focus { outline:2px solid " + COR_DESTAQUE + "; background:#2a2a2a; }");
            html.AppendLine("table.tabela-dim { width:100%; border-collapse:collapse; }");
            html.AppendLine("table.tabela-dim td { border:1px solid #3a3a3a; padding:6px 10px; font-size:12.5px; }");
            html.AppendLine("td.td-rotulo-dim { background:#242424; color:#ccc; }");
            html.AppendLine("td.td-rotulo-dim .eixo-dim { color:#888; }");
            html.AppendLine("td.td-valor-dim { background:#242424; font-weight:bold; text-align:right; }");
            html.AppendLine("td.td-valor-dim-materia { color:#6f9bbd; }");
            html.AppendLine("td.td-valor-dim-peca { color:#dba85f; }");
            html.AppendLine("td.td-valor-dim:focus { outline:2px solid " + COR_DESTAQUE + "; background:#2a2a2a; }");
            html.AppendLine(".rotulo-campo { display:block; color:" + COR_DESTAQUE + "; font-size:10px; text-transform:uppercase; letter-spacing:0.5px; margin-bottom:3px; }");
            html.AppendLine(".campo-vazio { display:block; border-bottom:1px solid #555; height:16px; }");
            html.AppendLine(".vista-setup { text-align:center; background:#000; border:1px solid #3a3a3a; padding:10px; margin-bottom:15px; }");
            html.AppendLine(".vista-principal img { max-width:100%; max-height:450px; }");
            html.AppendLine(".vistas-container { display:flex; gap:12px; margin-bottom:15px; flex-wrap:wrap; }");
            html.AppendLine(".vista-dim { flex:1; min-width:280px; background:#f5f5f5 !important; }");
            html.AppendLine(".vista-dim .vista-titulo { color:#333; }");
            html.AppendLine(".vista-dim svg { width:100%; height:220px; }");
            html.AppendLine(".dim-legenda { color:#333; font-size:13px; font-weight:bold; margin-top:6px; }");
            html.AppendLine(".vista-titulo { color:" + COR_DESTAQUE + "; font-size:11px; text-transform:uppercase; letter-spacing:0.5px; margin-bottom:6px; }");
            html.AppendLine(".vista-setup img { max-width:100%; max-height:350px; }");
            html.AppendLine("h1 { color:#fff; font-size:22px; letter-spacing:1px; margin-bottom:2px; }");
            html.AppendLine(".info { color:#999; margin-bottom:20px; font-size:13px; }");
            html.AppendLine("@media print {");
            html.AppendLine("  @page { size: A4 landscape; margin: 8mm; }");
            html.AppendLine("  body { background:#fff; color:#000; margin:0; }");
            html.AppendLine("  .pagina-impressao { page-break-after: always; page-break-inside: avoid; }");
            html.AppendLine("  .pagina-impressao:last-of-type { page-break-after: auto; }");
            html.AppendLine("  h1, h2 { color:#000; }");
            html.AppendLine("  h2 { border-bottom-color:#000; }");
            html.AppendLine("  .info { color:#333; }");
            html.AppendLine("  table.toollist td, table.oplist td, table.oplist th { border-color:#999; }");
            html.AppendLine("  table.toollist-compacta th { background:#e0e0e0 !important; color:#000 !important; }");
            html.AppendLine("  table.toollist-compacta td { background:#fff !important; color:#000 !important; border-color:#999; }");
            html.AppendLine("  td.c-nome { background:#f0f0f0 !important; }");
            html.AppendLine("  td.c-icone { background:#1e1e1e !important; }");
            html.AppendLine("  td.c-nome .titulo, td.c-nome .numero { color:#000 !important; }");
            html.AppendLine("  td.c-dados, td.c-tempo { color:#000 !important; }");
            html.AppendLine("  td.c-dados .rotulo, td.c-tempo .rotulo { color:#333 !important; }");
            html.AppendLine("  table.oplist th { background:#e0e0e0 !important; color:#000 !important; }");
            html.AppendLine("  table.oplist td { color:#000 !important; }");
            html.AppendLine("  table.oplist tr:nth-child(even) td { background:#f2f2f2 !important; }");
            html.AppendLine("  .resumo { background:#e0e0e0 !important; color:#000 !important; border-color:#999 !important; }");
            html.AppendLine("  .logo-texto { fill:#000 !important; }");
            html.AppendLine("  .cabecalho { border-bottom-color:#000; }");
            html.AppendLine("  td.td-rotulo-info, td.td-rotulo-dim, .titulo-tabela-dim { background:#e0e0e0 !important; color:#000 !important; }");
            html.AppendLine("  td.td-valor-info, td.td-valor-dim { background:#fff !important; color:#000 !important; border-color:#999; }");
            html.AppendLine("  .campo-instrucoes { background:#fff !important; color:#000 !important; border-color:#999; }");
            html.AppendLine("  .vista-grande .vista-titulo { color:#000 !important; }");
            html.AppendLine("  .ferramenta-detalhada-card { background:#f5f5f5 !important; border-color:#999; }");
            html.AppendLine("  td.td-rotulo-fd { background:#e0e0e0 !important; color:#000 !important; }");
            html.AppendLine("  td.td-valor-fd { background:#fff !important; color:#000 !important; border-color:#999; }");
            html.AppendLine("  .vista-setup { border-color:#999; }");
            html.AppendLine("  .vista-titulo { color:#000 !important; }");
            html.AppendLine("  .rotulo-campo { color:#555 !important; }");
            html.AppendLine("  .campo-vazio { border-bottom-color:#000; }");
            html.AppendLine("  tr { page-break-inside: avoid; }");
            html.AppendLine("}");
            html.AppendLine("table.toollist { width:100%; border-collapse: collapse; table-layout:fixed; }");
            html.AppendLine("table.toollist-compacta { width:100%; border-collapse:collapse; margin-bottom:15px; }");
            html.AppendLine("table.toollist-compacta th { background:linear-gradient(to bottom, #2d5f7c, #1c3d52); color:#fff; text-align:left; padding:7px 10px; border:1px solid #333; border-bottom:2px solid " + COR_DESTAQUE + "; font-size:12px; font-weight:bold; }");
            html.AppendLine("table.toollist-compacta td { padding:5px 10px; border:1px solid #333; font-size:12px; color:#ddd; }");
            html.AppendLine("table.toollist-compacta tr:nth-child(odd) td { background:#1a1a1a; }");
            html.AppendLine("table.toollist-compacta tr:nth-child(even) td { background:#252b2f; }");
            html.AppendLine("table.toollist-compacta td.num { text-align:right; }");
            html.AppendLine("table.toollist td { border:1px solid #3a3a3a; padding:10px 12px; vertical-align:top; }");
            html.AppendLine("td.c-nome { width:11%; background:#242424; }");
            html.AppendLine("td.c-nome .titulo { font-weight:bold; font-size:14px; color:#fff; }");
            html.AppendLine("td.c-nome .numero { color:#888; font-size:11px; margin-top:6px; }");
            html.AppendLine("td.c-dados { width:9%; font-size:12px; }");
            html.AppendLine("td.c-dados .linha { display:flex; justify-content:space-between; align-items:center; padding:2px 0; }");
            html.AppendLine("td.c-dados .rotulo { color:#888; display:flex; align-items:center; gap:4px; }");
            html.AppendLine("td.c-dados .rotulo svg { flex-shrink:0; }");
            html.AppendLine("td.c-tempo { width:11%; font-size:12px; }");
            html.AppendLine("td.c-tempo .linha { display:flex; justify-content:space-between; align-items:center; padding:2px 0; }");
            html.AppendLine("td.c-tempo .rotulo { color:#888; display:flex; align-items:center; gap:4px; }");
            html.AppendLine("td.c-tempo .rotulo svg { flex-shrink:0; }");
            html.AppendLine("td.c-desc { width:11%; font-size:11.5px; color:#ccc; }");
            html.AppendLine("td.c-desc .rotulo-desc { color:#888; font-size:11px; text-transform:uppercase; letter-spacing:0.5px; margin-bottom:4px; }");
            html.AppendLine("td.c-desc ul { margin:0; padding-left:16px; }");
            html.AppendLine("td.c-desc li { margin-bottom:2px; }");
            html.AppendLine("td.c-icone { width:420px; height:150px; text-align:center; background:#181818; padding:8px; overflow:hidden; }");
            html.AppendLine("td.c-icone svg { max-width:100%; max-height:100%; display:block; margin:0 auto; }");
            html.AppendLine(".resumo { margin-top:18px; padding:12px 16px; background:#242424; border:1px solid #3a3a3a; font-weight:bold; font-size:13px; color:" + COR_DESTAQUE + "; }");
            html.AppendLine("h2 { color:" + COR_DESTAQUE + "; font-size:22px; font-weight:bold; margin-top:35px; margin-bottom:12px; letter-spacing:1.5px; border-bottom:2px solid " + COR_DESTAQUE + "; padding-bottom:6px; }");
            html.AppendLine(".oplist-duas-colunas { display:flex; gap:15px; align-items:flex-start; }");
            html.AppendLine(".oplist-coluna { flex:1; min-width:0; }");
            html.AppendLine("table.oplist { width:100%; border-collapse:collapse; table-layout:fixed; }");
            html.AppendLine("table.oplist col.col-img { width:70px; }");
            html.AppendLine("table.oplist col.col-seq { width:5%; }");
            html.AppendLine("table.oplist col.col-op { width:30%; }");
            html.AppendLine("table.oplist col.col-ferr { width:23%; }");
            html.AppendLine("table.oplist col.col-tc { width:15%; }");
            html.AppendLine("table.oplist col.col-t { width:15%; }");
            html.AppendLine("td.td-img { text-align:center; padding:3px !important; background:#000; }");
            html.AppendLine("img.thumb-op { width:60px; height:45px; object-fit:cover; cursor:pointer; transition:transform 0.15s; }");
            html.AppendLine("img.thumb-op:hover { transform:scale(1.15); }");
            html.AppendLine("#lightbox-overlay { display:none; position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(0,0,0,0.92); z-index:1000; cursor:zoom-out; align-items:center; justify-content:center; flex-direction:column; }");
            html.AppendLine("#lightbox-overlay img { max-width:90%; max-height:80%; border:2px solid " + COR_DESTAQUE + "; }");
            html.AppendLine("#lightbox-titulo { color:#fff; font-size:16px; margin-top:14px; font-family:Arial, sans-serif; }");
            html.AppendLine("table.oplist td, table.oplist th { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }");
            html.AppendLine("table.oplist th { background:linear-gradient(to bottom, #2d5f7c, #1c3d52); color:#fff; text-align:left; padding:6px 8px; border:1px solid #333; border-bottom:2px solid " + COR_DESTAQUE + "; font-size:11px; font-weight:bold; }");
            html.AppendLine("table.oplist td { padding:4px 8px; border:1px solid #333; font-size:11px; color:#ddd; }");
            html.AppendLine("table.oplist td.tempo-corte { color:#4caf50; font-weight:bold; }");
            html.AppendLine("table.oplist td.tempo-total { color:#e74c3c; font-weight:bold; }");
            html.AppendLine("table.oplist tr:nth-child(odd) td { background:#1a1a1a; }");
            html.AppendLine("table.oplist tr:nth-child(even) td { background:#252b2f; }");
            html.AppendLine("table.oplist td.num { text-align:right; }");
            html.AppendLine("</style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            // ------------------------------------------------------------
            // CABECALHO: logo CADCAMWise + titulo + campos de identificacao
            // da folha de processo (personalizavel: programador, maquina,
            // material, revisao - preenchimento manual antes de imprimir).
            // ------------------------------------------------------------
            // ================================================================
            // PAGINA 1: cabecalho + info + dimensoes + imagem da peca
            // ================================================================
            html.AppendLine("<div class='pagina-impressao'>");

            html.AppendLine("<div class='cabecalho'>");
            html.AppendLine("<div class='cabecalho-logo'>");
            html.AppendLine("<svg width='230' height='54' viewBox='0 0 260 60' xmlns='http://www.w3.org/2000/svg'>");
            html.AppendLine("<rect x='2' y='10' width='40' height='40' rx='8' fill='" + COR_DESTAQUE + "'/>");
            html.AppendLine("<circle cx='22' cy='30' r='13' fill='none' stroke='#1e1e1e' stroke-width='2.5'/>");
            html.AppendLine("<line x1='22' y1='30' x2='22' y2='17' stroke='#1e1e1e' stroke-width='2.5'/>");
            html.AppendLine("<line x1='22' y1='30' x2='31' y2='30' stroke='#1e1e1e' stroke-width='2.5'/>");
            html.AppendLine("<circle cx='22' cy='30' r='2' fill='#1e1e1e'/>");
            html.AppendLine(string.Format("<text x='52' y='38' font-family='Segoe UI, Arial' font-size='26' font-weight='700' class='logo-texto' fill='#ffffff'>{0}<tspan fill='{1}'>{2}</tspan></text>", MARCA_PARTE1, COR_DESTAQUE, MARCA_PARTE2));
            html.AppendLine("</svg>");
            html.AppendLine("</div>");
            html.AppendLine("<div class='cabecalho-titulo'>");
            html.AppendLine(string.Format("<h1>{0}</h1>", TITULO_DOCUMENTO));
            html.AppendLine(string.Format("<div class='info'>{0} &nbsp;&nbsp;|&nbsp;&nbsp; Erstellt am {1} &nbsp;&nbsp;|&nbsp;&nbsp; {2} Operationen &nbsp;&nbsp;|&nbsp;&nbsp; {3} Werkzeuge</div>",
                nomePeca, dataGeracao, totalOperacoes, ferramentas.Count));
            html.AppendLine("</div>");
            html.AppendLine("</div>");

            // Extrai as dimensoes ANTES de montar as tabelas
            double blocoL, blocoW, blocoH;
            bool temBloco = ObterDimensoesBlank(theSession, workPart, out blocoL, out blocoW, out blocoH);

            double pecaL = 0, pecaW = 0, pecaH = 0;
            Body corpoPeca = null;
            bool temPeca = false;
            if (temBloco)
            {
                temPeca = ObterDimensoesPeca(workPart, blocoL, blocoW, blocoH, out pecaL, out pecaW, out pecaH, out corpoPeca);
            }

            // --- Linha 1: Programador/Empresa/etc + Ordem de Servico/etc, lado a lado ---
            html.AppendLine("<div class='duas-colunas-topo'>");

            html.AppendLine("<div class='coluna-esquerda-topo'>");
            html.AppendLine("<table class='tabela-info'>");
            html.AppendLine(LinhaInfoEditavel("PROGRAMMIERER", programador));
            html.AppendLine(LinhaInfoEditavel("UNTERNEHMEN", ""));
            html.AppendLine(LinhaInfoEditavel("KUNDE", ""));
            html.AppendLine(LinhaInfoEditavel("MASCHINE", ""));
            html.AppendLine(LinhaInfoEditavel("STEUERUNG", ""));
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            html.AppendLine("<div class='coluna-direita-topo'>");
            html.AppendLine("<table class='tabela-info'>");
            html.AppendLine(LinhaInfoEditavel("AUFTRAGSNUMMER", "Nº"));
            html.AppendLine(LinhaInfoEditavel("DATUM", dataGeracao.Split(' ')[0]));
            html.AppendLine(LinhaInfoEditavel("LIEFERTERMIN", ""));
            html.AppendLine(LinhaInfoEditavel("ANZ. TEILE", "Nº"));
            html.AppendLine(LinhaInfoEditavel("BEDIENER", "NAME:"));
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            html.AppendLine("</div>");

            // --- Linha 2: Dimensoes da Materia Prima + Dimensoes da Peca, lado a lado ---
            html.AppendLine("<div class='duas-colunas-topo'>");

            html.AppendLine("<div class='coluna-esquerda-topo'>");
            html.AppendLine("<div class='titulo-tabela-dim'>ROHMATERIAL-ABMESSUNGEN</div>");
            html.AppendLine("<table class='tabela-dim'>");
            html.AppendLine(LinhaDimEditavel("LÄNGE", "X", temBloco ? blocoL.ToString("F1") : "NaN", "td-valor-dim-materia"));
            html.AppendLine(LinhaDimEditavel("BREITE", "Y", temBloco ? blocoW.ToString("F1") : "NaN", "td-valor-dim-materia"));
            html.AppendLine(LinhaDimEditavel("HÖHE/DICKE", "Z", temBloco ? blocoH.ToString("F1") : "NaN", "td-valor-dim-materia"));
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            html.AppendLine("<div class='coluna-direita-topo'>");
            html.AppendLine("<div class='titulo-tabela-dim'>TEIL-ABMESSUNGEN</div>");
            html.AppendLine("<table class='tabela-dim'>");
            html.AppendLine(LinhaDimEditavel("LÄNGE", "X", temPeca ? pecaL.ToString("F1") : "NaN", "td-valor-dim-peca"));
            html.AppendLine(LinhaDimEditavel("BREITE", "Y", temPeca ? pecaW.ToString("F1") : "NaN", "td-valor-dim-peca"));
            html.AppendLine(LinhaDimEditavel("HÖHE/DICKE", "Z", temPeca ? pecaH.ToString("F1") : "NaN", "td-valor-dim-peca"));
            html.AppendLine("</table>");
            html.AppendLine("</div>");

            html.AppendLine("</div>");

            // --- Imagens de diagrama: Materia Prima + Peca, lado a lado ---
            if (temBloco || temPeca)
            {
                html.AppendLine("<div class='vistas-container'>");

                if (temBloco)
                {
                    html.AppendLine("<div class='vista-setup vista-dim'>");
                    html.AppendLine("<div class='vista-titulo'>Rohmaterial - Isometrisch</div>");
                    html.AppendLine(GerarDiagramaBloco(blocoL, blocoW, blocoH, "materiaprima"));
                    html.AppendLine(string.Format("<div class='dim-legenda'>Länge: {0:F0}mm &nbsp;|&nbsp; Breite: {1:F0}mm &nbsp;|&nbsp; Höhe: {2:F0}mm</div>", blocoL, blocoW, blocoH));
                    html.AppendLine("</div>");
                }

                if (temPeca)
                {
                    html.AppendLine("<div class='vista-setup vista-dim'>");
                    html.AppendLine("<div class='vista-titulo'>Teil - Isometrisch</div>");
                    html.AppendLine(GerarDiagramaBloco(pecaL, pecaW, pecaH, "peca"));
                    html.AppendLine(string.Format("<div class='dim-legenda'>Länge: {0:F0}mm &nbsp;|&nbsp; Breite: {1:F0}mm &nbsp;|&nbsp; Höhe: {2:F0}mm</div>", pecaL, pecaW, pecaH));
                    html.AppendLine("</div>");
                }

                html.AppendLine("</div>");
            }

            // --- Espaco restante: foto real da peca montada na morsa
            // (mesma captura ja usada na pagina 2 - imagemPrincipal) ---
            if (!string.IsNullOrEmpty(imagemPrincipal))
            {
                html.AppendLine("<div class='vista-setup vista-dim vista-peca-larga'>");
                html.AppendLine("<div class='vista-titulo'>Teil in Vorrichtung - Isometrische Ansicht</div>");
                html.AppendLine(string.Format("<img onclick=\"abrirLightbox(this.src, 'Teil in Vorrichtung')\" style='cursor:pointer; width:100%; height:500px; object-fit:fill;' src='data:image/jpeg;base64,{0}' alt='Teil im Schraubstock montiert'/>", imagemPrincipal));
                html.AppendLine("</div>");
            }

            html.AppendLine("</div>"); // fim pagina-impressao 1

            // ================================================================
            // PAGINA 2: SETUP INSTRUCTIONS (pagina inteira)
            // Layout: campo de instrucoes no topo (largura total, curto),
            // depois as duas imagens (horizontal) empilhadas embaixo.
            // ================================================================
            if (!string.IsNullOrEmpty(imagemPrincipal))
            {
                html.AppendLine("<div class='pagina-impressao'>");
                html.AppendLine("<h2>SETUP INSTRUCTIONS</h2>");

                // --- Campo de instrucoes: topo, largura total, curto ---
                html.AppendLine("<div class='setup-instrucoes-campo-topo'>");
                html.AppendLine("<div class='titulo-tabela-dim'>INSTRUCOES DE SETUP</div>");
                html.AppendLine("<div class='campo-instrucoes campo-instrucoes-topo' contenteditable='true'></div>");
                html.AppendLine("</div>");

                // --- Imagens: uma do lado da outra ---
                html.AppendLine("<div class='setup-instrucoes-imagens-lado-a-lado'>");

                html.AppendLine("<div class='vista-setup vista-principal setup-instrucoes-imagem-horizontal'>");
                html.AppendLine("<div class='vista-titulo'>Konfiguration (Vorrichtung)</div>");
                html.AppendLine(string.Format("<img onclick=\"abrirLightbox(this.src, 'Vista Principal')\" style='cursor:pointer' src='data:image/jpeg;base64,{0}' alt='Isometrische Hauptansicht mit Vorrichtung'/>", imagemPrincipal));
                html.AppendLine("</div>");

                if (!string.IsNullOrEmpty(imagemOrigemWCS))
                {
                    html.AppendLine("<div class='vista-setup vista-principal setup-instrucoes-imagem-horizontal'>");
                    html.AppendLine("<div class='vista-titulo'>Werkstück-Nullpunkt (WCS / G54) - Draufsicht</div>");
                    html.AppendLine(string.Format("<img onclick=\"abrirLightbox(this.src, 'Origem WCS')\" style='cursor:pointer' src='data:image/jpeg;base64,{0}' alt='WCS Werkstück-Nullpunkt'/>", imagemOrigemWCS));
                    html.AppendLine("</div>");
                }

                html.AppendLine("</div>"); // fim setup-instrucoes-imagens-lado-a-lado

                html.AppendLine("</div>"); // fim pagina-impressao 2
            }

            // ================================================================
            // PAGINA 3: TOOL LIST + TOOLPATH OPERATIONS
            // ================================================================
            html.AppendLine("<div class='pagina-impressao'>");

            html.AppendLine("<h2>TOOL LIST</h2>");
            html.AppendLine("<table class='toollist-compacta'>");
            html.AppendLine("<tr><th>T#</th><th>Name</th><th class='num'>Durchmesser</th><th class='num'>Länge</th></tr>");

            foreach (KeyValuePair<string, DadosFerramenta> kv in ferramentas)
            {
                DadosFerramenta f = kv.Value;

                html.AppendLine(string.Format(
                    "<tr><td>{0}</td><td>{1}</td><td class='num'>{2}</td><td class='num'>{3}</td></tr>",
                    f.NumeroFerramenta >= 0 ? f.NumeroFerramenta.ToString() : "-",
                    f.Nome,
                    FormatarMm(f.Diametro),
                    FormatarMm(f.ComprimentoTotal)));
            }

            html.AppendLine("</table>");

            html.AppendLine("<h2>TOOLPATH OPERATIONS</h2>");

            // Divide a lista em 2 metades, renderizadas lado a lado -
            // reduz a altura total pela metade quando ha muitas Operationen.
            int totalOps = OperationenOrdenadas.Count;
            int metade = (totalOps + 1) / 2;
            List<OperacaoOrdenada> primeiraMetade = OperationenOrdenadas.GetRange(0, Math.Min(metade, totalOps));
            List<OperacaoOrdenada> segundaMetade = totalOps > metade
                ? OperationenOrdenadas.GetRange(metade, totalOps - metade)
                : new List<OperacaoOrdenada>();

            html.AppendLine("<div class='oplist-duas-colunas'>");

            html.AppendLine("<div class='oplist-coluna'>");
            html.AppendLine(GerarTabelaOperacoes(primeiraMetade));
            html.AppendLine("</div>");

            if (segundaMetade.Count > 0)
            {
                html.AppendLine("<div class='oplist-coluna'>");
                html.AppendLine(GerarTabelaOperacoes(segundaMetade));
                html.AppendLine("</div>");
            }

            html.AppendLine("</div>");

            html.AppendLine(string.Format(
                "<div class='resumo'>GESAMTSUMME &nbsp;&nbsp;|&nbsp;&nbsp; Schnittzeit: {0} &nbsp;&nbsp;|&nbsp;&nbsp; Gesamtzeit: {1}</div>",
                FormatarTempo(tempoCorteTotalGeral), FormatarTempo(tempoTotalGeral)));

            html.AppendLine("</div>"); // fim pagina-impressao 3

            // ================================================================
            // PAGINA 4: WERKZEUGDETAIL
            // ================================================================
            html.AppendLine("<div class='pagina-impressao'>");
            html.AppendLine("<h2>WERKZEUGDETAIL</h2>");
            html.AppendLine("<div class='vistas-container'>");

            foreach (KeyValuePair<string, DadosFerramenta> kv in ferramentas)
            {
                DadosFerramenta f = kv.Value;
                string OperationenLista = string.Join(", ", f.Operacoes.ToArray());

                html.AppendLine("<div class='ferramenta-detalhada-card'>");

                html.AppendLine("<div class='ferramenta-vertical-wrapper'>");
                html.AppendLine("<div class='rotar-90'>" + GerarIconeSvgFerramenta(f) + "</div>");
                html.AppendLine("</div>");

                html.AppendLine("<table class='tabela-ferramenta-detalhe'>");
                html.AppendLine(string.Format("<tr><td class='td-rotulo-fd' colspan='2'>Operacao: {0}</td></tr>", OperationenLista));
                html.AppendLine(string.Format("<tr><td class='td-rotulo-fd'>Werkzeugnummer:</td><td class='td-valor-fd'>{0}</td></tr>", f.NumeroFerramenta >= 0 ? f.NumeroFerramenta.ToString() : "-"));
                html.AppendLine(string.Format("<tr><td class='td-rotulo-fd'>Werkzeugname:</td><td class='td-valor-fd'>{0}</td></tr>", f.Nome));
                html.AppendLine(string.Format("<tr><td class='td-rotulo-fd'>Werkzeugdurchmesser:</td><td class='td-valor-fd'>{0}</td></tr>", FormatarMm(f.Diametro)));
                html.AppendLine(string.Format("<tr><td class='td-rotulo-fd'>Werkzeug-Freilänge:</td><td class='td-valor-fd'>{0}</td></tr>", FormatarMm(f.ComprimentoCorte)));

                if (temBloco && f.ComprimentoCorte > 0)
                {
                    bool alcancaProfundidade = f.ComprimentoCorte >= blocoH;
                    string corAlerta = alcancaProfundidade ? "#4caf50" : "#e74c3c";
                    string textoAlerta = alcancaProfundidade
                        ? string.Format("OK - erreicht {0:F0}mm Blocktiefe ({1:F0}mm)", blocoH, f.ComprimentoCorte)
                        : string.Format("ACHTUNG - Blocktiefe ({0:F0}mm) größer als die Werkzeugreichweite ({1:F0}mm)", blocoH, f.ComprimentoCorte);

                    html.AppendLine(string.Format(
                        "<tr><td class='td-rotulo-fd'>Mindestauskraglänge (geschätzt):</td><td class='td-valor-fd' style='color:{0};'>{1}</td></tr>",
                        corAlerta, textoAlerta));
                }

                html.AppendLine("</table>");

                html.AppendLine("</div>");
            }

            html.AppendLine("</div>");
            html.AppendLine("</div>"); // fim pagina-impressao 4

            // ================================================================
            // PAGINA 5+: TOOLPATH IMAGES (mostra o IPW - estagio real do
            // material - em vez do toolpath). PAGINADO: agrupa em blocos
            // de 4 imagens por folha, pra nao virar uma pagina gigante
            // quando ha muitas Operationen.
            // ================================================================
            List<OperacaoOrdenada> OperationenComImagemIPW = new List<OperacaoOrdenada>();
            foreach (OperacaoOrdenada op in OperationenOrdenadas)
            {
                if (!string.IsNullOrEmpty(op.ImagemIPW)) OperationenComImagemIPW.Add(op);
            }

            const int imagensPorPagina = 4;
            int totalPaginasImagens = (int)Math.Ceiling(OperationenComImagemIPW.Count / (double)imagensPorPagina);

            for (int p = 0; p < totalPaginasImagens; p++)
            {
                html.AppendLine("<div class='pagina-impressao'>");
                html.AppendLine(string.Format("<h2>TOOLPATH IMAGES{0}</h2>",
                    totalPaginasImagens > 1 ? string.Format(" ({0}/{1})", p + 1, totalPaginasImagens) : ""));
                html.AppendLine("<div class='vistas-container-grande'>");

                int inicio = p * imagensPorPagina;
                int fim = Math.Min(inicio + imagensPorPagina, OperationenComImagemIPW.Count);

                for (int i = inicio; i < fim; i++)
                {
                    OperacaoOrdenada op = OperationenComImagemIPW[i];
                    html.AppendLine("<div class='vista-grande'>");
                    html.AppendLine(string.Format("<div class='vista-titulo'>{0}. {1}</div>", op.Sequencia, op.NomeOperacao));
                    html.AppendLine(string.Format("<img onclick=\"abrirLightbox(this.src, '{1}')\" src='data:image/jpeg;base64,{0}' alt='{1}'/>", op.ImagemIPW, op.NomeOperacao));
                    html.AppendLine("</div>");
                }

                html.AppendLine("</div>");
                html.AppendLine("</div>"); // fim pagina-impressao 5+
            }

            // ------------------------------------------------------------
            // Lightbox: clica na miniatura -> abre grande sobre a tela;
            // clica de novo em qualquer lugar -> fecha. JS puro, sem
            // dependencia externa, mantendo o HTML autocontido.
            // ------------------------------------------------------------
            html.AppendLine("<div id='lightbox-overlay' onclick=\"this.style.display='none'\">");
            html.AppendLine("<img id='lightbox-img' src=''/>");
            html.AppendLine("<div id='lightbox-titulo'></div>");
            html.AppendLine("</div>");
            html.AppendLine("<script>");
            html.AppendLine("function abrirLightbox(src, titulo) {");
            html.AppendLine("  document.getElementById('lightbox-img').src = src;");
            html.AppendLine("  document.getElementById('lightbox-titulo').innerText = titulo;");
            html.AppendLine("  document.getElementById('lightbox-overlay').style.display = 'flex';");
            html.AppendLine("}");
            html.AppendLine("</script>");

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            // ----------------------------------------------------------------
            // 3) Salva e abre
            // ----------------------------------------------------------------
            string pastaBase = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string pastaSaida = System.IO.Path.Combine(pastaBase, "ShopDocumentation");

            if (!Directory.Exists(pastaSaida))
            {
                Directory.CreateDirectory(pastaSaida);
            }

            string nomeArquivo = string.Format("{0}_ToolList_{1}.html", nomePeca, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            string caminhoCompleto = System.IO.Path.Combine(pastaSaida, nomeArquivo);

            File.WriteAllText(caminhoCompleto, html.ToString(), Encoding.UTF8);

            theSession.ListingWindow.WriteLine(
                string.Format("=== HTML gerado com sucesso: {0} ===", caminhoCompleto));

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(caminhoCompleto) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                theSession.ListingWindow.WriteLine(
                    string.Format("(Nao foi possivel abrir automaticamente: {0})", ex.Message));
            }
        }

        // ----------------------------------------------------------------
        // Icones pequenos (pictogramas originais, estilo proprio) pra cada
        // tipo de parametro - mostrados ao lado do rotulo na tabela.
        // ----------------------------------------------------------------
        private static string IconePequeno(string tipo)
        {
            string cor = COR_DESTAQUE;
            switch (tipo)
            {
                case "D":
                    return "<svg width='22' height='22' viewBox='0 0 28 28'><circle cx='14' cy='14' r='9' fill='none' stroke='" + cor + "' stroke-width='2'/><line x1='6' y1='22' x2='22' y2='6' stroke='" + cor + "' stroke-width='2'/></svg>";
                case "R1":
                    return "<svg width='22' height='22' viewBox='0 0 28 28'><path d='M8 20 Q8 24 12 24 L18 24' fill='none' stroke='" + cor + "' stroke-width='2.5'/><path d='M8 20 L8 8' fill='none' stroke='" + cor + "' stroke-width='2.5'/></svg>";
                case "L":
                    return "<svg width='22' height='22' viewBox='0 0 28 28'><rect x='10' y='3' width='8' height='16' fill='" + cor + "'/><path d='M10 19 L14 25 L18 19 Z' fill='#999'/></svg>";
                case "FL":
                    return "<svg width='22' height='22' viewBox='0 0 28 28'><rect x='10' y='3' width='8' height='10' fill='#999'/><path d='M10 13 L14 23 L18 13 Z' fill='" + cor + "'/></svg>";
                case "#FL":
                    return "<svg width='22' height='22' viewBox='0 0 28 28'><line x1='9' y1='4' x2='6' y2='24' stroke='" + cor + "' stroke-width='2'/><line x1='14' y1='4' x2='13' y2='24' stroke='" + cor + "' stroke-width='2'/><line x1='19' y1='4' x2='20' y2='24' stroke='" + cor + "' stroke-width='2'/></svg>";
                case "T#":
                    return "<svg width='22' height='22' viewBox='0 0 28 28'><rect x='4' y='4' width='20' height='20' rx='2' fill='none' stroke='" + cor + "' stroke-width='2'/><text x='7' y='19' fill='" + cor + "' font-size='13' font-weight='bold' font-family='Arial'>T#</text></svg>";
                case "T":
                case "Tc":
                    return "<svg width='22' height='22' viewBox='0 0 28 28'><circle cx='14' cy='14' r='10' fill='none' stroke='" + cor + "' stroke-width='2'/><line x1='14' y1='14' x2='14' y2='7' stroke='" + cor + "' stroke-width='2'/><line x1='14' y1='14' x2='19' y2='16' stroke='" + cor + "' stroke-width='2'/></svg>";
                case "Nº Op.":
                    return "<svg width='22' height='22' viewBox='0 0 28 28'><rect x='4' y='3' width='20' height='22' rx='1' fill='none' stroke='#666' stroke-width='1.2'/><line x1='7' y1='8' x2='21' y2='8' stroke='" + cor + "' stroke-width='1.5'/><line x1='7' y1='13' x2='21' y2='13' stroke='" + cor + "' stroke-width='1.5'/><line x1='7' y1='18' x2='16' y2='18' stroke='" + cor + "' stroke-width='1.5'/></svg>";
                default:
                    return "";
            }
        }

        // Gera uma linha de tabela com rotulo fixo + campo editavel
        // (contenteditable) pro usuario digitar direto no navegador -
        // usado pros dados de empresa/programador e campos de
        // preenchimento (Ordem de Servico, Data, etc).
        private static string LinhaInfoEditavel(string rotulo, string valorInicial)
        {
            return string.Format(
                "<tr><td class='td-rotulo-info'>{0}</td><td class='td-valor-info' contenteditable='true'>{1}</td></tr>",
                rotulo, valorInicial);
        }

        // Gera uma linha de tabela de dimensao (Comprimento/Largura/
        // Altura) com eixo (X/Y/Z) e valor editavel, ja preenchido com o
        // dado real extraido da peca/bloco.
        private static string LinhaDimEditavel(string rotulo, string eixo, string valor, string classeExtra)
        {
            return string.Format(
                "<tr><td class='td-rotulo-dim'>{0} <span class='eixo-dim'>({1})</span></td><td class='td-valor-dim {3}' contenteditable='true'>{2}</td></tr>",
                rotulo, eixo, valor, classeExtra);
        }

        private static string LinhaDado(string label, string valor)
        {
            return string.Format(
                "<div class='linha'><span class='rotulo'>{0} {1}=</span><span>{2}</span></div>",
                IconePequeno(label), label, valor);
        }

        // ----------------------------------------------------------------
        // Captura a view atual do NX como imagem JPG (via UFSession, API
        // de baixo nivel confirmada pela comunidade - nxjournaling.com,
        // "Export Assembly to Excel with pictures"), converte pra base64
        // e retorna a string pronta pra embutir num <img> no HTML.
        //
        // NAO reorienta a view (ao contrario do exemplo original, que
        // forcava Trimetric) - captura exatamente o que esta na tela,
        // incluindo a fixacao/setup como o usuario configurou.
        //
        // Se falhar por qualquer motivo, retorna string vazia e o
        // chamador simplesmente pula a secao de imagem (nao quebra o
        // resto do relatorio).
        // ----------------------------------------------------------------
        // ----------------------------------------------------------------
        // Extrai as dimensoes do BLOCO/MATERIA-PRIMA (Blank) via a API
        // confirmada por journal real: o bloco e definido de forma
        // PARAMETRICA (nao e um corpo separado nesse tipo de setup),
        // entao lemos direto BlockLength/BlockWidth/BlockHeight do
        // MillGeomBuilder do WORKPIECE.
        // ----------------------------------------------------------------
        private static bool ObterDimensoesBlank(
            Session theSession, Part workPart, out double comprimento, out double largura, out double altura)
        {
            comprimento = 0; largura = 0; altura = 0;

            try
            {
                NXOpen.CAM.FeatureGeometry workpiece =
                    (NXOpen.CAM.FeatureGeometry)workPart.CAMSetup.CAMGroupCollection.FindObject("WORKPIECE");

                NXOpen.CAM.MillGeomBuilder millGeomBuilder =
                    workPart.CAMSetup.CAMGroupCollection.CreateMillGeomBuilder(workpiece);

                try
                {
                    comprimento = millGeomBuilder.BlankGeometry.BlockLength;
                    largura = millGeomBuilder.BlankGeometry.BlockWidth;
                    altura = millGeomBuilder.BlankGeometry.BlockHeight;
                }
                finally
                {
                    millGeomBuilder.Destroy();
                }

                return comprimento > 0 && largura > 0 && altura > 0;
            }
            catch (Exception ex)
            {
                theSession.ListingWindow.WriteLine(
                    string.Format("  [AVISO] Nao foi possivel ler dimensoes do bloco (Blank): {0}", ex.Message));
                return false;
            }
        }

        // ----------------------------------------------------------------
        // Identifica o corpo da PECA FINAL: calcula a bounding box de
        // cada corpo do part, e retorna as dimensoes do corpo cujas
        // dimensoes NAO batem com as do bloco (Blank) - ou seja, o corpo
        // "menor"/diferente e a peca usinada, nao a materia-prima.
        // ----------------------------------------------------------------
        private static bool ObterDimensoesPeca(
            Part workPart, double blocoComprimento, double blocoLargura, double blocoAltura,
            out double comprimento, out double largura, out double altura, out Body corpoPeca)
        {
            comprimento = 0; largura = 0; altura = 0; corpoPeca = null;
            const double tolerancia = 0.5;

            foreach (Body body in workPart.Bodies)
            {
                double[] bMin = new double[] { double.MaxValue, double.MaxValue, double.MaxValue };
                double[] bMax = new double[] { double.MinValue, double.MinValue, double.MinValue };

                foreach (Face face in body.GetFaces())
                {
                    foreach (Edge edge in face.GetEdges())
                    {
                        Point3d v1, v2;
                        edge.GetVertices(out v1, out v2);

                        double[][] pontos = new double[][] { new double[] { v1.X, v1.Y, v1.Z }, new double[] { v2.X, v2.Y, v2.Z } };
                        foreach (double[] p in pontos)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                if (p[i] < bMin[i]) bMin[i] = p[i];
                                if (p[i] > bMax[i]) bMax[i] = p[i];
                            }
                        }
                    }
                }

                double dx = bMax[0] - bMin[0];
                double dy = bMax[1] - bMin[1];
                double dz = bMax[2] - bMin[2];

                // Ordena as 3 dimensoes de cada corpo pra comparar sem
                // depender de qual eixo corresponde a qual (L/W/H podem
                // estar em eixos diferentes dependendo da orientacao)
                double[] dimsCorpo = new double[] { dx, dy, dz };
                Array.Sort(dimsCorpo);
                double[] dimsBloco = new double[] { blocoComprimento, blocoLargura, blocoAltura };
                Array.Sort(dimsBloco);

                bool bateComBloco =
                    Math.Abs(dimsCorpo[0] - dimsBloco[0]) < tolerancia &&
                    Math.Abs(dimsCorpo[1] - dimsBloco[1]) < tolerancia &&
                    Math.Abs(dimsCorpo[2] - dimsBloco[2]) < tolerancia;

                if (!bateComBloco)
                {
                    comprimento = dx; largura = dy; altura = dz; corpoPeca = body;
                    return true;
                }
            }

            return false;
        }

        // ----------------------------------------------------------------
        // Gera um diagrama SVG simples (bloco em perspectiva isometrica
        // basica) com cotas de Comprimento, Largura e Altura no mesmo
        // estilo de desenho tecnico ja usado no icone da ferramenta
        // (linhas de extensao + setas + texto).
        // ----------------------------------------------------------------
        private static string GerarDiagramaBloco(double comprimento, double largura, double altura, string idBloco)
        {
            double canvasW = 320, canvasH = 220;
            string gradId = "blk_" + idBloco;

            // Projecao isometrica simplificada (2:1) de um bloco
            double escala = 130.0 / Math.Max(comprimento, Math.Max(largura, altura));
            double L = comprimento * escala;
            double W = largura * escala;
            double H = altura * escala;

            // Vetores isometricos simplificados
            double xIx = 0.87, xIy = 0.5;   // eixo X vai pra direita-cima
            double yIx = -0.87, yIy = 0.5;  // eixo Y vai pra esquerda-cima
            double zIx = 0, zIy = -1;       // eixo Z vai pra cima

            // Calcula a extensao real do desenho (sem deslocamento ainda),
            // pra poder centralizar de verdade dentro do canvas, reservando
            // margem pras cotas (esquerda pra H, embaixo pra L).
            double[] xs = new double[8];
            double[] ys = new double[8];
            double[][] cantos = new double[][] {
                new double[]{0,0,0}, new double[]{L,0,0}, new double[]{0,W,0}, new double[]{L,W,0},
                new double[]{0,0,H}, new double[]{L,0,H}, new double[]{0,W,H}, new double[]{L,W,H}
            };
            for (int i = 0; i < 8; i++)
            {
                xs[i] = cantos[i][0] * xIx + cantos[i][1] * yIx + cantos[i][2] * zIx;
                ys[i] = cantos[i][0] * xIy + cantos[i][1] * yIy + cantos[i][2] * zIy;
            }

            double margemEsq = 50, margemDir = 20, margemTopo = 15, margemBaixo = 40;
            double areaUtilW = canvasW - margemEsq - margemDir;
            double areaUtilH = canvasH - margemTopo - margemBaixo;

            double minX = xs[0], maxX = xs[0], minY = ys[0], maxY = ys[0];
            for (int i = 1; i < 8; i++)
            {
                if (xs[i] < minX) minX = xs[i];
                if (xs[i] > maxX) maxX = xs[i];
                if (ys[i] < minY) minY = ys[i];
                if (ys[i] > maxY) maxY = ys[i];
            }

            double ox = margemEsq + (areaUtilW - (maxX - minX)) / 2.0 - minX;
            double oy = margemTopo + (areaUtilH - (maxY - minY)) / 2.0 - minY;

            Func<double, double, double, double[]> proj = (x, y, z) => new double[] {
                ox + x * xIx + y * yIx + z * zIx,
                oy + x * xIy + y * yIy + z * zIy
            };

            double[] p000 = proj(0, 0, 0);
            double[] p100 = proj(L, 0, 0);
            double[] p010 = proj(0, W, 0);
            double[] p110 = proj(L, W, 0);
            double[] p001 = proj(0, 0, H);
            double[] p101 = proj(L, 0, H);
            double[] p011 = proj(0, W, H);
            double[] p111 = proj(L, W, H);

            StringBuilder svg = new StringBuilder();
            svg.Append(string.Format("<svg width='100%' height='100%' viewBox='0 0 {0} {1}' preserveAspectRatio='xMidYMid meet' xmlns='http://www.w3.org/2000/svg'>", canvasW, canvasH));

            // Gradientes metalicos: cores diferentes por tipo de bloco pra
            // diferenciar visualmente Materia Prima (aco azulado) de Peca
            // (bronze/cobre) - tambem mais saturados que antes, pra nao
            // "lavar" contra o fundo claro do card.
            bool ehPeca = idBloco == "peca";

            string corTopo1, corTopo2, corTopo3, corTopo4, corTopo5;
            string corFrente1, corFrente2, corFrente3, corFrente4, corFrente5;
            string corLado1, corLado2, corLado3, corLado4;

            if (ehPeca)
            {
                corTopo1 = "#f0c896"; corTopo2 = "#c9944f"; corTopo3 = "#f5d9ad"; corTopo4 = "#a8763a"; corTopo5 = "#7d5726";
                corFrente1 = "#b8863f"; corFrente2 = "#dba85f"; corFrente3 = "#96692c"; corFrente4 = "#c99a5b"; corFrente5 = "#6b4a1f";
                corLado1 = "#5c3f1a"; corLado2 = "#82602f"; corLado3 = "#472f13"; corLado4 = "#6b4a24";
            }
            else
            {
                corTopo1 = "#bcd9f0"; corTopo2 = "#7fa8c9"; corTopo3 = "#d4e8f7"; corTopo4 = "#5c85a8"; corTopo5 = "#3d5f7d";
                corFrente1 = "#6f9bbd"; corFrente2 = "#98bdd9"; corFrente3 = "#547891"; corFrente4 = "#82a9c7"; corFrente5 = "#3d5a70";
                corLado1 = "#2e4759"; corLado2 = "#4a6b82"; corLado3 = "#233642"; corLado4 = "#395364";
            }

            svg.Append("<defs>");
            svg.Append(string.Format(
                "<linearGradient id='{0}_topo' x1='0' y1='0' x2='1' y2='1'>" +
                "<stop offset='0%' stop-color='{1}'/><stop offset='30%' stop-color='{2}'/>" +
                "<stop offset='55%' stop-color='{3}'/><stop offset='80%' stop-color='{4}'/>" +
                "<stop offset='100%' stop-color='{5}'/></linearGradient>", gradId, corTopo1, corTopo2, corTopo3, corTopo4, corTopo5));
            svg.Append(string.Format(
                "<linearGradient id='{0}_frente' x1='0' y1='0' x2='0' y2='1'>" +
                "<stop offset='0%' stop-color='{1}'/><stop offset='25%' stop-color='{2}'/>" +
                "<stop offset='50%' stop-color='{3}'/><stop offset='75%' stop-color='{4}'/>" +
                "<stop offset='100%' stop-color='{5}'/></linearGradient>", gradId, corFrente1, corFrente2, corFrente3, corFrente4, corFrente5));
            svg.Append(string.Format(
                "<linearGradient id='{0}_lado' x1='0' y1='0' x2='1' y2='0'>" +
                "<stop offset='0%' stop-color='{1}'/><stop offset='35%' stop-color='{2}'/>" +
                "<stop offset='65%' stop-color='{3}'/><stop offset='100%' stop-color='{4}'/>" +
                "</linearGradient>", gradId, corLado1, corLado2, corLado3, corLado4));
            svg.Append("</defs>");

            Action<double[], double[]> linha = (a, b) => svg.Append(string.Format(
                "<line x1='{0:F1}' y1='{1:F1}' x2='{2:F1}' y2='{3:F1}' stroke='#222' stroke-width='0.8'/>", a[0], a[1], b[0], b[1]));

            // Face de cima (visivel)
            svg.Append(string.Format("<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1} {6:F1},{7:F1}' fill='url(#{8}_topo)' stroke='#222' stroke-width='0.8'/>",
                p001[0], p001[1], p101[0], p101[1], p111[0], p111[1], p011[0], p011[1], gradId));

            // Face frontal (direita)
            svg.Append(string.Format("<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1} {6:F1},{7:F1}' fill='url(#{8}_frente)' stroke='#222' stroke-width='0.8'/>",
                p100[0], p100[1], p110[0], p110[1], p111[0], p111[1], p101[0], p101[1], gradId));

            // Face lateral (esquerda)
            svg.Append(string.Format("<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1} {6:F1},{7:F1}' fill='url(#{8}_lado)' stroke='#222' stroke-width='0.8'/>",
                p000[0], p000[1], p010[0], p010[1], p011[0], p011[1], p001[0], p001[1], gradId));

            // Arestas visiveis restantes
            linha(p000, p100); linha(p010, p110); linha(p100, p110);
            linha(p101, p111); linha(p011, p111);

            // Cota Comprimento (L) - ao longo da aresta frontal-inferior,
            // com fundo branco atras pra garantir legibilidade
            double[] meioL = proj(L / 2, 0, 0);
            svg.Append(string.Format("<rect x='{0:F1}' y='{1:F1}' width='58' height='16' rx='3' fill='#fff' stroke='#ccc' stroke-width='0.5'/>",
                meioL[0] - 29, meioL[1] + 6));
            svg.Append(string.Format("<text x='{0:F1}' y='{1:F1}' fill='#1a5276' font-size='12' font-family='Arial' font-weight='bold' text-anchor='middle'>L: {2:F0}mm</text>",
                meioL[0], meioL[1] + 17, comprimento));

            // Cota Largura (W)
            double[] meioW = proj(0, W / 2, 0);
            svg.Append(string.Format("<rect x='{0:F1}' y='{1:F1}' width='58' height='16' rx='3' fill='#fff' stroke='#ccc' stroke-width='0.5'/>",
                meioW[0] - 47, meioW[1] - 2));
            svg.Append(string.Format("<text x='{0:F1}' y='{1:F1}' fill='#1a5276' font-size='12' font-family='Arial' font-weight='bold' text-anchor='middle'>W: {2:F0}mm</text>",
                meioW[0] - 18, meioW[1] + 10, largura));

            // Cota Altura (H)
            double[] meioH = proj(0, 0, H / 2);
            svg.Append(string.Format("<rect x='{0:F1}' y='{1:F1}' width='58' height='16' rx='3' fill='#fff' stroke='#ccc' stroke-width='0.5'/>",
                p000[0] + 4, meioH[1] - 8));
            svg.Append(string.Format("<text x='{0:F1}' y='{1:F1}' fill='#1a5276' font-size='12' font-family='Arial' font-weight='bold' text-anchor='start'>H: {2:F0}mm</text>",
                p000[0] + 8, meioH[1] + 4, altura));

            svg.Append("</svg>");
            return svg.ToString();
        }

        // ----------------------------------------------------------------
        // Captura uma imagem mostrando SO o corpo indicado: esconde todos
        // os outros corpos do part, orienta pra isometrica, tira a foto,
        // e restaura a visibilidade de todos os corpos no final (usa
        // DisplayManager.BlankObjects/UnblankObjects, API padrao NXOpen).
        // ----------------------------------------------------------------
        private static string CapturarCorpoIsoladoBase64(Session theSession, Part workPart, Body corpoAlvo)
        {
            List<DisplayableObject> corposParaEsconder = new List<DisplayableObject>();

            // Todos os outros corpos do part (bloco, etc). Nota: nao
            // escondemos componentes de montagem aqui (tentamos antes,
            // mas quebrou a captura - provavelmente a peca/bloco estao
            // DENTRO de componentes, entao esconder todos os componentes
            // escondia tudo, deixando a imagem em branco).
            foreach (Body b in workPart.Bodies)
            {
                if (b.Tag != corpoAlvo.Tag)
                {
                    corposParaEsconder.Add(b);
                }
            }

            string resultado = "";

            try
            {
                if (corposParaEsconder.Count > 0)
                {
                    theSession.DisplayManager.BlankObjects(corposParaEsconder.ToArray());
                }

                workPart.ModelingViews.WorkView.Orient(View.Canned.Isometric, View.ScaleAdjustment.Fit);
                resultado = CapturarViewAtualBase64(theSession, workPart);
            }
            catch (Exception ex)
            {
                theSession.ListingWindow.WriteLine(
                    string.Format("  [AVISO] Nao foi possivel capturar corpo isolado: {0}", ex.Message));
            }
            finally
            {
                try
                {
                    if (corposParaEsconder.Count > 0)
                    {
                        theSession.DisplayManager.UnblankObjects(corposParaEsconder.ToArray());
                    }
                }
                catch { }
            }

            return resultado;
        }

        // Percorre recursivamente os componentes filhos de uma montagem,
        // adicionando cada um na lista de objetos a esconder (a morsa/
        // fixacao geralmente e um componente carregado, nao um Body
        // simples do part principal).
        private static void AdicionarComponentesRecursivo(NXOpen.Assemblies.Component componente, List<DisplayableObject> lista)
        {
            foreach (NXOpen.Assemblies.Component filho in componente.GetChildren())
            {
                lista.Add(filho);
                AdicionarComponentesRecursivo(filho, lista);
            }
        }

        // ----------------------------------------------------------------
        // Gera um diagrama 2D simples (retangulo plano) com cotas nos
        // dois lados - usado pras vistas de Topo e Lateral do bloco.
        // ----------------------------------------------------------------
        private static string GerarDiagrama2D(double largura, double altura, string rotuloLargura, string rotuloAltura, string cor)
        {
            double canvasW = 260, canvasH = 180;
            double margem = 40;

            double areaW = canvasW - margem * 2;
            double areaH = canvasH - margem * 2;
            double escala = Math.Min(areaW / largura, areaH / altura);

            double retW = largura * escala;
            double retH = altura * escala;

            double x0 = (canvasW - retW) / 2.0;
            double y0 = (canvasH - retH) / 2.0 - 5;

            StringBuilder svg = new StringBuilder();
            svg.Append(string.Format("<svg width='100%' height='100%' viewBox='0 0 {0} {1}' preserveAspectRatio='xMidYMid meet' xmlns='http://www.w3.org/2000/svg'>", canvasW, canvasH));

            svg.Append(string.Format("<rect x='{0:F1}' y='{1:F1}' width='{2:F1}' height='{3:F1}' fill='{4}' opacity='0.75' stroke='#222' stroke-width='1'/>",
                x0, y0, retW, retH, cor));

            // Cota horizontal (embaixo)
            double yCota = y0 + retH + 22;
            svg.Append(string.Format("<line x1='{0:F1}' y1='{1:F1}' x2='{0:F1}' y2='{2:F1}' stroke='#888' stroke-width='0.6'/>", x0, y0 + retH + 4, yCota));
            svg.Append(string.Format("<line x1='{0:F1}' y1='{1:F1}' x2='{0:F1}' y2='{2:F1}' stroke='#888' stroke-width='0.6'/>", x0 + retW, y0 + retH + 4, yCota));
            svg.Append(string.Format("<line x1='{0:F1}' y1='{1:F1}' x2='{2:F1}' y2='{1:F1}' stroke='#333' stroke-width='0.8'/>", x0, yCota, x0 + retW));
            svg.Append(string.Format("<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {2:F1},{4:F1}' fill='#333'/>", x0, yCota, x0 + 6, yCota - 2.5, yCota + 2.5));
            svg.Append(string.Format("<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {2:F1},{4:F1}' fill='#333'/>", x0 + retW, yCota, x0 + retW - 6, yCota - 2.5, yCota + 2.5));
            svg.Append(string.Format("<text x='{0:F1}' y='{1:F1}' fill='#000' font-size='12' font-weight='bold' font-family='Arial' text-anchor='middle'>{2:F0}mm ({3})</text>",
                x0 + retW / 2.0, yCota + 15, largura, rotuloLargura));

            // Cota vertical (esquerda)
            double xCota = x0 - 22;
            svg.Append(string.Format("<line x1='{0:F1}' y1='{1:F1}' x2='{2:F1}' y2='{1:F1}' stroke='#888' stroke-width='0.6'/>", x0 - 4, y0, xCota));
            svg.Append(string.Format("<line x1='{0:F1}' y1='{1:F1}' x2='{2:F1}' y2='{1:F1}' stroke='#888' stroke-width='0.6'/>", x0 - 4, y0 + retH, xCota));
            svg.Append(string.Format("<line x1='{0:F1}' y1='{1:F1}' x2='{0:F1}' y2='{2:F1}' stroke='#333' stroke-width='0.8'/>", xCota, y0, y0 + retH));
            svg.Append(string.Format("<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{3:F1}' fill='#333'/>", xCota, y0, xCota - 2.5, y0 + 6, xCota + 2.5));
            svg.Append(string.Format("<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{3:F1}' fill='#333'/>", xCota, y0 + retH, xCota - 2.5, y0 + retH - 6, xCota + 2.5));
            svg.Append(string.Format("<text x='{0:F1}' y='{1:F1}' fill='#000' font-size='12' font-weight='bold' font-family='Arial' text-anchor='middle' transform='rotate(-90 {0:F1} {1:F1})'>{2:F0}mm ({3})</text>",
                xCota - 15, y0 + retH / 2.0, altura, rotuloAltura));

            svg.Append("</svg>");
            return svg.ToString();
        }

        // ----------------------------------------------------------------
        // Simbolo universal de origem/zero de trabalho: circulo dividido
        // ao meio (preto/branco) com linhas de mira (crosshair) alem da
        // borda - padrao classico de desenho tecnico/usinagem pra marcar
        // origem/centro de referencia. Como nao temos como calcular a
        // posicao EXATA em pixel da origem real na foto (exigiria a
        // matriz de projecao da camera do NX, que nao temos acesso), o
        // simbolo e posicionado como referencia central da imagem, com
        // legenda deixando isso claro.
        // ----------------------------------------------------------------
        private static string GerarSimboloOrigem()
        {
            StringBuilder svg = new StringBuilder();
            svg.Append("<svg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'>");

            // Linhas de mira (crosshair) alem da borda do circulo
            svg.Append("<line x1='30' y1='2' x2='30' y2='14' stroke='#fff' stroke-width='2'/>");
            svg.Append("<line x1='30' y1='46' x2='30' y2='58' stroke='#fff' stroke-width='2'/>");
            svg.Append("<line x1='2' y1='30' x2='14' y2='30' stroke='#fff' stroke-width='2'/>");
            svg.Append("<line x1='46' y1='30' x2='58' y2='30' stroke='#fff' stroke-width='2'/>");

            // Circulo dividido ao meio (metade preta, metade branca) -
            // simbolo classico de origem
            svg.Append("<path d='M 30 14 A 16 16 0 0 1 30 46 Z' fill='#000' stroke='#fff' stroke-width='1.5'/>");
            svg.Append("<path d='M 30 14 A 16 16 0 0 0 30 46 Z' fill='#fff' stroke='#000' stroke-width='1.5'/>");
            svg.Append("<circle cx='30' cy='30' r='16' fill='none' stroke='#ff3b30' stroke-width='1.5'/>");
            svg.Append("<circle cx='30' cy='30' r='1.8' fill='#ff3b30'/>");

            svg.Append("</svg>");
            return svg.ToString();
        }

        private static string CapturarViewAtualBase64(Session theSession, Part workPart)
        {
            string caminhoTemp = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "shopdoc_view_" + Guid.NewGuid().ToString("N") + ".jpg");

            try
            {
                UFSession ufs = UFSession.GetUFSession();
                ufs.Disp.CreateImage(caminhoTemp, UFDisp.ImageFormat.Jpeg, UFDisp.BackgroundColor.White);

                if (!File.Exists(caminhoTemp))
                {
                    return "";
                }

                byte[] bytes = File.ReadAllBytes(caminhoTemp);
                return Convert.ToBase64String(bytes);
            }
            catch (Exception ex)
            {
                theSession.ListingWindow.WriteLine(
                    string.Format("  [AVISO] Nao foi possivel capturar a vista atual: {0}", ex.Message));
                return "";
            }
            finally
            {
                try { if (File.Exists(caminhoTemp)) File.Delete(caminhoTemp); } catch { }
            }
        }

        // Gera uma tabela de Toolpath Operations pra uma lista (usada 2x
        // - uma pra cada metade - quando dividimos em 2 colunas).
        private static string GerarTabelaOperacoes(List<OperacaoOrdenada> lista)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<table class='oplist'>");
            sb.AppendLine("<colgroup><col class='col-img'><col class='col-seq'><col class='col-op'><col class='col-ferr'><col class='col-tc'><col class='col-t'></colgroup>");
            sb.AppendLine("<tr><th>Img</th><th>#</th><th>Operation</th><th>Werkzeug</th><th class='num'>Schnittzeit</th><th class='num'>Gesamtzeit</th></tr>");

            foreach (OperacaoOrdenada op in lista)
            {
                string imgTag = string.IsNullOrEmpty(op.ImagemBase64)
                    ? "-"
                    : string.Format("<img class='thumb-op' onclick=\"abrirLightbox(this.src, '{1}')\" src='data:image/jpeg;base64,{0}' alt='{1}'/>", op.ImagemBase64, op.NomeOperacao);

                sb.AppendLine(string.Format(
                    "<tr><td class='td-img'>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td class='num tempo-corte'>{4}</td><td class='num tempo-total'>{5}</td></tr>",
                    imgTag, op.Sequencia, op.NomeOperacao, op.NomeFerramenta,
                    FormatarTempo(op.TempoCorteSegundos), FormatarTempo(op.TempoTotalSegundos)));
            }

            sb.AppendLine("</table>");
            return sb.ToString();
        }

        // ----------------------------------------------------------------
        // Extrai o diametro do NOME da ferramenta via regex - aceita
        // tanto o formato com sufixo "MM" (ex: "DRILL-D12MM") quanto SEM
        // sufixo (ex: "HSS_DRILL_D25", "BORE_BAR_DIAM_40" - convencao
        // real observada nas ferramentas do usuario). Retorna -1 se nao
        // encontrar nenhum padrao reconhecivel.
        // ----------------------------------------------------------------
        // ----------------------------------------------------------------
        // Tenta extrair dados REAIS de ferramentas tipo broca via
        // DrillStdToolBuilder (API confirmada por journal do usuario -
        // TlNumberBuilder confirmado; TlDiameterBuilder/TlHeightBuilder/
        // TlFluteLnBuilder sao tentativas, ja que compartilham o mesmo
        // padrao de nome do MillToolBuilder, mas nao foram confirmados
        // via journal especificamente pra broca). Retorna true se
        // conseguiu pelo menos o diametro.
        // ----------------------------------------------------------------
        // ----------------------------------------------------------------
        // Tenta extrair o NUMERO DA FERRAMENTA (e outros dados, se
        // disponiveis) usando os builders ESPECIFICOS revelados pelas
        // proprias mensagens de erro de cast anteriores (ex:
        // "Unable to cast object of type 'NXOpen.CAM.
        // DrillSpotdrillToolBuilder' to type 'DrillStdToolBuilder'").
        // Isso confirma os nomes reais das classes pra cada tipo de
        // ferramenta de furacao - tentamos cada uma na ordem, ja que o
        // numero da ferramenta e uma propriedade universal (deve
        // existir em todas, dado que TlNumberBuilder ja funcionou tanto
        // em MillToolBuilder quanto em DrillStdToolBuilder).
        // ----------------------------------------------------------------
        private static void TentarExtrairNumeroViaBuildersEspecificos(
            Session theSession, Part workPart, NXOpen.CAM.Tool ferramenta, DadosFerramenta dadosFerr)
        {
            if (dadosFerr.NumeroFerramenta >= 0) return; // ja temos, nao precisa tentar de novo

            // Tentativa: Spotdrill (ex: CENTER_DRILL)
            try
            {
                NXOpen.CAM.DrillSpotdrillToolBuilder b = workPart.CAMSetup.CAMGroupCollection.CreateDrillSpotdrillToolBuilder(ferramenta);
                try { dadosFerr.NumeroFerramenta = (int)b.TlNumberBuilder.Value; } catch { }
                try { if (dadosFerr.Diametro <= 0) dadosFerr.Diametro = b.TlDiameterBuilder.Value; } catch { }
                b.Destroy();
                if (dadosFerr.NumeroFerramenta >= 0)
                {
                    theSession.ListingWindow.WriteLine(string.Format("  [INFO] '{0}': numero extraido via DrillSpotdrillToolBuilder.", dadosFerr.Nome));
                    return;
                }
            }
            catch { }

            // Tentativa: Counterbore (ex: CBORE_24MM)
            try
            {
                NXOpen.CAM.DrillCounterboreToolBuilder b = workPart.CAMSetup.CAMGroupCollection.CreateDrillCounterboreToolBuilder(ferramenta);
                try { dadosFerr.NumeroFerramenta = (int)b.TlNumberBuilder.Value; } catch { }
                try { if (dadosFerr.Diametro <= 0) dadosFerr.Diametro = b.TlDiameterBuilder.Value; } catch { }
                b.Destroy();
                if (dadosFerr.NumeroFerramenta >= 0)
                {
                    theSession.ListingWindow.WriteLine(string.Format("  [INFO] '{0}': numero extraido via DrillCounterboreToolBuilder.", dadosFerr.Nome));
                    return;
                }
            }
            catch { }

            // NOTA: tentativa de "DrillBoringBarToolBuilder" (pra
            // BORE_BAR_DIAM_40) foi removida - esse nome de classe nao
            // existe (era so um palpite baseado no erro de cast
            // "DrillBoringBarTool", sem "Builder" no final - o nome
            // real do builder ainda nao foi confirmado). Precisaria de
            // outro journal pra descobrir o nome certo, se quiser
            // resolver isso depois.
        }

        private static bool TentarExtrairViaDrillStdToolBuilder(
            Session theSession, Part workPart, NXOpen.CAM.Tool ferramenta, DadosFerramenta dadosFerr)
        {
            NXOpen.CAM.DrillStdToolBuilder drillBuilder = null;
            bool conseguiuDiametro = false;

            try
            {
                drillBuilder = workPart.CAMSetup.CAMGroupCollection.CreateDrillStdToolBuilder(ferramenta);

                try { dadosFerr.NumeroFerramenta = (int)drillBuilder.TlNumberBuilder.Value; } catch { }
                try { dadosFerr.Diametro = drillBuilder.TlDiameterBuilder.Value; conseguiuDiametro = dadosFerr.Diametro > 0; } catch { }
                try { dadosFerr.ComprimentoTotal = drillBuilder.TlHeightBuilder.Value; } catch { }
                try { dadosFerr.ComprimentoCorte = drillBuilder.TlFluteLnBuilder.Value; } catch { }
            }
            catch (Exception ex)
            {
                theSession.ListingWindow.WriteLine(
                    string.Format("  [INFO] DrillStdToolBuilder nao disponivel para '{0}': {1}", dadosFerr.Nome, ex.Message));

                // Se nao e uma broca padrao, tenta os builders
                // especificos (Spotdrill/Counterbore/BoringBar) so pra
                // pegar o numero da ferramenta.
                TentarExtrairNumeroViaBuildersEspecificos(theSession, workPart, ferramenta, dadosFerr);
            }
            finally
            {
                if (drillBuilder != null) drillBuilder.Destroy();
            }

            return conseguiuDiametro;
        }

        private static double TentarExtrairDiametroDoNome(string nome)
        {
            // Tentativa 1: padrao com prefixo D/DIAM (ex: "HSS_DRILL_D25",
            // "DRILL-D12MM", "BORE_BAR_DIAM_40")
            Match m = Regex.Match(nome, @"D(?:IAM)?_?(\d+(\.\d+)?)(?:MM)?\b", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            // Tentativa 2: numero no FINAL do nome, sem prefixo D/DIAM
            // (ex: "CBORE_24MM") - ancorado no fim da string ($) pra
            // evitar pegar numeros aleatorios no meio do nome.
            Match m2 = Regex.Match(nome, @"_(\d+(\.\d+)?)(?:MM)?$", RegexOptions.IgnoreCase);
            if (m2.Success)
            {
                return double.Parse(m2.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            return -1;
        }

        private static string FormatarMm(double valor)
        {
            return valor >= 0 ? valor.ToString("F2") : "-";
        }

        private static string FormatarTempo(double segundos)
        {
            TimeSpan ts = TimeSpan.FromSeconds(segundos);
            return string.Format("{0:D2}h {1:D2}m {2:D2}s", (int)ts.TotalHours, ts.Minutes, ts.Seconds);
        }

        // ----------------------------------------------------------------
        // Gera um icone SVG simples de fresa (silhueta lateral), com
        // proporcoes calculadas a partir das dimensoes REAIS da
        // ferramenta.
        // ----------------------------------------------------------------
        private static string GerarIconeSvgFerramenta(DadosFerramenta f)
        {
            // ID unico pros gradientes (evita conflito quando varias
            // ferramentas aparecem na mesma pagina - cada SVG precisa de
            // IDs proprios de gradiente).
            string gradId = "g" + Math.Abs((f.Nome ?? "tool").GetHashCode());

            double diametro = f.Diametro > 0 ? f.Diametro : 10.0;
            double compTotal = f.ComprimentoTotal > 0 ? f.ComprimentoTotal : 60.0;
            double compCorte = f.ComprimentoCorte > 0 ? f.ComprimentoCorte : compTotal * 0.4;
            double raioCanto = f.RaioCanto >= 0 ? f.RaioCanto : 0.0;

            if (compCorte > compTotal) compCorte = compTotal;

            // Canvas HORIZONTAL. Layout (esquerda -> direita) = mapeamento
            // da referencia ISO/ER vertical girada 90°: ponta de corte
            // (equivale ao "fundo" da peca de referencia) -> haste ->
            // PORTA-FERRAMENTA (porca de pinca ER -> corpo com aneis ->
            // flange V -> cone ISO -> pino de tracao, equivale ao "topo").
            double canvasW = 420.0;
            double canvasH = 120.0;
            double margemEsq = 34.0;
            double margemDir = 8.0;
            double margemDimensao = 20.0;
            double margemTopo = 10.0;

            double areaUtilAltura = canvasH - margemDimensao - margemTopo;
            double larguraCanvasUtil = canvasW - margemEsq - margemDir;

            // Se temos secoes REAIS do holder, calcula a escala pelo
            // comprimento FISICO TOTAL (ferramenta + holder real), pra
            // tudo caber proporcionalmente certo no canvas. Sem secoes
            // reais, reserva um espaco fixo generico pro holder (fallback).
            double comprimentoHolderReal = 0.0;
            foreach (SecaoHolder s in f.SecoesHolder) comprimentoHolderReal += s.Comprimento;

            double larguraHolder; // espaco (em px) reservado pro porta-ferramenta
            double escala;

            if (f.SecoesHolder.Count > 0 && comprimentoHolderReal > 0)
            {
                double comprimentoFisicoTotal = compTotal + comprimentoHolderReal;
                escala = larguraCanvasUtil / comprimentoFisicoTotal;
                larguraHolder = comprimentoHolderReal * escala;
            }
            else
            {
                larguraHolder = 170.0;
                escala = (larguraCanvasUtil - larguraHolder) / compTotal;
            }

            double alturaFerramentaPx = diametro * escala;
            if (alturaFerramentaPx > areaUtilAltura)
            {
                alturaFerramentaPx = areaUtilAltura;
            }

            double compCortePx = compCorte * escala;
            // A haste "entra" um pouco na porca de pinca (sobreposicao
            // visual), entao encurtamos um pouco o trecho visivel da
            // haste antes do holder comecar.
            double sobreposicaoNaPorca = 10.0;
            double compHastePx = Math.Max(0.0, (compTotal - compCorte) * escala - sobreposicaoNaPorca);

            double raioCantoPx = raioCanto * escala;

            double centroY = margemTopo + areaUtilAltura / 2.0;

            double inicioCorteX = margemEsq;
            double fimCorteX = inicioCorteX + compCortePx;

            double metadeAltura = Math.Max(alturaFerramentaPx / 2.0, 3.0);

            StringBuilder svg = new StringBuilder();
            svg.Append(string.Format(
                "<svg width='100%' height='100%' viewBox='0 0 {0} {1}' preserveAspectRatio='xMidYMid meet' xmlns='http://www.w3.org/2000/svg'>",
                canvasW, canvasH));

            // ------------------------------------------------------------
            // Gradientes metalicos (efeito cilindrico/3D): claro-escuro-
            // claro na perpendicular ao eixo da ferramenta, simulando o
            // brilho de uma superficie cilindrica de metal.
            // ------------------------------------------------------------
            svg.Append("<defs>");
            svg.Append(string.Format(
                "<linearGradient id='{0}_ouro' x1='0' y1='0' x2='0' y2='1'>" +
                "<stop offset='0%' stop-color='#8a6d1a'/><stop offset='20%' stop-color='#f5d876'/>" +
                "<stop offset='45%' stop-color='#d4af37'/><stop offset='60%' stop-color='#fff3c4'/>" +
                "<stop offset='80%' stop-color='#b8901f'/><stop offset='100%' stop-color='#6b5313'/>" +
                "</linearGradient>", gradId));
            svg.Append(string.Format(
                "<linearGradient id='{0}_aco' x1='0' y1='0' x2='0' y2='1'>" +
                "<stop offset='0%' stop-color='#5a5a5a'/><stop offset='20%' stop-color='#d8d8d8'/>" +
                "<stop offset='45%' stop-color='#999999'/><stop offset='60%' stop-color='#f0f0f0'/>" +
                "<stop offset='80%' stop-color='#7a7a7a'/><stop offset='100%' stop-color='#3a3a3a'/>" +
                "</linearGradient>", gradId));
            svg.Append(string.Format(
                "<linearGradient id='{0}_marrom' x1='0' y1='0' x2='0' y2='1'>" +
                "<stop offset='0%' stop-color='#3d2817'/><stop offset='25%' stop-color='#a97b45'/>" +
                "<stop offset='50%' stop-color='#6b4423'/><stop offset='70%' stop-color='#c99a5b'/>" +
                "<stop offset='100%' stop-color='#2a1a0d'/>" +
                "</linearGradient>", gradId));
            svg.Append(string.Format(
                "<linearGradient id='{0}_marromEscuro' x1='0' y1='0' x2='0' y2='1'>" +
                "<stop offset='0%' stop-color='#1a1008'/><stop offset='35%' stop-color='#5a3d20'/>" +
                "<stop offset='60%' stop-color='#3d2817'/><stop offset='100%' stop-color='#140d05'/>" +
                "</linearGradient>", gradId));
            svg.Append("</defs>");

            // ------------------------------------------------------------
            // PORTA-FERRAMENTA: usa as SECOES REAIS extraidas via
            // HolderSectionBuilder (API confirmada) quando disponiveis -
            // desenha cada secao como um trapezio (diametro inferior ->
            // diametro superior) na escala real, uma apos a outra. Se nao
            // tiver secoes (ex: ferramenta sem holder definido), cai no
            // desenho generico de reserva.
            // ------------------------------------------------------------
            double xHolderIni = fimCorteX + (compTotal - compCorte) * escala - sobreposicaoNaPorca;

            if (f.SecoesHolder.Count > 0)
            {
                double xAtualHolder = xHolderIni;

                foreach (SecaoHolder secao in f.SecoesHolder)
                {
                    double compSecaoPx = secao.Comprimento * escala;
                    double metadeInf = (secao.DiametroInferior / 2.0) * escala;
                    double metadeSup = (secao.DiametroSuperior / 2.0) * escala;

                    // Garante um tamanho minimo visivel mesmo em escalas
                    // pequenas
                    if (metadeInf < 2) metadeInf = 2;
                    if (metadeSup < 2) metadeSup = 2;
                    if (compSecaoPx < 3) compSecaoPx = 3;

                    double xFimSecao = xAtualHolder + compSecaoPx;

                    svg.Append(string.Format(
                        "<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {2:F1},{4:F1} {0:F1},{5:F1}' fill='url(#" + gradId + "_marrom)' stroke='#4a2f18' stroke-width='0.5'/>",
                        xAtualHolder, centroY - metadeInf,
                        xFimSecao, centroY - metadeSup,
                        centroY + metadeSup,
                        centroY + metadeInf));

                    // Linha divisoria entre secoes
                    svg.Append(string.Format(
                        "<line x1='{0:F1}' y1='{1:F1}' x2='{0:F1}' y2='{2:F1}' stroke='#111' stroke-width='0.5'/>",
                        xAtualHolder, centroY - metadeInf, centroY + metadeInf));

                    xAtualHolder = xFimSecao;
                }
            }
            else
            {
                // Fallback: desenho generico (usado quando a ferramenta
                // nao tem secoes de holder definidas)
                double wPorca = larguraHolder * 0.22;
                double wCorpo = larguraHolder * 0.20;
                double wFlange = larguraHolder * 0.14;
                double wCone = larguraHolder * 0.32;
                double wPino = larguraHolder * 0.12;

                double xPorcaIni = xHolderIni;
                double xPorcaFim = xPorcaIni + wPorca;
                double xCorpoFim = xPorcaFim + wCorpo;
                double xFlangeFim = xCorpoFim + wFlange;
                double xConeFim = xFlangeFim + wCone;

                double metadePorca = metadeAltura + 9.0;
                double metadeCorpo = metadeAltura + 7.0;
                double metadeFlange = metadeAltura + 15.0;
                double metadePino = metadeAltura * 0.5;

                svg.Append(string.Format(
                    "<rect x='{0:F1}' y='{1:F1}' width='{2:F1}' height='{3:F1}' rx='2' fill='url(#" + gradId + "_marromEscuro)' stroke='#221408' stroke-width='0.6'/>",
                    xPorcaIni, centroY - metadePorca, wPorca, metadePorca * 2));

                svg.Append(string.Format(
                    "<rect x='{0:F1}' y='{1:F1}' width='{2:F1}' height='{3:F1}' fill='url(#" + gradId + "_marrom)' stroke='#3d2817' stroke-width='0.5'/>",
                    xPorcaFim, centroY - metadeCorpo, wCorpo, metadeCorpo * 2));

                svg.Append(string.Format(
                    "<rect x='{0:F1}' y='{1:F1}' width='{2:F1}' height='{3:F1}' fill='url(#" + gradId + "_marrom)' stroke='#3d2817' stroke-width='0.6'/>",
                    xCorpoFim, centroY - metadeFlange, wFlange, metadeFlange * 2));

                svg.Append(string.Format(
                    "<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {2:F1},{4:F1} {0:F1},{5:F1}' fill='url(#" + gradId + "_marrom)' stroke='#4a2f18' stroke-width='0.5'/>",
                    xFlangeFim, centroY - metadeFlange * 0.75,
                    xConeFim, centroY - metadePino,
                    centroY + metadePino,
                    centroY + metadeFlange * 0.75));

                svg.Append(string.Format(
                    "<rect x='{0:F1}' y='{1:F1}' width='{2:F1}' height='{3:F1}' rx='1.5' fill='url(#" + gradId + "_marromEscuro)' stroke='#221408' stroke-width='0.5'/>",
                    xConeFim, centroY - metadePino, wPino, metadePino * 2));
            }

            // ------------------------------------------------------------
            // FERRAMENTA (corte + haste), desenhada por cima do inicio do
            // holder pra dar a impressao de estar inserida na porca.
            // ------------------------------------------------------------

            // Parte de corte (flute) - ESQUERDA. Usa um PATH customizado
            // em vez de rect+rx, pra arredondar SO a ponta (esquerda) -
            // o lado da haste (direita) fica reto. Isso e importante pra
            // fresas esfericas (raioCanto = metade do diametro): sem
            // isso, um rect+rx arredondaria os 4 cantos, deixando a
            // ferramenta inteira parecendo uma esfera, nao so a ponta.
            double topoY = centroY - metadeAltura;
            double baseY = centroY + metadeAltura;
            double raioClamp = Math.Min(raioCantoPx, metadeAltura); // nao deixa o raio passar da metade da altura

            if (f.EhBroca)
            {
                // BROCA: desenha a parte reta (corpo helicoidal) + ponta
                // CONICA (nao arredondada) formada pelo angulo de ponta
                // real (ou 118 graus padrao da industria, se nao
                // confirmado). O comprimento da ponta conica e calculado
                // trigonometricamente a partir do raio e do meio-angulo.
                double anguloPontaGraus = f.AnguloPonta > 0 ? f.AnguloPonta : 118.0;
                double meioAnguloRad = (anguloPontaGraus / 2.0) * Math.PI / 180.0;
                double comprimentoPontaPx = metadeAltura / Math.Tan(meioAnguloRad);
                if (comprimentoPontaPx > compCortePx * 0.6) comprimentoPontaPx = compCortePx * 0.6; // limite visual

                double xInicioCorpo = inicioCorteX + comprimentoPontaPx;

                // Corpo reto (helicoidal) da broca
                svg.Append(string.Format(
                    "<rect x='{0:F1}' y='{1:F1}' width='{2:F1}' height='{3:F1}' fill='url(#" + gradId + "_ouro)' stroke='#5a3d0a' stroke-width='0.5'/>",
                    xInicioCorpo, topoY, compCortePx - comprimentoPontaPx, metadeAltura * 2));

                // Ponta conica (triangulo apontando pra esquerda)
                svg.Append(string.Format(
                    "<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {2:F1},{4:F1}' fill='url(#" + gradId + "_ouro)' stroke='#5a3d0a' stroke-width='0.5'/>",
                    inicioCorteX, centroY, xInicioCorpo, topoY, baseY));

                // Linhas de flauta helicoidal (no corpo reto, nao na ponta)
                int numLinhasBroca = 3;
                for (int lb = 0; lb < numLinhasBroca; lb++)
                {
                    double xLb = xInicioCorpo + ((compCortePx - comprimentoPontaPx) * (lb + 0.5) / numLinhasBroca);
                    svg.Append(string.Format(
                        "<line x1='{0:F1}' y1='{1:F1}' x2='{2:F1}' y2='{3:F1}' stroke='#1e1e1e' stroke-width='0.6'/>",
                        xLb, topoY, xLb - 3, baseY));
                }

                // Cota do angulo de ponta - arco + linha de chamada com
                // seta + texto maior, em cor de destaque, na ponta da broca
                double raioArco = Math.Min(comprimentoPontaPx * 0.7, 18.0);
                svg.Append(string.Format(
                    "<path d='M {0:F1} {1:F1} A {2:F1} {2:F1} 0 0 0 {3:F1} {4:F1}' fill='none' stroke='#888' stroke-width='0.6'/>",
                    inicioCorteX + raioArco, centroY - (raioArco * Math.Tan(meioAnguloRad)),
                    raioArco,
                    inicioCorteX + raioArco, centroY + (raioArco * Math.Tan(meioAnguloRad))));

                // Linha de chamada (leader) da ponta da broca ate o texto,
                // com seta na ponta de origem
                double xTextoAngulo = inicioCorteX + raioArco + 35;
                double yTextoAngulo = topoY - 14;
                svg.Append(string.Format(
                    "<line x1='{0:F1}' y1='{1:F1}' x2='{2:F1}' y2='{3:F1}' stroke='#e74c3c' stroke-width='1'/>",
                    inicioCorteX, centroY, xTextoAngulo, yTextoAngulo));
                svg.Append(string.Format(
                    "<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {4:F1},{5:F1}' fill='#e74c3c'/>",
                    inicioCorteX, centroY,
                    inicioCorteX + 8, centroY - 3,
                    inicioCorteX + 8, centroY + 3));

                svg.Append(string.Format(
                    "<rect x='{0:F1}' y='{1:F1}' width='46' height='18' rx='3' fill='#fff' stroke='#e74c3c' stroke-width='1'/>",
                    xTextoAngulo, yTextoAngulo - 12));
                svg.Append(string.Format(
                    "<text x='{0:F1}' y='{1:F1}' fill='#e74c3c' font-size='13' font-family='Arial' font-weight='bold'>{2:F0}°</text>",
                    xTextoAngulo + 5, yTextoAngulo + 1, anguloPontaGraus));
            }
            else if (raioClamp > 0.5)
            {
                string pathD = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "M {0:F1} {1:F1} L {2:F1} {1:F1} L {2:F1} {3:F1} L {4:F1} {3:F1} " +
                    "A {5:F1} {5:F1} 0 0 1 {6:F1} {7:F1} " +
                    "L {6:F1} {8:F1} " +
                    "A {5:F1} {5:F1} 0 0 1 {4:F1} {1:F1} Z",
                    /*0*/ inicioCorteX + raioClamp,
                    /*1*/ topoY,
                    /*2*/ fimCorteX,
                    /*3*/ baseY,
                    /*4*/ inicioCorteX + raioClamp,
                    /*5*/ raioClamp,
                    /*6*/ inicioCorteX,
                    /*7*/ baseY - raioClamp,
                    /*8*/ topoY + raioClamp);

                svg.Append("<path d='" + pathD + "' fill='url(#" + gradId + "_ouro)' stroke='#5a3d0a' stroke-width='0.5'/>");
            }
            else
            {
                // Sem raio de canto perceptivel - retangulo reto normal
                svg.Append(string.Format(
                    "<rect x='{0:F1}' y='{1:F1}' width='{2:F1}' height='{3:F1}' fill='url(#" + gradId + "_ouro)' stroke='#5a3d0a' stroke-width='0.5'/>",
                    inicioCorteX, topoY, compCortePx, alturaFerramentaPx));
            }

            // Haste (cabo) - vai ate dentro da porca de pinca
            svg.Append(string.Format(
                "<rect x='{0:F1}' y='{1:F1}' width='{2:F1}' height='{3:F1}' fill='url(#" + gradId + "_aco)' stroke='#333' stroke-width='0.5'/>",
                fimCorteX, centroY - metadeAltura, (xHolderIni + 15.0) - fimCorteX, alturaFerramentaPx));

            // Linhas de flauta na parte azul
            int numLinhas = 4;
            for (int li = 0; li < numLinhas; li++)
            {
                double xLinha = inicioCorteX + (compCortePx * (li + 0.5) / numLinhas);
                svg.Append(string.Format(
                    "<line x1='{0:F1}' y1='{1:F1}' x2='{2:F1}' y2='{3:F1}' stroke='#1e1e1e' stroke-width='0.6'/>",
                    xLinha, centroY - metadeAltura, xLinha - 3, centroY + metadeAltura));
            }

            // ------------------------------------------------------------
            // Cota de DIAMETRO (simbolo oficial ⌀), com linha de chamada
            // diagonal apontando pra borda da ferramenta - estilo desenho
            // tecnico.
            // ------------------------------------------------------------
            double yTopoFerramenta = centroY - metadeAltura;
            double xChamadaIni = 6.0;
            double yChamadaIni = margemTopo - 2.0;
            svg.Append(string.Format(
                "<line x1='{0:F1}' y1='{1:F1}' x2='{2:F1}' y2='{3:F1}' stroke='#aaa' stroke-width='0.7'/>",
                xChamadaIni, yChamadaIni, inicioCorteX + compCortePx * 0.3, yTopoFerramenta));
            svg.Append(string.Format(
                "<text x='{0:F1}' y='{1:F1}' fill='#ccc' font-size='10' font-family='Arial'>&#8960;{2:F0}</text>",
                xChamadaIni - 4, yChamadaIni - 2, diametro));

            // ------------------------------------------------------------
            // Cota de COMPRIMENTO TOTAL (L) - linha de cota com linhas de
            // extensao (ticks verticais) e setas nas pontas, estilo
            // desenho tecnico, cobrindo so a ferramenta (corte + haste),
            // sem contar o porta-ferramenta (que e so ilustrativo).
            // ------------------------------------------------------------
            double yCota = canvasH - 6;
            double yExtensaoTopo = centroY + metadeAltura + 3;
            double fimMedidaL = fimCorteX + (compTotal - compCorte) * escala;

            svg.Append(string.Format(
                "<line x1='{0:F1}' y1='{1:F1}' x2='{0:F1}' y2='{2:F1}' stroke='#888' stroke-width='0.5'/>",
                inicioCorteX, yExtensaoTopo, yCota));
            svg.Append(string.Format(
                "<line x1='{0:F1}' y1='{1:F1}' x2='{0:F1}' y2='{2:F1}' stroke='#888' stroke-width='0.5'/>",
                fimMedidaL, yExtensaoTopo, yCota));

            svg.Append(string.Format(
                "<line x1='{0:F1}' y1='{1:F1}' x2='{2:F1}' y2='{1:F1}' stroke='#ccc' stroke-width='0.7'/>",
                inicioCorteX, yCota, fimMedidaL));
            svg.Append(string.Format(
                "<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {2:F1},{4:F1}' fill='#ccc'/>",
                inicioCorteX, yCota, inicioCorteX + 5, yCota - 2, yCota + 2));
            svg.Append(string.Format(
                "<polygon points='{0:F1},{1:F1} {2:F1},{3:F1} {2:F1},{4:F1}' fill='#ccc'/>",
                fimMedidaL, yCota, fimMedidaL - 5, yCota - 2, yCota + 2));

            svg.Append(string.Format(
                "<text x='{0:F1}' y='{1:F1}' fill='#ccc' font-size='10' font-family='Arial' text-anchor='middle'>{2:F0}mm</text>",
                (inicioCorteX + fimMedidaL) / 2.0, yCota - 4, compTotal));

            svg.Append("</svg>");

            return svg.ToString();
        }

        public static int GetUnloadOption(string dummy)
        {
            return (int)Session.LibraryUnloadOption.Immediately;
        }
    }
}
