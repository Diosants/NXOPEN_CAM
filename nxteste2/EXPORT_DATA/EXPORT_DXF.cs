using System;
using System.Windows.Forms;
using NXOpen;

namespace NXCADAutomation.EXPORT_DATA
{
    // ══════════════════════════════════════════════════════════════════
    // EXPORT_DXF
    // ══════════════════════════════════════════════════════════════════
    // Exporta a geometria do work part atual (curvas, esbocos, anotacoes)
    // para um arquivo .dxf, usando o exportador nativo do NX
    // (theSession.DexManager.CreateDxfdwgCreator()).
    //
    // Uso tipico: work part com um layout 2D (curvas de referencia,
    // esboco de contorno, etc.) que precisa virar um .dxf pra mandar pra
    // fora (corte a laser, water jet, fornecedor externo, etc.).
    //
    // OBS: este exportador trabalha em cima do PART inteiro (curvas +
    // anotacoes visiveis), nao de uma folha de desenho (drawing sheet) -
    // isso e intencional, pensado pro caso de uso mais comum em CAD puro
    // (sem drafting), que e "manda esse contorno pra fora". Se no futuro
    // for necessario exportar direto de uma folha de desenho, isso pode
    // virar uma variante separada (EXPORT_DXF_FROM_DRAWING).
    // ══════════════════════════════════════════════════════════════════
    public class EXPORT_DXF
    {
        public static void Run(string[] args)
        {
            Session theSession = Session.GetSession();
            Part workPart = theSession.Parts.Work;
            ListingWindow lw = theSession.ListingWindow;

            if (workPart == null)
            {
                MessageBox.Show("No work part is loaded.", "Export DXF", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputFile = AskOutputFile(workPart);
            if (string.IsNullOrEmpty(outputFile))
                return;

            LogMessage(lw, "Exportando DXF de '" + workPart.Leaf + "' para: " + outputFile);

            // PENDENTE: alguns ambientes NX exigem um "SettingsFile" (.def)
            // explicito (ex.: dxfdwgCreator.SettingsFile = "...\dxfdwg\ug_metric.def").
            // Se o Commit() abaixo der erro pedindo um settings file, me
            // avise com a mensagem exata e eu adiciono o caminho certo pra
            // essa instalacao do NX2406.
            DxfdwgCreator dxfdwgCreator = null;
            try
            {
                dxfdwgCreator = theSession.DexManager.CreateDxfdwgCreator();
                dxfdwgCreator.InputFile = workPart.FullPath;
                dxfdwgCreator.OutputFile = outputFile;
                dxfdwgCreator.FileSaveFlag = false;
                dxfdwgCreator.FlattenAssembly = false;
                dxfdwgCreator.ViewEditMode = true;
                dxfdwgCreator.LayerMask = "1-256";
                dxfdwgCreator.ObjectTypes.Curves = true;
                dxfdwgCreator.ObjectTypes.Annotations = true;
                dxfdwgCreator.ObjectTypes.Structures = true;
                dxfdwgCreator.AutoCADRevision = DxfdwgCreator.AutoCADRevisionOptions.R2013;

                NXObject result = dxfdwgCreator.Commit();

                if (result != null)
                {
                    LogMessage(lw, "SUCESSO: DXF exportado com sucesso.");
                    MessageBox.Show("DXF exported successfully:\n" + outputFile, "Export DXF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    LogMessage(lw, "AVISO: exportador retornou nulo - verifique se ha geometria visivel no work part.");
                }
            }
            catch (Exception ex)
            {
                LogMessage(lw, "ERRO ao exportar DXF: " + ex.Message);
                MessageBox.Show("Error exporting DXF:\n" + ex.Message, "Export DXF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (dxfdwgCreator != null)
                    dxfdwgCreator.Destroy();
            }
        }

        /// <summary>
        /// Abre um SaveFileDialog pre-preenchido com o nome do work part
        /// (mesma pasta, extensao .dxf) e devolve o caminho escolhido, ou
        /// string vazia se o usuario cancelar.
        /// </summary>
        private static string AskOutputFile(Part workPart)
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Export DXF - choose output file";
                dlg.Filter = "DXF Files (*.dxf)|*.dxf|All Files (*.*)|*.*";
                dlg.FileName = System.IO.Path.GetFileNameWithoutExtension(workPart.Leaf) + ".dxf";
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
