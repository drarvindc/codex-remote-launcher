using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

internal static class Pr2HardeningTests
{
    private static int Main()
    {
        string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        string shipped = Path.Combine(baseDir, "runtime");
        string shippedState = Path.Combine(baseDir, "state");
        string root = Path.Combine(Path.GetTempPath(), "codex-pr2-hardening-" + Guid.NewGuid().ToString("N"));
        string testExe = Escape(Path.Combine(baseDir, "Pr2HardeningTests.exe"));
        try
        {
            Directory.CreateDirectory(shipped);
            File.WriteAllText(Path.Combine(shipped, "required-a.txt"), "a");
            Directory.CreateDirectory(Path.Combine(shipped, "nested"));
            File.WriteAllText(Path.Combine(shipped, "nested", "required-b.txt"), "b");
            Directory.CreateDirectory(shippedState);
            File.WriteAllText(Path.Combine(shippedState, "launcher-settings.example.json"), "example");
            var logger = new SimpleLogger(Path.Combine(root, "logs", "tests.log"));

            FirstRunSetup.EnsureScaffold(root, logger);
            Assert(File.Exists(Path.Combine(root, "runtime", "required-a.txt")), "empty root scaffold");
            Assert(File.Exists(Path.Combine(root, "state", "launcher-settings.example.json")), "empty root state");

            File.WriteAllText(Path.Combine(root, "runtime", "required-a.txt"), "keep");
            FirstRunSetup.EnsureScaffold(root, logger);
            Assert(File.ReadAllText(Path.Combine(root, "runtime", "required-a.txt")) == "keep", "complete runtime preserved");

            string partial = Path.Combine(Path.GetTempPath(), "codex-pr2-partial-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(partial, "runtime"));
            File.WriteAllText(Path.Combine(partial, "runtime", "required-a.txt"), "keep-partial");
            FirstRunSetup.EnsureScaffold(partial, logger);
            Assert(File.ReadAllText(Path.Combine(partial, "runtime", "required-a.txt")) == "keep-partial", "partial existing file preserved");
            Assert(File.Exists(Path.Combine(partial, "runtime", "nested", "required-b.txt")), "partial missing file repaired");
            Directory.Delete(partial, true);

            string settings = Path.Combine(root, "state", "launcher-settings.json");
            File.WriteAllText(settings, "sentinel");
            FirstRunSetup.EnsureScaffold(root, logger);
            Assert(File.ReadAllText(settings) == "sentinel", "existing settings preserved");

            string path, detail;
            Assert(SettingsAutoDetect.SelectSingleCandidate(new List<string> { "codex.exe" }, "Codex", out path, out detail) && path == "codex.exe", "one Codex candidate accepted");
            Assert(!SettingsAutoDetect.SelectSingleCandidate(new List<string> { "a", "b" }, "Codex", out path, out detail) && detail.Contains("2"), "multiple Codex candidates refused");
            Assert(SettingsAutoDetect.SelectSingleCandidate(new List<string> { "node.exe" }, "Node", out path, out detail) && path == "node.exe", "one Node candidate accepted");
            Assert(!SettingsAutoDetect.SelectSingleCandidate(new List<string> { "a", "b" }, "Node", out path, out detail) && detail.Contains("2"), "multiple Node candidates refused");

            string existingRoot = Path.Combine(root, "settings-valid");
            WriteSettings(existingRoot, "{\"codexExecutable\":\"" + testExe + "\",\"nodeExecutable\":\"" + testExe + "\",\"futureKey\":\"keep\"}");
            string existingJson = File.ReadAllText(Path.Combine(existingRoot, "state", "launcher-settings.json"));
            FirstRunSetup.RefreshSettingsForCandidates(existingRoot, logger, new List<string>(), new List<string>());
            Assert(File.ReadAllText(Path.Combine(existingRoot, "state", "launcher-settings.json")) == existingJson, "valid settings unchanged");

            string codexRoot = Path.Combine(root, "stale-codex");
            WriteSettings(codexRoot, "{\"codexExecutable\":\"missing-codex.exe\",\"nodeExecutable\":\"" + testExe + "\",\"futureKey\":\"keep\"}");
            FirstRunSetup.RefreshSettingsForCandidates(codexRoot, logger, new List<string> { "replacement-codex.exe" }, new List<string>());
            Assert(ReadSetting(codexRoot, "codexExecutable") == "replacement-codex.exe" && ReadSetting(codexRoot, "futureKey") == "keep", "stale Codex refreshed and unknown key preserved");

            string nodeRoot = Path.Combine(root, "stale-node");
            WriteSettings(nodeRoot, "{\"codexExecutable\":\"" + testExe + "\",\"nodeExecutable\":\"missing-node.exe\"}");
            FirstRunSetup.RefreshSettingsForCandidates(nodeRoot, logger, new List<string>(), new List<string> { "replacement-node.exe" });
            Assert(ReadSetting(nodeRoot, "nodeExecutable") == "replacement-node.exe", "stale Node refreshed");

            string bothRoot = Path.Combine(root, "stale-both");
            WriteSettings(bothRoot, "{\"codexExecutable\":\"missing-codex.exe\",\"nodeExecutable\":\"missing-node.exe\"}");
            FirstRunSetup.RefreshSettingsForCandidates(bothRoot, logger, new List<string> { "replacement-codex.exe" }, new List<string> { "replacement-node.exe" });
            Assert(ReadSetting(bothRoot, "codexExecutable") == "replacement-codex.exe" && ReadSetting(bothRoot, "nodeExecutable") == "replacement-node.exe", "both stale values refreshed");

            AssertRefreshRefused(root, logger, "ambiguous-codex", "{\"codexExecutable\":\"missing.exe\",\"nodeExecutable\":\"" + testExe + "\"}", new List<string> { "a", "b" }, new List<string>(), "Codex");
            AssertRefreshRefused(root, logger, "ambiguous-node", "{\"codexExecutable\":\"" + testExe + "\",\"nodeExecutable\":\"missing.exe\"}", new List<string>(), new List<string> { "a", "b" }, "Node");
            Console.WriteLine("Pr2HardeningTests: PASS");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("Pr2HardeningTests: FAIL: " + ex.Message); return 1; }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            try { if (Directory.Exists(shipped)) Directory.Delete(shipped, true); } catch { }
            try { if (Directory.Exists(shippedState)) Directory.Delete(shippedState, true); } catch { }
        }
    }

    private static void Assert(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); }

    private static void WriteSettings(string root, string json)
    {
        Directory.CreateDirectory(Path.Combine(root, "state"));
        File.WriteAllText(Path.Combine(root, "state", "launcher-settings.json"), json);
    }

    private static string ReadSetting(string root, string name)
    {
        var value = new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(root, "state", "launcher-settings.json"))) as Dictionary<string, object>;
        return value[name] as string;
    }

    private static string Escape(string value) { return value.Replace("\\", "\\\\"); }

    private static void AssertRefreshRefused(string root, SimpleLogger logger, string name, string json, IList<string> codex, IList<string> node, string label)
    {
        string target = Path.Combine(root, name); WriteSettings(target, json); string before = File.ReadAllText(Path.Combine(target, "state", "launcher-settings.json")); bool refused = false;
        try { FirstRunSetup.RefreshSettingsForCandidates(target, logger, codex, node); } catch (InvalidOperationException ex) { refused = ex.Message.Contains(label); }
        Assert(refused && File.ReadAllText(Path.Combine(target, "state", "launcher-settings.json")) == before, "ambiguous " + label + " refused without rewrite");
    }
}
