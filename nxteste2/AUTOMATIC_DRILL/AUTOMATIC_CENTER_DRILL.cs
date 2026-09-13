// NX 2406
// Journal - Furacao passante com ferramenta correspondente ao diametro
//
using System;
using System.Collections.Generic;
using System.Globalization;
using NXOpen;
using NXOpen.CAM;

public class AUTOMATIC_CENTER_DRILL
{
    public static void Run(string[] args)
    {
        Session theSession = Session.GetSession();
        Part workPart = theSession.Parts.Work;
        Part displayPart = theSession.Parts.Display;



        theSession.ListingWindow.Open();

        // ---------------------------------------------------------------
        // AJUSTE AQUI conforme o seu setup:
        // ---------------------------------------------------------------
        string nomePrograma = "1234";           // grupo de PROGRAM onde a operacao sera inserida
        string nomeMetodo = "DRILL_METHOD";     // METHOD
        double profundidadeFuroPassante = 5; // AJUSTE conforme a espessura da peca + folga de saida

        // ---------------------------------------------------------------
        // Diametros de broca HSS realmente disponiveis na biblioteca de
        // ferramentas (ver ToolLibraryFromNX) - usado para achar o tamanho
        // de broca mais proximo do diametro do furo encontrado.
        // ---------------------------------------------------------------
        double[] diametrosDisponiveis = new double[]
        {
        2, 2.5, 3, 3.5, 4, 4.5, 5, 5.5, 6, 6.5, 7, 7.5, 8, 8.5,
        9, 9.5, 10, 10.5, 11, 11.5, 12, 12.5, 13, 13.5, 14, 14.5,
        15.5, 16.5, 17, 17.5, 18, 19, 20, 25
        };

        NCGroup nCGroupPrograma = ((NCGroup)workPart.CAMSetup.CAMGroupCollection.FindObject(nomePrograma));
        Method method1 = ((Method)workPart.CAMSetup.CAMGroupCollection.FindObject(nomeMetodo));

        // ---------------------------------------------------------------
        // 1) LEVANTAMENTO: pega apenas os grupos "furo comum" (HOLE_DIAMETER...),
        //    que carregam o diametro literal no nome. Grupos de rosca (M#) e
        //    socket head (M#_SOCKET_HEAD) NAO entram aqui - eles usam macho e
        //    ferramenta de contra-furo, nao broca reta por diametro.
        // ---------------------------------------------------------------
        List<FeatureGeometry> gruposDeFuroComum = new List<FeatureGeometry>();

        foreach (NCGroup grupo in workPart.CAMSetup.CAMGroupCollection)
        {
            FeatureGeometry fg = grupo as FeatureGeometry;
            if (fg == null)
                continue;

            if (!fg.Name.StartsWith("HOLE_DIAMETER", StringComparison.OrdinalIgnoreCase))
                continue;

            gruposDeFuroComum.Add(fg);
        }

        theSession.ListingWindow.WriteLine("Grupos de furo comum encontrados: " + gruposDeFuroComum.Count);
        theSession.ListingWindow.WriteLine("");

        // ---------------------------------------------------------------
        // 2) CRIACAO: uma operacao de furacao por grupo, com a ferramenta
        //    correspondente ao diametro extraido do nome do grupo.
        // ---------------------------------------------------------------
        List<CAMObject> operacoesCriadas = new List<CAMObject>();
        int totalCriadas = 0;
        int totalErros = 0;

        foreach (FeatureGeometry featureGeometryAtual in gruposDeFuroComum)
        {
            try
            {
                double? diametro = ExtrairDiametroDoNome(featureGeometryAtual.Name);

                if (!diametro.HasValue)
                {
                    theSession.ListingWindow.WriteLine(
                        "AVISO: nao foi possivel extrair o diametro do nome '" + featureGeometryAtual.Name + "' -> pulando.");
                    totalErros++;
                    continue;
                }

                string nomeFerramenta = "HSS_DRILL_D" + FormatarDiametro(EncontrarDiametroDisponivelMaisProximo(diametro.Value, diametrosDisponiveis));

                theSession.ListingWindow.WriteLine(
                    "Grupo '" + featureGeometryAtual.Name + "' (diametro real = " + diametro.Value +
                    ") -> ferramenta escolhida: " + nomeFerramenta);

                Tool tool1 = workPart.CAMSetup.CAMGroupCollection.FindObject(nomeFerramenta) as Tool;

                if (tool1 == null)
                {
                    theSession.ListingWindow.WriteLine(
                        "ERRO: ferramenta '" + nomeFerramenta + "' nao encontrada na biblioteca -> pulando grupo '" +
                        featureGeometryAtual.Name + "'.");
                    totalErros++;
                    continue;
                }

                string nomeOperacao = "DRILL_" + featureGeometryAtual.Name;

                NXOpen.CAM.Operation operacaoAtual;
                operacaoAtual = workPart.CAMSetup.CAMOperationCollection.CreateWithUserName(
                    nCGroupPrograma,
                    method1,
                    tool1,
                    featureGeometryAtual,
                    "hole_making",
                    "DRILLING",
                    OperationCollection.UseDefaultName.False,
                    nomeOperacao,
                    "Furo passante - " + nomeFerramenta);

                HoleDrilling holeDrillingAtual = ((HoleDrilling)operacaoAtual);
                HoleDrillingBuilder holeDrillingBuilderAtual;
                holeDrillingBuilderAtual = workPart.CAMSetup.CAMOperationCollection.CreateHoleDrillingBuilder(holeDrillingAtual);

                holeDrillingBuilderAtual.PredefinedDepth.Status = true;
                holeDrillingBuilderAtual.PredefinedDepth.Value = profundidadeFuroPassante;

                holeDrillingBuilderAtual.FeedsBuilder.SpindleRpmBuilder.Value = 1200.0;
                holeDrillingBuilderAtual.FeedsBuilder.FeedCutBuilder.Value = 150.0;

                NXObject resultadoCommit;
                resultadoCommit = holeDrillingBuilderAtual.Commit();

                holeDrillingBuilderAtual.Destroy();

                operacoesCriadas.Add((CAMObject)resultadoCommit);

                theSession.ListingWindow.WriteLine(
                    "Operacao '" + nomeOperacao + "' criada com ferramenta '" + nomeFerramenta + "'.");

                totalCriadas++;
            }
            catch (Exception ex)
            {
                totalErros++;
                theSession.ListingWindow.WriteLine(
                    "ERRO ao criar operacao para o grupo '" + featureGeometryAtual.Name + "' -> " + ex.Message);
            }
        }

        theSession.ListingWindow.WriteLine("");
        theSession.ListingWindow.WriteLine("Total de operacoes criadas: " + totalCriadas);
        theSession.ListingWindow.WriteLine("Total de erros: " + totalErros);

        // ---------------------------------------------------------------
        // 3) GERACAO DO TOOLPATH: so depois que TODAS as operacoes ja
        //    foram criadas.
        // ---------------------------------------------------------------
        if (operacoesCriadas.Count > 0)
        {
            theSession.ListingWindow.WriteLine("");
            theSession.ListingWindow.WriteLine("Gerando toolpath para " + operacoesCriadas.Count + " operacao(oes)...");

            CAMObject[] objetosParaGerar = operacoesCriadas.ToArray();
            workPart.CAMSetup.GenerateToolPath(objetosParaGerar);

            theSession.ListingWindow.WriteLine("Toolpath gerado.");
        }
        else
        {
            theSession.ListingWindow.WriteLine("Nenhuma operacao foi criada, nada a gerar.");
        }
    }

