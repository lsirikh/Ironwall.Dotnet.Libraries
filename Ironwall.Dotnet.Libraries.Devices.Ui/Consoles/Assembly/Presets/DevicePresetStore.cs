using Ironwall.Dotnet.Libraries.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
/****************************************************************************
   Purpose      : 프리셋 파일 저장소 — 봉투 · 원자적 쓰기 · 깨진 파일 · 더 새 판 · 씨앗(FR-10).
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>저장소가 지금 어떤 처지인가.</summary>
public enum PresetStoreState
{
    /// <summary>읽었고 쓸 수 있다.</summary>
    Ready,

    /// <summary>파일이 더 새 판이다 — 읽기만 한다. 쓰면 새 판이 넣어 둔 칸을 지운다.</summary>
    ReadOnlyNewerSchema,

    /// <summary>깨진 파일을 <c>.corrupt-…</c> 로 옮기고 씨앗으로 다시 시작했다. 쓸 수 있다.</summary>
    RecoveredFromCorruption,

    /// <summary>파일을 아예 다루지 못한다(권한 · 잠김 …). 목록이 비고 쓰지 않는다.</summary>
    Unavailable,
}

/// <summary>한 번의 손질 결과. <paramref name="Message"/> 는 실패 사유 · 알릴 만한 성공 모두에 쓴다.</summary>
public sealed record PresetStoreResult(bool IsSuccess, string? Message);

/// <summary>
/// 조립 결과(프리셋)를 이 PC 에 보관한다 — <c>%LocalAppData%\Ironwall\device-assembly-presets.json</c>.
/// </summary>
/// <remarks>
/// <para><b>표시 설정 파일과 다르다.</b> <c>ConsolePrefs</c> 는 깨지면 조용히 기본값으로 시작해도 된다(폭 · 열 뿐이다).
/// 프리셋은 <b>사용자가 손으로 조립한 결과</b>라 조용히 사라지면 안 된다 — 그래서 깨진 파일을 덮지도 지우지도 않고
/// <c>.corrupt-&lt;시각&gt;</c> 로 <b>옮긴</b> 뒤 알린다(FR-10 · 리스크 표).</para>
/// <para><b>쓰기는 원자적이다</b> — 같은 폴더의 <c>.tmp-&lt;guid&gt;</c> 에 쓰고 flush 한 뒤 바꿔 끼운다.
/// 쓰다 죽어도 옛 파일이 그대로 남는다.</para>
/// <para><b>같은 경로를 보는 저장소가 둘이면 서로를 모른다</b> — 잠금을 걸지 않는다. 나중에 쓴 쪽이 이긴다.
/// 창이 하나씩만 뜨는 쓰임을 전제로 한다(조립기 · 프리셋 관리). 두 앱(개발본 + 배포본)이 같이 떠서
/// 양쪽에서 프리셋을 고치면 늦게 저장한 쪽만 남는다.</para>
/// <para><see cref="Load"/> 는 <b>따로 부른다</b> — 만드는 것만으로 파일을 건드리지 않는다(씨앗 쓰기가
/// 생성자에서 일어나면 미리보기 도구 · 테스트가 원치 않는 파일을 남긴다).</para>
/// <para>스레드: UI 스레드에서만 부른다.</para>
/// </remarks>
public sealed class DevicePresetStore
{
    /// <summary>이 코드가 아는 봉투 판.</summary>
    public const int CurrentSchema = 1;

    /// <summary>이름 길이 상한 — 목록 · 카드가 감당하는 길이.</summary>
    public const int MaxNameLength = 80;

    private const string CopySuffix = " 복사본";

    private readonly string _path;
    private readonly Func<DateTimeOffset> _clock;
    private readonly List<DevicePreset> _presets = new();
    private IReadOnlyList<DevicePreset> _snapshot = Array.Empty<DevicePreset>();

    /// <param name="path">프리셋 파일 경로. 테스트는 임시 폴더를 넣는다.</param>
    /// <param name="clock">지금 시각. 넣지 않으면 <see cref="DateTimeOffset.Now"/> — 로직 안에서 시계를 직접 읽지 않는다.</param>
    public DevicePresetStore(string path, Func<DateTimeOffset>? clock = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("프리셋 파일 경로가 비어 있다.", nameof(path));

        // 경로는 여기서 한 번만 펴 둔다 — 상대 경로 · ".." 가 섞인 채 돌아다니지 않게.
        _path = Path.GetFullPath(path);
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    /// <summary>기본 위치 — <c>%LocalAppData%\Ironwall\device-assembly-presets.json</c>.</summary>
    public static string DefaultPath
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Ironwall", "device-assembly-presets.json");

    /// <summary>이 저장소가 보는 파일(펴진 절대 경로).</summary>
    public string FilePath => _path;

    public PresetStoreState State { get; private set; } = PresetStoreState.Ready;

    /// <summary>사람에게 보일 한 줄. 알릴 것이 없으면 null.</summary>
    public string? StateMessage { get; private set; }

    /// <summary>더 새 판이거나 파일을 못 다루면 쓰지 않는다.</summary>
    public bool IsReadOnly
        => State is PresetStoreState.ReadOnlyNewerSchema or PresetStoreState.Unavailable;

    /// <summary>카테고리 차례 안에서 이름 차례(현재 문화권 · 대소문자 무시).</summary>
    public IReadOnlyList<DevicePreset> Presets => _snapshot;

    /// <summary>목록이 바뀌었다(성공한 손질 한 번에 한 번).</summary>
    public event EventHandler? Changed;

    public static string NewId() => Guid.NewGuid().ToString("N");

    #region Load

    /// <summary>
    /// 파일을 읽는다. <b>예외를 밖으로 내보내지 않는다</b> — 프리셋 파일 때문에 창이 죽어서는 안 된다.
    /// </summary>
    /// <remarks>
    /// 씨앗은 <b>파일이 없거나 깨졌을 때만</b> 심는다. 사용자가 지운 씨앗이 다음에 켤 때 되살아나면
    /// "지워도 소용없다"가 된다.
    /// </remarks>
    public void Load()
    {
        _presets.Clear();
        State = PresetStoreState.Ready;
        StateMessage = null;

        try
        {
            if (!File.Exists(_path))
            {
                SeedAndWrite(PresetStoreState.Ready, null);
                return;
            }

            string text;
            try
            {
                text = File.ReadAllText(_path, Encoding.UTF8);
            }
            catch (Exception ex) when (IsIoFailure(ex))
            {
                Unavailable($"프리셋 파일을 읽지 못했다 — {ex.Message}");
                return;
            }

            DevicePresetFile file;
            try
            {
                file = DevicePresetFile.Parse(text);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException)
            {
                RecoverFromCorruption();
                return;
            }

            var presets = file.ToPresets(out var skipped);
            var skippedNote = skipped > 0 ? $"프리셋 {skipped}건은 모르는 카테고리라 건너뛰었다" : null;

            if (file.Schema > CurrentSchema)
            {
                _presets.AddRange(presets);
                Publish();
                State = PresetStoreState.ReadOnlyNewerSchema;
                StateMessage = Join($"프리셋 파일이 더 새 판(schema {file.Schema})이다 — 읽기 전용으로 열었다", skippedNote);
                return;
            }

            _presets.AddRange(presets);
            Publish();
            State = PresetStoreState.Ready;
            StateMessage = skippedNote;
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            Unavailable($"프리셋 파일을 열지 못했다 — {ex.Message}");
        }
    }

    /// <summary>깨진 파일은 <b>덮지도 지우지도 않고</b> 옆으로 옮긴다 — 사용자의 조립 결과다.</summary>
    private void RecoverFromCorruption()
    {
        string movedName;
        try
        {
            movedName = MoveAside();
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            Unavailable($"프리셋 파일이 깨졌는데 옮기지도 못했다 — {ex.Message}");
            return;
        }

        SeedAndWrite(PresetStoreState.RecoveredFromCorruption,
                     $"프리셋 파일이 깨져 있어 '{movedName}' 로 옮겼다");
    }

    private string MoveAside()
    {
        var stamp = _clock().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var target = $"{_path}.corrupt-{stamp}";

        // 같은 초에 두 번 깨져도 앞의 것을 덮지 않는다.
        for (var i = 2; File.Exists(target) && i < 1000; i++)
            target = $"{_path}.corrupt-{stamp}-{i}";

        File.Move(_path, target);
        return System.IO.Path.GetFileName(target);
    }

    private void SeedAndWrite(PresetStoreState state, string? note)
    {
        _presets.Clear();
        _presets.AddRange(DevicePresetSeeds.All.Select(DevicePresetSanitizer.Sanitize));
        Publish();

        var error = Persist();
        if (error is not null)
        {
            Unavailable(Join(note, $"본보기 프리셋을 쓰지 못했다 — {error}"));
            return;
        }

        State = state;
        StateMessage = note;
    }

    private void Unavailable(string? message)
    {
        _presets.Clear();
        Publish();
        State = PresetStoreState.Unavailable;
        StateMessage = message;
    }

    #endregion

    #region Read

    public IReadOnlyList<DevicePreset> ForCategory(EnumDeviceCategory category)
        => _snapshot.Where(p => p.Category == category).ToList();

    public DevicePreset? Find(string id)
        => string.IsNullOrWhiteSpace(id)
            ? null
            : _presets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    #endregion

    #region Mutate

    /// <summary>
    /// 더하거나(없던 Id) 갈아 끼운다(있던 Id). 넣기 전에 <see cref="DevicePresetSanitizer.Sanitize"/> 를 거친다.
    /// </summary>
    /// <remarks>
    /// <b>유형 공통 사실 여덟은 여기서 막지 않는다</b> — <see cref="DevicePresetSanitizer.FindForbiddenFacts"/> 로
    /// 화면이 먼저 보여 주고 사람이 고치게 한다. 저장을 막아 버리면 고치던 것을 통째로 잃는다.
    /// </remarks>
    public PresetStoreResult Save(DevicePreset preset)
    {
        if (preset is null) return Fail("저장할 프리셋이 없다.");
        if (IsReadOnly) return Fail(ReadOnlyReason("저장할"));
        if (string.IsNullOrWhiteSpace(preset.Id)) return Fail("프리셋에 식별자가 없다.");

        var clean = DevicePresetSanitizer.Sanitize(preset);
        var invalid = ValidateName(clean.Name, clean.Category, clean.Id);
        if (invalid is not null) return Fail(invalid);

        var backup = Snapshot();
        var index = IndexOf(clean.Id);
        var record = clean with { UpdatedAt = _clock() };

        if (index >= 0) _presets[index] = record;
        else _presets.Add(record);

        return Commit(backup, null);
    }

    public PresetStoreResult Rename(string id, string newName)
    {
        if (IsReadOnly) return Fail(ReadOnlyReason("이름을 바꿀"));

        var index = IndexOf(id);
        if (index < 0) return Fail("그 프리셋이 없다.");

        var name = (newName ?? string.Empty).Trim();
        var invalid = ValidateName(name, _presets[index].Category, _presets[index].Id);
        if (invalid is not null) return Fail(invalid);

        var backup = Snapshot();
        _presets[index] = _presets[index] with { Name = name, UpdatedAt = _clock() };
        return Commit(backup, null);
    }

    /// <summary>같은 내용 · 새 Id 로 한 벌 더 만든다. 복사본은 씨앗이 아니다(고쳐 쓰라고 만드는 것이다).</summary>
    public PresetStoreResult Duplicate(string id, out DevicePreset? copy)
    {
        copy = null;
        if (IsReadOnly) return Fail(ReadOnlyReason("복제할"));

        var source = Find(id);
        if (source is null) return Fail("그 프리셋이 없다.");

        var backup = Snapshot();
        var record = DevicePresetSanitizer.Sanitize(source) with
        {
            Id = NewId(),
            Name = NextFreeName(source.Category, source.Name, CopyCandidate),
            IsSeed = false,
            UpdatedAt = _clock(),
        };
        _presets.Add(record);

        var result = Commit(backup, null);
        if (result.IsSuccess) copy = record;
        return result;
    }

    public PresetStoreResult Delete(string id)
    {
        if (IsReadOnly) return Fail(ReadOnlyReason("삭제할"));

        var index = IndexOf(id);
        if (index < 0) return Fail("그 프리셋이 없다.");

        var backup = Snapshot();
        _presets.RemoveAt(index);
        return Commit(backup, null);
    }

    #endregion

    #region Export / Import

    /// <summary>
    /// 고른 프리셋(<paramref name="ids"/> 가 null 이면 전부)을 같은 봉투 형식으로 다른 파일에 쓴다.
    /// <b>읽기 전용이어도 된다</b> — 내보내기는 이 저장소의 파일을 건드리지 않고, 오히려 더 새 판 · 손상 때
    /// 사용자가 건져 낼 유일한 통로다.
    /// </summary>
    public PresetStoreResult Export(string filePath, IEnumerable<string>? ids = null)
    {
        if (!TryFullPath(filePath, out var target, out var reason)) return Fail(reason!);

        var chosen = _snapshot.AsEnumerable();
        if (ids is not null)
        {
            var wanted = new HashSet<string>(ids.Where(i => !string.IsNullOrWhiteSpace(i)), StringComparer.OrdinalIgnoreCase);
            chosen = chosen.Where(p => wanted.Contains(p.Id));
        }

        var list = chosen.ToList();
        var json = DevicePresetFile.Wrap(list, _clock()).ToJson();
        try
        {
            WriteAtomic(target!, json);
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return Fail($"프리셋을 내보내지 못했다 — {ex.Message}");
        }

        return new PresetStoreResult(true, $"프리셋 {list.Count}건을 내보냈다.");
    }

    /// <summary>
    /// 봉투 파일을 읽어 <b>새 Id</b> 로 더한다. 같은 카테고리에 같은 이름이 있으면 <c>" (2)"</c> 를 붙인다.
    /// 더 새 판 · 깨진 파일은 <b>이 저장소를 건드리지 않고</b> 거절한다.
    /// </summary>
    public PresetStoreResult Import(string filePath)
    {
        if (IsReadOnly) return Fail(ReadOnlyReason("가져올"));
        if (!TryFullPath(filePath, out var source, out var reason)) return Fail(reason!);
        if (!File.Exists(source)) return Fail("가져올 파일이 없다.");

        DevicePresetFile file;
        try
        {
            file = DevicePresetFile.Parse(File.ReadAllText(source!, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException)
        {
            return Fail("가져올 파일이 깨져 있어 아무것도 가져오지 않았다.");
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return Fail($"가져올 파일을 읽지 못했다 — {ex.Message}");
        }

        if (file.Schema > CurrentSchema)
            return Fail($"가져올 파일이 더 새 판(schema {file.Schema})이라 가져오지 않았다.");

        var incoming = file.ToPresets(out var skippedCategory);
        var backup = Snapshot();
        var imported = 0;
        var renamed = 0;
        var firstRenamed = (string?)null;
        var skippedName = 0;

        foreach (var entry in incoming)
        {
            var clean = DevicePresetSanitizer.Sanitize(entry);
            var name = Shorten(clean.Name);
            if (string.IsNullOrWhiteSpace(name)) { skippedName++; continue; }

            var free = NextFreeName(clean.Category, name, ClashCandidate);
            if (!string.Equals(free, name, StringComparison.Ordinal))
            {
                renamed++;
                firstRenamed ??= free;
            }

            _presets.Add(clean with
            {
                Id = NewId(),
                Name = free,
                IsSeed = false,
                UpdatedAt = _clock(),
            });
            imported++;
        }

        var note = $"프리셋 {imported}건을 가져왔다.";
        if (renamed > 0)
        {
            var suffix = firstRenamed is null ? "(2)" : SuffixOf(firstRenamed);
            note += $" 가져온 {imported}건 중 {renamed}건은 같은 이름이 있어 '{suffix}' 를 붙였다.";
        }
        if (skippedCategory > 0) note += $" {skippedCategory}건은 모르는 카테고리라 건너뛰었다.";
        if (skippedName > 0) note += $" {skippedName}건은 이름이 없어 건너뛰었다.";

        if (imported == 0)
        {
            Restore(backup);
            return new PresetStoreResult(true, note);
        }

        return Commit(backup, note);
    }

    #endregion

    #region Persist

    /// <summary>
    /// 목록을 파일에 쓴다. 실패하면 <b>메모리를 되돌린다</b> — 화면에는 저장된 것처럼 남고 파일에는 없는
    /// 어긋남이 가장 고약하다.
    /// </summary>
    private PresetStoreResult Commit(List<DevicePreset> backup, string? successNote)
    {
        Publish();
        var error = Persist();
        if (error is not null)
        {
            Restore(backup);
            return Fail($"프리셋을 저장하지 못했다 — {error}");
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return new PresetStoreResult(true, successNote);
    }

    private string? Persist()
    {
        try
        {
            WriteAtomic(_path, DevicePresetFile.Wrap(_snapshot, _clock()).ToJson());
            return null;
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// 임시 파일에 쓰고 <b>바꿔 끼운다</b>. 임시 이름에 guid 를 붙여 두 앱이 같은 <c>.tmp</c> 를 두고 다투지 않게 한다.
    /// </summary>
    private static void WriteAtomic(string path, string json)
    {
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory!);

        var temp = $"{path}.tmp-{Guid.NewGuid():N}";
        try
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);     // 전원이 나가도 반쪽 파일이 남지 않게
            }

            if (File.Exists(path)) File.Replace(temp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else File.Move(temp, path, overwrite: true);
        }
        catch (Exception)
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { /* 다음 쓰기가 새 이름을 쓴다 */ }
            throw;
        }
    }

    #endregion

    #region Helpers

    private static bool IsIoFailure(Exception ex)
        => ex is IOException or UnauthorizedAccessException or NotSupportedException
              or ArgumentException or System.Security.SecurityException;

    private static PresetStoreResult Fail(string message) => new(false, message);

    private string ReadOnlyReason(string action)
        => State == PresetStoreState.ReadOnlyNewerSchema
            ? $"프리셋 파일이 더 새 판이라 {action} 수 없다."
            : $"프리셋 파일을 다룰 수 없어 {action} 수 없다.";

    private int IndexOf(string? id)
        => string.IsNullOrWhiteSpace(id)
            ? -1
            : _presets.FindIndex(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    private string? ValidateName(string? name, EnumDeviceCategory category, string id)
    {
        var text = (name ?? string.Empty).Trim();
        if (text.Length == 0) return "프리셋 이름이 비어 있다.";
        if (text.Length > MaxNameLength) return $"프리셋 이름이 {MaxNameLength}자를 넘는다.";
        if (NameTaken(category, text, id)) return "같은 카테고리에 같은 이름의 프리셋이 이미 있다.";
        return null;
    }

    private bool NameTaken(EnumDeviceCategory category, string name, string? exceptId)
        => _presets.Any(p => p.Category == category
                             && !string.Equals(p.Id, exceptId, StringComparison.OrdinalIgnoreCase)
                             && string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase));

    /// <summary>비어 있는 이름을 찾을 때까지 꼬리표를 올린다.</summary>
    private string NextFreeName(EnumDeviceCategory category, string baseName, Func<string, int, string> candidate)
    {
        for (var i = 1; i < 1000; i++)
        {
            var name = candidate(baseName, i);
            if (!NameTaken(category, name, null)) return name;
        }
        return Shorten($"{baseName} {NewId()[..6]}");
    }

    private static string CopyCandidate(string baseName, int i)
    {
        var tail = i == 1 ? CopySuffix : $"{CopySuffix} {i}";
        return Trim(baseName, tail);
    }

    private static string ClashCandidate(string baseName, int i)
        => i == 1 ? Shorten(baseName) : Trim(baseName, $" ({i})");

    /// <summary>머리를 잘라 꼬리표까지 넣고도 상한을 넘지 않게 한다.</summary>
    private static string Trim(string baseName, string tail)
    {
        var head = baseName;
        if (head.Length + tail.Length > MaxNameLength)
            head = head[..Math.Max(0, MaxNameLength - tail.Length)].TrimEnd();
        return head + tail;
    }

    private static string Shorten(string? name)
    {
        var text = (name ?? string.Empty).Trim();
        return text.Length <= MaxNameLength ? text : text[..MaxNameLength].TrimEnd();
    }

    /// <summary>알림 글에 보일 꼬리표만 떼어 낸다 — "이름 (2)" → "(2)".</summary>
    private static string SuffixOf(string name)
    {
        var open = name.LastIndexOf('(');
        return open >= 0 ? name[open..] : "(2)";
    }

    private static string? Join(string? first, string? second)
        => first is null ? second : second is null ? first : $"{first} · {second}";

    private List<DevicePreset> Snapshot() => new(_presets);

    private void Restore(List<DevicePreset> backup)
    {
        _presets.Clear();
        _presets.AddRange(backup);
        Publish();
    }

    /// <summary>카테고리 차례 → 이름 차례로 세우고 밖에 보일 한 벌을 굳힌다.</summary>
    private void Publish()
    {
        _presets.Sort(static (a, b) =>
        {
            var byCategory = ((int)a.Category).CompareTo((int)b.Category);
            if (byCategory != 0) return byCategory;
            var byName = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            return byName != 0 ? byName : string.CompareOrdinal(a.Id, b.Id);
        });
        _snapshot = _presets.ToArray();
    }

    private static bool TryFullPath(string? path, out string? full, out string? reason)
    {
        full = null;
        reason = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "파일 경로가 비어 있다.";
            return false;
        }

        try
        {
            full = System.IO.Path.GetFullPath(path!);
            return true;
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            reason = $"쓸 수 없는 파일 경로다 — {ex.Message}";
            return false;
        }
    }

    #endregion
}
