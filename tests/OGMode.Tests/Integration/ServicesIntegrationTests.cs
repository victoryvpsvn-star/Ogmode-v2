using System.ServiceProcess;
using ogmm.Core;

namespace OGMode.Tests.Integration
{
    [Collection("OGModeData")]
    public class ServicesIntegrationTests
    {
        [IntegrationFact]
        public void StopAll_ThenRestoreAll_BringsEveryRunningServiceBack()
        {
            using var iso = new IsolatedAppData();
            var wanted = SysProbe.AllGameServices().ToList();
            var before = SysProbe.ServiceStatuses(wanted);
            var state = AppState.Load();
            var logs = new List<string>();

            try
            {
                ServiceTweaks.StopAll(wanted, state, logs.Add);

                // Mọi dịch vụ OGMode báo là đã dừng thì thực sự phải đang dừng
                foreach (var name in state.StoppedServices)
                    Assert.Equal(ServiceControllerStatus.Stopped, SysProbe.ServiceStatus(name));

                // Danh sách dừng được lưu xuống đĩa để khôi phục sau khi tắt/mở lại app
                Assert.Equal(state.StoppedServices, AppState.Load().StoppedServices);
                Assert.NotEmpty(logs);
            }
            finally
            {
                ServiceTweaks.RestoreAll(state, logs.Add);
            }

            var after = SysProbe.ServiceStatuses(wanted);
            foreach (var kv in before.Where(k => k.Value == ServiceControllerStatus.Running))
                Assert.True(after[kv.Key] == ServiceControllerStatus.Running,
                    $"Dịch vụ {kv.Key} đang chạy trước đó nhưng chưa được bật lại ({after[kv.Key]})");
            Assert.Empty(state.StoppedServices);
        }

        [IntegrationFact]
        public void StopAll_LogsMissingServices_AndLeavesStateEmpty()
        {
            var state = new AppState { Transient = true };
            var logs = new List<string>();

            ServiceTweaks.StopAll(new[] { new GameService("OGMode_Khong_Ton_Tai", "Dịch vụ giả") }, state, logs.Add);

            Assert.Empty(state.StoppedServices);
            Assert.Contains(logs, l => l.Contains("không có trên máy này"));
        }

        [IntegrationFact]
        public void StopAll_Cancelled_Throws_AndLeavesServicesUntouched()
        {
            var wanted = SysProbe.AllGameServices().ToList();
            var before = SysProbe.ServiceStatuses(wanted);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                ServiceTweaks.StopAll(wanted, new AppState { Transient = true }, _ => { }, cts.Token));

            Assert.Equal(before, SysProbe.ServiceStatuses(wanted));
        }
    }
}
