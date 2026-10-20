using ogmm.Core;

namespace ogmm
{
    /// <summary>Trang "Dọn Rác": quét -> chọn mục -> dọn. Giao diện dựng bằng code, cùng tông màu với các form khác.</summary>
    public class Formclean : Form
    {
        private readonly IReadOnlyList<JunkCategory> categories = JunkCleaner.BuildCategories();
        private readonly Dictionary<string, ScanResult> scanResults = new();

        private readonly ListView list = new();
        private readonly Button btnScan = new(), btnClean = new(), btnStop = new();
        private readonly ProgressBar progress = new();
        private readonly Label lblStatus = new();
        private CancellationTokenSource? cts;
        private bool busy;

        private static readonly Color Bg = Color.FromArgb(46, 51, 73);
        private static readonly Color PanelBg = Color.FromArgb(36, 42, 59);
        private static readonly Color HeaderBg = Color.FromArgb(24, 30, 54);
        private static readonly Color Accent = Color.FromArgb(0, 126, 249);

        public Formclean()
        {
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Bg;
            ClientSize = new Size(749, 434);
            FormBorderStyle = FormBorderStyle.None;
            Name = "Formclean";
            Text = "Formclean";
            Font = new Font("Segoe UI", 9F);

            var title = new Label
            {
                AutoSize = true,
                Font = new Font("Arial", 12F, FontStyle.Bold),
                ForeColor = SystemColors.ButtonFace,
                Location = new Point(24, 14),
                Text = "Dọn Rác Hệ Thống",
            };
            var hint = new Label
            {
                AutoSize = true,
                ForeColor = SystemColors.ControlDark,
                Location = new Point(24, 40),
                Text = "Tích chọn mục muốn dọn rồi bấm \"Dọn dẹp\". Rê chuột vào từng mục để xem ghi chú.",
            };

            // Hàng tiêu đề tự vẽ (ListView để HeaderStyle.None cho khỏi bị tiêu đề trắng chói)
            var header = new System.Windows.Forms.Panel { Location = new Point(24, 66), Size = new Size(701, 24), BackColor = HeaderBg };
            header.Controls.Add(HeaderLabel("Mục", 28));
            header.Controls.Add(HeaderLabel("Dung lượng", 28 + 420));
            header.Controls.Add(HeaderLabel("Số tệp", 28 + 420 + 120));

            list.Location = new Point(24, 90);
            list.Size = new Size(701, 205);
            list.View = View.Details;
            list.HeaderStyle = ColumnHeaderStyle.None;
            list.CheckBoxes = true;
            list.FullRowSelect = true;
            list.MultiSelect = false;
            list.HideSelection = false;
            list.BorderStyle = BorderStyle.None;
            list.BackColor = PanelBg;
            list.ForeColor = SystemColors.ButtonFace;
            list.ShowItemToolTips = true;
            list.Columns.Add("Mục", 448);
            list.Columns.Add("Dung lượng", 120);
            list.Columns.Add("Số tệp", 110);
            foreach (var c in categories)
            {
                var item = new ListViewItem(c.Name) { Checked = c.DefaultChecked, Tag = c, ToolTipText = c.Note };
                item.SubItems.Add("—");
                item.SubItems.Add("—");
                list.Items.Add(item);
            }
            list.ItemChecked += (s, e) => UpdateTotal();

            StyleButton(btnScan, "Quét", 24, Accent);
            StyleButton(btnClean, "Dọn dẹp", 24 + 130, Color.FromArgb(127, 255, 200), dark: true);
            StyleButton(btnStop, "Dừng", 24 + 260, Color.FromArgb(255, 127, 127), dark: true);
            btnStop.Enabled = false;
            btnScan.Click += async (s, e) => await RunScanAsync();
            btnClean.Click += async (s, e) => await RunCleanAsync();
            btnStop.Click += (s, e) => cts?.Cancel();

            progress.Location = new Point(24, 350);
            progress.Size = new Size(701, 8);
            progress.Style = ProgressBarStyle.Continuous;

            lblStatus.Location = new Point(24, 364);
            lblStatus.Size = new Size(701, 56);
            lblStatus.ForeColor = SystemColors.ButtonFace;
            lblStatus.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            lblStatus.Text = "Đang chuẩn bị quét...";

            Controls.AddRange(new Control[] { title, hint, header, list, btnScan, btnClean, btnStop, progress, lblStatus });

            Shown += async (s, e) => await RunScanAsync();
            FormClosing += (s, e) => cts?.Cancel();
        }

        private static Label HeaderLabel(string text, int x) => new()
        {
            AutoSize = true,
            ForeColor = SystemColors.ButtonFace,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            Location = new Point(x, 4),
            Text = text,
        };

