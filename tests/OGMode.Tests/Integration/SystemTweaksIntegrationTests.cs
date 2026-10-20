using ogmm.Core;

namespace OGMode.Tests.Integration
{
    [Collection("OGModeData")]
    public class RegistryIntegrationTests
    {
        [IntegrationFact]
        public void Apply_SetsEveryTweak_ThenRestore_ReturnsOriginal()
        {
            using var iso = new IsolatedAppData();
            var before = SysProbe.RegistrySnapshot();
            var state = AppState.Load();
            try
            {
                SystemTweaks.ApplyRegistry(state, _ => { });

                foreach (var t in SystemTweaks.GameRegistryTweaks)
                    Assert.Equal(t.Value, SysProbe.ReadDword(t.Path, t.Name));
                Assert.Equal(SystemTweaks.GameRegistryTweaks.Length, state.RegistryBackup.Count);

                SystemTweaks.RestoreRegistry(state, _ => { });

                Assert.Equal(before, SysProbe.RegistrySnapshot());
                Assert.Empty(state.RegistryBackup);
            }
            finally
            {
                foreach (var kv in before)
                {
                    int sep = kv.Key.LastIndexOf('|');
                    SysProbe.WriteDword(kv.Key[..sep], kv.Key[(sep + 1)..], kv.Value);
                }
            }
        }

        [IntegrationFact]
        public void Restore_BringsBackPreexistingValue()
        {
            using var iso = new IsolatedAppData();
            var t = SystemTweaks.GameRegistryTweaks[0];
            int? original = SysProbe.ReadDword(t.Path, t.Name);
            int preset = t.Value == 1 ? 0 : 1; // khác giá trị tweak để chắc chắn có thay đổi
            try
            {
                SysProbe.WriteDword(t.Path, t.Name, preset);
                var state = AppState.Load();

                SystemTweaks.ApplyRegistry(state, _ => { });
                Assert.Equal(t.Value, SysProbe.ReadDword(t.Path, t.Name));

                SystemTweaks.RestoreRegistry(state, _ => { });
                Assert.Equal(preset, SysProbe.ReadDword(t.Path, t.Name));
            }
            finally { SysProbe.WriteDword(t.Path, t.Name, original); }
        }

        [IntegrationFact]
        public void Apply_Twice_KeepsTheOriginalBackup()
        {
            using var iso = new IsolatedAppData();
            var t = SystemTweaks.GameRegistryTweaks[0];
            int? original = SysProbe.ReadDword(t.Path, t.Name);
            int preset = t.Value == 1 ? 0 : 1;
            try
            {
                SysProbe.WriteDword(t.Path, t.Name, preset);
                var state = AppState.Load();

                SystemTweaks.ApplyRegistry(state, _ => { });
                SystemTweaks.ApplyRegistry(state, _ => { }); // lần 2 không được ghi đè bản sao lưu bằng giá trị đã tweak
                SystemTweaks.RestoreRegistry(state, _ => { });

                Assert.Equal(preset, SysProbe.ReadDword(t.Path, t.Name));
            }
            finally { SysProbe.WriteDword(t.Path, t.Name, original); }
        }

        [IntegrationFact]
        public void Restore_DeletesValuesThatDidNotExistBefore()
        {
            using var iso = new IsolatedAppData();
            var t = SystemTweaks.GameRegistryTweaks[0];
            int? original = SysProbe.ReadDword(t.Path, t.Name);
            try
            {
                SysProbe.WriteDword(t.Path, t.Name, null); // đảm bảo chưa tồn tại
                var state = AppState.Load();

                SystemTweaks.ApplyRegistry(state, _ => { });
                Assert.NotNull(SysProbe.ReadDword(t.Path, t.Name));

                SystemTweaks.RestoreRegistry(state, _ => { });
                Assert.Null(SysProbe.ReadDword(t.Path, t.Name));
            }
            finally { SysProbe.WriteDword(t.Path, t.Name, original); }
        }
    }

    [Collection("OGModeData")]
    public class WindowEffectsIntegrationTests
    {
        [IntegrationFact]
        public void Apply_ThenRestore_ReturnsOriginalSettings()
        {
            using var iso = new IsolatedAppData();
            var before = SysProbe.SpiSnapshot();
            var state = AppState.Load();

            SystemTweaks.ApplyWindowEffects(state, _ => { });
            if (before.Count > 0) Assert.NotEmpty(state.SpiBackup); // đọc được thì phải có sao lưu

            SystemTweaks.RestoreWindowEffects(state, _ => { });

            Assert.Equal(before, SysProbe.SpiSnapshot());
            Assert.Empty(state.SpiBackup);
        }
    }

    [Collection("OGModeData")]
    public class PowerIntegrationTests
    {
        [IntegrationFact]
        public void Apply_ThenRestore_ReturnsOriginalPlan_AndDeletesCreatedPlan()
        {
            using var iso = new IsolatedAppData();
            string? before = SysProbe.ActiveScheme();
            if (before == null) return; // máy ảo không có powercfg dùng được -> không có gì để kiểm tra

            var state = AppState.Load();
            SystemTweaks.ApplyHighPerformancePower(state, _ => { });
            string? created = state.CreatedPowerScheme;

            try
            {
                if (state.PreviousPowerScheme != null)
                {
                    string? now = SysProbe.ActiveScheme();
                    Assert.True(
                        now == "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" || now == created,
                        $"Power plan sau khi áp dụng là {now}");
                }
            }
            finally
            {
                SystemTweaks.RestorePower(state, _ => { });
            }

            Assert.Equal(before, SysProbe.ActiveScheme());
            Assert.Null(state.PreviousPowerScheme);
            Assert.Null(state.CreatedPowerScheme);
            if (created != null) Assert.False(SysProbe.SchemeExists(created), "Power plan do OGMode tạo phải bị xoá");
        }
    }

    [Collection("OGModeData")]
    public class ToolsIntegrationTests
    {
        [IntegrationFact]
        public void RunnerIsElevated() => Assert.True(SysProbe.IsAdmin());

        [IntegrationFact]
        public void FlushDns_Works() => Assert.True(SystemTweaks.FlushDns());

        [IntegrationFact]
        public void MemoryClean_TrimsProcesses_AndDoesNotThrow()
        {
            string msg = MemoryCleaner.CleanAll(out int trimmed, out bool standby);
            Assert.True(trimmed > 0, "Phải thu gọn được ít nhất 1 tiến trình");
            Assert.Contains("tiến trình", msg);
            Assert.Equal(standby, msg.Contains("đã xoá standby list"));
        }

        [IntegrationFact]
        public void PurgeStandbyList_Succeeds_WhenAdmin() => Assert.True(MemoryCleaner.PurgeStandbyList());

        [IntegrationFact]
        public void MemoryInfo_IsSane_AfterClean()
        {
            MemoryCleaner.TrimWorkingSets();
            var m = MemoryCleaner.GetInfo();
            Assert.True(m.TotalMiB > 0);
            Assert.InRange(m.UsedPercent, 1, 100);
        }
    }
}
