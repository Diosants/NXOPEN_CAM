// PATHNC AUTOMATION - QuoteForms.cs
//   QuoteResultForm      : mostra o detalhamento do orcamento e salva na tabela Quote
//   ActualTimeDialog     : registra tempo real de maquina/setup de uma peca ja salva

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PathNCAutomation.ShopDoc;

namespace nxteste2
{
    // ====================================================================
    public class QuoteResultForm : Form
    {
        private readonly QuoteEstimate _q;
        private readonly ShopDocModel _atual;
        private readonly TextBox _txtCustomer = new TextBox();
        private readonly TextBox _txtNotes = new TextBox();
        private readonly Label _lblSaved = new Label();

        private static readonly Color ColorHeaderBg = Color.FromArgb(30, 41, 59);
        private static readonly Color ColorPrimary = Color.FromArgb(37, 99, 235);
        private static readonly Color ColorBorder = Color.FromArgb(226, 232, 240);
        private static readonly Color ColorMuted = Color.FromArgb(100, 116, 139);

        public QuoteResultForm(QuoteEstimate q, ShopDocModel atual)
        {
            _q = q; _atual = atual;
            Text = "PATHNC — Quote & Lead Time";
            Size = new Size(640, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Build();
        }

        private void Build()
        {
            QuoteSettings s = _q.Settings;
            string cur = s.Currency + " ";

            Panel header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = ColorHeaderBg, Padding = new Padding(16, 10, 16, 10) };
            Label title = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                Text = string.Format("{0}   ×{1}\nUnit {2}{3:N2}   ·   Total {2}{4:N2}   ·   Lead time {5:F0} days   ·   Confidence: {6}",
                    _q.PartName, _q.Quantity, cur, _q.UnitPrice, _q.TotalPrice, _q.LeadTimeDays, _q.ConfidenceLabel)
            };
            header.Controls.Add(title);

