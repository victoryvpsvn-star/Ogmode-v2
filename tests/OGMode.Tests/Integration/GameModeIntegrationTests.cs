using System.Diagnostics;
using System.ServiceProcess;
using ogmm.Core;

namespace OGMode.Tests.Integration
{
    [Collection("OGModeData")]
    public class GameModeIntegrationTests
    {
        private static void AssertServicesBack(Dictionary<string, ServiceControllerStatus> before)
        {
            var after = SysProbe.ServiceStatuses(SysProbe.AllGameServices());
            foreach (var kv in before.Where(k => k.Value == ServiceControllerStatus.Running))
                Assert.True(after[kv.Key] == ServiceControllerStatus.Running,
                    $"Dịch vụ {kv.Key} chưa được bật lại ({after[kv.Key]})");
        }

        private static void RestoreRegistryManually(Dictionary<string, int?> before)
        {
            foreach (var kv in before)
            {
                int sep = kv.Key.LastIndexOf('|');
                SysProbe.WriteDword(kv.Key[..sep], kv.Key[(sep + 1)..], kv.Value);
            }
        }

        [IntegrationFact]
        public void FullCycle_Enable_ThenDisable_RestoresEverything()
        {
            using var iso = new IsolatedAppData();
            var regBefore = SysProbe.RegistrySnapshot();
            var spiBefore = SysProbe.SpiSnapshot();
            string? powerBefore = SysProbe.ActiveScheme();
            var svcBefore = SysProbe.ServiceStatuses(SysProbe.AllGameServices());
            var enableLog = new List<string>();

            try
            {
                GameModeEngine.Enable(new GameModeOptions { Bluetooth = true, Xbox = true, KillExplorer = false }, enableLog.Add);

                // --- Đã bật: kiểm tra từng thay đổi ---
                var saved = AppState.Load(); // đọc lại từ đĩa như khi mở lại app
                Assert.True(saved.GameModeEnabled);
                Assert.NotEmpty(saved.RegistryBackup);
                foreach (var t in SystemTweaks.GameRegistryTweaks)
                    Assert.Equal(t.Value, SysProbe.ReadDword(t.Path, t.Name));
                foreach (var name in saved.StoppedServices)
                    Assert.Equal(ServiceControllerStatus.Stopped, SysProbe.ServiceStatus(name));
                Assert.Contains(enableLog, l => l.Contains("Power plan"));
                Assert.Contains(enableLog, l => l.Contains("Giải phóng RAM"));

                // --- Tắt (dùng state đọc từ đĩa) ---
                var disableLog = new List<string>();
                GameModeEngine.Disable(disableLog.Add);

                var after = AppState.Load();
                Assert.False(after.GameModeEnabled);
                Assert.Empty(after.StoppedServices);
                Assert.Empty(after.RegistryBackup);
                Assert.Empty(after.SpiBackup);
                Assert.Null(after.PreviousPowerScheme);
                Assert.Null(after.CreatedPowerScheme);
                Assert.False(after.ExplorerKilled);

                Assert.Equal(regBefore, SysProbe.RegistrySnapshot());
                Assert.Equal(spiBefore, SysProbe.SpiSnapshot());
                Assert.Equal(powerBefore, SysProbe.ActiveScheme());
                AssertServicesBack(svcBefore);
            }
            finally
            {
                // Dù test fail cũng cố trả máy về như cũ
                try { GameModeEngine.Disable(_ => { }); } catch { }
                RestoreRegistryManually(regBefore);
            }
        }

        [IntegrationFact]
        public void Enable_Twice_IsIdempotent_AndDisableStillRestores()
        {
            using var iso = new IsolatedAppData();
            var regBefore = SysProbe.RegistrySnapshot();
            string? powerBefore = SysProbe.ActiveScheme();
            try
            {
                GameModeEngine.Enable(new GameModeOptions(), _ => { });
                var second = new List<string>();
                GameModeEngine.Enable(new GameModeOptions(), second.Add);
                Assert.Single(second);
                Assert.Contains("đang bật sẵn", second[0]);

                GameModeEngine.Disable(_ => { });

                Assert.Equal(regBefore, SysProbe.RegistrySnapshot());
                Assert.Equal(powerBefore, SysProbe.ActiveScheme());
            }
            finally
            {
                try { GameModeEngine.Disable(_ => { }); } catch { }
                RestoreRegistryManually(regBefore);
            }
        }

        [IntegrationFact]
        public void Disable_WhenNeverEnabled_ChangesNothing()
        {
            using var iso = new IsolatedAppData();
            var regBefore = SysProbe.RegistrySnapshot();
            string? powerBefore = SysProbe.ActiveScheme();
            var svcBefore = SysProbe.ServiceStatuses(SysProbe.AllGameServices());

            GameModeEngine.Disable(_ => { });

            Assert.Equal(regBefore, SysProbe.RegistrySnapshot());
            Assert.Equal(powerBefore, SysProbe.ActiveScheme());
            Assert.Equal(svcBefore, SysProbe.ServiceStatuses(SysProbe.AllGameServices()));
            Assert.False(AppState.Load().GameModeEnabled);
        }

        [IntegrationFact]
        public void Disable_RecoversFromInterruptedEnable()
        {
            // Giả lập: máy sập / app bị tắt giữa chừng khi đang bật Game Mode
            // (đã đổi registry + lưu sao lưu nhưng chưa kịp đánh dấu GameModeEnabled)
            using var iso = new IsolatedAppData();
            var regBefore = SysProbe.RegistrySnapshot();
            try
            {
                var state = AppState.Load();
                SystemTweaks.ApplyRegistry(state, _ => { });
                Assert.False(AppState.Load().GameModeEnabled);
                Assert.NotEmpty(AppState.Load().RegistryBackup);

                GameModeEngine.Disable(_ => { }); // lần mở app sau đó, người dùng bấm tắt

                Assert.Equal(regBefore, SysProbe.RegistrySnapshot());
            }
            finally { RestoreRegistryManually(regBefore); }
        }

        [IntegrationFact]
        public void Explorer_KilledByEnable_IsStartedAgainByDisable()
        {
            if (Process.GetProcessesByName("explorer").Length == 0) return; // runner không có shell -> bỏ qua

            using var iso = new IsolatedAppData();
            var regBefore = SysProbe.RegistrySnapshot();
            try
            {
                GameModeEngine.Enable(new GameModeOptions { KillExplorer = true }, _ => { });
                Assert.True(AppState.Load().ExplorerKilled);

                GameModeEngine.Disable(_ => { });

                Assert.False(AppState.Load().ExplorerKilled);
                Assert.NotEmpty(Process.GetProcessesByName("explorer"));
            }
            finally
            {
                try { GameModeEngine.Disable(_ => { }); } catch { }
                RestoreRegistryManually(regBefore);
                if (Process.GetProcessesByName("explorer").Length == 0)
                    SystemTweaks.StartExplorer(_ => { });
            }
        }
    }
}
