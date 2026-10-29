# Weight Tracker - Project Plan and Architecture Proposal

## Project status

This plan records the project as executed and distinguishes implemented behavior
from proposed follow-up work. It is based on the application source, the existing
project report, the test harness, and the project setup checklist.

| Item | Status |
|---|---|
| Windows desktop application and core workflows | Implemented |
| Local SQLite storage, settings, and weekly summaries | Implemented |
| CSV/JSON exchange, database backup, and restore | Implemented |
| Self-contained Windows x64 publish process | Implemented |
| Core service checks | 14 checks reported passing |
| Static C# network-client source audit | Passed; not an OS-level isolation test |
| GUI startup and clean shutdown | Smoke-tested |
| Runtime behavior with Windows networking blocked | Not yet verified |
| Automated UI/accessibility coverage | Proposed follow-up |

The local `dist/` folder is publish output and is ignored by Git. It is not part
of the source tree on `origin/main`; source changes and the release build remain
separate.

Repository: [Weight Tracker on GitHub](https://github.com/yuri-dsc-y/FirstCodeAppSoftU).

## Project purpose and scope

Weight Tracker is an offline-first, single-user Windows application for recording
dated body-weight measurements and reviewing change over time. It provides
measurement entry, editable history, weekly averages, a local trend chart,
display settings, and local data import/export and backup workflows.

### In scope

- C# WPF desktop UI targeting .NET 10 on Windows.
- One measurement per date, with an optional note.
- Store values in kilograms and convert for kg/lb display.
- Calculate calendar weeks Monday through Sunday from recorded entries.
- Keep records, settings, and derived weekly averages in a local SQLite database.
- Export CSV/JSON, import validated JSON, and back up or restore the database.
- Publish a self-contained Windows x64 executable.

### Out of scope

- Accounts, cloud sync, telemetry, remote APIs, and server-side storage.
- Medical advice, diagnosis, or treatment functionality.
- A guarantee of network isolation based only on source inspection.
- UI automation, accessibility certification, and broad deployment management
  in the current release.

## Executed project plan

| Work package | Executed outcome | Evidence / acceptance |
|---|---|---|
| Requirements and setup | Selected WPF/.NET 10, SQLite, local charting, and offline-first operation; scaffolded source, tests, resources, and Windows scripts. | Project files, README, and setup checklist |
| Measurement and analytics | Implemented kg/lb conversion, input validation, date ranges, weight change, and Monday-Sunday boundaries. | `Services/WeightService.cs`; core checks |
| Persistence | Implemented SQLite schema version 1, settings, measurement CRUD, and transactional weekly-average recalculation. | `Services/TrackerStore.cs`; core checks |
| User experience | Implemented entry, dashboard, history, weekly view, chart, themes, and data settings. | `MainWindow.xaml` and `MainWindow.xaml.cs`; GUI smoke test |
| Local data workflows | Implemented local quote loading, CSV/JSON export, validated JSON import, database backup, and validated restore. | `Services/QuoteService.cs`, `Services/DataTransferService.cs`, and `Services/TrackerStore.cs` |
| Verification and release | Added core test harness, static network-source audit, and Windows self-contained publish script. | `tests/`, `tools/network_audit.ps1`, and `build_windows.bat` |
| Documentation | Added data-location, dependency, redistribution, offline-audit, and project-report documentation. | Repository documentation |

The project is considered functionally implemented for the stated scope. The
offline runtime test and expanded UI coverage remain explicit follow-up work,
not completed acceptance criteria.

## Architecture proposal

### Proposed logical layers

```text
WPF Presentation
    -> Application workflows
        -> Domain rules and models
        -> Persistence and local-file adapters

Tests exercise domain rules, persistence, and workflows.
Build and audit scripts support verification and release.
```

Dependencies should point inward: the presentation layer may invoke application
workflows, and infrastructure may implement persistence/file operations used by
those workflows. Domain rules should not depend on WPF, SQLite, or the filesystem.
Keep all adapters local; adding a network dependency would require a separately
reviewed scope and privacy decision.

### Separation of concerns

| Concern | Current location | Responsibility | Boundary to preserve |
|---|---|---|---|
| Presentation | `MainWindow.xaml`, `MainWindow.xaml.cs`, `App.xaml` | WPF layout, user input, navigation, display formatting, chart rendering, dialogs, and refresh orchestration | UI code should present state and forward user intent; it should not own SQL or import-format parsing. |
| Domain | `Models/Measurement.cs`, `Services/WeightService.cs` | Measurement/settings value types, unit conversion, week boundaries, ranges, and change calculations | Rules should remain deterministic and testable without WPF or database access. |
| Persistence | `Services/TrackerStore.cs` | SQLite schema/migration, parameterized queries, transactions, settings, records, weekly averages, and database backup/restore | Database-specific details stay behind the store boundary; writes and derived aggregate updates remain atomic. |
| Data exchange | `Services/DataTransferService.cs` | CSV/JSON file formats, serialization, parsing, and import validation | File-format rules stay out of the window and are validated before database mutation. |
| Local content | `Services/QuoteService.cs`, `Resources/` | Loading and selecting bundled quote data | Bundled content remains local; missing/invalid-resource behavior should remain explicit and testable. |
| Quality and release | `tests/`, `tools/`, `build_windows.bat` | Core checks, source network audit, and Windows publish orchestration | Release checks must not be presented as stronger evidence than they provide. |

### Current implementation and recommended evolution

The service and model boundaries above already exist. `MainWindow.xaml.cs` also
coordinates workflows and performs significant formatting and rendering logic;
this is practical for the current application, but it couples UI behavior to
application orchestration. If the UI grows, introduce a small application/workflow
layer and WPF view models incrementally. Keep the existing services as the
behavioral baseline, and make each extraction test-backed. A wholesale rewrite
or new framework is not required for the present scope.

Suggested future source organization, only if justified by growth:

```text
Models/                 Domain values
Services/               Domain rules and application workflows
Persistence/            SQLite implementation
DataExchange/           CSV/JSON adapters
Views/                  WPF windows and controls
ViewModels/             Presentation state and commands
Resources/              Bundled local assets
tests/                  Core and workflow checks
```

## Decision record

| ID | Decision | Rationale and consequence |
|---|---|---|
| D-01 | Build a native C# WPF application for Windows on .NET 10. | Matches the desktop target and allows native controls and local deployment. The UI is Windows-specific. |
| D-02 | Keep application data local; do not design in network services, accounts, or telemetry. | Supports privacy and offline-first use. Source audits and runtime environment tests provide different levels of evidence. |
| D-03 | Use SQLite for measurements and settings. | Provides durable local storage and transactions without a server. Users must protect and back up their database. |
| D-04 | Store weights canonically in kilograms. | Unit preference changes affect presentation rather than rewriting records; exports also use kg. |
| D-05 | Define weeks as Monday through Sunday and derive averages from actual records. | Makes summaries predictable and avoids inventing missing measurements. The UI must label incomplete weeks appropriately. |
| D-06 | Recalculate weekly averages in the same transaction as measurement changes. | Keeps derived summaries consistent with their source records. |
| D-07 | Draw the chart with WPF shapes instead of adding a chart package. | Avoids an extra runtime dependency and keeps rendering local; custom chart behavior carries maintenance and UI-testing costs. |
| D-08 | Publish a self-contained Windows x64 single-file executable through the Windows build script. | Target PCs do not need a separately installed .NET runtime. Build output is generated locally and excluded from Git; source and release artifacts are handled separately. |
| D-09 | Use focused executable checks and a source-pattern audit for current verification. | Keeps checks reproducible without a separate test framework. These checks do not replace UI automation or firewall-blocked runtime verification. |

## Assumptions

- The primary target is a Windows x64 desktop user with access to a persistent
  writable folder.
- The application is single-user and local; concurrent multi-user access and
  synchronization are not requirements.
- A measurement date is unique, and stored weights are finite positive numbers
  represented in kilograms.
- Calendar dates and weekly boundaries use the app's local date context; weeks
  begin on Monday.
- A portable publish stores app data beside the executable; a development install
  uses the user's local application data location.
- The .NET SDK and NuGet access may be required on the build machine. The deployed
  self-contained application is not expected to restore packages at runtime.
- The bundled quote file's wording and attribution are not independently
  verified; its source material did not include source titles or URLs.
- Generated binaries are release output, not required source for building or
  reviewing the application.

## Risk register

| ID | Risk | Impact | Mitigation / follow-up |
|---|---|---|---|
| R-01 | Offline behavior has not been tested with Windows networking blocked. | A runtime dependency could undermine the intended offline behavior. | Run a controlled blocked-network test on the published executable and preserve firewall or packet evidence before making a runtime isolation claim. |
| R-02 | Local database loss, corruption, or accidental deletion. | Measurement history may be lost. | Keep backup/restore workflows, validate restored databases, explain data location, and encourage independent backups. |
| R-03 | User data folder is unavailable or not writable. | Startup or saving could fail. | Prompt for another persistent folder and report errors; do not silently move data to temporary storage. |
| R-04 | Weekly aggregates become inconsistent after future storage changes. | Dashboard, chart, and weekly table may disagree. | Keep mutations and recalculation transactional; expand migration and regression tests when schema or calculation rules change. |
| R-05 | UI logic concentrated in the main window becomes difficult to extend. | Regressions and slower maintenance as workflows grow. | Extract view models/workflow services incrementally only when needed; preserve behavior with focused tests. |
| R-06 | Static network scan provides incomplete assurance. | Binary or OS-level behavior is not covered. | Treat it as a source check only; combine it with dependency review and blocked-network runtime verification. |
| R-07 | Dependency or quote provenance is incomplete. | Redistribution or attribution concerns. | Review resolved package licenses and independently verify quote sources before redistribution. |
| R-08 | UI and accessibility regressions are not covered by the core service checks. | Keyboard, screen-reader, layout, and interaction issues may escape detection. | Add Windows UI/accessibility tests for navigation, validation, dialogs, and chart behavior. |

## AI-generated suggestions and review record

Copilot in VS Code assisted with implementation and refinement across WPF, C#,
tests, and documentation. The existing project report records representative
prompt summaries, not a verbatim transcript. The original conversation does not
provide a complete item-by-item provenance trail for generated lines; this
record therefore describes the review process and evidence rather than claiming
precise authorship for individual code.

| AI-assisted area / representative suggestion | Review and disposition | Verification or limitation |
|---|---|---|
| Keep kilograms as the stored value while supporting kg/lb presentation. | Accepted as the application data rule. | Unit conversion and measurement checks; values are validated before persistence. |
| Use Monday-Sunday calendar weeks and calculate summaries from recorded entries. | Accepted; missing dates are not interpolated. | Boundary and aggregate checks; transactional recalculation on mutation. |
| Validate imported measurements before applying changes. | Accepted to prevent malformed imports from partially changing records. | Import checks and database transaction behavior. |
| Keep charting and data workflows local without adding network clients. | Accepted for the offline-first scope. | Static C# audit found no configured network clients; this is not proof of blocked-network runtime behavior. |
| Use a self-contained Windows x64 release and focused core checks. | Accepted for deployment and repeatability. | Publish and startup smoke test reported; full OS-level offline check remains pending. |

AI output is treated as an unverified proposal, not as a requirement or test
result. Before adoption, the developer checks consistency with project scope,
privacy, existing code patterns, and data-integrity rules; reviews the resulting
diff; and runs relevant deterministic tests/builds. Claims about security,
offline guarantees, package licenses, or external content require evidence
independent of the AI suggestion. No unmeasured comparison between AI tools is
claimed.

## Completion criteria and next steps

The implemented scope is accepted when the app can create, edit, list, and delete
local measurements; derive correct weekly summaries; display history and a local
chart; persist settings; and complete validated data exchange and backup/restore
workflows. The existing project report records 14 passing core checks and a
successful GUI startup/shutdown smoke test.

Before describing the release as runtime-verified offline:

1. Run the published executable with Windows networking blocked and record the
   test setup and observed result.
2. Add targeted Windows UI/accessibility checks for navigation, validation,
   dialogs, and chart rendering.
3. Add database migration and additional empty/duplicate/boundary-date tests as
   the schema or behavior evolves.
4. Review package license metadata and verify quote provenance before
   redistribution.
