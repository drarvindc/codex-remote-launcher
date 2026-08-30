using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class LauncherProgram
{
    private static int Main(string[] args)
    {
        string root = "C:\\tools\\codexremote-dotnet"; bool verifyOnly = false;
        for (int i = 0; i < args.Length; i++) { if (args[i] == "--root" && i + 1 < args.Length) root = Path.GetFullPath(args[++i]); else if (args[i] == "--verify-only") verifyOnly = true; }
        Directory.CreateDirectory(root);
        var logger = new SimpleLogger(Path.Combine(root, "logs", "launcher.log"));
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Exception error = e.ExceptionObject as Exception ?? new Exception(Convert.ToString(e.ExceptionObject));
            logger.WriteCrash("UnhandledException", error, e.IsTerminating ? "terminating" : "non-terminating");
        };
        TaskScheduler.UnobservedTaskException += (s, e) => { logger.WriteCrash("UnobservedTaskException", e.Exception, "unobserved-task"); e.SetObserved(); };
        try
        {
            CodexProcessDetector detector = new CodexProcessDetector();
            var roots = detector.Find(null, null);
            if (!verifyOnly && roots.Count != 0) { string message = "Close Codex first, then run Codex Remote."; logger.Write("Guard", message + " roots=" + roots.Count); ShowMessage(message, "Codex Remote"); return 2; }
            string executable = ReadSetting(root, "codexExecutable"); string node = ReadSetting(root, "nodeExecutable");
            if (!File.Exists(executable)) throw new FileNotFoundException("Codex executable not found", executable);
            string failure; if (!new PackageChecker(root, logger).Verify(executable, node, out failure)) throw new InvalidOperationException("package verification failed: " + failure);
            if (verifyOnly) { logger.Write("VerifyOnly", "package compatibility verified"); Console.WriteLine("Compatible"); return 0; }
            var launcher = new CodexLauncher(logger); int renderer = launcher.ReservePort(); int main = launcher.ReservePort(); logger.Write("SpecialLaunchStarted", "renderer=" + renderer + " main=" + main);
            var special = launcher.StartSpecial(executable, renderer, main);
            if (special == null || special.HasExited) throw new InvalidOperationException("special process exited immediately");
            logger.Write("SpecialLaunchConfirmed", "pid=" + special.Id + " renderer=" + renderer + " main=" + main);
            if (!launcher.PortsOpen(renderer, main, 10000)) throw new InvalidOperationException("debug ports were not confirmed");
            string orchestratorFailure; if (!new OrchestratorRunner(root, logger).Run(node, renderer, main, out orchestratorFailure)) throw new InvalidOperationException("orchestrator failed: " + (orchestratorFailure ?? "bridge proof failed"));
            logger.Write("Active", "bridge proof accepted pid=" + special.Id + " renderer=" + renderer + " main=" + main);
            Console.WriteLine("Active"); return 0;
        }
        catch (Exception ex) { logger.WriteCrash("Launcher", ex, "activation-failure"); string message = "Codex Remote could not start.\n\n" + ex.Message + "\n\nLog: " + logger.Path; Console.Error.WriteLine(ex.ToString()); ShowMessage(message, "Codex Remote"); return 1; }
    }

    private static void ShowMessage(string message, string title)
    {
        try { MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { }
    }

    private static string ReadSetting(string root, string name)
    {
        string path = Path.Combine(root, "state", "launcher-settings.json"); if (!File.Exists(path)) throw new FileNotFoundException("Launcher settings missing", path);
        string text = File.ReadAllText(path); string marker = "\"" + name + "\""; int key = text.IndexOf(marker, StringComparison.Ordinal); int colon = text.IndexOf(':', key + marker.Length); int first = text.IndexOf('"', colon + 1); int last = text.IndexOf('"', first + 1);
        if (key < 0 || colon < 0 || first < 0 || last <= first) throw new InvalidOperationException("Launcher setting missing: " + name);
        return text.Substring(first + 1, last - first - 1).Replace("\\\\", "\\");
    }
}
