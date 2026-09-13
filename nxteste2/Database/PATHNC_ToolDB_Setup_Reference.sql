-- ══════════════════════════════════════════════════════════════════════
-- PATHNC_ToolDB - script de referência (NÃO é obrigatório rodar isso na
-- mão). ToolDatabase.cs já cria o banco, as tabelas e semeia os dados
-- padrão sozinho, na primeira vez que qualquer journal ou a tela
-- "Manage Tool / Material Database" tentar ler/gravar algo.
--
-- Esse script existe só de referência/documentação (pra você inspecionar
-- no SQL Server Management Studio, ou rodar manualmente se preferir criar
-- o banco você mesmo antes da primeira execução). É seguro rodar mais de
-- uma vez - cada CREATE só age se a tabela ainda não existir.
--
-- Instância usada por padrão pelo ToolDatabase.cs: localhost\SQLEXPRESS
-- Autenticação: Windows Authentication (conta Windows logada)
-- ══════════════════════════════════════════════════════════════════════

IF DB_ID('PATHNC_ToolDB') IS NULL
    CREATE DATABASE [PATHNC_ToolDB];
GO

USE [PATHNC_ToolDB];
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Materials')
CREATE TABLE Materials (
    Id INT IDENTITY PRIMARY KEY,
    Code NVARCHAR(20) NOT NULL UNIQUE,
    Name NVARCHAR(100) NOT NULL,
    EndmillRoughVc FLOAT NOT NULL,   -- Vc (m/min) endmill/broca de topo - desbaste
    EndmillFinishVc FLOAT NOT NULL,  -- Vc (m/min) endmill/broca de topo - acabamento
    CutterVc FLOAT NOT NULL,         -- Vc (m/min) cutter/facemill com pastilha
    DrillVc FLOAT NOT NULL,          -- Vc (m/min) broca HSS (furação/counterbore)
    SortOrder INT NOT NULL DEFAULT 0 -- ordem de exibição no combo de seleção; índice 0 = material padrão se o diálogo for cancelado
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Tools')
CREATE TABLE Tools (
    Id INT IDENTITY PRIMARY KEY,
    ToolName NVARCHAR(50) NOT NULL UNIQUE, -- precisa bater com o nome real da ferramenta na biblioteca CAM do NX
    Diameter FLOAT NOT NULL,
    IsCutter BIT NOT NULL DEFAULT 0,       -- 1 = cutter/facemill com pastilha; 0 = endmill inteiriço
    UseForRough BIT NOT NULL DEFAULT 0,    -- candidata a ferramenta de DESBASTE (largura da figura)
    UseForFinish BIT NOT NULL DEFAULT 0    -- candidata a ferramenta de ACABAMENTO (raio de canto)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Threads')
CREATE TABLE Threads (
    Id INT IDENTITY PRIMARY KEY,
    SizeLabel NVARCHAR(10) NOT NULL UNIQUE,  -- M4, M5, M6...
    DrillDiameter FLOAT NOT NULL,            -- diâmetro da broca de macho (mm)
    NominalDiam FLOAT NOT NULL,              -- diâmetro nominal da rosca (mm)
    Pitch FLOAT NOT NULL,                    -- passo (mm)
    TapToolName NVARCHAR(50) NOT NULL        -- precisa bater com o nome real do macho na biblioteca CAM do NX
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Sockets')
CREATE TABLE Sockets (
    Id INT IDENTITY PRIMARY KEY,
    SizeLabel NVARCHAR(10) NOT NULL UNIQUE,  -- M4, M5, M6... (tamanho do parafuso allen)
    HeadDiameter FLOAT NOT NULL,             -- diâmetro da cabeça (mm) - usado pra achar a ferramenta CBORE mais próxima
    GroupName NVARCHAR(50) NOT NULL          -- nome do grupo de feature associado (ex.: M10_SOCKET_HEAD)
);
GO

-- Os INSERTs de seed (valores padrão = os mesmos que já estavam
-- hardcoded no código antes desta migração) são feitos pelo próprio
-- ToolDatabase.cs (SeedMaterialsIfEmpty / SeedToolsIfEmpty /
-- SeedThreadsIfEmpty / SeedSocketsIfEmpty), só quando a tabela
-- correspondente estiver vazia - não precisa repetir aqui.
