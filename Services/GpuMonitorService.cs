using System.Diagnostics;

namespace ProcessAnalyzerPro.Services;

/// <summary>
/// Reads per-process GPU % Utilization from the "GPU Engine" performance counter category.
/// Requires Windows 10 1903+ and a discrete/integrated GPU with WDDM 2.x driver.
/// Falls back to an empty dictionary silently if the category is unavailable.
/// </summary>
public static class GpuMonitorService
{
    private const string CategoryName = "GPU Engine";
    private const string CounterName  = "% Utilization";

    private static readonly object _lock = new();
    private static readonly Dictionary<string, PerformanceCounter> _counters = new();
    private static readonly bool _available;

    static GpuMonitorService()
    {
        try { _available = PerformanceCounterCategory.Exists(CategoryName); }
        catch { }
    }

    /// <summary>Returns GPU % per PID (clamped to 100). Empty if GPU counters unavailable.</summary>
    public static Dictionary<int, float> GetGpuUsageByPid()
    {
        var result = new Dictionary<int, float>();
        if (!_available) return result;

        lock (_lock)
        {
            try
            {
                string[] current;
                try
                {
                    var cat = new PerformanceCounterCategory(CategoryName);
                    current = cat.GetInstanceNames()
                               .Where(n => n.StartsWith("pid_", StringComparison.OrdinalIgnoreCase))
                               .ToArray();
                }
                catch { return result; }

                var currentSet = current.ToHashSet();

                // Remove stale counters
                foreach (var stale in _counters.Keys.Where(k => !currentSet.Contains(k)).ToList())
                {
                    try { _counters[stale].Dispose(); } catch { }
                    _counters.Remove(stale);
                }

                // Register new counters (warm-up read returns 0; next call returns real value)
                foreach (var inst in current.Where(i => !_counters.ContainsKey(i)))
                {
                    try
                    {
                        var c = new PerformanceCounter(CategoryName, CounterName, inst, readOnly: true);
                        c.NextValue();          // warm-up — intentionally discarded
                        _counters[inst] = c;
                    }
                    catch { }
                }

                // Read values and group by PID
                foreach (var (instance, counter) in _counters)
                {
                    if (!TryExtractPid(instance, out int pid)) continue;
                    try
                    {
                        float val = counter.NextValue();
                        if (val > 0f)
                            result[pid] = Math.Min(100f, result.GetValueOrDefault(pid) + val);
                    }
                    catch { }
                }
            }
            catch { }
        }

        return result;
    }

    public static void Cleanup()
    {
        lock (_lock)
        {
            foreach (var c in _counters.Values)
                try { c.Dispose(); } catch { }
            _counters.Clear();
        }
    }

    // "pid_1234_luid_..." → pid = 1234
    private static bool TryExtractPid(string instance, out int pid)
    {
        int start = 4;                               // skip "pid_"
        int end   = instance.IndexOf('_', start);
        if (end < 0) end = instance.Length;
        return int.TryParse(instance[start..end], out pid);
    }
}
