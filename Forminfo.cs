using System.Diagnostics;

namespace ogmm
{
    public partial class Forminfo : Form
    {
        public Forminfo()
        {
            InitializeComponent();
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            OpenUrl("https://www.facebook.com/SiroCandy06/");
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không mở được liên kết:\n" + url + "\n\n" + ex.Message,
                    "OGMode", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            OpenUrl("https://discord.gg/NUhDfy8hXa");
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            OpenUrl("https://www.youtube.com/@osteup");
        }

        private void pictureBox4_Click(object sender, EventArgs e)
        {
            OpenUrl("https://osteup.com/");
        }

    }
}
