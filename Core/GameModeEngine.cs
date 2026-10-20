namespace ogmm.Core
{
    internal sealed class GameModeOptions
    {
        public bool Bluetooth { get; init; }
        public bool Xbox { get; init; }
        public bool KillExplorer { get; init; }
    }

    /// <summary>Bật/tắt Game Mode. Mọi thay đổi đều có sao lưu và được hoàn tác chính xác.</summary>
    internal static class GameModeEngine
    {
        public static void Enable(GameModeOptions opt, Action<string> log, CancellationToken ct = default)
        {
            var state = AppState.Load();
            if (state.GameModeEnabled)
            {
                log("Game Mode đang bật sẵn.");
                return;
            }

            log("► Hiệu ứng giao diện");
            SystemTweaks.ApplyRegistry(state, log);
            SystemTweaks.ApplyWindowEffects(state, log);

            log("");
            log("► Power plan");
            SystemTweaks.ApplyHighPerformancePower(state, log);

            log("");
            log("► Dịch vụ nền");
            var services = new List<GameService>(ServiceTweaks.Default);
            if (opt.Bluetooth) services.AddRange(ServiceTweaks.Bluetooth);
            if (opt.Xbox) services.AddRange(ServiceTweaks.Xbox);
            ServiceTweaks.StopAll(services, state, log, ct);

            if (opt.KillExplorer)
            {
                log("");
                log("► Windows Explorer");
                SystemTweaks.KillExplorer(log);
                state.ExplorerKilled = true;
                state.Save();
            }

            log("");
            log("► Giải phóng RAM");
            log("  ✓ " + MemoryCleaner.CleanAll(out _, out _));

            // Đánh dấu bật SAU CÙNG; các bước trên đã được lưu dần nên vẫn khôi phục được nếu bị ngắt giữa chừng.
            state.GameModeEnabled = true;
            state.Save();
        }

        public static void Disable(Action<string> log)
        {
            var state = AppState.Load();

            log("► Dịch vụ nền");
            ServiceTweaks.RestoreAll(state, log);

            log("");
            log("► Hiệu ứng giao diện");
            SystemTweaks.RestoreRegistry(state, log);
            SystemTweaks.RestoreWindowEffects(state, log);

            log("");
            log("► Power plan");
            SystemTweaks.RestorePower(state, log);

            if (state.ExplorerKilled)
            {
                log("");
                log("► Windows Explorer");
                SystemTweaks.StartExplorer(log);
                state.ExplorerKilled = false;
            }

            state.GameModeEnabled = false;
            state.Save();
        }
    }
}
