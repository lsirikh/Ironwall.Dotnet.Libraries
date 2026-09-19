using System.IO;
using System.Text.Json;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>콘솔 하나가 기억하는 것.</summary>
public sealed class ConsolePrefEntry
{
    public double DetailWidth { get; set; } = ConsoleLayoutMath.DetailDefault;

    /// <summary>"열" 메뉴에서 전체 열을 켰는가.</summary>
    public bool ShowAllColumns { get; set; }

    /// <summary>사용자가 끈 열(열 키).</summary>
    public List<string> HiddenColumns { get; set; } = new();

    /// <summary>마지막으로 본 레일 항목.</summary>
    public string? LastRailKey { get; set; }
}

/// <summary>
/// 콘솔의 표시 설정(상세 폭 · 열 · 마지막 레일)을 콘솔 키별로 기억한다 — 이 PC 의 이 사용자 한정.
/// </summary>
/// <remarks>
/// <para><b><c>appsettings.json</c> 에 넣지 않는다.</b> 그 파일은 실행 중 비원자적 쓰기로 깨진 전례가 있다.
/// 전용 파일에, <b>임시 파일에 쓰고 교체</b>한다 — 쓰다 죽어도 옛 파일이 남는다.</para>
/// <para>파일이 깨졌으면 조용히 기본값으로 시작한다(표시 설정일 뿐이다). 스레드: 호출은 UI 스레드에서만.</para>
/// </remarks>
public sealed class ConsolePrefs
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path;
    private Dictionary<string, ConsolePrefEntry> _entries = new(StringComparer.Ordinal);

    public ConsolePrefs(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        Load();
    }

    /// <summary>기본 위치 — <c>%LocalAppData%\Ironwall\console-prefs.json</c>.</summary>
    public static string DefaultPath
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ironwall", "console-prefs.json");

    /// <summary>파일을 읽다 실패했는가(진단용). 실패해도 기본값으로 동작한다.</summary>
    public bool WasCorrupt { get; private set; }

    public ConsolePrefEntry Get(string consoleKey)
    {
        if (!_entries.TryGetValue(consoleKey, out var entry))
            _entries[consoleKey] = entry = new ConsolePrefEntry();
        entry.DetailWidth = ConsoleLayoutMath.ClampDetailWidth(entry.DetailWidth);
        entry.HiddenColumns ??= new List<string>();
        return entry;
    }

    /// <summary>저장한다. 실패하면 false — 표시 설정 때문에 창이 죽어서는 안 된다.</summary>
    public bool Save()
    {
        var temp = _path + ".tmp";
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.WriteAllText(temp, JsonSerializer.Serialize(_entries, Json));
            File.Move(temp, _path, overwrite: true);          // 같은 볼륨 안의 교체 — 중간 상태가 보이지 않는다
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { /* 임시 파일은 다음 저장이 덮어쓴다 */ }
            return false;
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            var loaded = JsonSerializer.Deserialize<Dictionary<string, ConsolePrefEntry>>(File.ReadAllText(_path));
            if (loaded != null) _entries = new Dictionary<string, ConsolePrefEntry>(loaded.Where(p => p.Value != null), StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            WasCorrupt = true;
            _entries = new Dictionary<string, ConsolePrefEntry>(StringComparer.Ordinal);
        }
    }
}
