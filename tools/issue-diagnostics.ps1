param(
    [switch]$NoPause
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$OutputPath = Join-Path $PSScriptRoot 'codex-remote-diagnostics.txt'

$LauncherRoot = 'C:\tools\codexremote-dotnet'
$LauncherPath = Join-Path $LauncherRoot 'CodexRemoteLauncher.exe'
$SettingsPath = Join-Path $LauncherRoot 'state\launcher-settings.json'
$LauncherLogPath = Join-Path $LauncherRoot 'logs\launcher.log'

function Get-SafeError([object]$ErrorRecord) {
    if ($null -eq $ErrorRecord) { return 'unknown error' }
    return ($ErrorRecord.Exception.GetType().FullName + ': ' + $ErrorRecord.Exception.Message)
}

function Get-RedactedCommandLine([string]$CommandLine) {
    if ([string]::IsNullOrWhiteSpace($CommandLine)) { return 'Not readable' }
    $safe = $CommandLine
    $safe = [regex]::Replace($safe, '(?i)(--?(?:token|password|cookie|secret|private[-_]?key|api[-_]?key))(?:\s+|=)[^\s"]+', '$1=<redacted>')
    $safe = [regex]::Replace($safe, '(?i)(authorization\s*[:=]\s*)[^\s"]+', '$1<redacted>')
    return $safe
}

function Get-ReportSafeText([string]$Text) {
    if ($null -eq $Text) { return '' }
    $safe = $Text
    $profile = [string]$env:USERPROFILE
    if (-not [string]::IsNullOrWhiteSpace($profile)) {
        $safe = $safe -replace [regex]::Escape($profile), '%USERPROFILE%'
    }
    $safe = [regex]::Replace($safe, '(?i)(--?(?:token|password|cookie|secret|private[-_]?key|api[-_]?key))(?:\s+|=)[^\s"]+', '$1=<redacted>')
    $safe = [regex]::Replace($safe, '(?i)(authorization\s*[:=]\s*)[^\s"]+', '$1<redacted>')
    return $safe
}

function Get-OperatingSystemText {
    try {
        $os = Get-CimInstance Win32_OperatingSystem
        return ($os.Caption + ' ' + $os.Version + ' build ' + $os.BuildNumber)
    }
    catch { return 'Unavailable (' + (Get-SafeError $_) + ')' }
}

function Get-CodexPackageInfo {
    try {
        $packages = @(Get-AppxPackage -Name 'OpenAI.Codex' -ErrorAction Stop)
        if ($packages.Count -eq 0) { return [pscustomobject]@{ Found = $false; Error = 'No OpenAI.Codex package was found.' } }
        $package = @($packages | Sort-Object Version -Descending)[0]
        $installLocation = [string]$package.InstallLocation
        $executable = Join-Path $installLocation 'app\ChatGPT.exe'
        if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
            $executable = 'Not found under package install location'
        }
        return [pscustomobject]@{
            Found = $true
            FullName = [string]$package.PackageFullName
            Version = [string]$package.Version
            InstallLocation = $installLocation
            Executable = $executable
            Error = ''
        }
    }
    catch { return [pscustomobject]@{ Found = $false; Error = (Get-SafeError $_) } }
}

function Get-CodexProcesses {
    try {
        $currentSession = (Get-Process -Id $PID).SessionId
        $processes = @(Get-CimInstance Win32_Process -Filter "Name = 'ChatGPT.exe' OR Name = 'Codex.exe'" -ErrorAction Stop | Where-Object { [int]$_.SessionId -eq [int]$currentSession })
        $mainProcesses = @($processes | Where-Object {
            [string]$_.Name -eq 'ChatGPT.exe' -and [string]$_.CommandLine -notmatch '(?i)(^|\s)--type='
        })
        $rows = @($mainProcesses | ForEach-Object {
            $started = 'Not readable'
            try { $started = ([Management.ManagementDateTimeConverter]::ToDateTime([string]$_.CreationDate)).ToString('o') } catch { }
            [pscustomobject]@{
                Pid = [int]$_.ProcessId
                SessionId = [int]$_.SessionId
                Name = [string]$_.Name
                ExecutablePath = if ($_.ExecutablePath) { [string]$_.ExecutablePath } else { 'Not readable' }
                CreationTime = $started
                CommandLine = Get-RedactedCommandLine ([string]$_.CommandLine)
            }
        })
        return [pscustomobject]@{ Found = ($processes.Count -gt 0); Rows = $rows; Error = '' }
    }
    catch { return [pscustomobject]@{ Found = $false; Rows = @(); Error = (Get-SafeError $_) } }
}

