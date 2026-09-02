using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

internal static class FirstRunSetup
{
    internal static void EnsureScaffold(string root, SimpleLogger logger)
    {
        string payloadDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrEmpty(payloadDir)) return;

        string srcRuntime = Path.Combine(payloadDir, "runtime");
        string dstRuntime = Path.Combine(root, "runtime");
        if (Directory.Exists(srcRuntime))
        {
            try { int repaired = RepairDirectory(srcRuntime, dstRuntime); logger.Write("FirstRunSetup", "runtime scaffold checked; missing files repaired=" + repaired); }
            catch (Exception ex) { logger.Write("FirstRunSetup", "runtime scaffold repair failed: " + ex.Message); throw new InvalidOperationException("runtime scaffold repair failed", ex); }
        }

        string srcExample = Path.Combine(payloadDir, "state", "launcher-settings.example.json");
        string dstStateDir = Path.Combine(root, "state");
        string dstExample = Path.Combine(dstStateDir, "launcher-settings.example.json");
        if (File.Exists(srcExample) && !File.Exists(dstExample))
        {
            try
            {
                Directory.CreateDirectory(dstStateDir);
                File.Copy(srcExample, dstExample);
                logger.Write("FirstRunSetup", "copied example settings to " + dstExample);
            }
            catch (Exception ex) { logger.Write("FirstRunSetup", "example settings copy failed: " + ex.Message); throw new InvalidOperationException("example settings copy failed", ex); }
        }
    }

    internal static void TryAutoWriteSettings(string root, SimpleLogger logger)
    {
        string settingsPath = Path.Combine(root, "state", "launcher-settings.json");
        if (File.Exists(settingsPath)) return;

        string codexExe, nodeExe, codexDetail, nodeDetail;
        bool foundCodex = SettingsAutoDetect.TryFindCodexExecutable(logger, out codexExe, out codexDetail);
        bool foundNode = SettingsAutoDetect.TryFindNodeExecutable(logger, out nodeExe, out nodeDetail);

        if (!foundCodex || !foundNode)
        {
            string reason = (foundCodex ? "" : codexDetail) + (!foundCodex && !foundNode ? "; " : "") + (foundNode ? "" : nodeDetail);
            logger.Write("FirstRunSetup", "auto-detect could not write settings automatically: " + reason);
            return;
        }

        try
        {
            var data = new Dictionary<string, object>
            {
                { "codexExecutable", codexExe },
                { "nodeExecutable", nodeExe }
            };
            Directory.CreateDirectory(Path.Combine(root, "state"));
            File.WriteAllText(settingsPath, new JavaScriptSerializer().Serialize(data));
            logger.Write("FirstRunSetup", "auto-detected and wrote settings: codexExecutable=" + codexExe + " nodeExecutable=" + nodeExe);
        }
        catch (Exception ex) { logger.Write("FirstRunSetup", "auto-write of settings failed: " + ex.Message); }
    }

    internal static void EnsureSettings(string root, SimpleLogger logger)
    {
        string settingsPath = Path.Combine(root, "state", "launcher-settings.json");
        if (!File.Exists(settingsPath)) { TryAutoWriteSettings(root, logger); return; }
        RefreshExistingSettings(root, logger, null, null);
    }

    internal static void RefreshSettingsForCandidates(string root, SimpleLogger logger, IList<string> codexCandidates, IList<string> nodeCandidates)
    {
        RefreshExistingSettings(root, logger, codexCandidates, nodeCandidates);
    }

    private static void RefreshExistingSettings(string root, SimpleLogger logger, IList<string> suppliedCodex, IList<string> suppliedNode)
    {
        string settingsPath = Path.Combine(root, "state", "launcher-settings.json");
        Dictionary<string, object> settings;
        try { settings = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(settingsPath)) as Dictionary<string, object>; }
        catch (Exception ex) { throw new InvalidOperationException("launcher settings could not be read: " + ex.Message, ex); }
        if (settings == null) throw new InvalidOperationException("launcher settings could not be read: invalid JSON");

        string codex = GetSettingString(settings, "codexExecutable");
        string node = GetSettingString(settings, "nodeExecutable");
        bool codexStale = String.IsNullOrEmpty(codex) || !File.Exists(codex);
        bool nodeStale = String.IsNullOrEmpty(node) || !File.Exists(node);
        if (!codexStale && !nodeStale) return;

        string replacement, detail;
        if (codexStale)
        {
            bool found = suppliedCodex != null
                ? SettingsAutoDetect.SelectSingleCandidate(suppliedCodex, "Codex", out replacement, out detail)
                : SettingsAutoDetect.TryFindCodexExecutable(logger, out replacement, out detail);
            if (!found) { logger.Write("SettingsRefresh", "refresh refused: " + detail); throw new InvalidOperationException("Codex installation changed, but the launcher could not identify a single current Codex installation automatically. " + detail); }
            settings["codexExecutable"] = replacement;
            logger.Write("SettingsRefresh", "Codex path stale; replacement detected");
        }
        if (nodeStale)
        {
            bool found = suppliedNode != null
                ? SettingsAutoDetect.SelectSingleCandidate(suppliedNode, "Node", out replacement, out detail)
                : SettingsAutoDetect.TryFindNodeExecutable(logger, out replacement, out detail);
            if (!found) { logger.Write("SettingsRefresh", "refresh refused: " + detail); throw new InvalidOperationException("Node runtime changed, but the launcher could not identify a single current Node runtime automatically. " + detail); }
            settings["nodeExecutable"] = replacement;
            logger.Write("SettingsRefresh", "Node path stale; replacement detected");
        }
        File.WriteAllText(settingsPath, new JavaScriptSerializer().Serialize(settings));
        logger.Write("SettingsRefresh", "settings refreshed automatically");
    }

    private static string GetSettingString(Dictionary<string, object> settings, string name)
    {
        object value;
        return settings.TryGetValue(name, out value) ? value as string : null;
    }

    private static int RepairDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        int repaired = 0;
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destination = Path.Combine(destDir, Path.GetFileName(file));
            if (!File.Exists(destination)) { File.Copy(file, destination, false); repaired++; }
        }
        foreach (string dir in Directory.GetDirectories(sourceDir)) repaired += RepairDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        return repaired;
    }
}
