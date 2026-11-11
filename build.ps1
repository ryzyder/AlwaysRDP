[CmdletBinding()]
param([switch]$Test,[switch]$Store=$true)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework x64 compiler not found. Build on Windows with .NET Framework 4.8 installed.' }
$outputFolder = Join-Path $projectRoot 'build\StoreCandidate'
New-Item -ItemType Directory -Force -Path $outputFolder | Out-Null
$sourceNames = @('RdpSupervisor.cs','ConnectionManager.cs','SharedResources.cs','Credentials.cs','Localization.cs','Branding.cs','AppPaths.cs','PackagedStartup.cs','RdpPreferencesConsent.cs','Desktop2Controller.cs','StartupOnboarding.cs')
$sources = @($sourceNames | ForEach-Object { Join-Path $projectRoot ('src\AlwaysRDP\' + $_) })
$references = @('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Core.dll','/reference:System.Xml.Linq.dll','/reference:System.Security.dll')
if($Store) {
    $references+=@('/define:STORE_BUILD',('/reference:'+(Join-Path $env:WINDIR 'System32\WinMetadata\Windows.ApplicationModel.winmd')),('/reference:'+(Join-Path $env:WINDIR 'System32\WinMetadata\Windows.Foundation.winmd')),('/reference:'+(Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\System.Runtime.dll')))
}
$executable = Join-Path $outputFolder 'AlwaysRDP.exe'
$icon = Join-Path $projectRoot 'assets\RDP-Permanente.ico'
& $compiler /nologo /target:winexe /platform:x64 ('/out:' + $executable) ('/win32icon:' + $icon) @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
foreach ($name in @('RDP-Permanente.ico','RDP-Permanente.png')) { Copy-Item -LiteralPath (Join-Path $projectRoot ('assets\'+$name)) -Destination $outputFolder -Force }
foreach ($name in @('LICENSE','AUTHORS.md','THIRD_PARTY.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $outputFolder -Force }
if ($Test) {
    $testFolder = Join-Path $projectRoot 'build\Tests'
    New-Item -ItemType Directory -Force -Path $testFolder | Out-Null
    $testExe = Join-Path $testFolder 'ProfileTests.exe'
    & $compiler /nologo /target:exe /platform:x64 /main:ProfileTests ('/out:'+$testExe) @references @sources (Join-Path $projectRoot 'tests\ProfileTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    & $testExe
    if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
}
Write-Output ('Build complete: '+$executable)
