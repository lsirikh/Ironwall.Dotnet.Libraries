using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;

/// <summary>
/// 콘솔 상세 칸의 여섯 상태(설계 정본 window-layout-system-storyboard.html L920-929).
/// </summary>
public enum ConsoleDetailState
{
    /// <summary>선택 없음 — 안내 한 줄과 이 종류의 정상 · 장애 수.</summary>
    None,
    /// <summary>한 개.</summary>
    Single,
    /// <summary>여러 개 — 값이 다른 칸은 "— 여러 값 —", 손댄 칸만 적용.</summary>
    Multiple,
    /// <summary>새로 등록 — 다이얼로그 없이 같은 자리에서.</summary>
    Create,
    /// <summary>미적용 변경 — 바닥 막대가 경고색, 이동을 막는다.</summary>
    Dirty,
    /// <summary>읽기 전용 — 칸은 잠기고 이유 한 줄.</summary>
    ReadOnly,
}

/// <summary>이동(행 · 레일 · 추가 · 검색 …)의 종류. 미적용 변경이 있을 때 무엇을 막을지 가른다.</summary>
public enum ConsoleNavigation
{
    SelectRow,
    SwitchRail,
    BeginCreate,
    Refresh,
    /// <summary>검색 · 필터 입력 — 선택을 바꾸지 않으므로 막지 않는다.</summary>
    Search,
    /// <summary>"열" 메뉴 · 상세 폭 — 표시만 바꾼다.</summary>
    ChangeView,
}

/// <summary>
/// 상세 칸 상태 판정 — 순수 함수. 우선순위: 읽기 전용 &gt; 등록 &gt; 미적용 변경 &gt; 선택 수.
/// </summary>
public static class ConsoleDetailStateMachine
{
    public const string BlockedNotice = "적용하거나 되돌린 뒤 이동하세요";
    public const string MixedValuesText = "— 여러 값 —";

    public static ConsoleDetailState Resolve(int selectedCount, bool isCreating, int dirtyCount, bool isReadOnly)
    {
        if (selectedCount < 0) throw new ArgumentOutOfRangeException(nameof(selectedCount));

        if (isCreating) return ConsoleDetailState.Create;               // 권한이 없으면 애초에 [추가] 가 꺼져 있다
        if (selectedCount == 0) return ConsoleDetailState.None;
        if (isReadOnly) return ConsoleDetailState.ReadOnly;
        if (dirtyCount > 0) return ConsoleDetailState.Dirty;
        return selectedCount == 1 ? ConsoleDetailState.Single : ConsoleDetailState.Multiple;
    }

    /// <summary>미적용 변경이 있으면 선택을 바꾸는 이동을 막는다. 표시만 바꾸는 조작은 통과.</summary>
    public static bool IsBlocked(ConsoleNavigation navigation, int dirtyCount)
        => dirtyCount > 0
           && navigation is (ConsoleNavigation.SelectRow or ConsoleNavigation.SwitchRail
                             or ConsoleNavigation.BeginCreate or ConsoleNavigation.Refresh);

    /// <summary>등록 폼의 바닥 막대 글 — 서랍 막대(단추 둘 옆 약 170px)에 한 줄로 들어가는 길이.</summary>
    public const string CreateFooter = "아직 등록 전입니다";

    /// <summary>바닥 막대 문구.</summary>
    public static string FooterText(ConsoleDetailState state, int dirtyCount, string? lastMessage = null) => state switch
    {
        ConsoleDetailState.None => string.IsNullOrEmpty(lastMessage) ? "선택 대기" : lastMessage!,
        ConsoleDetailState.ReadOnly => "읽기 전용",
        // 서랍 360 의 막대(단추 둘 옆)에서 "않습 / 니다" 로 갈렸다(한글은 음절마다 줄바꿈 자리) — 짧게.
        ConsoleDetailState.Create => CreateFooter,
        ConsoleDetailState.Dirty => $"변경 {dirtyCount}건 미적용",
        _ => string.IsNullOrEmpty(lastMessage) ? "변경 없음" : lastMessage!,
    };

    /// <summary>[적용] · [되돌리기] 를 켤 것인가.</summary>
    public static bool CanApply(ConsoleDetailState state, int dirtyCount)
        => state == ConsoleDetailState.Dirty || (state == ConsoleDetailState.Create && dirtyCount > 0);