            TableLayoutPanel t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16, 12, 16, 8), AutoScroll = true };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

            Row(t, "BASIS", null, true);
            Row(t, "Similar parts", _q.SimilarPartsLabel);
            Row(t, "Correction factor (actual/estimated)", string.Format("{0:F2}  ({1} calibrated part{2})", _q.CorrectionFactor, _q.CalibrationSamples, _q.CalibrationSamples == 1 ? "" : "s"));
            Row(t, "Operations (from best match)", _q.OperationCount.ToString());

            Row(t, "TIME", null, true);
            Row(t, "Machining per part", string.Format("{0:F1} min  ({1:F2} h)", _q.EstMachiningMin, _q.EstMachiningMin / 60));
            Row(t, "Machining for batch", string.Format("{0:F1} min  ({1:F2} h)", _q.EstMachiningMin * _q.Quantity, _q.EstMachiningMin * _q.Quantity / 60));
            Row(t, "Setup", string.Format("{0:F0} min", _q.EstSetupMin));
            Row(t, "CAM programming", string.Format("{0:F0} min  ({1} ops × {2:F0} min)", _q.EstProgrammingMin, _q.OperationCount, s.ProgrammingMinPerOp));

            Row(t, "COST", null, true);
            Row(t, string.Format("Machine ({0}{1:N0}/h)", cur, s.MachineRatePerHour), cur + _q.MachineCost.ToString("N2"));
            Row(t, string.Format("Programming ({0}{1:N0}/h)", cur, s.ProgrammingRatePerHour), cur + _q.ProgrammingCost.ToString("N2"));
            Row(t, "Cost subtotal", cur + _q.Cost.ToString("N2"));
            Row(t, string.Format("Margin {0:F0}%", s.MarginPercent), cur + (_q.TotalPrice - _q.Cost).ToString("N2"));
            Row(t, "TOTAL PRICE", cur + _q.TotalPrice.ToString("N2"), true);
            Row(t, "Unit price", cur + _q.UnitPrice.ToString("N2"));

            Row(t, "LEAD TIME", null, true);
            Row(t, "Queue", string.Format("{0:F1} days", s.QueueDays));
            Row(t, "Programming + machine", string.Format("{0:F1} days", _q.LeadTimeDays - s.QueueDays));
            Row(t, "ESTIMATED LEAD TIME", string.Format("{0:F0} working days", _q.LeadTimeDays), true);

            // rodape: cliente / notas / salvar
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 120, Padding = new Padding(16, 8, 16, 10) };
            Panel line = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorBorder };
            Label lc = new Label { Text = "Customer", Location = new Point(16, 14), AutoSize = true, ForeColor = ColorMuted };
            _txtCustomer.Location = new Point(90, 10); _txtCustomer.Width = 260;
            Label ln = new Label { Text = "Notes", Location = new Point(16, 44), AutoSize = true, ForeColor = ColorMuted };
            _txtNotes.Location = new Point(90, 40); _txtNotes.Width = 500;
            Button btnSave = new Button
            {
                Text = "Save Quote",
                Location = new Point(370, 8),
                Size = new Size(110, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorPrimary,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            Button btnCopy = new Button
            {
                Text = "Copy Summary",
                Location = new Point(486, 8),
                Size = new Size(104, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(13, 148, 136),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnCopy.FlatAppearance.BorderSize = 0;
            _lblSaved.Location = new Point(16, 74); _lblSaved.AutoSize = true; _lblSaved.ForeColor = Color.FromArgb(22, 163, 74);

            btnSave.Click += (s2, e) =>
            {
                try
                {
                    int id = new QuoteService().SaveQuote(_q, _atual, _txtCustomer.Text, _txtNotes.Text);
                    _lblSaved.Text = "Saved as Quote #" + id + ".";
                    btnSave.Enabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Save failed:\n" + ex.Message, "Quote", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            btnCopy.Click += (s2, e) => Clipboard.SetText(Summary());

            footer.Controls.AddRange(new Control[] { lc, _txtCustomer, ln, _txtNotes, btnSave, btnCopy, _lblSaved, line });

            Controls.Add(t);
            Controls.Add(footer);
            Controls.Add(header);
        }

        private static void Row(TableLayoutPanel t, string label, string value, bool bold = false)
        {
            Label l = new Label
            {
                Text = label,
                AutoSize = true,
                Margin = new Padding(0, bold ? 10 : 3, 0, 3),
                Font = new Font("Segoe UI", bold ? 9F : 8.5F, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = bold ? Color.FromArgb(30, 41, 59) : ColorMuted
            };
            Label v = new Label
            {
                Text = value ?? "",
                AutoSize = true,
                Margin = new Padding(0, bold ? 10 : 3, 0, 3),
                Font = new Font("Segoe UI", bold ? 9.5F : 8.5F, bold ? FontStyle.Bold : FontStyle.Regular)
            };
            int r = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            t.Controls.Add(l, 0, r);
            t.Controls.Add(v, 1, r);
        }

        private string Summary()
        {
            QuoteSettings s = _q.Settings;
            return string.Format(
                "QUOTE — {0} (x{1})\nBased on: {2}\nMachining: {3:F1} min/part | Setup: {4:F0} min | Programming: {5:F0} min\n" +
                "Machine cost: {6} {7:N2} | Programming: {6} {8:N2} | Margin: {9:F0}%\n" +
                "TOTAL: {6} {10:N2}  (unit {6} {11:N2})\nLead time: {12:F0} working days\nConfidence: {13}",
                _q.PartName, _q.Quantity, _q.SimilarPartsLabel, _q.EstMachiningMin, _q.EstSetupMin, _q.EstProgrammingMin,
                s.Currency, _q.MachineCost, _q.ProgrammingCost, s.MarginPercent, _q.TotalPrice, _q.UnitPrice,
                _q.LeadTimeDays, _q.ConfidenceLabel);
        }
    }

    // ====================================================================
    public class ActualTimeDialog : Form
    {
        private readonly ComboBox _cmbPart = new ComboBox();
        private readonly NumericUpDown _nudTime = new NumericUpDown();
        private readonly NumericUpDown _nudSetup = new NumericUpDown();
        private readonly TextBox _txtNote = new TextBox();

        public ActualTimeDialog(string preselectPart)
        {
            Text = "Register actual machining time";
            Size = new Size(420, 260);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.White;

            int y = 16;
            Add("Part (last revision)", y); _cmbPart.Location = new Point(160, y); _cmbPart.Width = 230;
            _cmbPart.DropDownStyle = ComboBoxStyle.DropDownList; y += 36;
            Add("Actual machining (min)", y); _nudTime.Location = new Point(160, y); _nudTime.Width = 120;
            _nudTime.Maximum = 100000; _nudTime.DecimalPlaces = 1; y += 36;
            Add("Actual setup (min)", y); _nudSetup.Location = new Point(160, y); _nudSetup.Width = 120;
            _nudSetup.Maximum = 10000; _nudSetup.DecimalPlaces = 0; y += 36;
            Add("Note (machine, operator...)", y); _txtNote.Location = new Point(160, y); _txtNote.Width = 230; y += 44;

            Button ok = new Button
            {
                Text = "Save",
                Location = new Point(200, y),
                Size = new Size(90, 28),
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White
            };
            ok.FlatAppearance.BorderSize = 0;
            Button cancel = new Button { Text = "Cancel", Location = new Point(300, y), Size = new Size(90, 28), DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };
            Controls.AddRange(new Control[] { _cmbPart, _nudTime, _nudSetup, _txtNote, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;

            try
            {
                foreach (string p in new QuoteService().ListPartNames()) _cmbPart.Items.Add(p);
                if (!string.IsNullOrEmpty(preselectPart))
                {
                    int i = _cmbPart.Items.IndexOf(preselectPart);
                    if (i >= 0) _cmbPart.SelectedIndex = i;
                }
                if (_cmbPart.SelectedIndex < 0 && _cmbPart.Items.Count > 0) _cmbPart.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load parts:\n" + ex.Message, "Actual time", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            ok.Click += (s, e) =>
            {
                if (_cmbPart.SelectedItem == null || _nudTime.Value <= 0)
                {
                    MessageBox.Show("Select a part and enter the actual machining time.", "Actual time", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                try
                {
                    bool okDb = new QuoteService().RegisterActualTime(
                        _cmbPart.SelectedItem.ToString(), (double)_nudTime.Value,
                        _nudSetup.Value > 0 ? (double?)_nudSetup.Value : null, _txtNote.Text);
                    if (!okDb) { MessageBox.Show("Part not found in database.", "Actual time"); DialogResult = DialogResult.None; }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Save failed:\n" + ex.Message, "Actual time", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None;
                }
            };
        }

        private void Add(string text, int y)
        {
            Controls.Add(new Label { Text = text, Location = new Point(16, y + 3), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) });
        }
    }

    // ====================================================================
    public class QuoteHistoryForm : Form
    {
        private readonly TextBox _txtFilter = new TextBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _lblCount = new Label();
        private List<QuoteRecord> _rows = new List<QuoteRecord>();

        public QuoteHistoryForm()
        {
            Text = "PATHNC — Quote History";
            Size = new Size(1000, 560);
            MinimumSize = new Size(760, 400);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);

            Panel top = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(12, 10, 12, 6) };
            Label l = new Label { Text = "Filter (part or customer):", Location = new Point(12, 13), AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139) };
            _txtFilter.Location = new Point(170, 10); _txtFilter.Width = 260;
            Button btnSearch = new Button
            {
                Text = "Search",
                Location = new Point(440, 8),
                Size = new Size(80, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White
            };
            btnSearch.FlatAppearance.BorderSize = 0;
            Button btnCopy = new Button
            {
                Text = "Copy Selected Summary",
                Location = new Point(530, 8),
                Size = new Size(170, 26),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(13, 148, 136),
                ForeColor = Color.White
            };
            btnCopy.FlatAppearance.BorderSize = 0;
            _lblCount.Location = new Point(712, 13); _lblCount.AutoSize = true; _lblCount.ForeColor = Color.FromArgb(100, 116, 139);
            top.Controls.AddRange(new Control[] { l, _txtFilter, btnSearch, btnCopy, _lblCount });

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false; _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = Color.White; _grid.BorderStyle = BorderStyle.None;
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            _grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F);

            Label hint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Text = "  Double-click a row to see the full breakdown.",
                ForeColor = Color.FromArgb(148, 163, 184),
                Font = new Font("Segoe UI", 7.5F),
                TextAlign = ContentAlignment.MiddleLeft
            };

            Controls.Add(_grid);
            Controls.Add(hint);
            Controls.Add(top);

            btnSearch.Click += (s, e) => Reload();
            _txtFilter.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { Reload(); e.SuppressKeyPress = true; } };
            _grid.CellDoubleClick += (s, e) => ShowDetail();
            btnCopy.Click += (s, e) => { QuoteRecord q = Selected(); if (q != null) Clipboard.SetText(Summary(q)); };

            Reload();
        }

        private void Reload()
        {
            try
            {
                _rows = new QuoteService().ListQuotes(_txtFilter.Text);
                _grid.DataSource = _rows.ConvertAll(q => new
                {
                    Id = q.QuoteId,
                    Date = q.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    Part = q.PartName,
                    Customer = q.CustomerName,
                    Qty = q.Quantity,
                    Unit = q.Currency + " " + q.UnitPrice.ToString("N2"),
                    Total = q.Currency + " " + q.TotalPrice.ToString("N2"),
                    Days = q.LeadTimeDays.ToString("F0"),
                    MachMin = q.EstMachiningMin.ToString("F1"),
                    Factor = q.CorrectionFactor.ToString("F2"),
                    By = q.CreatedBy,
                });
                _lblCount.Text = _rows.Count + " quote(s)";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not load quotes:\n" + ex.Message, "Quote History", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private QuoteRecord Selected()
        {
            if (_grid.SelectedRows.Count == 0) return null;
            int i = _grid.SelectedRows[0].Index;
            return (i >= 0 && i < _rows.Count) ? _rows[i] : null;
        }

        private void ShowDetail()
        {
            QuoteRecord q = Selected();
            if (q == null) return;
            MessageBox.Show(Summary(q), "Quote #" + q.QuoteId, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string Summary(QuoteRecord q)
        {
            return string.Format(
                "QUOTE #{0} — {1} (x{2})\nCustomer: {3}\nDate: {4:dd/MM/yyyy HH:mm}  by {5}\n\n" +
                "Based on: {6}\nCorrection factor: {7:F2}\n" +
                "Machining: {8:F1} min/part | Setup: {9:F0} min | Programming: {10:F0} min\n" +
                "Machine cost: {11} {12:N2} | Programming: {11} {13:N2} | Margin: {14:F0}%\n" +
                "TOTAL: {11} {15:N2}  (unit {11} {16:N2})\nLead time: {17:F0} working days\n\nNotes: {18}",
                q.QuoteId, q.PartName, q.Quantity, q.CustomerName, q.CreatedAt.ToLocalTime(), q.CreatedBy,
                q.SimilarParts, q.CorrectionFactor, q.EstMachiningMin, q.EstSetupMin, q.EstProgrammingMin,
                q.Currency, q.MachineCost, q.ProgrammingCost, q.MarginPercent, q.TotalPrice, q.UnitPrice,
                q.LeadTimeDays, q.Notes);
        }
    }
}