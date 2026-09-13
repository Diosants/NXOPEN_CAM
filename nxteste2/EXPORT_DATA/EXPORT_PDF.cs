using System;
using System.Windows.Forms;
using NXOpen;

namespace NXCADAutomation.EXPORT_DATA
{
    // ══════════════════════════════════════════════════════════════════
    // EXPORT_PDF
    // ══════════════════════════════════════════════════════════════════
    // Exporta a folha de desenho (drawing sheet) ATIVA do work part atual
    // para um arquivo .pdf, usando o exportador nativo do NX
    // (workPart.PlotManager.CreatePrintPdfbuilder()).
    //
    // Pre-requisito: o work part precisa ter pelo menos uma folha de
    // desenho (Drawings.DrawingSheet) e essa folha precisa estar aberta/
    // ativa na sessao - por isso o metodo checa workPart.DrawingSheets
    // antes de tentar exportar e avisa se nao encontrar nenhuma.
    // ══════════════════════════════════════════════════════════════════
    public class EXPORT_PDF
    {
        public static void Run(string[] args)
        {
            Session theSession = Session.GetSession();
            Part workPart = theSession.Parts.Work;
            ListingWindow lw = theSession.ListingWindow;

            if (workPart == null)
            {
                MessageBox.Show("No work part is loaded.", "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (workPart.DrawingSheets.ToArray().Length == 0)
            {
                LogMessage(lw, "AVISO: nenhuma folha de desenho (drawing sheet) encontrada em '" + workPart.Leaf + "'.");
                MessageBox.Show(
                    "This part has no drawing sheets. Open or create a drawing sheet before exporting to PDF.",
                    "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputFile = AskOutputFile(workPart);
            if (string.IsNullOrEmpty(outputFile))
                return;

            LogMessage(lw, "Exportando PDF de '" + workPart.Leaf + "' para: " + outputFile);

            PrintPDFBuilder printPdfBuilder = null;
            try
            {
                printPdfBuilder = workPart.PlotManager.CreatePrintPdfbuilder();
                printPdfBuilder.Filename = outputFile;

                NXObject result = printPdfBuilder.Commit();

                if (result != null)
                {
                    LogMessage(lw, "SUCESSO: PDF exportado com sucesso.");
                    MessageBox.Show("PDF exported successfully:\n" + outputFile, "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    LogMessage(lw, "AVISO: exportador retornou nulo - verifique se ha uma folha de desenho ativa.");
                }
            }
            catch (Exception ex)
            {
                LogMessage(lw, "ERRO ao exportar PDF: " + ex.Message);
                MessageBox.Show("Error exporting PDF:\n" + ex.Message, "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (printPdfBuilder != null)
                    printPdfBuilder.Destroy();
            }
        }

        /// <summary>
        /// Abre um SaveFileDialog pre-preenchido com o nome do work part
        /// (mesma pasta, extensao .pdf) e devolve o caminho escolhido, ou
        /// string vazia se o usuario cancelar.
        /// </summary>
        private static string AskOutputFile(Part workPart)
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Export PDF - choose output file";
                dlg.Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*";
                dlg.FileName = System.IO.Path.GetFileNameWithoutExtension(workPart.Leaf) + ".pdf";
                try
                {
                    string partDir = System.IO.Path.GetDirectoryName(workPart.FullPath);
                    if (!string.IsNullOrEmpty(partDir))
                        dlg.InitialDirectory = partDir;
                }
                catch
                {
                    // Se o work part ainda nao foi salvo (sem FullPath valido),
                    // deixa o SaveFileDialog usar o diretorio padrao dele.
                }
                if (dlg.ShowDialog() != DialogResult.OK)
                    return string.Empty;
                return dlg.FileName;
            }
        }

        /// <summary>
        /// Escreve uma linha na Listing Window, abrindo-a primeiro se
        /// ainda nao estiver aberta.
        /// </summary>
        private static void LogMessage(ListingWindow lw, string message)
        {
            if (!lw.IsOpen)
                lw.Open();
            lw.WriteLine(message);
        }

        public static int GetUnloadOption(string dummy)
        {
            return (int)Session.LibraryUnloadOption.Immediately;
        }
    }
}
