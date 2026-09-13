using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

// ══════════════════════════════════════════════════════════════════════
// ToolDatabaseManagerForm — tela simples de edição (grade) das 4 tabelas
// do ToolDatabase.cs (Materiais / Ferramentas / Roscas / Soquetes).
//
// Abra com: new ToolDatabaseManagerForm().ShowDialog();
//
// Cada aba carrega os dados do SQL Server ao abrir (chamando
// ToolDatabase.EnsureDatabaseReady() primeiro, que cria banco/tabelas e
// semeia com os valores padrão na primeira vez que alguém abrir esta
// tela). "Add Row" insere uma linha nova em branco na grade (só grava no
// banco quando você clicar "Save All Changes"); "Delete Selected" apaga
// IMEDIATAMENTE do banco se a linha já existir lá (pede confirmação).
//
// Se o SQL Server não estiver acessível, a tela mostra o erro exato (em
// vez de cair silenciosamente pro fallback como os journals fazem) - é
// justamente aqui que você quer saber o que está errado com a conexão.
// ══════════════════════════════════════════════════════════════════════
public class ToolDatabaseManagerForm : Form
{
    private Label lblStatus;
    private TabControl tabs;

    private DataGridView dgvMaterials;
    private BindingList<MaterialVM> materialRows;

    private DataGridView dgvTools;
    private BindingList<ToolVM> toolRows;

    private DataGridView dgvThreads;
    private BindingList<ThreadVM> threadRows;

    private DataGridView dgvSockets;
    private BindingList<SocketVM> socketRows;

    public ToolDatabaseManagerForm()
    {
        Text = "PATHNC - Banco de Ferramentas e Materiais";
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9F);
        ClientSize = new Size(760, 560);
        MinimumSize = new Size(640, 420);

        BuildTopBar();
        BuildTabs();

