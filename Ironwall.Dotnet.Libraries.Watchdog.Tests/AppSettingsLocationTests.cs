using Ironwall.Dotnet.Libraries.Base.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Watchdog.Tests;

/// <summary>
/// 설정 파일 위치(Config\ 정본 · 옛 루트 폴백 · 처음 쓸 때 이관) — <see cref="AppSettingsLocation"/>.
/// </summary>
public class AppSettingsLocationTests : IDisposable
{
    private const string Name = AppSettingsLocation.AppSettingsFileName;
    private readonly string _base = Path.Combine(Path.GetTempPath(), "AppSettingsLocationTests_" + Guid.NewGuid().ToString("N"));
    private string Config => Path.Combine(_base, "Config", Name);
    private string Legacy => Path.Combine(_base, Name);

    public AppSettingsLocationTests() => Directory.CreateDirectory(_base);
    public void Dispose() { try { Directory.Delete(_base, recursive: true); } catch (IOException) { } }

    [Fact]
    public void should_read_config_file_when_only_new_location_exists()
    {
        Assert.Equal(Config, AppSettingsLocation.ResolveReadPath(_base, Name, p => p == Config));
    }

    [Fact]
    public void should_read_legacy_root_file_when_only_old_location_exists()
    {
        Assert.Equal(Legacy, AppSettingsLocation.ResolveReadPath(_base, Name, p => p == Legacy));
    }

    [Fact]
    public void should_prefer_config_file_when_both_exist()
    {
        Assert.Equal(Config, AppSettingsLocation.ResolveReadPath(_base, Name, _ => true));
    }

    [Fact]
    public void should_point_to_config_file_when_neither_exists()
    {
        Assert.Equal(Config, AppSettingsLocation.ResolveReadPath(_base, Name, _ => false));
    }

    [Fact]
    public void should_copy_legacy_file_into_config_on_first_write()
    {
        File.WriteAllText(Legacy, "{\"old\":1}");

        var path = AppSettingsLocation.PrepareWrite(_base, Name);

        Assert.Equal(Config, path);
        Assert.Equal("{\"old\":1}", File.ReadAllText(Config));
        Assert.True(File.Exists(Legacy));   // 옛 파일은 지우지 않는다
        Assert.Equal(Config, AppSettingsLocation.ResolveReadPath(_base, Name));
    }

    [Fact]
    public void should_keep_existing_config_file_when_both_exist_on_write()
    {
        File.WriteAllText(Legacy, "{\"old\":1}");
        Directory.CreateDirectory(Path.GetDirectoryName(Config)!);
        File.WriteAllText(Config, "{\"new\":2}");

        Assert.Equal(Config, AppSettingsLocation.PrepareWrite(_base, Name));
        Assert.Equal("{\"new\":2}", File.ReadAllText(Config));
    }

    [Fact]
    public void should_create_config_folder_without_file_when_neither_exists_on_write()
    {
        Assert.Equal(Config, AppSettingsLocation.PrepareWrite(_base, Name));
        Assert.True(Directory.Exists(Path.GetDirectoryName(Config)));
        Assert.False(File.Exists(Config));
    }

    [Fact]
    public void should_place_login_prefs_in_same_config_folder()
    {
        Assert.Equal(Path.Combine(_base, "Config", "login-prefs.json"),
            AppSettingsLocation.GetConfigPath(_base, AppSettingsLocation.LoginPrefsFileName));
    }
}
