namespace OGMode.Tests
{
    /// <summary>
    /// Mọi test đụng tới biến môi trường OGMODE_DATA_DIR phải nằm chung collection này
    /// (chạy tuần tự) để không giẫm lên nhau và không bao giờ chạm %AppData% thật.
    /// </summary>
    [CollectionDefinition("OGModeData", DisableParallelization = true)]
    public sealed class OGModeDataCollection { }

    /// <summary>Thư mục tạm riêng cho từng test, tự xoá khi Dispose.</summary>
    public sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ogmode-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Combine(params string[] parts) => System.IO.Path.Combine(new[] { Path }.Concat(parts).ToArray());

        public string CreateFile(string relative, int bytes = 10, DateTime? lastWriteUtc = null)
        {
            string full = Combine(relative.Split('/', '\\'));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, new byte[bytes]);
            if (lastWriteUtc.HasValue) File.SetLastWriteTimeUtc(full, lastWriteUtc.Value);
            return full;
        }

        public void Dispose()
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
                    File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(Path, recursive: true);
            }
            catch { /* dọn tạm, lỗi cũng không sao */ }
        }
    }

    /// <summary>Trỏ AppState sang thư mục tạm trong suốt vòng đời của test.</summary>
    public sealed class IsolatedAppData : IDisposable
    {
        private readonly string? _old;
        public TempDir Dir { get; } = new();

        public IsolatedAppData()
        {
            _old = Environment.GetEnvironmentVariable("OGMODE_DATA_DIR");
            Environment.SetEnvironmentVariable("OGMODE_DATA_DIR", Dir.Path);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("OGMODE_DATA_DIR", _old);
            Dir.Dispose();
        }
    }
}
