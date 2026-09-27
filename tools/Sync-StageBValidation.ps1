param()
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$validationRoot = Join-Path $sourceRoot '.utmp\stage-b-validation'
New-Item -ItemType Directory -Path $validationRoot -Force | Out-Null
# A separate Unity project avoids closing or changing the user's open editor session.
# Incremental copies never delete source or validation assets.
foreach ($folder in @('Assets','Packages','ProjectSettings')) {
    $source = Join-Path $sourceRoot $folder
    $target = Join-Path $validationRoot $folder
    & robocopy $source $target /E /NFL /NDL /NJH /NJS /NP /R:1 /W:1
    if ($LASTEXITCODE -ge 8) { throw ('Validation snapshot copy failed: ' + $folder) }
}
$cache = Join-Path $sourceRoot 'Library\PackageCache'
if (Test-Path -LiteralPath $cache) {
    & robocopy $cache (Join-Path $validationRoot 'Library\PackageCache') /E /NFL /NDL /NJH /NJS /NP /R:1 /W:1
    if ($LASTEXITCODE -ge 8) { throw 'Package cache copy failed.' }
}
Write-Output ('Validation project: ' + $validationRoot)
exit 0
