using ogmm.Core;

namespace OGMode.Tests
{
    [Collection("OGModeData")]
    public class AppStateTests
    {
        [Fact]
        public void Folder_UsesOverride_WhenEnvVarSet()
        {
            using var iso = new IsolatedAppData();
            Assert.Equal(iso.Dir.Path, AppState.Folder);
        }

        [Fact]
        public void Folder_DefaultsToAppDataOGMode_WhenNoOverride()
        {
            string? old = Environment.GetEnvironmentVariable("OGMODE_DATA_DIR");
            try
            {
                Environment.SetEnvironmentVariable("OGMODE_DATA_DIR", null);
                string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OGMode");
                Assert.Equal(expected, AppState.Folder);
            }
            finally { Environment.SetEnvironmentVariable("OGMODE_DATA_DIR", old); }
        }

        [Fact]
        public void Load_ReturnsCleanState_WhenNoFile()
        {
            using var iso = new IsolatedAppData();
            var s = AppState.Load();
            Assert.False(s.GameModeEnabled);
            Assert.Empty(s.StoppedServices);
            Assert.Empty(s.RegistryBackup);
            Assert.Empty(s.SpiBackup);
        }

        [Fact]
        public void SaveThenLoad_RoundTripsEverything()
        {
            using var iso = new IsolatedAppData();
            var s = new AppState
            {
                GameModeEnabled = true,
                ExplorerKilled = true,
                PreviousPowerScheme = "381b4222-f694-41f0-9685-ff5bb260df2e",
                CreatedPowerScheme = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            };
            s.StoppedServices.AddRange(new[] { "SysMain", "WSearch" });
            s.RegistryBackup[@"Software\Test|Value1"] = 1;
            s.RegistryBackup[@"Software\Test|Value2"] = null; // trước đó chưa tồn tại
            s.SpiBackup[0x0048u] = 7;
            s.Save();

            var l = AppState.Load();
            Assert.True(l.GameModeEnabled);
            Assert.True(l.ExplorerKilled);
            Assert.Equal(s.PreviousPowerScheme, l.PreviousPowerScheme);
            Assert.Equal(s.CreatedPowerScheme, l.CreatedPowerScheme);
            Assert.Equal(new[] { "SysMain", "WSearch" }, l.StoppedServices);
            Assert.Equal(1, l.RegistryBackup[@"Software\Test|Value1"]);
            Assert.True(l.RegistryBackup.ContainsKey(@"Software\Test|Value2"));
            Assert.Null(l.RegistryBackup[@"Software\Test|Value2"]);
            Assert.Equal(7, l.SpiBackup[0x0048u]);
        }

        [Fact]
        public void Save_DoesNothing_ForTransientState()
        {
            using var iso = new IsolatedAppData();
            new AppState { Transient = true, GameModeEnabled = true }.Save();
            Assert.False(File.Exists(Path.Combine(iso.Dir.Path, "state.json")));
        }

        [Fact]
        public void Save_LeavesNoTempFileBehind()
        {
            using var iso = new IsolatedAppData();
            new AppState { GameModeEnabled = true }.Save();
            Assert.True(File.Exists(Path.Combine(iso.Dir.Path, "state.json")));
            Assert.False(File.Exists(Path.Combine(iso.Dir.Path, "state.json.tmp")));
        }

        [Fact]
        public void Load_FallsBackToCleanState_WhenFileCorrupt()
        {
            using var iso = new IsolatedAppData();
            File.WriteAllText(Path.Combine(iso.Dir.Path, "state.json"), "{ this is not json ]]]");
            var s = AppState.Load();
            Assert.False(s.GameModeEnabled);
            Assert.Empty(s.StoppedServices);
        }

        [Fact]
        public void Save_Overwrites_PreviousState()
        {
            using var iso = new IsolatedAppData();
            new AppState { GameModeEnabled = true }.Save();
            new AppState { GameModeEnabled = false }.Save();
            Assert.False(AppState.Load().GameModeEnabled);
        }

        [Fact]
        public void LogError_AppendsLines()
        {
            using var iso = new IsolatedAppData();
            AppState.LogError("lỗi một");
            AppState.LogError("lỗi hai");
            string text = File.ReadAllText(Path.Combine(iso.Dir.Path, "error.log"));
            Assert.Contains("lỗi một", text);
            Assert.Contains("lỗi hai", text);
            Assert.Equal(2, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        }

        [Fact]
        public void GameMode_Enable_IsNoOp_WhenAlreadyEnabled()
        {
            using var iso = new IsolatedAppData();
            new AppState { GameModeEnabled = true }.Save();

            var logs = new List<string>();
            GameModeEngine.Enable(new GameModeOptions(), logs.Add);

            Assert.Single(logs);
            Assert.Contains("đang bật sẵn", logs[0]);
        }
    }
}
