using System;
using System.Runtime.InteropServices;

internal static class AppxActivation
{
    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    private class ApplicationActivationManagerClass { }

    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        int ActivateApplication([In] string appUserModelId, [In] string arguments, [In] int options, [Out] out uint processId);
    }

    internal static uint Activate(string appUserModelId, string arguments)
    {
        var manager = (IApplicationActivationManager)new ApplicationActivationManagerClass();
        uint processId;
        int hr = manager.ActivateApplication(appUserModelId, arguments, 0, out processId);
        if (hr != 0)
        {
            Exception mapped = Marshal.GetExceptionForHR(hr);
            throw mapped ?? new InvalidOperationException("ActivateApplication failed: 0x" + hr.ToString("X8"));
        }
        return processId;
    }

    internal static string FamilyNameFromPackageFullName(string packageFullName)
    {
        int publisherSep = packageFullName.LastIndexOf("__", StringComparison.Ordinal);
        int nameEnd = packageFullName.IndexOf('_');
        if (publisherSep < 0 || nameEnd < 0 || nameEnd >= publisherSep)
            throw new InvalidOperationException("unrecognized package folder name: " + packageFullName);
        string namePart = packageFullName.Substring(0, nameEnd);
        string publisherId = packageFullName.Substring(publisherSep + 2);
        return namePart + "_" + publisherId;
    }
}
