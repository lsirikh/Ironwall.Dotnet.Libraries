using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 캔버스 오버레이 — HUD · 배치 문구 · 확인 오버레이 · 되돌리기 막대 · M 모드 표시 (IMPL-23 · FR-11 · FR-14 · FR-32 · FR-35 · FR-40)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>캔버스 안 확인 오버레이의 내용(결정 #2 — 새 창이 아니다). 문구는 <c>UnitMapText.Confirm*</c> 가 만든다.</summary>
/// <param name="Title">제목 한 줄.</param>
/// <param name="Lines">본문 줄들.</param>
/// <param name="OkText">확정 단추 글("옮기기" · "연결").</param>
/// <param name="CanConfirm">지금 확정할 수 있는가 — 앞선 작업이 끝나기 전에는 <c>false</c>(자동 전송 금지, 시나리오 ISSUE-23).</param>
/// <param name="BusyText"><paramref name="CanConfirm"/> 가 거짓일 때 알릴 한 줄.</param>
public sealed record UnitMapConfirmPrompt(string Title, IReadOnlyList<string> Lines, string OkText, bool CanConfirm = true, string? BusyText = null);

/// <summary>캔버스 아래 막대 — 마지막 한 일(되돌리기) 또는 실패 사유. 다음 조작까지 남는다(타이머 없음 — FR-35).</summary>
/// <param name="Message">사람 말로 한 줄.</param>
/// <param name="IsError">실패 · 충돌 — 왼쪽 세로 막대 모양(색이 아니라 형태).</param>
/// <param name="CanUndo">[되돌리기] 를 낼 것인가.</param>
public sealed record UnitMapBar(string Message, bool IsError, bool CanUndo, string? ActionText = null);

/// <summary>막대의 보조 단추(예: [인접선 켜기] — FR-26 v1.3). 뷰모델이 구현하면 단추가 부른다.</summary>
public interface IUnitMapBarAction
{
    /// <summary>막대의 <see cref="UnitMapBar.ActionText"/> 단추를 눌렀다.</summary>
    void RunBarAction();
}

/// <summary>
/// 오버레이 단추가 부르는 뷰모델 명령. 관계도 뷰모델이 <see cref="IUnitMapInteraction"/> 과 <b>함께</b> 구현한다
/// (없으면 단추는 아무것도 하지 않는다). <see cref="IUnitMapInteraction"/> 을 넓히지 않으려고 따로 둔다.
/// </summary>
public interface IUnitMapOverlayCommands
{
    /// <summary>확인 오버레이 — [확정](<c>Enter</c>) 이면 <c>true</c>, [취소](<c>Esc</c>) 면 <c>false</c>. 취소는 서버 0.</summary>
    void Confirm(bool accept);

    /// <summary>되돌리기 막대의 [되돌리기] — <c>Ctrl+Z</c> 와 같다.</summary>
    void Undo();

    /// <summary>막대 닫기(✕).</summary>
    void DismissBar();

    /// <summary>배치 읽기 실패의 [다시 시도].</summary>
    void RetryLayout();
}

/// <summary>오버레이 묶음 틀 — UIA peer 가 있는 <see cref="Border"/>(확인 오버레이 뿌리 <c>Units.Map.Confirm</c>).</summary>
public sealed class UnitMapOverlayPanel : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() => new OverlayPeer(this);

    private sealed class OverlayPeer : FrameworkElementAutomationPeer
    {
        public OverlayPeer(FrameworkElement owner) : base(owner) { }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        protected override string GetClassNameCore() => nameof(UnitMapOverlayPanel);

        protected override bool IsControlElementCore() => true;
    }
}

