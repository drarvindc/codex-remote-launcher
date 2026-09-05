using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;

internal sealed class OrchestratorParseResult
{
    internal bool Success;
    internal bool BridgeProof;
    internal string Status;
    internal string Failure;
    internal string Diagnostics;
}

internal static class OrchestratorResultParser
{
    internal static OrchestratorParseResult Parse(string stdout)
    {
        try
        {
            var root = new JavaScriptSerializer().DeserializeObject(stdout) as Dictionary<string, object>;
            if (root == null) return Fail("invalid-json-shape", "result is not an object");

            object okValue;
            if (!root.TryGetValue("ok", out okValue) || !(okValue is bool)) return Fail("required-status-missing", "ok");
            if (!(bool)okValue) return Fail("explicit-bridge-failure", OptionalError(root));

            object protocolValue;
            if (!root.TryGetValue("protocolVersion", out protocolValue) || Convert.ToInt32(protocolValue) != 1)
                return Fail("required-status-invalid", "protocolVersion");

            Dictionary<string, object> renderer;
            Dictionary<string, object> main = null;
            if (!TryObject(root, "renderer", out renderer)) return Fail("required-proof-missing", "renderer");
            TryObject(root, "main", out main);

            if (main != null)
            {
                Dictionary<string, object> closure;
                if (!TryObject(main, "inspectorPortClosed", out closure)) return Fail("required-proof-missing", "main.inspectorPortClosed");
                bool confirmed;
                string closureCode;
                if (!TryBool(closure, "confirmed", out confirmed) || !TryString(closure, "code", out closureCode)
                    || !confirmed || closureCode != "ECONNREFUSED")
                    return Fail("bridge-proof-not-proven", "main.inspectorPortClosed");
            }

            Dictionary<string, object> probe;
            if (!TryObject(renderer, "probe", out probe)) return Fail("required-proof-missing", "renderer.probe");
            bool rendererProof;
            string targetGate;
            if (!TryBool(probe, "proof", out rendererProof) || !TryString(probe, "targetGate", out targetGate)
                || !rendererProof || targetGate != "782640499")
                return Fail("bridge-proof-not-proven", "renderer.probe");

            bool newDocumentScriptInstalled;
            if (!TryBool(renderer, "newDocumentScriptInstalled", out newDocumentScriptInstalled) || !newDocumentScriptInstalled)
                return Fail("bridge-proof-not-proven", "renderer.newDocumentScriptInstalled");

            var diagnostics = new StringBuilder();
            AppendOptional(diagnostics, "renderer.targetUrl", renderer, "targetUrl");
            AppendOptionalObject(diagnostics, "renderer.currentDocument", renderer, "currentDocument");
            AppendOptionalObject(diagnostics, "main.payloadReport", main, "payloadReport");
            return new OrchestratorParseResult
            {
                Success = true,
                BridgeProof = true,
                Status = "success",
                Diagnostics = diagnostics.ToString()
            };
        }
        catch (Exception ex)
        {
            return Fail("invalid-orchestrator-json", ex.Message);
        }
    }

    private static OrchestratorParseResult Fail(string status, string detail)
    {
        return new OrchestratorParseResult { Success = false, BridgeProof = false, Status = status, Failure = detail ?? "" };
    }

    private static bool TryObject(Dictionary<string, object> source, string key, out Dictionary<string, object> value)
    {
        object candidate;
        value = null;
        return source != null && source.TryGetValue(key, out candidate)
            && (value = candidate as Dictionary<string, object>) != null;
    }

    private static bool TryBool(Dictionary<string, object> source, string key, out bool value)
    {
        object candidate;
        value = false;
        return source != null && source.TryGetValue(key, out candidate) && candidate is bool && (value = (bool)candidate);
    }

    private static bool TryString(Dictionary<string, object> source, string key, out string value)
    {
        object candidate;
        value = null;
        return source != null && source.TryGetValue(key, out candidate) && (value = candidate as string) != null;
    }

    private static string OptionalError(Dictionary<string, object> root)
    {
        object error;
        return root.TryGetValue("error", out error) ? new JavaScriptSerializer().Serialize(error) : "ok=false";
    }

    private static void AppendOptional(StringBuilder output, string label, Dictionary<string, object> source, string key)
    {
        object value;
        if (source != null && source.TryGetValue(key, out value) && value != null)
            output.Append(label).Append('=').Append(Convert.ToString(value)).Append(' ');
    }

    private static void AppendOptionalObject(StringBuilder output, string label, Dictionary<string, object> source, string key)
    {
        object value;
        if (source != null && source.TryGetValue(key, out value) && value != null)
            output.Append(label).Append('=').Append(new JavaScriptSerializer().Serialize(value)).Append(' ');
    }
}
