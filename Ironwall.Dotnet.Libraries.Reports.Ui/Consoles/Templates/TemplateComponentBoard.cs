using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Templates;

/// <summary>
/// 템플릿 구성 요소 한 줄 — 켤지 끌지와 <b>순서</b>를 갖는다.
/// </summary>
public sealed class TemplateComponentItem : PropertyChangedBase
{
    private bool _isEnabled;

    public TemplateComponentItem(string id, string display, string? category)
    {
        Id = id;
        Display = display;
        Category = category;
    }

    /// <summary>서버 컴포넌트 문자열 id(<c>components[].id</c>).</summary>
    public string Id { get; }

    public string Display { get; }

    public string? Category { get; }

    /// <summary>차트 종류(서버 <c>chart_type</c>) — PIE · BAR · LINE, 그리드 · 요약카드는 null.</summary>
    public string? ChartType { get; init; }

    /// <summary>서버 설명문 — 툴팁.</summary>
    public string? Description { get; init; }

    /// <summary>종류 한국어 라벨 — null(그리드 · 요약)은 "표/요약".</summary>
    public string ChartTypeLabel => ChartType switch
    {
        "PIE" => "원형",
        "BAR" => "막대",
        "LINE" => "추이",
        null or "" => "표/요약",
        _ => ChartType!,
    };

    /// <summary>계측 이름 — 바인딩식이라 인스턴스마다 다르다(템플릿에 고정 리터럴을 쓰지 않는다).</summary>
    public string AutomationId => $"Reports.Detail.Component.{Id}";

    /// <summary>보고서에 실을 것인가.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            NotifyOfPropertyChange();
            EnabledChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>보드가 구독한다 — 체크가 바뀌면 순서 · 미적용 계산을 다시 한다.</summary>
    internal event EventHandler? EnabledChanged;

    /// <summary>알림 없이 값을 넣는다(적재 · 되돌리기).</summary>
    internal void SetEnabledQuiet(bool value)
    {
        if (_isEnabled == value) return;
        _isEnabled = value;
        NotifyOfPropertyChange(nameof(IsEnabled));
    }

    public override string ToString() => Display;
}

/// <summary>
/// 템플릿 구성 보드 — 체크 · <b>순서</b> · 미적용 판정을 모은 <b>순수</b> 모델(WPF 를 모른다).
/// </summary>
/// <remarks>
/// <para><b>왜 순수인가</b>: UIA 에 드래그 패턴이 없어(.NET 8 WPF) 드래그는 자동화로 단언할 수 없다.
/// 그래서 판정을 여기로 빼고 헤드리스로 단언한다 — 드래그 규칙의 "판정은 순수 함수" 조항.</para>
/// <para><b>순서의 뜻</b>: 서버는 <c>components[].order</c> 를 받는다(<see cref="ReportComponentConfigDto.Order"/>).
/// 켠 것만 목록 순서대로 0 부터 다시 매겨 보낸다. <b>끈 줄을 옮기는 것은 아무 뜻이 없다</b> —
/// 그래서 그때는 미적용 변경으로 세지 않는다(<see cref="IsDirty"/>).</para>
/// <para><b>서버 왕복 0</b>: 순서를 바꿀 때 아무것도 보내지 않는다. [적용] 한 번에 PATCH 1건으로 나간다
/// — 행마다 PATCH 를 반복하면 드래그 1회가 N 왕복이 된다(드래그 규칙).</para>
/// </remarks>
public sealed class TemplateComponentBoard
{
    private IReadOnlyList<(string Id, bool Enabled)> _baseline = Array.Empty<(string, bool)>();

    /// <summary>화면에 보이는 순서 그대로. 켠 줄 · 끈 줄이 섞여 있다.</summary>
    public ObservableCollection<TemplateComponentItem> Items { get; } = new();

    /// <summary>체크 · 순서 중 무엇이든 바뀌었다.</summary>
    public event EventHandler? Changed;

    public int EnabledCount => Items.Count(i => i.IsEnabled);

    public int Count => Items.Count;

    /// <summary>켠 것의 <b>순서까지 포함한</b> 지금 상태. 미적용 판정의 기준이다.</summary>
    public string Signature => string.Join(",", Items.Where(i => i.IsEnabled).Select(i => i.Id));

    /// <summary>마지막으로 저장(또는 적재)했을 때의 서명.</summary>
    public string BaselineSignature { get; private set; } = string.Empty;

    /// <summary>켠 것의 집합 또는 순서가 달라졌는가.</summary>
    public bool IsDirty => !string.Equals(Signature, BaselineSignature, StringComparison.Ordinal);

