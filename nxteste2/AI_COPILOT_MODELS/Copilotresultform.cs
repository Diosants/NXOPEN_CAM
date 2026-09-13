// PATHNC AUTOMATION - CopilotResultForm.cs
//
// Janela modeless com o resultado do StrategyHistoryService:
//   - cabecalho: peca atual x melhor vizinha, tempo estimado
//   - grid 1: pecas similares (score, dimensoes, tempo)
//   - grid 2: operacoes da peca selecionada no grid 1
//   - grid 3: parametros medianos por ferramenta (todas as vizinhas)
//
// O botao "Apply Strategy" dispara o evento ApplyRequested com a vizinha
// selecionada; quem cria as operacoes no NX e o StrategyApplier (chamado
// pelo Form1, que tem a barra de progresso).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PathNCAutomation.ShopDoc;

namespace nxteste2
{
    public class CopilotResultForm : Form
    {
        private readonly Label _lblHeader = new Label();
        private readonly Label _lblSequence = new Label();
        private readonly DataGridView _gridParts = new DataGridView();
        private readonly DataGridView _gridOps = new DataGridView();
        private readonly DataGridView _gridTools = new DataGridView();
        private readonly Button _btnApply = new Button();
        private readonly PictureBox _picAtual = new PictureBox();
        private readonly PictureBox _picVizinha = new PictureBox();
        private readonly Label _lblPicAtual = new Label();
        private readonly Label _lblPicVizinha = new Label();

        private StrategySuggestion _sug;

        // Disparado ao clicar em "Apply Strategy" com a vizinha selecionada
        public event Action<SimilarPart> ApplyRequested;

        public SimilarPart VizinhaSelecionada
        {
            get
            {
                if (_sug == null || _gridParts.SelectedRows.Count == 0) return null;
                int idx = _gridParts.SelectedRows[0].Index;
                return (idx >= 0 && idx < _sug.Vizinhos.Count) ? _sug.Vizinhos[idx] : null;
            }
        }

        private static readonly Color ColorPrimary = Color.FromArgb(37, 99, 235);
        private static readonly Color ColorHeaderBg = Color.FromArgb(30, 41, 59);
        private static readonly Color ColorBorder = Color.FromArgb(226, 232, 240);

        public CopilotResultForm()
        {
            Text = "PATHNC Copilot — Strategy Suggestion";
            Size = new Size(1000, 760);
            MinimumSize = new Size(820, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            BuildLayout();
        }

        private void BuildLayout()
        {
            // Cabecalho
            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = ColorHeaderBg, Padding = new Padding(14, 8, 14, 8) };
            _lblHeader.Dock = DockStyle.Fill;
            _lblHeader.ForeColor = Color.White;
            _lblHeader.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold);
            header.Controls.Add(_lblHeader);

