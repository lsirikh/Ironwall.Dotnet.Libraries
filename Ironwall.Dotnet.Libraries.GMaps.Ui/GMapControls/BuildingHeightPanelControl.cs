using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>「건물 높이」 판이 부르는 쪽(MapViewModel) — 손잡이 끌기 · 키 한 칸 · 판 안 되돌리기.</summary>
public interface IBuildingHeightPanelHost
{
    /// <summary>손잡이를 잡았다 — 지금 미리보기 높이를 끌기 시작 높이로.</summary>
    void BeginHeightDrag();
    /// <summary>손잡이 끄는 중 — totalDy = 누른 자리에서 세로로 움직인 px(위 = 음수).</summary>
    void DragHeight(double totalDy);
    /// <summary>끌기 끝 — canceled 면(Esc · 캡처 잃음) 끌기 시작 높이로.</summary>
    void EndHeightDrag(bool canceled);
    /// <summary>↑/↓ 한 칸(Shift = 0.1 m) — 처리했으면 true.</summary>
    bool StepHeightByKey(bool up, bool shift);
    /// <summary>판 안 되돌리기(Ctrl+Z) — 처리했으면 true.</summary>
    bool UndoHeight();
}

/// <summary>
/// 「건물 높이」 판(2026-10-19) — 3D 에서 고른 건물의 높이 · 층수를 보이고 직접 넣는다(보안 구역 층수).
/// GroupRotationPanelControl(OverlayWindow 패턴) 답습 — Control + Themes/BuildingHeightPanelStyle.xaml + 머리 띠 끌기(OverlayHeaderDrag)
/// + DynamicResource 토큰(라이트/다크). 폼은 DataContext(=MapViewModel)에 바인딩.
/// 높이 손잡이(Thumb, AutomationId Map.BuildingHeight.Handle)의 끌기 이벤트는 판에서 받아 <see cref="Host"/> 로 넘긴다(x:Name 없이).
/// </summary>
public class BuildingHeightPanelControl : Control
{
    private Thumb? _dragThumb;

    static BuildingHeightPanelControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BuildingHeightPanelControl),
            new FrameworkPropertyMetadata(typeof(BuildingHeightPanelControl)));
    }

    public BuildingHeightPanelControl()
    {
        Width = 320;
        AutomationProperties.SetAutomationId(this, "Map.BuildingHeight.Panel");
        AutomationProperties.SetName(this, "건물 높이");
        Helpers.OverlayHeaderDrag.Attach(this, 38);   // 머리 띠(38)만 끌기
        PreviewKeyDown += OnPreviewKeyDown;
        AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnHandleDragStarted));
        AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnHandleDragDelta));
        AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnHandleDragCompleted));
    }

    /// <summary>끌기 · 키를 넘길 곳(MapViewModel).</summary>
    public IBuildingHeightPanelHost? Host { get; set; }

    /// <summary>손잡이를 끄는 중인가.</summary>
    public bool IsDraggingHandle => _dragThumb?.IsDragging == true;

    #region - Dependency Properties -
    /// <summary>머리 띠 닫기(✕) 커맨드 — 취소(원래 높이로)와 같다.</summary>
    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand),
            typeof(BuildingHeightPanelControl), new PropertyMetadata(null));
    #endregion

    /// <summary>커스텀 Control 은 peer 가 없어 AutomationId 가 UIA 트리에 안 나온다 — 판 루트를 찾을 수 있게 peer 를 둔다.</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    /// <summary>끄는 중이면 끌기만 취소(끌기 전 높이로) — 처리했으면 true. 지도 창 Esc(MapViewModel)가 먼저 부른다.</summary>
    public bool CancelHandleDrag()
    {
        if (!IsDraggingHandle) return false;
        _dragThumb!.CancelDrag();   // DragCompleted(Canceled=true) → Host.EndHeightDrag(true)
        return true;
    }

    private static bool IsHandle(object source) => source is Thumb t && AutomationProperties.GetAutomationId(t) == "Map.BuildingHeight.Handle";

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

    /// <summary>
    /// 판 안 키 — 입력칸 {Enter} = 그 값 바로 넣기, ↑/↓ = ±1층(Shift = ±0.1 m), Ctrl+Z = 판 안 되돌리기, Esc = 끌기 취소 또는 판 취소.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0, ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        switch (e.Key)
        {
            case Key.Enter when e.OriginalSource is TextBox box:
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                box.SelectAll();
                e.Handled = true;
                break;
            case Key.Up or Key.Down when !ctrl:
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
