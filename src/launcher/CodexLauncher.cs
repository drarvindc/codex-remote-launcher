using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;

internal sealed class CodexLauncher
{
    private readonly SimpleLogger logger;
    internal CodexLauncher(SimpleLogger logger) { this.logger = logger; }
    internal int ReservePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port;
    }
    internal Process StartSpecial(string executablePath, int rendererPort, int mainPort)
    {
        string arguments = "--remote-debugging-address=127.0.0.1 --remote-debugging-port=" + rendererPort + " --inspect=127.0.0.1:" + mainPort;
        logger.Write("SpecialLaunch", "exe=" + executablePath + " args=" + arguments);
        return Process.Start(new ProcessStartInfo { FileName = executablePath, Arguments = arguments, WorkingDirectory = Path.GetDirectoryName(executablePath), UseShellExecute = false, CreateNoWindow = false });
    }
    internal bool PortsOpen(int rendererPort, int mainPort, int timeoutMs)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (CanConnect(rendererPort) && CanConnect(mainPort)) return true;
            System.Threading.Thread.Sleep(100);
        }
        return false;
    }
    private bool CanConnect(int port)
    {
        using (var client = new TcpClient()) { try { client.Connect("127.0.0.1", port); return true; } catch (SocketException) { return false; } }
    }
}