            // Rodape com sequencia + botao
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 70, Padding = new Padding(14, 8, 14, 8) };
            Panel footerLine = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorBorder };
            _lblSequence.Dock = DockStyle.Fill;
            _lblSequence.Font = new Font("Segoe UI", 8.5F);
            _lblSequence.ForeColor = Color.FromArgb(71, 85, 105);
            _btnApply.Text = "Apply Strategy to Current Part";
            _btnApply.Dock = DockStyle.Right;
            _btnApply.Width = 230;
            _btnApply.Enabled = false;
            _btnApply.Cursor = Cursors.Hand;
            _btnApply.Click += (s, e) =>
            {
                SimilarPart v = VizinhaSelecionada;
                if (v == null) return;
                if (ApplyRequested != null) ApplyRequested(v);
            };
            _btnApply.FlatStyle = FlatStyle.Flat;
            _btnApply.BackColor = ColorPrimary;
            _btnApply.ForeColor = Color.White;
            _btnApply.FlatAppearance.BorderSize = 0;
            footer.Controls.Add(_lblSequence);
            footer.Controls.Add(_btnApply);
            footer.Controls.Add(footerLine);

            // Faixa de imagens: peca atual x vizinha selecionada
            Panel strip = new Panel { Dock = DockStyle.Top, Height = 190, Padding = new Padding(14, 8, 14, 4) };
            Panel stripLine = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = ColorBorder };
            TableLayoutPanel tl = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tl.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
            tl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            ConfigurePic(_picAtual); ConfigurePic(_picVizinha);
            ConfigurePicLabel(_lblPicAtual, "CURRENT PART");
            ConfigurePicLabel(_lblPicVizinha, "SELECTED SIMILAR PART");
            tl.Controls.Add(_lblPicAtual, 0, 0);
            tl.Controls.Add(_lblPicVizinha, 1, 0);
            tl.Controls.Add(_picAtual, 0, 1);
            tl.Controls.Add(_picVizinha, 1, 1);
            strip.Controls.Add(tl);
            strip.Controls.Add(stripLine);

            // Corpo: 3 grids em split
            SplitContainer splitV = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 160 };
            SplitContainer splitH = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 520 };

            ConfigureGrid(_gridParts);
            ConfigureGrid(_gridOps);
            ConfigureGrid(_gridTools);

            splitV.Panel1.Controls.Add(WrapWithTitle(_gridParts, "SIMILAR PARTS (select one to see its operations)"));
            splitH.Panel1.Controls.Add(WrapWithTitle(_gridOps, "OPERATIONS USED"));
            splitH.Panel2.Controls.Add(WrapWithTitle(_gridTools, "MEDIAN PARAMETERS PER TOOL"));
            splitV.Panel2.Controls.Add(splitH);

            Controls.Add(splitV);
            Controls.Add(strip);
            Controls.Add(footer);
            Controls.Add(header);

            _gridParts.SelectionChanged += (s, e) =>
            {
                ShowOperationsOfSelected();
                _btnApply.Enabled = VizinhaSelecionada != null && VizinhaSelecionada.Operations.Count > 0;
            };
        }

        private static void ConfigurePic(PictureBox p)
        {
            p.Dock = DockStyle.Fill;
            p.Margin = new Padding(4);
            p.SizeMode = PictureBoxSizeMode.Zoom;
            p.BackColor = Color.FromArgb(241, 245, 249);
            p.BorderStyle = BorderStyle.FixedSingle;
        }

        private static void ConfigurePicLabel(Label l, string text)
        {
            l.Text = text;
            l.Dock = DockStyle.Fill;
            l.Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
            l.ForeColor = Color.FromArgb(71, 85, 105);
            l.TextAlign = ContentAlignment.MiddleLeft;
        }

        private static Image BytesToImage(byte[] png)
        {
            if (png == null || png.Length == 0) return null;
            try
            {
                using (var ms = new System.IO.MemoryStream(png))
                    return Image.FromStream(ms);
            }
            catch { return null; }
        }

        private static void SetImage(PictureBox p, byte[] png)
        {
            Image old = p.Image;
            p.Image = BytesToImage(png);
            if (old != null) old.Dispose();
        }

        private static Panel WrapWithTitle(Control inner, string title)
        {
            Panel p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 8) };
            Label t = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 20,
                Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
            };
            inner.Dock = DockStyle.Fill;
            p.Controls.Add(inner);
            p.Controls.Add(t);
            return p;
        }

        private static void ConfigureGrid(DataGridView g)
        {
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.BackgroundColor = Color.White;
            g.BorderStyle = BorderStyle.FixedSingle;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            g.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F);
        }

        // ------------------------------------------------------------
        public void Load(ShopDocModel atual, StrategySuggestion sug)
        {
            _sug = sug;
            SimilarPart melhor = sug.Vizinhos[0];
            SetImage(_picAtual, atual.Thumbnail);

            _lblHeader.Text = string.Format(
                "Current: {0}  ({1:F0} × {2:F0} × {3:F0} mm, {4} faces)      Best match: {5} rev.{6}  ({7:P0})      Est. time: {8:F1} min",
                atual.PartName, atual.SizeX, atual.SizeY, atual.SizeZ, atual.FaceCount,
                melhor.PartName, melhor.Revision, melhor.Score, sug.TempoEstimadoMin);

            _gridParts.DataSource = sug.Vizinhos.Select(v => new
            {
                Score = string.Format("{0:P0}", v.Score),
                Part = v.PartName,
                Rev = v.Revision,
                Size = string.Format("{0:F0} × {1:F0} × {2:F0}", v.SizeX, v.SizeY, v.SizeZ),
                Faces = v.FaceCount,
                Ops = v.Operations.Count,
                TimeMin = Math.Round(v.TotalTimeMin, 1),
                Saved = v.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
            }).ToList();

            _gridTools.DataSource = sug.Parametros.Select(p => new
            {
                Tool = p.ToolName,
                Dia = p.ToolDiameter.HasValue ? p.ToolDiameter.Value.ToString("F1") : "",
                RPM = p.RpmMediana.HasValue ? p.RpmMediana.Value.ToString("F0") : "",
                Feed = p.FeedMediana.HasValue ? p.FeedMediana.Value.ToString("F0") : "",
                Uses = p.Ocorrencias,
            }).ToList();

            _lblSequence.Text = "Suggested sequence: " +
                (sug.SequenciaMaisComum.Count > 0 ? string.Join("  →  ", sug.SequenciaMaisComum) : "(none)");

            if (_gridParts.Rows.Count > 0)
                _gridParts.Rows[0].Selected = true;
            ShowOperationsOfSelected();
        }

        private void ShowOperationsOfSelected()
        {
            if (_sug == null || _gridParts.SelectedRows.Count == 0) return;
            int idx = _gridParts.SelectedRows[0].Index;
            if (idx < 0 || idx >= _sug.Vizinhos.Count) return;

            SetImage(_picVizinha, _sug.Vizinhos[idx].Thumbnail);
            _lblPicVizinha.Text = "SELECTED SIMILAR PART — " + _sug.Vizinhos[idx].PartName;

            List<ShopDocOperationModel> ops = _sug.Vizinhos[idx].Operations;
            _gridOps.DataSource = ops.Select(o => new
            {
                Seq = o.Seq,
                Operation = o.OperationName,
                Type = o.OperationType,
                Tool = o.ToolName,
                T = o.ToolNumber,
                RPM = o.Rpm.HasValue ? o.Rpm.Value.ToString("F0") : "",
                Feed = o.FeedCut.HasValue ? o.FeedCut.Value.ToString("F0") : "",
                Method = o.Method,
                Min = Math.Round(o.TimeMin, 1),
            }).ToList();
        }
    }
}