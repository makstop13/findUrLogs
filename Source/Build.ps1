$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 C# compiler not found.' }
$destination = Split-Path -Parent $PSScriptRoot
$sources = @('Program.cs', 'Scanner.cs', 'Design.cs', 'Ui.cs', 'Theme.cs', 'AppearanceDialog.cs', 'AssemblyInfo.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$iconArgs = @()
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'findUrLogs.ico')) { $iconArgs = @("/win32icon:$(Join-Path $PSScriptRoot 'findUrLogs.ico')") }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/win32manifest:$(Join-Path $PSScriptRoot 'app.manifest')" "/out:$(Join-Path $destination 'findUrLogs.exe')" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $iconArgs $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'findUrLogs.exe.config') -Destination $destination -Force
Write-Output (Join-Path $destination 'findUrLogs.exe')
