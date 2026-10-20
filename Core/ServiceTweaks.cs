using System.ServiceProcess;

namespace ogmm.Core
{
    internal readonly record struct GameService(string Name, string Display, bool IsPrefix = false);

    /// <summary>
    /// Dừng/khởi động dịch vụ an toàn: xử lý dịch vụ phụ thuộc, có timeout,
    /// và chỉ khôi phục những dịch vụ mà CHÍNH OGMode đã dừng.
    /// </summary>
    internal static class ServiceTweaks
    {
        // Danh sách mặc định: an toàn, không đụng vào bảo mật hay mạng.
        // (Đã BỎ so với bản cũ: EventLog, Dnscache, mpssvc/Firewall, Themes.)
        public static readonly GameService[] Default =
        {
            new("SysMain", "SysMain (Superfetch)"),
            new("WSearch", "Windows Search"),
            new("DiagTrack", "Connected User Experiences and Telemetry"),
            new("MapsBroker", "Downloaded Maps Manager"),
            new("DoSvc", "Delivery Optimization"),
            new("BITS", "Background Intelligent Transfer Service"),
            new("Spooler", "Print Spooler"),
            new("WerSvc", "Windows Error Reporting"),
            new("PcaSvc", "Program Compatibility Assistant"),
            new("TapiSrv", "Telephony"),
            new("SCardSvr", "Smart Card"),
            new("ScDeviceEnum", "Smart Card Device Enumeration"),
            new("seclogon", "Secondary Logon"),
            new("WpcMonSvc", "Parental Controls"),
            new("wisvc", "Windows Insider Service"),
            new("lmhosts", "TCP/IP NetBIOS Helper"),
            new("SharedAccess", "Internet Connection Sharing (ICS)"),
            new("LanmanServer", "Server (chia sẻ file)"),
            new("FrameServer", "Windows Camera Frame Server"),
            new("BcastDVRUserService", "Broadcast DVR User Service", IsPrefix: true),
        };

        public static readonly GameService[] Bluetooth =
        {
            new("bthserv", "Bluetooth Support Service"),
            new("BTAGService", "Bluetooth Audio Gateway Service"),
            new("BthAvctpSvc", "AVCTP service"),
            new("BluetoothUserService", "Bluetooth User Support Service", IsPrefix: true),
        };

        public static readonly GameService[] Xbox =
        {
            new("XblAuthManager", "Xbox Live Auth Manager"),
            new("XblGameSave", "Xbox Live Game Save"),
        };

        // ------------------------------------------------------------------

        public static void StopAll(IEnumerable<GameService> wanted, AppState state, Action<string> log, CancellationToken ct = default)
        {
            ServiceController[] all;
            try { all = ServiceController.GetServices(); }
            catch (Exception ex) { log($"  ✗ Không đọc được danh sách dịch vụ: {ex.Message}"); return; }

            try
            {
                foreach (var svc in wanted)
                {
                    ct.ThrowIfCancellationRequested();
                    var matches = all.Where(s => svc.IsPrefix
                        ? s.ServiceName.StartsWith(svc.Name, StringComparison.OrdinalIgnoreCase)
                        : s.ServiceName.Equals(svc.Name, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (matches.Count == 0) { log($"  - {svc.Display}: không có trên máy này"); continue; }
                    foreach (var sc in matches) StopOne(sc, svc.Display, state, log);
                }
            }
            finally
            {
                foreach (var s in all) s.Dispose();
                state.Save();
            }
        }

        private static void StopOne(ServiceController sc, string display, AppState state, Action<string> log)
        {
            try
            {
                sc.Refresh();
                if (sc.Status != ServiceControllerStatus.Running) { log($"  - {display}: đã tắt từ trước"); return; }
                if (!sc.CanStop) { log($"  ✗ {display}: Windows không cho phép dừng"); return; }

                StopWithDependents(sc, state);
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(12));
                if (!state.StoppedServices.Contains(sc.ServiceName)) state.StoppedServices.Add(sc.ServiceName);
                log($"  ✓ Đã dừng {display}");
            }
            catch (System.ServiceProcess.TimeoutException) { log($"  ✗ {display}: hết thời gian chờ"); }
            catch (Exception ex) { log($"  ✗ {display}: {ex.Message}"); }
        }

        private static void StopWithDependents(ServiceController sc, AppState state)
        {
            foreach (var dep in sc.DependentServices)
            {
                using (dep)
                {
                    dep.Refresh();
                    if (dep.Status != ServiceControllerStatus.Stopped && dep.Status != ServiceControllerStatus.StopPending && dep.CanStop)
                    {
                        StopWithDependents(dep, state);
                        try
                        {
                            dep.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(8));
                            if (!state.StoppedServices.Contains(dep.ServiceName)) state.StoppedServices.Add(dep.ServiceName);
                        }
                        catch { }
                    }
                }
            }
            sc.Refresh();
            if (sc.Status == ServiceControllerStatus.Running) sc.Stop();
        }

        /// <summary>Khởi động lại đúng những dịch vụ OGMode đã dừng (theo thứ tự ngược lại).</summary>
        public static void RestoreAll(AppState state, Action<string> log)
        {
            if (state.StoppedServices.Count == 0) { log("  - Không có dịch vụ nào cần khôi phục"); return; }

            foreach (var name in Enumerable.Reverse(state.StoppedServices).ToList())
            {
                try
                {
                    using var sc = new ServiceController(name);
                    sc.Refresh();
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        if (sc.StartType == ServiceStartMode.Disabled) { log($"  - {name}: đang bị vô hiệu hoá, bỏ qua"); }
                        else
                        {
                            sc.Start();
                            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(12));
                            log($"  ✓ Đã bật lại {name}");
                        }
                    }
                    state.StoppedServices.Remove(name);
                }
                catch (Exception ex)
                {
                    log($"  ✗ {name}: {ex.Message}");
                    // dịch vụ không còn tồn tại (vd. per-user service đã đổi hậu tố) -> bỏ khỏi danh sách
                    if (ex is InvalidOperationException) state.StoppedServices.Remove(name);
                }
            }
            state.Save();
        }
    }
}
