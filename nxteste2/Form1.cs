using FBM_DRILLINGS;
using FBM_MACHINING.Strategies;
using FBM_MACHINING_HELPERS;
using FBM_MACHINING_PARCIAL_POCKET;
using FBM_MACHINING_PLANAR_RECTANGULAR;
using FBM_MACHINING_PLANAR_SURFACE;
using FBM_MACHINING_RECTANGULAR_POCKET;
using NX_3_PLUS_TWO_CREATE_FEATURE_GROUP;
using NX_3_PLUS_TWO_RECOGNIZE_FEATURES;
using NX_3_PLUS_TWO_TOOLPATHS;
using NX_3_PLUS_TWO_WCS;
using NXCADAutomation.ShopDocumentation;
using NXOpen.CAM;
using NXOpen.Features;
using NXOPEN_3X_TOOLPATHS;
using NXOPEN_CHECKS;
using NXOPEN_MACHINES;
using PATHNC.COUNTERBORE_STANDARD;
using PATHNC.FBM_CORNER_NOTCH_STRAIGHT;
using PATHNC.FBM_HOLE_RECTANGULAR_STRAIGHT;
using PATHNC.MOLD_WIZARD;
using PathNCAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TOOL_LIBRARY_FROM_NX;
namespace nxteste2
{
    public partial class Form1 : Form
    {
        private MachiningContext context;
        private List<IMachiningStrategy> strategies;
        private List<IMachiningStrategy> strategiesP1;
        private List<IMachiningStrategy> strategiesMetricTap;
        private List<IMachiningStrategy> strategiesSockedHead;
        private List<IMachiningStrategy> strategiesRough;
        private List<IMachiningStrategy> strategiesDrills;
        private List<IMachiningStrategy> strategiesPockets;
        private List<IMachiningStrategy> strategiesPlanarSurface;
        // ── Controles que precisam ser lidos por outros métodos (Form1_Load,
        // botões "Executar", etc.) ficam como campos da classe. ──
        private ComboBox cmbDrills;
        private ComboBox cmbMetricTap;
        private ComboBox cmbSocketHead;
        private ComboBox cmbPockets;
        private ComboBox cmbProfiles;
        private ComboBox cmbSurfaces;
        private ComboBox cmb3xMachines;
        private ComboBox cmb4AxisMachines;
        private ComboBox cmb5AxisMachines;
        private ComboBox cmbLatheMachines;
        private ComboBox cmbMillTurnMachines;
        private ComboBox cmbPostProcessors;
        private ComboBox cmb3Plus2Machines;
        private ComboBox cmbShopDocLanguage;
        private List<PictureBox> _fixtureIcons = new List<PictureBox>();
        // Lista de seleção PRÓPRIA pros ícones de máquina da página 3+2 Axis
        // (separada de _fixtureIcons) - senão SelectFixtureIcon/SelectMachineIcon
        // desmarcariam ícones do grupo errado ao clicar num do outro grupo.
        private List<PictureBox> _machineIcons = new List<PictureBox>();
        private Panel navPanel;
        private Panel contentPanel;
        private Button[] navButtons;
        private Panel[] pages;
        // ── Barra de status/progresso, fixa no rodapé do painel (visível em
        // qualquer página). Ver BuildStatusBar()/ReportProgress(). ──
        private ProgressBar progressBar;
        private Label lblStatusBar;
        // Percentual "de largada" mostrado assim que qualquer etapa/comando
        // começa — em vez de pular de 0%, o que dá a impressão de que nada
        // está acontecendo enquanto a primeira parte roda. Ver
        // ReportProgressIndeterminate().
        private const int ProgressBaselinePercent = 30;
        private int _templateCardIndex = 0;
        private int _strategyCardIndex = 0;
        // ── Layout estreito, pra caber embutido dentro do NX ──
        private const int PAD = 16;
        private const int NAV_WIDTH = 116;
        private const int CONTENT_WIDTH = 380 - NAV_WIDTH - (PAD * 2); // = 232
        // ── Paleta de cores — mantém a identidade visual original (navy +
        // azul/verde/laranja/vermelho), só organizada como constantes reutilizáveis. ──
        private static readonly Color ColorHeaderBg = Color.FromArgb(30, 41, 59);
        private static readonly Color ColorNavBg = Color.FromArgb(15, 23, 42);
        private static readonly Color ColorNavHover = Color.FromArgb(30, 41, 59);
        private static readonly Color ColorContentBg = Color.FromArgb(248, 250, 252);
        private static readonly Color ColorTextPrimary = Color.FromArgb(15, 23, 42);
        private static readonly Color ColorTextSecondary = Color.FromArgb(71, 85, 105);
        private static readonly Color ColorTextMuted = Color.FromArgb(148, 163, 184);
        private static readonly Color ColorBorder = Color.FromArgb(226, 232, 240);
        private static readonly Color ColorPrimary = Color.FromArgb(37, 99, 235);
        private static readonly Color ColorPrimaryHover = Color.FromArgb(29, 78, 216);
        private static readonly Color ColorSuccess = Color.FromArgb(22, 163, 74);
        private static readonly Color ColorSuccessHover = Color.FromArgb(21, 128, 61);
        private static readonly Color ColorWarning = Color.FromArgb(217, 119, 6);
        private static readonly Color ColorWarningHover = Color.FromArgb(180, 98, 4);
        private static readonly Color ColorDanger = Color.FromArgb(220, 38, 38);
        private static readonly Color ColorDangerHover = Color.FromArgb(185, 28, 28);
        private static readonly Color ColorTeal = Color.FromArgb(13, 148, 136);
        private static readonly Color ColorTealHover = Color.FromArgb(15, 118, 110);
        // Cinza neutro — usado só no botão "Descolorir Furos" (a ação é
        // literalmente "voltar tudo pro cinza", então o botão segue a
        // mesma cor do resultado, em vez de usar Warning/Danger que
        // sugeririam alerta).
        private static readonly Color ColorNeutral = Color.FromArgb(100, 116, 139);
        private static readonly Color ColorNeutralHover = Color.FromArgb(71, 85, 105);
        private static readonly Color ColorSelectedTint = Color.FromArgb(219, 234, 254);
        public Form1()
        {
            InitializeComponent();
            BuildUi();
        }
        // ══════════════════════════════════════════════════════════════════
        // MONTAGEM DA INTERFACE
        // ══════════════════════════════════════════════════════════════════
        private void BuildUi()
        {
            this.Load += Form1_Load;
            Panel header = BuildHeader();
            // Cada item leva um glifo curto na frente do texto — dá um ponto
            // de reconhecimento visual rápido pra cada página sem precisar
            // de ícones desenhados/arquivos de imagem extra.
            string[] navLabels =
            {
                "⚙ Setup",
                "⬡ FBM",
                "▦ 2D Mill",
                "◆ 3-Axis",
                "✚ 3+2 Axis",
                "✳ 5-Axis",
                "⛭ Mold Wizard",
                "▤ Templates",
                "★ Strategy Advisor",
                "⌖ Workplanes",
                "✦ AI Assistant",
                "✎ Shop Doc",
                "◎ Copilot",
                "$ Quote",
            };
            string[] navFullNames =
            {
                "Setup",
                "Feature Based Machining",
                "2D Mill Strategy",
                "3-Axis Milling",
                "3+2 Axis",
                "5-Axis Simultaneous",
                "Mold Wizard – Core, Cavity & Mold Base",
                "Templates",
                "Strategy Advisor – Strategies by Geometry",
                "Workplanes – Cylindrical and Block WCS",
                "AI Assistant – Chat with Claude (Anthropic)",
                "Shop Documentation – Setup Sheet Generator",
                "Copilot – Strategy suggestions from your own history",
                "Quote – Cost and lead time estimate from history",
            };
            // Índice(s) do(s) item(ns) de navegação que devem ganhar o selo
            // "NOVO" ao lado do texto. Adicione outros índices aqui quando
            // lançar a próxima feature nova.
            bool[] navIsNew = new bool[navLabels.Length];
            navIsNew[navLabels.Length - 1] = true; // "Quote" (indice dinamico - sempre marca o ULTIMO item da lista)
            pages = new Panel[navLabels.Length];
            pages[0] = BuildPageSetup();
            pages[1] = BuildPageFBM();
            pages[2] = BuildPage2D();
            pages[3] = BuildPage3D();
            pages[4] = BuildPage3Plus2Axis();
            pages[5] = BuildPageComingSoon(
                "5-Axis Simultaneous",
                "Simultaneous 5-axis machining",
                "This module is under development. It will bring simultaneous milling "
                + "strategies for complex surfaces, with tool axis control and collision "
                + "avoidance integrated into the same FBM workflow.");
            pages[6] = BuildPageMoldWizard();
            pages[7] = BuildPageTemplates();
            pages[8] = BuildPageStrategyAdvisor();
            pages[9] = BuildPageWorkplanes();
            pages[10] = BuildPageAIAssistant();
            pages[11] = BuildPageShopDocumentation();
            pages[12] = BuildPageCopilot();
            pages[13] = BuildPageQuote();
            navPanel = BuildNav(navLabels, navFullNames, navIsNew);
            contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg };
            foreach (Panel p in pages)
            {
                p.Visible = false;
                contentPanel.Controls.Add(p);
            }
            Panel statusBar = BuildStatusBar();
            // Ordem importa pro docking: o statusBar (Bottom) precisa ser
            // adicionado ANTES do contentPanel (Fill), senão o Fill não
            // reserva espaço pra ele no rodapé.
            this.Controls.Add(statusBar);
            this.Controls.Add(contentPanel);
            this.Controls.Add(navPanel);
            this.Controls.Add(header);
            SelectPage(0);
        }
        private Panel BuildHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 67, BackColor = ColorHeaderBg };
            // Gradiente sutil (navy escuro → navy um pouco mais claro, na
            // diagonal) em vez de cor chapada — dá profundidade ao cabeçalho
            // sem precisar de nenhuma imagem/recurso externo.
            header.Paint += (s, e) =>
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Rectangle(0, 0, header.Width, header.Height),
                    ColorHeaderBg, Color.FromArgb(51, 65, 90), 35f))
                {
                    e.Graphics.FillRectangle(brush, header.ClientRectangle);
                }
            };
            // Faixa de destaque (3px, cor primária) colada na base do
            // cabeçalho — separa visualmente o header do resto do painel.
            Panel accentStripe = new Panel { Dock = DockStyle.Bottom, Height = 3, BackColor = ColorPrimary };
            // Pequeno indicador circular ao lado do título, só pra dar um
            // toque de "produto" — não representa nenhum status real ainda,
            // mas fica pronto pra virar indicador de conexão/licença depois.
            Panel dot = new Panel
            {
                Location = new Point(16, 13),
                Size = new Size(8, 8),
                BackColor = ColorSuccess,
            };
            dot.Paint += (s, e) =>
            {
                using (SolidBrush b = new SolidBrush(ColorSuccess))
                    e.Graphics.FillEllipse(b, 0, 0, dot.Width - 1, dot.Height - 1);
            };
            Label title = new Label
            {
                Text = "PATHNC AUTOMATION",
                Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(30, 8),
            };
            Label subtitle = new Label
            {
                Text = "NXCAM FBM · Mold Plates Machining",
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColorTextMuted,
                AutoSize = true,
                Location = new Point(31, 34),
            };
            header.Controls.Add(dot);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(accentStripe);
            return header;
        }
        private Panel BuildNav(string[] navLabels, string[] navFullNames, bool[] navIsNew = null)
        {
            Panel nav = new Panel { Dock = DockStyle.Left, Width = NAV_WIDTH, BackColor = ColorNavBg };
            ToolTip tooltip = new ToolTip();
            FlowLayoutPanel navFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = ColorNavBg,
                Padding = new Padding(0, 12, 0, 0),
            };
            navButtons = new Button[navLabels.Length];
            for (int i = 0; i < navLabels.Length; i++)
            {
                bool isNew = navIsNew != null && i < navIsNew.Length && navIsNew[i];
                Button btn = CreateNavButton(navLabels[i], i, isNew);
                tooltip.SetToolTip(btn, navFullNames[i]);
                navButtons[i] = btn;
                navFlow.Controls.Add(btn);
            }
            Panel navFiller = new Panel { Dock = DockStyle.Fill, BackColor = ColorNavBg };
            nav.Controls.Add(navFiller);
            nav.Controls.Add(navFlow);
            return nav;
        }
        private Button CreateNavButton(string text, int index, bool isNew = false)
        {
            Button btn = new Button
            {
                Text = "  " + text,
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(203, 213, 225),
                BackColor = ColorNavBg,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleLeft,
                Size = new Size(NAV_WIDTH, 44),
                Margin = new Padding(0),
                Cursor = Cursors.Hand,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ColorNavHover;
            int capturedIndex = index;
            btn.Click += (s, e) => SelectPage(capturedIndex);
            if (isNew)
            {
                // Selo "NOVO" no canto superior direito do botão, pra
                // destacar um item de navegação recém-adicionado. Button é
                // um Control normal, então dá pra colocar um Label filho nele
                // como badge — mesmo truque usado em notificação de app.
                Label badge = new Label
                {
                    Text = "NEW",
                    AutoSize = false,
                    Size = new Size(34, 14),
                    Location = new Point(NAV_WIDTH - 42, 4),
                    Font = new Font("Segoe UI", 6.5F, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = ColorDanger,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand,
                };
                // Clique no selo deve se comportar igual clique no botão.
                badge.Click += (s, e) => SelectPage(capturedIndex);
                btn.Controls.Add(badge);
            }
            return btn;
        }
        private void SelectPage(int index)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                bool active = (i == index);
                pages[i].Visible = active;
                navButtons[i].BackColor = active ? ColorPrimary : ColorNavBg;
                navButtons[i].ForeColor = active ? Color.White : Color.FromArgb(203, 213, 225);
            }
        }
        // ══════════════════════════════════════════════════════════════════
        // BARRA DE STATUS/PROGRESSO (rodapé, visível em qualquer página)
        // ══════════════════════════════════════════════════════════════════
        private Panel BuildStatusBar()
        {
            Panel bar = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = Color.White, Padding = new Padding(10, 5, 10, 8) };
            Panel topBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorBorder };
            lblStatusBar = new Label
            {
                Text = "Ready.",
                Dock = DockStyle.Top,
                Height = 16,
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColorTextMuted,
            };
            progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Style = ProgressBarStyle.Continuous,
                MarqueeAnimationSpeed = 30,
            };
            bar.Controls.Add(progressBar);
            bar.Controls.Add(lblStatusBar);
            bar.Controls.Add(topBorder);
            return bar;
        }
        // Outras janelas (ex.: ChatAssistantForm) se inscrevem aqui pra
        // mostrar o mesmo progresso na própria tela delas — assim quem
        // disparou o Auto Drill pelo chat vê a barra ali, não só no rodapé
        // do painel principal que pode estar escondido atrás do NX.
        // O terceiro parâmetro (indeterminate) indica se é progresso "de
        // verdade" (percent confiável, barra contínua) ou só um aviso de
        // "estou trabalhando nisso" sem saber quanto falta (barra animada).
        public event Action<string, int, bool> ProgressChanged;
        // Atualiza a barra de status/progresso com um percentual CONHECIDO
        // (ex.: grupo 3/8 do contra-furo). Chame com percent entre 0 e 100.
        // O Application.DoEvents() é necessário porque as chamadas NXOpen
        // que geram o progresso de verdade rodam de forma síncrona na
        // thread de UI (ver histórico do travamento do Auto Drill) — sem
        // isso, a barra só "pintaria" depois que TUDO já tivesse
        // terminado, o que anula o propósito de mostrar progresso ao vivo.
        public void ReportProgress(string status, int percent)
        {
            int clamped = Math.Max(0, Math.Min(100, percent));
            if (lblStatusBar != null && progressBar != null)
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                lblStatusBar.Text = status;
                progressBar.Value = clamped;
            }
            if (ProgressChanged != null)
                ProgressChanged(status, clamped, false);
            Application.DoEvents();
        }
        // Usa isso pras etapas "caixa-preta" — onde a gente sabe que algo
        // está rodando (RECOGNIZE_FEATURES, FG_PECK_DRILL_ALL_HOLES, um
        // comando qualquer do chat, etc.) mas NÃO tem como saber o
        // percentual real, porque não temos acesso ao código-fonte dessas
        // classes pra instrumentar um evento de progresso como fizemos no
        // AUTOMATIC_COUNTERBORE.
        //
        // Regra: se a barra estava "zerada"/parada no início, pula direto
        // pro percentual de largada (ProgressBaselinePercent = 30%) em vez
        // de ficar em 0% parecendo que nada está acontecendo. Se já tinha
        // avançado mais que isso (ex.: uma etapa anterior do mesmo ciclo já
        // reportou progresso real), MANTÉM o valor atual — nunca deixa a
        // barra andar pra trás só porque a etapa seguinte não tem dado
        // fino.
        public void ReportProgressIndeterminate(string status)
        {
            int shown = ProgressBaselinePercent;
            if (lblStatusBar != null && progressBar != null)
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                if (progressBar.Value > ProgressBaselinePercent)
                    shown = progressBar.Value;
                lblStatusBar.Text = status;
                progressBar.Value = shown;
            }
            if (ProgressChanged != null)
                ProgressChanged(status, shown, true);
            Application.DoEvents();
        }
        // ══════════════════════════════════════════════════════════════════
        // HELPERS DE LAYOUT (usados por todas as páginas)
        // ══════════════════════════════════════════════════════════════════
        private void PageHeader(Panel page, string title, string subtitle)
        {
            Label t = new Label
            {
                Text = title,
                Location = new Point(PAD, 20),
                Size = new Size(CONTENT_WIDTH, 40),
                AutoSize = false,
                Font = new Font("Segoe UI Semibold", 13F, FontStyle.Bold),
                ForeColor = ColorTextPrimary,
            };
            Label s = new Label
            {
                Text = subtitle,
                Location = new Point(PAD, 62),
                Size = new Size(CONTENT_WIDTH, 48),
                AutoSize = false,
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = ColorTextMuted,
            };
            page.Controls.Add(t);
            page.Controls.Add(s);
        }
        private void SectionTitle(Panel page, string text, int x, int y)
        {
            // Barrinha de destaque (3×14, cor primária) antes do texto — dá
            // um ponto de ritmo visual repetido em toda seção da página, em
            // vez de só texto cinza sem nenhum acento de cor.
            Panel accent = new Panel { Location = new Point(x, y + 1), Size = new Size(3, 13), BackColor = ColorPrimary };
            Label lbl = new Label
            {
                Text = text,
                Location = new Point(x + 9, y),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                ForeColor = ColorTextSecondary,
            };
            page.Controls.Add(accent);
            page.Controls.Add(lbl);
        }
        private void FieldLabel(Panel page, string text, int x, int y)
        {
            Label lbl = new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold),
                ForeColor = ColorTextMuted,
            };
            page.Controls.Add(lbl);
        }
        private void Divider(Panel page, int x, int y, int w)
        {
            Panel line = new Panel { Location = new Point(x, y), Size = new Size(w, 1), BackColor = ColorBorder };
            page.Controls.Add(line);
        }
        private ComboBox MakeCombo(Panel page, int x, int y, int w)
        {
            ComboBox cb = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FormattingEnabled = true,
                Font = new Font("Segoe UI", 9F),
            };
            page.Controls.Add(cb);
            return cb;
        }
        private Button ActionButton(Panel page, string text, int x, int y, int w, int h, Color back, Color hover, EventHandler onClick)
        {
            Button btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = back,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = hover;
            if (onClick != null)
                btn.Click += onClick;
            page.Controls.Add(btn);
            ApplyRoundedCorners(btn, 6);
            return btn;
        }
        // Arredonda os cantos de um controle (botões, principalmente) via
        // Region — truque leve do WinForms que não exige nenhuma lib extra.
        // Chamar DEPOIS do controle já estar com o tamanho final definido
        // (Region é calculada em cima do Width/Height atual).
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
        // ══════════════════════════════════════════════════════════════════
        // PÁGINA: SETUP
        // ══════════════════════════════════════════════════════════════════
        private Panel BuildPageSetup()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "Setup", "Machine selection, fixtures, post-processors and tool library");
            int y = 116;
            SectionTitle(page, "SELECT MACHINE", PAD, y); y += 22;
            FieldLabel(page, "3X", PAD, y); y += 14;
            cmb3xMachines = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            FieldLabel(page, "4-AXIS", PAD, y); y += 14;
            cmb4AxisMachines = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            FieldLabel(page, "5-AXIS", PAD, y); y += 14;
            cmb5AxisMachines = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            FieldLabel(page, "LATHE", PAD, y); y += 14;
            cmbLatheMachines = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            FieldLabel(page, "MILL TURN MACHINE", PAD, y); y += 14;
            cmbMillTurnMachines = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            ActionButton(page, "Run Selected Machine", PAD, y, CONTENT_WIDTH, 32, ColorPrimary, ColorPrimaryHover, (s, e) => RunSelectedMachine()); y += 40;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            SectionTitle(page, "VISES / FIXTURES", PAD, y); y += 22;
            AddFixtureIcon(page, "vise", "Standard Vise", PAD, y, (s, e) => LoadAndAlignVise_Auto.Run(null));
            // "Self-Centering" já dispara o mesmo import da morsa nova (ver
            // botão logo abaixo) - clicar no ícone é um atalho pra mesma
            // ação, não só seleção visual como os outros 2 ícones ainda são.
            AddFixtureIcon(page, "self_centering", "Self-Centering", PAD + 60, y, (s, e) => RunImportVise());
            AddFixtureIcon(page, "fixture_plate", "Fixture Plate", PAD + 120, y);
            AddFixtureIcon(page, "custom", "Custom", PAD + 180, y);
            y += 76;
            // Prévia visual da morsa nova - Resources.Vise_Import (print que
            // você vai adicionar na pasta Resources do projeto, mesmo nome
            // da propriedade gerada). É só uma segunda forma de disparar a
            // MESMA ação do botão "Import Vise (.prt)" logo abaixo
            // (RunImportVise()) - não é um controle separado, não tem estado
            // de seleção própria (por isso não usa _fixtureIcons/AddFixtureIcon).
            PictureBox viseImportPreview = new PictureBox
            {
                Image = global::PATHNC.Properties.Resources.VISE_5AXIS,
                Location = new Point(PAD, y),
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
            };
            viseImportPreview.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColorBorder, 1))
                    e.Graphics.DrawRectangle(p, 0, 0, viseImportPreview.Width - 1, viseImportPreview.Height - 1);
            };
            viseImportPreview.Click += (s, e) => RunImportVise();
            Label viseImportCap = new Label
            {
                Text = "New Vise",
                Location = new Point(PAD - 6, y + 50),
                Size = new Size(60, 24),
                Font = new Font("Segoe UI", 6.5F),
                ForeColor = ColorTextMuted,
                TextAlign = ContentAlignment.TopCenter,
            };
            page.Controls.Add(viseImportPreview);
            page.Controls.Add(viseImportCap);
            y += 62;
            // Importa qualquer .prt de morsa/fixture pra montagem atual (via
            // ImportViseComponent.cs - genérico, não fica preso ao
            // "day_one_setup_vice_assmbly" hardcoded do LoadAndAlignVise_Auto.cs).
            // Pensado pra morsa nova (centered/self-centering vise) que ainda
            // vai ser inserida no projeto - só importa/insere, sem alinhar
            // automaticamente (ver aviso no topo do ImportViseComponent.cs
            // sobre por que o alinhamento é um passo separado pra esse tipo
            // de morsa).
            ActionButton(page, "Import Vise (.prt)", PAD, y, CONTENT_WIDTH, 34, ColorSuccess, ColorSuccessHover, (s, e) => RunImportVise()); y += 42;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            SectionTitle(page, "POST PROCESSORS", PAD, y); y += 22;
            FieldLabel(page, "POST PROCESSOR", PAD, y); y += 14;
            cmbPostProcessors = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            SectionTitle(page, "VERIFICATION", PAD, y); y += 22;
            ActionButton(page, "Check Toolpaths / Check Collision", PAD, y, CONTENT_WIDTH, 44, ColorWarning, ColorWarningHover, (s, e) => ShowNotImplemented("Check Toolpaths / Check Collision")); y += 52;
            ActionButton(page, "Simulate", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover, (s, e) => ShowNotImplemented("Simulate")); y += 42;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            SectionTitle(page, "TOOL LIBRARY", PAD, y); y += 22;
            ActionButton(page, "Import Tools", PAD, y, CONTENT_WIDTH, 34, ColorSuccess, ColorSuccessHover, (s, e) => ToolLibraryFromNX.Run(null)); y += 42;
            // ── O scaffold de grade CRUD que ficava aqui (comentado, com
            // ToolRecord/dgvTools/toolRecords manuais) foi substituído pela
            // tela real ToolDatabaseManagerForm.cs, que edita as 4 tabelas
            // (Materiais/Ferramentas/Roscas/Soquetes) direto no SQL Server
            // via ToolDatabase.cs - exatamente o "quando o banco de dados
            // real existir" que o comentário antigo previa. ──
            ActionButton(page, "Manage Tool / Material Database", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover, (s, e) => new ToolDatabaseManagerForm().ShowDialog(this)); y += 42;
            return page;
        }
        private void RunSelectedMachine()
        {
            if (cmb3xMachines.SelectedItem != null)
            {
                RunSelected3xMachine();
                return;
            }
            if (cmb4AxisMachines.SelectedItem != null || cmb5AxisMachines.SelectedItem != null
                || cmbLatheMachines.SelectedItem != null || cmbMillTurnMachines.SelectedItem != null)
            {
                ShowNotImplemented("Selected machine type");
                return;
            }
            MessageBox.Show("Select a machine.", "Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        // Abre um seletor de arquivo pra escolher o .prt da morsa (pensado
        // pra morsa nova/centered vise que ainda vai ser inserida no
        // projeto) e importa pra montagem atual via ImportViseComponent -
        // só insere o componente na montagem, NÃO roda nenhum alinhamento
        // automático (isso é um passo separado, específico da geometria de
        // cada morsa - ver aviso no topo do ImportViseComponent.cs).
        private void RunImportVise()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Select the vise .prt file";
                dlg.Filter = "NX Part Files (*.prt)|*.prt|All Files (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;
                ReportProgressIndeterminate("Importing vise component...");
                try
                {
                    ImportViseComponent.Run(new[] { dlg.FileName });
                    ReportProgress("Vise imported.", 100);
                }
                catch (Exception ex)
                {
                    ReportProgress("Error importing vise.", 0);
                    MessageBox.Show("Error importing vise:\n" + ex.Message, "Import Vise", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        // Placeholder pra botões ainda sem lógica NXOpen conectada. Deixa o
        // botão funcional (não quebrado/mudo) e explícito sobre o que falta.
        private void ShowNotImplemented(string feature)
        {
            MessageBox.Show(
                feature + " is not connected yet. Wire this button up to your NXOpen call in Form1.cs.",
                "Not implemented",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        // ── Ícones de vise/fixture: desenhados em código como placeholder.
        // Troque CreatePlaceholderIcon (ou a atribuição de pic.Image) pelas
        // imagens reais quando as tiver.
        //
        // CORRIGIDO: antes havia DUAS versões deste método (sobrecarga) - uma
        // com 5 parâmetros que só dava "throw new NotImplementedException()",
        // e essa aqui com 6. As chamadas sem handler de clique (self_centering,
        // fixture_plate, custom) batiam na versão de 5 parâmetros e estouravam
        // a exceção durante a construção da Form1 (dentro de Main()). Removida
        // a versão quebrada e o parâmetro onClick agora é opcional (default
        // null), caindo em ShowNotImplemented quando não informado. ──
        private void AddFixtureIcon(Panel page, string kind, string caption, int x, int y, Action<object, object> onClick = null)
        {
            PictureBox pic = new PictureBox
            {
                Image = CreatePlaceholderIcon(kind),
                Location = new Point(x, y),
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
            };
            pic.Paint += (s, e) =>
            {
                Color borderColor = (pic.BackColor == ColorSelectedTint) ? ColorPrimary : ColorBorder;
                using (Pen p = new Pen(borderColor, 2))
                    e.Graphics.DrawRectangle(p, 0, 0, pic.Width - 1, pic.Height - 1);
            };
            pic.Click += (s, e) =>
            {
                SelectFixtureIcon(pic);
                if (onClick != null)
                    onClick(s, e);
                else
                    ShowNotImplemented(caption);
            };
            Label cap = new Label
            {
                Text = caption,
                Location = new Point(x - 6, y + 50),
                Size = new Size(60, 24),
                Font = new Font("Segoe UI", 6.5F),
                ForeColor = ColorTextMuted,
                TextAlign = ContentAlignment.TopCenter,
            };
            page.Controls.Add(pic);
            page.Controls.Add(cap);
            _fixtureIcons.Add(pic);
        }
        private void SelectFixtureIcon(PictureBox selected)
        {
            foreach (PictureBox pic in _fixtureIcons)
            {
                pic.BackColor = (pic == selected) ? ColorSelectedTint : Color.White;
                pic.Invalidate();
            }
        }
        private Bitmap CreatePlaceholderIcon(string kind)
        {
            Bitmap bmp = new Bitmap(40, 40);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Pen pen = new Pen(ColorTextSecondary, 2))
                using (Brush brush = new SolidBrush(ColorTextMuted))
                {
                    switch (kind)
                    {
                        case "vise":
                            g.DrawRectangle(pen, 4, 8, 32, 8);
                            g.DrawRectangle(pen, 4, 24, 32, 8);
                            g.FillRectangle(brush, 15, 13, 10, 14);
                            break;
                        case "self_centering":
                            g.DrawEllipse(pen, 4, 4, 32, 32);
                            g.FillEllipse(brush, 14, 14, 12, 12);
                            break;
                        case "fixture_plate":
                            g.DrawRectangle(pen, 4, 4, 32, 32);
                            g.FillEllipse(brush, 7, 7, 5, 5);
                            g.FillEllipse(brush, 28, 7, 5, 5);
                            g.FillEllipse(brush, 7, 28, 5, 5);
                            g.FillEllipse(brush, 28, 28, 5, 5);
                            break;
                        default:
                            g.DrawRectangle(pen, 4, 4, 32, 32);
                            g.DrawLine(pen, 4, 4, 36, 36);
                            g.DrawLine(pen, 36, 4, 4, 36);
                            break;
                    }
                }
            }
            return bmp;
        }
        // ══════════════════════════════════════════════════════════════════
        // PÁGINA: FEATURE BASED MACHINING
        // ══════════════════════════════════════════════════════════════════
        private Panel BuildPageFBM()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "Feature Based Machining", "Automatic feature recognition and machining of holes, threads and pockets");
            int y = 116;
            SectionTitle(page, "SETUP", PAD, y); y += 22;
            ActionButton(page, "Find Features", PAD, y, CONTENT_WIDTH, 34, ColorWarning, ColorWarningHover, (s, e) => RECOGNIZE_FEATURES.Run(null)); y += 42;
            ActionButton(page, "Create Feature Group", PAD, y, CONTENT_WIDTH, 34, ColorSuccess, ColorSuccessHover, (s, e) => CREATE_GROUP_FEATURES.Run(null)); y += 42;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            SectionTitle(page, "HOLES AND THREADS", PAD, y); y += 22;
            FieldLabel(page, "DRILLS", PAD, y); y += 14;
            cmbDrills = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            FieldLabel(page, "METRIC TAP", PAD, y); y += 14;
            cmbMetricTap = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            FieldLabel(page, "SOCKET HEAD", PAD, y); y += 14;
            cmbSocketHead = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            ActionButton(page, "Run", PAD, y, CONTENT_WIDTH, 32, ColorPrimary, ColorPrimaryHover, (s, e) => RunHoleThreadStrategy()); y += 40;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            SectionTitle(page, "AUTOMATION", PAD, y); y += 22;
            ActionButton(page, "Classify Holes by Diameter", PAD, y, CONTENT_WIDTH, 44, ColorDanger, ColorDangerHover, (s, e) =>
            {
                TOP_RECOGNIZE_FEATURE.Run(null);
                CREATE_TOP_FEATURE_GROUP.Run(null);
                FIND_ALL_HOLES_BY_DIAMETER.Run(null);
            }); y += 52;
            ActionButton(page, "Auto Drill — Full Cycle", PAD, y, CONTENT_WIDTH, 44, ColorTeal, ColorTealHover, (s, e) => RunAutoDrillCycle()); y += 52;
            ActionButton(page, "Auto 2D Feature Machined — Full Cycle", PAD, y, CONTENT_WIDTH, 44, ColorTeal, ColorTealHover, (s, e) =>
            {
                FBM_ALL_FEATURES_MACHINED.Run(null);
            }); y += 52;
            // Reconhece os furos do work part (agrupando faces cilíndricas
            // coaxiais, não só por diâmetro solto) e classifica/pinta cada
            // um por CATEGORIA (rosca, alargador H7, soquete allen, coluna
            // de molde, ambíguo, normal) - é só uma checagem visual de QC
            // (não roda nenhuma operação/estratégia), útil pra conferir de
            // olho a classificação antes do Auto Drill/Counterbore rodar de
            // verdade. Era o botão "[TEST] Classify Holes by Category"
            // (classe COLOR_HOLES_BY_CATEGORY_TEST) - promovido a botão
            // oficial, substituindo a antiga classificação só por diâmetro
            // (HoleDiameterColorizer.ScanAndColorHolesByDiameterAuto).
            ActionButton(page, "Color Holes by Category (QC)", PAD, y, CONTENT_WIDTH, 44, ColorWarning, ColorWarningHover, (s, e) => RunColorizeHoles()); y += 52;
            // Desfaz a coloração acima - não existe "remover cor" de verdade
            // na API de display do NX pra face individual, então "descolorir"
            // na prática é forçar todas as faces cilíndricas de volta pro
            // mesmo cinza neutro (ver HoleDiameterColorizer.ResetHoleColorsToGray).
            ActionButton(page, "Uncolor Holes (gray)", PAD, y, CONTENT_WIDTH, 34, ColorNeutral, ColorNeutralHover, (s, e) => RunUncolorHoles()); y += 42;
            return page;
        }

        private void RunHoleThreadStrategy()
        {
            if (cmbMetricTap.SelectedItem is IMachiningStrategy strategy)
            {
                context.Execute(strategy.FeatureName);
            }
            else if (cmbSocketHead.SelectedItem is IMachiningStrategy socketStrategy)
            {
                context.Execute(socketStrategy.FeatureName);
            }
            else if (cmbDrills.SelectedItem is IMachiningStrategy drillStrategy)
            {
                context.Execute(drillStrategy.FeatureName);
            }
            else
            {
                MessageBox.Show("Select a strategy.", "FBM Machining", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        // Sequência completa do "Auto Drill" — reconhece as features, cria
        // os grupos, e roda toda a furação/rosca automática. Extraído pra
        // método público (em vez de ficar só dentro do lambda do botão) pra
        // poder ser chamado tanto pelo clique do botão quanto pelo gatilho
        // de texto do AI Assistant (ChatAssistantForm) — mesmo código, uma
        // única fonte de verdade.
        //
        // ATUALIZADO: as 4 etapas antigas de furação/contra-furo/rosca
        // (FG_CENTER_DRILL_ALL_HOLES, FG_PECK_DRILL_ALL_HOLES,
        // AUTOMATIC_COUNTERBORE, ALL_THREADS_COUNTERSINK) foram substituídas
        // por UMA chamada a AUTODRILL_GEOMETRIA_PROPRIA — que já faz tudo
        // isso num pipeline só (pergunta o material, centro -> furo por
        // diâmetro -> contra-furo -> rosca, com geometria própria por
        // operação e a furação da parte estreita sob o contra-furo que o
        // pipeline antigo deixava de fora). RECOGNIZE_FEATURES e
        // CREATE_GROUP_FEATURES continuam rodando antes, do jeito que já
        // rodavam, porque AUTODRILL_GEOMETRIA_PROPRIA ainda parte dos
        // grupos FG_STEP* que eles criam.
        //
        // Progresso: as 3 etapas são todas "caixa-preta" pra barra — a
        // gente só sabe o início e o fim de cada uma, nunca o meio — então
        // mostra a barra animada (Marquee) em vez de um percentual que não
        // significa nada.
        public void RunAutoDrillCycle()
        {
            string[] stepLabels =
            {
                "Recognizing features (RECOGNIZE_FEATURES)...",
                "Creating feature groups (CREATE_GROUP_FEATURES)...",
                "Drilling, counterboring and threading (AUTODRILL_GEOMETRIA_PROPRIA)...",
            };
            Action[] steps =
            {
                () => RECOGNIZE_FEATURES.Run(null),
                () => CREATE_GROUP_FEATURES.Run(null),
                () => AUTODRILL_GEOMETRIA_PROPRIA.Run(null),
            };
            try
            {
                for (int i = 0; i < steps.Length; i++)
                {
                    ReportProgressIndeterminate(stepLabels[i]);
                    steps[i]();
                }
                ReportProgress("Auto Drill completed.", 100);

                // Popup ilustrativo com quantos furos/operações foram
                // processados nessa execução (AUTODRILL_GEOMETRIA_PROPRIA
                // guarda o resumo em LastRunSummary antes de Run(null)
                // retornar). Modeless (Show, não ShowDialog) pra não travar
                // o NX se o usuário deixar aberto. Aparece tanto vindo do
                // clique do botão quanto do gatilho de texto no chat, porque
                // os dois caminhos passam por este mesmo método.
                if (AUTODRILL_GEOMETRIA_PROPRIA.LastRunSummary != null)
                {
                    new AutoDrillSummaryForm(AUTODRILL_GEOMETRIA_PROPRIA.LastRunSummary).Show(this);
                }
            }
            catch (Exception ex)
            {
                ReportProgress("Error in Auto Drill.", 0);
                MessageBox.Show("Error in Auto Drill:\n" + ex.Message, "Auto Drill", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // Reconhece os furos do work part (via COLOR_HOLES_BY_CATEGORY_TEST -
        // agrupamento por eixo/coaxialidade, não só diâmetro solto) e
        // classifica/pinta cada um por categoria (rosca, alargador H7,
        // soquete allen, coluna de molde, ambíguo, normal) — checagem visual
        // rápida de QC, não roda nenhuma operação/estratégia de usinagem.
        // Extraído pra método público (mesmo padrão do RunAutoDrillCycle) pra
        // poder ser chamado tanto pelo botão "Color Holes by Category (QC)"
        // quanto pelo gatilho de texto do AI Assistant (ChatAssistantForm).
        // (Antiga versão usava HoleDiameterColorizer.ScanAndColorHolesByDiameterAuto,
        // que só agrupava por diâmetro solto, sem checar coaxialidade nem
        // classificar por categoria - substituída por essa.)
        public void RunColorizeHoles()
        {
            NXOpen.Session theSession = NXOpen.Session.GetSession();
            NXOpen.Part workPart = theSession.Parts.Work;
            if (workPart == null)
            {
                MessageBox.Show("No part open.", "Color Holes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ReportProgressIndeterminate("Recognizing holes and classifying by category...");
            try
            {
                COLOR_HOLES_BY_CATEGORY_TEST.Run(new string[0]);
                ReportProgress("Holes colored by category.", 100);
            }
            catch (Exception ex)
            {
                ReportProgress("Error coloring holes.", 0);
                MessageBox.Show("Error coloring holes:\n" + ex.Message, "Color Holes", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // Desfaz o "Colorir Furos por Diâmetro (QC)" - varre as mesmas faces
        // cilíndricas (mesmo critério de assembly recursivo com fallback pro
        // Part direto) e força todas de volta pro mesmo cinza neutro, num
        // único undo mark. Extraído pra método público (mesmo padrão de
        // RunColorizeHoles/RunAutoDrillCycle) pra também poder ser chamado
        // pelo chat.
        public void RunUncolorHoles()
        {
            NXOpen.Session theSession = NXOpen.Session.GetSession();
            NXOpen.Part workPart = theSession.Parts.Work;
            if (workPart == null)
            {
                MessageBox.Show("No part open.", "Uncolor Holes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ReportProgressIndeterminate("Uncoloring holes (reverting to gray)...");
            try
            {
                int count = HoleDiameterColorizer.ResetHoleColorsToGray(theSession, workPart);
                ReportProgress("Holes uncolored: " + count + " face(s) reverted to gray.", 100);
            }
            catch (Exception ex)
            {
                ReportProgress("Error uncoloring holes.", 0);
                MessageBox.Show("Error uncoloring holes:\n" + ex.Message, "Uncolor Holes", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // ══════════════════════════════════════════════════════════════════
        // PÁGINA: 2D MILL STRATEGY
        // ══════════════════════════════════════════════════════════════════
        private Panel BuildPage2D()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "2D Mill Strategy", "Selection of 2D milling strategies for pockets, profiles and surfaces");
            int y = 116;
            SectionTitle(page, "STRATEGY", PAD, y); y += 22;
            FieldLabel(page, "POCKETS/SLOTS", PAD, y); y += 14;
            cmbPockets = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            FieldLabel(page, "PROFILES", PAD, y); y += 14;
            cmbProfiles = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            FieldLabel(page, "SURFACES", PAD, y); y += 14;
            cmbSurfaces = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            ActionButton(page, "Run", PAD, y, CONTENT_WIDTH, 32, ColorPrimary, ColorPrimaryHover, (s, e) => RunFbmStrategy());
            return page;
        }
        private void RunFbmStrategy()
        {
            if (cmbPockets.SelectedItem is IMachiningStrategy pocketStrategy)
            {
                context.Execute(pocketStrategy.FeatureName);
            }
            else if (cmbProfiles.SelectedItem is IMachiningStrategy profileStrategy)
            {
                context.Execute(profileStrategy.FeatureName);
            }
            else if (cmbSurfaces.SelectedItem is IMachiningStrategy facesStrategy)
            {
                context.Execute(facesStrategy.FeatureName);
            }
            else
            {
                MessageBox.Show("Select a Strategy.", "FBM Machining", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        // ══════════════════════════════════════════════════════════════════
        // PÁGINA: 3-AXIS MILLING
        // ══════════════════════════════════════════════════════════════════
        private Panel BuildPage3D()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "3-Axis Milling", "Roughing, finishing and facing strategies in 3 axes");
            int y = 116;
            SectionTitle(page, "ROUGHING", PAD, y); y += 22;
            ActionButton(page, "Adaptive Mill", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover, (s, e) => TOP_ADAPTIVE_MILL.Run(null)); y += 42;
            ActionButton(page, "Cavity Mill", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover, (s, e) => ROUGHINTERACTIVE.Run()); y += 42;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            SectionTitle(page, "FINISHING", PAD, y); y += 22;
            ActionButton(page, "ZLevel Profile", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover, (s, e) => ZLEVELINTERACTIVE.Run(null)); y += 42;
            ActionButton(page, "Raster", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover, (s, e) => RASTERINTERACTIVE.Run(null)); y += 42;
            ActionButton(page, "Finishing Face", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover, (s, e) => FINISHINGFACE.Run(null)); y += 42;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            SectionTitle(page, "FACING", PAD, y); y += 22;
            ActionButton(page, "Facing Mill (Rough + Finish)", PAD, y, CONTENT_WIDTH, 44, ColorPrimary, ColorPrimaryHover, (s, e) =>
            {
                FBM_RECTANGULAR_PLANAR_ROUGH.Run(null);
                FBM_RECTANGULAR_PLANAR_FINISH.Run(null);
            }); y += 52;
            return page;
        }
        private void RunSelected3xMachine()
        {
            switch (cmb3xMachines.SelectedItem)
            {
                case THREE_AXIS_MILLING_FANUC _:
                    THREE_AXIS_MILLING_FANUC.Run(null);
                    break;
                case THREE_AXIS_MILLING_SIEMENS _:
                    THREE_AXIS_MILLING_SIEMENS.Run(null);
                    break;
                case THREE_AXIS_MILLING_HEIDENHAIN _:
                    THREE_AXIS_MILLING_HEIDENHAIN.Run(null);
                    break;
                case THREE_AXIS_MILLING_GENERIC _:
                    THREE_AXIS_MILLING_GENERIC.Run(null);
                    break;
                default:
                    MessageBox.Show("Select a machine.", "3-Axis Machining", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }
        }
        // ══════════════════════════════════════════════════════════════════
        // PÁGINA: 3+2 AXIS
        // ══════════════════════════════════════════════════════════════════
        // Duas seções:
        //   - WORKPLANES: os 5 WCS/MCS de direção cardeal fixa (ex-TOP_VIEW.cs /
        //     FRONT_VIEW.cs / BACK_VIEW.cs / LEFT_VIEW.cs / RIGHT_VIEW.cs, pasta
        //     WORKPLANES/, namespace NX_3_PLUS_TWO_WCS) - um botão por direção
        //     mais um "Create All" que roda as 5 em sequência (Top → Front →
        //     Back → Right → Left). Úteis pra criar/inspecionar um WCS
        //     específico na mão, ou pular a direção que fica presa na morsa.
        //   - PIPELINE STAGES: pipeline 3+2 (TEMPLATES_3+2/) construído nesta
        //     mesma sessão - detecta as direções de acesso reais da peça
        //     sozinho (não fica limitado às 5 direções cardeais acima). Cada
        //     estágio é uma classe própria e independente (THREE_PLUS_TWO_
        //     ROUGH.cs / THREE_PLUS_TWO_REST_MILL.cs / THREE_PLUS_TWO_
        //     SEMI_FINISH.cs, todas usando o núcleo compartilhado THREE_
        //     PLUS_TWO_COMMON.cs) - 3 botões separados, um por classe, pra
        //     não travar a UI esperando TODAS as etapas de TODAS as direções
        //     de uma vez só - mais um botão discreto "Run All Stages" que
        //     chama THREE_PLUS_TWO_PIPELINE.cs (orquestrador fino que só
        //     encadeia as 3 classes de estágio em sequência).
        private Panel BuildPage3Plus2Axis()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "3+2 Axis", "Automatic WCS positioning for multiple table orientations");
            int y = 116;
            // ── MACHINE ──────────────────────────────────────────────────
            // AVISO: reaproveita a MESMA lógica e as MESMAS classes da combo
            // "3X" da página Setup (THREE_AXIS_MILLING_SIEMENS/FANUC/
            // HEIDENHAIN + RunSelected3xMachine) - igual ao que já acontece
            // com 4-AXIS/5-AXIS/LATHE/MILL TURN MACHINE lá, que também ainda
            // não têm classe própria. Ainda NÃO existe uma classe de
            // máquina/post-processor ESPECÍFICA pra 3+2 Axis, então por
            // enquanto os 3 itens abaixo rodam o journal do 3-Axis como
            // placeholder. TROQUE THREE_AXIS_MILLING_SIEMENS/FANUC/
            // HEIDENHAIN abaixo (e em RunSelected3Plus2Machine) por classes
            // próprias de 3+2 assim que existirem.
            // Ícones: Resources.Machine_Siemens / Machine_Fanuc /
            // Machine_Heidenhain - prints que você vai adicionar na pasta
            // Resources do projeto (mesmo nome da propriedade gerada).
            SectionTitle(page, "MACHINE", PAD, y); y += 22;
            //  AddMachineIcon(page, global::PATHNC.Properties.Resources.Machine_Siemens, "Siemens", PAD, y,
            //     () => SelectMachineComboItem(typeof(THREE_AXIS_MILLING_SIEMENS)));
            //  AddMachineIcon(page, global::PATHNC.Properties.Resources.Machine_Fanuc, "Fanuc", PAD + 60, y,
            // () => SelectMachineComboItem(typeof(THREE_AXIS_MILLING_FANUC)));
            // AddMachineIcon(page, global::PATHNC.Properties.Resources.Machine_Heidenhain, "Heidenhain", PAD + 120, y,
            //  () => SelectMachineComboItem(typeof(THREE_AXIS_MILLING_HEIDENHAIN)));
            y += 76;
            FieldLabel(page, "CONTROLLER", PAD, y); y += 14;
            cmb3Plus2Machines = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            ActionButton(page, "Run Selected Machine", PAD, y, CONTENT_WIDTH, 32, ColorPrimary, ColorPrimaryHover,
                (s, e) => RunSelected3Plus2Machine()); y += 40;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;

            SectionTitle(page, "VISE", PAD, y); y += 22;
            ActionButton(page, "Load && Align Centered Vise", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover,
                (s, e) => LoadAndAlignCenteredVise_Auto.Run(null)); y += 42;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            SectionTitle(page, "WORKPLANES", PAD, y); y += 22;
            Label workplanesHint = new Label
            {
                Text = "Creates the fixed-axis WCS/MCS used by the pipeline below "
                     + "(tool axis pointing straight out of each face). Skip the "
                     + "direction that stays clamped in the vise.",
                Location = new Point(PAD, y),
                Size = new Size(CONTENT_WIDTH, 40),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColorTextMuted,
            };
            page.Controls.Add(workplanesHint); y += 46;
            ActionButton(page, "Top", PAD, y, CONTENT_WIDTH, 30, ColorPrimary, ColorPrimaryHover, (s, e) => TOP_VIEW.Run(null)); y += 36;
            ActionButton(page, "Front", PAD, y, CONTENT_WIDTH, 30, ColorPrimary, ColorPrimaryHover, (s, e) => FRONT_VIEW.Run(null)); y += 36;
            ActionButton(page, "Back", PAD, y, CONTENT_WIDTH, 30, ColorPrimary, ColorPrimaryHover, (s, e) => BACK_VIEW.Run(null)); y += 36;
            ActionButton(page, "Right", PAD, y, CONTENT_WIDTH, 30, ColorPrimary, ColorPrimaryHover, (s, e) => RIGHT_VIEW.Run(null)); y += 36;
            ActionButton(page, "Left", PAD, y, CONTENT_WIDTH, 30, ColorPrimary, ColorPrimaryHover, (s, e) => LEFT_VIEW.Run(null)); y += 44;
            ActionButton(page, "Create All (Top → Front → Back → Right → Left)", PAD, y, CONTENT_WIDTH, 44, ColorTeal, ColorTealHover,
                (s, e) => RunAllWorkplanes()); y += 52;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;

            SectionTitle(page, "PIPELINE STAGES", PAD, y); y += 22;
            Label pipelineHint = new Label
            {
                Text = "Detects the part's real access directions on its own (not "
                     + "limited to the 5 workplanes above). Run one stage at a time - "
                     + "each button only waits for its own toolpath generation, so "
                     + "you can check the result before starting the next one.",
                Location = new Point(PAD, y),
                Size = new Size(CONTENT_WIDTH, 56),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColorTextMuted,
            };
            page.Controls.Add(pipelineHint); y += 62;
            ActionButton(page, "1. Rough (Cavity Mill)", PAD, y, CONTENT_WIDTH, 38, ColorPrimary, ColorPrimaryHover,
                (s, e) => THREE_PLUS_TWO_ROUGH.Run(null)); y += 46;
            ActionButton(page, "2. Rest Mill", PAD, y, CONTENT_WIDTH, 38, ColorWarning, ColorWarningHover,
                (s, e) => THREE_PLUS_TWO_REST_MILL.Run(null)); y += 46;
            ActionButton(page, "3. Semi-Finish (ZLevel)", PAD, y, CONTENT_WIDTH, 38, ColorSuccess, ColorSuccessHover,
                (s, e) => THREE_PLUS_TWO_SEMI_FINISH.Run(null)); y += 46;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            // Opção combinada (as 3 etapas em sequência, igual antes) - fica
            // como alternativa mais discreta, não mais o caminho principal,
            // já que é justamente o botão único que estava demorando demais.
            ActionButton(page, "Run All Stages (Rough → Rest Mill → Semi-Finish)", PAD, y, CONTENT_WIDTH, 34, ColorTeal, ColorTealHover,
                (s, e) => THREE_PLUS_TWO_PIPELINE.Run(null)); y += 42;
            return page;
        }
        // Cria os 5 WCS/MCS de acesso 3+2 (Top/Front/Back/Right/Left) em
        // sequência, na mesma ordem usada pelo TEMPLATE_3+2_EXAMPLE_01.cs
        // original. Extraído pra método público (mesmo padrão de
        // RunAutoDrillCycle/RunColorizeHoles) pra poder ser chamado tanto pelo
        // botão "Create All" quanto, no futuro, por um gatilho de texto do AI
        // Assistant sem duplicar a sequência.
        public void RunAllWorkplanes()
        {
            string[] stepLabels =
            {
                "Creating Top workplane...",
                "Creating Front workplane...",
                "Creating Back workplane...",
                "Creating Right workplane...",
                "Creating Left workplane...",
            };
            Action[] steps =
            {
                () => TOP_VIEW.Run(null),
                () => FRONT_VIEW.Run(null),
                () => BACK_VIEW.Run(null),
                () => RIGHT_VIEW.Run(null),
                () => LEFT_VIEW.Run(null),
            };
            try
            {
                for (int i = 0; i < steps.Length; i++)
                {
                    ReportProgressIndeterminate(stepLabels[i]);
                    steps[i]();
                }
                ReportProgress("All 5 workplanes created (Top, Front, Back, Right, Left).", 100);
            }
            catch (Exception ex)
            {
                ReportProgress("Error creating workplanes.", 0);
                MessageBox.Show("Error creating workplanes:\n" + ex.Message, "Workplanes", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddMachineIcon(Panel page, Image img, string caption, int x, int y, Action onSelect)
        {
            PictureBox pic = new PictureBox
            {
                Image = img,
                Location = new Point(x, y),
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.White,
                Cursor = Cursors.Hand,
            };
            pic.Paint += (s, e) =>
            {
                Color borderColor = (pic.BackColor == ColorSelectedTint) ? ColorPrimary : ColorBorder;
                using (Pen p = new Pen(borderColor, 2))
                    e.Graphics.DrawRectangle(p, 0, 0, pic.Width - 1, pic.Height - 1);
            };
            pic.Click += (s, e) =>
            {
                SelectMachineIcon(pic);
                if (onSelect != null)
                    onSelect();
            };
            Label cap = new Label
            {
                Text = caption,
                Location = new Point(x - 6, y + 50),
                Size = new Size(60, 24),
                Font = new Font("Segoe UI", 6.5F),
                ForeColor = ColorTextMuted,
                TextAlign = ContentAlignment.TopCenter,
            };
            page.Controls.Add(pic);
            page.Controls.Add(cap);
            _machineIcons.Add(pic);
        }
        private void SelectMachineIcon(PictureBox selected)
        {
            foreach (PictureBox pic in _machineIcons)
            {
                pic.BackColor = (pic == selected) ? ColorSelectedTint : Color.White;
                pic.Invalidate();
            }
        }
        // Seleciona, na combo CONTROLLER, o item cujo tipo concreto bate com
        // machineType - usado tanto pelo clique nos ícones (AddMachineIcon)
        // quanto poderia ser reaproveitado por outro gatilho futuro.
        private void SelectMachineComboItem(Type machineType)
        {
            if (cmb3Plus2Machines == null) return;
            foreach (object item in cmb3Plus2Machines.Items)
            {
                if (machineType.IsInstanceOfType(item))
                {
                    cmb3Plus2Machines.SelectedItem = item;
                    return;
                }
            }
        }
        // AVISO: mesmo esquema de RunSelected3xMachine (página Setup), só
        // que com as 3 máquinas reaproveitadas do 3-Axis como placeholder -
        // troque pelos cases de classes próprias de 3+2 quando existirem.
        private void RunSelected3Plus2Machine()
        {
            switch (cmb3Plus2Machines.SelectedItem)
            {
                case THREE_AXIS_MILLING_SIEMENS _:
                    THREE_AXIS_MILLING_SIEMENS.Run(null);
                    break;
                case THREE_AXIS_MILLING_FANUC _:
                    THREE_AXIS_MILLING_FANUC.Run(null);
                    break;
                case THREE_AXIS_MILLING_HEIDENHAIN _:
                    THREE_AXIS_MILLING_HEIDENHAIN.Run(null);
                    break;
                default:
                    MessageBox.Show("Select a machine.", "3+2 Axis Machine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }
        }
        private Panel BuildPageMoldWizard()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "Mold Wizard", "Automation of core/cavity and mold base components");
            int y = 116;
            // ── CORE / CAVITY ─────────────────────────────────────────────
            SectionTitle(page, "CORE / CAVITY MACHINING", PAD, y); y += 22;
            ActionButton(page, "Rough Wizard", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover,
                (s, e) => MOLD_WIZARD_ROUGH.Run(null)); y += 42;
            ActionButton(page, "Rest Mill", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover,
                (s, e) => MOLD_WIZARD_REST_MILL.Run(null)); y += 42;
            ActionButton(page, "Finish Wizard", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover,
                (s, e) => MOLD_WIZARD_FINISH.Run(null)); y += 42;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            // Botão combinado — dispara o ciclo completo (rough + rest +
            // finish) numa cavidade/macho inteiro. Nome segue o mesmo
            // padrão dos botões "Ciclo Completo" já usados na página FBM.
            ActionButton(page, "Auto Cycle — Core & Cavity", PAD, y, CONTENT_WIDTH, 44, ColorTeal, ColorTealHover, (s, e) =>
            {
                // TODO: plugar a sequência real quando as classes existirem, ex.:
                // MOLD_ROUGH_WIZARD.Run(null);
                // MOLD_REST_MILL.Run(null);
                // MOLD_FINISH_WIZARD.Run(null);
                MOLD_ALTO_CICLE_ROUGH_FINISH.Run(null);
            }); y += 52;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            // ── MOLD BASE ────────────────────────────────────────────────
            SectionTitle(page, "MOLD BASE", PAD, y); y += 22;
            FieldLabel(page, "BASE PLATES", PAD, y); y += 14;
            ActionButton(page, "Top Base Plate", PAD, y, CONTENT_WIDTH, 30, ColorSuccess, ColorSuccessHover,
                (s, e) => ShowNotImplemented("Top Base Plate")); y += 36;
            ActionButton(page, "Bottom Base Plate", PAD, y, CONTENT_WIDTH, 30, ColorSuccess, ColorSuccessHover,
                (s, e) => ShowNotImplemented("Bottom Base Plate")); y += 42;
            FieldLabel(page, "TOOLING PLATES", PAD, y); y += 14;
            ActionButton(page, "Cavity Retainer Plate", PAD, y, CONTENT_WIDTH, 30, ColorSuccess, ColorSuccessHover,
                (s, e) => ShowNotImplemented("Cavity Retainer Plate")); y += 42;
            FieldLabel(page, "EJECTOR SYSTEM", PAD, y); y += 14;
            ActionButton(page, "Ejector Plate", PAD, y, CONTENT_WIDTH, 30, ColorSuccess, ColorSuccessHover,
                (s, e) => ShowNotImplemented("Ejector Plate")); y += 36;
            ActionButton(page, "Pin Retainer Plate", PAD, y, CONTENT_WIDTH, 30, ColorSuccess, ColorSuccessHover,
                (s, e) => ShowNotImplemented("Pin Retainer Plate")); y += 42;
            FieldLabel(page, "SPECIAL COMPONENTS", PAD, y); y += 14;
            ActionButton(page, "Inserts", PAD, y, CONTENT_WIDTH, 30, ColorWarning, ColorWarningHover,
                (s, e) => ShowNotImplemented("Inserts")); y += 36;
            ActionButton(page, "Slides", PAD, y, CONTENT_WIDTH, 30, ColorWarning, ColorWarningHover,
                (s, e) => ShowNotImplemented("Slides")); y += 36;
            ActionButton(page, "Electrodes", PAD, y, CONTENT_WIDTH, 30, ColorWarning, ColorWarningHover,
                (s, e) => ShowNotImplemented("Electrodes")); y += 36;
            return page;
        }

        // PÁGINAS "EM DESENVOLVIMENTO" (3+2 e 5-Axis)

        private Panel BuildPageComingSoon(string title, string subtitle, string message)
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg };
            PageHeader(page, title, subtitle);
            Label badge = new Label
            {
                Text = "IN DEVELOPMENT",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold),
                ForeColor = ColorWarningHover,
                BackColor = Color.FromArgb(254, 243, 199),
                Padding = new Padding(8, 3, 8, 3),
                Location = new Point(PAD, 116),
            };
            page.Controls.Add(badge);
            Label msg = new Label
            {
                Text = message,
                Location = new Point(PAD, 150),
                Size = new Size(CONTENT_WIDTH, 180),
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColorTextSecondary,
            };
            page.Controls.Add(msg);
            return page;
        }

        // PÁGINA: TEMPLATES

        private Panel BuildPageTemplates()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "Templates", "Complete example workflows — click a template to run the entire machining sequence");
            AddTemplateCard(page, "Prismatic Part", global::PATHNC.Properties.Resources.Screenshot_2026_07_03_183052, (s, e) =>
            {
                RECOGNIZE_FEATURES.Run(null);
                CREATE_GROUP_FEATURES.Run(null);
                TOP_ADAPTIVE_MILL.Run(null);
                FBM_PLANAR_SURFACE_FLOOR_FINISH.Run(null);
                FBM_PLANAR_SURFACE_WALL_FINISH.Run(null);
                FBM_SLOT_PARTIAL_RECTANGULAR_FLOOR_FINISH.Run(null);
                FBM_SLOT_PARTIAL_RECTANGULAR_WALL_FINISH.Run(null);
                M8.Run(null);
                FG_STEP2HOLE_THROUGH.Run(null);
                TOP_PLANAR_DEBUR_DIAM_12MM.Run(null);
            });
            AddTemplateCard(page, "3+2 Axis Example", global::PATHNC.Properties.Resources.Screenshot_2026_07_03_200950, (s, e) =>
            {
                THREE_PLUS_TWO_PIPELINE.Run(null);

            });
            AddTemplateCard(page, "Mold Plate P1", global::PATHNC.Properties.Resources.Screenshot_2026_07_07_194539, (s, e) =>
            {
                FBM_RECTANGULAR_POCKET_ROUGH.Run(null);
                FBM_RECTANGULAR_POCKET_REST_MILL.Run(null);
                FBM_RECTANGULAR_POCKET_FLOOR_FINISH.Run(null);
                FBM_RECTANGULAR_POCKET_WALL_FINISH.Run(null);
                FBM_POCKET_OPEN_ROUGH.Run(null);
                FBM_POCKET_OPEN_FLOOR_FINISH.Run(null);
                FBM_POCKET_OPEN_WALL_FINISH.Run(null);
                FBM_POCKET_OBROUND_STRAIGHT_ROUGH.Run(null);
                FBM_POCKET_OBROUND_STRAIGHT_FLOOR_FINISH.Run(null);
                FBM_POCKET_OBROUND_STRAIGHT_WALL_FINISH.Run(null);
                FIND_ALL_HOLES_BY_DIAMETER.Run(null);
                FIND_ALL_HOLES_CENTER_DRILL.Run(null);
            });
            AddTemplateCard(page, "Plate with Holes", global::PATHNC.Properties.Resources.Screenshot_2026_07_11_085227, (s, e) =>
            {
                FBM_RECTANGULAR_PLANAR_ROUGH.Run(null);
                FBM_RECTANGULAR_PLANAR_FINISH.Run(null);
                ZLEVELINTERACTIVE.Run(null);
                FIND_ALL_HOLES_CENTER_DRILL.Run(null);
            });
            AddTemplateCard(page, "Feature Group 02", global::PATHNC.Properties.Resources.Screenshot_2026_07_12_100257, (s, e) =>
            {
                FBM_PLANAR_SURFACE_ROUGH.Run(null);
                FBM_PLANAR_ROUND_FLOOR_FINISH.Run(null);
                FBM_PLANAR_SURFACE_WALL_FINISH.Run(null);
            });
            return page;
        }
        private void AddTemplateCard(Panel page, string caption, Image img, EventHandler handler)
        {
            int cardW = CONTENT_WIDTH, cardH = 150, gap = 12;
            int x = PAD;
            int y = 116 + _templateCardIndex * (cardH + gap);
            _templateCardIndex++;
            Panel card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(cardW, cardH),
                BackColor = Color.White,
                Cursor = Cursors.Hand,
            };

            bool hovering = false;
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(hovering ? ColorPrimary : ColorBorder, hovering ? 2 : 1))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };
            EventHandler onEnter = (s, e) => { hovering = true; card.Invalidate(); };
            EventHandler onLeave = (s, e) => { hovering = false; card.Invalidate(); };
            card.MouseEnter += onEnter;
            card.MouseLeave += onLeave;
            PictureBox pic = new PictureBox
            {
                Image = img,
                Location = new Point(1, 1),
                Size = new Size(cardW - 2, 108),
                SizeMode = PictureBoxSizeMode.StretchImage,
                Cursor = Cursors.Hand,
            };
            Label capLbl = new Label
            {
                Text = caption,
                Location = new Point(8, 112),
                Size = new Size(cardW - 16, 34),
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = ColorTextPrimary,
            };

            pic.MouseEnter += onEnter;
            pic.MouseLeave += onLeave;
            capLbl.MouseEnter += onEnter;
            capLbl.MouseLeave += onLeave;
            pic.Click += handler;
            card.Click += handler;
            capLbl.Click += handler;
            card.Controls.Add(pic);
            card.Controls.Add(capLbl);
            page.Controls.Add(card);
        }
        private Panel BuildPageStrategyAdvisor()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "Strategy Advisor",
                "Choose a ready-made strategy based on the geometry identified on the part");
            // TODO: trocar as imagens abaixo pelas ilustrações reais de cada
            // estratégia (adicionar como Resource, mesmo esquema usado nos
            // cards de Templates).
            AddStrategyCard(page, "Corner Notch Straight", global::PATHNC.Properties.Resources.CORNER_NOTCH_STRAIGHT,
               (s, e) => WIZARD_CORNER_NOTCH_STRAIGHT.Run(null));
            AddStrategyCard(page, "Open Pocket", global::PATHNC.Properties.Resources.OPEN_POCKET,
               (s, e) => WIZARD_HOLE_RECTANGULAR_STRAIGHT.Run(null));
            AddStrategyCard(page, "Counterbore", global::PATHNC.Properties.Resources.COUNTERBORE_ROLAMENTO,
               (s, e) => WIZARD_CTBORE_STANDARD.Run(null));
            AddStrategyCard(page, "Through Hole Drilling", null,
                (s, e) => ShowNotImplemented("Through Hole Drilling"));
            AddStrategyCard(page, "Blind Hole", null,
                (s, e) => ShowNotImplemented("Blind Hole"));
            AddStrategyCard(page, "Deburring", null,
                (s, e) => ShowNotImplemented("Deburring"));
            AddStrategyCard(page, "Pocket Roughing", null,
                (s, e) => ShowNotImplemented("Pocket Roughing"));
            AddStrategyCard(page, "Pocket Finishing", null,
                (s, e) => ShowNotImplemented("Pocket Finishing"));
            AddStrategyCard(page, "ZLevel ZigZag — Open Faces", null,
                (s, e) => ShowNotImplemented("ZLevel ZigZag"));
            return page;
        }

        private void AddStrategyCard(Panel page, string caption, Image img, EventHandler onApply)
        {
            int cardW = CONTENT_WIDTH, cardH = 190, gap = 12;
            int x = PAD;
            int y = 116 + _strategyCardIndex * (cardH + gap);
            _strategyCardIndex++;
            Panel card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(cardW, cardH),
                BackColor = Color.White,
            };
            card.Paint += (s, e) =>
            {
                using (Pen p = new Pen(ColorBorder))
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
            };
            PictureBox pic = new PictureBox
            {
                Image = img,
                Location = new Point(1, 1),
                Size = new Size(cardW - 2, 108),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.FromArgb(241, 245, 249),
            };
            Label capLbl = new Label
            {
                Text = caption,
                Location = new Point(8, 112),
                Size = new Size(cardW - 16, 34),
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = ColorTextPrimary,
            };
            Button btnApply = new Button
            {
                Text = "Apply",
                Location = new Point(8, cardH - 40),
                Size = new Size(cardW - 16, 30),
                BackColor = ColorPrimary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            btnApply.FlatAppearance.BorderSize = 0;
            btnApply.FlatAppearance.MouseOverBackColor = ColorPrimaryHover;
            if (onApply != null)
                btnApply.Click += onApply;
            card.Controls.Add(pic);
            card.Controls.Add(capLbl);
            card.Controls.Add(btnApply);
            ApplyRoundedCorners(btnApply, 5);
            page.Controls.Add(card);
        }

        // PÁGINA: WORKPLANES

        private Panel BuildPageWorkplanes()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "Workplanes",
                "Creation of WCS from cylindrical or block geometry (bounding box)");
            int y = 116;
            // ── BLOCK ────────────────────────────────────────────────────
            SectionTitle(page, "BLOCK", PAD, y); y += 22;
            ActionButton(page, "Top of Block", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover,
                (s, e) => WORKPLANE_TOP_BLOCK.Run(null)); y += 42;
            ActionButton(page, "Right Side", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover,
                (s, e) => ShowNotImplemented("Workplane — Right Side")); y += 42;
            ActionButton(page, "Left Side", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover,
                (s, e) => ShowNotImplemented("Workplane — Left Side")); y += 42;
            ActionButton(page, "Base (Bottom)", PAD, y, CONTENT_WIDTH, 34, ColorPrimary, ColorPrimaryHover,
                (s, e) => WORKPLANE_BOTTOM_BLOCK.Run(null)); y += 42;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            // ── CYLINDRICAL ──────────────────────────────────────────────
            SectionTitle(page, "CYLINDRICAL", PAD, y); y += 22;
            ActionButton(page, "Top of Cylinder", PAD, y, CONTENT_WIDTH, 34, ColorTeal, ColorTealHover,
                (s, e) => ShowNotImplemented("Workplane — Top of Cylinder")); y += 42;
            ActionButton(page, "Bottom of Cylinder", PAD, y, CONTENT_WIDTH, 34, ColorTeal, ColorTealHover,
                (s, e) => ShowNotImplemented("Workplane — Bottom of Cylinder")); y += 42;
            return page;
        }

        // ══════════════════════════════════════════════════════════════════
        // PÁGINA: SHOP DOCUMENTATION
        // Requer: ShopDocDatabase.cs no projeto + arquivo
        // PathNCAutomationDB.connection ao lado da DLL (ou variável de
        // ambiente PATHNC_DB_CONNECTION).
        // ══════════════════════════════════════════════════════════════════

        private static readonly string[] ShopDocLangCodes = { "PT", "EN", "DE", "ES", "FR", "TR", "ZH", "NL" };

        private Panel BuildPageShopDocumentation()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "Shop Documentation",
                "Automatic Setup Sheet generation (tools, dimensions, toolpath images)");
            int y = 116;

            // ── LANGUAGE ─────────────────────────────────────────────────
            SectionTitle(page, "LANGUAGE", PAD, y); y += 22;
            cmbShopDocLanguage = MakeCombo(page, PAD, y, CONTENT_WIDTH); y += 30;
            cmbShopDocLanguage.Items.AddRange(new object[] {
                "Português (PT)",
                "English (EN)",
                "Deutsch (DE)",
                "Español (ES)",
                "Français (FR)",
                "Türkçe (TR)",
                "中文 (ZH)",
                "Nederlands (NL)",
            });
            cmbShopDocLanguage.SelectedIndex = 0;
            y += 10;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;

            // ── SETUP SHEET ──────────────────────────────────────────────
            SectionTitle(page, "SETUP SHEET", PAD, y); y += 22;
            ActionButton(page, "Generate Setup Sheet", PAD, y, CONTENT_WIDTH, 40, ColorPrimary, ColorPrimaryHover,
                (s, e) => RunSelectedShopDocumentation()); y += 48;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;

            // ── BATCH & DATABASE ─────────────────────────────────────────
            SectionTitle(page, "BATCH & DATABASE", PAD, y); y += 22;
            ActionButton(page, "Batch Process (Multiple Parts)", PAD, y, CONTENT_WIDTH, 34, ColorTeal, ColorTealHover,
                (s, e) => ShowNotImplemented("Shop Documentation — Batch Process")); y += 42;
            ActionButton(page, "Save to Database (SQL)", PAD, y, CONTENT_WIDTH, 34, ColorTeal, ColorTealHover,
                (s, e) => SaveShopDocToDatabase()); y += 42;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;

            // ── CUSTOMIZATION (placeholder - implementar depois) ─────────
            SectionTitle(page, "CUSTOMIZATION", PAD, y); y += 22;
            ActionButton(page, "Client Branding Settings", PAD, y, CONTENT_WIDTH, 34, ColorSuccess, ColorSuccessHover,
                (s, e) => ShowNotImplemented("Shop Documentation — Client Branding")); y += 42;
            ActionButton(page, "Custom Template Editor", PAD, y, CONTENT_WIDTH, 34, ColorSuccess, ColorSuccessHover,
                (s, e) => ShowNotImplemented("Shop Documentation — Custom Template")); y += 42;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;

            // ── ABOUT ────────────────────────────────────────────────────
            SectionTitle(page, "ABOUT", PAD, y); y += 22;
            Label shopDocInfoLabel = new Label
            {
                Text = "Generates a complete, print-ready Setup Sheet (5+ pages, A4 landscape) "
                     + "with: tool list and detailed tool drawings, raw material and part "
                     + "dimensions, fixture/origin views, toolpath operations table, and "
                     + "machining progress (IPW) images. The HTML file opens automatically "
                     + "and is saved to your Documents\\ShopDocumentation folder. "
                     + "Save to Database stores the setup (operations, tools, feeds, time) "
                     + "and the generated HTML in PathNCAutomationDB with automatic revisioning.",
                Location = new Point(PAD, y),
                Size = new Size(CONTENT_WIDTH, 110),
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 9f),
            };
            page.Controls.Add(shopDocInfoLabel);
            y += 120;
            return page;
        }

        // Dispara a classe SHOP_DOCUMENTATION_XX certa de acordo com o
        // idioma selecionado no combo da pagina Shop Documentation.
        private void RunSelectedShopDocumentation()
        {
            switch (cmbShopDocLanguage.SelectedIndex)
            {
                case 0: SHOP_DOCUMENTATION_PT.Run(null); break;
                case 1: SHOP_DOCUMENTATION_EN.Run(null); break;
                case 2: SHOP_DOCUMENTATION_DE.Run(null); break;
                case 3: SHOP_DOCUMENTATION_ES.Run(null); break;
                case 4: SHOP_DOCUMENTATION_FR.Run(null); break;
                case 5: SHOP_DOCUMENTATION_TR.Run(null); break;
                case 6: SHOP_DOCUMENTATION_ZH.Run(null); break;
                case 7: SHOP_DOCUMENTATION_NL.Run(null); break;
                default:
                    MessageBox.Show("Select a language.", "Shop Documentation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }
        }

        // Le o setup CAM da Work Part, anexa o HTML mais recente de
        // Documents\ShopDocumentation e grava tudo no PathNCAutomationDB
        // (tabelas ShopDoc / ShopDocOperation) numa unica transacao.
        private void SaveShopDocToDatabase()
        {
            Cursor previous = Cursor.Current;
            try
            {
                Cursor.Current = Cursors.WaitCursor;

                int idx = Math.Max(0, cmbShopDocLanguage.SelectedIndex);
                string lang = ShopDocLangCodes[Math.Min(idx, ShopDocLangCodes.Length - 1)];

                PathNCAutomation.ShopDoc.ShopDocModel doc =
                    PathNCAutomation.ShopDoc.ShopDocCollector.FromWorkPart(lang);

                if (doc.Operations.Count == 0)
                {
                    MessageBox.Show("No operations found in the current CAM setup.",
                        "Shop Documentation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Tuple<int, int> result = new PathNCAutomation.ShopDoc.ShopDocRepository().Save(doc);

                MessageBox.Show(
                    string.Format(
                        "Saved: {0}\nShopDoc #{1}, revision {2}\n{3} operations, {4:F1} min\nHTML: {5}",
                        doc.PartName, result.Item1, result.Item2,
                        doc.Operations.Count, doc.TotalTimeMin,
                        doc.HtmlFileName ?? "(none found)"),
                    "Shop Documentation", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save failed:\n" + ex.Message,
                    "Shop Documentation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = previous;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // PÁGINA: COPILOT (NOVO)
        // Sugestão de estratégia e parâmetros a partir das peças já salvas
        // no PathNCAutomationDB (StrategyHistoryService + CopilotResultForm).
        // ══════════════════════════════════════════════════════════════════

        private NumericUpDown nudCopilotTop;
        private NumericUpDown nudCopilotMinScore;
        private Label lblCopilotStatus;
        private CopilotResultForm _copilotResultForm;

        private Panel BuildPageCopilot()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "Copilot",
                "Suggests strategy and cutting parameters based on similar parts you already machined");
            int y = 116;

            Label badge = new Label
            {
                Text = "BETA",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold),
                ForeColor = ColorSuccessHover,
                BackColor = Color.FromArgb(220, 252, 231),
                Padding = new Padding(8, 3, 8, 3),
                Location = new Point(PAD, y),
            };
            page.Controls.Add(badge);
            y += 34;

            // ── SEARCH SETTINGS ─────────────────────────────────────────
            SectionTitle(page, "SEARCH SETTINGS", PAD, y); y += 22;

            FieldLabel(page, "SIMILAR PARTS TO SHOW", PAD, y); y += 14;
            nudCopilotTop = new NumericUpDown
            {
                Location = new Point(PAD, y),
                Size = new Size(CONTENT_WIDTH, 23),
                Minimum = 1,
                Maximum = 10,
                Value = 3,
                Font = new Font("Segoe UI", 9F),
            };
            page.Controls.Add(nudCopilotTop); y += 30;

            FieldLabel(page, "MINIMUM SIMILARITY (%)", PAD, y); y += 14;
            nudCopilotMinScore = new NumericUpDown
            {
                Location = new Point(PAD, y),
                Size = new Size(CONTENT_WIDTH, 23),
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                Increment = 5,
                Font = new Font("Segoe UI", 9F),
            };
            page.Controls.Add(nudCopilotMinScore); y += 36;

            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;

            // ── SUGGEST ─────────────────────────────────────────────────
            SectionTitle(page, "SUGGEST", PAD, y); y += 22;
            ActionButton(page, "Find Similar Parts & Suggest Strategy", PAD, y, CONTENT_WIDTH, 44, ColorPrimary, ColorPrimaryHover,
                (s, e) => RunCopilotSuggest()); y += 52;

            lblCopilotStatus = new Label
            {
                Text = "Uses the parts saved with \"Save to Database\" on the Shop Doc page.",
                Location = new Point(PAD, y),
                Size = new Size(CONTENT_WIDTH, 40),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColorTextMuted,
            };
            page.Controls.Add(lblCopilotStatus); y += 46;

            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;

            // ── ABOUT ───────────────────────────────────────────────────
            SectionTitle(page, "ABOUT", PAD, y); y += 22;
            Label about = new Label
            {
                Text = "Compares the current part (size, volume, face count) with every "
                     + "part in PathNCAutomationDB and returns the closest ones, the "
                     + "operation sequence that was actually used on them, median RPM and "
                     + "feed per tool, and an estimated machining time. The more parts you "
                     + "save, the better the suggestions get.",
                Location = new Point(PAD, y),
                Size = new Size(CONTENT_WIDTH, 120),
                ForeColor = Color.FromArgb(100, 116, 139),
                Font = new Font("Segoe UI", 9f),
            };
            page.Controls.Add(about);
            y += 130;
            return page;
        }

        private void RunCopilotSuggest()
        {
            Cursor previous = Cursor.Current;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                ReportProgressIndeterminate("Copilot: reading current part signature...");

                PathNCAutomation.ShopDoc.ShopDocModel atual =
                    PathNCAutomation.ShopDoc.StrategyHistoryService.AssinaturaAtual();

                ReportProgressIndeterminate("Copilot: searching similar parts in database...");

                int top = (int)nudCopilotTop.Value;
                double minScore = (double)nudCopilotMinScore.Value / 100.0;

                PathNCAutomation.ShopDoc.StrategySuggestion sug =
                    new PathNCAutomation.ShopDoc.StrategyHistoryService().Sugerir(atual, top, minScore);

                if (sug.Vizinhos.Count == 0)
                {
                    ReportProgress("Copilot: no similar parts found.", 100);
                    lblCopilotStatus.Text = string.Format(
                        "No parts above {0}% similarity. Lower the threshold or save more parts.",
                        nudCopilotMinScore.Value);
                    MessageBox.Show(
                        "No similar parts found in the database above the minimum similarity.\n\n"
                        + "Save more parts with \"Save to Database\" on the Shop Doc page, "
                        + "or lower the minimum similarity.",
                        "Copilot", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                ReportProgress(string.Format("Copilot: {0} similar part(s) found.", sug.Vizinhos.Count), 100);
                lblCopilotStatus.Text = string.Format(
                    "Best match: {0} ({1:P0}). Estimated time: {2:F1} min.",
                    sug.Vizinhos[0].PartName, sug.Vizinhos[0].Score, sug.TempoEstimadoMin);

                if (_copilotResultForm == null || _copilotResultForm.IsDisposed)
                {
                    _copilotResultForm = new CopilotResultForm();
                    _copilotResultForm.ApplyRequested += ApplyCopilotStrategy;
                    _copilotResultForm.Show(this);
                }
                _copilotResultForm.Load(atual, sug);
                _copilotResultForm.Activate();
            }
            catch (Exception ex)
            {
                ReportProgress("Copilot: error.", 0);
                MessageBox.Show("Copilot failed:\n" + ex.Message, "Copilot", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = previous;
            }
        }

        // Cria na Work Part o esqueleto do programa da peca vizinha
        // (grupo COPILOT_*, ferramentas, operacoes com RPM/avanco).
        // Nao seleciona geometria nem gera caminho - ver StrategyApplier.cs.
        private void ApplyCopilotStrategy(PathNCAutomation.ShopDoc.SimilarPart vizinha)
        {
            DialogResult ok = MessageBox.Show(
                string.Format(
                    "Create {0} operation(s) from \"{1}\" (rev.{2}) in the current part?\n\n"
                    + "A new program group COPILOT_{3} will be created with the tools, "
                    + "methods, RPM and feeds from that part. Geometry is NOT selected "
                    + "automatically - you will edit each operation afterwards.",
                    vizinha.Operations.Count, vizinha.PartName, vizinha.Revision,
                    vizinha.PartName.ToUpperInvariant()),
                "Copilot — Apply Strategy", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ok != DialogResult.Yes) return;

            Cursor previous = Cursor.Current;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                ReportProgressIndeterminate("Copilot: creating operations from " + vizinha.PartName + "...");

                PathNCAutomation.ShopDoc.ApplyResult r =
                    new PathNCAutomation.ShopDoc.StrategyApplier().Apply(vizinha);

                ReportProgress(string.Format("Copilot: {0} operation(s) created in {1}.",
                    r.OperacoesCriadas.Count, r.ProgramGroupName), 100);

                MessageBox.Show(r.ToString(), "Copilot — Apply Strategy",
                    MessageBoxButtons.OK,
                    r.Puladas.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ReportProgress("Copilot: apply failed.", 0);
                MessageBox.Show("Apply failed:\n" + ex.Message, "Copilot", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = previous;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // PÁGINA: QUOTE (NOVO)
        // Orcamento e prazo a partir das pecas similares do historico
        // (QuoteService + QuoteResultForm + ActualTimeDialog).
        // ══════════════════════════════════════════════════════════════════

        private NumericUpDown nudQuoteQty;
        private NumericUpDown nudQuoteMachineRate;
        private NumericUpDown nudQuoteProgRate;
        private NumericUpDown nudQuoteMargin;
        private NumericUpDown nudQuoteHoursDay;
        private NumericUpDown nudQuoteQueueDays;
        private NumericUpDown nudQuoteSetupMin;
        private NumericUpDown nudQuoteMinPerOp;
        private Label lblQuoteStatus;
        private bool _quoteSettingsLoaded;

        private Panel BuildPageQuote()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "Quote", "Cost and lead time estimate for the current part, based on similar parts already machined");
            int y = 116;

            SectionTitle(page, "BATCH", PAD, y); y += 22;
            FieldLabel(page, "QUANTITY", PAD, y); y += 14;
            nudQuoteQty = Nud(page, y, 1, 10000, 1, 0); y += 36;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;

            SectionTitle(page, "COST PARAMETERS", PAD, y); y += 22;
            FieldLabel(page, "MACHINE RATE (PER HOUR)", PAD, y); y += 14;
            nudQuoteMachineRate = Nud(page, y, 0, 100000, 180, 2); y += 30;
            FieldLabel(page, "PROGRAMMING RATE (PER HOUR)", PAD, y); y += 14;
            nudQuoteProgRate = Nud(page, y, 0, 100000, 150, 2); y += 30;
            FieldLabel(page, "DEFAULT SETUP (MIN)", PAD, y); y += 14;
            nudQuoteSetupMin = Nud(page, y, 0, 10000, 45, 0); y += 30;
            FieldLabel(page, "PROGRAMMING MIN PER OPERATION", PAD, y); y += 14;
            nudQuoteMinPerOp = Nud(page, y, 0, 1000, 12, 0); y += 30;
            FieldLabel(page, "MARGIN (%)", PAD, y); y += 14;
            nudQuoteMargin = Nud(page, y, 0, 500, 25, 0); y += 36;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;

            SectionTitle(page, "CAPACITY", PAD, y); y += 22;
            FieldLabel(page, "MACHINE HOURS PER DAY", PAD, y); y += 14;
            nudQuoteHoursDay = Nud(page, y, 1, 24, 8, 1); y += 30;
            FieldLabel(page, "QUEUE (DAYS BEFORE MACHINE)", PAD, y); y += 14;
            nudQuoteQueueDays = Nud(page, y, 0, 365, 2, 1); y += 36;
            ActionButton(page, "Save Parameters as Default", PAD, y, CONTENT_WIDTH, 30, ColorNeutral, ColorNeutralHover,
                (s, e) => SaveQuoteSettings()); y += 38;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;

            SectionTitle(page, "ESTIMATE", PAD, y); y += 22;
            ActionButton(page, "Estimate Current Part", PAD, y, CONTENT_WIDTH, 44, ColorPrimary, ColorPrimaryHover,
                (s, e) => RunQuoteEstimate()); y += 52;
            lblQuoteStatus = new Label
            {
                Text = "Uses the Copilot similarity settings (top N, minimum %).",
                Location = new Point(PAD, y),
                Size = new Size(CONTENT_WIDTH, 40),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColorTextMuted,
            };
            page.Controls.Add(lblQuoteStatus); y += 46;
            ActionButton(page, "Quote History", PAD, y, CONTENT_WIDTH, 34, ColorTeal, ColorTealHover,
                (s, e) => new QuoteHistoryForm().Show(this)); y += 42;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;

            SectionTitle(page, "CALIBRATION", PAD, y); y += 22;
            Label calHint = new Label
            {
                Text = "After a part runs on the machine, register the real time. "
                     + "The estimate uses the median actual/estimated ratio of all calibrated parts.",
                Location = new Point(PAD, y),
                Size = new Size(CONTENT_WIDTH, 52),
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColorTextMuted,
            };
            page.Controls.Add(calHint); y += 56;
            ActionButton(page, "Register Actual Machining Time", PAD, y, CONTENT_WIDTH, 34, ColorSuccess, ColorSuccessHover,
                (s, e) => RegisterActualTime()); y += 42;

            page.VisibleChanged += (s, e) => { if (page.Visible && !_quoteSettingsLoaded) LoadQuoteSettings(); };
            return page;
        }

        private NumericUpDown Nud(Panel page, int y, decimal min, decimal max, decimal val, int decimals)
        {
            NumericUpDown n = new NumericUpDown
            {
                Location = new Point(PAD, y),
                Size = new Size(CONTENT_WIDTH, 23),
                Minimum = min,
                Maximum = max,
                Value = val,
                DecimalPlaces = decimals,
                Font = new Font("Segoe UI", 9F),
                ThousandsSeparator = true,
            };
            page.Controls.Add(n);
            return n;
        }

        private PathNCAutomation.ShopDoc.QuoteSettings ReadQuoteSettingsFromUi()
        {
            return new PathNCAutomation.ShopDoc.QuoteSettings
            {
                MachineRatePerHour = (double)nudQuoteMachineRate.Value,
                ProgrammingRatePerHour = (double)nudQuoteProgRate.Value,
                DefaultSetupMin = (double)nudQuoteSetupMin.Value,
                ProgrammingMinPerOp = (double)nudQuoteMinPerOp.Value,
                MarginPercent = (double)nudQuoteMargin.Value,
                MachineHoursPerDay = (double)nudQuoteHoursDay.Value,
                QueueDays = (double)nudQuoteQueueDays.Value,
            };
        }

        private void LoadQuoteSettings()
        {
            try
            {
                PathNCAutomation.ShopDoc.QuoteSettings s = new PathNCAutomation.ShopDoc.QuoteService().LoadSettings();
                nudQuoteMachineRate.Value = (decimal)s.MachineRatePerHour;
                nudQuoteProgRate.Value = (decimal)s.ProgrammingRatePerHour;
                nudQuoteSetupMin.Value = (decimal)s.DefaultSetupMin;
                nudQuoteMinPerOp.Value = (decimal)s.ProgrammingMinPerOp;
                nudQuoteMargin.Value = (decimal)s.MarginPercent;
                nudQuoteHoursDay.Value = (decimal)s.MachineHoursPerDay;
                nudQuoteQueueDays.Value = (decimal)s.QueueDays;
                _quoteSettingsLoaded = true;
            }
            catch { /* banco indisponivel: fica com os defaults da UI */ }
        }

        private void SaveQuoteSettings()
        {
            try
            {
                new PathNCAutomation.ShopDoc.QuoteService().SaveSettings(ReadQuoteSettingsFromUi());
                lblQuoteStatus.Text = "Parameters saved as DEFAULT profile.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save failed:\n" + ex.Message, "Quote", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunQuoteEstimate()
        {
            Cursor previous = Cursor.Current;
            try
            {
                Cursor.Current = Cursors.WaitCursor;
                ReportProgressIndeterminate("Quote: reading current part and searching history...");

                PathNCAutomation.ShopDoc.ShopDocModel atual =
                    PathNCAutomation.ShopDoc.StrategyHistoryService.AssinaturaAtual();

                int top = nudCopilotTop != null ? (int)nudCopilotTop.Value : 3;
                double minScore = nudCopilotMinScore != null ? (double)nudCopilotMinScore.Value / 100.0 : 0.5;

                PathNCAutomation.ShopDoc.StrategySuggestion sug =
                    new PathNCAutomation.ShopDoc.StrategyHistoryService().Sugerir(atual, top, minScore);

                PathNCAutomation.ShopDoc.QuoteEstimate q =
                    new PathNCAutomation.ShopDoc.QuoteService().Estimate(atual, sug, (int)nudQuoteQty.Value, ReadQuoteSettingsFromUi());

                ReportProgress(string.Format("Quote: {0} {1:N2} total, {2:F0} days.", q.Settings.Currency, q.TotalPrice, q.LeadTimeDays), 100);
                lblQuoteStatus.Text = string.Format("Last estimate: {0} {1:N2} ({2:F0} days, confidence {3}).",
                    q.Settings.Currency, q.TotalPrice, q.LeadTimeDays, q.ConfidenceLabel);

                new QuoteResultForm(q, atual).Show(this);
            }
            catch (Exception ex)
            {
                ReportProgress("Quote: error.", 0);
                MessageBox.Show("Estimate failed:\n" + ex.Message, "Quote", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = previous;
            }
        }

        private void RegisterActualTime()
        {
            string atual = null;
            try { atual = NXOpen.Session.GetSession().Parts.Work != null ? NXOpen.Session.GetSession().Parts.Work.Leaf : null; } catch { }
            using (ActualTimeDialog d = new ActualTimeDialog(atual))
            {
                if (d.ShowDialog(this) == DialogResult.OK)
                    lblQuoteStatus.Text = "Actual time registered. Future estimates will use it.";
            }
        }

        // PÁGINA: AI ASSISTANT (NOVO)

        private Panel BuildPageAIAssistant()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, BackColor = ColorContentBg, AutoScroll = true };
            PageHeader(page, "AI Assistant", "Chat with Claude (Anthropic) about machining, CAM and how to use the panel");
            // Mesmo padrão visual do selo "EM DESENVOLVIMENTO" de
            // BuildPageComingSoon, só que verde/"novidade" em vez de laranja.
            Label badge = new Label
            {
                Text = "NEW",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold),
                ForeColor = ColorSuccessHover,
                BackColor = Color.FromArgb(220, 252, 231),
                Padding = new Padding(8, 3, 8, 3),
                Location = new Point(PAD, 116),
            };
            page.Controls.Add(badge);
            int y = 150;
            Label msg = new Label
            {
                Text = "Open a chat window with Claude to ask questions about "
                     + "machining strategies, recognized features, or the "
                     + "PATHNC AUTOMATION workflow itself.",
                Location = new Point(PAD, y),
                Size = new Size(CONTENT_WIDTH, 70),
                Font = new Font("Segoe UI", 9F),
                ForeColor = ColorTextSecondary,
            };
            page.Controls.Add(msg);
            y += 80;
            ActionButton(page, "Open AI Assistant", PAD, y, CONTENT_WIDTH, 38, ColorPrimary, ColorPrimaryHover,
                (s, e) => OpenAiAssistant()); y += 46;
            Divider(page, PAD, y, CONTENT_WIDTH); y += 16;
            SectionTitle(page, "SETTINGS", PAD, y); y += 22;
            ActionButton(page, "Change API Key", PAD, y, CONTENT_WIDTH, 30, ColorWarning, ColorWarningHover,
                (s, e) =>
                {
                    ApiKeyStore.ClearSavedKey();
                    MessageBox.Show(
                        "Key removed. The next message you send will prompt the assistant to ask for the key again.",
                        "AI Assistant", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }); y += 36;
            return page;
        }
        // Janela de chat é modeless (Show, não ShowDialog) pra deixar o
        // usuário continuar clicando nos outros botões do painel/NX enquanto
        // conversa com o assistente. Se já estiver aberta, só traz pra frente
        // em vez de abrir uma segunda instância.
        private ChatAssistantForm _aiAssistantForm;
        private void OpenAiAssistant()
        {
            if (_aiAssistantForm == null || _aiAssistantForm.IsDisposed)
            {
                _aiAssistantForm = new ChatAssistantForm(this);
                _aiAssistantForm.Show(this);
            }
            else
            {
                _aiAssistantForm.Activate();
            }
        }
        // CARGA DE ESTRATÉGIAS (lógica de negócio original, inalterada)
       
        private void Form1_Load(object sender, EventArgs e)
        {
            this.TopMost = true;
            context = new MachiningContext();
            strategies = new List<IMachiningStrategy>();
            strategiesP1 = new List<IMachiningStrategy>();
            strategiesMetricTap = new List<IMachiningStrategy>();
            strategiesSockedHead = new List<IMachiningStrategy>();
            strategiesDrills = new List<IMachiningStrategy>();
            strategiesRough = new List<IMachiningStrategy>();
            strategiesPockets = new List<IMachiningStrategy>();
            strategiesPlanarSurface = new List<IMachiningStrategy>();
            var openPocket = new OpenPocketStrategy();
            var closedPocket = new ClosedPocketStrategy();
            var moldCounterbore = new MoldCounterboreStrategy();
            var planarSurface = new PlanarSurfaceStrategy();
            var p1Plate = new P1PlateStrategy();
            var m6Tap = new M6TapStrategy();
            var m8Tap = new M8TapStrategy();
            var m10Tap = new M10TapStrategy();
            var m12Tap = new M12TapStrategy();
            var m14Tap = new M14TapStrategy();
            var m16Tap = new M16TapStrategy();
            var m18Tap = new M18TapStrategy();
            var m20Tap = new M20TapStrategy();
            var m10SockedHead = new M10SocketHeadStrategy();
            var m12SocketHead = new M12SocketHeadStrategy();
            var m16SockedHead = new M16SocketHeadStrategy();
            var m20SocketHead = new M20SocketHeadStrategy();
            var m24SocketHead = new M24SocketHeadStrategy();
            var centerDrillTopBlock = new CenterDrillTopBlockStrategy();
            var chamferMilling = new ChamferMillingStrategy();
            var cboreMill = new CboreMillStrategy();
            var countersink = new CSinkStrategy();
            var blindPeckDrill = new BlindPeckDrillStrategy();
            var throughPeckDrill = new ThroughPeckDrillStrategy();
            strategiesPockets.Add(openPocket);
            strategiesPockets.Add(closedPocket);
            strategies.Add(moldCounterbore);
            strategies.Add(planarSurface);
            strategiesP1.Add(p1Plate);
            strategiesMetricTap.Add(m6Tap); strategiesMetricTap.Add(m8Tap);
            strategiesMetricTap.Add(m10Tap); strategiesMetricTap.Add(m12Tap);
            strategiesMetricTap.Add(m14Tap); strategiesMetricTap.Add(m16Tap);
            strategiesMetricTap.Add(m18Tap); strategiesMetricTap.Add(m20Tap);
            strategiesSockedHead.Add(m10SockedHead);
            strategiesSockedHead.Add(m12SocketHead);
            strategiesSockedHead.Add(m16SockedHead);
            strategiesSockedHead.Add(m20SocketHead);
            strategiesSockedHead.Add(m24SocketHead);
            strategiesDrills.Add(centerDrillTopBlock);
            strategiesDrills.Add(chamferMilling);
            strategiesDrills.Add(cboreMill);
            strategiesDrills.Add(countersink);
            strategiesDrills.Add(blindPeckDrill);
            strategiesDrills.Add(throughPeckDrill);
            strategiesPlanarSurface.Add(planarSurface);
            context.Register(openPocket);
            context.Register(closedPocket);
            context.Register(moldCounterbore);
            context.Register(planarSurface);
            context.Register(p1Plate);
            context.Register(m6Tap); context.Register(m8Tap);
            context.Register(m10Tap); context.Register(m12Tap);
            context.Register(m14Tap); context.Register(m16Tap);
            context.Register(m18Tap); context.Register(m20Tap);
            context.Register(m10SockedHead);
            context.Register(m12SocketHead);
            context.Register(m16SockedHead);
            context.Register(m20SocketHead);
            context.Register(m24SocketHead);
            context.Register(centerDrillTopBlock);
            context.Register(chamferMilling);
            context.Register(cboreMill);
            context.Register(countersink);
            context.Register(blindPeckDrill);
            context.Register(throughPeckDrill);
            context.Register(planarSurface);
            if (cmbMetricTap != null)
            {
                cmbMetricTap.DataSource = strategiesMetricTap;
                cmbMetricTap.DisplayMember = "FeatureName";
                cmbMetricTap.SelectedIndex = -1;
            }
            if (cmbSocketHead != null)
            {
                cmbSocketHead.DataSource = strategiesSockedHead;
                cmbSocketHead.DisplayMember = "FeatureName";
                cmbSocketHead.SelectedIndex = -1;
            }
            if (cmbDrills != null)
            {
                cmbDrills.DataSource = strategiesDrills;
                cmbDrills.DisplayMember = "FeatureName";
                cmbDrills.SelectedIndex = -1;
            }
            if (cmbPockets != null)
            {
                cmbPockets.DataSource = strategiesPockets;
                cmbPockets.DisplayMember = "FeatureName";
                cmbPockets.SelectedIndex = -1;
            }
            if (cmbSurfaces != null)
            {
                cmbSurfaces.DataSource = strategiesPlanarSurface;
                cmbSurfaces.DisplayMember = "FeatureName";
                cmbSurfaces.SelectedIndex = -1;
            }
            if (cmb3xMachines != null)
            {
                cmb3xMachines.DisplayMember = "FeatureName";
                cmb3xMachines.Items.AddRange(new object[]
                {
                    new THREE_AXIS_MILLING_FANUC(),
                    new THREE_AXIS_MILLING_SIEMENS(),
                    new THREE_AXIS_MILLING_HEIDENHAIN(),
                    new THREE_AXIS_MILLING_GENERIC(),
                });
                cmb3xMachines.SelectedIndex = -1;
            }
            // AVISO: mesmas 3 classes reaproveitadas do 3-Axis - ver comentário
            // no topo da seção MACHINE em BuildPage3Plus2Axis.
            if (cmb3Plus2Machines != null)
            {
                cmb3Plus2Machines.DisplayMember = "FeatureName";
                cmb3Plus2Machines.Items.AddRange(new object[]
                {
                    new THREE_AXIS_MILLING_SIEMENS(),
                    new THREE_AXIS_MILLING_FANUC(),
                    new THREE_AXIS_MILLING_HEIDENHAIN(),
                });
                cmb3Plus2Machines.SelectedIndex = -1;
            }
        }
        public static int GetUnloadOption(string dummy)
        {
            return (int)NXOpen.Session.LibraryUnloadOption.Explicitly;
        }
    }
}