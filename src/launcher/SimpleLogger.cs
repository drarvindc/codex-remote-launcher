using System;
using System.IO;
using System.Text;

internal sealed class SimpleLogger
{
    private readonly string path;
    private const long MaxLogBytes = 1024 * 1024;
    internal string Path { get { return path; } }
    internal SimpleLogger(string path) { this.path = path; Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)); }
    internal void Write(string eventName, string message)
    {
        RotateIfNeeded(path);
        string line = DateTime.UtcNow.ToString("o") + " [" + eventName + "] " + message;
        Console.WriteLine(line);
        File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
    }

    internal void WriteCrash(string stage, Exception error, string reason)
    {
        string crashPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "launcher-crash.log");
        RotateIfNeeded(crashPath);
        string line = DateTime.UtcNow.ToString("o") + " [Crash] stage=" + (stage ?? "unknown") +
            " reason=" + (reason ?? "unknown") + " type=" + error.GetType().FullName +
            " message=" + error.Message + Environment.NewLine + error.StackTrace;
        Console.Error.WriteLine(line);
        File.AppendAllText(crashPath, line + Environment.NewLine, new UTF8Encoding(false));
    }

    internal void WriteCrash(string stage, string reason, int exitCode)
    {
        string crashPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "launcher-crash.log");
        RotateIfNeeded(crashPath);
        string line = DateTime.UtcNow.ToString("o") + " [Exit] stage=" + (stage ?? "unknown") +
            " reason=" + (reason ?? "unknown") + " exitCode=" + exitCode;
        Console.Error.WriteLine(line);
        File.AppendAllText(crashPath, line + Environment.NewLine, new UTF8Encoding(false));
    }

    private static void RotateIfNeeded(string file)
    {
        try
        {
            if (!File.Exists(file) || new FileInfo(file).Length < MaxLogBytes) return;
            string rotated = file + ".1";
            if (File.Exists(rotated)) File.Delete(rotated);
            File.Move(file, rotated);
        }
        catch { }
    }
}