public partial class UnitMapCanvas
{
    #region - AutomationId (FR-40 캔버스 몫) -
    public const string ID_ZOOM_IN = "Units.Map.ZoomIn";
    public const string ID_ZOOM_OUT = "Units.Map.ZoomOut";
    public const string ID_FIT = "Units.Map.Fit";
    public const string ID_ZOOM_LEVEL = "Units.Map.ZoomLevel";
    public const string ID_LAYOUT_STATUS = "Units.Map.LayoutStatus";
    public const string ID_LAYOUT_RETRY = "Units.Map.LayoutRetry";
    public const string ID_CONFIRM = "Units.Map.Confirm";
    public const string ID_CONFIRM_TEXT = "Units.Map.Confirm.Text";
    public const string ID_CONFIRM_OK = "Units.Map.Confirm.Ok";
    public const string ID_CONFIRM_CANCEL = "Units.Map.Confirm.Cancel";
    public const string ID_UNDO = "Units.Map.Undo";
    public const string ID_UNDO_DISMISS = "Units.Map.UndoDismiss";
    public const string ID_UNDO_TEXT = "Units.Map.UndoText";
    public const string ID_BAR_ACTION = "Units.Map.BarAction";

    /// <summary>캔버스 밖 · 오버레이 위에 놓아 취소했을 때 막대 문구(FR-29 v1.3 ②).</summary>
    public const string DROP_OUTSIDE_CANCELLED = "관계도 밖에 놓아 취소했습니다";
    public const string ID_MOVE_MODE = "Units.Map.MoveMode";
    #endregion

    private Border _hud = null!;
    private ConsoleText _zoomLabel = null!;
    private Button _zoomIn = null!, _zoomOut = null!, _fit = null!;
    private Border _statusNote = null!;
    private ConsoleText _statusText = null!;
    private Button _retry = null!;
    private Border _moveMode = null!;
    private ConsoleText _moveModeText = null!;
    private Border _bar = null!;
    private Border _barErrorMark = null!;
    private ConsoleText _barText = null!;
    private Button _undo = null!, _undoDismiss = null!, _barAction = null!;
    private string? _localNotice;
    private UnitMapOverlayPanel _confirm = null!;
    private ConsoleText _confirmTitle = null!, _confirmMessage = null!, _confirmBusy = null!;
    private Button _confirmOk = null!, _confirmCancel = null!;
    private Border _focusRing = null!;

    #region - 의존 속성(뷰모델이 묶는다) -
    public static readonly DependencyProperty ConfirmPromptProperty = DependencyProperty.Register(
        nameof(ConfirmPrompt), typeof(UnitMapConfirmPrompt), typeof(UnitMapCanvas), new PropertyMetadata(null, OnOverlayChanged));

    /// <summary>확인 오버레이(상위 바꾸기 · 인접 연결 — FR-32). <c>null</c> 이면 닫힌다. 떠 있는 동안 캔버스 입력은 막힌다.</summary>
    public UnitMapConfirmPrompt? ConfirmPrompt { get => (UnitMapConfirmPrompt?)GetValue(ConfirmPromptProperty); set => SetValue(ConfirmPromptProperty, value); }

    public static readonly DependencyProperty BarProperty = DependencyProperty.Register(
        nameof(Bar), typeof(UnitMapBar), typeof(UnitMapCanvas), new PropertyMetadata(null, OnOverlayChanged));

    /// <summary>되돌리기 · 실패 막대(FR-34 · FR-35). <c>null</c> 이면 숨는다 — 시간으로 사라지지 않는다.</summary>
    public UnitMapBar? Bar { get => (UnitMapBar?)GetValue(BarProperty); set => SetValue(BarProperty, value); }

    public static readonly DependencyProperty LayoutStatusTextProperty = DependencyProperty.Register(
        nameof(LayoutStatusText), typeof(string), typeof(UnitMapCanvas), new PropertyMetadata(null, OnOverlayChanged));

    /// <summary>배치 상태 문구(FR-11, <c>UnitMapText.LayoutStatus</c>). 비면 숨는다.</summary>
    public string? LayoutStatusText { get => (string?)GetValue(LayoutStatusTextProperty); set => SetValue(LayoutStatusTextProperty, value); }

    public static readonly DependencyProperty CanRetryLayoutProperty = DependencyProperty.Register(
        nameof(CanRetryLayout), typeof(bool), typeof(UnitMapCanvas), new PropertyMetadata(false, OnOverlayChanged));

