# Project Setup Checklist

- [x] Verify `.github/copilot-instructions.md` exists.
- [x] Clarify requirements: C# WPF, .NET 10, SQLite, local chart, self-contained Windows app, offline runtime.
- [x] Scaffold the application source, tests, resources, and Windows scripts.
- [x] Implement the app UI, database, weekly averages, chart, history, settings, and local data workflows.
- [x] Optional extension: C# Dev Kit; no Azure extension or Azure service is needed.
- [x] Build the WPF app with .NET SDK 10.0.403.
- [x] Run core tests: 14 checks pass, including quote resource loading and selection.
- [x] Run static source network audit: no configured network clients found.
- [x] Documentation: README, data-location guide, dependency and removal notes, offline audit guidance.
- [x] Publish self-contained Windows package: `dist/WeightTracker/WeightTracker.exe` (Windows x64, single-file).
- [x] Run core tests: 14 checks pass, including quote resource loading and selection.
- [x] Launch the GUI and confirm startup and clean shutdown.
- [ ] Verify runtime behavior with Windows networking blocked.
