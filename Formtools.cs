using System.Drawing.Drawing2D;
using ogmm.Core;

namespace ogmm
{
    public partial class Formtools : Form
    {
        private Label lblQuickResult = null!;
        private Button btnDns = null!, btnExplorer = null!, btnRestore = null!;

        public Formtools()
        {
            InitializeComponent();
            MakeCircularButton(button1);
            BuildQuickTools();
            GraphUpdate();
            timer1.Start(); // timer nằm trong `components` nên tự dừng khi form bị đóng (bản cũ để Task chạy vô hạn)
        }

        // ---------------- RAM ----------------
        private void timer1_Tick(object? sender, EventArgs e) => GraphUpdate();

        private void GraphUpdate()
        {
            var m = MemoryCleaner.GetInfo();
            if (m.TotalMiB <= 0) return;
            progressBar1.Value = Math.Clamp(m.UsedPercent, 0, 100);
            label2.Text = $"RAM Sử dụng ({m.UsedPercent}%) - trống {m.AvailMiB:N0} MB";
        }

        private async void button1_Click(object? sender, EventArgs e)
        {
            button1.Enabled = false;
            try
            {
                var before = MemoryCleaner.GetInfo();
                string detail = await Task.Run(() => MemoryCleaner.CleanAll(out _, out _));
                await Task.Delay(1500); // chờ Windows cập nhật số liệu
                var after = MemoryCleaner.GetInfo();
                GraphUpdate();

                long freed = Math.Max(0, after.AvailMiB - before.AvailMiB);
                MessageBox.Show(
                    $"RAM từ {before.UsedPercent}% giảm xuống {after.UsedPercent}% (giải phóng ~{freed:N0} MB)\n{detail}",
                    "OGMode - Dọn RAM", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppState.LogError(ex.ToString());
                MessageBox.Show("Không dọn được RAM: " + ex.Message, "OGMode", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { button1.Enabled = true; }
        }

        private static void MakeCircularButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Size = new Size(80, 80);
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, button.Width, button.Height);
            button.Region = new Region(path);
        }

        private void Panel1_Paint(object? sender, PaintEventArgs e)
        {
            ControlPaint.DrawBorder(e.Graphics, panel1.ClientRectangle, Color.FromArgb(70, 77, 111), ButtonBorderStyle.Solid);
        }

        private void Formtools_Paint(object? sender, PaintEventArgs e)
        {
            using var borderPen = new Pen(Color.FromArgb(70, 77, 111), 2);
            var old = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawEllipse(borderPen, 1, 1, button1.Width - 3, button1.Height - 3);
            e.Graphics.SmoothingMode = old;
        }

        // ---------------- Tối ưu nhanh ----------------
        private void BuildQuickTools()
        {
            var panel = new Panel
            {
                Location = new Point(panel1.Right + 16, panel1.Top),
                Size = new Size(420, panel1.Height),
                Name = "panelQuick",
            };
            panel.Paint += (s, e) => ControlPaint.DrawBorder(e.Graphics, panel.ClientRectangle, Color.FromArgb(70, 77, 111), ButtonBorderStyle.Solid);

            var title = new Label
            {
                AutoSize = true,
                Font = new Font("Arial", 12F, FontStyle.Bold),
                ForeColor = SystemColors.ButtonFace,
                Location = new Point(17, 13),
                Text = "Tối Ưu Nhanh",
            };

            btnDns = MakeToolButton("Làm mới DNS", 17, 48);
            btnExplorer = MakeToolButton("Khởi động lại Explorer", 17 + 195, 48);
            btnRestore = MakeToolButton("Khôi phục mặc định", 17, 88);
            btnRestore.Width = 195 * 2 - 10;

            lblQuickResult = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = SystemColors.ButtonFace,
                Location = new Point(17, 126),
                Size = new Size(385, 26),
                Text = "Khôi phục mặc định = hoàn tác mọi thay đổi Game Mode.",
            };

            btnDns.Click += (s, e) =>
            {
                lblQuickResult.Text = SystemTweaks.FlushDns() ? "✓ Đã làm mới bộ nhớ đệm DNS." : "✗ Không làm mới được DNS.";
            };

            btnExplorer.Click += async (s, e) =>
            {
                btnExplorer.Enabled = false;
                try
                {
                    string last = "";
                    await Task.Run(() =>
                    {
                        SystemTweaks.KillExplorer(m => last = m);
                        Thread.Sleep(500);
                        SystemTweaks.StartExplorer(m => last = m);
                    });
                    lblQuickResult.Text = last.Trim();
                }
                finally { btnExplorer.Enabled = true; }
            };

            btnRestore.Click += async (s, e) =>
            {
                btnRestore.Enabled = false;
                try
                {
                    int errors = 0;
                    await Task.Run(() => GameModeEngine.Disable(m => { if (m.Contains('✗')) errors++; }));
                    lblQuickResult.Text = errors == 0 ? "✓ Đã khôi phục toàn bộ về trạng thái ban đầu." : $"Đã khôi phục, {errors} mục lỗi (xem trang Game Mode).";
                }
                catch (Exception ex) { lblQuickResult.Text = "✗ " + ex.Message; }
                finally { btnRestore.Enabled = true; }
            };

            panel.Controls.AddRange(new Control[] { title, btnDns, btnExplorer, btnRestore, lblQuickResult });
            Controls.Add(panel);
        }

        private static Button MakeToolButton(string text, int x, int y)
        {
            var b = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(185, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(36, 42, 59),
                ForeColor = SystemColors.ButtonFace,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                UseVisualStyleBackColor = false,
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(0, 126, 249);
            b.FlatAppearance.BorderSize = 1;
            return b;
        }
    }
}
