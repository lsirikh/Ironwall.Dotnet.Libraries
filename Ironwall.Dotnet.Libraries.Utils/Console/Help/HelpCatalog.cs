using System.Diagnostics;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 설명 목록 — 키 → <see cref="HelpEntry"/>. 각 UI 프로젝트가 제 항목을 등록하고 화면은 키만 건다(help-callout PRD FR-03).
/// </summary>
/// <remarks>
/// <para><b>등록 방법</b>: 각 UI 어셈블리에 설명 목록 클래스(예: <c>DevicesHelp</c>)를 두고 <c>[ModuleInitializer]</c> 에서
/// <see cref="Register"/> 를 한 번 부른다. 그 어셈블리의 형식이 처음 쓰일 때(= 그 어셈블리의 뷰가 처음 뜰 때) 등록되므로
/// Autofac 모듈 · 부트스트래퍼 순서와 무관하고, 컨테이너 없이 뜨는 시험 창 · 미리보기 도구에서도 같다.
/// 키를 거는 XAML 과 그 문구는 <b>같은 어셈블리</b>에 둔다 — 그래야 뷰가 뜨기 전에 등록이 끝나 있다.</para>
/// <para>같은 키를 다시 등록하면 나중 것이 이긴다(디버그 로그). 없는 키를 찾으면 <c>null</c> — 화면은 "?" 를 끄고 죽지 않는다.</para>
/// <para>호출 스레드: 아무 스레드(모듈 초기화는 처음 쓰는 스레드에서 돈다). 내부는 잠금, <see cref="Changed"/> 는 잠금 밖에서 울린다.</para>
/// </remarks>
public static class HelpCatalog
{
    private static readonly object _gate = new();
    private static readonly Dictionary<string, HelpEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>항목이 더해지거나 바뀌었다 — 이미 뜬 "?" 가 없던 키를 다시 찾는다. 처리기는 제 디스패처로 옮겨 가야 한다.</summary>
    public static event EventHandler? Changed;

    /// <summary>항목들을 등록한다. 같은 키는 덮어쓴다.</summary>
    public static void Register(IEnumerable<HelpEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var changed = false;
        lock (_gate)
        {
            foreach (var entry in entries)
            {
                if (entry is null) continue;
                if (_entries.TryGetValue(entry.Key, out var old))
                {
                    if (ReferenceEquals(old, entry) || old.Equals(entry)) continue;
                    Debug.WriteLine($"[HelpCatalog] '{entry.Key}' 을(를) 다시 등록 — 나중 것이 이긴다.");
                }
                _entries[entry.Key] = entry;
                changed = true;
            }
        }
        if (changed) Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>키로 찾는다. 없으면 <c>false</c>.</summary>
    public static bool TryGet(string? key, out HelpEntry entry)
    {
        entry = null!;
        if (string.IsNullOrWhiteSpace(key)) return false;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var found)) return false;
            entry = found;
            return true;
        }
    }

    /// <summary>키로 찾는다. 없으면 <c>null</c>.</summary>
    public static HelpEntry? Find(string? key) => TryGet(key, out var entry) ? entry : null;

    /// <summary>지금 등록된 키(사본).</summary>
    public static IReadOnlyCollection<string> Keys
    {
        get { lock (_gate) return _entries.Keys.ToList(); }
    }
}
