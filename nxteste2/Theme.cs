// PATHNC AUTOMATION - Theme.cs
//
// Tema visual unico do pacote. Regras:
//   - Uma acao primaria (azul) por pagina; todo o resto e secundario (branco,
//     borda cinza). Vermelho so para acoes destrutivas. Verde/laranja viram
//     cor de STATUS, nunca de botao.
//   - Secoes viram cartoes brancos sobre fundo cinza-claro.
//   - Feedback de sucesso vai para a status bar; MessageBox so para erro e
//     confirmacao.
//
// Form1 usa estes helpers nos seus proprios ActionButton/SectionTitle/Divider,
// entao todas as paginas mudam de uma vez. Forms secundarias migram
// gradualmente (Theme.StyleForm / Theme.StyleGrid / Theme.PrimaryButton...).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace nxteste2
{
    public static class Theme
    {
        // ---------------- Paleta ----------------
        public static readonly Color PageBg = Color.FromArgb(243, 244, 246);     // fundo das paginas
        public static readonly Color CardBg = Color.White;
        public static readonly Color CardBorder = Color.FromArgb(209, 213, 219);
        public static readonly Color Line = Color.FromArgb(229, 231, 235);

        public static readonly Color TextPrimary = Color.FromArgb(17, 24, 39);
        public static readonly Color TextSecondary = Color.FromArgb(75, 85, 99);
        public static readonly Color TextMuted = Color.FromArgb(107, 114, 128);

        public static readonly Color Accent = Color.FromArgb(37, 99, 235);
        public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
        public static readonly Color AccentSoft = Color.FromArgb(219, 234, 254);

        public static readonly Color Danger = Color.FromArgb(185, 28, 28);
        public static readonly Color DangerSoft = Color.FromArgb(254, 242, 242);
        public static readonly Color Success = Color.FromArgb(21, 128, 61);
        public static readonly Color Warning = Color.FromArgb(180, 83, 9);

        public static readonly Color NavBg = Color.FromArgb(15, 23, 42);
        public static readonly Color NavHover = Color.FromArgb(30, 41, 59);
        public static readonly Color NavActiveBg = Color.FromArgb(30, 41, 59);
        public static readonly Color NavText = Color.FromArgb(203, 213, 225);
        public static readonly Color HeaderBg = Color.FromArgb(30, 41, 59);

        public static readonly Color SecondaryBorder = Color.FromArgb(156, 163, 175);
        public static readonly Color SecondaryHover = Color.FromArgb(243, 244, 246);

        // ---------------- Fontes ----------------
        public static readonly Font FontBody = new Font("Segoe UI", 9F);
        public static readonly Font FontSmall = new Font("Segoe UI", 8F);
        public static readonly Font FontLabel = new Font("Segoe UI", 8F);                 // rotulo de campo, sem negrito
        public static readonly Font FontSection = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        public static readonly Font FontButton = new Font("Segoe UI", 9F);
        public static readonly Font FontButtonPrimary = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
        public static readonly Font FontTitle = new Font("Segoe UI Semibold", 13F, FontStyle.Bold);
        public static readonly Font FontSubtitle = new Font("Segoe UI", 8.5F);

        public const int ButtonHeight = 30;
        public const int PrimaryButtonHeight = 36;
        public const int Radius = 6;

        public enum ButtonKind { Primary, Secondary, Danger }

        // ---------------- Botoes ----------------
        public static Button MakeButton(ButtonKind kind, string text, int x, int y, int w, int h, EventHandler onClick)
        {
            Button b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            switch (kind)
            {
                case ButtonKind.Primary:
                    b.BackColor = Accent; b.ForeColor = Color.White; b.Font = FontButtonPrimary;
                    b.FlatAppearance.BorderSize = 0;
                    b.FlatAppearance.MouseOverBackColor = AccentHover;
                    break;
                case ButtonKind.Danger:
                    b.BackColor = Color.White; b.ForeColor = Danger; b.Font = FontButton;
                    b.FlatAppearance.BorderSize = 1; b.FlatAppearance.BorderColor = Color.FromArgb(252, 165, 165);
                    b.FlatAppearance.MouseOverBackColor = DangerSoft;
                    break;
                default:
                    b.BackColor = Color.White; b.ForeColor = TextPrimary; b.Font = FontButton;
                    b.FlatAppearance.BorderSize = 1; b.FlatAppearance.BorderColor = SecondaryBorder;
                    b.FlatAppearance.MouseOverBackColor = SecondaryHover;
                    break;
            }
            if (onClick != null) b.Click += onClick;
            RoundCorners(b, Radius);
            return b;
        }

        public static Button PrimaryButton(string text, int x, int y, int w, EventHandler onClick)
        { return MakeButton(ButtonKind.Primary, text, x, y, w, PrimaryButtonHeight, onClick); }
        public static Button SecondaryButton(string text, int x, int y, int w, EventHandler onClick)
        { return MakeButton(ButtonKind.Secondary, text, x, y, w, ButtonHeight, onClick); }
        public static Button DangerButton(string text, int x, int y, int w, EventHandler onClick)
        { return MakeButton(ButtonKind.Danger, text, x, y, w, ButtonHeight, onClick); }

        public static void RoundCorners(Control c, int radius)
        {
            int d = radius * 2;
            if (c.Width <= d || c.Height <= d) return;
            GraphicsPath path = new GraphicsPath();
            Rectangle r = new Rectangle(0, 0, c.Width, c.Height);
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            c.Region = new Region(path);
        }

        // ---------------- Cartoes ----------------
        // Uso incremental (compativel com o fluxo "y += ..." do Form1):
        //   BeginCard(page, y) -> ... adiciona controles ... -> EndCard(page, yFinal)
        // O cartao e um Panel enviado para tras, entao os controles ficam por cima.
        private class CardState { public int StartY; }
        private static readonly Dictionary<Panel, CardState> _open = new Dictionary<Panel, CardState>();
        public const int CardPadX = 12;
        public const int CardPadY = 10;

        public static void BeginCard(Panel page, int y)
        {
            EndCardIfOpen(page);
            _open[page] = new CardState { StartY = y - CardPadY };
        }

        public static void EndCard(Panel page, int yBottom)
        {
            CardState st;
            if (!_open.TryGetValue(page, out st)) return;
            _open.Remove(page);
            int x = 16 - CardPadX;
            int w = page.ClientSize.Width > 0 ? page.ClientSize.Width - 2 * x : 232 + 2 * CardPadX;
            Panel card = new Panel
            {
                Location = new Point(x, st.StartY),
                Size = new Size(w, Math.Max(20, yBottom + CardPadY - st.StartY)),
                BackColor = CardBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(CardBorder))
                    DrawRoundedRect(e.Graphics, p, new Rectangle(0, 0, card.Width - 1, card.Height - 1), 8);
            };
            page.Controls.Add(card);
            card.SendToBack();
        }

        // Fecha o ultimo cartao da pagina usando o controle mais baixo
        public static void EndCardIfOpen(Panel page)
        {
            CardState st;
            if (!_open.TryGetValue(page, out st)) return;
            int bottom = st.StartY + 40;
            foreach (Control c in page.Controls)
                if (c.Top >= st.StartY && c.Bottom > bottom) bottom = c.Bottom;
            EndCard(page, bottom + 4);
        }

        public static void DrawRoundedRect(Graphics g, Pen pen, Rectangle r, int radius)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int d = radius * 2;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                g.DrawPath(pen, p);
            }
        }

        // ---------------- Rotulos ----------------
        public static Label SectionLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = FontSection,
                ForeColor = TextSecondary,
            };
        }

        public static Label FieldLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = FontLabel,
                ForeColor = TextMuted,
            };
        }

        public static Label Hint(string text, int x, int y, int w, int h)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                Font = FontSmall,
                ForeColor = TextMuted,
            };
        }

        // ---------------- Combos / inputs ----------------
        public static void StyleCombo(ComboBox cb)
        {
            cb.FlatStyle = FlatStyle.Flat;
            cb.Font = FontBody;
            cb.BackColor = Color.White;
        }

        // ---------------- Forms secundarias ----------------
        public static void StyleForm(Form f)
        {
            f.BackColor = Color.White;
            f.Font = FontBody;
        }

        public static void StyleGrid(DataGridView g)
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
            g.BorderStyle = BorderStyle.None;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.GridColor = Line;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            g.ColumnHeadersDefaultCellStyle.BackColor = PageBg;
            g.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
            g.ColumnHeadersDefaultCellStyle.Font = FontSection;
            g.ColumnHeadersHeight = 30;
            g.DefaultCellStyle.Font = FontSmall;
            g.DefaultCellStyle.ForeColor = TextPrimary;
            g.DefaultCellStyle.SelectionBackColor = AccentSoft;
            g.DefaultCellStyle.SelectionForeColor = TextPrimary;
            g.RowTemplate.Height = 26;
        }

        // Cabecalho escuro padrao das janelas secundarias
        public static Panel FormHeader(string title, string subtitle)
        {
            Panel h = new Panel { Dock = DockStyle.Top, Height = subtitle == null ? 52 : 66, BackColor = HeaderBg, Padding = new Padding(16, 10, 16, 8) };
            Label t = new Label { Text = title, AutoSize = true, Location = new Point(16, 12), ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold) };
            h.Controls.Add(t);
            if (subtitle != null)
                h.Controls.Add(new Label { Text = subtitle, AutoSize = true, Location = new Point(16, 38), ForeColor = NavText, Font = FontSmall });
            Panel stripe = new Panel { Dock = DockStyle.Bottom, Height = 2, BackColor = Accent };
            h.Controls.Add(stripe);
            return h;
        }
    }
}