function Get-SpecialFlagEvidence([object[]]$Processes) {
    $remote = $false
    $inspect = $false
    foreach ($process in @($Processes)) {
        $line = [string]$process.CommandLine
        if ($line -match '(^|\s)--remote-debugging-address(?:=|\s)') { $remote = $true }
        if ($line -match '(^|\s)--remote-debugging-port(?:=|\s)') { $remote = $true }
        if ($line -match '(^|\s)--inspect(?:[=\s]|$)') { $inspect = $true }
    }
    return [pscustomobject]@{ Remote = $remote; Inspect = $inspect }
}

function Get-NodeCandidates {
    $base = Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\runtimes\cua_node'
    try {
        if (-not (Test-Path -LiteralPath $base -PathType Container)) { return @() }
        return @(Get-ChildItem -LiteralPath $base -Directory -ErrorAction Stop | ForEach-Object {
            $node = Join-Path $_.FullName 'bin\node.exe'
            if (Test-Path -LiteralPath $node -PathType Leaf) { $node }
        })
    }
    catch { return @() }
}

function Get-SafeSettings {
    if (-not (Test-Path -LiteralPath $SettingsPath -PathType Leaf)) {
        return [pscustomobject]@{ Present = $false; Codex = ''; Node = ''; Error = '' }
    }
    try {
        $settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
        return [pscustomobject]@{
            Present = $true
            Codex = [string]$settings.codexExecutable
            Node = [string]$settings.nodeExecutable
            Error = ''
        }
    }
    catch { return [pscustomobject]@{ Present = $true; Codex = ''; Node = ''; Error = (Get-SafeError $_) } }
}

function Get-LatestMarkers {
    $names = @('FirstRunSetup','SettingsRefresh','PackageVerified','SpecialLaunchStarted','SpecialLaunchConfirmed','DebugPortStatus','BootstrapStrategy','Orchestrator','OrchestratorResult','bridgeProof','Active','Crash')
    $result = [ordered]@{}
    foreach ($name in $names) { $result[$name] = 'NOT FOUND' }
    if (-not (Test-Path -LiteralPath $LauncherLogPath -PathType Leaf)) { return $result }
    try {
        $lines = @(Get-Content -LiteralPath $LauncherLogPath -Tail 500 -ErrorAction Stop)
        foreach ($name in $names) {
            $matches = @($lines | Where-Object { $_ -match ('\[' + [regex]::Escape($name) + '\]') -or $_ -match ('\b' + [regex]::Escape($name) + '\b') })
            if ($matches.Count -gt 0) {
                $line = [string]$matches[$matches.Count - 1]
                switch ($name) {
                    'BootstrapStrategy' {
                        if ($line -match '\[BootstrapStrategy\]\s*([^\s]+)') { $result[$name] = $matches[1] } else { $result[$name] = 'FOUND' }
                    }
                    'OrchestratorResult' {
                        if ($line -match '(?i)status=([^\s]+)') { $result[$name] = $matches[1] } else { $result[$name] = 'FOUND' }
                    }
                    'bridgeProof' {
                        if ($line -match '(?i)bridgeProof=(true|false)') { $result[$name] = $matches[1].ToUpperInvariant() } else { $result[$name] = 'FOUND' }
                    }
                    'Crash' { $result[$name] = 'FOUND' }
                    default { $result[$name] = 'YES' }
                }
            }
        }
    }
    catch { $result['Crash'] = 'Unable to read launcher log: ' + (Get-SafeError $_) }
    return $result
}

