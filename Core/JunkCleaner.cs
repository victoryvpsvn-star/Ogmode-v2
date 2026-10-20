using System.Diagnostics;
using System.ServiceProcess;

namespace ogmm.Core
{
    internal sealed class JunkCategory
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public string Note { get; init; } = "";
        public bool DefaultChecked { get; init; }
        public Func<IEnumerable<string>> Roots { get; init; } = () => Array.Empty<string>();
        /// <summary>Chỉ xoá tệp cũ hơn khoảng này (tránh đụng tệp đang được chương trình khác dùng).</summary>
        public TimeSpan MinAge { get; init; } = TimeSpan.Zero;
        public Func<string, bool>? FileFilter { get; init; }
        /// <summary>Dịch vụ cần dừng tạm thời trong lúc dọn (sẽ bật lại ngay sau đó).</summary>
        public string[] StopServices { get; init; } = Array.Empty<string>();
        /// <summary>Không dọn nếu tiến trình này đang chạy.</summary>
        public string[] BlockedByProcess { get; init; } = Array.Empty<string>();
        public bool IsRecycleBin { get; init; }
        public bool NeedsConfirm { get; init; }
    }

    internal readonly record struct ScanResult(long Bytes, int Files);
    internal readonly record struct CleanResult(long FreedBytes, int Deleted, int Failed, bool Blocked = false, string? BlockedReason = null);

    internal static class JunkCleaner
    {
        private static readonly EnumerationOptions EnumOpts = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            // Bỏ qua junction/symlink để không bao giờ "đi lạc" ra ngoài thư mục cần dọn.
            // (Đặt rõ ràng để vẫn tính cả tệp Hidden/System như thumbcache.)
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        // ------------------------------------------------------------------
        // Danh mục
        // ------------------------------------------------------------------
        public static IReadOnlyList<JunkCategory> BuildCategories()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string userTemp = Path.GetTempPath().TrimEnd('\\', '/');

            return new List<JunkCategory>
            {
                new()
                {
                    Id = "temp", Name = "Tệp tạm (người dùng + Windows)", DefaultChecked = true,
                    Note = "Bỏ qua tệp mới sửa trong 1 giờ qua",
                    Roots = () => new[] { userTemp, Path.Combine(win, "Temp") },
                    MinAge = TimeSpan.FromHours(1),
                },
                new()
                {
                    Id = "wu", Name = "Cache Windows Update + Delivery Optimization", DefaultChecked = true,
                    Note = "Tạm dừng dịch vụ cập nhật khi dọn, bản cập nhật sẽ tải lại nếu cần",
                    Roots = () => new[]
                    {
                        Path.Combine(win, "SoftwareDistribution", "Download"),
                        Path.Combine(win, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache"),
                    },
                    StopServices = new[] { "wuauserv", "bits", "dosvc" },
                    BlockedByProcess = new[] { "TiWorker" },
                },
                new()
                {
                    Id = "browser", Name = "Cache trình duyệt (Chrome, Edge, Brave, Cốc Cốc, Opera, Firefox)", DefaultChecked = true,
                    Note = "Chỉ xoá cache, KHÔNG xoá mật khẩu/cookie/lịch sử. Nên đóng trình duyệt trước",
                    Roots = () => BrowserCacheRoots(local),
                },
                new()
                {
                    Id = "crash", Name = "Báo cáo lỗi & file dump", DefaultChecked = true,
                    Note = "WER, CrashDumps, Minidump, MEMORY.DMP",
                    Roots = () => new[]
                    {
                        Path.Combine(local, "CrashDumps"),
                        Path.Combine(win, "Minidump"),
                        Path.Combine(win, "MEMORY.DMP"),
                        Path.Combine(common, "Microsoft", "Windows", "WER", "ReportArchive"),
                        Path.Combine(common, "Microsoft", "Windows", "WER", "ReportQueue"),
                        Path.Combine(common, "Microsoft", "Windows", "WER", "Temp"),
                        Path.Combine(local, "Microsoft", "Windows", "WER"),
                    },
                },
                new()
                {
                    Id = "thumbs", Name = "Cache ảnh thu nhỏ & icon", DefaultChecked = true,
                    Note = "Windows tự tạo lại; một số tệp đang được Explorer dùng sẽ bị bỏ qua",
                    Roots = () => new[] { Path.Combine(local, "Microsoft", "Windows", "Explorer") },
                    FileFilter = f =>
                    {
                        string n = Path.GetFileName(f);
                        return (n.StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase)
                             || n.StartsWith("iconcache_", StringComparison.OrdinalIgnoreCase))
                             && n.EndsWith(".db", StringComparison.OrdinalIgnoreCase);
                    },
                },
                new()
                {
                    Id = "shader", Name = "Shader cache (DirectX, NVIDIA, AMD, Intel)", DefaultChecked = false,
                    Note = "Game có thể giật nhẹ ở lần chạy đầu để biên dịch lại shader",
                    Roots = () => new[]
                    {
                        Path.Combine(local, "D3DSCache"),
                        Path.Combine(local, "NVIDIA", "DXCache"),
                        Path.Combine(local, "NVIDIA", "GLCache"),
                        Path.Combine(common, "NVIDIA Corporation", "NV_Cache"),
                        Path.Combine(local, "AMD", "DxCache"),
                        Path.Combine(local, "AMD", "DxcCache"),
                        Path.Combine(local, "AMD", "GLCache"),
                        Path.Combine(local, "Intel", "ShaderCache"),
                    },
                },
                new()
                {
                    Id = "recycle", Name = "Thùng rác", DefaultChecked = false, IsRecycleBin = true, NeedsConfirm = true,
                    Note = "Xoá vĩnh viễn mọi thứ trong Recycle Bin",
                },
            };
        }

        private static IEnumerable<string> BrowserCacheRoots(string local)
        {
            string[] chromium =
            {
                Path.Combine(local, "Google", "Chrome", "User Data"),
                Path.Combine(local, "Microsoft", "Edge", "User Data"),
                Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data"),
                Path.Combine(local, "CocCoc", "Browser", "User Data"),
                Path.Combine(local, "Vivaldi", "User Data"),
            };
            string[] cacheDirs = { "Cache", "Code Cache", "GPUCache", "DawnGraphiteCache", "DawnWebGPUCache" };

            foreach (var userData in chromium)
            {
                if (!Directory.Exists(userData)) continue;
                foreach (var profile in SafeDirs(userData))
                {
                    string n = Path.GetFileName(profile);
                    if (n == "Default" || n == "Guest Profile" || n.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                        foreach (var c in cacheDirs) yield return Path.Combine(profile, c);
                }
                yield return Path.Combine(userData, "ShaderCache");
                yield return Path.Combine(userData, "GrShaderCache");
            }

            // Opera / Opera GX không có thư mục profile con
            foreach (var opera in new[] { "Opera Stable", "Opera GX Stable" })
                foreach (var c in new[] { "Cache", "Code Cache", "GPUCache" })
                    yield return Path.Combine(local, "Opera Software", opera, c);

            string ffProfiles = Path.Combine(local, "Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(ffProfiles))
                foreach (var p in SafeDirs(ffProfiles)) yield return Path.Combine(p, "cache2");
        }

        private static IEnumerable<string> SafeDirs(string path)
        {
            try { return Directory.GetDirectories(path); }
            catch { return Array.Empty<string>(); }
        }

        // ------------------------------------------------------------------
        // An toàn đường dẫn
        // ------------------------------------------------------------------
        internal static bool IsSafeRoot(string root)
        {
            try
            {
                string full = Path.GetFullPath(root).TrimEnd('\\', '/');
                string? driveRoot = Path.GetPathRoot(full)?.TrimEnd('\\', '/');
                if (string.IsNullOrEmpty(full) || full.Equals(driveRoot, StringComparison.OrdinalIgnoreCase)) return false;

                // Phải sâu ít nhất 2 cấp dưới ổ đĩa (vd. C:\Windows\Temp)
                string rel = full[(driveRoot?.Length ?? 0)..].Trim('\\', '/');
                if (rel.Split('\\', '/').Length < 2 && !File.Exists(full)) return false;

                var forbidden = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                }
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(f => Path.GetFullPath(f).TrimEnd('\\', '/'));

                foreach (var f in forbidden)
                {
                    if (f.Equals(full, StringComparison.OrdinalIgnoreCase)) return false;                 // trùng thư mục quan trọng
                    if (f.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase)) return false;      // là thư mục cha của thư mục quan trọng
                }
                return true;
            }
            catch { return false; }
        }

        // ------------------------------------------------------------------
        // Duyệt tệp
        // ------------------------------------------------------------------
        private static IEnumerable<string> EnumFiles(string root)
        {
            if (File.Exists(root)) { yield return root; yield break; }
            if (!Directory.Exists(root)) yield break;

            IEnumerator<string> it;
            try { it = Directory.EnumerateFiles(root, "*", EnumOpts).GetEnumerator(); }
            catch { yield break; }

            using (it)
            {
                while (true)
                {
                    string cur;
                    try { if (!it.MoveNext()) break; cur = it.Current; }
                    catch { break; }
                    yield return cur;
                }
            }
        }

        private static bool Eligible(JunkCategory c, FileInfo fi, DateTime cutoffUtc)
        {
            if (c.FileFilter != null && !c.FileFilter(fi.FullName)) return false;
            if (c.MinAge > TimeSpan.Zero && fi.LastWriteTimeUtc > cutoffUtc) return false;
            return true;
        }

        // ------------------------------------------------------------------
        // Quét
        // ------------------------------------------------------------------
        public static ScanResult Scan(JunkCategory c, CancellationToken ct)
        {
            if (c.IsRecycleBin)
            {
                var info = new Native.SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.SHQUERYRBINFO>() };
                try
                {
                    if (Native.SHQueryRecycleBin(null, ref info) == 0)
                        return new ScanResult(info.i64Size, (int)Math.Min(info.i64NumItems, int.MaxValue));
                }
                catch { }
                return new ScanResult(0, 0);
            }

            long bytes = 0; int files = 0;
            DateTime cutoff = DateTime.UtcNow - c.MinAge;
            foreach (var root in c.Roots().Where(IsSafeRoot))
            {
                foreach (var path in EnumFiles(root))
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var fi = new FileInfo(path);
                        if (!Eligible(c, fi, cutoff)) continue;
                        bytes += fi.Length; files++;
                    }
                    catch { }
                }
            }
            return new ScanResult(bytes, files);
        }

        // ------------------------------------------------------------------
        // Dọn
        // ------------------------------------------------------------------
        public static CleanResult Clean(JunkCategory c, Action<string> log, CancellationToken ct)
        {
            if (c.IsRecycleBin)
            {
                long before = Scan(c, ct).Bytes;
                int hr;
                try { hr = Native.SHEmptyRecycleBin(IntPtr.Zero, null, Native.SHERB_NOCONFIRMATION | Native.SHERB_NOPROGRESSUI | Native.SHERB_NOSOUND); }
                catch { hr = -1; }
                // Thùng rác đã rỗng thì Windows có thể trả mã lỗi -> vẫn coi là xong
                bool ok = hr == 0 || before == 0;
                return new CleanResult(hr == 0 ? before : 0, hr == 0 ? 1 : 0, ok ? 0 : 1);
            }

            foreach (var proc in c.BlockedByProcess)
            {
                if (Process.GetProcessesByName(proc).Length > 0)
                    return new CleanResult(0, 0, 0, true, "Windows đang cài bản cập nhật, bỏ qua để tránh lỗi");
            }

            var tempState = new AppState { Transient = true };
            foreach (var svc in c.StopServices)
            {
                try
                {
                    using var sc = new ServiceController(svc);
                    sc.Refresh();
                    if (sc.Status == ServiceControllerStatus.Running && sc.CanStop)
                    {
                        // dùng lại logic dừng + dependents
                        ServiceTweaks.StopAll(new[] { new GameService(svc, svc) }, tempState, _ => { }, ct);
                    }
                }
                catch { }
            }

            long freed = 0; int deleted = 0, failed = 0;
            try
            {
                DateTime cutoff = DateTime.UtcNow - c.MinAge;
                foreach (var root in c.Roots().Where(IsSafeRoot))
                {
                    ct.ThrowIfCancellationRequested();
                    bool isFile = File.Exists(root);

                    foreach (var path in EnumFiles(root))
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            var fi = new FileInfo(path);
                            if (!Eligible(c, fi, cutoff)) continue;
                            long len = fi.Length;
                            if (fi.IsReadOnly) fi.IsReadOnly = false;
                            fi.Delete();
                            freed += len; deleted++;
                        }
                        catch { failed++; }
                    }

                    if (!isFile) RemoveEmptyDirs(root, ct);
                }
            }
            finally
            {
                // Bật lại đúng những dịch vụ đã dừng tạm
                ServiceTweaks.RestoreAll(tempState, _ => { });
            }
            return new CleanResult(freed, deleted, failed);
        }

        private static void RemoveEmptyDirs(string root, CancellationToken ct)
        {
            if (!Directory.Exists(root)) return;
            try
            {
                var dirs = Directory.EnumerateDirectories(root, "*", EnumOpts)
                                    .OrderByDescending(d => d.Length) // thư mục con sâu nhất trước
                                    .ToList();
                foreach (var d in dirs)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d);
                    }
                    catch { }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }
        }

        // ------------------------------------------------------------------
        public static string FormatSize(long bytes)
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return i == 0 ? $"{v:0} {u[i]}" : $"{v:0.##} {u[i]}";
        }
    }
}
