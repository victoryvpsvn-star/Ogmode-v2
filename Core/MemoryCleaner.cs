using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ogmm.Core
{
    internal static class MemoryCleaner
    {
        public readonly record struct MemInfo(long TotalMiB, long AvailMiB, int UsedPercent);

        public static MemInfo GetInfo()
        {
            var ms = new Native.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
            if (!Native.GlobalMemoryStatusEx(ref ms) || ms.ullTotalPhys == 0)
                return new MemInfo(0, 0, 0);
            return new MemInfo((long)(ms.ullTotalPhys / 1048576UL), (long)(ms.ullAvailPhys / 1048576UL), (int)ms.dwMemoryLoad);
        }

        /// <summary>Thu nhỏ working set của mọi tiến trình có thể truy cập. Trả về số tiến trình xử lý được.</summary>
        public static int TrimWorkingSets()
        {
            int ok = 0;
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    IntPtr h = IntPtr.Zero;
                    try
                    {
                        if (p.Id <= 4) continue; // Idle / System
                        h = Native.OpenProcess(Native.PROCESS_QUERY_INFORMATION | Native.PROCESS_SET_QUOTA, false, p.Id);
                        if (h != IntPtr.Zero && Native.EmptyWorkingSet(h)) ok++;
                    }
                    catch { }
                    finally
                    {
                        if (h != IntPtr.Zero) Native.CloseHandle(h);
                    }
                }
            }
            return ok;
        }

        /// <summary>Xoá "standby list" (RAM đang giữ cache). Cần quyền Administrator.</summary>
        public static bool PurgeStandbyList()
        {
            try
            {
                if (!EnablePrivilege("SeProfileSingleProcessPrivilege")) return false;
                int command = Native.MemoryPurgeStandbyList;
                return Native.NtSetSystemInformation(Native.SystemMemoryListInformation, ref command, sizeof(int)) == 0;
            }
            catch { return false; }
        }

        private static bool EnablePrivilege(string name)
        {
            IntPtr token = IntPtr.Zero;
            try
            {
                if (!Native.OpenProcessToken(Native.GetCurrentProcess(), Native.TOKEN_ADJUST_PRIVILEGES | Native.TOKEN_QUERY, out token))
                    return false;
                if (!Native.LookupPrivilegeValue(null, name, out var luid)) return false;

                var tp = new Native.TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = Native.SE_PRIVILEGE_ENABLED };
                if (!Native.AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero)) return false;
                // AdjustTokenPrivileges trả true cả khi chưa cấp đủ quyền -> kiểm tra GetLastError
                return Marshal.GetLastWin32Error() == 0;
            }
            finally
            {
                if (token != IntPtr.Zero) Native.CloseHandle(token);
            }
        }

        /// <summary>Dọn RAM đầy đủ: trim working set + xoá standby list. Trả về mô tả ngắn gọn.</summary>
        public static string CleanAll(out int trimmed, out bool standbyPurged)
        {
            trimmed = TrimWorkingSets();
            standbyPurged = PurgeStandbyList();
            return $"Đã thu gọn {trimmed} tiến trình" + (standbyPurged ? ", đã xoá standby list." : " (không xoá được standby list).");
        }
    }
}
