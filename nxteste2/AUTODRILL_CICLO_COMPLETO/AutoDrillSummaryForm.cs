using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace nxteste2
{
    // Popup ilustrativo mostrado assim que o Auto Drill (RunAutoDrillCycle,
    // acionado pelo botão "Auto Drill — Ciclo Completo" OU pelas frases do
    // chat como "roda os furos") termina - resume quantos furos e operações
    // CAM foram processados nessa execução.
    //
    // De propósito, NÃO mostra "tempo ganho" comparado ao manual - decisão
    // explícita do usuário, porque quanto tempo um furo/rosca/contra-furo
    // leva pra programar à mão varia demais de oficina pra oficina pra virar
    // um número "oficial" no painel. Só contagem real (furos + operações),
    // lida de AUTODRILL_GEOMETRIA_PROPRIA.LastRunSummary depois que
    // Run(null) retorna.
    public class AutoDrillSummaryForm : Form
    {
        private static readonly Color ColorHeaderBg = Color.FromArgb(30, 41, 59);
        private static readonly Color ColorContentBg = Color.FromArgb(248, 250, 252);
        private static readonly Color ColorTextPrimary = Color.FromArgb(15, 23, 42);
        private static readonly Color ColorTextSecondary = Color.FromArgb(71, 85, 105);
        private static readonly Color ColorTextMuted = Color.FromArgb(148, 163, 184);
        private static readonly Color ColorBorder = Color.FromArgb(226, 232, 240);
        private static readonly Color ColorPrimary = Color.FromArgb(37, 99, 235);
        private static readonly Color ColorSuccess = Color.FromArgb(22, 163, 74);
        private static readonly Color ColorWarning = Color.FromArgb(217, 119, 6);
        private static readonly Color ColorTeal = Color.FromArgb(13, 148, 136);
        private static readonly Color ColorIndigo = Color.FromArgb(99, 102, 241);
        private static readonly Color ColorCardBg = Color.White;
        private static readonly Color ColorWarnBg = Color.FromArgb(255, 251, 235);
        private static readonly Color ColorWarnText = Color.FromArgb(146, 64, 14);

        private const int PAD = 18;
        private const int WIDTH = 420;

        private readonly AUTODRILL_GEOMETRIA_PROPRIA.AutoDrillSummary _s;

        public AutoDrillSummaryForm(AUTODRILL_GEOMETRIA_PROPRIA.AutoDrillSummary summary)
        {
            _s = summary ?? new AUTODRILL_GEOMETRIA_PROPRIA.AutoDrillSummary();
            Text = "Auto Drill — Run Summary";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(WIDTH, 660);
            MinimumSize = new Size(360, 480);
            BackColor = ColorContentBg;
            Font = new Font("Segoe UI", 9F);
            BuildUi();
        }

        // ══════════════════════════════════════════════════════════════════
        // MONTAGEM DA INTERFACE
        // ══════════════════════════════════════════════════════════════════
        private void BuildUi()
        {
            Panel header = BuildHeader();
            Panel scroll = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };

            int contentW = WIDTH - PAD * 2 - SystemInformation.VerticalScrollBarWidth;
            int y = PAD;

            // ── Cartão de destaque: total de furos + operações criadas ──
            Panel heroCard = new Panel
            {
                Location = new Point(PAD, y),
                Size = new Size(contentW, 92),
                BackColor = ColorHeaderBg,
            };
            ApplyRoundedCorners(heroCard, 10);
            AddHeroStat(heroCard, "HOLES PROCESSED", _s.TotalFurosProcessados.ToString(), 0, contentW / 2);
            AddHeroStat(heroCard, "OPERATIONS CREATED", _s.OperacoesCriadas.ToString(), contentW / 2, contentW / 2);
            scroll.Controls.Add(heroCard);
            y += 92 + 18;

            // ── Barra de proporção por categoria + legenda ──
            Panel bar = BuildProportionBar(contentW);
            bar.Location = new Point(PAD, y);
            scroll.Controls.Add(bar);
            y += bar.Height + 8;

            Panel legend = BuildLegend(contentW);
            legend.Location = new Point(PAD, y);
            scroll.Controls.Add(legend);
            y += legend.Height + 22;

            // ── Cartões por categoria ──
            SectionTitle(scroll, "HOLES BY CATEGORY", PAD, y); y += 26;
            y = AddStatCard(scroll, "Thread (pilot + finish)", _s.FurosRosca, _s.TamanhosRosca, ColorTeal, y, contentW);
            y = AddStatCard(scroll, "Socket Head (allen)", _s.FurosSoquete, _s.TamanhosSoquete, ColorIndigo, y, contentW);
            y = AddStatCard(scroll, "Generic Counterbore", _s.FurosContraFuroGenerico, _s.TamanhosContraFuroGenerico, ColorSuccess, y, contentW);
            y = AddStatCard(scroll, "Normal Hole", _s.FurosNormais, _s.TamanhosNormais, ColorPrimary, y, contentW);
            y = AddStatCard(scroll, "Unclassified", _s.FurosNaoClassificados, _s.TamanhosNaoClassificados, ColorWarning, y, contentW);

            if (_s.FurosEstreitosSobContraFuro > 0)
            {
                Label info = new Label
                {
                    Text = "↳ includes " + _s.FurosEstreitosSobContraFuro + " narrow hole(s) drilled below the counterbore (through/blind), in addition to the wide mouth.",
                    Location = new Point(PAD, y),
                    Size = new Size(contentW, 30),
                    Font = new Font("Segoe UI", 7.5F),
                    ForeColor = ColorTextMuted,
                };
                scroll.Controls.Add(info);
                y += 34;
            }

            // ── Avisos (só aparece se tiver algo) ──
            int totalAvisos = _s.Ignorados + _s.GruposSemDiametro + _s.MedicaoDeStepsFalhou;
            if (totalAvisos > 0)
            {
                y += 6;
                Divider(scroll, PAD, y, contentW); y += 16;
                SectionTitle(scroll, "WARNINGS", PAD, y); y += 26;

                Panel warnCard = new Panel { Location = new Point(PAD, y), Size = new Size(contentW, 10), BackColor = ColorWarnBg };
                int wy = 10;
                if (_s.Ignorados > 0)
                    wy = AddWarnLine(warnCard, _s.Ignorados + " group(s) ignored during the scan.", wy, contentW);
                if (_s.GruposSemDiametro > 0)
                    wy = AddWarnLine(warnCard, _s.GruposSemDiametro + " group(s) without a readable DIAMETER_1.", wy, contentW);
                if (_s.MedicaoDeStepsFalhou > 0)
                    wy = AddWarnLine(warnCard, _s.MedicaoDeStepsFalhou + " counterbore hole(s) without a successful step measurement — check by hand whether they need a narrow hole underneath.", wy, contentW);
                warnCard.Size = new Size(contentW, wy + 8);
                warnCard.Paint += (s, e) =>
                {
                    using (Pen p = new Pen(ColorWarning, 1))
                        e.Graphics.DrawRectangle(p, 0, 0, warnCard.Width - 1, warnCard.Height - 1);
                };
                scroll.Controls.Add(warnCard);
                y += warnCard.Height + 16;
            }

            y += 6;
            Divider(scroll, PAD, y, contentW); y += 14;
            Label materialLbl = new Label
            {
                Text = "Material used: " + (string.IsNullOrEmpty(_s.MaterialUsado) ? "—" : _s.MaterialUsado)
                    + "   ·   " + _s.GruposVarridos + " group(s) / " + _s.FeaturesVarridas + " feature(s) scanned",
                Location = new Point(PAD, y),
                Size = new Size(contentW, 32),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColorTextMuted,
            };
            scroll.Controls.Add(materialLbl);
            y += 42;

            Button close = new Button
            {
                Text = "Close",
                Location = new Point(PAD, y),
                Size = new Size(contentW, 32),
                BackColor = ColorPrimary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            close.FlatAppearance.BorderSize = 0;
            close.Click += (s, e) => Close();
            scroll.Controls.Add(close);
            ApplyRoundedCorners(close, 6);
            y += 44;

            // Ordem importa pro docking: header (Top) depois do scroll (Fill),
            // senão o Fill não reserva espaço pra ele.
            Controls.Add(scroll);
            Controls.Add(header);
        }

        private Panel BuildHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = ColorHeaderBg };
            Label title = new Label
            {
                Text = "Auto Drill — Run Summary",
                Location = new Point(PAD, 16),
                Size = new Size(WIDTH - PAD * 2, 26),
                Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
                ForeColor = Color.White,
            };
            header.Controls.Add(title);
            return header;
        }

        private void AddHeroStat(Panel card, string label, string value, int x, int w)
        {
            Label lblLabel = new Label
            {
                Text = label,
                Location = new Point(x + 14, 14),
                Size = new Size(w - 20, 16),
                Font = new Font("Segoe UI Semibold", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
            };
            Label lblValue = new Label
            {
                Text = value,
                Location = new Point(x + 14, 30),
                Size = new Size(w - 20, 50),
                Font = new Font("Segoe UI", 28F, FontStyle.Bold),
                ForeColor = Color.White,
            };
            card.Controls.Add(lblLabel);
            card.Controls.Add(lblValue);
        }

        // Barra horizontal com um segmento colorido por categoria, largura
        // proporcional à quantidade de furos daquela categoria - só um
        // reforço visual rápido de "onde foi a maior parte do trabalho".
        private Panel BuildProportionBar(int width)
        {
            Panel bar = new Panel { Size = new Size(width, 14), BackColor = ColorBorder };
            ApplyRoundedCorners(bar, 7);
            int total = _s.TotalFurosProcessados;
            if (total <= 0)
                return bar;
            int[] counts = { _s.FurosRosca, _s.FurosSoquete, _s.FurosContraFuroGenerico, _s.FurosNormais, _s.FurosNaoClassificados };
            Color[] colors = { ColorTeal, ColorIndigo, ColorSuccess, ColorPrimary, ColorWarning };
            int x = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] <= 0)
                    continue;
                int segW = (int)Math.Round((counts[i] / (double)total) * width);
                if (x + segW > width)
                    segW = width - x;
                if (segW <= 0)
                    continue;
                Panel seg = new Panel { Location = new Point(x, 0), Size = new Size(segW, bar.Height), BackColor = colors[i] };
                bar.Controls.Add(seg);
                x += segW;
            }
            return bar;
        }

        private Panel BuildLegend(int width)
        {
            Panel legend = new Panel { Size = new Size(width, 20) };
            string[] labels = { "Thread", "Socket", "Counterbore", "Normal", "Unclass." };
            Color[] colors = { ColorTeal, ColorIndigo, ColorSuccess, ColorPrimary, ColorWarning };
            int x = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                Panel dot = new Panel { Location = new Point(x, 6), Size = new Size(8, 8), BackColor = colors[i] };
                Label lbl = new Label
                {
                    Text = labels[i],
                    Location = new Point(x + 12, 2),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 7F),
                    ForeColor = ColorTextMuted,
                };
                legend.Controls.Add(dot);
                legend.Controls.Add(lbl);
                x += TextRenderer.MeasureText(labels[i], lbl.Font).Width + 28;
            }
            return legend;
        }

        private void SectionTitle(Panel page, string text, int x, int y)
        {
            Panel accent = new Panel { Location = new Point(x, y + 1), Size = new Size(3, 13), BackColor = ColorPrimary };
            Label lbl = new Label
            {
                Text = text,
                Location = new Point(x + 9, y),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = ColorTextSecondary,
            };
            page.Controls.Add(accent);
            page.Controls.Add(lbl);
        }

        private void Divider(Panel page, int x, int y, int w)
        {
            Panel line = new Panel { Location = new Point(x, y), Size = new Size(w, 1), BackColor = ColorBorder };
            page.Controls.Add(line);
        }

        // Um cartão por categoria: tarja colorida à esquerda, nome + quantos
        // tamanhos distintos, e o número de furos em destaque à direita.
        // Retorna o próximo Y disponível (mesmo padrão dos helpers do Form1).
        private int AddStatCard(Panel page, string label, int count, int tamanhos, Color accent, int y, int contentW)
        {
            int h = 46;
            Panel card = new Panel { Location = new Point(PAD, y), Size = new Size(contentW, h), BackColor = ColorCardBg };
            ApplyRoundedCorners(card, 8);
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColorBorder, 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };
            Panel stripe = new Panel { Location = new Point(0, 0), Size = new Size(4, h), BackColor = accent };
            Label lblName = new Label
            {
                Text = label,
                Location = new Point(16, 8),
                Size = new Size(contentW - 100, 16),
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = ColorTextPrimary,
            };
            Label lblSub = new Label
            {
                Text = tamanhos + " distinct size(s)",
                Location = new Point(16, 25),
                Size = new Size(contentW - 100, 14),
                Font = new Font("Segoe UI", 7F),
                ForeColor = ColorTextMuted,
            };
            Label lblCount = new Label
            {
                Text = count.ToString(),
                Location = new Point(contentW - 84, 0),
                Size = new Size(74, h),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = accent,
            };
            card.Controls.Add(stripe);
            card.Controls.Add(lblName);
            card.Controls.Add(lblSub);
            card.Controls.Add(lblCount);
            page.Controls.Add(card);
            return y + h + 10;
        }

        private int AddWarnLine(Panel card, string text, int y, int contentW)
        {
            Label lbl = new Label
            {
                Text = "⚠ " + text,
                Location = new Point(12, y),
                Size = new Size(contentW - 24, 32),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColorWarnText,
            };
            card.Controls.Add(lbl);
            return y + 32;
        }

        // Mesmo truque leve do Form1 (Region a partir de um GraphicsPath) —
        // duplicado aqui porque é privado lá, não dá pra reaproveitar direto.
        private static void ApplyRoundedCorners(Control control, int radius)
        {
            int d = radius * 2;
            if (control.Width <= d || control.Height <= d)
                return;
            GraphicsPath path = new GraphicsPath();
            Rectangle bounds = new Rectangle(0, 0, control.Width, control.Height);
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            control.Region = new Region(path);
        }
    }
}
