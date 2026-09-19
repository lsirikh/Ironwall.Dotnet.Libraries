using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.IO;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// console-kernel FR-03 · FR-07 — 콘솔 표시 설정의 로컬 기억. <c>appsettings.json</c> 밖 전용 파일 · 임시 파일에 쓰고 교체 ·
/// 깨진 파일은 기본값으로 복구.
/// </summary>
public sealed class ConsolePrefsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ironwall-console-prefs-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "console-prefs.json");

    public void Dispose()
    {
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { /* 임시 폴더 — 남아도 무해 */ }
    }

    [Fact]
    public void should_start_with_defaults_when_file_is_missing()
    {
        var prefs = new ConsolePrefs(FilePath);

        var entry = prefs.Get("Devices");

        Assert.False(prefs.WasCorrupt);
        Assert.Equal(340, entry.DetailWidth);
        Assert.False(entry.ShowAllColumns);
        Assert.Empty(entry.HiddenColumns);
    }

    [Fact]
    public void should_round_trip_per_console_key()
    {
        var prefs = new ConsolePrefs(FilePath);
        prefs.Get("Devices").DetailWidth = 420;
        prefs.Get("Devices").HiddenColumns.Add("unit");
        prefs.Get("Devices").LastRailKey = "camera";
        prefs.Get("Accounts").ShowAllColumns = true;

        Assert.True(prefs.Save());
        var reloaded = new ConsolePrefs(FilePath);

        Assert.Equal(420, reloaded.Get("Devices").DetailWidth);
        Assert.Equal(new[] { "unit" }, reloaded.Get("Devices").HiddenColumns);
        Assert.Equal("camera", reloaded.Get("Devices").LastRailKey);
        Assert.True(reloaded.Get("Accounts").ShowAllColumns);
        Assert.Equal(340, reloaded.Get("Accounts").DetailWidth);
    }

    [Fact]
    public void should_clamp_stored_width_when_out_of_range()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{ \"Devices\": { \"DetailWidth\": 9000 } }");

        Assert.Equal(480, new ConsolePrefs(FilePath).Get("Devices").DetailWidth);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("[1,2,3]")]
    public void should_fall_back_to_defaults_when_file_is_corrupt(string content)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, content);

        var prefs = new ConsolePrefs(FilePath);

        Assert.True(prefs.WasCorrupt);
        Assert.Equal(340, prefs.Get("Devices").DetailWidth);
        Assert.True(prefs.Save());                                  // 다음 저장이 깨진 파일을 덮어 고친다
        Assert.False(new ConsolePrefs(FilePath).WasCorrupt);
    }

    [Fact]
    public void should_leave_no_temp_file_behind_after_save()
    {
        var prefs = new ConsolePrefs(FilePath);
        prefs.Get("Devices").DetailWidth = 300;

        prefs.Save();

        Assert.True(File.Exists(FilePath));
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void should_report_failure_instead_of_throwing_when_path_is_unwritable()
    {
        Directory.CreateDirectory(_directory);
        var blocked = Path.Combine(_directory, "as-directory");
        Directory.CreateDirectory(blocked);                          // 파일 자리에 폴더가 있다 → 교체 실패

        var prefs = new ConsolePrefs(blocked);

        Assert.False(prefs.Save());
    }

    [Fact]
    public void should_keep_prefs_outside_appsettings()
    {
        Assert.DoesNotContain("appsettings", ConsolePrefs.DefaultPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("console-prefs.json", ConsolePrefs.DefaultPath);
    }
}