    /// <summary>[다시 시도] 를 낼 것인가(배치 읽기 실패일 때).</summary>
    public bool CanRetryLayout { get => (bool)GetValue(CanRetryLayoutProperty); set => SetValue(CanRetryLayoutProperty, value); }

    public static readonly DependencyProperty MoveModeTextProperty = DependencyProperty.Register(
        nameof(MoveModeText), typeof(string), typeof(UnitMapCanvas), new PropertyMetadata(null, OnOverlayChanged));

    /// <summary><c>M</c> 위치 이동 모드 표시(FR-37). 비면 숨는다.</summary>
    public string? MoveModeText { get => (string?)GetValue(MoveModeTextProperty); set => SetValue(MoveModeTextProperty, value); }

    private static void OnOverlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (UnitMapCanvas)d;
        var focusInside = canvas.HasFocusInside();
        if (e.Property == BarProperty) canvas._localNotice = null;          // 뷰모델의 새 막대가 캔버스 알림을 대신한다
        canvas.UpdateOverlays();

        if (e.Property == ConfirmPromptProperty && e.OldValue is null && e.NewValue is not null)
        {
            // 포커스는 오버레이 첫 단추로 — Enter · Esc 가 캔버스를 떠나지 않게.
            if (canvas.IsGestureActive) canvas.FinishGesture(commit: false);
            MoveFocus(canvas._confirmOk);
            return;
        }

