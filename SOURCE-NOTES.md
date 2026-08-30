# CodexRemoteLauncher source notes

This local standalone utility is named **CodexRemoteLauncher** and is not an
official OpenAI product.

The launcher uses ideas and techniques derived from:

- [naipi11/CodexRemote-fix](https://github.com/naipi11/CodexRemote-fix) (MIT License)
- [hunterbeach's original gist/research](https://gist.github.com/hunterbeach/dc4b74bda0e045e33f308099182b4f80)

The hunterbeach gist does not display an explicit license notice. It is treated
as research/reference only; no gist source text is shipped as reusable code.

This standalone implementation intentionally uses a minimal direct-launch
.NET approach. It preserves the proven package check, Special/debug launch,
orchestrator, and bridge-proof sequence, while replacing the earlier
lifecycle/takeover architecture with launcher-only behavior. It does not
watch, kill, take over, install a scheduled task, run a tray process, update,
or publish anything.

The launcher refuses to proceed when an ordinary Codex process is already
running. Close Codex first, then run Codex Remote.