    /// <summary>
    /// 카탈로그 + 저장된 구성으로 보드를 세운다.
    /// 저장된 순서(<c>order</c>)가 앞서고, 나머지 카탈로그 항목이 뒤를 잇는다.
    /// </summary>
    public void Load(IEnumerable<ReportComponentCategoryDto>? catalog, IEnumerable<ReportComponentConfigDto>? saved)
    {
        var savedList = (saved ?? Enumerable.Empty<ReportComponentConfigDto>())
            .Where(c => c.Enabled)
            .OrderBy(c => c.Order)
            .Select(c => c.Id)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var savedRank = savedList.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i, StringComparer.Ordinal);

        var built = new List<TemplateComponentItem>();
        foreach (var category in catalog ?? Enumerable.Empty<ReportComponentCategoryDto>())
        {
            foreach (var entry in category.Entries)
            {
                if (string.IsNullOrEmpty(entry.Id)) continue;
                if (built.Any(b => string.Equals(b.Id, entry.Id, StringComparison.Ordinal))) continue;

                var item = new TemplateComponentItem(entry.Id, entry.Name ?? entry.Id, category.Label ?? category.Category)
                {
                    ChartType = entry.ChartType,
                    Description = entry.Description,
                };
                item.SetEnabledQuiet(savedRank.ContainsKey(entry.Id));
                built.Add(item);
            }
        }

        // 저장된 구성 중 카탈로그에 없는 것 — 말없이 빼면 [적용] 때 조용히 사라진다. 보이게 두고 켜 둔다.
        foreach (var id in savedList.Where(id => built.All(b => !string.Equals(b.Id, id, StringComparison.Ordinal))))
        {
            var orphan = new TemplateComponentItem(id, id, "서버 카탈로그에 없음");
            orphan.SetEnabledQuiet(true);
            built.Add(orphan);
        }

        // 저장된 순서가 앞. 카탈로그 순서는 그 뒤에 원래 차례대로.
        var ordered = built
            .Select((item, index) => (item, index))
            .OrderBy(x => savedRank.TryGetValue(x.item.Id, out var rank) ? rank : int.MaxValue)
            .ThenBy(x => x.index)
            .Select(x => x.item)
            .ToList();

        foreach (var item in Items) item.EnabledChanged -= OnItemEnabledChanged;
        Items.Clear();
        foreach (var item in ordered)
        {
            item.EnabledChanged += OnItemEnabledChanged;
            Items.Add(item);
        }

        MarkBaseline();
    }

    /// <summary>지금 상태를 "저장된 상태"로 굳힌다 — 적재 직후와 저장 성공 직후에 부른다.</summary>
    public void MarkBaseline()
    {
        _baseline = Items.Select(i => (i.Id, i.IsEnabled)).ToList();
        BaselineSignature = Signature;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>마지막 기준으로 되돌린다 — 체크와 순서를 함께 복원한다(서버 미호출).</summary>
    public void Revert()
    {
        if (_baseline.Count == 0) return;

        var byId = Items.ToDictionary(i => i.Id, StringComparer.Ordinal);
        var restored = new List<TemplateComponentItem>();
        foreach (var (id, enabled) in _baseline)
        {
            if (!byId.TryGetValue(id, out var item)) continue;
            item.SetEnabledQuiet(enabled);
            restored.Add(item);
        }
        // 기준에 없던 줄(있을 수 없지만 방어) 은 뒤에 붙인다.
        restored.AddRange(Items.Where(i => !restored.Contains(i)));

        Items.Clear();
        foreach (var item in restored) Items.Add(item);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 고른 줄들을 <paramref name="insertionIndex"/> 자리로 옮긴다(옮기기 <b>전</b> 목록 기준 0..Count).
    /// 실제로 자리가 바뀌었으면 참.
    /// </summary>
    public bool Move(IReadOnlyList<int> fromIndexes, int insertionIndex)
    {
        if (fromIndexes is null || fromIndexes.Count == 0) return false;
        if (insertionIndex < 0 || insertionIndex > Items.Count) return false;
        if (fromIndexes.Any(i => i < 0 || i >= Items.Count)) return false;

        var before = Items.Select(i => i.Id).ToList();
        DragMath.MoveMany(Items, fromIndexes, insertionIndex);
        var after = Items.Select(i => i.Id).ToList();
        if (before.SequenceEqual(after, StringComparer.Ordinal)) return false;

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// 서버로 보낼 구성 — <b>켠 것만</b>, 목록 순서대로 <c>order</c> 를 0 부터 다시 매긴다.
    /// </summary>
    public List<ReportComponentConfigDto> ToConfig()
        => Items.Where(i => i.IsEnabled)
                .Select((item, index) => new ReportComponentConfigDto { Id = item.Id, Order = index, Enabled = true })
                .ToList();

    private void OnItemEnabledChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