        // 확인 오버레이 · M 모드 · 막대가 닫히면 포커스를 캔버스로 되돌린다(시나리오 ISSUE-52) — 포커스가 캔버스 안에 있었을 때만.
        // 확인 오버레이가 닫히면 끈 노드(없으면 고른 노드)로(SIM-K157 · K163).
        var closed = e.NewValue is null || (e.NewValue is string text && string.IsNullOrWhiteSpace(text));
        if (closed && e.OldValue is not null && focusInside)
            canvas.RestoreFocus(e.Property == ConfirmPromptProperty ? canvas.LastDraggedUnitId : null);
    }

    /// <summary>키보드 포커스(또는 창이 비활성일 때의 논리 포커스)가 이 캔버스 안인가.</summary>
    private bool HasFocusInside()
    {
        if (IsKeyboardFocusWithin) return true;
        if (FocusManager.GetFocusedElement(FocusManager.GetFocusScope(this)) is not DependencyObject focused) return false;
        for (var current = focused; current is not null; current = ParentOf(current))
            if (ReferenceEquals(current, this)) return true;
        return false;
    }

    /// <summary>
    /// 캔버스로 포커스 — <paramref name="preferUnitId"/> 노드가 있으면 그 노드, 아니면 고른 노드, 아니면 캔버스.
    /// 창이 비활성이면 논리 포커스만 옮겨 두어 창이 돌아오면 그 요소가 받는다.
    /// </summary>
    internal void RestoreFocus(int? preferUnitId = null)
    {
        UIElement target = this;
        if (preferUnitId is int p && _nodes.TryGetValue(p, out var preferred) && preferred.Visibility == Visibility.Visible) target = preferred;
        else if (SelectedUnitId is int s && _nodes.TryGetValue(s, out var selected) && selected.Visibility == Visibility.Visible) target = selected;
        MoveFocus(target);
    }

    /// <summary>누른 곳으로 포커스 — 노드면 그 노드, 빈 곳이면 고른 노드(없으면 캔버스).</summary>
    private void FocusPressed(UnitMapNode? node)
    {
        if (node is not null) MoveFocus(node);
        else RestoreFocus();
    }

    /// <summary>뷰모델의 포커스 요청 — 캔버스만 받는다(상세 칸은 콘솔 뷰).</summary>
    private void OnFocusRequested(object? sender, UnitMapFocusTarget target)
    {
        if (target == UnitMapFocusTarget.Canvas) RestoreFocus();
    }

    /// <summary>Tab 으로 캔버스에 들어오면 고른 노드로 넘긴다(한 정지점 — 노드는 Tab 정지점이 아니다).</summary>
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        if (ReferenceEquals(e.NewFocus, this) && SelectedUnitId is int s && _nodes.TryGetValue(s, out var node) && node.Visibility == Visibility.Visible)
            node.Focus();
    }

    /// <summary>캔버스가 스스로 띄우는 막대 한 줄 — 예: 캔버스 밖에 놓아 취소. 앞 막대 위에 뜨고, 뷰모델의 다음 막대 · ✕ 까지 남는다.</summary>
    internal void ShowLocalNotice(string text)
    {
        _localNotice = text;
        UpdateOverlays();
    }

    private static void MoveFocus(UIElement target)
    {
        target.Focus();
        if (!target.IsKeyboardFocused) FocusManager.SetFocusedElement(FocusManager.GetFocusScope(target), target);
    }
    #endregion

    /// <summary>확인 오버레이가 떠 있다 — 캔버스의 다른 입력은 막힌다(시나리오 ISSUE-18).</summary>
    internal bool IsConfirmOpen => ConfirmPrompt is not null;

    private IUnitMapOverlayCommands? OverlayCommands => Interaction as IUnitMapOverlayCommands;

    #region - 조립 -
    private void BuildOverlays()
    {
        // HUD — 오른쪽 아래: [−] 단계 · 배율 [+] [⤢ 전체 보기] (FR-14 · SB S1).
        _zoomOut = MiniButton("−", ID_ZOOM_OUT, (_, _) => ZoomStep(-1));
        _zoomLabel = Text(ID_ZOOM_LEVEL, 11.5, FontWeights.SemiBold);
        _zoomLabel.MinWidth = 76;
        _zoomLabel.TextAlignment = TextAlignment.Center;
        _zoomLabel.VerticalAlignment = VerticalAlignment.Center;
        _zoomIn = MiniButton("+", ID_ZOOM_IN, (_, _) => ZoomStep(+1));
        _fit = MiniButton("⤢ 전체 보기", ID_FIT, (_, _) => Fit());
        _hud = OverlayBox(HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(12), translucent: true,
                     Row(_zoomOut, _zoomLabel, _zoomIn, _fit));

        // 배치 상태 문구 — 위 가운데(FR-11). 읽기 실패면 [다시 시도].
        _statusText = Text(ID_LAYOUT_STATUS, 11, FontWeights.Normal, "TextSecondaryBrush");
        _statusText.VerticalAlignment = VerticalAlignment.Center;
        _retry = MiniButton("다시 시도", ID_LAYOUT_RETRY, (_, _) => OverlayCommands?.RetryLayout());
        _retry.Margin = new Thickness(8, 0, 0, 0);
        _statusNote = OverlayBox(HorizontalAlignment.Center, VerticalAlignment.Top, new Thickness(0, 8, 0, 0), translucent: true, Row(_statusText, _retry));
        _statusNote.CornerRadius = new CornerRadius(11);
        _statusNote.Padding = new Thickness(11, 3, 11, 3);

        // M 위치 이동 모드 — 왼쪽 위.
        _moveModeText = Text(ID_MOVE_MODE, 11.5, FontWeights.SemiBold);
        _moveMode = OverlayBox(HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(12, 8, 0, 0), translucent: true, _moveModeText);

        // 되돌리기 · 실패 막대 — 왼쪽 아래. 실패는 왼쪽 4px 세로 막대(형태로 가른다).
        _barErrorMark = new Border { Width = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(-6, -4, 6, -4), IsHitTestVisible = false };
        _barErrorMark.SetResourceReference(Border.BackgroundProperty, "StatusCriticalBrush");
        _barText = Text(ID_UNDO_TEXT, 11.5, FontWeights.Normal);
        _barText.VerticalAlignment = VerticalAlignment.Center;
        _barText.TextTrimming = TextTrimming.CharacterEllipsis;
        _barText.MaxWidth = 380;
        _undo = MiniButton("되돌리기", ID_UNDO, (_, _) => OverlayCommands?.Undo());
        _undo.Margin = new Thickness(10, 0, 0, 0);
        _barAction = MiniButton(string.Empty, ID_BAR_ACTION, (_, _) => (Interaction as IUnitMapBarAction)?.RunBarAction());
        _barAction.Margin = new Thickness(10, 0, 0, 0);
        _undoDismiss = MiniButton("✕", ID_UNDO_DISMISS, (_, _) =>
        {
            if (_localNotice is not null) { _localNotice = null; UpdateOverlays(); return; }   // 알림만 걷는다 — 밑의 막대(되돌리기)는 그대로
            OverlayCommands?.DismissBar();
        });
        _undoDismiss.Margin = new Thickness(6, 0, 0, 0);
        _bar = OverlayBox(HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(12), translucent: false,
                     Row(_barErrorMark, _barText, _undo, _barAction, _undoDismiss));
        _bar.MaxWidth = 520;

        // 확인 오버레이 — 캔버스 가운데(결정 #2: 새 창 아님). 그림자 없음(그림자는 끌리는 사본 하나만 — NFR-05).
        _confirmTitle = Text(null, 13, FontWeights.Bold);
        _confirmMessage = Text(ID_CONFIRM_TEXT, 11.5, FontWeights.Normal);
        _confirmMessage.Margin = new Thickness(0, 8, 0, 0);
        _confirmMessage.TextWrapping = TextWrapping.Wrap;
        _confirmBusy = Text(null, 11, FontWeights.Normal, "TextSecondaryBrush");
        _confirmBusy.Margin = new Thickness(0, 6, 0, 0);
        _confirmOk = MiniButton(string.Empty, ID_CONFIRM_OK, (_, _) => ConfirmChoice(true), "Console.Button.Primary", focusable: true);
        _confirmCancel = MiniButton("취소  Esc", ID_CONFIRM_CANCEL, (_, _) => ConfirmChoice(false), focusable: true);
        _confirmCancel.Margin = new Thickness(8, 0, 0, 0);
        var buttons = Row(_confirmOk, _confirmCancel);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        buttons.Margin = new Thickness(0, 14, 0, 0);
        var body = new StackPanel();
        body.Children.Add(_confirmTitle);
        body.Children.Add(_confirmMessage);
        body.Children.Add(_confirmBusy);
        body.Children.Add(buttons);
        _confirm = new UnitMapOverlayPanel
        {
            Width = 318,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 12, 14, 12),
            Child = body,
        };
        _confirm.SetResourceReference(Border.BackgroundProperty, "SurfaceAltBrush");
        _confirm.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        AutomationProperties.SetAutomationId(_confirm, ID_CONFIRM);

        // 확인 오버레이 안에서만 Tab 이 돈다(두 단추) — 캔버스 전체는 한 Tab 정지점이다.
        KeyboardNavigation.SetTabNavigation(_confirm, KeyboardNavigationMode.Cycle);

        // 캔버스 포커스 표시 — 가장자리 2px 링(선택 · 후보 어휘와 겹치지 않는 형태). 입력에 투명.
        _focusRing = new Border { BorderThickness = new Thickness(2), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        _focusRing.SetResourceReference(Border.BorderBrushProperty, "FocusRingBrush");

        foreach (var element in new UIElement[] { _focusRing, _hud, _statusNote, _moveMode, _bar, _confirm }) _overlay.Children.Add(element);
        _statusNote.SizeChanged += (_, _) => OnOverlayBandChanged();
        _statusNote.IsVisibleChanged += (_, _) => OnOverlayBandChanged();
        _hud.SizeChanged += (_, _) => OnOverlayBandChanged();          // 첫 전체 보기가 HUD 가 재어지기 전에 돌았을 때
        UpdateOverlays();
    }
    #endregion

    #region - 갱신 -
    private void UpdateOverlays()
    {
        if (_confirm is null) return;

        _statusText.Text = LayoutStatusText ?? string.Empty;
        _statusNote.Visibility = string.IsNullOrWhiteSpace(LayoutStatusText) ? Visibility.Collapsed : Visibility.Visible;
        _retry.Visibility = CanRetryLayout ? Visibility.Visible : Visibility.Collapsed;

        _moveModeText.Text = MoveModeText ?? string.Empty;
        _moveMode.Visibility = string.IsNullOrWhiteSpace(MoveModeText) ? Visibility.Collapsed : Visibility.Visible;

        // 더 새것이 먼저 — 캔버스 알림(취소 안내 등)은 뷰모델의 새 막대가 오면 지워지므로(OnOverlayChanged), 남아 있다면 지금 막대보다 새것이다.
        // (실창 8회차 SIM-D087 · D-09: 앞 조작의 막대가 남아 있으면 "관계도 밖에 놓아 취소했습니다" 가 그 밑에 깔려 안 보였다 — FR-29 v1.3 ②)
        var bar = _localNotice is { } notice ? new UnitMapBar(notice, false, false) : Bar;
        _bar.Visibility = bar is null ? Visibility.Collapsed : Visibility.Visible;
        _barText.Text = bar?.Message ?? string.Empty;
        _barErrorMark.Visibility = bar?.IsError == true ? Visibility.Visible : Visibility.Collapsed;
        _undo.Visibility = bar?.CanUndo == true ? Visibility.Visible : Visibility.Collapsed;
        _barAction.Content = bar?.ActionText ?? string.Empty;
        _barAction.Visibility = string.IsNullOrWhiteSpace(bar?.ActionText) ? Visibility.Collapsed : Visibility.Visible;

        var prompt = ConfirmPrompt;
        _confirm.Visibility = prompt is null ? Visibility.Collapsed : Visibility.Visible;
        _confirmTitle.Text = prompt?.Title ?? string.Empty;
        _confirmMessage.Text = prompt is null ? string.Empty : string.Join(Environment.NewLine, prompt.Lines);
        _confirmOk.Content = prompt is null ? string.Empty : $"{prompt.OkText}  Enter";
        _confirmOk.IsEnabled = prompt?.CanConfirm ?? false;
        _confirmBusy.Text = prompt is { CanConfirm: false } ? prompt.BusyText ?? string.Empty : string.Empty;
        _confirmBusy.Visibility = string.IsNullOrEmpty(_confirmBusy.Text) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>전체 보기가 오버레이 띠와 노드 사이에 두는 틈(DIU).</summary>
    public const double OVERLAY_GAP = 4.0;

    /// <summary>
    /// 전체 보기가 비울 띠 — 위(배치 문구 · M 표시)와 아래(HUD · 되돌리기 막대) 중 <b>지금 보이는</b> 것의 실제 높이 + 여백 + 틈.
    /// 좌우는 비우지 않는다(띠가 이미 위 · 아래 가장자리를 차지한다).
    /// </summary>
    internal GraphInsets OverlayInsets()
    {
        if (_hud is null) return default;
        double Band(FrameworkElement box, double margin)
        {
            if (box.Visibility != Visibility.Visible) return 0;
            var height = box.ActualHeight > 0 ? box.ActualHeight : box.DesiredSize.Height;
            return height > 0 ? margin + height + OVERLAY_GAP : 0;
        }

        var top = Math.Max(Band(_statusNote, _statusNote.Margin.Top), Band(_moveMode, _moveMode.Margin.Top));
        var bottom = Math.Max(Band(_hud, _hud.Margin.Bottom), Band(_bar, _bar.Margin.Bottom));
        return new GraphInsets(0, top, 0, bottom);
    }

    /// <summary>
    /// 배치 문구가 나타나거나 크기가 바뀌었다(배치 GET 응답은 첫 그림 뒤에 온다) — 마지막 자동 뷰(전체 보기 · 가운데 두기) 그대로라면
    /// (뷰 · 장면이 같다) 새 띠로 같은 요청을 한 번 다시 한다. 사용자가 팬 · 줌했거나 장면이 바뀌었으면 건드리지 않는다.
    /// 되돌리기 막대 · M 표시는 사용자 조작의 결과라 여기서 뷰를 옮기지 않는다(다음 [전체 보기]가 비운다).
    /// </summary>
    private void OnOverlayBandChanged()
    {
        if (_refitPending) return;
        _refitPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _refitPending = false;
            if (IsDragging || _autoView is not { } auto) return;
            if (auto.View != _view || auto.Bounds != Scene.WorldBounds) return;
            if (OverlayInsets() == auto.Insets) return;
            auto.Replay();
        }));
    }

    private bool _refitPending;

    /// <summary>끄는 동안 오버레이 판정에 쓰는 틀(보이는 것만 본다).</summary>
    private IEnumerable<FrameworkElement> OverlayBoxes()
    {
        if (_hud is null) yield break;
        yield return _hud;
        yield return _bar;
        yield return _statusNote;
        yield return _moveMode;
    }

    // 포커스 표시 — 캔버스 또는 그 안(고른 노드)에 키보드 포커스가 있을 때(오버레이 단추 포함).
    protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusWithinChanged(e);
        if (_focusRing is not null) _focusRing.Visibility = IsKeyboardFocusWithin ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>포커스 표시가 보이는가(시험).</summary>
    internal bool IsFocusRingVisible => _focusRing?.Visibility == Visibility.Visible;

    /// <summary>HUD "단계 · 배율" — 줌 · 단계가 바뀔 때마다.</summary>
    private void UpdateHud()
    {
        if (_zoomLabel is null) return;
        _zoomLabel.Text = UnitMapText.ZoomLabel(_level, _view.Scale);
    }

    /// <summary>확인 오버레이의 확정 · 취소. 확정할 수 없는 동안(<see cref="UnitMapConfirmPrompt.CanConfirm"/>)의 <c>Enter</c> 는 무시.</summary>
    internal void ConfirmChoice(bool accept)
    {
        var prompt = ConfirmPrompt;
        if (prompt is null) return;
        if (accept && !prompt.CanConfirm) return;
        OverlayCommands?.Confirm(accept);
    }
    #endregion

    #region - 부품 -
    private static ConsoleText Text(string? automationId, double size, FontWeight weight, string brushToken = "TextPrimaryBrush")
    {
        var text = new ConsoleText { FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.NoWrap };
        text.SetResourceReference(TextBlock.ForegroundProperty, brushToken);
        if (automationId is not null) AutomationProperties.SetAutomationId(text, automationId);
        return text;
    }

    private static Button MiniButton(string content, string automationId, RoutedEventHandler click, string style = "Console.Button.Mini", bool focusable = false)
    {
        // HUD · 막대 · 문구 단추는 Tab 정지점이 아니다(캔버스 = 한 정지점 — 키 짝: + − 0 · Ctrl+Z). 마우스 · UIA Invoke 로 누른다.
        var button = new Button { Content = content, MinWidth = 24, Padding = new Thickness(8, 2, 8, 2), VerticalAlignment = VerticalAlignment.Center, Focusable = focusable };
        button.SetResourceReference(StyleProperty, style);
        AutomationProperties.SetAutomationId(button, automationId);
        button.Click += click;
        return button;
    }

    private static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var child in children) row.Children.Add(child);
        return row;
    }

    /// <summary>오버레이 틀. 반투명은 HUD · 문구 · M 표시만(NFR-05 — 반투명 층 최소), 막대는 불투명.</summary>
    private static Border OverlayBox(HorizontalAlignment h, VerticalAlignment v, Thickness margin, bool translucent, UIElement child)
    {
        var border = new Border
        {
            HorizontalAlignment = h,
            VerticalAlignment = v,
            Margin = margin,
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Child = child,
        };
        border.SetResourceReference(Border.BackgroundProperty, translucent ? "SurfaceTranslucentBrush" : "SurfaceAltBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        return border;
    }
    #endregion

    /// <summary>HUD [+] · [−] · <c>+</c> · <c>−</c> — 선택 부대가 보이면 그 점, 아니면 가운데를 고정한 채 한 칸.</summary>
    internal void ZoomStep(int direction)
    {
        if (IsDragging || direction == 0) return;
        ZoomAt(ZoomAnchor(), direction > 0 ? GraphViewport.WheelStep : 1 / GraphViewport.WheelStep);
    }

    private Point ZoomAnchor()
    {
        var size = ViewportSize;
        if (SelectedUnitId is int id && IsInView(id) && Scene.Positions.TryGetValue(id, out var world))
            return _view.WorldToScreen(world);
        return new Point(size.Width / 2, size.Height / 2);
    }
}
