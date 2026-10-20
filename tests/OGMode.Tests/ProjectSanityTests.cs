using System.Reflection;
using System.Runtime.Versioning;

namespace OGMode.Tests
{
    /// <summary>Chốt chặn: không để dự án vô tình quay về .NET 8 hoặc lệch phiên bản.</summary>
    public class ProjectSanityTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ogmm.csproj"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy ogmm.csproj");
        }

        [Fact]
        public void Runtime_IsDotNet10()
        {
            Assert.Equal(10, Environment.Version.Major);
        }

        [Fact]
        public void AppAssembly_TargetsNet10Windows()
        {
            var asm = typeof(ogmm.Core.AppState).Assembly;
            var tfm = asm.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
            Assert.NotNull(tfm);
            Assert.Contains("Version=v10.0", tfm);
        }

        [Fact]
        public void AppAssembly_NameAndVersion()
        {
            var name = typeof(ogmm.Core.AppState).Assembly.GetName();
            Assert.Equal("ogmm", name.Name);
            Assert.Equal(2, name.Version!.Major);
        }

        [Fact]
        public void Csproj_TargetsNet10_AndNothingOlder()
        {
            string root = RepoRoot();
            var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.Contains(@"\bin\") && !f.Contains(@"\obj\"))
                .Where(f => f.EndsWith(".csproj") || f.EndsWith(".pubxml") || f.EndsWith(".yml"));

            foreach (var f in files)
            {
                string text = File.ReadAllText(f);
                Assert.DoesNotContain("net8", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("net6", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("dotnet-version: 8", text, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Contains("net10.0-windows", File.ReadAllText(Path.Combine(root, "ogmm.csproj")));
        }

        [Fact]
        public void Manifest_RequiresAdministrator()
        {
            string manifest = File.ReadAllText(Path.Combine(RepoRoot(), "app.manifest"));
            Assert.Contains("requireAdministrator", manifest);
        }
    }
}
