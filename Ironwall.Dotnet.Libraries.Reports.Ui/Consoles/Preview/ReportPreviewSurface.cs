using Ironwall.Dotnet.Libraries.Utils.Consoles;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;

/// <summary>미리보기 자리에 무엇을 그릴 것인가.</summary>
public enum ReportPreviewMode
{
    /// <summary>살아 있는 WebView2 를 만든다.</summary>
    Live,

    /// <summary>네이티브 창을 만들지 않고 까닭이 적힌 자리표시자를 그린다.</summary>
    Placeholder,
}

/// <summary>고른 보고서가 미리보기로서 어떤 상태인가.</summary>
public enum ReportPreviewContent
{
    /// <summary>아무것도 고르지 않았다.</summary>
    NoSelection,

    /// <summary>아직 만들어지는 중(PENDING · GENERATING).</summary>
    InProgress,

    /// <summary>생성이 실패했다.</summary>
    Failed,

    /// <summary>생성이 취소됐다.</summary>
    Cancelled,

    /// <summary>HTML 을 받아오는 중.</summary>
    Loading,

    /// <summary>HTML 이 준비됐다.</summary>
    Ready,
}

/// <summary>미리보기 자리의 판정 결과 — 자리표시자면 <b>반드시 까닭이 있다</b>(빈 칸을 내지 않는다).</summary>
/// <param name="Mode">살아 있는 WebView2 인가, 자리표시자인가.</param>
/// <param name="Reason">자리표시자에 크게 적을 한 줄. <see cref="ReportPreviewMode.Live"/> 면 빈 글자.</param>
/// <param name="Hint">그 아래 작게 적을 안내(무엇을 하면 되는지). 없을 수 있다.</param>
public readonly record struct ReportPreviewSurface(ReportPreviewMode Mode, string Reason, string Hint)
{
    public bool IsLive => Mode == ReportPreviewMode.Live;
    public bool IsPlaceholder => Mode == ReportPreviewMode.Placeholder;
    public bool HasHint => !string.IsNullOrEmpty(Hint);

    public static ReportPreviewSurface Live { get; } = new(ReportPreviewMode.Live, string.Empty, string.Empty);

    public static ReportPreviewSurface Placeholder(string reason, string hint = "")
        => new(ReportPreviewMode.Placeholder, reason, hint);
}

/// <summary>
/// ★ 공역(airspace) 판정 — <b>순수 함수</b>. WebView2 는 네이티브 창이라 같은 최상위 창 안에서
/// 그 위에 WPF 를 못 그린다. 그래서 "언제 살아 있는 WebView2 를 만들어도 되는가" 를 한 곳에서 정한다.
/// (설계 정본 window-layout-system-storyboard.html L1290 · 인벤토리 §3 L30)
/// </summary>
/// <remarks>
/// <para>세 가지가 미리보기를 내린다.</para>
/// <list type="number">
///   <item>서랍 · 접힘(창 폭 1280 미만) — 상세가 목록 <b>위로</b> 밀려 나오며 스크림을 덮는다.
///         네이티브 표면을 거기 두면 스크림과 목록을 뚫는다(WL L982-983).</item>
///   <item>같은 창의 WPF 오버레이(확인 · 안내 · 진행 팝업) — 미리보기가 <b>팝업을 가린다</b>.
///         이미 한 번 당한 결함이다(다운로드 실패 안내가 미리보기 뒤로 깔렸다).</item>
///   <item>크게 보기 창이 열려 있다 — 같은 HTML 을 두 번 그릴 까닭이 없고, 그 창은 별도 HWND 라
///         여기 남은 미리보기가 오히려 방해가 된다.</item>
/// </list>
/// <para>런타임이 없을 수도 있다 — 그때도 콘솔은 정상이어야 하므로 까닭을 적고 PDF 로 유도한다.</para>
/// <para>UI 에 기대지 않으므로 헤드리스로 전수 단언할 수 있다 — 드래그 · 레이아웃과 같은 원칙이다.</para>
/// </remarks>
public static class ReportPreviewSurfaceRules
{
    public const string NoSelectionReason = "보고서를 고르면 여기에 미리보기가 나옵니다";
    public const string RuntimeMissingReason = "이 PC 에 WebView2 런타임이 없어 미리보기를 그릴 수 없습니다";
    public const string RuntimeMissingHint = "[PDF 내려받기] 로 내용을 확인하세요";
    public const string LargeViewReason = "큰 창에서 보고 있습니다";
    public const string LargeViewHint = "그 창을 닫으면 미리보기가 이 자리로 돌아옵니다";
    public const string OverlayReason = "확인 창이 열려 있는 동안 미리보기를 잠시 내렸습니다";
    public const string OverlayHint = "미리보기가 네이티브 창이라 확인 창을 가리기 때문입니다";
    // 짧게 둔다 — WPF 는 한글을 음절 단위로 끊으므로, 좁은 칸에서 긴 문장은 마지막 줄에
    // 음절 하나만 남긴다(접힘 900 에서 "…없습니 / 다" 로 갈라졌다).
    public const string NarrowReason = "창이 좁아 여기서는 미리보기를 못 그립니다";
    public const string NarrowHint = "[크게 보기] 를 누르면 큰 창으로 봅니다";
    public const string InProgressReason = "아직 만들어지는 중입니다";
    public const string InProgressHint = "아래 진행 상황이 단계와 퍼센트를 보여 줍니다";
    public const string FailedReason = "생성에 실패한 보고서입니다";
    public const string FailedHint = "아래 사유를 확인하고 다시 생성하세요";
    public const string CancelledReason = "생성이 취소된 보고서입니다";
    public const string CancelledHint = "[새 보고서] 에서 다시 만들 수 있습니다";
    public const string LoadingReason = "미리보기를 불러오는 중입니다";

