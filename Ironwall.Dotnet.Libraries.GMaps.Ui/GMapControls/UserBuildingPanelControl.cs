using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 「건물 그리기」 판(2026-10-19) — 지도에 그린 윤곽을 들어올릴 높이(층수 · 높이 · 층고 · ±1층 · 손잡이 끌기 · ↑/↓)와 이름을 넣고 저장 · 지우기.
/// 「건물 높이」 판(BuildingHeightPanelControl)과 같은 크롬 · 손잡이 · 키 규칙 — Control + Themes/UserBuildingPanelStyle.xaml + 머리 띠 끌기.
/// 손잡이(Thumb, AutomationId Map.UserBuilding.Handle) 끌기 · 키는 <see cref="Host"/>(같은 IBuildingHeightPanelHost 규약)로 넘긴다(x:Name 없이).
/// </summary>
public class UserBuildingPanelControl : Control
{
    private Thumb? _dragThumb;

    static UserBuildingPanelControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(UserBuildingPanelControl),
            new FrameworkPropertyMetadata(typeof(UserBuildingPanelControl)));
    }

    public UserBuildingPanelControl()
    {
        Width = 320;
        AutomationProperties.SetAutomationId(this, "Map.UserBuilding.Panel");
        AutomationProperties.SetName(this, "건물 그리기");
        Helpers.OverlayHeaderDrag.Attach(this, 38);
        PreviewKeyDown += OnPreviewKeyDown;
        AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(OnHandleDragStarted));
        AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(OnHandleDragDelta));
        AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(OnHandleDragCompleted));
    }

    /// <summary>끌기 · 키를 넘길 곳(MapViewModel 의 건물 그리기 어댑터).</summary>
    public IBuildingHeightPanelHost? Host { get; set; }

    public bool IsDraggingHandle => _dragThumb?.IsDragging == true;

    /// <summary>머리 띠 닫기(✕) — 취소와 같다.</summary>
    public ICommand? CloseCommand
    {
        get => (ICommand?)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }
    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand), typeof(UserBuildingPanelControl), new PropertyMetadata(null));

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    /// <summary>끄는 중이면 끌기만 취소 — 처리했으면 true.</summary>
    public bool CancelHandleDrag()
    {
        if (!IsDraggingHandle) return false;
        _dragThumb!.CancelDrag();
        return true;
    }

    private static bool IsHandle(object source) => source is Thumb t && AutomationProperties.GetAutomationId(t) == "Map.UserBuilding.Handle";

    private void OnHandleDragStarted(object sender, DragStartedEventArgs e)
    {
        if (!IsHandle(e.OriginalSource)) return;
        _dragThumb = (Thumb)e.OriginalSource;
        Host?.BeginHeightDrag();
        e.Handled = true;
    }

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

    /// <summary>판 안 키 — 입력칸 Enter = 그 값 넣기, ↑/↓ = ±1층(Shift = ±0.1 m), Ctrl+Z = 되돌리기, Esc = 끌기 취소 또는 취소.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0, ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool nameBox = e.OriginalSource is TextBox nb && AutomationProperties.GetAutomationId(nb) == "Map.UserBuilding.Name";
        switch (e.Key)
        {
            case Key.Enter when e.OriginalSource is TextBox box:
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                box.SelectAll();
                e.Handled = true;
                break;
            case Key.Up or Key.Down when !ctrl && !nameBox:
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
