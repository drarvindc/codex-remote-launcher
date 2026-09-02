using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

internal static class SettingsAutoDetect
{
    internal static bool TryFindCodexExecutable(SimpleLogger logger, out string path, out string detail)
    {
        var candidates = new List<string>();

        try
        {
            using (RegistryKey packages = Registry.ClassesRoot.OpenSubKey(
                @"Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"))
            {
                if (packages != null)
                {
                    foreach (string name in packages.GetSubKeyNames())
                    {
                        if (name.IndexOf("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) != 0) continue;
                        using (RegistryKey pkg = packages.OpenSubKey(name))
                        {
                            object rootFolder = pkg != null ? pkg.GetValue("PackageRootFolder") : null;
                            if (rootFolder == null) continue;
                            string exe = Path.Combine(rootFolder.ToString(), "app", "ChatGPT.exe");
                            if (File.Exists(exe) && !candidates.Contains(exe)) candidates.Add(exe);
                        }
                    }
                }
            }
        }
        catch (Exception ex) { logger.Write("AutoDetect", "registry discovery failed: " + ex.GetType().Name + ": " + ex.Message); }

        try
        {
            string windowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
            if (Directory.Exists(windowsApps))
            {
                foreach (string dir in Directory.GetDirectories(windowsApps, "OpenAI.Codex_*"))
                {
                    string exe = Path.Combine(dir, "app", "ChatGPT.exe");
                    if (File.Exists(exe) && !candidates.Contains(exe)) candidates.Add(exe);
                }
            }
        }
        catch (Exception ex) { logger.Write("AutoDetect", "WindowsApps fallback failed: " + ex.GetType().Name + ": " + ex.Message); }

        return SelectSingleCandidate(candidates, "Codex", out path, out detail);
    }

    internal static bool TryFindNodeExecutable(SimpleLogger logger, out string path, out string detail)
    {
        var candidates = new List<string>();

        try
        {
            string runtimes = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenAI", "Codex", "runtimes", "cua_node");
            if (Directory.Exists(runtimes))
            {
                foreach (string dir in Directory.GetDirectories(runtimes))
                {
                    string exe = Path.Combine(dir, "bin", "node.exe");
                    if (File.Exists(exe)) candidates.Add(exe);
                }
            }
        }
        catch (Exception ex) { logger.Write("AutoDetect", "Node runtime discovery failed: " + ex.GetType().Name + ": " + ex.Message); }

        return SelectSingleCandidate(candidates, "Node", out path, out detail);
    }

    internal static bool SelectSingleCandidate(IList<string> candidates, string label, out string path, out string detail)
    {
        var unique = new List<string>();
        foreach (string candidate in candidates) if (!unique.Contains(candidate)) unique.Add(candidate);
        if (unique.Count == 1) { path = unique[0]; detail = null; return true; }
        path = null;
        detail = unique.Count == 0 ? "no " + label + " candidate found" : "found " + unique.Count + " candidate " + label + " installs, refusing to guess: " + string.Join(", ", unique.ToArray());
        return false;
    }
}
