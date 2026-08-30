using System;
using System.IO;
using System.Text;

internal sealed class StatusWriter
{
    private readonly string path;
    internal StatusWriter(string path) { this.path = path; Directory.CreateDirectory(Path.GetDirectoryName(path)); }
    internal void Write(string state, CodexRoot root, string error = null)
    {
        string pid = root == null ? "null" : root.Pid.ToString();
        string escaped = error == null ? "" : error.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
        string json = "{\"state\":\"" + state + "\",\"pid\":" + pid + ",\"error\":\"" + escaped + "\",\"updatedAtUtc\":\"" + DateTime.UtcNow.ToString("o") + "\"}";
        File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
    }
}
