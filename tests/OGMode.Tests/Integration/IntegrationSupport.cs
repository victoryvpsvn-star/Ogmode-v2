using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Win32;
using ogmm.Core;

namespace OGMode.Tests.Integration
{
    /// <summary>
    /// Test THAY ĐỔI HỆ THỐNG THẬT (registry, power plan, dịch vụ, thư mục hệ thống...).
    /// Chỉ chạy khi OGMODE_INTEGRATION=1 (workflow CI đặt biến này trên máy ảo dùng 1 lần) và có quyền Admin.
    /// Trên máy bạn, `dotnet test` sẽ tự bỏ qua (Skipped) nên không có gì bị đụng tới.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class IntegrationFactAttribute : FactAttribute
    {
        public IntegrationFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("OGMODE_INTEGRATION") != "1")
                Skip = "Chỉ chạy trên máy ảo CI (OGMODE_INTEGRATION=1) vì test này thay đổi hệ thống thật.";
            else if (!SysProbe.IsAdmin())
                Skip = "Cần quyền Administrator.";
        }
    }

    internal static class SysProbe
    {
        public static bool IsAdmin()
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }

        // ---------------- Registry ----------------
        public static int? ReadDword(string path, string name)
        {
            using var k = Registry.CurrentUser.OpenSubKey(path);
            return k?.GetValue(name) is int i ? i : null;
        }

        public static void WriteDword(string path, string name, int? value)
        {
            using var k = Registry.CurrentUser.CreateSubKey(path, writable: true)!;
            if (value.HasValue) k.SetValue(name, value.Value, RegistryValueKind.DWord);
            else k.DeleteValue(name, throwOnMissingValue: false);
        }

        public static Dictionary<string, int?> RegistrySnapshot() =>
            SystemTweaks.GameRegistryTweaks.ToDictionary(t => t.Path + "|" + t.Name, t => ReadDword(t.Path, t.Name));

        // ---------------- Power ----------------
        public static (int Code, string Output) Run(string file, string args)
        {
            using var p = Process.Start(new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            })!;
            string o = p.StandardOutput.ReadToEnd();
            p.WaitForExit(15000);
            return (p.ExitCode, o);
        }

        public static string? ActiveScheme() => SystemTweaks.GetActiveScheme();

        public static bool SchemeExists(string guid) =>
            Run("powercfg.exe", "/list").Output.Contains(guid, StringComparison.OrdinalIgnoreCase);

        // ---------------- Services ----------------
        public static IEnumerable<GameService> AllGameServices() =>
            ServiceTweaks.Default.Concat(ServiceTweaks.Bluetooth).Concat(ServiceTweaks.Xbox);

        public static Dictionary<string, ServiceControllerStatus> ServiceStatuses(IEnumerable<GameService> wanted)
        {
            var result = new Dictionary<string, ServiceControllerStatus>(StringComparer.OrdinalIgnoreCase);
            var all = ServiceController.GetServices();
            try
            {
                foreach (var w in wanted)
                    foreach (var s in all.Where(s => w.IsPrefix
                        ? s.ServiceName.StartsWith(w.Name, StringComparison.OrdinalIgnoreCase)
                        : s.ServiceName.Equals(w.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        s.Refresh();
                        result[s.ServiceName] = s.Status;
                    }
            }
            finally { foreach (var s in all) s.Dispose(); }
            return result;
        }

        public static ServiceControllerStatus? ServiceStatus(string name)
        {
            try { using var sc = new ServiceController(name); return sc.Status; }
            catch { return null; }
        }

        // ---------------- SPI ----------------
        public static Dictionary<uint, int> SpiSnapshot()
        {
            var d = new Dictionary<uint, int>();
            foreach (var (get, _, _) in SystemTweaks.SpiBoolEffects)
            {
                int cur = 0;
                if (Native.SystemParametersInfo(get, 0, ref cur, 0)) d[get] = cur;
            }
            return d;
        }
    }

    /// <summary>Tạo tệp thử trong thư mục thật và dọn lại đúng những gì đã tạo.</summary>
    internal sealed class CleanupScope : IDisposable
    {
        private readonly List<string> _created = new();

        public string Make(string path, DateTime? lastWriteUtc = null, int bytes = 16)
        {
            string dir = Path.GetDirectoryName(path)!;
            string? firstMissing = null;
            for (string? d = dir; d != null && !Directory.Exists(d); d = Path.GetDirectoryName(d)) firstMissing = d;

            Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, new byte[bytes]);
            if (lastWriteUtc.HasValue) File.SetLastWriteTimeUtc(path, lastWriteUtc.Value);
            _created.Add(firstMissing ?? path);
            return path;
        }

        public void Dispose()
        {
            foreach (var p in _created)
            {
                try
                {
                    if (Directory.Exists(p)) Directory.Delete(p, recursive: true);
                    else if (File.Exists(p)) { File.SetAttributes(p, FileAttributes.Normal); File.Delete(p); }
                }
                catch { }
            }
        }
    }
}
