using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using NXOPEN_MACHINES;
using TOOL_LIBRARY_FROM_NX;
using NX_3_PLUS_TWO_CREATE_FEATURE_GROUP;
using NX_3_PLUS_TWO_RECOGNIZE_FEATURES;
using PATHNC.MOLD_WIZARD;
namespace nxteste2
{
    // Janela de chat com o assistente Claude (Anthropic). Aberta a partir do
    // botão "Abrir Assistente IA" na página "AI Assistant" do Form1.
    //
    // É modeless (Form1 chama .Show(this), não .ShowDialog()) pra deixar o
    // usuário continuar usando o resto do painel/o NX enquanto conversa.
    //
    // Além de conversar com o Claude, essa janela também reconhece alguns
    // "comandos" digitados em texto livre (ex.: "roda o autodrill") e
    // dispara a ação correspondente diretamente no Form1 — SEM passar pela
    // API da Anthropic pra decidir isso. É de propósito: rodar toolpath de
    // verdade não deve depender da IA "interpretar" a frase, só de bater
    // com uma lista fixa de palavras-chave que você controla (veja
    // BuildCommands mais abaixo).
    public class ChatAssistantForm : Form
    {
        private Panel transcriptPanel;
        private TextBox txtInput;
        private Button btnSend;
        private Button btnSettings;
        private Label lblStatus;
        // Barra de progresso, ligada no evento Form1.ProgressChanged — mostra
        // aqui, na própria janela do chat, o mesmo progresso que aparece no
        // rodapé do painel principal (útil quando o Auto Drill/FBM é
        // disparado por texto e o usuário está de olho nessa janela, não no
        // painel principal).
        private NeonProgressBar progressBar;
        private Label lblProgress;
        // ── Paleta "AI Assistant": tema escuro com acento roxo → ciano
        // (linguagem visual comum em produtos de IA), pra essa janela se
        // destacar como uma área diferente do resto do painel (que é
        // claro). Trocada a pedido do usuário — o branco simples demais. ──
        private static readonly Color ColorHeaderBg = Color.FromArgb(17, 15, 30);
        private static readonly Color ColorHeaderBgLight = Color.FromArgb(88, 28, 135);
        private static readonly Color ColorPrimary = Color.FromArgb(139, 92, 246);
        private static readonly Color ColorPrimaryHover = Color.FromArgb(124, 58, 237);
        private static readonly Color ColorAccentCyan = Color.FromArgb(34, 211, 238);
        private static readonly Color ColorTextPrimary = Color.FromArgb(226, 232, 240);
        private static readonly Color ColorTextMuted = Color.FromArgb(148, 163, 184);
        private static readonly Color ColorDanger = Color.FromArgb(248, 113, 113);
        private static readonly Color ColorCommand = Color.FromArgb(45, 212, 191);
        private static readonly Color ColorBubbleAssistant = Color.FromArgb(30, 32, 48);
        private static readonly Color ColorBubbleSystem = Color.FromArgb(8, 47, 42);
        private static readonly Color ColorBubbleError = Color.FromArgb(58, 15, 15);
        private static readonly Color ColorBackground = Color.FromArgb(13, 14, 22);
        private static readonly Color ColorSurface = Color.FromArgb(22, 24, 38);
        private static readonly Color ColorBorder = Color.FromArgb(48, 51, 74);
        private readonly Form1 _owner;
        private readonly AnthropicClient _client = new AnthropicClient();
        private readonly List<ChatMessage> _history = new List<ChatMessage>();
        private readonly List<ChatCommand> _commands;
        private readonly List<BubbleRecord> _bubbles = new List<BubbleRecord>();
        // Dá contexto ao assistente sobre o que ele está ajudando. Ajuste
        // livremente esse texto conforme o painel evoluir.
        private const string SystemPrompt =
            "You are an assistant integrated into PATHNC AUTOMATION, an NXOpen panel "
            + "for CAM machining automation in Siemens NX (milling, drilling, "
            + "automatic feature recognition / FBM, molds). Respond clearly and "
            + "objectively in English, helping the user with questions about "
            + "machining, CAM, NXOpen, and how to use the panel itself.";
        // Estilo visual de cada balão de mensagem no transcript.
        private enum BubbleRole { User, Assistant, System, Error }
        private class BubbleRecord
        {
            public string Author;
            public string Text;
            public BubbleRole Role;
        }
        // Uma ação que o chat pode disparar por texto: uma lista de
        // sinônimos/palavras-chave, uma descrição pra mostrar no chat, e o
        // método do Form1 a chamar quando bater.
        private class ChatCommand
        {
            public string[] Keywords;
            public string Description;
            public Action Run;
        }
        public ChatAssistantForm(Form1 owner)
        {
            _owner = owner;
            _commands = BuildCommands();
            Text = "AI Assistant — Claude";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 560);
            MinimumSize = new Size(360, 400);
            BackColor = ColorBackground;
            BuildUi();
            // Assina o progresso do Form1 (RunAutoDrillCycle/etc. chamam
            // ReportProgress lá, que dispara ProgressChanged) pra refletir
            // aqui na própria janela do chat. Desinscreve ao fechar, senão
            // o Form1 mantém uma referência viva desse form fechado.
            if (_owner != null)
                _owner.ProgressChanged += OnOwnerProgressChanged;
            FormClosed += (s, e) =>
            {
                if (_owner != null)
                    _owner.ProgressChanged -= OnOwnerProgressChanged;
            };
        }
        private void OnOwnerProgressChanged(string status, int percent, bool indeterminate)
        {
            if (lblProgress == null || progressBar == null)
                return;
            lblProgress.Text = status;
            // O Form1 já resolve o percentual "de exibição" antes de
            // disparar o evento (percentual real quando conhecido, ou o
            // baseline/valor mantido quando é uma etapa "caixa-preta" — ver
            // Form1.ReportProgress/ReportProgressIndeterminate), então
            // aqui é só refletir o número recebido.
            progressBar.Value = Math.Max(0, Math.Min(100, percent));
            Application.DoEvents();
        }
        // Lista de comandos reconhecidos por texto. Pra adicionar um novo,
        // é só acrescentar mais um ChatCommand aqui — nenhum outro lugar do
        // arquivo precisa mudar.
        //
        // IMPORTANTE sobre as keywords "frase completa" (tipo "import a
        // 3-axis machine with fanuc control"): elas precisam ficar DENTRO
        // do ChatCommand certo (o da mesma marca/acao), nunca coladas no
        // comando errado - foi exatamente isso que quebrou o comando da
        // Fanuc antes (a frase mencionando "fanuc" tinha sido colada, por
        // engano, dentro do Keywords do comando Generic). O MatchCommand
        // agora tambem escolhe sempre o keyword MAIS ESPECIFICO (mais
        // longo) entre todos os que baterem, entao nao adianta so acertar
        // a ordem da lista - o texto tem que estar de fato no comando
        // certo.
        private List<ChatCommand> BuildCommands()
        {
            return new List<ChatCommand>
            {
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "autodrill", "auto drill", "auto-drill",
                        "furos automatico", "furacao automatica", "furos automaticos",
                        "rodar furos", "roda os furos", "roda furos",
                    },
                    Description = "Auto Drill — Full Cycle",
                    Run = () => _owner.RunAutoDrillCycle(),
                },
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "auto 2d feature machined", "fbm automatico", "fbm automático",
                        "roda o fbm", "rodar o fbm", "roda fbm", "executar fbm",
                    },
                    Description = "Auto 2D Feature Machined — Full Cycle",
                    // FBM_ALL_FEATURES_MACHINED é uma classe global (sem
                    // namespace), igual RECOGNIZE_FEATURES etc. — por isso dá
                    // pra chamar direto aqui, sem precisar de using extra.
                    Run = () => FBM_ALL_FEATURES_MACHINED.Run(null),
                },
                // ── Colorir/Classificar Furos por Categoria (aba FBM →
                // AUTOMAÇÃO → botão "Color Holes by Category (QC)"). Mesma
                // chamada do botão (_owner.RunColorizeHoles()) — checagem
                // visual de QC, não roda nenhuma operação/estratégia de
                // usinagem. Era só "por diâmetro"; agora classifica por
                // categoria (rosca/alargador/soquete/coluna de molde) - os
                // atalhos antigos de diâmetro continuam funcionando, só
                // apontam pro mesmo botão novo.
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "colorir furos", "colorir os furos", "colorir furo", "colorir o furo",
                        "colorir por diametro", "colorir por diâmetro",
                        "colorir furos por diametro", "colorir furos por diâmetro",
                        "colorir diametros", "colorir diâmetros",
                        "colorir por categoria", "colorir furos por categoria",
                        "classificar furos", "classificar furos por categoria", "classificar por categoria",
                        "color holes", "colour holes", "color the holes", "colour the holes",
                        "color holes by diameter", "colorize holes",
                        "color holes by category", "colour holes by category",
                        "classify holes", "classify holes by category",
                    },
                    Description = "Color Holes by Category (QC)",
                    Run = () => _owner.RunColorizeHoles(),
                },
                // ── Descolorir Furos (aba FBM → AUTOMAÇÃO → botão
                // "Descolorir Furos (cinza)"). Mesma chamada do botão
                // (_owner.RunUncolorHoles()) — desfaz a coloração acima,
                // forçando todas as faces cilíndricas de volta pro cinza.
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "descolorir furos", "descolorir os furos", "descolorir furo", "descolorir o furo",
                        "tirar cor dos furos", "tirar a cor dos furos", "remover cor dos furos", "remover a cor dos furos",
                        "deixar furos cinza", "deixar os furos cinza", "furos cinza", "furos em cinza",
                        "uncolor holes", "reset hole colors", "remove hole colors",
                        "gray out holes", "grey out holes", "gray the holes", "grey the holes",
                    },
                    Description = "Uncolor Holes (gray)",
                    Run = () => _owner.RunUncolorHoles(),
                },
                // ── Seleção de máquina 3-Axis (aba Setup → SELECT MACHINE →
                // 3X). Mesmas classes que RunSelected3xMachine() já chama a
                // partir do ComboBox — aqui só dá outro jeito de disparar a
                // mesma ação, por texto. Além do nome da marca sozinho, cada
                // uma das 4 máquinas abaixo tem as MESMAS variações de frase
                // natural ("import a 3-axis machine with <marca> control"),
                // pra ficar simétrico entre elas — cada frase mora dentro do
                // ChatCommand da marca correspondente, nunca em outra.
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "fanuc",
                        "import a 3-axis machine with fanuc control",
                        "import 3-axis machine with fanuc control",
                        "import a 3x machine with fanuc control",
                        "importar maquina fanuc", "maquina fanuc",
                    },
                    Description = "Select 3-Axis Machine — Fanuc",
                    Run = () => THREE_AXIS_MILLING_FANUC.Run(null),
                },
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "siemens",
                        "import a 3-axis machine with siemens control",
                        "import 3-axis machine with siemens control",
                        "import a 3x machine with siemens control",
                        "importar maquina siemens", "maquina siemens",
                    },
                    Description = "Select 3-Axis Machine — Siemens",
                    Run = () => THREE_AXIS_MILLING_SIEMENS.Run(null),
                },
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "heidenhain", "heidenhein", "heidenhaim",
                        "import a 3-axis machine with heidenhain control",
                        "import 3-axis machine with heidenhain control",
                        "import a 3x machine with heidenhain control",
                        "importar maquina heidenhain", "maquina heidenhain",
                    },
                    Description = "Select 3-Axis Machine — Heidenhain",
                    Run = () => THREE_AXIS_MILLING_HEIDENHAIN.Run(null),
                },
                // Palavras-chave propositalmente largas (só o nome da marca)
                // a pedido do usuário — "generic" é a exceção, porque sozinha
                // é uma palavra comum demais em português/inglês e
                // dispararia à toa em conversas normais, por isso aqui só
                // frases mais completas.
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "maquina generica", "machine generic", "generic machine",
                        "generic cnc", "cnc generico", "cnc generic",
                        "3x generico", "3x generic", "importar cnc generico",
                        "import generic cnc", "select generic", "selecionar generica",
                        "maquina genérica",
                        "import a 3-axis machine with generic control",
                        "import 3-axis machine with generic control",
                        "import a 3x machine with generic control",
                    },
                    Description = "Select 3-Axis Machine — Generic",
                    Run = () => THREE_AXIS_MILLING_GENERIC.Run(null),
                },
                // ── Vise/morsa (aba Setup → VISES/FIXTURES → ícone "Standard
                // Vise"). Mesma chamada que o ícone já usa
                // (LoadAndAlignVise_Auto.Run(null)) — classe global (sem
                // namespace), igual FBM_ALL_FEATURES_MACHINED, por isso não
                // precisa de using extra aqui.
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "vise", "morsa", "align vise", "load vise", "standard vise",
                        "carregar morsa", "alinhar morsa", "fixar peca", "prender peca",
                        "get the standard vise and align it against the part",
                    },
                    Description = "Load and Align Vise (Standard Vise)",
                    Run = () => LoadAndAlignVise_Auto.Run(null),
                },
                // ── Tool Library (aba Setup → TOOL LIBRARY → botão "Import
                // Tools"). Mesma chamada do botão (ToolLibraryFromNX.Run(null)).
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "tool library", "biblioteca de ferramentas", "biblioteca de ferramenta",
                        "importar ferramentas", "importar ferramenta", "import tools", "import tool",
                        "carregar ferramentas", "carregar biblioteca",
                    },
                    Description = "Import Tools (Tool Library)",
                    Run = () => ToolLibraryFromNX.Run(null),
                },
                // ── Localizar Features (aba FBM → SETUP → botão "Localizar
                // Features"). Mesma chamada do botão (RECOGNIZE_FEATURES.Run(null)).
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "localizar features", "localizar feature", "localizar as features",
                        "reconhecer features", "reconhecer feature", "reconhecer as features",
                        "locate features", "find features", "recognize features", "recognise features",
                    },
                    Description = "Find Features (RECOGNIZE_FEATURES)",
                    Run = () => RECOGNIZE_FEATURES.Run(null),
                },
                // ── Criar Grupo de Features (aba FBM → SETUP → botão "Criar
                // Grupo de Features"). Mesma chamada do botão
                // (CREATE_GROUP_FEATURES.Run(null)).
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "criar grupo de features", "criar grupos de features",
                        "criar grupo de feature", "agrupar features", "criar grupo de figuras",
                        "create feature group", "create feature groups",
                        "create group of features", "group features",
                    },
                    Description = "Create Feature Group (CREATE_GROUP_FEATURES)",
                    Run = () => CREATE_GROUP_FEATURES.Run(null),
                },
                // ── Mold Wizard (aba Mold Wizard → CORE/CAVITY MACHINING).
                // Mesmas chamadas dos 4 botões daquela página.
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "rough wizard", "assistente de desbaste", "desbaste wizard", "wizard de desbaste",
                    },
                    Description = "Rough Wizard (MOLD_WIZARD_ROUGH)",
                    Run = () => MOLD_WIZARD_ROUGH.Run(null),
                },
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "rest mill", "rest milling", "usinagem rest mill",
                    },
                    Description = "Rest Mill (MOLD_WIZARD_REST_MILL)",
                    Run = () => MOLD_WIZARD_REST_MILL.Run(null),
                },
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "finish wizard", "assistente de acabamento", "acabamento wizard", "wizard de acabamento",
                    },
                    Description = "Finish Wizard (MOLD_WIZARD_FINISH)",
                    Run = () => MOLD_WIZARD_FINISH.Run(null),
                },
                new ChatCommand
                {
                    Keywords = new[]
                    {
                        "auto cycle core cavity", "auto cycle core and cavity", "auto cycle macho cavidade",
                        "ciclo automatico macho e cavidade", "ciclo automatico core cavity",
                        "ciclo completo macho e cavidade", "ciclo completo core cavity",
                    },
                    Description = "Auto Cycle — Core & Cavity (MOLD_ALTO_CICLE_ROUGH_FINISH)",
                    Run = () => MOLD_ALTO_CICLE_ROUGH_FINISH.Run(null),
                },
            };
        }
        // ══════════════════════════════════════════════════════════════════
        // MONTAGEM DA INTERFACE
        // ══════════════════════════════════════════════════════════════════
        private void BuildUi()
        {
            Panel header = BuildHeader();
            transcriptPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColorBackground,
                AutoScroll = true,
            };
            // Os balões são posicionados manualmente (ver RenderBubble), não
            // com FlowLayoutPanel — por isso precisa recalcular tudo quando a
            // janela é redimensionada (largura do balão/lado depende da
            // largura disponível).
            transcriptPanel.Resize += (s, e) => RelayoutBubbles();
            Panel progressRow = BuildProgressRow();
            Panel inputPanel = BuildInputArea();
            // Ordem importa pro docking: progressRow (Bottom) precisa ser
            // adicionado ANTES de inputPanel (também Bottom), pra ficar
            // logo ACIMA da caixa de texto em vez de embaixo dela.
            Controls.Add(transcriptPanel);
            Controls.Add(progressRow);
            Controls.Add(inputPanel);
            Controls.Add(header);
            AppendLine("Assistant",
                "Hello! I'm the PATHNC AUTOMATION assistant. Ask me anything about "
                + "machining, CAM, NXOpen, or how to use the panel. I also understand "
                + "direct commands, like \"roda o autodrill\" or \"roda o fbm\".",
                BubbleRole.Assistant);
            txtInput.Focus();
        }
        private Panel BuildHeader()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = ColorHeaderBg };
            // Mesmo tratamento visual do cabeçalho do Form1: gradiente sutil
            // + faixa de destaque colada na base, pra manter a mesma
            // identidade visual entre o painel principal e essa janela.
            header.Paint += (s, e) =>
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(
                    new Rectangle(0, 0, header.Width, header.Height),
                    ColorHeaderBg, ColorHeaderBgLight, 35f))
                {
                    e.Graphics.FillRectangle(brush, header.ClientRectangle);
                }
            };
            Panel accentStripe = new Panel { Dock = DockStyle.Bottom, Height = 3, BackColor = ColorPrimary };
            Label title = new Label
            {
                Text = "✦ AI Assistant",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(14, 7),
            };
            Label subtitle = new Label
            {
                Text = "Powered by Claude · Anthropic",
                ForeColor = ColorAccentCyan,
                Font = new Font("Segoe UI", 7.5F),
                AutoSize = true,
                Location = new Point(15, 27),
            };
            // Botão de engrenagem — reconfigura/troca a chave da API a
            // qualquer momento, sem precisar ir até a página do painel
            // principal. Ancorado no canto superior direito pra acompanhar
            // o redimensionamento da janela.
            btnSettings = new Button
            {
                Text = "⚙",
                Size = new Size(30, 26),
                Location = new Point(ClientSize.Width - 40, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = ColorSurface,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                Cursor = Cursors.Hand,
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.FlatAppearance.MouseOverBackColor = ColorHeaderBgLight;
            btnSettings.Click += (s, e) => ConfigureApiKey();
            ApplyRoundedCorners(btnSettings, 6);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(btnSettings);
            header.Controls.Add(accentStripe);
            return header;
        }
        // Faixa fina de progresso entre o transcript e a caixa de texto —
        // fica em branco/parada até o Form1 disparar ProgressChanged (ex.:
        // rodando o Auto Drill ou o FBM automático, pelo botão OU pelo
        // comando de texto).
        private Panel BuildProgressRow()
        {
            Panel row = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = ColorSurface, Padding = new Padding(10, 4, 10, 8) };
            Panel topBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorBorder };
            lblProgress = new Label
            {
                Text = "",
                Dock = DockStyle.Top,
                Height = 16,
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = ColorTextMuted,
            };
            // Barra "customizada" (NeonProgressBar, ver classe no fim do
            // arquivo) em vez do ProgressBar padrão do Windows — o controle
            // padrão sempre pinta um fundo claro/cinza fixo (ignora o tema
            // escuro), o que ficaria destoando muito aqui.
            progressBar = new NeonProgressBar
            {
                Dock = DockStyle.Fill,
                BackColor = ColorSurface,
                Value = 0,
            };
            row.Controls.Add(progressBar);
            row.Controls.Add(lblProgress);
            row.Controls.Add(topBorder);
            return row;
        }
        private Panel BuildInputArea()
        {
            Panel inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 88,
                Padding = new Padding(10, 8, 10, 10),
                BackColor = ColorSurface,
            };
            // Linha divisória sutil separando a área de digitação do
            // transcript, em vez de emendar direto sem nenhum limite visual.
            Panel topBorder = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = ColorBorder };
            lblStatus = new Label
            {
                Text = "",
                Dock = DockStyle.Top,
                Height = 16,
                ForeColor = ColorAccentCyan,
                Font = new Font("Segoe UI", 7.5F),
            };
            Panel inputRow = new Panel { Dock = DockStyle.Fill, BackColor = ColorSurface };
            btnSend = new Button
            {
                Text = "Send",
                Dock = DockStyle.Right,
                Width = 84,
                BackColor = ColorPrimary,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Margin = new Padding(8, 0, 0, 0),
            };
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.FlatAppearance.MouseOverBackColor = ColorPrimaryHover;
            btnSend.Click += async (s, e) => await SendCurrentInputAsync();
            txtInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                Font = new Font("Segoe UI", 9.5F),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(30, 32, 48),
                ForeColor = Color.White,
            };
            txtInput.KeyDown += TxtInput_KeyDown;
            // Ordem de adição importa com Dock: adiciona o botão (Right)
            // antes pra sobrar o resto do espaço (Fill) pro TextBox.
            inputRow.Controls.Add(txtInput);
            inputRow.Controls.Add(btnSend);
            inputPanel.Controls.Add(inputRow);
            inputPanel.Controls.Add(lblStatus);
            inputPanel.Controls.Add(topBorder);
            ApplyRoundedCorners(btnSend, 6);
            return inputPanel;
        }
        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            // Enter envia; Shift+Enter quebra linha (comportamento padrão de
            // chat).
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                _ = SendCurrentInputAsync();
            }
        }
        private void ConfigureApiKey()
        {
            ApiKeyStore.ClearSavedKey();
            AppendLine("Assistant",
                "API key removed. Send any message and I'll ask for the new key "
                + "(the box should now appear in front of everything).",
                BubbleRole.System);
        }
        private async Task SendCurrentInputAsync()
        {
            string userText = txtInput.Text.Trim();
            if (string.IsNullOrEmpty(userText))
                return;
            txtInput.Text = "";
            AppendLine("You", userText, BubbleRole.User);
            // Primeiro checa se é um comando conhecido (ex.: "roda o
            // autodrill"). Se for, executa direto e NÃO manda essa mensagem
            // pra API — a ação já foi tomada de forma determinística.
            ChatCommand matched = MatchCommand(userText);
            if (matched != null)
            {
                RunCommand(matched);
                return;
            }
            _history.Add(new ChatMessage { Role = "user", Content = userText });
            SetBusy(true);
            try
            {
                string reply = await _client.SendAsync(_history, SystemPrompt);
                _history.Add(new ChatMessage { Role = "assistant", Content = reply });
                AppendLine("Assistant", reply, BubbleRole.Assistant);
            }
            catch (Exception ex)
            {
                // Se a chamada falhar (ex.: chave inválida, sem internet),
                // desfaz a última entrada do histórico pra não mandar de
                // novo uma "pergunta" sem resposta na próxima mensagem.
                _history.RemoveAt(_history.Count - 1);
                AppendLine("Error", ex.Message, BubbleRole.Error);
            }
            finally
            {
                SetBusy(false);
                txtInput.Focus();
            }
        }
        // Executa um ChatCommand batido pelo texto do usuário. cmd.Run() faz
        // chamadas NXOpen/COM, que precisam rodar de forma SÍNCRONA na
        // mesma thread de UI, exatamente como o clique do botão já fazia
        // (ver histórico: uma versão anterior com "await Task.Yield()" antes
        // do cmd.Run() causava travamento logo após o ciclo terminar).
        private void RunCommand(ChatCommand cmd)
        {
            if (_owner == null)
            {
                AppendLine("Error", "This command is not available — window opened without a reference to the main panel.", BubbleRole.Error);
                return;
            }
            SetBusy(true);
            AppendLine("Assistant", "Running \"" + cmd.Description + "\"...", BubbleRole.System);
            // Reporta na barra de progresso (Form1 + esta janela, via
            // ProgressChanged) mesmo pra comandos que não têm sub-etapas
            // próprias — assim TODO comando disparado pelo chat aparece na
            // barra, não só o Auto Drill (que já reporta o progresso fino
            // dele mesmo dentro de RunAutoDrillCycle/AUTOMATIC_COUNTERBORE).
            // Como não temos como saber o percentual real desses outros
            // comandos (código-fonte deles não instrumentado com um evento
            // de progresso), usa a barra animada (indeterminate) em vez de
            // travar num número que não significa nada — RunAutoDrillCycle
            // sobrescreve isso com o progresso real dele assim que começa.
            _owner.ReportProgressIndeterminate("Running: " + cmd.Description + "...");
            try
            {
                cmd.Run();
                _owner.ReportProgress(cmd.Description + " completed.", 100);
                AppendLine("Assistant", "\"" + cmd.Description + "\" completed.", BubbleRole.System);
            }
            catch (Exception ex)
            {
                _owner.ReportProgress("Failed \"" + cmd.Description + "\".", 0);
                AppendLine("Error", "Failed to run \"" + cmd.Description + "\": " + ex.Message, BubbleRole.Error);
            }
            finally
            {
                SetBusy(false);
                txtInput.Focus();
            }
        }
        // Compara o texto digitado contra as palavras-chave de cada
        // comando. Basta a frase CONTER uma das palavras-chave — não
        // precisa ser exata.
        //
        // CORRECAO: antes, o primeiro (comando, keyword) que batesse
        // ganhava, na ordem em que os comandos aparecem em BuildCommands.
        // Isso e fragil: se uma keyword generica/curta (ex.: um comando
        // que aparece mais cedo na lista) acabar sendo substring de uma
        // frase pensada pra outro comando mais especifico, o comando
        // errado dispara sem nenhum aviso. Agora ele varre TODOS os
        // comandos e fica com o que tiver a keyword MAIS LONGA/especifica
        // encontrada no texto, nao mais o primeiro da lista.
        private ChatCommand MatchCommand(string text)
        {
            string normalized = NormalizeForMatch(text);
            ChatCommand best = null;
            int bestKeywordLength = 0;
            foreach (ChatCommand cmd in _commands)
            {
                foreach (string keyword in cmd.Keywords)
                {
                    string normalizedKeyword = NormalizeForMatch(keyword);
                    if (normalizedKeyword.Length == 0)
                        continue;
                    if (normalizedKeyword.Length <= bestKeywordLength)
                        continue; // ja temos algo mais especifico - nao vale a pena nem testar
                    if (normalized.Contains(normalizedKeyword))
                    {
                        best = cmd;
                        bestKeywordLength = normalizedKeyword.Length;
                    }
                }
            }
            return best;
        }
        // Minusculas + sem acento + espacos colapsados/aparados. O
        // colapso de espacos (\s+ -> " ") existe porque varias keywords
        // deste arquivo foram coladas a partir de texto corrigido em
        // outro lugar (ex.: "the  Standard", com espaco duplo) - sem
        // normalizar isso, a keyword so bate se o usuario digitar
        // exatamente aquele espaco duplo tambem, o que nunca acontece.
        private static string NormalizeForMatch(string text)
        {
            string noAccents = RemoveDiacritics((text ?? string.Empty).ToLowerInvariant());
            return Regex.Replace(noAccents, @"\s+", " ").Trim();
        }
        private static string RemoveDiacritics(string text)
        {
            string normalized = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();
            foreach (char c in normalized)
            {
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
        private void SetBusy(bool busy)
        {
            btnSend.Enabled = !busy;
            txtInput.Enabled = !busy;
            lblStatus.Text = busy ? "Querying the assistant..." : "";
        }
        // ══════════════════════════════════════════════════════════════════
        // BALÕES DE MENSAGEM (transcript)
        // ══════════════════════════════════════════════════════════════════
        private void AppendLine(string author, string text, BubbleRole role)
        {
            _bubbles.Add(new BubbleRecord { Author = author, Text = text, Role = role });
            RelayoutBubbles();
        }
        // Recria e reposiciona TODOS os balões a partir de _bubbles. É mais
        // simples e confiável do que tentar mover balões existentes quando a
        // janela é redimensionada (a largura de cada balão depende da
        // largura disponível no momento).
        private void RelayoutBubbles()
        {
            if (transcriptPanel == null)
                return;
            transcriptPanel.SuspendLayout();
            Control[] existing = new Control[transcriptPanel.Controls.Count];
            transcriptPanel.Controls.CopyTo(existing, 0);
            foreach (Control c in existing)
            {
                transcriptPanel.Controls.Remove(c);
                c.Dispose();
            }
            int y = 10;
            foreach (BubbleRecord rec in _bubbles)
                y = RenderBubble(rec, y);
            transcriptPanel.AutoScrollMinSize = new Size(0, y + 8);
            transcriptPanel.ResumeLayout();
            // Rola pro final — truque padrão do WinForms: atribuir
            // AutoScrollPosition com o valor máximo do scroll vertical.
            if (transcriptPanel.VerticalScroll.Visible)
                transcriptPanel.AutoScrollPosition = new Point(0, transcriptPanel.VerticalScroll.Maximum);
        }
        private int RenderBubble(BubbleRecord rec, int y)
        {
            int panelWidth = transcriptPanel.ClientSize.Width;
            if (panelWidth < 160)
                panelWidth = 160;
            int maxBubbleWidth = (int)(panelWidth * 0.8);
            if (maxBubbleWidth < 140)
                maxBubbleWidth = 140;
            Font textFont = new Font("Segoe UI", 9.5F);
            Size measured = TextRenderer.MeasureText(
                rec.Text, textFont, new Size(maxBubbleWidth - 24, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int bubbleWidth = measured.Width + 24;
            if (bubbleWidth > maxBubbleWidth)
                bubbleWidth = maxBubbleWidth;
            if (bubbleWidth < 90)
                bubbleWidth = 90;
            int bubbleHeight = measured.Height + 16 + 20; // padding + espaço do rótulo do autor
            Color bg, fg, authorColor;
            switch (rec.Role)
            {
                case BubbleRole.User:
                    bg = ColorPrimary; fg = Color.White; authorColor = Color.FromArgb(233, 213, 255);
                    break;
                case BubbleRole.Error:
                    bg = ColorBubbleError; fg = ColorDanger; authorColor = ColorDanger;
                    break;
                case BubbleRole.System:
                    bg = ColorBubbleSystem; fg = Color.FromArgb(110, 231, 183); authorColor = ColorCommand;
                    break;
                default: // Assistant
                    bg = ColorBubbleAssistant; fg = ColorTextPrimary; authorColor = ColorTextMuted;
                    break;
            }
            Panel bubble = new Panel { Size = new Size(bubbleWidth, bubbleHeight), BackColor = bg };
            ApplyRoundedCorners(bubble, 10);
            Label lblAuthor = new Label
            {
                Text = rec.Author,
                Font = new Font("Segoe UI Semibold", 7F, FontStyle.Bold),
                ForeColor = authorColor,
                AutoSize = true,
                Location = new Point(10, 5),
            };
            Label lblText = new Label
            {
                Text = rec.Text,
                Font = textFont,
                ForeColor = fg,
                Location = new Point(10, 21),
                Size = new Size(bubbleWidth - 20, measured.Height + 4),
            };
            bubble.Controls.Add(lblAuthor);
            bubble.Controls.Add(lblText);
            int x = (rec.Role == BubbleRole.User) ? (panelWidth - bubbleWidth - 24) : 8;
            if (x < 8)
                x = 8;
            bubble.Location = new Point(x, y);
            transcriptPanel.Controls.Add(bubble);
            return y + bubbleHeight + 10;
        }
        // Arredonda os cantos de um controle via Region — mesmo truque leve
        // usado no Form1 (ApplyRoundedCorners), duplicado aqui porque é
        // privado lá e as duas classes não compartilham um helper comum
        // ainda. Se quiser evitar a duplicação, dá pra mover isso pra uma
        // classe utilitária estática compartilhada depois.
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
        // Barra de progresso desenhada à mão (em vez do ProgressBar padrão
        // do Windows/WinForms). Motivo: o controle nativo sempre pinta um
        // fundo cinza/branco fixo por trás do preenchimento — não dá pra
        // trocar essa cor sem interop nativo (SendMessage) que nem sempre
        // funciona com os temas visuais mais novos do Windows. Como essa
        // janela agora é escura, aquele retângulo claro do controle nativo
        // ficaria destoando muito. Esse controle simples só pinta um
        // "trilho" arredondado escuro e, por cima, um preenchimento em
        // gradiente roxo → ciano proporcional a Value (0–100) — a mesma
        // linguagem de cor usada no resto da janela.
        private class NeonProgressBar : Control
        {
            private int _value;
            public int Value
            {
                get { return _value; }
                set
                {
                    int clamped = Math.Max(0, Math.Min(100, value));
                    if (clamped == _value)
                        return;
                    _value = clamped;
                    Invalidate();
                }
            }
            public NeonProgressBar()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.UserPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw,
                    true);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle track = new Rectangle(0, 0, Math.Max(Width - 1, 1), Math.Max(Height - 1, 1));
                using (GraphicsPath trackPath = RoundedPath(track, track.Height / 2))
                using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(40, 43, 64)))
                {
                    e.Graphics.FillPath(trackBrush, trackPath);
                }
                if (_value > 0 && Width > 4)
                {
                    int fillWidth = (int)(Width * (_value / 100.0));
                    if (fillWidth < Height)
                        fillWidth = Math.Min(Height, Width);
                    Rectangle fill = new Rectangle(0, 0, Math.Max(fillWidth - 1, 1), track.Height);
                    using (GraphicsPath fillPath = RoundedPath(fill, fill.Height / 2))
                    using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                        new Rectangle(0, 0, Math.Max(Width, 1), Math.Max(Height, 1)),
                        Color.FromArgb(139, 92, 246), Color.FromArgb(34, 211, 238), 0f))
                    {
                        e.Graphics.FillPath(fillBrush, fillPath);
                    }
                }
            }
            private static GraphicsPath RoundedPath(Rectangle bounds, int radius)
            {
                int d = radius * 2;
                if (d > bounds.Height)
                    d = bounds.Height;
                if (d < 2)
                    d = 2;
                GraphicsPath path = new GraphicsPath();
                path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
                path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
                path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
                path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }
}
