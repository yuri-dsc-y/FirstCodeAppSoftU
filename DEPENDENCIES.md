# Dependencies and redistribution notes

The app is a native WPF desktop program. Its only third-party application dependency is
`Microsoft.Data.Sqlite` 10.0.14, licensed MIT. It brings SQLitePCLRaw native SQLite
components; inspect the exact resolved package licenses in `obj/project.assets.json`
before redistribution. The .NET runtime and WPF are published self-contained using the
installed .NET SDK. The chart is drawn with native WPF shapes. No Azure service or Azure
extension is required.

Quote entries are copied from the user-provided `Resources/quotes to copy.txt`; source
titles and URLs were not present in that file and have not been researched or verified.

The test harness uses the same application project and .NET libraries; no separate test
framework is required. NuGet restore is a build-time action and can access nuget.org.
The packaged app does not download packages or contact NuGet at runtime.

## Offline audit

Run `powershell -ExecutionPolicy Bypass -File tools/network_audit.ps1` to search C# source
for common HTTP, DNS, socket, and listener APIs. The application source has no such
clients. This static scan cannot prove binary behavior or an OS security boundary. Test
the published executable in a network-blocked Windows environment and inspect firewall
or packet logs before making a runtime isolation claim.

For single-file publish, .NET may extract native libraries to its runtime extraction
directory. The application database is explicitly stored alongside the executable in
portable mode or in `%LOCALAPPDATA%` otherwise, never in that extraction directory.