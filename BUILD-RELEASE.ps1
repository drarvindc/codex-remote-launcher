$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'src\launcher'
$output = Join-Path $root 'CodexRemoteLauncher.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path -LiteralPath $compiler)) { throw "C# compiler not found" }
$files = Get-ChildItem -LiteralPath $source -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName }
& $compiler /nologo /target:winexe /optimize+ /debug- /platform:anycpu /out:$output /reference:System.Management.dll /reference:System.Web.Extensions.dll /reference:System.Windows.Forms.dll $files
if ($LASTEXITCODE -ne 0) { throw "Release build failed: $LASTEXITCODE" }
Write-Output $output
