$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$node = (Get-Command node -ErrorAction Stop).Source
& $node (Join-Path $PSScriptRoot 'FuseInspectionTests.mjs')
if ($LASTEXITCODE -ne 0) { throw "FuseInspectionTests failed: $LASTEXITCODE" }
