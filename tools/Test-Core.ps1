param()
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    throw 'C# Framework compiler missing. Run the same tests in Unity Test Runner > EditMode.'
}
$resultsPath = Join-Path $projectRoot 'TestResults'
New-Item -ItemType Directory -Force -Path $resultsPath | Out-Null
$executablePath = Join-Path $resultsPath 'CoreTests.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets\_Project\Scripts\Core') -Filter '*.cs' | ForEach-Object { $_.FullName })
$sources += Join-Path $projectRoot 'Assets\_Project\Tests\Shared\DomainTestCases.cs'
$sources += Join-Path $PSScriptRoot 'CoreTestRunner.cs'
& $compilerPath /nologo /target:exe /warnaserror+ "/out:$executablePath" $sources
if ($LASTEXITCODE -ne 0) { throw 'Core compilation failed.' }
& $executablePath | Tee-Object -FilePath (Join-Path $resultsPath 'core-tests.txt')
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed. See TestResults/core-tests.txt.' }
