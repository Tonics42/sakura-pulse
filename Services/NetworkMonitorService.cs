using System.Runtime.InteropServices;

namespace ProcessAnalyzerPro.Services;

/// <summary>
/// Retrieves per-PID TCP connection counts using iphlpapi.dll.
/// Falls back gracefully to an empty dictionary on any failure.
/// </summary>
internal static class NetworkMonitorService
{
    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        nint pTcpTable, ref int pdwSize, bool bOrder,
        int ulAf, TcpTableClass tableClass, uint reserved = 0);

    private enum TcpTableClass
    {
        TcpTableOwnerPidAll = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddr;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] LocalPort;
        public uint RemoteAddr;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] RemotePort;
        public int OwningPid;
    }

    /// <summary>Returns a map of PID -> active TCP connection count.</summary>
    public static Dictionary<int, int> GetConnectionCountsByPid()
    {
        var result = new Dictionary<int, int>();
        try
        {
            int bufferSize = 0;
            GetExtendedTcpTable(nint.Zero, ref bufferSize, true, 2, TcpTableClass.TcpTableOwnerPidAll);
            if (bufferSize <= 0) return result;

            nint buffer = Marshal.AllocHGlobal(bufferSize);
            try
            {
                uint ret = GetExtendedTcpTable(buffer, ref bufferSize, true, 2, TcpTableClass.TcpTableOwnerPidAll);
                if (ret != 0) return result;

                int numEntries = Marshal.ReadInt32(buffer);
                nint rowPtr = buffer + 4;
                int rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPtr);
                    if (row.OwningPid > 0)
                        result[row.OwningPid] = result.GetValueOrDefault(row.OwningPid) + 1;
                    rowPtr += rowSize;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            // Network info is best-effort; never crash the monitor loop
        }
        return result;
    }
}
