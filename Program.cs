using ogmm.Core;

namespace ogmm
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Chỉ cho chạy 1 bản OGMode cùng lúc (2 bản sẽ tranh nhau dừng/bật dịch vụ)
            using var mutex = new Mutex(true, @"Local\OGMode.SingleInstance", out bool isNew);
            if (!isNew)
            {
                MessageBox.Show("OGMode đang chạy rồi.", "OGMode", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ReportCrash(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash(e.ExceptionObject as Exception);

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }

        private static void ReportCrash(Exception? ex)
        {
            AppState.LogError(ex?.ToString() ?? "Lỗi không xác định");
            try
            {
                MessageBox.Show("OGMode gặp lỗi: " + (ex?.Message ?? "không rõ") +
                                "\n\nChi tiết đã lưu tại:\n" + Path.Combine(AppState.Folder, "error.log"),
                    "OGMode", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