        Load += (s, e) => RefreshAllTabs();
    }

    // ── VIEW MODELS (propriedades, não campos - o DataGridView precisa de
    // propriedades pra gerar/editar colunas automaticamente). Convertidos
    // de/pra ToolDatabase.*Row na hora de carregar/salvar. ──
    private class MaterialVM
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public double EndmillRoughVc { get; set; }
        public double EndmillFinishVc { get; set; }
        public double CutterVc { get; set; }
        public double DrillVc { get; set; }
        public int SortOrder { get; set; }
    }

    private class ToolVM
    {
        public int Id { get; set; }
        public string ToolName { get; set; }
        public double Diameter { get; set; }
        public bool IsCutter { get; set; }
        public bool UseForRough { get; set; }
        public bool UseForFinish { get; set; }
    }

    private class ThreadVM
    {
        public int Id { get; set; }
        public string SizeLabel { get; set; }
        public double DrillDiameter { get; set; }
        public double NominalDiam { get; set; }
        public double Pitch { get; set; }
        public string TapToolName { get; set; }
    }

    private class SocketVM
    {
        public int Id { get; set; }
        public string SizeLabel { get; set; }
        public double HeadDiameter { get; set; }
        public string GroupName { get; set; }
    }

    private void BuildTopBar()
    {
        Panel top = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8) };

        lblStatus = new Label
        {
            Text = "Servidor: " + ToolDatabase.ServerDescription,
            Location = new Point(8, 12),
            AutoSize = true,
            ForeColor = Color.FromArgb(80, 80, 84),
        };
        top.Controls.Add(lblStatus);

        Button btnTest = new Button { Text = "Testar Conexão", Location = new Point(340, 6), Size = new Size(110, 26), FlatStyle = FlatStyle.Flat };
        btnTest.Click += (s, e) =>
        {
            string msg;
            bool ok = ToolDatabase.TestConnection(out msg);
            lblStatus.Text = msg;
            lblStatus.ForeColor = ok ? Color.FromArgb(21, 128, 61) : Color.FromArgb(185, 28, 28);
        };
        top.Controls.Add(btnTest);

        Button btnPrepare = new Button { Text = "Preparar Banco", Location = new Point(456, 6), Size = new Size(110, 26), FlatStyle = FlatStyle.Flat };
        btnPrepare.Click += (s, e) =>
        {
            List<string> log = new List<string>();
            bool ok = ToolDatabase.EnsureDatabaseReady(l => log.Add(l));
            if (ok)
            {
                lblStatus.Text = "Banco pronto (" + ToolDatabase.ServerDescription + ").";
                lblStatus.ForeColor = Color.FromArgb(21, 128, 61);
                RefreshAllTabs();
            }
            else
            {
                lblStatus.Text = "Falha ao preparar o banco - ver detalhes abaixo.";
                lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
            }
            string detail = log.Count > 0 ? string.Join(Environment.NewLine, log) : "(sem mensagens)";
            MessageBox.Show(detail, "Preparar Banco", MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        };
        top.Controls.Add(btnPrepare);

        Button btnRefresh = new Button { Text = "Recarregar Tudo", Location = new Point(572, 6), Size = new Size(110, 26), FlatStyle = FlatStyle.Flat };
        btnRefresh.Click += (s, e) => RefreshAllTabs();
        top.Controls.Add(btnRefresh);

        Controls.Add(top);
    }

    private void BuildTabs()
    {
        tabs = new TabControl { Dock = DockStyle.Fill };

        tabs.TabPages.Add(BuildMaterialsTab());
        tabs.TabPages.Add(BuildToolsTab());
        tabs.TabPages.Add(BuildThreadsTab());
        tabs.TabPages.Add(BuildSocketsTab());

        Controls.Add(tabs);
    }

    // ══════════════ MATERIAIS ══════════════
    private TabPage BuildMaterialsTab()
    {
        TabPage page = new TabPage("Materiais");
        materialRows = new BindingList<MaterialVM>();
        dgvMaterials = MakeGrid(page, materialRows);

        AddButtonBar(page,
            () => materialRows.Add(new MaterialVM { Code = "NOVO", Name = "Novo material", EndmillRoughVc = 100, EndmillFinishVc = 150, CutterVc = 120, DrillVc = 20, SortOrder = materialRows.Count }),
            () =>
            {
                MaterialVM sel = CurrentRow(dgvMaterials) as MaterialVM;
                if (sel == null) return;
                if (sel.Id != 0)
                {
                    if (MessageBox.Show("Apagar material '" + sel.Name + "' do banco?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                    ToolDatabase.DeleteMaterial(sel.Id);
                    InvalidateMaterialCaches();
                }
                materialRows.Remove(sel);
            },
            () =>
            {
                foreach (MaterialVM m in materialRows)
                    ToolDatabase.SaveMaterial(new ToolDatabase.MaterialRow { Id = m.Id, Code = m.Code, Name = m.Name, EndmillRoughVc = m.EndmillRoughVc, EndmillFinishVc = m.EndmillFinishVc, CutterVc = m.CutterVc, DrillVc = m.DrillVc, SortOrder = m.SortOrder });
                InvalidateMaterialCaches();
                RefreshMaterials();
                MessageBox.Show("Materiais salvos - já valem no próximo Run() desta sessão.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

        return page;
    }

    private void RefreshMaterials()
    {
        materialRows.RaiseListChangedEvents = false;
        materialRows.Clear();
        try
        {
            foreach (ToolDatabase.MaterialRow r in ToolDatabase.GetAllMaterialsForEditing())
                materialRows.Add(new MaterialVM { Id = r.Id, Code = r.Code, Name = r.Name, EndmillRoughVc = r.EndmillRoughVc, EndmillFinishVc = r.EndmillFinishVc, CutterVc = r.CutterVc, DrillVc = r.DrillVc, SortOrder = r.SortOrder });
        }
        finally
        {
            materialRows.RaiseListChangedEvents = true;
            materialRows.ResetBindings();
        }
    }

    // ══════════════ FERRAMENTAS ══════════════
    private TabPage BuildToolsTab()
    {
        TabPage page = new TabPage("Ferramentas");
        toolRows = new BindingList<ToolVM>();
        dgvTools = MakeGrid(page, toolRows);

        AddButtonBar(page,
            () => toolRows.Add(new ToolVM { ToolName = "NOVA_FERRAMENTA", Diameter = 10, IsCutter = false, UseForRough = false, UseForFinish = false }),
            () =>
            {
                ToolVM sel = CurrentRow(dgvTools) as ToolVM;
                if (sel == null) return;
                if (sel.Id != 0)
                {
                    if (MessageBox.Show("Apagar ferramenta '" + sel.ToolName + "' do banco?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                    ToolDatabase.DeleteTool(sel.Id);
                    FBM_ALL_FEATURES_MACHINED.InvalidateDatabaseCaches();
                }
                toolRows.Remove(sel);
            },
            () =>
            {
                foreach (ToolVM t in toolRows)
                    ToolDatabase.SaveTool(new ToolDatabase.ToolRow { Id = t.Id, ToolName = t.ToolName, Diameter = t.Diameter, IsCutter = t.IsCutter, UseForRough = t.UseForRough, UseForFinish = t.UseForFinish });
                FBM_ALL_FEATURES_MACHINED.InvalidateDatabaseCaches();
                RefreshTools();
                MessageBox.Show("Ferramentas salvas - já valem no próximo Run() desta sessão.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

        return page;
    }

    private void RefreshTools()
    {
        toolRows.RaiseListChangedEvents = false;
        toolRows.Clear();
        try
        {
            foreach (ToolDatabase.ToolRow r in ToolDatabase.GetAllToolsForEditing())
                toolRows.Add(new ToolVM { Id = r.Id, ToolName = r.ToolName, Diameter = r.Diameter, IsCutter = r.IsCutter, UseForRough = r.UseForRough, UseForFinish = r.UseForFinish });
        }
        finally
        {
            toolRows.RaiseListChangedEvents = true;
            toolRows.ResetBindings();
        }
    }

    // ══════════════ ROSCAS ══════════════
    private TabPage BuildThreadsTab()
    {
        TabPage page = new TabPage("Roscas");
        threadRows = new BindingList<ThreadVM>();
        dgvThreads = MakeGrid(page, threadRows);

        AddButtonBar(page,
            () => threadRows.Add(new ThreadVM { SizeLabel = "M#", DrillDiameter = 0, NominalDiam = 0, Pitch = 0, TapToolName = "TAP_M#X#" }),
            () =>
            {
                ThreadVM sel = CurrentRow(dgvThreads) as ThreadVM;
                if (sel == null) return;
                if (sel.Id != 0)
                {
                    if (MessageBox.Show("Apagar rosca '" + sel.SizeLabel + "' do banco?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                    ToolDatabase.DeleteThread(sel.Id);
                }
                threadRows.Remove(sel);
            },
            () =>
            {
                foreach (ThreadVM t in threadRows)
                    ToolDatabase.SaveThread(new ToolDatabase.ThreadRow { Id = t.Id, SizeLabel = t.SizeLabel, DrillDiameter = t.DrillDiameter, NominalDiam = t.NominalDiam, Pitch = t.Pitch, TapToolName = t.TapToolName });
                RefreshThreads();
                MessageBox.Show("Roscas salvas.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

        return page;
    }

    private void RefreshThreads()
    {
        threadRows.RaiseListChangedEvents = false;
        threadRows.Clear();
        try
        {
            foreach (ToolDatabase.ThreadRow r in ToolDatabase.GetAllThreadsForEditing())
                threadRows.Add(new ThreadVM { Id = r.Id, SizeLabel = r.SizeLabel, DrillDiameter = r.DrillDiameter, NominalDiam = r.NominalDiam, Pitch = r.Pitch, TapToolName = r.TapToolName });
        }
        finally
        {
            threadRows.RaiseListChangedEvents = true;
            threadRows.ResetBindings();
        }
    }

    // ══════════════ SOQUETES ══════════════
    private TabPage BuildSocketsTab()
    {
        TabPage page = new TabPage("Soquetes");
        socketRows = new BindingList<SocketVM>();
        dgvSockets = MakeGrid(page, socketRows);

        AddButtonBar(page,
            () => socketRows.Add(new SocketVM { SizeLabel = "M#", HeadDiameter = 0, GroupName = "M#_SOCKET_HEAD" }),
            () =>
            {
                SocketVM sel = CurrentRow(dgvSockets) as SocketVM;
                if (sel == null) return;
                if (sel.Id != 0)
                {
                    if (MessageBox.Show("Apagar soquete '" + sel.SizeLabel + "' do banco?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                    ToolDatabase.DeleteSocket(sel.Id);
                }
                socketRows.Remove(sel);
            },
            () =>
            {
                foreach (SocketVM sck in socketRows)
                    ToolDatabase.SaveSocket(new ToolDatabase.SocketRow { Id = sck.Id, SizeLabel = sck.SizeLabel, HeadDiameter = sck.HeadDiameter, GroupName = sck.GroupName });
                RefreshSockets();
                MessageBox.Show("Soquetes salvos.", "Save", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

        return page;
    }

    private void RefreshSockets()
    {
        socketRows.RaiseListChangedEvents = false;
        socketRows.Clear();
        try
        {
            foreach (ToolDatabase.SocketRow r in ToolDatabase.GetAllSocketsForEditing())
                socketRows.Add(new SocketVM { Id = r.Id, SizeLabel = r.SizeLabel, HeadDiameter = r.HeadDiameter, GroupName = r.GroupName });
        }
        finally
        {
            socketRows.RaiseListChangedEvents = true;
            socketRows.ResetBindings();
        }
    }

    // Materiais é lido/cacheado tanto por FBM_ALL_FEATURES_MACHINED.cs
    // quanto por AUTODRILL_GEOMETRIA_PROPRIA.cs (fonte única, ver
    // ToolDatabase.cs) - os dois precisam ser avisados quando a tabela
    // Materiais muda, senão um dos dois continuaria com o valor antigo em
    // memória até você fechar e abrir o PATHNC AUTOMATION de novo.
    private void InvalidateMaterialCaches()
    {
        FBM_ALL_FEATURES_MACHINED.InvalidateDatabaseCaches();
        AUTODRILL_GEOMETRIA_PROPRIA.InvalidateDatabaseCaches();
    }

    // ══════════════ HELPERS DE UI (compartilhados pelas 4 abas) ══════════════
    private DataGridView MakeGrid<T>(TabPage page, BindingList<T> source)
    {
        DataGridView grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            DataSource = source,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            Font = new Font("Segoe UI", 8.5F),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        };
        page.Controls.Add(grid);
        return grid;
    }

    private void AddButtonBar(TabPage page, Action onAdd, Action onDelete, Action onSave)
    {
        Panel bar = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(4) };

        Button btnAdd = new Button { Text = "Add Row", Location = new Point(4, 6), Size = new Size(90, 28), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(22, 163, 74), ForeColor = Color.White };
        btnAdd.Click += (s, e) => onAdd();
        bar.Controls.Add(btnAdd);

        Button btnDel = new Button { Text = "Delete Selected", Location = new Point(100, 6), Size = new Size(120, 28), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White };
        btnDel.Click += (s, e) => onDelete();
        bar.Controls.Add(btnDel);

        Button btnSave = new Button { Text = "Save All Changes", Location = new Point(226, 6), Size = new Size(140, 28), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White };
        btnSave.Click += (s, e) =>
        {
            try { onSave(); }
            catch (Exception ex) { MessageBox.Show("Erro salvando: " + ex.Message, "Save", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        };
        bar.Controls.Add(btnSave);

        page.Controls.Add(bar);
        // Painel de baixo precisa ser adicionado DEPOIS do grid (que é Fill)
        // pra reservar espaço - mesma regra de ordem de docking usada em
        // Form1.BuildUi() pro statusBar.
        bar.BringToFront();
    }

    private object CurrentRow(DataGridView grid)
    {
        if (grid.CurrentRow == null) return null;
        return grid.CurrentRow.DataBoundItem;
    }

    private void RefreshAllTabs()
    {
        try
        {
            RefreshMaterials();
            RefreshTools();
            RefreshThreads();
            RefreshSockets();
            lblStatus.Text = "Conectado - " + ToolDatabase.ServerDescription;
            lblStatus.ForeColor = Color.FromArgb(21, 128, 61);
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Não foi possível carregar do banco: " + ex.Message;
            lblStatus.ForeColor = Color.FromArgb(185, 28, 28);
        }
    }
}