    /// <summary>적용 뒤 알림 문구 — "3개에 2건 적용했습니다".</summary>
    public static string AppliedMessage(int selectedCount, int appliedFieldCount)
        => (selectedCount > 1 ? $"{selectedCount}개에 " : string.Empty) + $"{appliedFieldCount}건 적용했습니다";
}

/// <summary>
/// 여러 개를 골랐을 때 한 칸의 값 — 전부 같으면 그 값, 아니면 "여러 값".
/// </summary>
public readonly struct MixedValue<T>
{
    private MixedValue(bool isMixed, T? value) { IsMixed = isMixed; Value = value; }

    public bool IsMixed { get; }
    public T? Value { get; }

    public static MixedValue<T> Of(IEnumerable<T?> values, IEqualityComparer<T?>? comparer = null)
    {
        comparer ??= EqualityComparer<T?>.Default;
        using var e = values.GetEnumerator();
        if (!e.MoveNext()) return new MixedValue<T>(false, default);

        var first = e.Current;
        while (e.MoveNext())
            if (!comparer.Equals(first, e.Current)) return new MixedValue<T>(true, default);
        return new MixedValue<T>(false, first);
    }

    /// <summary>화면에 찍을 글자.</summary>
    public string Display(Func<T?, string>? format = null)
        => IsMixed ? ConsoleDetailStateMachine.MixedValuesText : (format?.Invoke(Value) ?? Value?.ToString() ?? string.Empty);
}

/// <summary>
/// 손댄 칸 추적 — <b>손댄 칸만</b> 적용한다. 원래 값으로 되돌려 놓으면 손대지 않은 것으로 친다.
/// </summary>
public sealed class DirtyFieldTracker
{
    private readonly Dictionary<string, (object? Original, object? Current, bool HasOriginal)> _fields = new(StringComparer.Ordinal);

    /// <summary>여러 개 선택에서 식별 칸(장비번호 등)은 한꺼번에 덮어쓰지 않는다.</summary>
    private readonly HashSet<string> _identityKeys = new(StringComparer.Ordinal);

    public event EventHandler? Changed;

    public int Count => _fields.Count;
    public bool IsDirty => _fields.Count > 0;
    public IReadOnlyCollection<string> Keys => _fields.Keys;

    public void MarkIdentity(params string[] keys)
    {
        foreach (var k in keys) _identityKeys.Add(k);
    }

    public bool IsTouched(string key) => _fields.ContainsKey(key);

    /// <summary>
    /// 칸을 고쳤다. <paramref name="hasOriginal"/> 이 거짓이면(여러 값 · 새 항목) 어떤 입력이든 변경으로 친다.
    /// </summary>
    public void Touch(string key, object? original, object? current, bool hasOriginal = true)
    {
        var before = _fields.Count;
        var had = _fields.ContainsKey(key);

        if (hasOriginal && Equals(original, current)) _fields.Remove(key);
        else _fields[key] = (original, current, hasOriginal);

        if (before != _fields.Count || had) Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>적용할 (칸, 값) 목록. 여러 개를 골랐으면 식별 칸은 뺀다.</summary>
    public IReadOnlyList<KeyValuePair<string, object?>> ChangesFor(int selectedCount)
        => _fields
            .Where(f => selectedCount <= 1 || !_identityKeys.Contains(f.Key))
            .Select(f => new KeyValuePair<string, object?>(f.Key, f.Value.Current))
            .ToList();

    public void Clear()
    {
        if (_fields.Count == 0) return;
        _fields.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// 미적용 이동 차단 — 막았으면 <see cref="Blocked"/> 를 울려 바닥 막대가 문구를 바꾸고 흔들리게 한다.
/// </summary>
public sealed class NavigationGuard
{
    private readonly Func<int> _dirtyCount;

    public NavigationGuard(Func<int> dirtyCount) => _dirtyCount = dirtyCount ?? throw new ArgumentNullException(nameof(dirtyCount));

    public event EventHandler<ConsoleNavigation>? Blocked;

    /// <summary>이동해도 되면 true. 막았으면 false 를 돌려주고 <see cref="Blocked"/> 를 울린다.</summary>
    public bool TryNavigate(ConsoleNavigation navigation)
    {
        if (!ConsoleDetailStateMachine.IsBlocked(navigation, _dirtyCount())) return true;
        Blocked?.Invoke(this, navigation);
        return false;
    }
}
