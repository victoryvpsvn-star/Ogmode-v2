using System.ServiceProcess;
using Microsoft.VisualBasic.FileIO;
using ogmm.Core;

namespace OGMode.Tests.Integration
{
    /// <summary>Dọn rác trên thư mục THẬT của máy ảo (Temp, Windows\Temp, cache trình duyệt, Windows Update, Thùng rác...).</summary>
    [Collection("OGModeData")]
    public class JunkCleanerIntegrationTests
    {
        private static readonly DateTime Old = DateTime.UtcNow.AddDays(-3);
        private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        private static string Win => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        private static JunkCategory Cat(string id) => JunkCleaner.BuildCategories().Single(c => c.Id == id);
        private static CleanResult Clean(string id) => JunkCleaner.Clean(Cat(id), _ => { }, CancellationToken.None);

        [IntegrationFact]
        public void Temp_DeletesOldFiles_KeepsRecentOnes()
        {
            using var scope = new CleanupScope();
            string dir = Path.Combine(Path.GetTempPath(), "ogmode-it-" + Guid.NewGuid().ToString("N"));
            string old = scope.Make(Path.Combine(dir, "old.tmp"), Old);
            string recent = scope.Make(Path.Combine(dir, "recent.tmp"));

            var r = Clean("temp");

            Assert.True(r.Deleted >= 1);
            Assert.False(File.Exists(old));
            Assert.True(File.Exists(recent), "Tệp mới sửa trong 1 giờ qua phải được giữ lại");
        }

        [IntegrationFact]
        public void WindowsTemp_DeletesOldFiles()
        {
            using var scope = new CleanupScope();
            string old = scope.Make(Path.Combine(Win, "Temp", "ogmode-it-" + Guid.NewGuid().ToString("N") + ".tmp"), Old);

            Clean("temp");

            Assert.False(File.Exists(old));
        }

        [IntegrationFact]
        public void BrowserCache_IsCleaned_ButLoginDataCookiesHistoryAreKept()
        {
            using var scope = new CleanupScope();
            string profile = Path.Combine(Local, "Google", "Chrome", "User Data", "Profile 99");

            string cache1 = scope.Make(Path.Combine(profile, "Cache", "Cache_Data", "data_0"));
            string cache2 = scope.Make(Path.Combine(profile, "Code Cache", "js", "index"));
            string gpu = scope.Make(Path.Combine(profile, "GPUCache", "data_1"));
            string login = scope.Make(Path.Combine(profile, "Login Data"));
            string cookies = scope.Make(Path.Combine(profile, "Network", "Cookies"));
            string history = scope.Make(Path.Combine(profile, "History"));
            string bookmarks = scope.Make(Path.Combine(profile, "Bookmarks"));

            Clean("browser");

            Assert.False(File.Exists(cache1));
            Assert.False(File.Exists(cache2));
            Assert.False(File.Exists(gpu));
            Assert.True(File.Exists(login), "Mật khẩu đã lưu không được xoá");
            Assert.True(File.Exists(cookies), "Cookie không được xoá");
            Assert.True(File.Exists(history), "Lịch sử không được xoá");
            Assert.True(File.Exists(bookmarks), "Bookmark không được xoá");
        }

        [IntegrationFact]
        public void Thumbnails_OnlyThumbAndIconCacheDbFilesAreDeleted()
        {
            using var scope = new CleanupScope();
            string dir = Path.Combine(Local, "Microsoft", "Windows", "Explorer");
            string thumb = scope.Make(Path.Combine(dir, "thumbcache_ogmode_it.db"), Old);
            string icon = scope.Make(Path.Combine(dir, "iconcache_ogmode_it.db"), Old);
            string other = scope.Make(Path.Combine(dir, "ogmode_it_keep.db"), Old);

            Clean("thumbs");

            Assert.False(File.Exists(thumb));
            Assert.False(File.Exists(icon));
            Assert.True(File.Exists(other));
        }

        [IntegrationFact]
        public void ShaderCache_IsDeleted()
        {
            using var scope = new CleanupScope();
            string f = scope.Make(Path.Combine(Local, "D3DSCache", "ogmode_it", "shader.bin"));

            Clean("shader");

            Assert.False(File.Exists(f));
        }

        [IntegrationFact]
        public void CrashDumps_AreDeleted()
        {
            using var scope = new CleanupScope();
            string f = scope.Make(Path.Combine(Local, "CrashDumps", "ogmode_it.dmp"));

            Clean("crash");

            Assert.False(File.Exists(f));
        }

        [IntegrationFact]
        public void WindowsUpdateCache_IsDeleted_AndServicesAreStartedAgain()
        {
            using var scope = new CleanupScope();
            string f = scope.Make(Path.Combine(Win, "SoftwareDistribution", "Download", "ogmode_it.tmp"));
            var svc = new[] { "wuauserv", "bits", "dosvc" };
            var before = svc.ToDictionary(s => s, SysProbe.ServiceStatus);

            var r = Clean("wu");

            if (!r.Blocked) Assert.False(File.Exists(f));
            foreach (var s in svc.Where(s => before[s] == ServiceControllerStatus.Running))
                Assert.Equal(ServiceControllerStatus.Running, SysProbe.ServiceStatus(s));
        }

        [IntegrationFact]
        public void RecycleBin_CanBeEmptied()
        {
            string tmp = Path.Combine(Path.GetTempPath(), "ogmode-it-recycle-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(tmp, "xoá vào thùng rác");
            try { FileSystem.DeleteFile(tmp, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin); }
            catch { /* một số phiên không hỗ trợ thùng rác -> vẫn kiểm tra nhánh dọn rỗng */ }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }

            var r = Clean("recycle");

            Assert.Equal(0, r.Failed);
            Assert.Equal(0, JunkCleaner.Scan(Cat("recycle"), CancellationToken.None).Files);
        }

        [IntegrationFact]
        public void ScanEveryCategory_NeverThrows_AndReturnsNonNegative()
        {
            foreach (var c in JunkCleaner.BuildCategories())
            {
                var r = JunkCleaner.Scan(c, CancellationToken.None);
                Assert.True(r.Bytes >= 0 && r.Files >= 0, c.Id);
            }
        }

        [IntegrationFact]
        public void CleanAllDefaultCategories_DoesNotThrow_AndLeavesLessJunk()
        {
            foreach (var c in JunkCleaner.BuildCategories().Where(c => c.DefaultChecked))
            {
                long before = JunkCleaner.Scan(c, CancellationToken.None).Bytes;
                var r = JunkCleaner.Clean(c, _ => { }, CancellationToken.None);
                long after = JunkCleaner.Scan(c, CancellationToken.None).Bytes;

                Assert.True(r.Deleted >= 0 && r.Failed >= 0, c.Id);
                Assert.True(after <= before + 1_048_576, $"[{c.Id}] sau khi dọn dung lượng không được tăng ({before} -> {after})"); // dung sai 1 MB vì hệ thống vẫn đang ghi tệp
            }
        }
    }
}
