using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

internal static class SettingsAutoDetect
{
    internal static bool TryFindCodexExecutable(out string path, out string detail)
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
        catch { }

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
        catch { }

        if (candidates.Count == 1) { path = candidates[0]; detail = null; return true; }
        path = null;
        detail = candidates.Count == 0
            ? "no OpenAI.Codex package found (checked the AppX registry and WindowsApps)"
            : "found " + candidates.Count + " candidate Codex installs, refusing to guess: " + string.Join(", ", candidates.ToArray());
        return false;
    }

    internal static bool TryFindNodeExecutable(out string path, out string detail)
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
        catch { }

        if (candidates.Count == 1) { path = candidates[0]; detail = null; return true; }
        path = null;
        detail = candidates.Count == 0
            ? "no cua_node runtime with bin\\node.exe found under %LOCALAPPDATA%\\OpenAI\\Codex\\runtimes\\cua_node"
            : "found " + candidates.Count + " candidate Node runtimes, refusing to guess: " + string.Join(", ", candidates.ToArray());
        return false;
    }
}
