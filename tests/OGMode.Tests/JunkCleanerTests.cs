using System.Diagnostics;
using ogmm.Core;

namespace OGMode.Tests
{
    public class JunkCleanerTests
    {
        private static readonly DateTime Old = DateTime.UtcNow.AddDays(-3);

        private static JunkCategory Cat(string root, TimeSpan? minAge = null, Func<string, bool>? filter = null,
                                        string[]? blockedBy = null) => new()
        {
            Id = "test",
            Name = "test",
            Roots = () => new[] { root },
            MinAge = minAge ?? TimeSpan.Zero,
            FileFilter = filter,
            BlockedByProcess = blockedBy ?? Array.Empty<string>(),
        };

        // ------------------------- FormatSize -------------------------
        [Theory]
        [InlineData(0, "0 B")]
        [InlineData(1, "1 B")]
        [InlineData(1023, "1023 B")]
        [InlineData(1024, "1 KB")]
        [InlineData(1536, "1.5 KB")]
        [InlineData(1048576, "1 MB")]
        [InlineData(1073741824, "1 GB")]
        [InlineData(1099511627776, "1 TB")]
        public void FormatSize_Works(long bytes, string expected)
        {
            // Culture-invariant: tránh lệch dấu thập phân (1,5 vs 1.5) trên máy khác locale
            var old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
                Assert.Equal(expected, JunkCleaner.FormatSize(bytes));
            }
            finally { Thread.CurrentThread.CurrentCulture = old; }
        }

        // ------------------------- Danh mục -------------------------
        [Fact]
        public void BuildCategories_HaveUniqueIdsAndNames()
        {
            var cats = JunkCleaner.BuildCategories();
            Assert.NotEmpty(cats);
            Assert.Equal(cats.Count, cats.Select(c => c.Id).Distinct().Count());
            Assert.All(cats, c => Assert.False(string.IsNullOrWhiteSpace(c.Name)));
        }

        [Fact]
        public void BuildCategories_ContainExpectedIds()
        {
            var ids = JunkCleaner.BuildCategories().Select(c => c.Id).ToHashSet();
            foreach (var id in new[] { "temp", "wu", "browser", "crash", "thumbs", "shader", "recycle" })
                Assert.Contains(id, ids);
        }

        [Fact]
        public void BuildCategories_RecycleBin_IsOptInAndNeedsConfirm()
        {
            var rb = JunkCleaner.BuildCategories().Single(c => c.IsRecycleBin);
            Assert.False(rb.DefaultChecked);
            Assert.True(rb.NeedsConfirm);
        }

        [Fact]
        public void BuildCategories_NoRootIsDangerous()
        {
            foreach (var c in JunkCleaner.BuildCategories().Where(c => !c.IsRecycleBin))
                foreach (var root in c.Roots())
                    Assert.True(JunkCleaner.IsSafeRoot(root), $"[{c.Id}] root không an toàn: {root}");
        }

        [Fact]
        public void ThumbnailFilter_OnlyMatchesCacheDbFiles()
        {
            var f = JunkCleaner.BuildCategories().Single(c => c.Id == "thumbs").FileFilter!;
            Assert.True(f(@"C:\x\thumbcache_256.db"));
            Assert.True(f(@"C:\x\iconcache_48.db"));
            Assert.False(f(@"C:\x\thumbcache_256.txt"));
            Assert.False(f(@"C:\x\important.db"));
            Assert.False(f(@"C:\x\document.docx"));
        }

        // ------------------------- IsSafeRoot -------------------------
        [Fact]
        public void IsSafeRoot_RejectsDriveRootAndTopLevelFolders()
        {
            string drive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!;
            Assert.False(JunkCleaner.IsSafeRoot(drive));
            Assert.False(JunkCleaner.IsSafeRoot(Path.Combine(drive, "Users")));
            Assert.False(JunkCleaner.IsSafeRoot(""));
        }