    /// <summary>
    /// 미리보기 자리를 정한다.
    /// </summary>
    /// <param name="layout">콘솔의 폭 판정(<see cref="ConsoleLayoutMath.Resolve"/> 결과).</param>
    /// <param name="isLargeViewOpen">[크게 보기] 창이 열려 있는가.</param>
    /// <param name="isBlockingOverlayOpen">같은 창에 WPF 팝업(확인 · 안내 · 진행)이 떠 있는가.</param>
    /// <param name="isRuntimeReady">WebView2 런타임을 초기화할 수 있는가.</param>
    /// <param name="content">고른 보고서의 미리보기 상태.</param>
    public static ReportPreviewSurface Resolve(ConsoleLayoutMode layout,
                                               bool isLargeViewOpen,
                                               bool isBlockingOverlayOpen,
                                               bool isRuntimeReady,
                                               ReportPreviewContent content)
    {
        // 고른 것이 없으면 공역을 따질 일이 없다 — 가장 쓸모 있는 안내를 먼저 낸다.
        if (content == ReportPreviewContent.NoSelection)
            return ReportPreviewSurface.Placeholder(NoSelectionReason);

        // 아래 넷은 "그릴 수 없다" 는 사실이라 내용 상태보다 앞선다.
        if (!isRuntimeReady)
            return ReportPreviewSurface.Placeholder(RuntimeMissingReason, RuntimeMissingHint);

        if (isLargeViewOpen)
            return ReportPreviewSurface.Placeholder(LargeViewReason, LargeViewHint);

        if (isBlockingOverlayOpen)
            return ReportPreviewSurface.Placeholder(OverlayReason, OverlayHint);

        if (layout != ConsoleLayoutMode.Docked)
            return ReportPreviewSurface.Placeholder(NarrowReason, NarrowHint);

        return content switch
        {
            ReportPreviewContent.InProgress => ReportPreviewSurface.Placeholder(InProgressReason, InProgressHint),
            ReportPreviewContent.Failed => ReportPreviewSurface.Placeholder(FailedReason, FailedHint),
            ReportPreviewContent.Cancelled => ReportPreviewSurface.Placeholder(CancelledReason, CancelledHint),
            ReportPreviewContent.Loading => ReportPreviewSurface.Placeholder(LoadingReason),
            _ => ReportPreviewSurface.Live,
        };
    }

    /// <summary>
    /// 크게 보기 창을 열 수 있는가 — <b>HTML 을 다 받은</b> 보고서이고 런타임이 있을 때만.
    /// 받는 중에 열면 빈 창이 뜨고, 그러면서 상세 칸 미리보기까지 자리표시자로 내려간다.
    /// 좁은 창에서도 <b>열 수 있다</b>(별도 HWND 라 공역 제약을 받지 않는다 — 그것이 이 창의 존재 이유다).
    /// </summary>
    public static bool CanOpenLargeView(bool isRuntimeReady, ReportPreviewContent content)
        => isRuntimeReady && content == ReportPreviewContent.Ready;
}
