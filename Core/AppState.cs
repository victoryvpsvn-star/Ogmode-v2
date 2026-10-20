using System.Text.Json;
using System.Text.Json.Serialization;

namespace ogmm.Core
{
    /// <summary>
    /// Trạng thái của OGMode, lưu ở %AppData%\OGMode\state.json.
    /// Dùng để khôi phục ĐÚNG những gì OGMode đã đổi (kể cả sau khi tắt/mở lại app hoặc máy sập).
    /// </summary>
    internal sealed class AppState
    {
        /// <summary>true = đối tượng tạm (vd. dọn rác), không bao giờ ghi ra state.json.</summary>
        [JsonIgnore]
        public bool Transient { get; init; }

        public bool GameModeEnabled { get; set; }

        /// <summary>Các dịch vụ OGMode đã dừng (theo thứ tự dừng). Khi khôi phục sẽ chạy ngược lại.</summary>
        public List<string> StoppedServices { get; set; } = new();

        public bool ExplorerKilled { get; set; }

        public string? PreviousPowerScheme { get; set; }
        public string? CreatedPowerScheme { get; set; }

        /// <summary>Giá trị registry (DWORD) gốc. Key = "đường\dẫn|tên". Null = trước đó chưa tồn tại.</summary>
        public Dictionary<string, int?> RegistryBackup { get; set; } = new();

        /// <summary>Giá trị SystemParametersInfo gốc. Key = mã SPI_GET.</summary>
        public Dictionary<uint, int> SpiBackup { get; set; } = new();

        // ------------------------------------------------------------------

        private static readonly object _lock = new();
        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

        /// <summary>Thư mục dữ liệu. Có thể đổi bằng biến môi trường OGMODE_DATA_DIR (dùng cho unit test, không đụng %AppData% thật).</summary>
        public static string Folder
        {
            get
            {
                string? overrideDir = Environment.GetEnvironmentVariable("OGMODE_DATA_DIR");
                return !string.IsNullOrWhiteSpace(overrideDir)
                    ? overrideDir
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OGMode");
            }
        }

        private static string FilePath => Path.Combine(Folder, "state.json");

        public static AppState Load()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(FilePath))
                    {
                        var s = JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath));
                        if (s != null) return s;
                    }
                }
                catch { /* file hỏng -> bắt đầu lại từ trạng thái sạch */ }
                return new AppState();
            }
        }

        public void Save()
        {
            if (Transient) return;
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    string tmp = FilePath + ".tmp";
                    File.WriteAllText(tmp, JsonSerializer.Serialize(this, _json));
                    File.Move(tmp, FilePath, overwrite: true);
                }
                catch { /* không để lỗi ghi file làm hỏng thao tác chính */ }
            }
        }

        public static void LogError(string message)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.AppendAllText(Path.Combine(Folder, "error.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
