param()
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$release=Get-Content -LiteralPath (Join-Path $root 'Builds/Windows/latest-build.json') -Raw | ConvertFrom-Json
$build=(Resolve-Path -LiteralPath (Join-Path $root $release.directory)).Path
$allowed=(Join-Path $root 'Builds\Windows')+[IO.Path]::DirectorySeparatorChar
if(-not $build.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Build path must be inside Builds/Windows.' }
if(-not (Test-Path -LiteralPath (Join-Path $build 'EmergencyVR.exe'))) { throw 'Build executable missing.' }
$demo=Join-Path $root 'demo'
$zip=Join-Path $demo 'releases/EmergencyVR-Windows.zip'
$payload=@(Get-ChildItem -LiteralPath $build | Where-Object { $_.Name -notmatch 'DoNotShip|BurstDebugInformation|BackUpThisFolder|\.log$' })
Compress-Archive -LiteralPath $payload.FullName -DestinationPath $zip -CompressionLevel Optimal -Force
$sha=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
$metadata=[ordered]@{version='0.4.0-visual'; createdUtc=$release.createdUtc; unityVersion=$release.unityVersion; sha256=$sha; bytes=(Get-Item -LiteralPath $zip).Length}
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $demo 'release.json') -Encoding UTF8
$stage=Join-Path $root ('Builds/RailwayDemo/'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path $stage | Out-Null
foreach($name in @('Dockerfile','railway.json','package.json','server.mjs','backend','build.mjs','public','releases','release.json','release-template.json','.dockerignore')) { Copy-Item -LiteralPath (Join-Path $demo $name) -Destination (Join-Path $stage $name) -Recurse -Force }
[pscustomobject]@{WindowsZip=$zip; RailwayDirectory=$stage; SHA256=$sha} | ConvertTo-Json | Tee-Object -FilePath (Join-Path $root 'Builds/client-demo-release.json')
