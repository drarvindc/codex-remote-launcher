using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Security.Principal;

internal sealed class CodexRoot
{
    internal int Pid;
    internal int SessionId;
    internal string CreationUtc;
    internal string ExecutablePath;
    internal string CommandLine;
    internal string Classification;
}

internal sealed class CodexProcessDetector
{
    private readonly string userSid = WindowsIdentity.GetCurrent().User.Value;
    private readonly int sessionId = Process.GetCurrentProcess().SessionId;

    internal List<CodexRoot> Find(string ownedPid, string ownedCreationUtc)
    {
        var result = new List<CodexRoot>();
        using (var searcher = new ManagementObjectSearcher("SELECT ProcessId,CommandLine,ExecutablePath FROM Win32_Process WHERE Name='ChatGPT.exe'"))
        using (var objects = searcher.Get())
        {
            foreach (ManagementObject item in objects)
            {
                try
                {
                    int pid = Convert.ToInt32(item["ProcessId"]);
                    using (Process p = Process.GetProcessById(pid))
                    {
                        if (p.SessionId != sessionId) continue;
                        string path = item["ExecutablePath"] as string;
                        string command = item["CommandLine"] as string ?? "";
                        if (command.IndexOf("--type=", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        string creation = p.StartTime.ToUniversalTime().ToString("o");
                        if (String.IsNullOrEmpty(path) || path.IndexOf("\\WindowsApps\\OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) < 0 || !path.EndsWith("\\app\\ChatGPT.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(new CodexRoot { Pid = pid, SessionId = p.SessionId, CreationUtc = creation, ExecutablePath = path, CommandLine = command, Classification = "Unproven" });
                            continue;
                        }
                        bool special = command.IndexOf("--remote-debugging-port=", StringComparison.OrdinalIgnoreCase) >= 0 && command.IndexOf("--inspect=", StringComparison.OrdinalIgnoreCase) >= 0;
                        string classification = special ? "Special" : "Ordinary";
                        if (special && ownedPid == pid.ToString() && String.Equals(ownedCreationUtc, creation, StringComparison.Ordinal)) classification = "OwnedSpecial";
                        result.Add(new CodexRoot { Pid = pid, SessionId = p.SessionId, CreationUtc = creation, ExecutablePath = path, CommandLine = command, Classification = classification });
                    }
                }
                catch { }
            }
        }
        return result;
    }
}
