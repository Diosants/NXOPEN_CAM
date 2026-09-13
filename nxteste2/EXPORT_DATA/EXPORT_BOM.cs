using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using NXOpen;
using NXOpen.Assemblies;

namespace NXCADAutomation.EXPORT_DATA
{
    // ══════════════════════════════════════════════════════════════════
    // EXPORT_BOM
    // ══════════════════════════════════════════════════════════════════
    // Percorre a montagem do work part atual (recursivamente, via
    // Component.GetChildren() - mesmo padrao ja usado nas rotinas de
    // VISES_FIXTURES do projeto PATHNC) e gera um BOM (bill of materials)
    // em .csv, com uma linha por PARTE UNICA (agrupando ocorrencias
    // repetidas do mesmo arquivo em uma linha so, com a coluna
    // "Quantity" somando as ocorrencias).
    //
    // Saida em .csv (nao .xlsx) de proposito: abre direto no Excel sem
    // precisar de referencia COM/Interop ao Excel instalado na maquina
    // (o que quebraria em qualquer PC sem Office, ou com versao
    // diferente do Excel). Se mais pra frente for necessario um .xlsx
    // "de verdade" (com formatacao, multiplas abas, etc.), isso pode
    // virar uma segunda automacao (EXPORT_BOM_XLSX) usando
    // Microsoft.Office.Interop.Excel.
    //
    // Colunas exportadas: Level (profundidade na arvore), Component Name
    // (nome da ocorrencia), Part Name (arquivo .prt de origem), Reference
    // Set, Quantity (ocorrencias do mesmo Part Name em toda a montagem).
    // ══════════════════════════════════════════════════════════════════
    public class EXPORT_BOM
    {
        private class BomRow
        {
            public string ComponentName;
            public string PartName;
            public string ReferenceSet;
            public int Level;
        }

        public static void Run(string[] args)
        {
            Session theSession = Session.GetSession();
            Part workPart = theSession.Parts.Work;
            ListingWindow lw = theSession.ListingWindow;

            if (workPart == null)
            {
                MessageBox.Show("No work part is loaded.", "Export BOM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Component root = workPart.ComponentAssembly.RootComponent;
            if (root == null)
            {
                LogMessage(lw, "AVISO: work part nao tem uma montagem (nenhum componente encontrado).");
                MessageBox.Show(
                    "This part has no assembly components (it is not a top-level assembly).",
                    "Export BOM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<BomRow> rows = new List<BomRow>();
            CollectComponents(root, 0, rows);

            if (rows.Count == 0)
            {
                LogMessage(lw, "AVISO: nenhum componente filho encontrado na montagem.");
                MessageBox.Show("No child components found in this assembly.", "Export BOM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outputFile = AskOutputFile(workPart);
            if (string.IsNullOrEmpty(outputFile))
                return;

            LogMessage(lw, "Gerando BOM de '" + workPart.Leaf + "' (" + rows.Count.ToString() + " componente(s) encontrado(s)) para: " + outputFile);

            try
            {
                WriteBomCsv(rows, outputFile);
                LogMessage(lw, "SUCESSO: BOM exportado com sucesso.");
                MessageBox.Show("BOM exported successfully:\n" + outputFile, "Export BOM", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogMessage(lw, "ERRO ao exportar BOM: " + ex.Message);
                MessageBox.Show("Error exporting BOM:\n" + ex.Message, "Export BOM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Percorre recursivamente a arvore de componentes a partir de
        /// "parent", adicionando uma BomRow por componente filho
        /// encontrado (em qualquer profundidade).
        /// </summary>
        private static void CollectComponents(Component parent, int level, List<BomRow> rows)
        {
            foreach (Component child in parent.GetChildren())
            {
                BomRow row = new BomRow();
                row.ComponentName = child.Name;
                row.Level = level;
                try
                {
                    row.PartName = (child.Prototype as Part) != null ? ((Part)child.Prototype).Leaf : "";
                }
                catch
                {
                    row.PartName = "";
                }
                try
                {
                    row.ReferenceSet = child.ReferenceSet;
                }
                catch
                {
                    row.ReferenceSet = "";
                }
                rows.Add(row);
                CollectComponents(child, level + 1, rows);
            }
        }

        /// <summary>
        /// Agrupa as linhas por Part Name (arquivo de origem) e escreve o
        /// CSV com uma linha por parte unica + coluna Quantity.
        /// </summary>
        private static void WriteBomCsv(List<BomRow> rows, string outputFile)
        {
            Dictionary<string, int> quantityByPartName = new Dictionary<string, int>();
            Dictionary<string, BomRow> firstRowByPartName = new Dictionary<string, BomRow>();

            foreach (BomRow row in rows)
            {
                string key = string.IsNullOrEmpty(row.PartName) ? row.ComponentName : row.PartName;
                if (!quantityByPartName.ContainsKey(key))
                {
                    quantityByPartName[key] = 0;
                    firstRowByPartName[key] = row;
                }
                quantityByPartName[key] = quantityByPartName[key] + 1;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Component Name,Part Name,Reference Set,Quantity,First Level");
            foreach (KeyValuePair<string, int> kv in quantityByPartName)
            {
                BomRow row = firstRowByPartName[kv.Key];
                sb.AppendLine(
                    CsvEscape(row.ComponentName) + "," +
                    CsvEscape(row.PartName) + "," +
                    CsvEscape(row.ReferenceSet) + "," +
                    kv.Value.ToString() + "," +
                    row.Level.ToString());
            }

            File.WriteAllText(outputFile, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Escapa um campo para CSV (aspas duplas quando o valor contem
        /// virgula, aspas ou quebra de linha).
        /// </summary>
        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            if (value.IndexOf(',') >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\n') >= 0)
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        /// <summary>
        /// Abre um SaveFileDialog pre-preenchido com o nome do work part
        /// (mesma pasta, sufixo "_BOM.csv") e devolve o caminho escolhido,
        /// ou string vazia se o usuario cancelar.
        /// </summary>
        private static string AskOutputFile(Part workPart)
        {
            using (SaveFileDialog dlg = new SaveFileDialog())
            {
                dlg.Title = "Export BOM - choose output file";
                dlg.Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                dlg.FileName = System.IO.Path.GetFileNameWithoutExtension(workPart.Leaf) + "_BOM.csv";
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
