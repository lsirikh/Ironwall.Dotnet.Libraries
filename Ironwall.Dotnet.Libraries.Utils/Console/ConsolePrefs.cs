using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>위 · 아래로 나눈 화면의 위 몫(0…1 · 결선 창 3D 보기 : 개념도). 없으면 콘솔 기본값.</summary>
    /// <remarks>값이 없으면 쓰지 않는다 — 옛 형식과 바이트가 같게(이 필드를 모르는 빌드와 섞여도 파일이 흔들리지 않는다).</remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? SplitRatio { get; set; }

    /// <summary>오른쪽 상세(속성) 칸을 접어 두었는가.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool DetailCollapsed { get; set; }

    /// <summary>거리(m) 표시를 켜 두었는가(결선 창 [거리 표시]). 끄면 쓰지 않는다.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ShowDistances { get; set; }

    /// <summary>
    /// 이 빌드가 모르는 키(다른 빌드가 넣은 필드 · 아직 여기 선언되지 않은 미래 필드).
    /// <b>지워지지 않고 그대로 들고 있다가 그대로 되돌려 쓴다</b>(D-08 — 이게 없으면
    /// 알 수 없는 키가 다음 <see cref="ConsolePrefs.Save"/> 에서 영구 소실된다).
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>
/// 콘솔의 표시 설정(상세 폭 · 열 · 마지막 레일)을 콘솔 키별로 기억한다 — 이 PC 의 이 사용자 한정.
/// </summary>
/// <remarks>
/// <para><b><c>appsettings.json</c> 에 넣지 않는다.</b> 그 파일은 실행 중 비원자적 쓰기로 깨진 전례가 있다.
/// 전용 파일에, <b>임시 파일에 쓰고 교체</b>한다 — 쓰다 죽어도 옛 파일이 남는다.</para>
/// <para>파일이 깨졌으면 조용히 기본값으로 시작한다(표시 설정일 뿐이다). 스레드: 호출은 UI 스레드에서만.</para>
/// <para><b>저장은 load-merge-write.</b> 콘솔마다 별도 <see cref="ConsolePrefs"/> 인스턴스를 들고 있어
/// 서로의 저장 시점을 모른다 — <see cref="Save"/> 는 메모리 스냅샷을 그대로 덮어쓰지 않고, 저장 직전
/// 디스크를 다시 읽어 <b>이 인스턴스가 <see cref="Get"/> 으로 실제 건드린 키만</b> 얹는다. 건드리지 않은
/// 키(다른 콘솔이 그 사이 저장한 값 포함)는 디스크의 최신 값을 그대로 보존한다.</para>
/// </remarks>
public sealed class ConsolePrefs
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private readonly string _path;
    private Dictionary<string, ConsolePrefEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>이 인스턴스가 <see cref="Get"/> 으로 실제 건드린 콘솔 키 — <see cref="Save"/> 의 병합 범위.</summary>
    private readonly HashSet<string> _touchedKeys = new(StringComparer.Ordinal);

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

    /// <summary>마지막 저장이 실패했다면 그 사유(진단용). 성공하면 null.</summary>
    public string? LastSaveError { get; private set; }

    public ConsolePrefEntry Get(string consoleKey)
    {
        _touchedKeys.Add(consoleKey);
        if (!_entries.TryGetValue(consoleKey, out var entry))
            _entries[consoleKey] = entry = new ConsolePrefEntry();
        entry.DetailWidth = ConsoleLayoutMath.ClampDetailWidth(entry.DetailWidth);
        entry.HiddenColumns ??= new List<string>();
        return entry;
    }

    /// <summary>
    /// 저장한다. 실패하면 false — 표시 설정 때문에 창이 죽어서는 안 된다.
    /// </summary>
    /// <remarks>
    /// load-merge-write: 저장 직전 디스크를 다시 읽고, 이 인스턴스가 <see cref="Get"/> 으로 건드린 키만
    /// 그 위에 얹어 쓴다. 다른 <see cref="ConsolePrefs"/> 인스턴스(다른 콘솔 · 다른 프로세스)가 그 사이
    /// 저장해 놓은, 이 인스턴스가 모르는 키는 그대로 보존된다 — 메모리 스냅샷을 통째로 덮어쓰지 않는다.
    /// </remarks>
    public bool Save()
    {
        // 임시 이름은 프로세스마다 다르게 — 앱이 둘 떠 있으면(개발본 + 배포본) 같은 .tmp 를 두고 다툰다.
        var temp = $"{_path}.{Environment.ProcessId}.tmp";
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var merged = ReadFromDisk();
            foreach (var key in _touchedKeys)
            {
                if (_entries.TryGetValue(key, out var entry)) merged[key] = entry;
            }

            File.WriteAllText(temp, JsonSerializer.Serialize(merged, Json));
            File.Move(temp, _path, overwrite: true);          // 같은 볼륨 안의 교체 — 중간 상태가 보이지 않는다
            _entries = merged;
            LastSaveError = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Security.SecurityException)
        {
            LastSaveError = ex.Message;
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
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Security.SecurityException)
        {
            WasCorrupt = true;
            _entries = new Dictionary<string, ConsolePrefEntry>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// 디스크의 현재 내용을 읽는다. 파일이 없거나 깨졌으면 빈 사전을 돌려준다(예외를 던지지 않는다) —
    /// <see cref="Save"/> 의 병합 기준이므로, 저장이 읽기 실패 때문에 죽어서는 안 된다.
    /// </summary>
    private Dictionary<string, ConsolePrefEntry> ReadFromDisk()
    {
        try
        {
            if (!File.Exists(_path)) return new Dictionary<string, ConsolePrefEntry>(StringComparer.Ordinal);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, ConsolePrefEntry>>(File.ReadAllText(_path));
            return loaded != null
                ? new Dictionary<string, ConsolePrefEntry>(loaded.Where(p => p.Value != null), StringComparer.Ordinal)
                : new Dictionary<string, ConsolePrefEntry>(StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.Security.SecurityException)
        {
            return new Dictionary<string, ConsolePrefEntry>(StringComparer.Ordinal);
        }
    }
}
