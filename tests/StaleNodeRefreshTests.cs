using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

internal static class StaleNodeRefreshTests
{
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "codex-stale-node-" + Guid.NewGuid().ToString("N"));
        string loggerPath = Path.Combine(root, "logs", "tests.log");
        try
        {
            string validCodex = Path.Combine(root, "current-codex.exe");
            string replacementNode = Path.Combine(root, "cua_node", "new", "node.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(validCodex));
            Directory.CreateDirectory(Path.GetDirectoryName(replacementNode));
            File.WriteAllText(validCodex, "codex");
            File.WriteAllText(replacementNode, "node");

            WriteSettings(root, "{\"codexExecutable\":\"" + Escape(validCodex) + "\",\"nodeExecutable\":\"missing-node.exe\",\"futureKey\":\"preserve\"}");
            var logger = new SimpleLogger(loggerPath);
            FirstRunSetup.RefreshSettingsForCandidates(root, logger, new List<string>(), new List<string> { replacementNode });

            var values = ReadSettings(root);
            Assert((string)values["codexExecutable"] == validCodex, "valid Codex path changed");
            Assert((string)values["nodeExecutable"] == replacementNode, "stale Node path was not refreshed");
            Assert((string)values["futureKey"] == "preserve", "unknown settings key was lost");

            string log = File.ReadAllText(loggerPath);
            Assert(log.Contains("Node path stale; replacement detected"), "Node refresh log missing");
            Assert(!log.Contains("Codex path stale; replacement detected"), "Codex-only refresh was falsely logged");
            Assert(log.Contains("settings refreshed automatically"), "completion log missing");
            Console.WriteLine("StaleNodeRefreshTests: PASS");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("StaleNodeRefreshTests: FAIL: " + ex.Message);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static void WriteSettings(string root, string json)
    {
        Directory.CreateDirectory(Path.Combine(root, "state"));
        File.WriteAllText(Path.Combine(root, "state", "launcher-settings.json"), json);
    }

    private static Dictionary<string, object> ReadSettings(string root)
    {
        return new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(root, "state", "launcher-settings.json"))) as Dictionary<string, object>;
    }

    private static string Escape(string value) { return value.Replace("\\", "\\\\"); }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
