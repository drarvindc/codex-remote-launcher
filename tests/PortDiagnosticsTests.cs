using System;

internal static class PortDiagnosticsTests
{
    private static void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private static int Main()
    {
        Expect(CodexLauncher.DescribePortFailure(true, false).IndexOf("main-process inspector did not", StringComparison.Ordinal) >= 0, "renderer-only diagnostic missing");
        Expect(CodexLauncher.DescribePortFailure(false, true).IndexOf("renderer debug endpoint did not", StringComparison.Ordinal) >= 0, "main-only diagnostic missing");
        Expect(CodexLauncher.DescribePortFailure(false, false) == "Neither Codex debug endpoint opened.", "both-missing diagnostic mismatch");
        Console.WriteLine("PortDiagnosticsTests: PASS");
        return 0;
    }
}
