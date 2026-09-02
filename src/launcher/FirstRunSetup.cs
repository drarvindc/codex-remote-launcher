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
        if (Directory.Exists(srcRuntime) && !Directory.Exists(dstRuntime))
        {
            try
            {
                CopyDirectory(srcRuntime, dstRuntime);
                logger.Write("FirstRunSetup", "copied runtime scaffold from " + srcRuntime + " to " + dstRuntime);
            }
            catch (Exception ex) { logger.Write("FirstRunSetup", "runtime scaffold copy failed: " + ex.Message); }
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
            catch (Exception ex) { logger.Write("FirstRunSetup", "example settings copy failed: " + ex.Message); }
        }
    }

    internal static void TryAutoWriteSettings(string root, SimpleLogger logger)
    {
        string settingsPath = Path.Combine(root, "state", "launcher-settings.json");
        if (File.Exists(settingsPath)) return;

        string codexExe, nodeExe, codexDetail, nodeDetail;
        bool foundCodex = SettingsAutoDetect.TryFindCodexExecutable(out codexExe, out codexDetail);
        bool foundNode = SettingsAutoDetect.TryFindNodeExecutable(out nodeExe, out nodeDetail);

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

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (string file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), false);
        foreach (string dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
    }
}
