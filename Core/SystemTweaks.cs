using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ogmm.Core
{
    /// <summary>Các tinh chỉnh có sao lưu giá trị gốc để khôi phục chính xác.</summary>
    internal static class SystemTweaks
    {
        // ====================== Registry (HKCU, DWORD) ======================
        internal readonly record struct RegTweak(string Path, string Name, int Value, string Label);

        internal static readonly RegTweak[] GameRegistryTweaks =
        {
            // Hiệu ứng Explorer
            new(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0, "Hiệu ứng thanh taskbar"),
            new(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect", 0, "Vùng chọn trong suốt"),
            new(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow", 0, "Bóng chữ icon desktop"),
            new(@"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0, "Aero Peek"),
            new(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, "Hiệu ứng trong suốt"),
            // Game Mode của Windows + tắt quay/ghi nền (Game DVR)
            new(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, "Windows Game Mode"),
            new(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1, "Windows Game Mode (tự động)"),
            new(@"System\GameConfigStore", "GameDVR_Enabled", 0, "Xbox Game DVR"),
            new(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, "Ghi hình nền (App Capture)"),
            new(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled", 0, "Ghi lại khoảnh khắc game"),
        };

        public static void ApplyRegistry(AppState state, Action<string> log)
        {
            foreach (var t in GameRegistryTweaks)
            {
                string key = t.Path + "|" + t.Name;
                try
                {
                    using var k = Registry.CurrentUser.CreateSubKey(t.Path, writable: true);
                    if (k == null) { log($"  ✗ {t.Label}: không mở được registry"); continue; }

                    if (!state.RegistryBackup.ContainsKey(key))
                    {
                        object? old = k.GetValue(t.Name);
                        state.RegistryBackup[key] = old is int i ? i : null;
                    }
                    k.SetValue(t.Name, t.Value, RegistryValueKind.DWord);
                    log($"  ✓ {t.Label}");
                }
                catch (Exception ex) { log($"  ✗ {t.Label}: {ex.Message}"); }
            }
            state.Save();
        }

        public static void RestoreRegistry(AppState state, Action<string> log)
        {
            foreach (var kv in state.RegistryBackup.ToList())
            {
                int sep = kv.Key.LastIndexOf('|');
                if (sep < 0) continue;
                string path = kv.Key[..sep], name = kv.Key[(sep + 1)..];
                try
                {
                    using var k = Registry.CurrentUser.CreateSubKey(path, writable: true);
                    if (k == null) continue;
                    if (kv.Value.HasValue) k.SetValue(name, kv.Value.Value, RegistryValueKind.DWord);
                    else k.DeleteValue(name, throwOnMissingValue: false);
                    state.RegistryBackup.Remove(kv.Key);
                }
                catch (Exception ex) { log($"  ✗ Khôi phục {name}: {ex.Message}"); }
            }
            log("  ✓ Đã khôi phục registry về giá trị ban đầu");
            state.Save();
        }

        // ====================== Hiệu ứng cửa sổ (SystemParametersInfo) ======================
        // (GET, SET) cho các hiệu ứng dạng BOOL
        internal static readonly (uint Get, uint Set, string Label)[] SpiBoolEffects =
        {
            (0x1042, 0x1043, "Hiệu ứng vùng client"),
            (0x1002, 0x1003, "Hiệu ứng menu"),
            (0x1004, 0x1005, "Hiệu ứng combobox"),
            (0x1006, 0x1007, "Cuộn mượt listbox"),
            (0x1012, 0x1013, "Menu mờ dần"),
            (0x1014, 0x1015, "Vùng chọn mờ dần"),
            (0x1016, 0x1017, "Tooltip hoạt hình"),
            (0x1018, 0x1019, "Tooltip mờ dần"),
            (0x101A, 0x101B, "Bóng con trỏ"),
        };
        internal const uint SPI_GETANIMATION = 0x0048;
        private const uint SPI_SETANIMATION = 0x0049;
        private const uint SpiFlags = Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE;

        public static void ApplyWindowEffects(AppState state, Action<string> log)
        {
            int ok = 0;
            foreach (var (get, set, label) in SpiBoolEffects)
            {
                try
                {
                    int cur = 0;
                    if (!Native.SystemParametersInfo(get, 0, ref cur, 0)) continue;
                    if (!state.SpiBackup.ContainsKey(get)) state.SpiBackup[get] = cur;
                    if (Native.SystemParametersInfo(set, 0, IntPtr.Zero, SpiFlags)) ok++;
                }
                catch { }
            }
            // Hiệu ứng thu/phóng cửa sổ (min/max animation) dùng struct ANIMATIONINFO {cbSize, iMinAnimate}
            try
            {
                int[] info = { 8, 0 };
                var h = System.Runtime.InteropServices.GCHandle.Alloc(info, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    var p = h.AddrOfPinnedObject();
                    if (Native.SystemParametersInfo(SPI_GETANIMATION, 8, p, 0))
                    {
                        if (!state.SpiBackup.ContainsKey(SPI_GETANIMATION)) state.SpiBackup[SPI_GETANIMATION] = info[1];
                        info[1] = 0;
                        if (Native.SystemParametersInfo(SPI_SETANIMATION, 8, p, SpiFlags)) ok++;
                    }
                }
                finally { h.Free(); }
            }
            catch { }
            state.Save();
            log(ok > 0 ? $"  ✓ Đã tắt {ok} hiệu ứng cửa sổ" : "  ✗ Không tắt được hiệu ứng cửa sổ");
        }

        public static void RestoreWindowEffects(AppState state, Action<string> log)
        {
            foreach (var (get, set, _) in SpiBoolEffects)
            {
                if (!state.SpiBackup.TryGetValue(get, out int old)) continue;
                try { Native.SystemParametersInfo(set, 0, (IntPtr)old, SpiFlags); } catch { }
                state.SpiBackup.Remove(get);
            }
            if (state.SpiBackup.TryGetValue(SPI_GETANIMATION, out int minAnim))
            {
                try
                {
                    int[] info = { 8, minAnim };
                    var h = System.Runtime.InteropServices.GCHandle.Alloc(info, System.Runtime.InteropServices.GCHandleType.Pinned);
                    try { Native.SystemParametersInfo(SPI_SETANIMATION, 8, h.AddrOfPinnedObject(), SpiFlags); }
                    finally { h.Free(); }
                }
                catch { }
                state.SpiBackup.Remove(SPI_GETANIMATION);
            }
            state.Save();
            log("  ✓ Đã khôi phục hiệu ứng cửa sổ");
        }

        // ====================== Power plan ======================
        private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        private const string UltimatePerformance = "e9a42b02-d5df-448d-aa00-03f14749eb61";
        private static readonly Regex GuidRegex = new(@"[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", RegexOptions.Compiled);

        private static (int Code, string Output) RunPowerCfg(string args)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("powercfg.exe", args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });
                if (p == null) return (-1, "");
                string o = p.StandardOutput.ReadToEnd();
                p.WaitForExit(8000);
                return (p.ExitCode, o);
            }
            catch { return (-1, ""); }
        }

        internal static string? GetActiveScheme()
        {
            var (code, output) = RunPowerCfg("/getactivescheme");
            if (code != 0) return null;
            var m = GuidRegex.Match(output);
            return m.Success ? m.Value.ToLowerInvariant() : null;
        }

        public static void ApplyHighPerformancePower(AppState state, Action<string> log)
        {
            string? current = GetActiveScheme();
            if (current == null) { log("  ✗ Không đọc được power plan hiện tại"); return; }
            if (current == HighPerformance || current == UltimatePerformance || current == state.CreatedPowerScheme)
            {
                log("  ✓ Máy đã ở chế độ Hiệu năng cao");
                return;
            }

            state.PreviousPowerScheme ??= current;

            if (RunPowerCfg($"/setactive {HighPerformance}").Code == 0)
            {
                log("  ✓ Đã chuyển sang power plan High performance");
                state.Save();
                return;
            }

            // Nhiều laptop (Modern Standby) không có sẵn High performance -> nhân bản Ultimate Performance
            var (code, output) = RunPowerCfg($"/duplicatescheme {UltimatePerformance}");
            var m = GuidRegex.Match(output);
            if (code == 0 && m.Success)
            {
                string created = m.Value.ToLowerInvariant();
                if (RunPowerCfg($"/setactive {created}").Code == 0)
                {
                    state.CreatedPowerScheme = created;
                    state.Save();
                    log("  ✓ Đã chuyển sang power plan Ultimate Performance");
                    return;
                }
                RunPowerCfg($"/delete {created}");
            }
            log("  ✗ Máy này không hỗ trợ power plan Hiệu năng cao");
            state.PreviousPowerScheme = null;
            state.Save();
        }

        public static void RestorePower(AppState state, Action<string> log)
        {
            if (state.PreviousPowerScheme == null && state.CreatedPowerScheme == null) return;
            if (state.PreviousPowerScheme != null)
            {
                bool ok = RunPowerCfg($"/setactive {state.PreviousPowerScheme}").Code == 0;
                log(ok ? "  ✓ Đã trả power plan về như cũ" : "  ✗ Không trả được power plan cũ (hãy chọn lại trong Power Options)");
            }
            if (state.CreatedPowerScheme != null) RunPowerCfg($"/delete {state.CreatedPowerScheme}");
            state.PreviousPowerScheme = null;
            state.CreatedPowerScheme = null;
            state.Save();
        }

        // ====================== Mạng ======================
        public static bool FlushDns()
        {
            try { return Native.DnsFlushResolverCache(); }
            catch { return false; }
        }

        // ====================== Explorer ======================
        public static void KillExplorer(Action<string> log)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("taskkill.exe", "/F /IM explorer.exe")
                { UseShellExecute = false, CreateNoWindow = true });
                p?.WaitForExit(5000);
                log("  ✓ Đã tắt Windows Explorer");
            }
            catch (Exception ex) { log($"  ✗ Không tắt được Explorer: {ex.Message}"); }
        }

        /// <summary>
        /// Mở lại Explorer. Ưu tiên chạy bằng quyền thường (qua Task Scheduler) để tránh
        /// Explorer bị "kế thừa" quyền Admin của OGMode; nếu thất bại thì mở trực tiếp.
        /// </summary>
        public static void StartExplorer(Action<string> log)
        {
            if (Process.GetProcessesByName("explorer").Length > 0) { log("  ✓ Explorer đang chạy"); return; }

            const string task = "OGMode_StartExplorer";
            try
            {
                RunHidden("schtasks.exe", $"/Create /TN {task} /TR \"explorer.exe\" /SC ONCE /ST 00:00 /RL LIMITED /IT /F");
                RunHidden("schtasks.exe", $"/Run /TN {task}");
                for (int i = 0; i < 20; i++)
                {
                    Thread.Sleep(250);
                    if (Process.GetProcessesByName("explorer").Length > 0) break;
                }
            }
            catch { }
            finally { try { RunHidden("schtasks.exe", $"/Delete /TN {task} /F"); } catch { } }

            if (Process.GetProcessesByName("explorer").Length == 0)
            {
                try { Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true }); }
                catch (Exception ex) { log($"  ✗ Không mở được Explorer: {ex.Message}"); return; }
            }
            log("  ✓ Đã mở lại Windows Explorer");
        }

        private static void RunHidden(string file, string args)
        {
            using var p = Process.Start(new ProcessStartInfo(file, args)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true });
            p?.StandardOutput.ReadToEnd();
            p?.WaitForExit(8000);
        }
    }
}
