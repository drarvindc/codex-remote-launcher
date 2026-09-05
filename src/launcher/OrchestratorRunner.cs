using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;

internal sealed class OrchestratorRunner
{
    private readonly string root;
    private readonly SimpleLogger logger;
    internal OrchestratorRunner(string root, SimpleLogger logger) { this.root = root; this.logger = logger; }
    internal bool Run(string nodePath, int rendererPort, int mainPort, out string failure)
    {
        string runtime = Path.Combine(root, "runtime", "src", "runtime");
        string orchestrator = Path.Combine(runtime, "orchestrator.js");
        string payload = Path.Combine(runtime, "main-payload.js");
        string arguments = "\"" + orchestrator + "\" --mode full --renderer-port " + rendererPort + " --main-port " + mainPort + " --timeout-ms 10000 --main-payload \"" + payload + "\"";
        return RunProcess(nodePath, runtime, arguments, false, out failure);
    }

    internal bool RunRenderer(string nodePath, int rendererPort, out string failure)
    {
        string runtime = Path.Combine(root, "runtime", "src", "runtime");
        string orchestrator = Path.Combine(runtime, "orchestrator.js");
        string arguments = "\"" + orchestrator + "\" --mode renderer --renderer-port " + rendererPort + " --timeout-ms 10000";
        return RunProcess(nodePath, runtime, arguments, true, out failure);
    }

    private bool RunProcess(string nodePath, string runtime, string arguments, bool rendererOnly, out string failure)
    {
        var start = new ProcessStartInfo { FileName = nodePath, Arguments = arguments, WorkingDirectory = runtime, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        using (var process = Process.Start(start))
        {
            if (process == null) { failure = "process-start-failed"; return false; }
            string stdout = process.StandardOutput.ReadToEnd(); string stderr = process.StandardError.ReadToEnd(); process.WaitForExit(12000);
            logger.Write("Orchestrator", "mode=" + (rendererOnly ? "renderer" : "full") + " exit=" + process.ExitCode + " stderr=" + stderr.Trim());
            failure = process.ExitCode == 0 ? null : "exit=" + process.ExitCode;
            if (process.ExitCode != 0) return false;
            OrchestratorParseResult parsed = OrchestratorResultParser.Parse(stdout);
            string resultLog = "status=" + parsed.Status + " bridgeProof=" + parsed.BridgeProof;
            if (!String.IsNullOrEmpty(parsed.Diagnostics)) resultLog += " diagnostics=" + parsed.Diagnostics.Trim();
            logger.Write("OrchestratorResult", resultLog);
            if (!parsed.Success) failure = parsed.Status + (String.IsNullOrEmpty(parsed.Failure) ? "" : ": " + parsed.Failure);
            return parsed.Success;
        }
    }
}