        [Theory]
        [InlineData(Environment.SpecialFolder.UserProfile)]
        [InlineData(Environment.SpecialFolder.Windows)]
        [InlineData(Environment.SpecialFolder.System)]
        [InlineData(Environment.SpecialFolder.ProgramFiles)]
        [InlineData(Environment.SpecialFolder.LocalApplicationData)]
        [InlineData(Environment.SpecialFolder.ApplicationData)]
        [InlineData(Environment.SpecialFolder.CommonApplicationData)]
        [InlineData(Environment.SpecialFolder.MyDocuments)]
        public void IsSafeRoot_RejectsImportantFolders(Environment.SpecialFolder folder)
        {
            string path = Environment.GetFolderPath(folder);
            if (string.IsNullOrEmpty(path)) return; // máy không có thư mục này
            Assert.False(JunkCleaner.IsSafeRoot(path), path);
        }

        [Fact]
        public void IsSafeRoot_RejectsParentOfImportantFolder()
        {
            string? parent = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            Assert.False(JunkCleaner.IsSafeRoot(parent!)); // = %USERPROFILE%\AppData
        }

        [Fact]
        public void IsSafeRoot_AcceptsNormalTempSubfolder()
        {
            using var t = new TempDir();
            Assert.True(JunkCleaner.IsSafeRoot(t.Path));
        }

        // ------------------------- Scan -------------------------
        [Fact]
        public void Scan_CountsAllFiles_WhenNoAgeLimit()
        {
            using var t = new TempDir();
            t.CreateFile("a.tmp", 100);
            t.CreateFile("sub/b.tmp", 200);
            t.CreateFile("sub/deep/c.tmp", 300);

            var r = JunkCleaner.Scan(Cat(t.Path), CancellationToken.None);

            Assert.Equal(3, r.Files);
            Assert.Equal(600, r.Bytes);
        }

        [Fact]
        public void Scan_SkipsRecentFiles_WhenMinAgeSet()
        {
            using var t = new TempDir();
            t.CreateFile("old.tmp", 100, Old);
            t.CreateFile("new.tmp", 500); // vừa tạo

            var r = JunkCleaner.Scan(Cat(t.Path, TimeSpan.FromHours(1)), CancellationToken.None);

            Assert.Equal(1, r.Files);
            Assert.Equal(100, r.Bytes);
        }

        [Fact]
        public void Scan_RespectsFileFilter()
        {
            using var t = new TempDir();
            t.CreateFile("thumbcache_32.db", 50);
            t.CreateFile("keep.txt", 70);

            var r = JunkCleaner.Scan(Cat(t.Path, filter: f => f.EndsWith(".db")), CancellationToken.None);

            Assert.Equal(1, r.Files);
            Assert.Equal(50, r.Bytes);
        }

        [Fact]
        public void Scan_ReturnsZero_ForMissingFolder()
        {
            using var t = new TempDir();
            var r = JunkCleaner.Scan(Cat(t.Combine("khong-co")), CancellationToken.None);
            Assert.Equal(new ScanResult(0, 0), r);
        }

        [Fact]
        public void Scan_RefusesUnsafeRoot()
        {
            string drive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!;
            var r = JunkCleaner.Scan(Cat(drive), CancellationToken.None);
            Assert.Equal(new ScanResult(0, 0), r);
        }

        [Fact]
        public void Scan_Throws_WhenCancelled()
        {
            using var t = new TempDir();
            t.CreateFile("a.tmp");
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.Throws<OperationCanceledException>(() => JunkCleaner.Scan(Cat(t.Path), cts.Token));
        }

        // ------------------------- Clean -------------------------
        [Fact]
        public void Clean_DeletesFilesAndRemovesEmptyFolders()
        {
            using var t = new TempDir();
            t.CreateFile("a.tmp", 100);
            t.CreateFile("sub/b.tmp", 200);
            t.CreateFile("sub/deep/c.tmp", 300);

            var r = JunkCleaner.Clean(Cat(t.Path), _ => { }, CancellationToken.None);

            Assert.Equal(3, r.Deleted);
            Assert.Equal(0, r.Failed);
            Assert.Equal(600, r.FreedBytes);
            Assert.True(Directory.Exists(t.Path));                       // thư mục gốc được giữ lại
            Assert.Empty(Directory.GetFileSystemEntries(t.Path));        // con thì dọn sạch
        }

