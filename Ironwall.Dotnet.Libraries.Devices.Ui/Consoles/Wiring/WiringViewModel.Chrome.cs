using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>결선 창 알림 갈래 — 앞일수록 먼저 보인다(할 일이 있는 것 → 경고 → 안내).</summary>
public enum WiringNoticeKind
{
    /// <summary>번호순 제안 — [이대로 적용].</summary>
    Suggestion = 0,
    /// <summary>번호 대역 미설정 — [대역 고르기].</summary>
    Bands = 1,
    /// <summary>체인을 고치다가 제안을 함께 적용함(경고).</summary>
    Applied = 2,
    /// <summary>옛 배치 변환 · 가지 합침(경고).</summary>
    Legacy = 3,
    /// <summary>결선 모양을 어떻게 정했나(안내).</summary>
    Topology = 4,
    /// <summary>펜스 구성 제안 · 서버 순서에 맞춤(안내).</summary>
    Fence = 5,
}

/// <summary>알림 한 줄.</summary>
public sealed record WiringNotice(WiringNoticeKind Kind, string Text, bool IsWarning)
{
    /// <summary>알림 목록 항목의 자동화 id 꼬리(<c>Devices.Wiring.Notices.Item.{Kind}</c>).</summary>
    public string AutomationKey => Kind.ToString();

    /// <summary>할 일 단추 글자 — 없으면 빈 글자.</summary>
    public string ActionText => Kind switch { WiringNoticeKind.Suggestion => "이대로 적용", WiringNoticeKind.Bands => "대역 고르기", _ => string.Empty };

    public bool HasAction => ActionText.Length > 0;
}

/// <summary>
/// 결선 창 배치 판정 — <b>순수 함수</b>(창 정리 · 렌더 검토 2026-10-01): 알림 한 줄로 접기(우선순위) · 펜스 보기 위/아래 나눔 비율.
/// </summary>
public static class WiringLayoutMath
{
    /// <summary>펜스 3D 보기 : 개념도 기본 비율(위 몫).</summary>
    public const double DEFAULT_SPLIT = 0.58;
    public const double MIN_SPLIT = 0.25;
    public const double MAX_SPLIT = 0.85;

    /// <summary>나눔 최소 높이(px) — 3D 보기 · 개념도(줄 두 개 + 눈금이 읽히는 높이).</summary>
    public const double MIN_CANVAS_HEIGHT = 160;
    public const double MIN_CONCEPT_HEIGHT = 150;

    /// <summary>속성 칸 기본 폭(px).</summary>
    public const double DETAIL_WIDTH = 320;

    /// <summary>
    /// 나눔 비율을 범위 안으로 — <see cref="MIN_SPLIT"/>…<see cref="MAX_SPLIT"/>, 그리고 <paramref name="totalHeight"/> 가 주어지면 위 · 아래가
    /// 저마다 최소 높이를 지키게. 둘 다 못 지킬 만큼 낮으면 가운데. 숫자가 아니면 기본.
    /// </summary>
    public static double ClampSplit(double ratio, double totalHeight = double.NaN)
    {
        var r = double.IsFinite(ratio) ? Math.Clamp(ratio, MIN_SPLIT, MAX_SPLIT) : DEFAULT_SPLIT;
        if (!double.IsFinite(totalHeight) || totalHeight <= 0) return r;
        var low = MIN_CANVAS_HEIGHT / totalHeight;
        var high = 1 - MIN_CONCEPT_HEIGHT / totalHeight;
        if (low > high) return 0.5;
        return Math.Clamp(r, low, high);
    }

    /// <summary>알림을 우선순위로 — 갈래 차례(<see cref="WiringNoticeKind"/>) 그대로. 빈 글자는 뺀다.</summary>
    public static IReadOnlyList<WiringNotice> OrderNotices(IEnumerable<WiringNotice> notices)
        => (notices ?? Enumerable.Empty<WiringNotice>()).Where(n => !string.IsNullOrWhiteSpace(n.Text)).OrderBy(n => (int)n.Kind).ToList();

    /// <summary>알림 칩 글자 — "알림 3". 없으면 빈 글자.</summary>
    public static string NoticeChipText(int count) => count > 0 ? $"알림 {count}" : string.Empty;
}

/// <summary>
/// 결선 창 겉모양(창 정리 2026-10-01) — 알림 한 줄 · 미배치 띠 · 펜스 보기 위/아래 나눔 · 속성 칸 접기.
/// </summary>
public sealed partial class WiringViewModel
{
    private static readonly HashSet<string> NoticeSources = new()
    {
        nameof(HasSuggestion), nameof(SuggestionText), nameof(HasNoBandsNotice), nameof(NoBandsNoticeText), nameof(HasAppliedNotice), nameof(AppliedNoticeText),
        nameof(HasLegacyNotice), nameof(LegacyNoticeText), nameof(HasTopologyNotice), nameof(TopologyNoticeText), nameof(HasFenceNotice), nameof(FenceNoticeText),
    };

