using System.Drawing.Drawing2D;
using System.Security.Principal;
using ogmm.Core;

namespace ogmm
{
    public partial class Formhome : Form
    {
        private static readonly Color ColorOn = Color.FromArgb(127, 255, 200);
        private static readonly Color ColorOff = Color.FromArgb(255, 127, 127);

        private CheckBox checkXbox = null!;
        private bool busy;

        public Formhome()
        {
            InitializeComponent();
            BuildExtraControls();
        }

        /// <summary>Thêm tuỳ chọn "Dịch vụ Xbox" (mặc định tắt để không làm hỏng đăng nhập Xbox/Game Pass).</summary>
        private void BuildExtraControls()
        {
            checkXbox = new CheckBox
            {
                AutoSize = true,
                Font = checkBox1.Font,
                ForeColor = checkBox1.ForeColor,
                Location = new Point(checkBox1.Right + 20, checkBox1.Top),
                Name = "checkXbox",
                Text = "Dịch Vụ Xbox",
                UseVisualStyleBackColor = true,
            };
            panel2.Controls.Add(checkXbox);

            toolTip1.SetToolTip(checkexplorer, "Tắt Explorer để tiết kiệm thêm RAM. Sẽ tự mở lại khi tắt Game Mode.");
            toolTip1.SetToolTip(checkBox1, "Dừng các dịch vụ Bluetooth (tai nghe/tay cầm Bluetooth sẽ mất kết nối).");
            toolTip1.SetToolTip(checkXbox, "Dừng Xbox Live Auth/Game Save. Đừng bật nếu bạn chơi game Xbox/Game Pass.");
        }

        private void Formhome_Load(object sender, EventArgs e)
        {
            MakeCircularButton(btngamemode);

            var state = AppState.Load();
            ApplyButtonState(state.GameModeEnabled ? "Đang Bật" : "Bắt Đầu", state.GameModeEnabled ? ColorOn : ColorOff);

            Userwindows.Text = "Username: " + GetUsername();
            label1.Text = "OGMode [ Version v2.0.0 ]";
            outputgamemode.ScrollBars = ScrollBars.Vertical;
            outputgamemode.Multiline = true;
        }

        private void ApplyButtonState(string text, Color color)
        {
            btngamemode.Text = text;
            btngamemode.BackColor = color;
        }

        private static void MakeCircularButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Size = new Size(128, 128);

            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, button.Width, button.Height);
            button.Region = new Region(path);
        }

        private static string GetUsername()
        {
            string name = WindowsIdentity.GetCurrent().Name;
            int i = name.LastIndexOf('\\');
            return i >= 0 ? name[(i + 1)..] : name;
        }

        // ---------- Log an toàn đa luồng ----------
        private void Log(string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(() => Log(text)); } catch (InvalidOperationException) { }
                return;
            }
            outputgamemode.AppendText(text + Environment.NewLine);
        }

        private async void btngamemode_Click(object sender, EventArgs e)
        {
            if (busy) return;
            busy = true;
            btngamemode.Enabled = false;
            outputgamemode.Text = string.Empty;

            try
            {
                bool wasOn = AppState.Load().GameModeEnabled;

                if (!wasOn)
                {
                    var opt = new GameModeOptions
                    {
                        Bluetooth = checkBox1.Checked,
                        Xbox = checkXbox.Checked,
                        KillExplorer = checkexplorer.Checked,
                    };
                    ApplyButtonState("Đang xử lý...", Color.Gray);
                    await Task.Run(() => GameModeEngine.Enable(opt, Log));
                    Log("");
                    Log("GAME MODE ĐÃ ĐƯỢC BẬT");
                    ApplyButtonState("Đang Bật", ColorOn);
                }
                else
                {
                    ApplyButtonState("Đang xử lý...", Color.Gray);
                    await Task.Run(() => GameModeEngine.Disable(Log));
                    Log("");
                    Log("GAME MODE ĐÃ ĐƯỢC TẮT");
                    ApplyButtonState("Đã Tắt", ColorOff);
                }
            }
            catch (Exception ex)
            {
                AppState.LogError(ex.ToString());
                Log("Lỗi: " + ex.Message);
                var st = AppState.Load();
                ApplyButtonState(st.GameModeEnabled ? "Đang Bật" : "Bắt Đầu", st.GameModeEnabled ? ColorOn : ColorOff);
            }
            finally
            {
                btngamemode.Enabled = true;
                busy = false;
            }
        }

        // Các handler checkbox vẫn được Designer nối sự kiện; giá trị được đọc trực tiếp lúc bấm nút nên không cần xử lý thêm.
        private void checkexplorer_CheckedChanged(object sender, EventArgs e) { }
        private void checkBox1_CheckedChanged(object sender, EventArgs e) { }
    }
}
