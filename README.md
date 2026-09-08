# Codex Remote Launcher

Codex Remote Launcher is an unofficial Windows launcher for Codex Desktop. It
starts Codex in the required debug/Special configuration and applies a small
runtime bridge so the shipped `Settings → Connections → Control other devices`
functionality can appear and work on affected Windows builds.

This project does not patch `ChatGPT.exe`, modify `app.asar`, or change files
under `C:\Program Files\WindowsApps`. The tested configuration does not require
Administrator privileges.

This relies on undocumented and internal Codex behavior. Future Codex updates
may break it. It is not supported, endorsed, or affiliated with OpenAI.

![Codex Remote Launcher hero banner showing Codex Desktop Windows remote control](docs/images/chatgpt_codex_remote_connection_windows_hero.png)

## Working on Windows

Tested successfully with Codex Desktop `26.831.2377.0` on Windows.

### Control other devices enabled

![Codex Desktop Windows Settings showing Control other devices enabled](docs/images/chatgpt_codex_remote_connection_windows_settings.png)

`Control other devices` is visible under `Settings → Connections`, with a
remote Windows device connected.

### Remote project connected and usable

![Remote Codex project connected and usable in Codex Desktop](docs/images/chatgpt_codex_remote_connection_windows.png)

The remote project is available directly inside Codex Desktop and can be used
interactively from the controlling Windows PC.

## Why this exists

Earlier work showed that Codex’s remote-control functionality can work on
Windows, but process takeover and lifecycle recovery add substantial
complexity. This project intentionally follows one small, explicit path:

`Codex closed → launcher → Special Codex → runtime bridge → remote enabled`

If ordinary Codex is already running, the launcher refuses safely. It does not
kill or take over the existing process.

## Requirements

- Windows with Codex Desktop installed from the Microsoft Store/MSIX package.
- A per-user bundled Node runtime supplied by the Codex installation.
- A signed-in Codex account and any account/workspace authorization required by
  Codex remote control.
- The final confirmed validation used Codex Desktop `26.825.6671.0` on
  Windows; compatibility must be checked after every Codex update.
- No Administrator privileges are required in the tested configuration.

Do not infer broad Windows or Codex-version compatibility from these results.

### Compatibility notes

Codex Desktop `26.901.2854.0` has been reported incompatible because its Electron
main-process inspector is disabled, even though the renderer CDP endpoint opens.
The launcher requires both endpoints for the current main-process bootstrap and
will fail closed when that incompatibility is detected. This does not establish
that every later Codex version is incompatible; future builds may change the
behavior.

### Compatibility matrix

| Codex About | Released | MSIX/package | Launcher | Status |
| --- | --- | --- | --- | --- |
| Not recorded | Not recorded | 26.825.6671.0 | v0.1.0 | Supported / tested |
| Not recorded | Not recorded | 26.825.6671.0 | v0.2.0 | Supported / tested |
| 26.831.21537 | 2 Sept 2026 | 26.831.2377.0 | v0.2.0 | Supported / tested |
| 26.901.41123 | 5 Sept 2026 | 26.901.5003.0 | v0.2.0 | Incompatible / tested |
| 26.901.41123 | 5 Sept 2026 | 26.901.5003.0 | v0.3.0 | Supported / tested |
| 26.901.41600 | 5 Sept 2026 | 26.901.5280.0 | v0.3.0 | Supported / tested |
| 26.901.51231 | 6 Sept 2026 | 26.901.6511.0 | v0.3.0 | Supported / tested |

The version shown in Codex About may differ from the Windows Store/MSIX package
version. This project records both when known. Compatibility is based on tested
combinations, not assumed version ranges.

Issue #3 originally reported MSIX `26.901.2854.0` with the same main-process
inspector failure. This does not establish that every 26.901 build is
universally incompatible.

Codex Remote Launcher aims to support the latest stable Windows Codex Desktop
build. New Codex releases may temporarily break compatibility because the
launcher depends on undocumented internal behavior.

## Usage

Normal setup:

1. Download the release ZIP.
2. Extract it to a normal writable folder.
3. Close Codex completely.
4. Run `CodexRemoteLauncher.exe` (or the `Codex Remote` shortcut).
5. On first run, the launcher prepares its runtime/state files, detects the
   installed Codex package and bundled `cua_node` runtime, and creates its
   launcher settings automatically.
6. Codex starts with remote-control support enabled.
7. Open `Settings -> Connections -> Control other devices`.

If Codex is already running, the launcher exits safely and displays:

`Close Codex first, then run Codex Remote.`

The launcher performs no automatic retry.

### After a Codex update

Store updates can change the WindowsApps package path. If a saved Codex or
Node path no longer exists, the launcher automatically searches again. It
refreshes the stale setting when exactly one valid replacement is found. If it
finds zero or multiple candidates, it refuses to guess and explains the issue
in the logs. Manual JSON configuration is only a fallback for unusual or
ambiguous installations.

## Architecture

The current launcher-only path is:

- package and `app.asar` compatibility verification;
- the per-user Codex Node runtime;
- loopback-only temporary debug ports;
- direct Special Codex launch;
- the orchestrator/runtime bridge;
- bridge-proof verification;
- an `Active` result after the proof succeeds.

The current design intentionally has none of the following:

- no watcher;
- no ordinary-process takeover;
- no lifecycle journal or replay;
- no installer or runtime-generation switching;
- no background service;
- no Administrator elevation requirement.

## Security and safety