    private static readonly HashSet<string> StripSources = new() { nameof(UnplacedCount), nameof(IsPaletteEmpty) };

    private double _fenceSplitRatio = WiringLayoutMath.DEFAULT_SPLIT;
    private bool _isDetailPaneOpen = true;
    private bool _isSensorDragging;
    private bool _isNoticeListOpen;
    private Action<double, bool>? _saveLayoutPrefs;

    /// <summary>파생 알림 · 띠 속성을 원천 속성과 함께 알린다.</summary>
    public override void NotifyOfPropertyChange([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null!)
    {
        base.NotifyOfPropertyChange(propertyName);
        if (propertyName is null) return;
        if (NoticeSources.Contains(propertyName))
        {
            foreach (var name in new[]
            {
                nameof(Notices), nameof(NoticeCount), nameof(HasNotices), nameof(HasMoreNotices), nameof(NoticeChipText), nameof(PrimaryNotice),
                nameof(IsSuggestionPrimary), nameof(IsBandsPrimary), nameof(IsAppliedPrimary), nameof(IsLegacyPrimary), nameof(IsTopologyPrimary), nameof(IsFencePrimary),
            }) base.NotifyOfPropertyChange(name);
        }
        if (StripSources.Contains(propertyName))
            foreach (var name in new[] { nameof(IsUnplacedStripExpanded), nameof(UnplacedStripText) }) base.NotifyOfPropertyChange(name);
        if (propertyName is nameof(IsFenceView) or nameof(IsTableView) or nameof(IsWiringStep)) base.NotifyOfPropertyChange(nameof(IsFenceCountsShown));
    }

    /// <summary>상태줄에 펜스 종류별 수를 보이는가 — 결선 단계의 펜스 보기에서만(옛 펜스 보기 바닥 띠를 상태줄로 합쳤다).</summary>
    public bool IsFenceCountsShown => IsWiringStep && IsFenceView;

    #region - Notices (알림 한 줄) -
    /// <summary>지금 알림 — 우선순위 차례(할 일 → 경고 → 안내).</summary>
    public IReadOnlyList<WiringNotice> Notices
    {
        get
        {
            var list = new List<WiringNotice>(6);
            if (HasSuggestion) list.Add(new WiringNotice(WiringNoticeKind.Suggestion, SuggestionText, false));
            if (HasNoBandsNotice) list.Add(new WiringNotice(WiringNoticeKind.Bands, NoBandsNoticeText, false));
            if (HasAppliedNotice) list.Add(new WiringNotice(WiringNoticeKind.Applied, AppliedNoticeText, true));
            if (HasLegacyNotice) list.Add(new WiringNotice(WiringNoticeKind.Legacy, LegacyNoticeText, true));
            if (HasTopologyNotice) list.Add(new WiringNotice(WiringNoticeKind.Topology, TopologyNoticeText, false));
            if (HasFenceNotice) list.Add(new WiringNotice(WiringNoticeKind.Fence, FenceNoticeText, false));
            return WiringLayoutMath.OrderNotices(list);
        }
    }

    public int NoticeCount => Notices.Count;
    public bool HasNotices => NoticeCount > 0;

    /// <summary>둘 이상이면 칩을 눌러 나머지를 본다.</summary>
    public bool HasMoreNotices => NoticeCount > 1;

    public string NoticeChipText => WiringLayoutMath.NoticeChipText(NoticeCount);

    /// <summary>한 줄에 보이는 알림(가장 앞).</summary>
    public WiringNotice? PrimaryNotice => Notices.FirstOrDefault();

    public bool IsSuggestionPrimary => PrimaryNotice?.Kind == WiringNoticeKind.Suggestion;
    public bool IsBandsPrimary => PrimaryNotice?.Kind == WiringNoticeKind.Bands;
    public bool IsAppliedPrimary => PrimaryNotice?.Kind == WiringNoticeKind.Applied;
    public bool IsLegacyPrimary => PrimaryNotice?.Kind == WiringNoticeKind.Legacy;
    public bool IsTopologyPrimary => PrimaryNotice?.Kind == WiringNoticeKind.Topology;
    public bool IsFencePrimary => PrimaryNotice?.Kind == WiringNoticeKind.Fence;

    /// <summary>알림 목록(칩을 누르면 펼친다).</summary>
    public bool IsNoticeListOpen
    {
        get => _isNoticeListOpen;
        set { if (_isNoticeListOpen == value) return; _isNoticeListOpen = value; NotifyOfPropertyChange(); }
    }

    /// <summary>알림 목록 항목의 할 일 — [이대로 적용] · [대역 고르기].</summary>
    public void RunNoticeAction(WiringNoticeKind kind)
    {
        IsNoticeListOpen = false;
        switch (kind)
        {
            case WiringNoticeKind.Suggestion: AcceptSuggestion(); break;
            case WiringNoticeKind.Bands: ShowBandPicker(); break;
        }
    }
    #endregion

    #region - Unplaced strip (미배치 · 빼는 곳 한 띠) -
    /// <summary>
    /// 미배치 칩 목록을 펼치는가 — 미배치 센서가 있을 때. 끄는 동안 높이를 바꾸면 3D 보기가 포인터 밑에서 움직이므로 끌기 중에는 바꾸지 않고
    /// (빼는 곳은 늘 띠 오른쪽에 있다 — 드롭존 등록 유지), 끝난 뒤 맞춘다.
    /// </summary>
    public bool IsUnplacedStripExpanded => UnplacedCount > 0;

    /// <summary>띠 왼쪽 글자 — "미배치 3" · "미배치 0 · 전부 결선에 붙었습니다 ✓".</summary>
    public string UnplacedStripText => UnplacedCount > 0 ? $"미배치 {UnplacedCount}" : "미배치 0 · 전부 결선에 붙었습니다 ✓";

    /// <summary>센서를 끄는 중인가(펜스 캔버스 · 표 · 개념도) — 빼는 곳 안내를 또렷이.</summary>
    public bool IsSensorDragging => _isSensorDragging;

    /// <summary>빼는 곳 글자 — 끄는 동안은 "여기 놓으면 …".</summary>
    public string BinHintText => _isSensorDragging ? "여기 놓으면 결선에서 빠집니다" : "빼는 곳";

    /// <summary>센서 끌기가 시작 · 끝났다(뷰가 알린다).</summary>
    public void NotifySensorDrag(bool dragging)
    {
        if (_isSensorDragging == dragging) return;
        _isSensorDragging = dragging;
        NotifyOfPropertyChange(nameof(IsSensorDragging));
        NotifyOfPropertyChange(nameof(BinHintText));
    }
    #endregion

    #region - Split · detail pane -
    /// <summary>펜스 3D 보기 몫(0.25…0.85 · 기본 0.58) — 나머지는 개념도.</summary>
    public double FenceSplitRatio => _fenceSplitRatio;

    /// <summary>나눔을 옮겼다(나눔 막대를 놓을 때) — 범위 안으로 누르고 기억한다.</summary>
    public double SetFenceSplitRatio(double ratio, double totalHeight = double.NaN)
    {
        var clamped = WiringLayoutMath.ClampSplit(ratio, totalHeight);
        if (Math.Abs(clamped - _fenceSplitRatio) < 1e-9) return clamped;
        _fenceSplitRatio = clamped;
        NotifyOfPropertyChange(nameof(FenceSplitRatio));
        SaveLayoutPrefs();
        return clamped;
    }

    /// <summary>오른쪽 속성 칸이 열려 있는가(접으면 그림이 폭을 갖는다 · 선택은 계속 따라가 다시 열면 맞다).</summary>
    public bool IsDetailPaneOpen
    {
        get => _isDetailPaneOpen;
        set
        {
            if (_isDetailPaneOpen == value) return;
            _isDetailPaneOpen = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(DetailToggleText));
            SaveLayoutPrefs();
        }
    }

