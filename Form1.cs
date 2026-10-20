using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ogmm
{
    public partial class Form1 : Form
    {
        // ----- Kéo cửa sổ bằng thanh tiêu đề tự vẽ -----
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        // ----- Bo góc cửa sổ -----
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);
        [DllImport("Gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private static readonly Color NavNormal = Color.FromArgb(24, 30, 54);
        private static readonly Color NavActive = Color.FromArgb(46, 51, 73);

        private Form? CurrentFormChild;
        private Button? IDnamesave;

        public Form1()
        {
            InitializeComponent();

            IntPtr rgn = CreateRoundRectRgn(0, 0, Width, Height, 25, 25);
            Region = Region.FromHrgn(rgn);
            DeleteObject(rgn); // Region.FromHrgn đã sao chép, giải phóng handle gốc để không rò rỉ GDI

            pnlnav.Height = btnhome.Height;
            pnlnav.Top = btnhome.Top;
            pnlnav.Left = btnhome.Left;
            btnhome.BackColor = NavActive;

            panel_move.MouseDown += PanelMove_MouseDown;
            label4.Text = "OGMode v" + (typeof(Form1).Assembly.GetName().Version?.ToString(3) ?? "2.0.0");
        }

        // ---------- Quyền Administrator ----------
        private static bool IsRunAsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // app.manifest đã yêu cầu quyền Admin; đây chỉ là lớp bảo vệ phụ (vd. khi chạy trong trình thiết kế).
            if (!IsRunAsAdmin())
            {
                MessageBox.Show("Hãy chạy OGMode với quyền Administrator để dùng đầy đủ chức năng.",
                    "Yêu cầu quyền Admin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }
            usernamelog.Text = Environment.UserName;
        }

        // ---------- Thanh tiêu đề ----------
        private void PanelMove_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }

        private void btnminimize_Click(object sender, EventArgs e) => WindowState = FormWindowState.Minimized;

        private void btnclose_Click(object sender, EventArgs e) => Close();

        // Cho phép thu nhỏ xuống taskbar dù form không có viền
        private const int WS_MINIMIZEBOX = 0x20000;
        private const int CS_DBLCLKS = 0x8;
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= WS_MINIMIZEBOX;
                cp.ClassStyle |= CS_DBLCLKS;
                return cp;
            }
        }

        // ---------- Điều hướng ----------
        private void SelectNav(Button b)
        {
            foreach (var n in new[] { btnhome, btngamemode, bnttool, bntcontact, button4 })
                n.BackColor = NavNormal;

            pnlnav.Height = b.Height;
            pnlnav.Top = b.Top;
            pnlnav.Left = b.Left;
            b.BackColor = NavActive;
            activebordersolid(b);
        }

        private void btnhome_Click(object sender, EventArgs e)
        {
            SelectNav(btnhome);
            CloseCurrentChild();   // bản cũ gọi CurrentFormChild.Close() khi còn null -> NullReferenceException
            title.Text = "Giới Thiệu";
        }

        private void btngamemode_Click(object sender, EventArgs e)
        {
            SelectNav(btngamemode);
            OpenChildForm(new Formhome());
            title.Text = "GameMode";
        }

        private void bnttool_Click(object sender, EventArgs e)
        {
            SelectNav(bnttool);
            OpenChildForm(new Formtools());
            title.Text = "Công Cụ";
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SelectNav(button4);
            OpenChildForm(new Formclean());
            title.Text = "Dọn Rác";
        }

        private void bntcontact_Click(object sender, EventArgs e)
        {
            SelectNav(bntcontact);
            OpenChildForm(new Forminfo());
            title.Text = "Liên hệ";
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            CloseCurrentChild();
            if (IDnamesave != null) IDnamesave.FlatAppearance.BorderSize = 0;
        }

        private void activebordersolid(Button b)
        {
            if (IDnamesave != null) IDnamesave.FlatAppearance.BorderSize = 0;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(0, 126, 249);
            b.FlatAppearance.BorderSize = 1;
            IDnamesave = b;
        }

        private void CloseCurrentChild()
        {
            if (CurrentFormChild == null) return;
            CurrentFormChild.Close();
            CurrentFormChild.Dispose();
            CurrentFormChild = null;
        }

        private void OpenChildForm(Form childForm)
        {
            CloseCurrentChild();
            CurrentFormChild = childForm;
            childForm.TopLevel = false;
            childForm.FormBorderStyle = FormBorderStyle.None;
            childForm.Dock = DockStyle.Fill;
            panel_body.Controls.Add(childForm);
            panel_body.Tag = childForm;
            childForm.BringToFront();
            childForm.Show();
        }

        // ---------- Hiệu ứng hiện dần khi mở ----------
        private void timer1_tick(object sender, EventArgs e)
        {
            Opacity = Math.Min(1.0, Opacity + 0.1);
            if (Opacity >= 1.0) timer1.Stop();
        }
    }
}