        private void StyleButton(Button b, string text, int x, Color color, bool dark = false)
        {
            b.Text = text;
            b.Location = new Point(x, 306);
            b.Size = new Size(120, 34);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = color;
            b.ForeColor = dark ? Color.FromArgb(24, 30, 54) : Color.White;
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold);
            b.UseVisualStyleBackColor = false;
        }

        private IEnumerable<ListViewItem> Items => list.Items.Cast<ListViewItem>();

        private void SetBusy(bool value)
        {
            busy = value;
            btnScan.Enabled = !value;
            btnClean.Enabled = !value;
            btnStop.Enabled = value;
            list.Enabled = !value;
            progress.Style = value ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            if (!value) progress.Value = 0;
        }

        private void UpdateTotal()
        {
            if (busy) return;
            long total = 0;
            foreach (var it in Items)
                if (it.Checked && it.Tag is JunkCategory c && scanResults.TryGetValue(c.Id, out var r)) total += r.Bytes;
            if (scanResults.Count > 0)
                lblStatus.Text = $"Có thể giải phóng: {JunkCleaner.FormatSize(total)} (các mục đã chọn).";
        }

        // ---------------- Quét ----------------
        private async Task RunScanAsync(bool keepStatus = false)
        {
            if (busy) return;
            SetBusy(true);
            cts = new CancellationTokenSource();
            var ct = cts.Token;
            if (!keepStatus) lblStatus.Text = "Đang quét, vui lòng đợi...";

            try
            {
                foreach (var item in Items)
                {
                    var c = (JunkCategory)item.Tag!;
                    item.SubItems[1].Text = "đang quét...";
                    var r = await Task.Run(() => JunkCleaner.Scan(c, ct), ct);
                    if (IsDisposed) return;
                    scanResults[c.Id] = r;
                    item.SubItems[1].Text = JunkCleaner.FormatSize(r.Bytes);
                    item.SubItems[2].Text = r.Files.ToString("N0");
                }
                SetBusy(false);
                if (!keepStatus) UpdateTotal();
            }
            catch (OperationCanceledException)
            {
                if (IsDisposed) return;
                SetBusy(false);
                lblStatus.Text = "Đã dừng quét.";
            }
            catch (Exception ex)
            {
                AppState.LogError(ex.ToString());
                if (IsDisposed) return;
                SetBusy(false);
                lblStatus.Text = "Lỗi khi quét: " + ex.Message;
            }
        }

        // ---------------- Dọn ----------------
        private async Task RunCleanAsync()
        {
            if (busy) return;
            var selected = Items.Where(i => i.Checked).Select(i => (JunkCategory)i.Tag!).ToList();
            if (selected.Count == 0)
            {
                lblStatus.Text = "Hãy tích chọn ít nhất một mục.";
                return;
            }

            var risky = selected.Where(c => c.NeedsConfirm).Select(c => c.Name).ToList();
            if (risky.Count > 0)
            {
                var ans = MessageBox.Show(
                    "Mục sau sẽ bị xoá vĩnh viễn, không thể hoàn tác:\n\n• " + string.Join("\n• ", risky) + "\n\nTiếp tục?",
                    "OGMode - Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (ans != DialogResult.Yes) return;
            }

            SetBusy(true);
            cts = new CancellationTokenSource();
            var ct = cts.Token;

            long freed = 0; int deleted = 0, failed = 0;
            var notes = new List<string>();
            try
            {
                foreach (var c in selected)
                {
                    lblStatus.Text = $"Đang dọn: {c.Name}...";
                    var r = await Task.Run(() => JunkCleaner.Clean(c, _ => { }, ct), ct);
                    if (IsDisposed) return;
                    freed += r.FreedBytes; deleted += r.Deleted; failed += r.Failed;
                    if (r.Blocked) notes.Add($"{c.Name}: {r.BlockedReason}");
                }
                SetBusy(false);
                string msg = $"✓ Đã dọn {JunkCleaner.FormatSize(freed)} ({deleted:N0} tệp)." +
                             (failed > 0 ? $" {failed:N0} tệp đang được sử dụng nên bỏ qua." : "");
                if (notes.Count > 0) msg += "\n" + string.Join("\n", notes);
                lblStatus.Text = msg;
                await RunScanAsync(keepStatus: true); // cập nhật lại số liệu
            }
            catch (OperationCanceledException)
            {
                if (IsDisposed) return;
                SetBusy(false);
                lblStatus.Text = $"Đã dừng. Đã dọn {JunkCleaner.FormatSize(freed)} trước khi dừng.";
                await RunScanAsync(keepStatus: true);
            }
            catch (Exception ex)
            {
                AppState.LogError(ex.ToString());
                if (IsDisposed) return;
                SetBusy(false);
                lblStatus.Text = "Lỗi khi dọn: " + ex.Message;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) cts?.Dispose();
            base.Dispose(disposing);
        }
    }
}
