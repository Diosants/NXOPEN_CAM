using System;
using System.Collections.Generic;
using System.Data.SqlClient;

// ══════════════════════════════════════════════════════════════════════
// ToolDatabase — camada única de acesso ao banco de ferramentas/materiais
// (SQL Server, Windows Authentication, instância local: localhost\SQLEXPRESS).
//
// SUBSTITUI (opcionalmente, com fallback automático) as tabelas hardcoded
// espalhadas em vários arquivos:
//   - MaterialTable / MaterialCuttingData  em FBM_ALL_FEATURES_MACHINED.cs
//   - MaterialTable / MaterialInfo         em AUTODRILL_GEOMETRIA_PROPRIA.cs
//   - RoughToolTable / FinishToolCandidatesAscending em FBM_ALL_FEATURES_MACHINED.cs
//   - ThreadTable (rosca)                  em AUTODRILL_GEOMETRIA_PROPRIA.cs
//   - SoqueteTable (soquete allen)         em AUTODRILL_GEOMETRIA_PROPRIA.cs
//
// COMO FUNCIONA: cada journal continua tendo sua tabela fixa interna (tal
// como já era, renomeada pra Default*). Antes de usar essa tabela fixa, o
// journal agora chama ToolDatabase.LoadXxx(log) - se o SQL Server estiver
// acessível E a tabela correspondente tiver linhas, usa os dados do banco;
// se QUALQUER coisa falhar (SQL Server desligado, instância errada, banco
// ainda não criado, sem permissão, tabela vazia, etc.), cai de volta pra
// tabela fixa interna, com um único aviso no log - NUNCA trava o journal
// por causa do banco. Ou seja: se você nunca configurar o banco, o
// comportamento continua EXATAMENTE igual ao de hoje.
//
// PRIMEIRA EXECUÇÃO: EnsureDatabaseReady() cria o banco "PATHNC_ToolDB" (se
// não existir), cria as 4 tabelas (se não existirem) e semeia cada uma com
// os MESMOS valores que já estavam hardcoded - só quando a tabela estiver
// vazia (nunca sobrescreve edições suas feitas depois, pela grade de
// gerenciamento). Isso é chamado automaticamente pelos Load*(), e também
// pode ser chamado manualmente (botão "Preparar Banco" na tela de
// gerenciamento).
//
// REQUISITO: SQL Server / SQL Server Express instalado e rodando NESTA
// máquina (Windows Authentication - usa a conta Windows logada, sem
// usuário/senha), instância "SQLEXPRESS" (o nome padrão de uma instalação
// "SQL Server Express" default). Se sua instância tiver outro nome, troque
// só a constante ServerInstance abaixo.
// ══════════════════════════════════════════════════════════════════════
public static class ToolDatabase
{
    // ── Troque aqui se o SQL Server não for local/instância padrão. ──
    private const string ServerInstance = @"localhost\SQLEXPRESS";
    private const string DatabaseName = "PATHNC_ToolDB";

    // Timeout curto de propósito: se o SQL Server não estiver acessível,
    // falha rápido (poucos segundos) e cai pro fallback hardcoded, em vez
    // de travar o journal por 15-30s esperando um servidor que não existe.
    private const int ConnectTimeoutSeconds = 3;

    private static string MasterConnectionString
    {
        get { return "Server=" + ServerInstance + ";Database=master;Integrated Security=True;Connect Timeout=" + ConnectTimeoutSeconds + ";TrustServerCertificate=True;"; }
    }

    public static string ConnectionString
    {
        get { return "Server=" + ServerInstance + ";Database=" + DatabaseName + ";Integrated Security=True;Connect Timeout=" + ConnectTimeoutSeconds + ";TrustServerCertificate=True;"; }
    }

    public static string ServerDescription
    {
        get { return ServerInstance + " / " + DatabaseName; }
    }

    // ══════════════════════════════════════════════════════════════════
    // DTOs — cada arquivo consumidor converte pra sua própria classe
    // interna (MaterialCuttingData, ToolSelection, ThreadInfo, SoqueteRef).
    // ToolDatabase não conhece nem depende dos tipos internos de cada
    // journal, só desses registros simples.
    // ══════════════════════════════════════════════════════════════════
    public class MaterialRow
    {
        public int Id;
        public string Code = "";
        public string Name = "";
        public double EndmillRoughVc;
        public double EndmillFinishVc;
        public double CutterVc;
        public double DrillVc;
        public int SortOrder;
    }

