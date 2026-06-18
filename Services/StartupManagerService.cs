using Microsoft.Win32;
using ProcessAnalyzerPro.Models;

namespace ProcessAnalyzerPro.Services;

public static class StartupManagerService
{
    private const string RunPath      = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public static List<StartupEntry> GetAllEntries()
    {
        var entries = new List<StartupEntry>();
        Collect(Registry.CurrentUser,  "HKCU", entries);
        Collect(Registry.LocalMachine, "HKLM", entries);
        return entries;
    }

    private static void Collect(RegistryKey hive, string location, List<StartupEntry> list)
    {
        try
        {
            using var run = hive.OpenSubKey(RunPath);
            if (run == null) return;

            foreach (var name in run.GetValueNames())
            {
                string cmd = run.GetValue(name)?.ToString() ?? string.Empty;
                list.Add(new StartupEntry
                {
                    Name      = name,
                    Command   = cmd,
                    Location  = location,
                    IsEnabled = !IsDisabled(name)
                });
            }
        }
        catch { }
    }

    private static bool IsDisabled(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(ApprovedPath);
            if (key?.GetValue(name) is byte[] val && val.Length > 0)
                return val[0] == 3;
        }
        catch { }
        return false;
    }

    public static void AddEntry(string name, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunPath)
            ?? throw new InvalidOperationException("Cannot open Run registry key.");
        key.SetValue(name, command);
    }

    public static void RemoveEntry(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedPath, writable: true);
        approved?.DeleteValue(name, throwOnMissingValue: false);
    }

    public static void SetEnabled(StartupEntry entry, bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(ApprovedPath)
            ?? throw new InvalidOperationException("Cannot open StartupApproved registry key.");

        var bytes = new byte[12];
        bytes[0] = (byte)(enabled ? 2 : 3);
        key.SetValue(entry.Name, bytes, RegistryValueKind.Binary);
        entry.IsEnabled = enabled;
    }
}
