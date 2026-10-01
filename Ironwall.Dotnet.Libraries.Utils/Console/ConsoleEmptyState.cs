using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/// <summary>
/// 빈 목록 자리 — 글리프 + 한 줄 제목 + 한 줄 힌트 (+ 선택적 단추 한 자리).
/// </summary>
/// <remarks>
/// D-33(측정) — 네 콘솔이 걸릴 것이 없을 때 본문을 <b>완전히 비운 채</b> 두고 30px 상태 띠 한 줄만 남겼다
/// (잉크 0.0%). 빈 화면은 "없음" 과 "덜 불러옴" 과 "고장" 을 구분해 주지 못한다.
/// <para>
/// 커널은 자리만 만든다 — 어느 콘솔에 붙일지는 그 창이 정한다(이 클래스는 어떤 View 도 건드리지 않는다).
/// 문구는 창이 넣는다: <see cref="Title"/> 은 무엇이 없는지, <see cref="Hint"/> 는 다음에 무엇을 하면 되는지.
/// </para>
/// </remarks>
public class ConsoleEmptyState : Control
{
    /// <summary>글리프를 주지 않았을 때 쓰는 기본 도형 — 빈 상자(테두리만).</summary>
    private static readonly Geometry DefaultGlyph =
        Geometry.Parse("M3,7 L16,2 L29,7 L29,23 L16,28 L3,23 z M3,7 L16,12 L29,7 M16,12 L16,28");

    static ConsoleEmptyState()
        => DefaultStyleKeyProperty.OverrideMetadata(typeof(ConsoleEmptyState), new FrameworkPropertyMetadata(typeof(ConsoleEmptyState)));

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(Geometry), typeof(ConsoleEmptyState), new PropertyMetadata(DefaultGlyph));
    /// <summary>가운데 도형. 색이 아니라 형태가 뜻을 나른다 — 굵은 채움이 아니라 얇은 윤곽선으로 그린다.</summary>
    public Geometry Glyph { get => (Geometry)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ConsoleEmptyState), new PropertyMetadata(string.Empty));
    /// <summary>무엇이 없는가 — "등록된 장비가 없습니다".</summary>
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public static readonly DependencyProperty HintProperty = DependencyProperty.Register(
        nameof(Hint), typeof(string), typeof(ConsoleEmptyState), new PropertyMetadata(string.Empty));
    /// <summary>다음에 무엇을 하면 되는가 — 한 줄. 비우면 숨는다. 사용법(<see cref="ConsoleHintKind.Usage"/>)이면 화면에 두지 않는다.</summary>
    public string Hint { get => (string)GetValue(HintProperty); set => SetValue(HintProperty, value); }

    public static readonly DependencyProperty HintKindProperty = DependencyProperty.Register(
        nameof(HintKind), typeof(ConsoleHintKind), typeof(ConsoleEmptyState), new PropertyMetadata(ConsoleHintKind.Action));
    /// <summary>
    /// 힌트의 종류(help-callout PRD FR-05) — 할 일 · 실패 · 권한 · 검색 결과 없음은 화면에 남고,
    /// 사용법(<see cref="ConsoleHintKind.Usage"/>)은 화면에서 빠진다(그 설명은 섹션 · 창의 "?" 몫). 기본은 할 일(예전처럼 보인다).
    /// </summary>
    public ConsoleHintKind HintKind { get => (ConsoleHintKind)GetValue(HintKindProperty); set => SetValue(HintKindProperty, value); }

    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(
        nameof(Action), typeof(object), typeof(ConsoleEmptyState), new PropertyMetadata(null));
    /// <summary>선택적 단추 한 자리(예: [등록] · [갱신]). 비우면 숨는다 — 권한이 없으면 아예 주지 않는다.</summary>
    public object? Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }
}

/// <summary>빈 자리 힌트의 종류 — 무엇이 화면에 남고 무엇이 "?" 로 가는가(help-callout PRD §2 · FR-05).</summary>
public enum ConsoleHintKind
{
    /// <summary>다음에 할 일 — "[등록] 으로 추가하세요". 화면에 남는다.</summary>
    Action,
    /// <summary>조회 실패 · 오류. 화면에 남는다.</summary>
    Failure,
    /// <summary>권한 거부. 화면에 남는다.</summary>
    Permission,
    /// <summary>검색 · 필터 결과 없음. 화면에 남는다.</summary>
    Search,
    /// <summary>사용법 · 원리 설명. 화면에서 빠진다 — 섹션 · 창 "?" 로 옮긴다.</summary>
    Usage,
}
