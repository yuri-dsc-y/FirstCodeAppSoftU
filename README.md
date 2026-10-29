# Weight Tracker

A native, offline-first Windows weight log built with C# WPF, .NET 10, and SQLite. It creates a database on first launch, keeps measurements in kilograms internally, and recalculates Monday-through-Sunday averages whenever entries change.

The trend chart plots weekly averages within the measurement date range, scales its vertical axis to the recorded weights, and labels representative average values directly on the graph. Hover over any weekly marker for its exact average and measurement count.

## Run on Windows

Install the .NET 10 SDK on a development machine and run `dotnet run --project WeightTracker.csproj`. Restore requires NuGet access to the declared SQLite package. The built app contains no update check, telemetry, browser view, web server, or remote content loading.

For a portable, self-contained single-file distribution, run `build_windows.bat` on Windows. The script runs the core tests and publishes `dist/WeightTracker/WeightTracker.exe` with the .NET runtime bundled. The local quote file and data/removal guides accompany it. No .NET installation is needed on the target PC.

A package has been built in `dist/WeightTracker/`. The executable is a Windows x64 single-file publish. Core service tests passed, and a startup smoke test confirmed that the GUI opens and closes cleanly. Runtime behavior with Windows networking blocked has not yet been verified.

## Data and backups

The portable build includes `portable.flag` and stores its database at `data/weight_tracker.db` beside the executable. In development/non-portable mode, data is stored at `%LOCALAPPDATA%/WeightTracker/weight_tracker.db`. If the selected location cannot be written, the app asks for another persistent folder; it does not fall back to a temporary directory. SQLite's bundled native library may be extracted by .NET single-file deployment, but personal data is never stored in that temporary extraction location.

Use Settings & data to create SQLite backups, restore a validated backup, export CSV/JSON, import validated JSON, open the data folder, or explicitly delete records. Deleting the application folder removes portable data in that folder, but backups and exports saved elsewhere must be deleted separately.

## Development checks

```powershell
dotnet run --project tests/WeightTracker.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File tools/network_audit.ps1
```

## Project report

The five-page project report, including two screenshots of the running application, is in [ProjectReport/ProjectReport.pdf](ProjectReport/ProjectReport.pdf). Its editable HTML source and screenshot files are alongside it.
The [executed project plan and architecture proposal](ProjectReport/ProjectPlan.md) records the separation of concerns, decisions, assumptions, risks, AI-assisted work, and outstanding verification.

The audit checks source imports and patterns; it does not prove OS-level network isolation or inspect every binary dependency. Before redistribution, test the packaged app in a network-blocked Windows environment and review the exact dependency versions and licenses listed in [DEPENDENCIES.md](DEPENDENCIES.md).

The bundled quote collection contains the 100 quote-and-attribution lines copied from the supplied `Resources/quotes to copy.txt`. The supplied file did not include source titles or URLs; wording and attribution have not been independently verified.