        [Fact]
        public void Clean_KeepsRecentFiles_AndFoldersThatStillHaveFiles()
        {
            using var t = new TempDir();
            string oldF = t.CreateFile("old.tmp", 100, Old);
            string newF = t.CreateFile("sub/new.tmp", 100);

            var r = JunkCleaner.Clean(Cat(t.Path, TimeSpan.FromHours(1)), _ => { }, CancellationToken.None);

            Assert.Equal(1, r.Deleted);
            Assert.False(File.Exists(oldF));
            Assert.True(File.Exists(newF));
            Assert.True(Directory.Exists(t.Combine("sub")));
        }

        [Fact]
        public void Clean_OnlyDeletesFilesMatchingFilter()
        {
            using var t = new TempDir();
            string cache = t.CreateFile("thumbcache_96.db");
            string other = t.CreateFile("quan-trong.db");

            JunkCleaner.Clean(Cat(t.Path, filter: f => Path.GetFileName(f).StartsWith("thumbcache_")), _ => { }, CancellationToken.None);

            Assert.False(File.Exists(cache));
            Assert.True(File.Exists(other));
        }

        [Fact]
        public void Clean_DeletesReadOnlyFiles()
        {
            using var t = new TempDir();
            string f = t.CreateFile("ro.tmp");
            File.SetAttributes(f, FileAttributes.ReadOnly);

            var r = JunkCleaner.Clean(Cat(t.Path), _ => { }, CancellationToken.None);

            Assert.Equal(1, r.Deleted);
            Assert.False(File.Exists(f));
        }

        [Fact]
        public void Clean_CountsLockedFilesAsFailed_NotCrash()
        {
            using var t = new TempDir();
            string locked = t.CreateFile("locked.tmp");
            t.CreateFile("free.tmp");

            using var fs = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);
            var r = JunkCleaner.Clean(Cat(t.Path), _ => { }, CancellationToken.None);

            Assert.Equal(1, r.Deleted);
            Assert.Equal(1, r.Failed);
            Assert.True(File.Exists(locked));
        }

        [Fact]
        public void Clean_RefusesUnsafeRoot()
        {
            string drive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!;
            var r = JunkCleaner.Clean(Cat(drive), _ => { }, CancellationToken.None);
            Assert.Equal(0, r.Deleted);
            Assert.Equal(0, r.FreedBytes);
        }

        [Fact]
        public void Clean_IsBlocked_WhenBlockingProcessRunning()
        {
            using var t = new TempDir();
            string f = t.CreateFile("a.tmp");
            string me = Process.GetCurrentProcess().ProcessName;

            var r = JunkCleaner.Clean(Cat(t.Path, blockedBy: new[] { me }), _ => { }, CancellationToken.None);

            Assert.True(r.Blocked);
            Assert.False(string.IsNullOrEmpty(r.BlockedReason));
            Assert.True(File.Exists(f));
        }

        [Fact]
        public void Clean_Throws_WhenCancelled_AndLeavesFiles()
        {
            using var t = new TempDir();
            string f = t.CreateFile("a.tmp");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => JunkCleaner.Clean(Cat(t.Path), _ => { }, cts.Token));
            Assert.True(File.Exists(f));
        }

        [Fact]
        public void Clean_HandlesSingleFileRoot()
        {
            using var t = new TempDir();
            string f = t.CreateFile("MEMORY.DMP", 400);

            var r = JunkCleaner.Clean(Cat(f), _ => { }, CancellationToken.None);

            Assert.Equal(1, r.Deleted);
            Assert.False(File.Exists(f));
            Assert.True(Directory.Exists(t.Path));
        }
    }
}
