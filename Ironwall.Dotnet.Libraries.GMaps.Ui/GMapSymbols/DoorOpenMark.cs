using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;

/// <summary>
/// 통문 · 함체 <b>문 열림</b> 표지 — 심볼 좌상단의 작은 문 모양(둥근 사각 + 빈 문틀 + 비스듬히 열린 문짝)이 문이 열려 있는 동안 깜빡인다.
/// 2D 폴백 · 3D 하우징 템플릿이 같은 요소를 쓴다. 3D 문짝 그림(HousingVisual.DoorOpen)은 건드리지 않고 그 위에 얹는다.
/// </summary>
/// <remarks>
/// <para><b>기존 깜빡임과의 구별</b>: 탐지 = 빨강 펄스 링 · 장애/ERROR = 주황 점 · 아이콘 깜빡임 · 구역선 빨강/주황 교차.
/// 이 표지는 자리(좌상단)와 <b>모양</b>(문)이 다르고 색은 정보 파랑(<c>StatusInfoBrush</c>) — 빨강 · 주황 · 앰버(경고 · 미저장)를 쓰지 않는다.</para>
/// <para><b>우선순위</b>: 탐지(Detecting) · 장애(Fault) · 장비 ERROR 가 함께 걸리면 템플릿 트리거가 <see cref="IsSuppressed"/> 를 켠다 —
/// 그 깜빡임이 이기고, 문 표지는 깜빡이지 않은 채 계속 보인다.</para>
/// <para><b>비용</b>: 디스패처(스레드)마다 <see cref="AnimationClock"/> <b>하나</b>를 깜빡이는 표지 모두의 Opacity 에 건다 — 같은 박자,
/// 표지마다 타이머 없음. 마지막 표지가 빠지면 시계를 멈추고 뗀다. 그림은 값이 바뀔 때만 다시 그린다(깜빡임은 Opacity 합성뿐).
/// 숨김 · 모양 끔 · 미리보기 · 화면 밖(<see cref="IsInViewProperty"/>) · 시스템 "애니메이션 줄이기"면 깜빡이지 않는다(애니메이션 줄이기는 고정 표지).</para>
/// </remarks>
public sealed class DoorOpenMark : FrameworkElement
{
    /// <summary>표지 한 변(px, 마커 좌표) — 부품 배지(12px)보다 조금 크게.</summary>
    internal const double MarkSize = 14.0;
    /// <summary>아이콘 밖으로 걸치는 양 — 부품 층 배지와 같다.</summary>
    internal const double Overhang = ComponentStatusOverlay.Overhang;
    /// <summary>깜빡임 반 주기 — 탐지 펄스(1.2초) · ERROR(0.7초) · 방송(0.6초)과 다른 박자(1초 주기).</summary>
    internal static readonly Duration HalfPeriod = new(TimeSpan.FromSeconds(0.5));

    [ThreadStatic] private static AnimationClock? t_clock;
    [ThreadStatic] private static int t_clockUsers;
    [ThreadStatic] private static HashSet<DoorOpenMark>? t_blinking;
    /// <summary>시험용 — 시스템 "애니메이션 줄이기"를 대신한다(null = 시스템 값).</summary>
    [ThreadStatic] internal static bool? ReduceMotionOverride;
    /// <summary>시험용 — 창(PresentationSource) 없이도 공용 시계를 건다. 기본은 창에 붙은 표지만 시계를 건다.</summary>
    [ThreadStatic] internal static bool ClockWithoutWindow;
    private bool _clockApplied;

    public DoorOpenMark()
    {
        IsHitTestVisible = false;
        Focusable = false;
        SnapsToDevicePixels = true;
        SetResourceReference(SurfaceFillProperty, "SurfaceBrush");
        SetResourceReference(MarkBrushProperty, "StatusInfoBrush");
        IsVisibleChanged += (_, _) => UpdateBlink();
        Unloaded += (_, _) => StopBlink();
        Loaded += (_, _) => { InvalidateVisual(); UpdateBlink(); };   // 창 밖에서 바뀐 문 상태를 붙는 순간 그린다
    }

    #region DPs
    /// <summary>
    /// 알림용 문 상태(마커 <c>DoorStatus</c>) — 열림일 때만 표지를 그린다. 다시 그리기는 창에 붙었을 때만 요청한다:
    /// 창 없는 곳에서 렌더 패스를 일으키면 같은 스레드의 문짝 스토리보드 시계가 함께 돈다(EVM-108 "시계가 돌기 전" 전제).
    /// </summary>
    public static readonly DependencyProperty DoorProperty = DependencyProperty.Register(nameof(Door), typeof(DoorIndicatorKind),
        typeof(DoorOpenMark), new FrameworkPropertyMetadata(DoorIndicatorKind.None, OnDoorChanged));
    public DoorIndicatorKind Door { get => (DoorIndicatorKind)GetValue(DoorProperty); set => SetValue(DoorProperty, value); }

    /// <summary>탐지 · 장애 우선 — 켜지면 깜빡이지 않고 고정 표지.</summary>
    public static readonly DependencyProperty IsSuppressedProperty = DependencyProperty.Register(nameof(IsSuppressed), typeof(bool),
        typeof(DoorOpenMark), new FrameworkPropertyMetadata(false, OnBlinkInputChanged));
    public bool IsSuppressed { get => (bool)GetValue(IsSuppressedProperty); set => SetValue(IsSuppressedProperty, value); }

