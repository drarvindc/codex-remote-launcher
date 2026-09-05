using System;
using System.Diagnostics;
using System.IO;

internal sealed class PackageChecker
{
    private readonly string root;
    private readonly SimpleLogger logger;
    internal PackageChecker(string root, SimpleLogger logger) { this.root = root; this.logger = logger; }

    internal bool Verify(string executablePath, string nodePath, out string failure)
    {
        string app = Path.GetDirectoryName(executablePath);
        string stage = "ResolvePackagePaths";
        string asar = Path.Combine(app, "resources", "app.asar");
        string native = Path.Combine(app, "resources", "native");
        string check = Path.Combine(root, "runtime", "src", "check-package.mjs");
        if (!File.Exists(asar) || !File.Exists(check) || !File.Exists(nodePath)) { failure = "package-check-input-missing"; return false; }
        string args = Quote(check) + " " + Quote(asar) + " " + Quote(native);
        try
        {
            stage = "StartCompatibilityChecker";
            var start = new ProcessStartInfo { FileName = nodePath, Arguments = args, WorkingDirectory = Path.GetDirectoryName(check), UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using (var process = Process.Start(start))
            {
                if (process == null) { failure = "package-check-process-start-failed"; return false; }
                stage = "ReadCompatibilityCheckerOutput";
                string stdout = process.StandardOutput.ReadToEnd(); string stderr = process.StandardError.ReadToEnd(); process.WaitForExit(15000);
                logger.Write("PackageVerified", "exit=" + process.ExitCode + " outputLength=" + stdout.Length + " errorLength=" + stderr.Length);
                if (process.ExitCode != 0) { failure = "package-check-exit=" + process.ExitCode; return false; }
                if (stdout.IndexOf("\"classification\":\"CandidateCompatible\"", StringComparison.Ordinal) < 0) { failure = "package-incompatible"; return false; }
                if (stdout.IndexOf("\"classification\":\"IncompatibleMainInspectorDisabled\"", StringComparison.Ordinal) >= 0)
                {
                    logger.Write("CompatibilityBlocked", "reason=main-process-inspector-disabled");
                    failure = "main-process-inspector-disabled";
                    return false;
                }
                if (stdout.IndexOf("\"classification\":\"Unknown\"", StringComparison.Ordinal) >= 0)
                    logger.Write("CompatibilityInspectorUnknown", "main-process-inspector=fuse-state-unknown");
                failure = null; return true;
            }
        }
        catch (Exception ex) { logger.Write("PackageVerificationError", "stage=" + stage + " type=" + ex.GetType().FullName + " message=" + ex.Message); failure = stage + ": " + ex.GetType().Name + ": " + ex.Message; return false; }
    }

    private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
}