    /// <summary>[속성 칸 접기/펴기].</summary>
    public void ToggleDetailPane() => IsDetailPaneOpen = !IsDetailPaneOpen;

    /// <summary>접기 단추 글자 — 열려 있으면 "›"(오른쪽으로 접기), 접혀 있으면 "‹".</summary>
    public string DetailToggleText => _isDetailPaneOpen ? "›" : "‹";

    /// <summary>
    /// 개인 표시 설정(<c>ConsolePrefs</c>)을 잇는다 — 저장된 나눔 · 칸 열림으로 시작하고, 바뀌면 <paramref name="save"/> 로 남긴다. 런처가 부른다.
    /// </summary>
    public void UseLayoutPrefs(double? splitRatio, bool? detailOpen, Action<double, bool>? save)
    {
        _saveLayoutPrefs = null;
        if (splitRatio is { } r) _fenceSplitRatio = WiringLayoutMath.ClampSplit(r);
        if (detailOpen is { } open) _isDetailPaneOpen = open;
        _saveLayoutPrefs = save;
        NotifyOfPropertyChange(nameof(FenceSplitRatio));
        NotifyOfPropertyChange(nameof(IsDetailPaneOpen));
        NotifyOfPropertyChange(nameof(DetailToggleText));
    }

    private void SaveLayoutPrefs()
    {
        try { _saveLayoutPrefs?.Invoke(_fenceSplitRatio, _isDetailPaneOpen); }
        catch (Exception ex) { _log?.Warning($"[Wiring] 표시 설정 저장 실패(무시): {ex.Message}"); }
    }
    #endregion
}
