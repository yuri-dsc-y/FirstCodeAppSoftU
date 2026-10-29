$ErrorActionPreference = 'Stop'
$sourceFiles = Get-ChildItem -Path (Join-Path $PSScriptRoot '..') -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '[\\/]bin[\\/]|[\\/]obj[\\/]|[\\/]dist[\\/]' }
$patterns = @(
    'using\s+System\.Net(\.|;)',
    '\b(HttpClient|WebClient|TcpClient|UdpClient|Socket|HttpListener)\b',
    'QNetworkAccessManager|WebSocket|Dns\.GetHost'
)
$matches = $sourceFiles | Select-String -Pattern $patterns
if ($matches) {
    $matches | ForEach-Object { Write-Output "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
    exit 1
}
Write-Output 'No configured network clients or socket APIs found in project C# sources.'
Write-Output 'This source audit does not inspect binaries or establish OS-level isolation.'