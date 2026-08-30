using System;

internal static class OrchestratorResultParserTests
{
    private const string Success = "{\"main\":{\"inspectorPortClosed\":{\"attempts\":1,\"code\":\"ECONNREFUSED\",\"confirmed\":true,\"elapsedMs\":2},\"payloadReport\":{\"installed\":true}},\"ok\":true,\"protocolVersion\":1,\"renderer\":{\"currentDocument\":{\"installed\":true},\"newDocumentScriptInstalled\":true,\"probe\":{\"proof\":true,\"targetGate\":\"782640499\"},\"targetUrl\":\"app://-/index.html\"}}";
    private const string WithoutOptional = "{\"main\":{\"inspectorPortClosed\":{\"code\":\"ECONNREFUSED\",\"confirmed\":true}},\"ok\":true,\"protocolVersion\":1,\"renderer\":{\"newDocumentScriptInstalled\":true,\"probe\":{\"proof\":true,\"targetGate\":\"782640499\"}}}";
    private const string ExplicitFailure = "{\"error\":{\"code\":\"BRIDGE_FAILED\",\"message\":\"renderer proof failed\"},\"ok\":false,\"stage\":\"renderer-install\"}";

    private static void Expect(string name, string json, bool expected)
    {
        OrchestratorParseResult result = OrchestratorResultParser.Parse(json);
        if (result.Success != expected) throw new Exception(name + " expected success=" + expected + " but got " + result.Status);
    }

    internal static int Main()
    {
        Expect("actual-success-shape", Success, true);
        Expect("optional-fields-omitted", WithoutOptional, true);
        Expect("malformed-json", "{not-json", false);
        Expect("explicit-bridge-failure", ExplicitFailure, false);
        Expect("nonzero-exit-represented-as-failure", "{\"ok\":false,\"error\":{\"code\":\"EXIT_1\"}}", false);
        Console.WriteLine("OrchestratorResultParserTests: PASS");
        return 0;
    }
}
