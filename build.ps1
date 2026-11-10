[CmdletBinding()]
param([switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework x64 compiler not found. Build on Windows with .NET Framework 4.8 installed.' }
$vendorFolder = Join-Path $projectRoot 'vendor'
$dependency = Join-Path $vendorFolder 'VirtualDesktopAccessor.dll'
$expectedHash = '8740C572A1C000E3B87FFEB1E4C397EAE9AF3BD4A2ABDC3BCFFACAB4493F8FF5'
New-Item -ItemType Directory -Force -Path $vendorFolder | Out-Null
if (!(Test-Path -LiteralPath $dependency)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/Ciantic/VirtualDesktopAccessor/releases/download/2024-12-16-windows11/VirtualDesktopAccessor.dll' -OutFile $dependency
}
if ((Get-FileHash -LiteralPath $dependency -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Native dependency hash mismatch. Do not run this binary.' }
$outputFolder = Join-Path $projectRoot 'build\Release'
New-Item -ItemType Directory -Force -Path $outputFolder | Out-Null
$sourceNames = @('RdpSupervisor.cs','ConnectionManager.cs','SharedResources.cs','Credentials.cs','Localization.cs','Branding.cs')
$sources = @($sourceNames | ForEach-Object { Join-Path $projectRoot ('src\AlwaysRDP\' + $_) })
$references = @('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Core.dll','/reference:System.Xml.Linq.dll','/reference:System.Security.dll')
$executable = Join-Path $outputFolder 'AlwaysRDP.exe'
$icon = Join-Path $projectRoot 'assets\RDP-Permanente.ico'
& $compiler /nologo /target:winexe /platform:x64 ('/out:' + $executable) ('/win32icon:' + $icon) @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
foreach ($name in @('RDP-Permanente.ico','RDP-Permanente.png')) { Copy-Item -LiteralPath (Join-Path $projectRoot ('assets\'+$name)) -Destination $outputFolder -Force }
Copy-Item -LiteralPath $dependency -Destination $outputFolder -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'third_party\VirtualDesktopAccessor\LICENSE.txt') -Destination (Join-Path $outputFolder 'LICENSE-VirtualDesktopAccessor.txt') -Force
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