- Debugging endpoints bind to `127.0.0.1` only.
- The launcher does not modify WindowsApps ACLs or ownership.
- It does not patch executables or `app.asar`.
- An existing Codex process is never killed by the launcher.
- Package compatibility verification is fail-closed.
- There is no automatic retry loop.
- Debug ports can execute code inside the Codex process; use this only on a
  trusted Windows machine and close Codex before returning to normal use.

## Troubleshooting

Having trouble with Remote?

1. Open the `tools` folder.
2. Double-click `run-issue-diagnostics.cmd`.
3. Wait for the diagnostic to finish.
4. Open `tools\codex-remote-diagnostics.txt`.
5. Paste the report into the GitHub issue.

If Windows prevents the diagnostic from running, the diagnostic window will
stay open and show the error; copy that message into the issue.

Advanced users may run `tools\issue-diagnostics.ps1` directly.

### Launcher says Codex is already running

Close Codex fully, then run the launcher again. The launcher intentionally does
not take over an existing process.

### Package verification fails

Inspect `logs\launcher.log` and `logs\launcher-crash.log`. The compatibility
checker validates the installed package before Special launch. A failure is a
stop condition; do not bypass it or modify the WindowsApps installation.

### Access denied when launching Node

The launcher deliberately uses Codex’s per-user bundled Node runtime rather
than trying to execute a protected Node binary inside WindowsApps. Confirm the
configured per-user runtime exists and rerun `--verify-only`.

### Access denied launching Codex (`System.ComponentModel.Win32Exception: Access is denied` at `Process.Start`)

This is a different failure than the one above: it happens on the Special
launch step itself, not the Node step. `logs\launcher-crash.log` will show the
exception originating in `Process.Start`/`StartWithCreateProcess`, and
`logs\launcher.log` will show a `SpecialLaunchStarted` line with no matching
`SpecialLaunchConfirmed` line after it.

Store-signed MSIX packages (Codex Desktop installed from the Microsoft Store
is one) block a plain `CreateProcess` call against the inner `ChatGPT.exe`
from an arbitrary parent process, even when `icacls` shows the launching user
has execute rights on the file. This reproduces independent of any launcher
arguments — a bare `Start-Process` with no debug flags and a correct working
directory fails identically. The launcher must activate the package via
`IApplicationActivationManager::ActivateApplication` (AppX/COM activation)
instead of `Process.Start`; that API also accepts the debug-launch arguments
via its `arguments` parameter. Confirm the fix by launching the same package
through `shell:AppsFolder\<PackageFamilyName>!App` in PowerShell — if that
succeeds while direct `Process.Start` fails, this is the cause.

### Codex launches but Control other devices is missing

Inspect the logs for `SpecialLaunch`, `SpecialLaunchConfirmed`, `Orchestrator`,
and `Active`. Also confirm that the package check succeeded. Do not take
ownership of WindowsApps or change its ACLs.

If the renderer debug endpoint opens but the main-process inspector does not,
the launcher selects its renderer-only bootstrap when the existing renderer
bridge can prove activation. Older builds continue to use the full main-
inspector path.

## Logging

Runtime logs are kept under `logs\`:

- `launcher.log` — normal guard, package, Special-launch, and orchestrator events;
- `launcher-crash.log` — user-visible activation failures and crash details.

Logs are rotated at approximately 1 MiB with one `.1` file retained. Logs and
local state are intentionally excluded from the distributable release bundle.

## Manual configuration fallback

Manual configuration is normally unnecessary. Use it only when automatic
detection is unavailable or ambiguous. To find the Codex path, run
`Get-AppxPackage OpenAI.Codex` in PowerShell and append `\app\ChatGPT.exe` to
the reported `InstallLocation`. Find the bundled Node runtime under
`%LOCALAPPDATA%\OpenAI\Codex\runtimes\cua_node\`; the executable is
`bin\node.exe` inside the selected runtime folder. Preserve any existing
settings keys when updating the JSON.

## Development and build

The release executable is built from the launcher sources in `src\launcher`.
The supplied `BUILD-RELEASE.ps1` uses the installed .NET Framework C# compiler
(`csc.exe`) because the .NET SDK is not required by this project.

From the project directory:

```powershell
.\BUILD-RELEASE.ps1
```

Use `CodexRemoteLauncher.exe --verify-only` to run package compatibility
verification without launching Codex.

The checked-in `state\launcher-settings.example.json` documents the two local
paths used by the launcher. The machine-specific `launcher-settings.json` is
created or refreshed automatically and is not included in the release bundle.

## Credits and acknowledgements

This project is not an official OpenAI product. Its investigation and runtime
techniques were informed by:

- [naipi11/CodexRemote-fix](https://github.com/naipi11/CodexRemote-fix), which is
  MIT-licensed;
- [hunterbeach’s Codex Windows runtime remote-control gist](https://gist.github.com/hunterbeach/dc4b74bda0e045e33f308099182b4f80),
  credited as original research/reference. No explicit license notice was
  identified on that gist, so this project does not treat its text as licensed
  reusable code.
- AppX/MSIX activation support and the initial first-run auto-configuration
  work were contributed by PatrickSys in PR #2.

The standalone implementation uses ideas and techniques derived from that
work, but replaces the lifecycle/takeover architecture with a minimal direct-
launch .NET approach. The runtime bridge in this bundle is maintained as the
standalone project’s own implementation.

## Project status

**Experimental.** Confirmed on Windows with the direct launcher: Codex Desktop
remote-control UI appeared, Control other devices was usable, and a remote
project accepted real-time text updates. Recheck compatibility after each
Codex update.

## License

The original code in this standalone project is released under the MIT License;
see `LICENSE`. Third-party names and references remain the property of their
respective authors. This project does not redistribute OpenAI binaries or
assets.
