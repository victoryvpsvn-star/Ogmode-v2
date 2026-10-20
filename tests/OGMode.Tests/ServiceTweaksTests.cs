using ogmm.Core;

namespace OGMode.Tests
{
    public class ServiceTweaksTests
    {
        // Các dịch vụ mà README v2.0 hứa là KHÔNG bao giờ dừng + vài dịch vụ lõi của Windows.
        private static readonly string[] NeverStop =
        {
            "mpssvc", "BFE", "Dnscache", "EventLog", "Themes",
            "WinDefend", "RpcSs", "RpcEptMapper", "DcomLaunch", "Winmgmt",
            "wuauserv", "Audiosrv", "AudioEndpointBuilder", "Dhcp", "nsi",
            "CryptSvc", "LSM", "PlugPlay", "ProfSvc", "UserManager",
        };

        private static IEnumerable<GameService> All() =>
            ServiceTweaks.Default.Concat(ServiceTweaks.Bluetooth).Concat(ServiceTweaks.Xbox);

        [Fact]
        public void Lists_AreNotEmpty()
        {
            Assert.NotEmpty(ServiceTweaks.Default);
            Assert.NotEmpty(ServiceTweaks.Bluetooth);
            Assert.NotEmpty(ServiceTweaks.Xbox);
        }

        [Fact]
        public void EveryEntry_HasNameAndDisplay()
        {
            foreach (var s in All())
            {
                Assert.False(string.IsNullOrWhiteSpace(s.Name), "Name trống");
                Assert.False(string.IsNullOrWhiteSpace(s.Display), $"Display trống ở {s.Name}");
            }
        }

        [Fact]
        public void NoDuplicateServiceNames_AcrossAllLists()
        {
            var dup = All().GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                           .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.Empty(dup);
        }

        [Fact]
        public void CriticalServices_AreNeverInTheStopList()
        {
            foreach (var s in All())
                Assert.DoesNotContain(s.Name, NeverStop, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void PrefixEntries_AreOnlyPerUserServices()
        {
            // Dịch vụ per-user có hậu tố ngẫu nhiên (_1a2b3c) -> chỉ những mục này được phép IsPrefix
            var allowed = new[] { "BcastDVRUserService", "BluetoothUserService" };
            foreach (var s in All().Where(x => x.IsPrefix))
                Assert.Contains(s.Name, allowed);
        }

        [Fact]
        public void RestoreAll_WithEmptyState_LogsAndDoesNotThrow()
        {
            var logs = new List<string>();
            ServiceTweaks.RestoreAll(new AppState { Transient = true }, logs.Add);
            Assert.Single(logs);
            Assert.Contains("Không có dịch vụ nào", logs[0]);
        }

        [Fact]
        public void RestoreAll_DropsServicesThatNoLongerExist()
        {
            var state = new AppState { Transient = true };
            state.StoppedServices.Add("OGMode_Service_Khong_Ton_Tai_" + Guid.NewGuid().ToString("N"));

            ServiceTweaks.RestoreAll(state, _ => { });

            Assert.Empty(state.StoppedServices);
        }
    }
}
