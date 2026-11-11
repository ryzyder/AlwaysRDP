[CmdletBinding()]
param([switch]$Test)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
& (Join-Path $root 'build.ps1') -Test:$Test -Store:$false
if($LASTEXITCODE -ne 0){throw 'Portable application build failed.'}
$release=Join-Path $root 'build\Portable'
New-Item -ItemType Directory -Force -Path $release | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'build\StoreCandidate\AlwaysRDP.exe') -Destination $release -Force
foreach($name in @('RDP-Permanente.ico','RDP-Permanente.png')){Copy-Item -LiteralPath (Join-Path $root ('assets\'+$name)) -Destination $release -Force}
foreach($name in @('LICENSE','AUTHORS.md','THIRD_PARTY.md')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $release -Force}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /define:PORTABLE_BUILD ('/out:'+(Join-Path $release 'AlwaysRDP.Desktop2.exe')) ('/win32icon:'+(Join-Path $root 'assets\RDP-Permanente.ico')) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll (Join-Path $root 'src\AlwaysRDP\Desktop2Helper.cs')
if($LASTEXITCODE -ne 0){throw 'Portable Desktop 2 helper compilation failed.'}
$native=Join-Path $root 'vendor\VirtualDesktopAccessor.dll'
$expected='8740C572A1C000E3B87FFEB1E4C397EAE9AF3BD4A2ABDC3BCFFACAB4493F8FF5'
if(!(Test-Path -LiteralPath $native)){throw 'VirtualDesktopAccessor.dll is missing. Restore the pinned dependency before creating a portable build.'}
if((Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash -ne $expected){throw 'VirtualDesktopAccessor.dll hash mismatch.'}
Copy-Item -LiteralPath $native -Destination $release -Force
Copy-Item -LiteralPath (Join-Path $root 'third_party\VirtualDesktopAccessor\LICENSE.txt') -Destination (Join-Path $release 'LICENSE-VirtualDesktopAccessor.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $release -Recurse -Force
Write-Output ('Portable build complete: '+$release)