function Add-Line([System.Collections.Generic.List[string]]$Lines, [string]$Text = '') {
    [void]$Lines.Add((Get-ReportSafeText $Text))
}

$lines = [System.Collections.Generic.List[string]]::new()
$package = $null
$processInfo = $null
$flags = $null
$settings = $null
$markers = $null
$launcherVersion = 'Not found'
$launcherHash = 'Not found'
$launcherWrite = 'Not found'
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$diagnosticSucceeded = $false
$diagnosticError = $null

Add-Line $lines '=================================================='
Add-Line $lines 'CODEX REMOTE LAUNCHER DIAGNOSTIC'
Add-Line $lines '=================================================='
Add-Line $lines ''
Add-Line $lines 'Safe to share publicly: YES'
Add-Line $lines 'No passwords, tokens, cookies, device keys, authentication databases, or project contents are collected.'
Add-Line $lines ''
Add-Line $lines ('Timestamp: ' + (Get-Date).ToString('o'))

try {
    $outputDirectory = Split-Path -Parent $resolvedOutput
    if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) { New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null }

    $package = Get-CodexPackageInfo
    $processInfo = Get-CodexProcesses
    $flags = Get-SpecialFlagEvidence @($processInfo.Rows)
    $settings = Get-SafeSettings
    $markers = Get-LatestMarkers

    if (Test-Path -LiteralPath $LauncherPath -PathType Leaf) {
        try { $launcherVersion = [string](Get-Item -LiteralPath $LauncherPath).VersionInfo.FileVersion } catch { }
        try { $launcherHash = [string](Get-FileHash -LiteralPath $LauncherPath -Algorithm SHA256).Hash } catch { }
        try { $launcherWrite = (Get-Item -LiteralPath $LauncherPath).LastWriteTime.ToString('o') } catch { }
    }

    $successfulActivation = ($markers['SpecialLaunchConfirmed'] -ne 'NOT FOUND' -and $markers['bridgeProof'] -match '(?i)true')
    $active = ($markers['Active'] -ne 'NOT FOUND' -and $markers['Active'] -match '(?i)(yes|active|true)')
    $currentSpecial = $processInfo.Found -and ($flags.Remote -or $flags.Inspect)
    $historicalSpecial = $successfulActivation
    $launcherInstalled = if (Test-Path -LiteralPath $LauncherPath -PathType Leaf) { 'YES' } else { 'NO' }
    $currentSessionLauncher = if (-not $processInfo.Found) { 'NOT RUNNING' } elseif ($currentSpecial) { 'YES' } else { 'NO' }
    $historicalLauncher = if ($historicalSpecial) { 'YES' } else { 'NO' }
    $classification = if ($currentSpecial) { 'SPECIAL LAUNCH' } elseif ($processInfo.Found) { 'NORMAL LAUNCH' } else { 'NOT RUNNING' }
    $bridge = if ($markers['bridgeProof'] -match '(?i)true') { 'TRUE' } elseif ($markers['bridgeProof'] -eq 'NOT FOUND') { 'NOT FOUND' } else { 'FALSE' }

    Add-Line $lines ('Windows: ' + (Get-OperatingSystemText))
    Add-Line $lines ''
    Add-Line $lines ('Codex package: ' + $(if ($package.Found) { $package.FullName + ' (version ' + $package.Version + ')' } else { 'NOT FOUND (' + $package.Error + ')' }))
    Add-Line $lines ('Codex executable: ' + $(if ($package.Found) { $package.Executable } else { 'NOT FOUND' }))
    Add-Line $lines ('Codex running: ' + $(if ($processInfo.Found) { 'YES' } else { 'NO' }))
    if ($processInfo.Error) { Add-Line $lines ('Process discovery note: ' + $processInfo.Error) }
    $mainRows = @($processInfo.Rows)
    $mainProcess = $null
    if ($mainRows.Count -gt 0) { $mainProcess = $mainRows[0] }
    Add-Line $lines ('Main Codex process: ' + $(if ($null -ne $mainProcess) { 'FOUND' } else { 'NOT FOUND' }))
    if ($null -ne $mainProcess) {
        Add-Line $lines ('PID: ' + $mainProcess.Pid)
        Add-Line $lines ('Session: ' + $mainProcess.SessionId)
        Add-Line $lines ('Creation: ' + $mainProcess.CreationTime)
        Add-Line $lines ('Main command line: ' + $mainProcess.CommandLine)
    }
    Add-Line $lines ''
    Add-Line $lines 'Special launch flags:'
    Add-Line $lines ('- remote debugging: ' + $(if ($flags.Remote) { 'YES' } else { 'NO' }))
    Add-Line $lines ('- inspect: ' + $(if ($flags.Inspect) { 'YES' } else { 'NO' }))
    Add-Line $lines ''
    Add-Line $lines ('Launcher executable: ' + $(if (Test-Path -LiteralPath $LauncherPath -PathType Leaf) { $LauncherPath } else { 'NOT FOUND' }))
    Add-Line $lines ('Launcher version: ' + $launcherVersion)
    Add-Line $lines ('Launcher SHA256: ' + $launcherHash)
    Add-Line $lines ('Launcher last write: ' + $launcherWrite)
    Add-Line $lines ''
    Add-Line $lines ('Configured Codex path: ' + $(if ($settings.Present) { $settings.Codex } else { 'NOT FOUND' }))
    Add-Line $lines ('Exists: ' + $(if ($settings.Codex -and (Test-Path -LiteralPath $settings.Codex -PathType Leaf)) { 'YES' } else { 'NO' }))
    Add-Line $lines ('Configured Node path: ' + $(if ($settings.Present) { $settings.Node } else { 'NOT FOUND' }))
    Add-Line $lines ('Exists: ' + $(if ($settings.Node -and (Test-Path -LiteralPath $settings.Node -PathType Leaf)) { 'YES' } else { 'NO' }))
    if ($settings.Error) { Add-Line $lines ('Settings note: ' + $settings.Error) }
    Add-Line $lines ''
    Add-Line $lines 'Latest launcher state:'
    foreach ($name in @('SpecialLaunchConfirmed','BootstrapStrategy','OrchestratorResult','bridgeProof','Active','Crash')) { Add-Line $lines ('- ' + $name + ': ' + $markers[$name]) }
    Add-Line $lines ''
    Add-Line $lines '=================================================='
    Add-Line $lines 'TROUBLESHOOTING SUMMARY'
    Add-Line $lines '=================================================='
    Add-Line $lines ''
    Add-Line $lines ('Launcher installed: ' + $launcherInstalled)
    Add-Line $lines ('Current session launched by remote launcher: ' + $currentSessionLauncher)
    Add-Line $lines ('Historical successful launcher session: ' + $historicalLauncher)
    Add-Line $lines ('Launch classification: ' + $classification)
    Add-Line $lines ('Latest bridge proof: ' + $bridge)
    Add-Line $lines ('Bootstrap strategy: ' + $(if ($markers['BootstrapStrategy'] -ne 'NOT FOUND') { $markers['BootstrapStrategy'] } else { 'NOT FOUND' }))
    Add-Line $lines ''

    $nodeInvalid = $settings.Present -and $settings.Node -and -not (Test-Path -LiteralPath $settings.Node -PathType Leaf)
    if ($classification -eq 'NOT RUNNING') {
        Add-Line $lines 'Codex is not currently running.'
        if ($historicalSpecial) {
            Add-Line $lines 'A previous successful launcher session was found in the log, but there is no current Codex session.'
        }
        Add-Line $lines ''
        Add-Line $lines 'Recommended action:'
        Add-Line $lines '1. Start Codex using Codex Remote Launcher.'
        Add-Line $lines '2. Open Settings -> Connections -> Control other devices.'
        Add-Line $lines '3. Check whether the Remote section appears.'
        Add-Line $lines '4. If the problem remains, run this diagnostic again while Codex is still open.'
        Add-Line $lines '5. Paste the new report into the GitHub issue.'
    }
    elseif ($classification -eq 'NORMAL LAUNCH') {
        Add-Line $lines 'Codex is currently running, but it appears to have been opened normally rather than through Codex Remote Launcher.'
        if ($nodeInvalid) { Add-Line $lines 'The configured Node runtime path is also invalid; the launcher may refresh it automatically after restart.' }
        Add-Line $lines ''
        Add-Line $lines 'Recommended action:'
        Add-Line $lines '1. Fully close Codex.'
        Add-Line $lines '2. Start Codex using Codex Remote Launcher.'
        Add-Line $lines '3. Open Settings -> Connections -> Control other devices.'
        Add-Line $lines '4. Check whether the Remote section appears.'
        Add-Line $lines '5. Run this diagnostic again if the issue remains.'
    }
    elseif ($classification -eq 'SPECIAL LAUNCH' -and $bridge -eq 'TRUE' -and $active) {
        Add-Line $lines 'The launcher side appears to be working correctly and Codex appears to have started in special remote-control mode successfully.'
        Add-Line $lines ''
        Add-Line $lines 'Expected result:'
        Add-Line $lines 'Settings -> Connections -> Control other devices should be available.'
        Add-Line $lines ''
        Add-Line $lines 'If the Remote section is still missing:'
        Add-Line $lines '1. Confirm the target computer is online.'
        Add-Line $lines '2. Confirm the correct target appears under Control other devices.'
        Add-Line $lines '3. Confirm both computers are using the intended accounts.'
        Add-Line $lines '4. Do not relaunch Codex from the normal Start-menu/ChatGPT shortcut.'
        Add-Line $lines '5. Paste this entire diagnostic report into the GitHub issue.'
    }
    elseif ($classification -eq 'SPECIAL LAUNCH') {
        Add-Line $lines 'Codex appears to have been started by Codex Remote Launcher, but remote activation did not complete successfully.'
        Add-Line $lines ''
        Add-Line $lines 'Please paste this entire diagnostic report into the GitHub issue.'
    }
    else {
        Add-Line $lines 'The script could not confidently determine whether Codex is running through Codex Remote Launcher.'
        Add-Line $lines ''
        Add-Line $lines 'Recommended action:'
        Add-Line $lines '1. Fully close Codex.'
        Add-Line $lines '2. Start: C:\tools\codexremote-dotnet\CodexRemoteLauncher.exe --root "C:\tools\codexremote-dotnet"'
        Add-Line $lines '3. Run this diagnostic again.'
        Add-Line $lines '4. Paste the report into the GitHub issue if the problem remains.'
    }

    [System.IO.File]::WriteAllLines($resolvedOutput, $lines)
    $diagnosticSucceeded = $true
}
catch {
    $diagnosticError = Get-ReportSafeText (Get-SafeError $_)
    Add-Line $lines ''
    Add-Line $lines 'Diagnostic status: FAILED'
    Add-Line $lines ('Error: ' + $diagnosticError)
    try {
        [System.IO.File]::WriteAllLines($resolvedOutput, $lines)
    }
    catch { $diagnosticError = Get-ReportSafeText ($diagnosticError + '; failure report could not be written: ' + (Get-SafeError $_)) }
}

if (-not $diagnosticSucceeded) {
    Write-Output 'DIAGNOSTIC FAILED'
    Write-Output ('Error: ' + $diagnosticError)
}
Write-Output 'Diagnostic complete.'
Write-Output ('Report: ' + $resolvedOutput)
if (-not $NoPause) {
    Write-Output 'Press Enter to close this window.'
    try { [void](Read-Host) } catch { }
}
if ($diagnosticSucceeded) { exit 0 }
exit 1
