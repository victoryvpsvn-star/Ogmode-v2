using System.Runtime.ExceptionServices;
using ogmm;

namespace OGMode.Tests
{
    /// <summary>
    /// Khởi tạo từng Form trên thread STA (không Show) để bắt lỗi InitializeComponent / resx / designer
    /// -- đúng loại lỗi từng gây crash ở trang Intro bản cũ.
    /// </summary>
    public class FormsSmokeTests
    {
        private static void RunSta(Action action)
        {
            ExceptionDispatchInfo? error = null;
            var t = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
            Assert.True(t.Join(TimeSpan.FromSeconds(60)), "Khởi tạo Form quá 60 giây (treo?)");
            error?.Throw();
        }

        [Theory]
        [InlineData("Form1")]
        [InlineData("Formhome")]
        [InlineData("Forminfo")]
        [InlineData("Formtools")]
        [InlineData("Formclean")]
        public void Form_CanBeCreatedAndDisposed(string name)
        {
            RunSta(() =>
            {
                using Form form = name switch
                {
                    "Form1" => new Form1(),
                    "Formhome" => new Formhome(),
                    "Forminfo" => new Forminfo(),
                    "Formtools" => new Formtools(),
                    "Formclean" => new Formclean(),
                    _ => throw new ArgumentOutOfRangeException(nameof(name)),
                };
                Assert.NotEmpty(form.Controls);
            });
        }
    }
}
