using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 높이 손잡이가 있는 판(「건물 높이」 · 「건물 만들기」)의 공용 바탕 — 머리 띠 끌기(OverlayHeaderDrag) · 닫기(✕) 명령 ·
/// 높이 손잡이(Thumb, AutomationId = <see cref="HandleAutomationId"/>) 끌기 · 판 안 키(Enter · ↑/↓ · Ctrl+Z · Esc)를
/// <see cref="Host"/>(IBuildingHeightPanelHost)로 넘긴다(x:Name 없이).
/// </summary>
public abstract class HeightPanelControlBase : Control
{
    private Thumb? _dragThumb;

    protected HeightPanelControlBase(string automationId, string name)
    {
        Width = 320;
        AutomationProperties.SetAutomationId(this, automationId);
        AutomationProperties.SetName(this, name);
        Helpers.OverlayHeaderDrag.Attach(this, 38);   // 머리 띠(38)만 끌기
        PreviewKeyDown += OnPreviewKeyDown;
        AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnHandleDragStarted));
        AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnHandleDragDelta));
        AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnHandleDragCompleted));
        // Tab 으로 스크롤 안 칸에 들어가면 그 칸이 보이게 — 기본 BringIntoView 는 논리 포커스(GotFocus) 길이라 콤보 안쪽 등에서 빠질 수 있다
        AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) => (e.OriginalSource as FrameworkElement)?.BringIntoView()));
    }

    /// <summary>판 높이 상한 = 지도(Canvas) 높이의 이 비율(2026-10-22 사용자 "너무 길어 높이 줄이고 스크롤" — 1920×1032 에서 약 640 px, 1366×768 에서 약 450 px).</summary>
    public const double FitMaxRatio = 0.7;
    /// <summary>판 끝과 지도 아래 가장자리 사이 여백(px) — 판을 아래로 끌어 놓아도 행동 줄이 지도 안에 남는다.</summary>
    public const double FitBottomMargin = 8;
    /// <summary>판이 줄어드는 가장 작은 높이(px) — 머리줄 · 행동 줄 · 내용 몇 줄은 보인다.</summary>
    public const double FitMinHeight = 200;
    private Canvas? _fitCanvas;

    /// <summary>
    /// 판 높이를 <see cref="FitHeight"/> 로 자른다 — 머리줄 · 행동 줄은 템플릿에서 고정이고 그 사이 내용만 ScrollViewer 가 스크롤한다.
    /// Canvas 는 자식을 끝없는 높이로 재므로 그대로 두면 판이 화면 밖까지 길어졌다(2026-10-22 헤디드 실측 — 1920×1032 에서 아래 단추가 화면 밖).
    /// </summary>
    protected override Size MeasureOverride(Size constraint)
    {
        if (FitHeight() is double h && h < constraint.Height) constraint = new Size(constraint.Width, h);
        return base.MeasureOverride(constraint);
    }

    /// <summary>지도 Canvas 안에서 판이 쓸 높이 = min(지도 × 0.7, 판 자리 아래 남은 높이 − 8), 최소 200 — Canvas 안이 아니거나 지도 크기를 아직 모르면 null(자르지 않는다).</summary>
    internal double? FitHeight()
    {
        if (VisualTreeHelper.GetParent(this) is not ContentPresenter cp || VisualTreeHelper.GetParent(cp) is not Canvas canvas) return null;
        if (!ReferenceEquals(canvas, _fitCanvas))
        {
            if (_fitCanvas != null) _fitCanvas.SizeChanged -= OnFitCanvasSizeChanged;
            _fitCanvas = canvas;
            canvas.SizeChanged += OnFitCanvasSizeChanged;   // 창 크기가 바뀌면 다시 잰다(Canvas 는 같은 끝없는 높이로 자식을 다시 재지 않는다)
        }
        if (canvas.ActualHeight <= 0) return null;
        double top = Canvas.GetTop(cp);
        if (double.IsNaN(top)) top = 0;
        return Math.Max(FitMinHeight, Math.Min(canvas.ActualHeight * FitMaxRatio, canvas.ActualHeight - top - FitBottomMargin));
    }

    private void OnFitCanvasSizeChanged(object sender, SizeChangedEventArgs e) => InvalidateMeasure();

    /// <summary>판 위 휠은 판 스크롤만 — 스크롤 밖(머리줄 · 행동 줄) 휠도 여기서 끝내 지도 줌 · 3D 로 올라가지 않게(ImageCropPanelControl 과 같은 관례).</summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        e.Handled = true;
    }

    /// <summary>높이 손잡이(Thumb)의 AutomationId.</summary>
    protected abstract string HandleAutomationId { get; }

    /// <summary>끌기 · 키를 넘길 곳(MapViewModel).</summary>
    public IBuildingHeightPanelHost? Host { get; set; }

    /// <summary>손잡이를 끄는 중인가.</summary>
    public bool IsDraggingHandle => _dragThumb?.IsDragging == true;

    /// <summary>머리 띠 닫기(✕) 커맨드.</summary>
    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand), typeof(HeightPanelControlBase), new PropertyMetadata(null));

    /// <summary>커스텀 Control 은 peer 가 없어 AutomationId 가 UIA 트리에 안 나온다 — 판 루트를 찾을 수 있게 peer 를 둔다.</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    /// <summary>끄는 중이면 끌기만 취소(끌기 전 높이로) — 처리했으면 true.</summary>
    public bool CancelHandleDrag()
    {
        if (!IsDraggingHandle) return false;
        _dragThumb!.CancelDrag();   // DragCompleted(Canceled=true) → Host.EndHeightDrag(true)
        return true;
    }

    private bool IsHandle(object source) => source is Thumb t && AutomationProperties.GetAutomationId(t) == HandleAutomationId;

    private void OnHandleDragStarted(object sender, DragStartedEventArgs e)
    {
        if (!IsHandle(e.OriginalSource)) return;
        _dragThumb = (Thumb)e.OriginalSource;
        Host?.BeginHeightDrag();
        e.Handled = true;
    }

    /// <summary>손잡이는 움직이지 않는 자리 표시라 VerticalChange 가 곧 누른 자리에서 움직인 거리다(Thumb 기준 좌표).</summary>
    private void OnHandleDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!IsHandle(e.OriginalSource)) return;
        Host?.DragHeight(e.VerticalChange);
        e.Handled = true;
    }

    private void OnHandleDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!IsHandle(e.OriginalSource)) return;
        _dragThumb = null;
        Host?.EndHeightDrag(e.Canceled);
        e.Handled = true;
    }

    /// <summary>판만의 키(먼저 본다) — 처리했으면 true. 기본은 없음.</summary>
    protected virtual bool HandleExtraKey(KeyEventArgs e) => false;

    /// <summary>
    /// 판 안 키 — 입력칸 {Enter} = 그 값 바로 넣기, ↑/↓ = ±1층(Shift = ±0.1 m, 이름 칸 제외), Ctrl+Z = 판 안 되돌리기, Esc = 끌기 취소 또는 닫기.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (HandleExtraKey(e)) { e.Handled = true; return; }
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0, ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool textOnly = e.OriginalSource is TextBox nb && AutomationProperties.GetAutomationId(nb).EndsWith(".Name");
        switch (e.Key)
        {
            case Key.Enter when e.OriginalSource is TextBox box:
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                box.SelectAll();
                e.Handled = true;
                break;
            case Key.Up or Key.Down when !ctrl && !textOnly:
                if (e.OriginalSource is TextBox t) t.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                e.Handled = Host?.StepHeightByKey(e.Key == Key.Up, shift) == true;
                break;
            case Key.Z when ctrl && !shift:
                e.Handled = Host?.UndoHeight() == true;
                break;
            case Key.Escape:
                if (!CancelHandleDrag() && CloseCommand?.CanExecute(null) == true) CloseCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