    /// <summary>
    /// 화면 안(상속) — 지도가 뷰포트가 바뀔 때 각 심볼 컨트롤에 걸고 템플릿 안의 표지로 상속된다. 기본 true(지도 밖 단독 사용).
    /// </summary>
    public static readonly DependencyProperty IsInViewProperty = DependencyProperty.RegisterAttached("IsInView", typeof(bool),
        typeof(DoorOpenMark), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits, OnBlinkInputChanged));
    public static bool GetIsInView(DependencyObject d) => (bool)d.GetValue(IsInViewProperty);
    public static void SetIsInView(DependencyObject d, bool value) => d.SetValue(IsInViewProperty, value);

    public static readonly DependencyProperty SurfaceFillProperty = DependencyProperty.Register(nameof(SurfaceFill), typeof(Brush),
        typeof(DoorOpenMark), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? SurfaceFill { get => (Brush?)GetValue(SurfaceFillProperty); set => SetValue(SurfaceFillProperty, value); }

    public static readonly DependencyProperty MarkBrushProperty = DependencyProperty.Register(nameof(MarkBrush), typeof(Brush),
        typeof(DoorOpenMark), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public Brush? MarkBrush { get => (Brush?)GetValue(MarkBrushProperty); set => SetValue(MarkBrushProperty, value); }
    #endregion

    /// <summary>표지를 그리는가(문 열림 · 보임) — 깜빡임과 무관.</summary>
    public bool IsDrawn => Door == DoorIndicatorKind.Open && Visibility == Visibility.Visible;

    /// <summary>지금 깜빡이는 상태인가(시험 판독) — 창에 붙어 있으면 공용 시계가 Opacity 를 움직인다.</summary>
    public bool IsBlinking => t_blinking?.Contains(this) == true;

    /// <summary>시험용 — 이 스레드의 공용 시계(깜빡이는 표지가 없으면 null).</summary>
    internal static AnimationClock? SharedClock => t_clock;

    private static bool AnimationsEnabled => ReduceMotionOverride is bool reduce ? !reduce : SystemParameters.ClientAreaAnimation;

    private static void OnBlinkInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DoorOpenMark mark) mark.UpdateBlink();
    }

    private static void OnDoorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DoorOpenMark mark) return;
        if (PresentationSource.FromVisual(mark) != null) mark.InvalidateVisual();
        mark.UpdateBlink();
    }
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == VisibilityProperty) UpdateBlink();
    }

    /// <summary>
    /// 깜빡일 조건을 다시 본다. 창 없는 곳(시험 · 오프스크린 렌더)에서는 IsVisible 이 늘 false 라 자기 Visibility 만 본다.
    /// 시계는 창에 붙은 표지만 건다 — 그려지지 않는 곳에서 Forever 시계가 렌더 틱을 돌려 같은 스레드의 다른 애니메이션을 밀지 않게.
    /// </summary>
    internal void UpdateBlink()
    {
        bool onScreen = PresentationSource.FromVisual(this) != null;
        bool shown = IsDrawn && (!onScreen || IsVisible);
        if (shown && !IsSuppressed && GetIsInView(this) && AnimationsEnabled)
        {
            (t_blinking ??= new HashSet<DoorOpenMark>()).Add(this);
            SetClock(onScreen || ClockWithoutWindow);
        }
        else StopBlink();
    }

    private void StopBlink()
    {
        t_blinking?.Remove(this);
        SetClock(false);
    }

    /// <summary>공용 시계를 걸거나 뗀다. 시계를 쓰는 표지가 하나도 없으면 시계를 멈추고 뗀다.</summary>
    private void SetClock(bool on)
    {
        if (on == _clockApplied) return;
        _clockApplied = on;
        if (on)
        {
            t_clockUsers++;
            t_clock ??= new DoubleAnimation(1.0, 0.25, HalfPeriod) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }.CreateClock();
            ApplyAnimationClock(OpacityProperty, t_clock);
            return;
        }
        ApplyAnimationClock(OpacityProperty, null);   // 원래 Opacity(1)로 돌아온다
        if (--t_clockUsers > 0 || t_clock == null) return;
        t_clock.Controller?.Stop();
        t_clock.Controller?.Remove();
        t_clock = null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Door != DoorIndicatorKind.Open) return;
        // 토큰이 병합되지 않은 곳에서도 형태는 보이게 — 시스템 기본 브러시로 폴백한다(캐시하지 않음).
        var surface = SurfaceFill ?? Brushes.White;
        var ink = MarkBrush ?? Brushes.SteelBlue;

        var rect = new Rect(-Overhang, -Overhang, MarkSize, MarkSize);
        dc.DrawRoundedRectangle(surface, new Pen(ink, 1.5), rect, 3, 3);
        var frame = new Rect(rect.X + 4, rect.Y + 2.5, 6, 9);
        dc.DrawRectangle(null, new Pen(ink, 1.2), frame);                                      // 빈 문틀
        var leaf = new StreamGeometry();                                                     // 바깥으로 비스듬히 열린 문짝
        using (var g = leaf.Open())
        {
            g.BeginFigure(new Point(frame.Left, frame.Top), true, true);
            g.LineTo(new Point(frame.Left - 2.5, frame.Top + 1.5), true, false);
            g.LineTo(new Point(frame.Left - 2.5, frame.Bottom + 1.2), true, false);
            g.LineTo(new Point(frame.Left, frame.Bottom), true, false);
        }
        leaf.Freeze();
        dc.DrawGeometry(ink, null, leaf);
    }
}
