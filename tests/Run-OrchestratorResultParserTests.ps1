$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$out = Join-Path $root 'tests\OrchestratorResultParserTests.exe'
& $compiler /nologo /target:exe /optimize+ /debug- /out:$out /reference:System.Web.Extensions.dll (Join-Path $root 'src\launcher\OrchestratorResultParser.cs') (Join-Path $root 'tests\OrchestratorResultParserTests.cs')
if ($LASTEXITCODE -ne 0) { throw "Test build failed: $LASTEXITCODE" }
& $out
if ($LASTEXITCODE -ne 0) { throw "Tests failed: $LASTEXITCODE" }