    public class ToolRow
    {
        public int Id;
        public string ToolName = "";
        public double Diameter;
        public bool IsCutter;
        public bool UseForRough;
        public bool UseForFinish;
    }

    public class ThreadRow
    {
        public int Id;
        public string SizeLabel = "";
        public double DrillDiameter;
        public double NominalDiam;
        public double Pitch;
        public string TapToolName = "";
    }

    public class SocketRow
    {
        public int Id;
        public string SizeLabel = "";
        public double HeadDiameter;
        public string GroupName = "";
    }

    // ══════════════════════════════════════════════════════════════════
    // CONEXÃO / DIAGNÓSTICO
    // ══════════════════════════════════════════════════════════════════
    public static bool TestConnection(out string message)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(MasterConnectionString))
            {
                conn.Open();
            }
            message = "OK - conectado em " + ServerInstance + " (Windows Authentication).";
            return true;
        }
        catch (Exception ex)
        {
            message = "Falha ao conectar em " + ServerInstance + ": " + ex.Message;
            return false;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // SETUP — cria banco/tabelas se faltarem, semeia se vazias. Idempotente
    // (seguro rodar toda vez / em todo Load*()).
    // ══════════════════════════════════════════════════════════════════
    public static bool EnsureDatabaseReady(Action<string> log)
    {
        try
        {
            using (SqlConnection conn = new SqlConnection(MasterConnectionString))
            {
                conn.Open();
                ExecNonQuery(conn, "IF DB_ID('" + DatabaseName + "') IS NULL CREATE DATABASE [" + DatabaseName + "];");
            }

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();

                ExecNonQuery(conn, @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Materials')
CREATE TABLE Materials (
    Id INT IDENTITY PRIMARY KEY,
    Code NVARCHAR(20) NOT NULL UNIQUE,
    Name NVARCHAR(100) NOT NULL,
    EndmillRoughVc FLOAT NOT NULL,
    EndmillFinishVc FLOAT NOT NULL,
    CutterVc FLOAT NOT NULL,
    DrillVc FLOAT NOT NULL,
    SortOrder INT NOT NULL DEFAULT 0
);");

                ExecNonQuery(conn, @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Tools')
CREATE TABLE Tools (
    Id INT IDENTITY PRIMARY KEY,
    ToolName NVARCHAR(50) NOT NULL UNIQUE,
    Diameter FLOAT NOT NULL,
    IsCutter BIT NOT NULL DEFAULT 0,
    UseForRough BIT NOT NULL DEFAULT 0,
    UseForFinish BIT NOT NULL DEFAULT 0
);");

                ExecNonQuery(conn, @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Threads')
CREATE TABLE Threads (
    Id INT IDENTITY PRIMARY KEY,
    SizeLabel NVARCHAR(10) NOT NULL UNIQUE,
    DrillDiameter FLOAT NOT NULL,
    NominalDiam FLOAT NOT NULL,
    Pitch FLOAT NOT NULL,
    TapToolName NVARCHAR(50) NOT NULL
);");

                ExecNonQuery(conn, @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Sockets')
CREATE TABLE Sockets (
    Id INT IDENTITY PRIMARY KEY,
    SizeLabel NVARCHAR(10) NOT NULL UNIQUE,
    HeadDiameter FLOAT NOT NULL,
    GroupName NVARCHAR(50) NOT NULL
);");

                SeedMaterialsIfEmpty(conn, log);
                SeedToolsIfEmpty(conn, log);
                SeedThreadsIfEmpty(conn, log);
                SeedSocketsIfEmpty(conn, log);
            }

            return true;
        }
        catch (Exception ex)
        {
            if (log != null)
                log("AVISO: ToolDatabase.EnsureDatabaseReady falhou (" + ex.Message + ") - os journals continuam funcionando com as tabelas fixas internas (fallback).");
            return false;
        }
    }

    private static void ExecNonQuery(SqlConnection conn, string sql)
    {
        using (SqlCommand cmd = new SqlCommand(sql, conn))
            cmd.ExecuteNonQuery();
    }

    private static int CountRows(SqlConnection conn, string table)
    {
        using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM " + table, conn))
            return (int)cmd.ExecuteScalar();
    }

    // ── Seed: os MESMOS valores que já estavam hardcoded em
    // FBM_ALL_FEATURES_MACHINED.cs (MaterialTable) - só grava se a tabela
    // estiver vazia, nunca sobrescreve. Nota: AUTODRILL_GEOMETRIA_PROPRIA.cs
    // tinha sua PRÓPRIA lista de materiais (com H13, sem Bronze, nomes em
    // inglês) - ela NÃO é usada aqui; o banco usa uma fonte única (esta),
    // igual à do FBM. Se precisar de H13 de volta, adicione pela tela de
    // gerenciamento. ──
    private static void SeedMaterialsIfEmpty(SqlConnection conn, Action<string> log)
    {
        if (CountRows(conn, "Materials") > 0) return;
        object[][] rows =
        {
            new object[] { "1020",   "Aço Carbono 1020",        140.0, 200.0, 170.0, 25.0, 0 },
            new object[] { "1045",   "Aço Carbono 1045",        100.0, 160.0, 130.0, 20.0, 1 },
            new object[] { "P20",    "Aço P20 (pré-temperado)",  70.0, 100.0,  90.0, 15.0, 2 },
            new object[] { "AL",     "Alumínio",                250.0, 400.0, 350.0, 80.0, 3 },
            new object[] { "BRONZE", "Bronze",                   90.0, 160.0, 120.0, 28.0, 4 },
            new object[] { "CU",     "Cobre",                   100.0, 180.0, 130.0, 30.0, 5 },
            new object[] { "NYLON",  "Nylon / Poliamida",       220.0, 280.0, 250.0, 60.0, 6 },
        };
        foreach (object[] r in rows)
        {
            using (SqlCommand cmd = new SqlCommand(
                "INSERT INTO Materials (Code, Name, EndmillRoughVc, EndmillFinishVc, CutterVc, DrillVc, SortOrder) VALUES (@c,@n,@er,@ef,@cu,@dv,@so)", conn))
            {
                cmd.Parameters.AddWithValue("@c", r[0]);
                cmd.Parameters.AddWithValue("@n", r[1]);
                cmd.Parameters.AddWithValue("@er", r[2]);
                cmd.Parameters.AddWithValue("@ef", r[3]);
                cmd.Parameters.AddWithValue("@cu", r[4]);
                cmd.Parameters.AddWithValue("@dv", r[5]);
                cmd.Parameters.AddWithValue("@so", r[6]);
                cmd.ExecuteNonQuery();
            }
        }
        if (log != null) log("ToolDatabase: tabela Materials semeada com " + rows.Length + " material(is) padrão.");
    }

    // ── Seed: união das tabelas RoughToolTable + FinishToolCandidatesAscending
    // de FBM_ALL_FEATURES_MACHINED.cs. ENDMILL_D12MM aparece nas duas
    // (UseForRough=1 E UseForFinish=1) - vira UMA linha só aqui. ──
    private static void SeedToolsIfEmpty(SqlConnection conn, Action<string> log)
    {
        if (CountRows(conn, "Tools") > 0) return;
        object[][] rows =
        {
            new object[] { "ENDMILL_D5MM",   5.0, false, true,  false },
            new object[] { "ENDMILL_D8MM",   8.0, false, true,  false },
            new object[] { "ENDMILL_D10MM", 10.0, false, false, true  },
            new object[] { "ENDMILL_D12MM", 12.0, false, true,  true  },
            new object[] { "ENDMILL_D14MM", 14.0, false, false, true  },
            new object[] { "ENDMILL_D16MM", 16.0, false, false, true  },
            new object[] { "ENDMILL_D18MM", 18.0, false, false, true  },
            new object[] { "CUTTER_D16_R.8", 16.0, true, true, false },
            new object[] { "CUTTER_D25_R.8", 25.0, true, true, false },
            new object[] { "CUTTER_D40_R1",  40.0, true, true, false },
        };
        foreach (object[] r in rows)
        {
            using (SqlCommand cmd = new SqlCommand(
                "INSERT INTO Tools (ToolName, Diameter, IsCutter, UseForRough, UseForFinish) VALUES (@tn,@d,@ic,@ur,@uf)", conn))
            {
                cmd.Parameters.AddWithValue("@tn", r[0]);
                cmd.Parameters.AddWithValue("@d", r[1]);
                cmd.Parameters.AddWithValue("@ic", r[2]);
                cmd.Parameters.AddWithValue("@ur", r[3]);
                cmd.Parameters.AddWithValue("@uf", r[4]);
                cmd.ExecuteNonQuery();
            }
        }
        if (log != null) log("ToolDatabase: tabela Tools semeada com " + rows.Length + " ferramenta(s) padrão (desbaste/acabamento).");
    }

    // ── Seed: mesma tabela de AUTODRILL_GEOMETRIA_PROPRIA.cs (BuildThreadTable). ──
    private static void SeedThreadsIfEmpty(SqlConnection conn, Action<string> log)
    {
        if (CountRows(conn, "Threads") > 0) return;
        object[][] rows =
        {
            new object[] { "M4",   3.3, 4.0,  0.7,  "TAP_M4X0.7"  },
            new object[] { "M5",   4.2, 5.0,  0.8,  "TAP_M5X0.8"  },
            new object[] { "M6",   5.0, 6.0,  1.0,  "TAP_M6X1.0"  },
            new object[] { "M8",   6.8, 8.0,  1.25, "TAP_M8X1.25" },
            new object[] { "M10",  8.5, 10.0, 1.5,  "TAP_M10X1.5" },
            new object[] { "M12", 10.2, 12.0, 1.75, "TAP_M12X1.75"},
            new object[] { "M14", 12.0, 14.0, 2.0,  "TAP_M14X2.0" },
            new object[] { "M16", 14.0, 16.0, 2.0,  "TAP_M16X2.0" },
            new object[] { "M18", 15.5, 18.0, 2.5,  "TAP_M18X2.5" },
            new object[] { "M20", 17.5, 20.0, 2.5,  "TAP_M20X2.5" },
            new object[] { "M22", 19.5, 22.0, 2.5,  "TAP_M22X2.5" },
            new object[] { "M24", 21.0, 24.0, 3.0,  "TAP_M24X3.0" },
        };
        foreach (object[] r in rows)
        {
            using (SqlCommand cmd = new SqlCommand(
                "INSERT INTO Threads (SizeLabel, DrillDiameter, NominalDiam, Pitch, TapToolName) VALUES (@sl,@dd,@nd,@p,@tt)", conn))
            {
                cmd.Parameters.AddWithValue("@sl", r[0]);
                cmd.Parameters.AddWithValue("@dd", r[1]);
                cmd.Parameters.AddWithValue("@nd", r[2]);
                cmd.Parameters.AddWithValue("@p", r[3]);
                cmd.Parameters.AddWithValue("@tt", r[4]);
                cmd.ExecuteNonQuery();
            }
        }
        if (log != null) log("ToolDatabase: tabela Threads semeada com " + rows.Length + " rosca(s) padrão.");
    }

    // ── Seed: mesma tabela de AUTODRILL_GEOMETRIA_PROPRIA.cs (BuildSoqueteTable). ──
    private static void SeedSocketsIfEmpty(SqlConnection conn, Action<string> log)
    {
        if (CountRows(conn, "Sockets") > 0) return;
        object[][] rows =
        {
            new object[] { "M4",   8.0 },
            new object[] { "M5",  10.0 },
            new object[] { "M6",  11.0 },
            new object[] { "M8",  14.0 },
            new object[] { "M10", 18.0 },
            new object[] { "M12", 20.0 },
            new object[] { "M16", 26.0 },
            new object[] { "M20", 33.0 },
            new object[] { "M24", 40.0 },
            new object[] { "M30", 50.0 },
        };
        foreach (object[] r in rows)
        {
            string sizeLabel = (string)r[0];
            using (SqlCommand cmd = new SqlCommand(
                "INSERT INTO Sockets (SizeLabel, HeadDiameter, GroupName) VALUES (@sl,@hd,@gn)", conn))
            {
                cmd.Parameters.AddWithValue("@sl", sizeLabel);
                cmd.Parameters.AddWithValue("@hd", r[1]);
                cmd.Parameters.AddWithValue("@gn", sizeLabel + "_SOCKET_HEAD");
                cmd.ExecuteNonQuery();
            }
        }
        if (log != null) log("ToolDatabase: tabela Sockets semeada com " + rows.Length + " soquete(s) padrão.");
    }

    // ══════════════════════════════════════════════════════════════════
    // LOAD (uso pelos journals) — NUNCA lança exceção: qualquer falha
    // (servidor fora do ar, banco não configurado, etc.) devolve null e
    // escreve UM aviso no log passado; quem chamou cai pra tabela fixa.
    // ══════════════════════════════════════════════════════════════════
    public static List<MaterialRow> LoadMaterials(Action<string> log)
    {
        try
        {
            EnsureDatabaseReady(null);
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                List<MaterialRow> list = new List<MaterialRow>();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT Id, Code, Name, EndmillRoughVc, EndmillFinishVc, CutterVc, DrillVc, SortOrder FROM Materials ORDER BY SortOrder, Id", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new MaterialRow
                        {
                            Id = r.GetInt32(0),
                            Code = r.GetString(1),
                            Name = r.GetString(2),
                            EndmillRoughVc = r.GetDouble(3),
                            EndmillFinishVc = r.GetDouble(4),
                            CutterVc = r.GetDouble(5),
                            DrillVc = r.GetDouble(6),
                            SortOrder = r.GetInt32(7),
                        });
                    }
                }
                if (list.Count == 0) return null;
                return list;
            }
        }
        catch (Exception ex)
        {
            if (log != null) log("AVISO: não foi possível ler 'Materials' do banco (" + ex.Message + ") - usando tabela fixa interna.");
            return null;
        }
    }

    public static List<ToolRow> LoadRoughTools(Action<string> log) { return LoadTools("UseForRough = 1", log); }
    public static List<ToolRow> LoadFinishTools(Action<string> log) { return LoadTools("UseForFinish = 1", log); }

    private static List<ToolRow> LoadTools(string whereClause, Action<string> log)
    {
        try
        {
            EnsureDatabaseReady(null);
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                List<ToolRow> list = new List<ToolRow>();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT Id, ToolName, Diameter, IsCutter, UseForRough, UseForFinish FROM Tools WHERE " + whereClause + " ORDER BY Diameter", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new ToolRow
                        {
                            Id = r.GetInt32(0),
                            ToolName = r.GetString(1),
                            Diameter = r.GetDouble(2),
                            IsCutter = r.GetBoolean(3),
                            UseForRough = r.GetBoolean(4),
                            UseForFinish = r.GetBoolean(5),
                        });
                    }
                }
                if (list.Count == 0) return null;
                return list;
            }
        }
        catch (Exception ex)
        {
            if (log != null) log("AVISO: não foi possível ler 'Tools' do banco (" + ex.Message + ") - usando tabela fixa interna.");
            return null;
        }
    }

    public static List<ThreadRow> LoadThreads(Action<string> log)
    {
        try
        {
            EnsureDatabaseReady(null);
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                List<ThreadRow> list = new List<ThreadRow>();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT Id, SizeLabel, DrillDiameter, NominalDiam, Pitch, TapToolName FROM Threads ORDER BY DrillDiameter", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new ThreadRow
                        {
                            Id = r.GetInt32(0),
                            SizeLabel = r.GetString(1),
                            DrillDiameter = r.GetDouble(2),
                            NominalDiam = r.GetDouble(3),
                            Pitch = r.GetDouble(4),
                            TapToolName = r.GetString(5),
                        });
                    }
                }
                if (list.Count == 0) return null;
                return list;
            }
        }
        catch (Exception ex)
        {
            if (log != null) log("AVISO: não foi possível ler 'Threads' do banco (" + ex.Message + ") - usando tabela fixa interna.");
            return null;
        }
    }

    public static List<SocketRow> LoadSockets(Action<string> log)
    {
        try
        {
            EnsureDatabaseReady(null);
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                List<SocketRow> list = new List<SocketRow>();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT Id, SizeLabel, HeadDiameter, GroupName FROM Sockets ORDER BY HeadDiameter", conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new SocketRow
                        {
                            Id = r.GetInt32(0),
                            SizeLabel = r.GetString(1),
                            HeadDiameter = r.GetDouble(2),
                            GroupName = r.GetString(3),
                        });
                    }
                }
                if (list.Count == 0) return null;
                return list;
            }
        }
        catch (Exception ex)
        {
            if (log != null) log("AVISO: não foi possível ler 'Sockets' do banco (" + ex.Message + ") - usando tabela fixa interna.");
            return null;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // CRUD (uso exclusivo da tela de gerenciamento, ToolDatabaseManagerForm)
    // — aqui as exceções SÃO propagadas de propósito, pra tela mostrar o
    // erro real do SQL Server ao usuário em vez de falhar silenciosamente.
    // ══════════════════════════════════════════════════════════════════
    public static List<MaterialRow> GetAllMaterialsForEditing()
    {
        EnsureDatabaseReady(null);
        List<MaterialRow> list = new List<MaterialRow>();
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            using (SqlCommand cmd = new SqlCommand(
                "SELECT Id, Code, Name, EndmillRoughVc, EndmillFinishVc, CutterVc, DrillVc, SortOrder FROM Materials ORDER BY SortOrder, Id", conn))
            using (SqlDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new MaterialRow
                    {
                        Id = r.GetInt32(0), Code = r.GetString(1), Name = r.GetString(2),
                        EndmillRoughVc = r.GetDouble(3), EndmillFinishVc = r.GetDouble(4),
                        CutterVc = r.GetDouble(5), DrillVc = r.GetDouble(6), SortOrder = r.GetInt32(7),
                    });
                }
            }
        }
        return list;
    }

    public static void SaveMaterial(MaterialRow m)
    {
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            string sql = (m.Id == 0)
                ? "INSERT INTO Materials (Code, Name, EndmillRoughVc, EndmillFinishVc, CutterVc, DrillVc, SortOrder) VALUES (@c,@n,@er,@ef,@cu,@dv,@so)"
                : "UPDATE Materials SET Code=@c, Name=@n, EndmillRoughVc=@er, EndmillFinishVc=@ef, CutterVc=@cu, DrillVc=@dv, SortOrder=@so WHERE Id=@id";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@c", m.Code);
                cmd.Parameters.AddWithValue("@n", m.Name);
                cmd.Parameters.AddWithValue("@er", m.EndmillRoughVc);
                cmd.Parameters.AddWithValue("@ef", m.EndmillFinishVc);
                cmd.Parameters.AddWithValue("@cu", m.CutterVc);
                cmd.Parameters.AddWithValue("@dv", m.DrillVc);
                cmd.Parameters.AddWithValue("@so", m.SortOrder);
                if (m.Id != 0) cmd.Parameters.AddWithValue("@id", m.Id);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static void DeleteMaterial(int id)
    {
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            using (SqlCommand cmd = new SqlCommand("DELETE FROM Materials WHERE Id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static List<ToolRow> GetAllToolsForEditing()
    {
        EnsureDatabaseReady(null);
        List<ToolRow> list = new List<ToolRow>();
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            using (SqlCommand cmd = new SqlCommand(
                "SELECT Id, ToolName, Diameter, IsCutter, UseForRough, UseForFinish FROM Tools ORDER BY Diameter", conn))
            using (SqlDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new ToolRow
                    {
                        Id = r.GetInt32(0), ToolName = r.GetString(1), Diameter = r.GetDouble(2),
                        IsCutter = r.GetBoolean(3), UseForRough = r.GetBoolean(4), UseForFinish = r.GetBoolean(5),
                    });
                }
            }
        }
        return list;
    }

    public static void SaveTool(ToolRow t)
    {
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            string sql = (t.Id == 0)
                ? "INSERT INTO Tools (ToolName, Diameter, IsCutter, UseForRough, UseForFinish) VALUES (@tn,@d,@ic,@ur,@uf)"
                : "UPDATE Tools SET ToolName=@tn, Diameter=@d, IsCutter=@ic, UseForRough=@ur, UseForFinish=@uf WHERE Id=@id";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@tn", t.ToolName);
                cmd.Parameters.AddWithValue("@d", t.Diameter);
                cmd.Parameters.AddWithValue("@ic", t.IsCutter);
                cmd.Parameters.AddWithValue("@ur", t.UseForRough);
                cmd.Parameters.AddWithValue("@uf", t.UseForFinish);
                if (t.Id != 0) cmd.Parameters.AddWithValue("@id", t.Id);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static void DeleteTool(int id)
    {
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            using (SqlCommand cmd = new SqlCommand("DELETE FROM Tools WHERE Id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static List<ThreadRow> GetAllThreadsForEditing()
    {
        EnsureDatabaseReady(null);
        List<ThreadRow> list = new List<ThreadRow>();
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            using (SqlCommand cmd = new SqlCommand(
                "SELECT Id, SizeLabel, DrillDiameter, NominalDiam, Pitch, TapToolName FROM Threads ORDER BY DrillDiameter", conn))
            using (SqlDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new ThreadRow
                    {
                        Id = r.GetInt32(0), SizeLabel = r.GetString(1), DrillDiameter = r.GetDouble(2),
                        NominalDiam = r.GetDouble(3), Pitch = r.GetDouble(4), TapToolName = r.GetString(5),
                    });
                }
            }
        }
        return list;
    }

    public static void SaveThread(ThreadRow t)
    {
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            string sql = (t.Id == 0)
                ? "INSERT INTO Threads (SizeLabel, DrillDiameter, NominalDiam, Pitch, TapToolName) VALUES (@sl,@dd,@nd,@p,@tt)"
                : "UPDATE Threads SET SizeLabel=@sl, DrillDiameter=@dd, NominalDiam=@nd, Pitch=@p, TapToolName=@tt WHERE Id=@id";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@sl", t.SizeLabel);
                cmd.Parameters.AddWithValue("@dd", t.DrillDiameter);
                cmd.Parameters.AddWithValue("@nd", t.NominalDiam);
                cmd.Parameters.AddWithValue("@p", t.Pitch);
                cmd.Parameters.AddWithValue("@tt", t.TapToolName);
                if (t.Id != 0) cmd.Parameters.AddWithValue("@id", t.Id);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static void DeleteThread(int id)
    {
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            using (SqlCommand cmd = new SqlCommand("DELETE FROM Threads WHERE Id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static List<SocketRow> GetAllSocketsForEditing()
    {
        EnsureDatabaseReady(null);
        List<SocketRow> list = new List<SocketRow>();
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            using (SqlCommand cmd = new SqlCommand(
                "SELECT Id, SizeLabel, HeadDiameter, GroupName FROM Sockets ORDER BY HeadDiameter", conn))
            using (SqlDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new SocketRow
                    {
                        Id = r.GetInt32(0), SizeLabel = r.GetString(1), HeadDiameter = r.GetDouble(2), GroupName = r.GetString(3),
                    });
                }
            }
        }
        return list;
    }

    public static void SaveSocket(SocketRow s)
    {
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            string sql = (s.Id == 0)
                ? "INSERT INTO Sockets (SizeLabel, HeadDiameter, GroupName) VALUES (@sl,@hd,@gn)"
                : "UPDATE Sockets SET SizeLabel=@sl, HeadDiameter=@hd, GroupName=@gn WHERE Id=@id";
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@sl", s.SizeLabel);
                cmd.Parameters.AddWithValue("@hd", s.HeadDiameter);
                cmd.Parameters.AddWithValue("@gn", s.GroupName);
                if (s.Id != 0) cmd.Parameters.AddWithValue("@id", s.Id);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public static void DeleteSocket(int id)
    {
        using (SqlConnection conn = new SqlConnection(ConnectionString))
        {
            conn.Open();
            using (SqlCommand cmd = new SqlCommand("DELETE FROM Sockets WHERE Id=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }
    }
}
