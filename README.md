# PATHNC AUTOMATION

**NX CAM automation suite for mold & tooling shops — NXOpen C#, Windows Forms, SQL Server.**

A portfolio project by Diogenes · CADCAMWise · 18+ years of CNC/CAM programming turned into code.

![panel](docs/img/panel.png)

> Not a finished product. This is a working lab where I explore how far NX CAM can be automated with NXOpen, and how a shop's own machining history can drive strategy and cost decisions. Everything runs inside Siemens NX 2406.

---

## What's in it

| Area | Highlights | Key files |
|---|---|---|
| **Feature-based machining** | Feature recognition → feature groups → automatic drilling, counterbore, tapping and countersink cycle with material-aware tool selection; hole classification by category (thread, H7, socket head, mold pillar) painted on the model | `Modules/FBM/`, `Core/Features/` |
| **3+2 axis pipeline** | Detects the part's real access directions, creates one MCS per direction, runs rough → rest mill → semi-finish per orientation | `Modules/ThreePlusTwo/` |
| **Interactive dialogs** | Block UI Styler dialogs (Cavity Mill, Z-Level, Raster) with assembly-aware face selection | `Modules/ThreeAxis/Interactive/` |
| **Shop documentation** | Print-ready setup sheet in 8 languages with tool drawings, fixture views and IPW images — saved to SQL Server with automatic revisioning | `Modules/ShopDoc/` |
| **Copilot** | Geometric signature of the current part → finds similar parts in the shop's own history → suggests the operation sequence, tools, RPM/feed actually used → creates the program skeleton on the new part | `Modules/Copilot/` |
| **Quote** | Cost and lead-time estimate from similar parts, calibrated by real machine times registered by the operator | `Modules/Quote/` |
| **AI assistant** | Chat with Claude (Anthropic) that can trigger the shop's automations by text | `Modules/AiAssistant/` |
| **Panel UI** | Code-built Windows Forms panel docked next to NX, single theme class, one primary action per page, status-bar feedback | `UI/` |

## Architecture

```
UI (Form1, forms, Theme)
  └─► Services (QuoteService, StrategyApplier, StrategyHistoryService)
        └─► Data access (ShopDocRepository, ShopDocCollector)
              └─► Siemens NX (NXOpen / UF)  ·  SQL Server (ADO.NET)
```

- **Strategy pattern** for the ~30 machining strategies selectable from the panel (`IMachiningStrategy` + `MachiningContext`).
- **Repository / service** split keeps SQL out of the UI and NXOpen out of the database code.
- **Builder pattern** as NX exposes it: create → configure → commit → destroy, always inside an undo mark.
- Idempotent, numbered SQL migrations in `database/`.

Details: [docs/STRUCTURE.md](docs/STRUCTURE.md) · [docs/CODING_STANDARDS.md](docs/CODING_STANDARDS.md)

## Things I learned the hard way

- A recorded journal is 600 lines of which 30 matter — and the 30 need the user's selection, not the recorder's edge IDs.
- `Placement.SetValue` in Drafting positions the model origin, not the view center. Measure with `AskViewBorders`, then move.
- Faces of assembly components belong to another `Part`; the selection rule must come from *that* part's `ScRuleFactory` or the cut area stays empty.
- A DLL hosted by NX never reads its own `App.config` — the process is `ugraf.exe`.
- ClickOnce installs a fine EXE that cannot run outside NX. Deployment of an NX add-in is a folder plus `UGII_USER_DIR`.

Two write-ups came out of this project (Portuguese):

- [*NXOpen CAM com C# — do journal ao produto*](docs/books/NXOpen_CAM_com_CSharp_PATHNC.pdf)
- [*SQL Server na prática — do NX ao banco de dados*](docs/books/SQL_Server_na_Pratica_PATHNC.pdf)

## Tech stack

C# 7.3 · .NET Framework 4.7.2 · NXOpen / NXOpen.UF (NX 2406) · Windows Forms · ADO.NET / SQL Server Express · Block UI Styler · Anthropic SDK

## Build & run

1. Siemens NX 2406 (x64) with NX CAM; Visual Studio 2022.
2. Open `PATHNC.sln`, fix the `HintPath` of the NXOpen references if NX is not in `C:\Program Files\Siemens\NX2406`, build **Release | x64**.
3. Optional database: run `database/*.sql` in order on SQL Server Express and create `PathNCAutomationDB.connection` next to the executable (see `database/PathNCAutomationDB.connection.example`).
4. In NX: **Tools → Journal → Play** → `bin\x64\Release\PATHNC.exe`.

## Roadmap / ideas

- Parameter-deviation warning on save (RPM/feed vs. shop median per tool)
- Revision diff for a part's programs
- Shop-doc web viewer for the shop floor
- Tool usage dashboard (library vs. programs)
- `ufsta` startup entry point + ribbon button (add-in style deployment)

## License

MIT — see `LICENSE`. Siemens NX and NXOpen are trademarks of Siemens Digital Industries Software and are not part of this repository.
