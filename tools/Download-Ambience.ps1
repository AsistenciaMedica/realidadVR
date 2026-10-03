# Public CC0 downloads; no account or credentials. Hashes preserve reviewed source bytes.
param()
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$projectRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'AmbienceSources.json') | ConvertFrom-Json
foreach ($asset in $manifest.assets) {
    $destination = Join-Path $projectRoot $asset.path
    if (Test-Path -LiteralPath $destination) {
        $actual = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -eq $asset.sha256) { Write-Output ('VERIFIED ' + $asset.path); continue }
        throw ('Existing audio differs from the reviewed source: ' + $asset.path)
    }
    New-Item -ItemType Directory -Force (Split-Path -Parent $destination) | Out-Null
    $temporary = $destination + '.download'
    Invoke-WebRequest -UseBasicParsing -Uri $asset.download -OutFile $temporary
    $actual = (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $asset.sha256) { throw ('Source hash changed; inspect before importing: ' + $asset.source) }
    Move-Item -LiteralPath $temporary -Destination $destination
    Write-Output ('DOWNLOADED ' + $asset.path)
}
