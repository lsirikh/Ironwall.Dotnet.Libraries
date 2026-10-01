using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System.IO;
using System.Text.Json;
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
    public void should_round_trip_the_split_ratio_and_collapsed_detail_and_leave_them_out_when_unset()
    {
        // Arrange — 결선 창(3D 보기 : 개념도 나눔 · 속성 칸 접기)만 쓰고, 다른 콘솔은 손대지 않는다
        var prefs = new ConsolePrefs(FilePath);
        prefs.Get("devices.wiring").SplitRatio = 0.7;
        prefs.Get("devices.wiring").DetailCollapsed = true;
        prefs.Get("devices.wiring").ShowDistances = true;
        prefs.Get("Devices").DetailWidth = 400;

        // Act
        Assert.True(prefs.Save());
        var reloaded = new ConsolePrefs(FilePath);
        var json = File.ReadAllText(FilePath);

        // Assert
        Assert.Equal(0.7, reloaded.Get("devices.wiring").SplitRatio);
        Assert.True(reloaded.Get("devices.wiring").DetailCollapsed);
        Assert.True(reloaded.Get("devices.wiring").ShowDistances);
        Assert.False(reloaded.Get("Devices").ShowDistances);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(json, "ShowDistances").Count);
        Assert.Null(reloaded.Get("Devices").SplitRatio);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(json, "SplitRatio").Count);       // 값이 없는 콘솔은 쓰지 않는다
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(json, "DetailCollapsed").Count);
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

    // D-08 — 미래 필드(이 빌드가 모르는 키)가 load → save 왕복에서 지워지지 않는다.
    [Fact]
    public void should_keep_unknown_key_when_entry_is_loaded_and_saved()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{ \"Devices\": { \"DetailWidth\": 400, \"WindowX\": 999, \"WindowY\": 123 } }");

        var prefs = new ConsolePrefs(FilePath);
        prefs.Get("Devices").ShowAllColumns = true;           // 알려진 필드만 건드린다 — WindowX/WindowY 는 이 빌드가 모른다
        Assert.True(prefs.Save());

        var raw = File.ReadAllText(FilePath);
        Assert.Contains("WindowX", raw);
        Assert.Contains("999", raw);
        Assert.Contains("WindowY", raw);
        Assert.Contains("123", raw);

        // 다음 빌드(WindowX/Y 를 아는 빌드)가 읽으면 그 값이 여전히 거기 있어야 한다 — JsonElement 로 왕복 확인.
        using var doc = JsonDocument.Parse(raw);
        var devices = doc.RootElement.GetProperty("Devices");
        Assert.Equal(999, devices.GetProperty("WindowX").GetInt32());
        Assert.Equal(123, devices.GetProperty("WindowY").GetInt32());
        Assert.True(devices.GetProperty("ShowAllColumns").GetBoolean());
    }

    // D-08 — 알려진 4개 필드만 있는 엔트리는 확장 데이터 추가 전과 바이트 단위로 같은 포맷을 낸다(골든 테스트).
    [Fact]
    public void should_serialize_known_fields_byte_identically_to_previous_format()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var entries = new Dictionary<string, ConsolePrefEntry>(StringComparer.Ordinal)
        {
            ["Devices"] = new ConsolePrefEntry
            {
                DetailWidth = 420,
                ShowAllColumns = true,
                HiddenColumns = new List<string> { "unit", "type" },
                LastRailKey = "camera",
            },
        };

        var json = JsonSerializer.Serialize(entries, options);

        const string expected = "{\r\n  \"Devices\": {\r\n    \"DetailWidth\": 420,\r\n    \"ShowAllColumns\": true,\r\n    \"HiddenColumns\": [\r\n      \"unit\",\r\n      \"type\"\r\n    ],\r\n    \"LastRailKey\": \"camera\"\r\n  }\r\n}";
        Assert.Equal(expected, json);
        Assert.DoesNotContain("Extra", json);                  // 확장 데이터 프로퍼티 자체는 출력에 안 나온다(비어 있을 때)
    }

    // 콘솔 두 개가 별도 인스턴스로 같은 파일을 쓸 때, 나중 저장이 먼저 저장을 지우지 않는다.
    [Fact]
    public void should_not_lose_other_writer_key_when_saving_concurrently()
    {
        var consoleA = new ConsolePrefs(FilePath);
        var consoleB = new ConsolePrefs(FilePath);             // consoleA 가 저장하기 전, 같은(빈) 상태에서 출발

        consoleA.Get("Devices").DetailWidth = 420;
        Assert.True(consoleA.Save());

        consoleB.Get("Accounts").ShowAllColumns = true;        // consoleB 는 "Devices" 를 건드린 적이 없다
        Assert.True(consoleB.Save());

        var reloaded = new ConsolePrefs(FilePath);
        Assert.Equal(420, reloaded.Get("Devices").DetailWidth);       // consoleA 의 저장이 살아남는다
        Assert.True(reloaded.Get("Accounts").ShowAllColumns);
    }

    // 파일이 깨져 있어도 Save() 가 예외를 던지지 않고 기본값으로 복구해 쓴다.
    [Fact]
    public void should_not_throw_when_saving_over_a_corrupt_file()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{ this is not json");

        var prefs = new ConsolePrefs(FilePath);
        Assert.True(prefs.WasCorrupt);

        var exception = Record.Exception(() => prefs.Get("Devices").DetailWidth = 300);
        Assert.Null(exception);

        Assert.True(prefs.Save());
        Assert.Equal(300, new ConsolePrefs(FilePath).Get("Devices").DetailWidth);
    }
}
