using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Utils.Consoles;

/****************************************************************************
   Purpose      : 콘솔 하나를 셸 안에서 옮기고 크기를 바꾸는 틀 (셸 표면 · N-14 · D-09/D-10 라이브러리 승격)
   Created By   : Claude (N-14 셸 표면 · D-09/D-10 라이브러리 승격)
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 콘솔을 감싸는 틀. 머리의 손잡이를 끌면 옮겨지고, 모서리를 끌면 크기가 바뀌며, 그 자리를 기억한다.
///
/// <para><b>손잡이만으로 시작한다</b>(<c>drag-first-ux.md</c> §Must Always): 머리 전체를 드래그 존으로 만들면
/// 그 위의 닫기 단추를 삼킨다. 게다가 UIA <c>InvokePattern</c> 은 히트테스트를 우회하므로
/// <b>하네스만 초록이고 사람 손은 안 되는</b> 상태가 된다.</para>
///
/// <para><b>왜 <see cref="Thumb"/> 인가</b>: 자동화 peer 가 실재하고, 캡처와 그 상실을 스스로 다루며,
/// 종료를 <see cref="Thumb.DragCompleted"/> <b>하나</b>로 알린다(마우스 업 · 캡처 상실 · <see cref="Thumb.CancelDrag"/> 전부).
/// <c>Button</c> 계열은 버블에서 캡처를 빼앗아 마우스다운 순간 드래그가 죽는다.</para>
///
/// <para><b>증분 좌표 함정</b>: <see cref="DragDeltaEventArgs"/> 의 값은 <b>손잡이 기준</b>이다. 손잡이가 표면과 같이
/// 움직이는 이 틀에서는 그 값이 누적이 아니라 증분이 되어 떨린다. 움직이지 않는 캔버스 기준으로 직접 잰다
/// (커널 <c>ConsoleShell</c> 의 스플리터가 같은 이유로 같은 일을 한다).</para>
///
/// <para><b>자리를 기억하는 저장소는 인터페이스로 받는다</b>(<see cref="ISurfaceLayoutStore"/>) — 파일 경로 ·
/// 스키마는 호스트마다 다르고, 이 라이브러리는 그것을 알 필요가 없다.</para>
///
/// <para><b>금지 구역</b>(<see cref="LeftInset"/>/<see cref="RightInset"/>)은 이 타입이 정하지 않는다 — 호출부가
/// 상수를 줄 수도, 셸의 실측 폭을 매 레이아웃마다 다시 재서 줄 수도 있다. 이 틀은 <see cref="Area"/> 를 통해
/// 그 값을 <see cref="SurfaceMath"/> 의 순수 함수에 넘기기만 한다.</para>
/// </summary>
[TemplatePart(Name = PartMove, Type = typeof(Thumb))]
public class SurfaceFrame : ContentControl
{
    private const string PartMove = "PART_Move";

    /// <summary>모서리 손잡이 이름 — 템플릿이 이 이름으로 <see cref="Thumb"/> 를 둔다.</summary>
    private static readonly (string Part, SurfaceEdge Edge)[] ResizeParts =
    {
        ("PART_ResizeLeft", SurfaceEdge.Left),
        ("PART_ResizeRight", SurfaceEdge.Right),
        ("PART_ResizeTop", SurfaceEdge.Top),
        ("PART_ResizeBottom", SurfaceEdge.Bottom),
        ("PART_ResizeTopLeft", SurfaceEdge.TopLeft),
        ("PART_ResizeTopRight", SurfaceEdge.TopRight),
        ("PART_ResizeBottomLeft", SurfaceEdge.BottomLeft),
        ("PART_ResizeBottomRight", SurfaceEdge.BottomRight),
    };

    private readonly List<(Thumb Thumb, SurfaceEdge Edge)> _hooked = new();

    private Canvas? _canvas;
    private Thumb? _move;

    private Thumb? _active;
    private bool _placed;
    private DispatcherTimer? _saveTimer;

    private bool _pressed;
    private bool _dragging;
    private SurfaceEdge _edge;
    private Point _pointerAtPress;
    private SurfaceBounds _boundsAtPress;

    static SurfaceFrame()
    {
        FocusableProperty.OverrideMetadata(typeof(SurfaceFrame), new FrameworkPropertyMetadata(false));
    }

    #region - Properties -
    public static readonly DependencyProperty SurfaceKeyProperty = DependencyProperty.Register(
        nameof(SurfaceKey), typeof(string), typeof(SurfaceFrame),
        new PropertyMetadata(string.Empty, (d, _) => ((SurfaceFrame)d).OnSurfaceKeyChanged()));
    /// <summary>자리를 기억할 때 쓰는 이름. 비면 이 틀은 아무 일도 하지 않는다.</summary>
    public string SurfaceKey { get => (string)GetValue(SurfaceKeyProperty); set => SetValue(SurfaceKeyProperty, value); }

    public static readonly DependencyProperty IsSurfaceProperty = DependencyProperty.Register(
        nameof(IsSurface), typeof(bool), typeof(SurfaceFrame),
        new PropertyMetadata(false, (d, _) => ((SurfaceFrame)d).OnSurfaceChanged()));
    /// <summary>옮길 수 있는 표면인가 — 로그인 · 진행 팝업 같은 흐름은 거짓이다.</summary>
    public bool IsSurface { get => (bool)GetValue(IsSurfaceProperty); set => SetValue(IsSurfaceProperty, value); }

    public static readonly DependencyProperty CanResizeProperty = DependencyProperty.Register(
        nameof(CanResize), typeof(bool), typeof(SurfaceFrame), new PropertyMetadata(false));
    /// <summary>크기를 바꿀 수 있는가 — 내용 뷰가 자기 크기를 못 박으면 거짓이다.</summary>
    public bool CanResize { get => (bool)GetValue(CanResizeProperty); set => SetValue(CanResizeProperty, value); }

    public static readonly DependencyProperty LeftInsetProperty = DependencyProperty.Register(
        nameof(LeftInset), typeof(double), typeof(SurfaceFrame), new PropertyMetadata(0d));
    /// <summary>
    /// 좌측 금지 구역(예: 햄버거 메뉴 스트립) — 이 위로는 표면이 갈 수 없다.
    /// 기본값은 0 이다 — <b>이 타입은 셸의 모양을 모른다.</b> 호출부(호스트)가 상수든 실측이든 채워 넣는다.
    /// </summary>
    public double LeftInset { get => (double)GetValue(LeftInsetProperty); set => SetValue(LeftInsetProperty, value); }

    public static readonly DependencyProperty RightInsetProperty = DependencyProperty.Register(
        nameof(RightInset), typeof(double), typeof(SurfaceFrame), new PropertyMetadata(0d));
    /// <summary>
    /// 우측 금지 구역(예: 이벤트 드로어) — 이 위로 가면 라이브 알람이 완전히 가려진다.
    /// 기본값은 0 이다 — 호출부가 실측을 넣지 않으면 표면은 전체 폭을 쓸 수 있는 것으로 간주된다.
    /// </summary>
    public double RightInset { get => (double)GetValue(RightInsetProperty); set => SetValue(RightInsetProperty, value); }

    public static readonly DependencyProperty MinSurfaceWidthProperty = DependencyProperty.Register(
        nameof(MinSurfaceWidth), typeof(double), typeof(SurfaceFrame),
        new PropertyMetadata(SurfaceMath.AbsoluteMinWidth));
    public double MinSurfaceWidth { get => (double)GetValue(MinSurfaceWidthProperty); set => SetValue(MinSurfaceWidthProperty, value); }

    public static readonly DependencyProperty MinSurfaceHeightProperty = DependencyProperty.Register(
        nameof(MinSurfaceHeight), typeof(double), typeof(SurfaceFrame),
        new PropertyMetadata(SurfaceMath.AbsoluteMinHeight));
    public double MinSurfaceHeight { get => (double)GetValue(MinSurfaceHeightProperty); set => SetValue(MinSurfaceHeightProperty, value); }

    /// <summary>처음 열릴 때의 크기(기억해 둔 것이 없을 때).</summary>
    public static readonly DependencyProperty PreferredWidthProperty = DependencyProperty.Register(
        nameof(PreferredWidth), typeof(double), typeof(SurfaceFrame), new PropertyMetadata(800d));
    public double PreferredWidth { get => (double)GetValue(PreferredWidthProperty); set => SetValue(PreferredWidthProperty, value); }

    public static readonly DependencyProperty PreferredHeightProperty = DependencyProperty.Register(
        nameof(PreferredHeight), typeof(double), typeof(SurfaceFrame), new PropertyMetadata(500d));
    public double PreferredHeight { get => (double)GetValue(PreferredHeightProperty); set => SetValue(PreferredHeightProperty, value); }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SurfaceFrame), new PropertyMetadata(string.Empty));
    /// <summary>
    /// 머리에 적힐 이름. <b>비워 두면 머리가 빈 띄가 된다</b> — 처음 그렸을 때 실제로 그렇게 나왔고,
    /// 무엇을 잡고 있는지 알 수 없어 렌더 결함처럼 보였다.
    /// </summary>
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>
    /// 틀의 이동 손잡이가 <b>콘솔 머리 위에 겹쳐 앉는</b> 폭(X1 — 창 제목 두 겹 제거). <b>상속</b>이라 틀이 한 번 주면
    /// 그 안의 콘솔 머리(커널 <see cref="ConsoleShell"/> 템플릿 · 호스트 카드 머리)가 이 값만큼 제목을 오른쪽으로 비킨다.
    /// 기본 0 — 틀 밖(미리보기 · 별도 창)의 콘솔은 아무것도 비키지 않는다.
    /// <see cref="ConsoleShell"/> 은 자기 본문에서 이 값을 0 으로 끊는다 — 안에 든 다른 콘솔 머리까지 비키면 안 된다.
    /// </summary>
    public static readonly DependencyProperty HeadGripWidthProperty = DependencyProperty.RegisterAttached(
        "HeadGripWidth", typeof(double), typeof(SurfaceFrame),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.Inherits));

    public static double GetHeadGripWidth(DependencyObject element)
        => (double)(element ?? throw new ArgumentNullException(nameof(element))).GetValue(HeadGripWidthProperty);

    public static void SetHeadGripWidth(DependencyObject element, double value)
        => (element ?? throw new ArgumentNullException(nameof(element))).SetValue(HeadGripWidthProperty, value);

    /// <summary>자리를 기억하는 곳. 시험은 가짜 구현을 준다.</summary>
    public ISurfaceLayoutStore? Store { get; set; }
    #endregion

    #region - 자리 -
    /// <summary>지금 자리.</summary>
    public SurfaceBounds Bounds
        => new(Canvas.GetLeft(this) is var x && double.IsFinite(x) ? x : 0,
               Canvas.GetTop(this) is var y && double.IsFinite(y) ? y : 0,
               double.IsFinite(Width) ? Width : ActualWidth,
               double.IsFinite(Height) ? Height : ActualHeight);

    /// <summary>표면이 놓일 수 있는 자리 — 캔버스에서 금지 구역을 뺀 것.</summary>
    public SurfaceArea Area
        => _canvas is null
            ? SurfaceArea.Of(0, 0)
            : SurfaceArea.Of(_canvas.ActualWidth, _canvas.ActualHeight, LeftInset, RightInset);

    /// <summary>자리를 적용한다 — 좌표는 <b>로컬 값으로 직접</b> 쓴다(Style Setter 로는 TwoWay 가 성립하지 않는다).</summary>
    public void ApplyBounds(SurfaceBounds bounds)
    {
        if (!bounds.IsUsable) return;

        Canvas.SetLeft(this, bounds.X);
        Canvas.SetTop(this, bounds.Y);
        Width = bounds.Width;
        Height = bounds.Height;
    }

    /// <summary>
    /// 처음 자리를 정한다 — 기억해 둔 것이 있으면 지금 셸에 맞춰 되살리고, 없으면 가운데.
    /// 되살릴 수 없으면(모니터가 빠졌거나 셸이 줄었거나) 기본 자리로 돌린다.
    /// </summary>
    public void RestorePlace()
    {
        if (!IsSurface || _canvas is null || Area.IsDegenerate) return;

        var fallback = SurfaceMath.Center(PreferredWidth, PreferredHeight, Area);
        var remembered = string.IsNullOrEmpty(SurfaceKey) ? null : Store?.TryGet(SurfaceKey);

        ApplyBounds(remembered is { } saved ? SurfaceMath.Recover(saved, Area, fallback) : fallback);
        _placed = true;
    }

    /// <summary>셸이 바뀌었다(크기 · DPI · 모니터 · 금지 구역 실폭) — 자리만 다시 민다. 크기는 자리에 안 맞을 때만 줄인다.</summary>
    public void Reclamp()
    {
        if (!IsSurface || Area.IsDegenerate) return;

        // 아직 자리를 되살리지 못했으면(첫 레이아웃 전에 SizeChanged 가 온다) 기억부터 읽는다 —
        // 그러지 않으면 기억해 둔 자리가 가운데로 덮이고 다음 드래그에 그게 저장된다.
        if (!_placed) { RestorePlace(); return; }

        var fallback = SurfaceMath.Center(PreferredWidth, PreferredHeight, Area);
        ApplyBounds(SurfaceMath.Recover(Bounds, Area, fallback));
    }
    #endregion

    #region - 배선 -
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        Unhook();

        _move = GetTemplateChild(PartMove) as Thumb;
        if (_move is not null) Hook(_move, SurfaceEdge.None);

        foreach (var (part, edge) in ResizeParts)
            if (GetTemplateChild(part) is Thumb thumb) Hook(thumb, edge);

        // 키 처리는 틀에 건다 — 손잡이에만 걸면 마우스 크기 조절 중의 Esc 가 닿지 않는다
        // (모서리 손잡이는 초점을 받지 않는다). PreviewKeyDown 은 터널이라 자식의 키도 먼저 본다.
        PreviewKeyDown -= OnHandleKeyDown;
        PreviewKeyDown += OnHandleKeyDown;

        ApplyAutomationIds();
    }

    /// <summary>
    /// 자동화 식별자를 붙인다. <b>템플릿을 얻은 때와 키가 바뀜 때 둘 다</b> 에서 부른다 —
    /// 템플릿은 층이 처음 보일 때 한 번 적용되는데 그때의 <see cref="SurfaceKey"/> 는 보통 비어 있다
    /// (첫 화면은 로그인 패널 — 표면이 아니다). 그때 한 번만 붙이면 아홉 식별자가 전부
    /// <c>Shell.Surface..Move</c> 처럼 빈 키로 굳어 버린다.
    /// </summary>
    private void ApplyAutomationIds()
    {
        if (_move is not null)
        {
            AutomationProperties.SetAutomationId(_move, SurfaceKeyIds.MoveId(SurfaceKey));
            AutomationProperties.SetName(_move, "창 옮기기 손잡이");
        }

        foreach (var (thumb, edge) in _hooked)
        {
            if (edge == SurfaceEdge.None) continue;
            AutomationProperties.SetAutomationId(thumb, SurfaceKeyIds.ResizeId(SurfaceKey, edge));
        }
    }

    private void Hook(Thumb thumb, SurfaceEdge edge)
    {
        thumb.DragStarted += OnDragStarted;
        thumb.DragDelta += OnDragDelta;
        thumb.DragCompleted += OnDragCompleted;
        _hooked.Add((thumb, edge));
    }

    private void Unhook()
    {
        foreach (var (thumb, _) in _hooked)
        {
            thumb.DragStarted -= OnDragStarted;
            thumb.DragDelta -= OnDragDelta;
            thumb.DragCompleted -= OnDragCompleted;
        }
        _hooked.Clear();

        PreviewKeyDown -= OnHandleKeyDown;
        _move = null;
        _active = null;
    }

    private void OnSurfaceKeyChanged()
    {
        ApplyAutomationIds();
        OnSurfaceChanged();
    }

    private void OnSurfaceChanged()
    {
        _canvas = Parent as Canvas;
        if (IsSurface) RestorePlace();
    }

    protected override void OnVisualParentChanged(DependencyObject oldParent)
    {
        base.OnVisualParentChanged(oldParent);
        _canvas = Parent as Canvas;
    }
    #endregion

    #region - 드래그 -
    /// <summary>움직이지 않는 캔버스 기준의 지금 포인터 위치. 커널의 시험 이음매를 그대로 존중한다.</summary>
    private Point Pointer()
    {
        if (_canvas is null) return new Point(0, 0);
        var over = DragPointer.Override;
        return over is not null ? over(_canvas) : Mouse.GetPosition(_canvas);
    }

    /// <summary>이 손잡이가 어느 모서리인가 — 이동 손잡이면 <see cref="SurfaceEdge.None"/>.</summary>
    private SurfaceEdge EdgeOf(object sender)
    {
        foreach (var (thumb, edge) in _hooked)
            if (ReferenceEquals(thumb, sender)) return edge;
        return SurfaceEdge.None;
    }

    private void OnDragStarted(object sender, DragStartedEventArgs e)
    {
        if (!IsSurface || _canvas is null) return;

        var edge = EdgeOf(sender);
        if (edge != SurfaceEdge.None && !CanResize) return;

        _active = sender as Thumb;
        _pressed = true;
        _dragging = false;
        _edge = edge;
        _pointerAtPress = Pointer();
        _boundsAtPress = Bounds;
    }

    private void OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_pressed || _canvas is null) return;

        // e.HorizontalChange 는 쓰지 않는다 — 손잡이 기준 좌표라 손잡이가 같이 움직이면 증분이 된다.
        var now = Pointer();
        var dx = now.X - _pointerAtPress.X;
        var dy = now.Y - _pointerAtPress.Y;

        if (!_dragging)
        {
            if (!DragMath.IsDrag(dx, dy)) return;   // 8.0 DIU 데드존 — 그 전에는 클릭이다
            _dragging = true;
        }

        ApplyBounds(_edge == SurfaceEdge.None
            ? SurfaceMath.Move(_boundsAtPress, dx, dy, Area)
            : SurfaceMath.Resize(_boundsAtPress, _edge, dx, dy, Area, MinSurfaceWidth, MinSurfaceHeight));
    }

    private void OnDragCompleted(object sender, DragCompletedEventArgs e) => FinishDrag(commit: !e.Canceled);

    /// <summary>
    /// 종료 — 마우스 업 · 캡처 상실 · <c>Esc</c> 가 전부 이리로 온다(<see cref="Thumb"/> 가 셋 다 같은 사건으로 알린다).
    /// 순서: ① 플래그 ② 시각(자리) 복원 ③ 통지(저장). 저장을 먼저 하면 그 안에서 재진입한다.
    /// 캡처는 <see cref="Thumb"/> 가 스스로 푼다 — 여기서 풀면 두 번 푸는 것이 된다.
    /// </summary>
    private void FinishDrag(bool commit)
    {
        var wasDragging = _dragging;
        _active = null;
        _pressed = false;
        _dragging = false;
        _edge = SurfaceEdge.None;

        if (!wasDragging) return;          // 데드존을 못 넘었다 = 클릭이었다

        if (!commit)
        {
            ApplyBounds(_boundsAtPress);   // Esc · 캡처 상실 = 끌기 전 자리로
            return;
        }

        Remember();
    }

    /// <summary>자리를 적어 둔다. 실패해도 앱을 멈추지 않는다.</summary>
    private void Remember()
    {
        if (string.IsNullOrEmpty(SurfaceKey) || Store is null) return;

        Store.Set(SurfaceKey, Bounds);

        // 디스크 쓰기는 몰아서 한 번 — 화살표를 누른 채 두면 1 DIU 마다 temp 파일 + 교체가 돌아
        // 초당 수십 번의 파일 쓰기가 된다. 값은 이미 기억에 들어갔으므로 잃어버리지 않는다.
        _saveTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background,
                                           (_, _) => FlushSave(), Dispatcher);
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void FlushSave()
    {
        _saveTimer?.Stop();
        Store?.Save();
    }
    #endregion

    #region - 키보드 -
    /// <summary>
    /// 손잡이에 초점이 있을 때 화살표로 같은 일을 한다 — 드래그 전용 UI 를 내지 않는다.
    /// <c>Shift</c> 는 크기, <c>Ctrl</c> 은 큰 걸음. <c>Esc</c> 는 드래그 중일 때만 소비한다.
    /// </summary>
    private void OnHandleKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!_pressed) return;         // 드래그 중이 아니면 다른 것이 쓰게 둔다
            _active?.CancelDrag();   // 이동이든 크기 조절이든 잡고 있는 그것을 취소한다
            e.Handled = true;
            return;
        }

        if (!IsSurface) return;

        var (dirX, dirY) = e.Key switch
        {
            Key.Left => (-1, 0),
            Key.Right => (1, 0),
            Key.Up => (0, -1),
            Key.Down => (0, 1),
            _ => (0, 0),
        };
        if (dirX == 0 && dirY == 0) return;

        var coarse = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var resize = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        if (resize && !CanResize) return;

        ApplyBounds(resize
            ? SurfaceMath.KeyboardResize(Bounds, dirX, dirY, coarse, Area, MinSurfaceWidth, MinSurfaceHeight)
            : SurfaceMath.KeyboardMove(Bounds, dirX, dirY, coarse, Area));

        Remember();
        e.Handled = true;
    }
    #endregion
}

/// <summary>
/// 자동화 식별자 조립 — <c>Shell.Surface.{key}.Move</c> / <c>...Resize.{edge}</c>.
/// 키가 비면 빈 글자를 돌려준다 — 반쪽 식별자를 붙이면 하네스가 찾지 못하면서도 있는 것처럼 보인다.
/// </summary>
public static class SurfaceKeyIds
{
    public static string MoveId(string? surfaceKey)
        => string.IsNullOrWhiteSpace(surfaceKey) ? string.Empty : $"Shell.Surface.{surfaceKey}.Move";

    public static string ResizeId(string? surfaceKey, SurfaceEdge edge)
        => string.IsNullOrWhiteSpace(surfaceKey) || edge == SurfaceEdge.None
            ? string.Empty
            : $"Shell.Surface.{surfaceKey}.Resize.{edge}";
}