    // Reconstitui o valor numerico a partir de um nome no formato "HOLE_DIAMETER42_00"
    // -> remove o prefixo, troca "_" por "." e faz o parse. Retorna null se falhar.
    private static double? ExtrairDiametroDoNome(string nomeGrupo)
    {
        const string prefixo = "HOLE_DIAMETER";

        if (!nomeGrupo.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
            return null;

        string resto = nomeGrupo.Substring(prefixo.Length);   // ex: "42_00"
        string comPonto = resto.Replace("_", ".");            // ex: "42.00"

        double valor;
        if (double.TryParse(comPonto, NumberStyles.Any, CultureInfo.InvariantCulture, out valor))
            return valor;

        return null;
    }

    // Formata o diametro para o padrao de nome de ferramenta da biblioteca:
    // valores inteiros -> "25" ; valores com decimal -> "17.5" (usa ponto, igual a biblioteca)
    private static string FormatarDiametro(double diametro)
    {
        double arredondado = Math.Round(diametro, 2);

        if (arredondado == Math.Truncate(arredondado))
            return ((int)arredondado).ToString(CultureInfo.InvariantCulture);

        return arredondado.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // Procura, dentro do array de diametros realmente disponiveis na biblioteca,
    // o valor mais proximo do diametro do furo encontrado na peca.
    private static double EncontrarDiametroDisponivelMaisProximo(double diametroAlvo, double[] diametrosDisponiveis)
    {
        double maisProximo = diametrosDisponiveis[0];
        double menorDiferenca = Math.Abs(diametrosDisponiveis[0] - diametroAlvo);

        for (int i = 1; i < diametrosDisponiveis.Length; i++)
        {
            double diferenca = Math.Abs(diametrosDisponiveis[i] - diametroAlvo);
            if (diferenca < menorDiferenca)
            {
                menorDiferenca = diferenca;
                maisProximo = diametrosDisponiveis[i];
            }
        }

        return maisProximo;
    }

    public static int GetUnloadOption(string dummy) { return (int)Session.LibraryUnloadOption.Immediately; }
}
