using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도의 노드 — Thumb 파생(UIA peer 실재), 단계별 모양은 스타일이 갈아 끼운다 (FR-20 · FR-22 · FR-23 · FR-28 · FR-41)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 관계도의 부대 노드 하나. <b>그림만 들고 판단은 하지 않는다</b> — 값은 캔버스가 장면(<see cref="UnitMapScene"/>)에서 채운다.
/// </summary>
/// <remarks>
/// <para><b>왜 <see cref="Thumb"/> 파생인가</b>(결정 #3 · PRD §2.5-A): 노드 본체 전체가 끌기 손잡이인데, 손잡이는
/// AutomationPeer 가 실재하는 타입이어야 한다(<c>Border</c> · <c>ContentControl</c> 은 UIA 트리에 안 나온다).
/// <c>Button</c> 류(<c>ButtonBase</c>)는 누르는 순간 캡처를 빼앗아 캡처 드래그가 자멸한다 — <c>Thumb</c> 는 아니다.</para>
/// <para><b><see cref="Thumb.DragDelta"/> 를 쓰지 않는다.</b> 그 값은 손잡이 기준 좌표라 노드가 포인터를 따라 움직이면
/// 증분이 되고, 총량으로 읽으면 절반에서 멈춘다(VER-01 실측 — probe log). 그래서 <c>Thumb</c> 의 누름 처리(캡처 ·
/// <c>DragStarted</c>)를 끄고, 누름 · 이동 · 뗌은 <b>캔버스가 루트 기준으로</b> 잰다. 노드는 입력을 삼키지 않고 올려 보낸다.</para>
/// <para>단계(L0 · L1 · L2)마다 도형 크기가 <b>화면에서 고정</b>이라(FR-21) 노드 요소의 크기도 단계 · 제대로만 정해진다.
/// 기하는 (단계, 제대) 한 쌍마다 한 번만 만들어 얼린 뒤 모든 노드가 나눠 쓴다(브러시가 아니라 기하라 얼려도 테마와 무관).</para>
/// <para>포커스: 캔버스 포커스는 <b>고른 노드</b>에 머문다(UIA 가 그 노드를 읽는다). 노드는 Tab 정지점이 아니다 — 캔버스 전체가 한 정지점(ISSUE-52).
/// 단축키는 캔버스의 터널 <c>PreviewKeyDown</c> 이 받으므로 노드에 포커스가 있어도 그대로 동작한다.</para>
/// </remarks>
public class UnitMapNode : Thumb
{
    /// <summary>노드 AutomationId 접두사 — <c>Units.Map.Node.{id}</c>(FR-40).</summary>
    public const string AUTOMATION_ID_PREFIX = "Units.Map.Node.";

    static UnitMapNode()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(UnitMapNode), new FrameworkPropertyMetadata(typeof(UnitMapNode)));
        // 포커스는 받는다(UIA 가 고른 노드를 읽는다 · 단축키는 캔버스 터널에서) — 그러나 Tab 정지점은 아니다(노드 200개를 돌지 않게).
        FocusableProperty.OverrideMetadata(typeof(UnitMapNode), new FrameworkPropertyMetadata(true));
        KeyboardNavigation.IsTabStopProperty.OverrideMetadata(typeof(UnitMapNode), new FrameworkPropertyMetadata(false));
        FocusVisualStyleProperty.OverrideMetadata(typeof(UnitMapNode), new FrameworkPropertyMetadata(null));
    }

    public UnitMapNode()
    {
        ApplyVisuals();
    }

    #region - 이벤트 -
    /// <summary>좌표 없는 선택 요청 — UIA <c>SelectionItem.Select()</c>. 캔버스가 받아 뷰모델로 넘긴다(버블).</summary>
    public static readonly RoutedEvent SelectRequestedEvent = EventManager.RegisterRoutedEvent(
        "SelectRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(UnitMapNode));

    public event RoutedEventHandler SelectRequested
    {
        add => AddHandler(SelectRequestedEvent, value);
        remove => RemoveHandler(SelectRequestedEvent, value);
    }

    /// <summary>좌표 없이 이 노드를 고르라고 올린다(자동화 · 키보드 경로).</summary>
    public void RequestSelect() => RaiseEvent(new RoutedEventArgs(SelectRequestedEvent, this));
    #endregion

    #region - 정체 -
    public static readonly DependencyProperty UnitIdProperty = DependencyProperty.Register(
        nameof(UnitId), typeof(int), typeof(UnitMapNode), new PropertyMetadata(0, OnUnitIdChanged));

    /// <summary>부대 id — AutomationId <c>Units.Map.Node.{id}</c> 를 함께 단다.</summary>
    public int UnitId { get => (int)GetValue(UnitIdProperty); set => SetValue(UnitIdProperty, value); }

    private static void OnUnitIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => AutomationProperties.SetAutomationId(d, AUTOMATION_ID_PREFIX + ((int)e.NewValue).ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(UnitMapLevel), typeof(UnitMapNode), new PropertyMetadata(UnitMapLevel.L1, OnShapeChanged));

    /// <summary>의미 줌 단계 — 바뀔 때만 스타일 트리거가 템플릿을 갈아 끼운다(NFR-03).</summary>
    public UnitMapLevel Level { get => (UnitMapLevel)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }

    public static readonly DependencyProperty EchelonProperty = DependencyProperty.Register(
        nameof(Echelon), typeof(EnumUnitEchelon?), typeof(UnitMapNode), new PropertyMetadata(null, OnShapeChanged));

    /// <summary>제대. 모르면 <c>null</c> — 틀 + "?"(끌 수 없다).</summary>
    public EnumUnitEchelon? Echelon { get => (EnumUnitEchelon?)GetValue(EchelonProperty); set => SetValue(EchelonProperty, value); }

    public static readonly DependencyProperty UnitNameProperty = DependencyProperty.Register(
        nameof(UnitName), typeof(string), typeof(UnitMapNode), new PropertyMetadata(string.Empty));

    /// <summary>전체 이름(L2 카드 · 툴팁).</summary>
    public string UnitName { get => (string)GetValue(UnitNameProperty); set => SetValue(UnitNameProperty, value); }

    public static readonly DependencyProperty ShortNameProperty = DependencyProperty.Register(
        nameof(ShortName), typeof(string), typeof(UnitMapNode), new PropertyMetadata(string.Empty));

    /// <summary>짧은 이름(L1 — 이름의 마지막 낱말).</summary>
    public string ShortName { get => (string)GetValue(ShortNameProperty); set => SetValue(ShortNameProperty, value); }

    public static readonly DependencyProperty CodeProperty = DependencyProperty.Register(
        nameof(Code), typeof(string), typeof(UnitMapNode), new PropertyMetadata(string.Empty));

    /// <summary>부대 코드(L2 — 고정폭).</summary>
    public string Code { get => (string)GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    #endregion

    #region - 상태 표지(FR-23) -
    public static readonly DependencyProperty IsSelectedNodeProperty = DependencyProperty.Register(
        nameof(IsSelectedNode), typeof(bool), typeof(UnitMapNode), new PropertyMetadata(false, OnIsSelectedNodeChanged));

    /// <summary>트리와 같은 선택(FR-02). L2 = 좌측 3px 막대 + 1.5 윤곽 / L0 · L1 = 모서리 괄호.</summary>
    public bool IsSelectedNode { get => (bool)GetValue(IsSelectedNodeProperty); set => SetValue(IsSelectedNodeProperty, value); }

    private static void OnIsSelectedNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (UIElementAutomationPeer.FromElement((UIElement)d) is UnitMapNodeAutomationPeer peer)
            peer.RaiseSelectionChanged((bool)e.OldValue, (bool)e.NewValue);
    }

    public static readonly DependencyProperty IsMovedProperty = DependencyProperty.Register(
        nameof(IsMoved), typeof(bool), typeof(UnitMapNode), new PropertyMetadata(false));

    /// <summary>공유 배치에 Δ 가 있다 — 핀.</summary>
    public bool IsMoved { get => (bool)GetValue(IsMovedProperty); set => SetValue(IsMovedProperty, value); }

    public static readonly DependencyProperty IsSuspendedProperty = DependencyProperty.Register(
        nameof(IsSuspended), typeof(bool), typeof(UnitMapNode), new PropertyMetadata(false));

    /// <summary>운용 중지 — 틀 사선 해치(L1 · L2) · 사선 하나(L0) + "중지"(L2).</summary>
    public bool IsSuspended { get => (bool)GetValue(IsSuspendedProperty); set => SetValue(IsSuspendedProperty, value); }

    public static readonly DependencyProperty IsMineProperty = DependencyProperty.Register(
        nameof(IsMine), typeof(bool), typeof(UnitMapNode), new PropertyMetadata(false));

    /// <summary>이 앱이 붙은 부대 — ★.</summary>
    public bool IsMine { get => (bool)GetValue(IsMineProperty); set => SetValue(IsMineProperty, value); }

    public static readonly DependencyProperty IsDimmedProperty = DependencyProperty.Register(
        nameof(IsDimmed), typeof(bool), typeof(UnitMapNode), new PropertyMetadata(false));

    /// <summary>제대 칩 강조에서 빠졌다 — 50% 흐림(FR-16).</summary>
    public bool IsDimmed { get => (bool)GetValue(IsDimmedProperty); set => SetValue(IsDimmedProperty, value); }

    public static readonly DependencyProperty ErrorCountProperty = DependencyProperty.Register(
        nameof(ErrorCount), typeof(int), typeof(UnitMapNode), new PropertyMetadata(0, OnErrorCountChanged));

    /// <summary>오류 장비 수 — ▲ + 수.</summary>
    public int ErrorCount { get => (int)GetValue(ErrorCountProperty); set => SetValue(ErrorCountProperty, value); }

    private static readonly DependencyPropertyKey HasErrorsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasErrors), typeof(bool), typeof(UnitMapNode), new PropertyMetadata(false));

    public static readonly DependencyProperty HasErrorsProperty = HasErrorsPropertyKey.DependencyProperty;

    public bool HasErrors => (bool)GetValue(HasErrorsProperty);

    private static void OnErrorCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => d.SetValue(HasErrorsPropertyKey, (int)e.NewValue > 0);

    public static readonly DependencyProperty DeviceCountProperty = DependencyProperty.Register(
        nameof(DeviceCount), typeof(int), typeof(UnitMapNode), new PropertyMetadata(0));

    /// <summary>이 부대에 직접 매인 장비 수(L2 "장비 N").</summary>
    public int DeviceCount { get => (int)GetValue(DeviceCountProperty); set => SetValue(DeviceCountProperty, value); }

    public static readonly DependencyProperty ShowDeviceBadgesProperty = DependencyProperty.Register(
        nameof(ShowDeviceBadges), typeof(bool), typeof(UnitMapNode), new PropertyMetadata(true));

    /// <summary>레이어 토글 "장비 배지"(FR-26).</summary>
    public bool ShowDeviceBadges { get => (bool)GetValue(ShowDeviceBadgesProperty); set => SetValue(ShowDeviceBadgesProperty, value); }

    public static readonly DependencyProperty DropStateProperty = DependencyProperty.Register(
        nameof(DropState), typeof(UnitMapNodeDropState), typeof(UnitMapNode), new PropertyMetadata(UnitMapNodeDropState.None));

    /// <summary>끄는 동안의 대상 표시(FR-30) — 캔버스가 끌기 시작 때 1회 · 머문 노드가 바뀔 때만 쓴다.</summary>
    public UnitMapNodeDropState DropState { get => (UnitMapNodeDropState)GetValue(DropStateProperty); set => SetValue(DropStateProperty, value); }

    public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.Register(
        nameof(LabelWidth), typeof(double), typeof(UnitMapNode), new PropertyMetadata(DEFAULT_LABEL_WIDTH, OnLabelWidthChanged));

    /// <summary>L1 짧은 이름 폭 = 칸 × 배율 − 8(말줄임). 캔버스가 L1 에서 배율이 바뀔 때만 쓴다.</summary>
    public double LabelWidth { get => (double)GetValue(LabelWidthProperty); set => SetValue(LabelWidthProperty, value); }

    /// <summary>L1 짧은 이름 기본 폭 — 배율 0.5 의 칸(100) − 8.</summary>
    public const double DEFAULT_LABEL_WIDTH = 92.0;

    private static readonly DependencyPropertyKey LabelLeftPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(LabelLeft), typeof(double), typeof(UnitMapNode), new PropertyMetadata(0.0));

    public static readonly DependencyProperty LabelLeftProperty = LabelLeftPropertyKey.DependencyProperty;

    /// <summary>짧은 이름의 왼쪽 — 부대 위치 점을 가운데로(노드 상자보다 넓어 양쪽으로 넘친다).</summary>
    public double LabelLeft => (double)GetValue(LabelLeftProperty);

    private static void OnLabelWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((UnitMapNode)d).UpdateLabelLeft();

    private void UpdateLabelLeft() => SetValue(LabelLeftPropertyKey, Visuals.Center.X - LabelWidth / 2);
    #endregion

    #region - 자동화 문자열(FR-41) -
    public static readonly DependencyProperty PeerNameProperty = DependencyProperty.Register(
        nameof(PeerName), typeof(string), typeof(UnitMapNode), new PropertyMetadata(string.Empty));

    /// <summary>peer <c>Name</c> — "7중대 (중대, c0207)". 문자열은 순수 함수(<c>UnitMapText</c>)가 만든다.</summary>
    public string PeerName { get => (string)GetValue(PeerNameProperty); set => SetValue(PeerNameProperty, value); }

    public static readonly DependencyProperty PeerStatusProperty = DependencyProperty.Register(
        nameof(PeerStatus), typeof(string), typeof(UnitMapNode), new PropertyMetadata(string.Empty));

    /// <summary>peer <c>ItemStatus</c> — "단계=L2; 옮김=예; 장비=16; 오류=0; 중지=아니오".</summary>
    public string PeerStatus { get => (string)GetValue(PeerStatusProperty); set => SetValue(PeerStatusProperty, value); }
    #endregion

    #region - 기하 -
    private static readonly DependencyPropertyKey VisualsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Visuals), typeof(UnitMapNodeVisuals), typeof(UnitMapNode), new PropertyMetadata(null));

    public static readonly DependencyProperty VisualsProperty = VisualsPropertyKey.DependencyProperty;

    /// <summary>이 (단계, 제대)의 도형 한 벌 — 템플릿이 <c>Path.Data</c> 로 묶는다.</summary>
    public UnitMapNodeVisuals Visuals => (UnitMapNodeVisuals)GetValue(VisualsProperty);

    /// <summary>노드 요소 안의 "부대 위치" 점 — 캔버스는 <c>Left = x × 배율 − CenterOffset.X</c> 로 놓는다.</summary>
    public Point CenterOffset => Visuals.Center;

    /// <summary>템플릿이 적용된 횟수(시험 · 성능 카운터 — 단계가 바뀔 때만 늘어야 한다, NFR-03).</summary>
    public int TemplateApplyCount { get; private set; }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        TemplateApplyCount++;
        _dropDecor = FindDropDecor();
        ScheduleDropDecorWarmUp();
    }

    #region - 대상 표시 미리 입히기(NFR-01) -
    // 대상 표시(UnitMapNodeDropDecor)는 접혀 있어 첫 그림에서 템플릿을 입지 않는다 — 첫 그림이 가볍다. 그대로 두면 첫 끌기 시작 때
    // 노드 200개가 한꺼번에 입어(측정 13 → 43 ms) 고스트가 늦게 뜬다. 그래서 첫 그림 뒤 한가할 때(ApplicationIdle) 노드마다 하나씩 입혀 둔다.
    // 한가한 틈이 오기 전에 끌기가 시작되면 보일 때 입는다(정확성은 같다 — 늦을 뿐).
    private UnitMapNodeDropDecor? _dropDecor;
    private System.Windows.Threading.DispatcherOperation? _dropDecorWarmUp;

    /// <summary>대상 표시가 템플릿을 입었는가(시험용).</summary>
    internal bool IsDropDecorRealized => _dropDecor is { } decor && VisualTreeHelper.GetChildrenCount(decor) > 0;

    private UnitMapNodeDropDecor? FindDropDecor()
    {
        if (VisualTreeHelper.GetChildrenCount(this) == 0 || VisualTreeHelper.GetChild(this, 0) is not System.Windows.Controls.Panel root) return null;
        foreach (var child in root.Children)
            if (child is UnitMapNodeDropDecor decor) return decor;
        return null;
    }

    private void ScheduleDropDecorWarmUp()
    {
        if (_dropDecor is null || _dropDecorWarmUp is { Status: System.Windows.Threading.DispatcherOperationStatus.Pending }) return;
        _dropDecorWarmUp = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(WarmUpDropDecor));
    }

    /// <summary>대상 표시에 템플릿을 입힌다(접힌 채 — 보이지 않고 재지도 않는다). 템플릿이 이미 바뀌어 떨어진 표시면 건너뛴다.</summary>
    internal void WarmUpDropDecor()
    {
        _dropDecorWarmUp = null;
        if (!IsLoaded) return;                                  // 그 사이 떨어진 노드(레일 전환으로 버려진 캔버스 등) — 헛일을 하지 않는다
        if (_dropDecor is { } decor && ReferenceEquals(VisualTreeHelper.GetParent(decor) is { } parent ? VisualTreeHelper.GetParent(parent) : null, this))
            decor.ApplyTemplate();
    }
    #endregion

    private static void OnShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((UnitMapNode)d).ApplyVisuals();

    // 템플릿이 TemplateBinding 으로 읽는 Visuals 의 값들(NFR-01) — Visuals.X 두 단계 경로 바인딩은 알림 없는 CLR 속성이라
    // WPF 가 반사로 풀고 값 변화 구독까지 달아 노드 200개 템플릿 입히기가 비쌌다. 값은 Visuals 가 바뀔 때만 다시 쓴다.
    private static readonly DependencyPropertyKey IsUnknownEchelonPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsUnknownEchelon), typeof(bool), typeof(UnitMapNode), new PropertyMetadata(false));

    public static readonly DependencyProperty IsUnknownEchelonProperty = IsUnknownEchelonPropertyKey.DependencyProperty;

    /// <summary>제대를 모른다 — 표지 자리에 "?"(<see cref="UnitMapNodeVisuals.IsUnknownEchelon"/>).</summary>
    public bool IsUnknownEchelon => (bool)GetValue(IsUnknownEchelonProperty);

    private static readonly DependencyPropertyKey UnknownMarkLeftPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(UnknownMarkLeft), typeof(double), typeof(UnitMapNode), new PropertyMetadata(0.0));

    public static readonly DependencyProperty UnknownMarkLeftProperty = UnknownMarkLeftPropertyKey.DependencyProperty;

    /// <summary>"?" 의 왼쪽(<see cref="UnitMapNodeVisuals.UnknownMarkLeft"/>).</summary>
    public double UnknownMarkLeft => (double)GetValue(UnknownMarkLeftProperty);

    private static readonly DependencyPropertyKey UnknownMarkTopPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(UnknownMarkTop), typeof(double), typeof(UnitMapNode), new PropertyMetadata(0.0));

    public static readonly DependencyProperty UnknownMarkTopProperty = UnknownMarkTopPropertyKey.DependencyProperty;

    /// <summary>"?" 의 윗변(<see cref="UnitMapNodeVisuals.UnknownMarkTop"/>).</summary>
    public double UnknownMarkTop => (double)GetValue(UnknownMarkTopProperty);

    private static readonly DependencyPropertyKey LabelTopPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(LabelTop), typeof(double), typeof(UnitMapNode), new PropertyMetadata(0.0));

    public static readonly DependencyProperty LabelTopProperty = LabelTopPropertyKey.DependencyProperty;

    /// <summary>L1 짧은 이름의 윗변(<see cref="UnitMapNodeVisuals.LabelTop"/>).</summary>
    public double LabelTop => (double)GetValue(LabelTopProperty);

    private static readonly DependencyPropertyKey ErrorGeometryPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ErrorGeometry), typeof(Geometry), typeof(UnitMapNode), new PropertyMetadata(null));

    public static readonly DependencyProperty ErrorGeometryProperty = ErrorGeometryPropertyKey.DependencyProperty;

    /// <summary>▲ 기하 — L2 카드 글 줄의 오류 표지(<see cref="UnitMapNodeVisuals.ErrorGeometry"/>).</summary>
    public Geometry? ErrorGeometry => (Geometry?)GetValue(ErrorGeometryProperty);

    private void ApplyVisuals()
    {
        var visuals = UnitMapNodeVisuals.For(Level, Echelon);
        SetValue(VisualsPropertyKey, visuals);
        SetValue(IsUnknownEchelonPropertyKey, visuals.IsUnknownEchelon);
        SetValue(UnknownMarkLeftPropertyKey, visuals.UnknownMarkLeft);
        SetValue(UnknownMarkTopPropertyKey, visuals.UnknownMarkTop);
        SetValue(LabelTopPropertyKey, visuals.LabelTop);
        SetValue(ErrorGeometryPropertyKey, visuals.ErrorGeometry);
        Width = visuals.Box.Width;
        Height = visuals.Box.Height;
        UpdateLabelLeft();
    }
    #endregion

    #region - 입력: Thumb 의 끌기를 끈다 -
    // 누름 · 이동 · 뗌은 캔버스가 루트 기준으로 잰다(FR-28). 여기서 base 를 부르면 Thumb 가 캡처를 잡고
    // DragStarted/DragDelta 를 낸다 — 손잡이 기준 좌표의 함정. Handled 도 세우지 않는다: 캔버스까지 올라가야 한다.
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { }

    protected override void OnMouseMove(MouseEventArgs e) { }
    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new UnitMapNodeAutomationPeer(this);

    public override string ToString() => $"{AUTOMATION_ID_PREFIX}{UnitId} {Level}";
}

/// <summary>
/// (단계, 제대) 한 쌍의 도형 — 얼린 기하. 노드 200개가 나눠 쓴다(단계가 바뀔 때만 갈아 끼운다).
/// </summary>
/// <remarks>기하는 <see cref="UnitSymbolGeometry"/> 의 경로 문자열에서 만든다. 색은 여기 없다 — 템플릿이
/// <c>DynamicResource</c> 로 매번 재해석한다(NFR-07).</remarks>
public sealed class UnitMapNodeVisuals
{
    private static readonly Dictionary<(UnitMapLevel, EnumUnitEchelon?), UnitMapNodeVisuals> s_cache = new();
    private static readonly object s_gate = new();

    private UnitMapNodeVisuals(UnitSymbolShape shape)
    {
        Shape = shape;
        Box = shape.Box;
        Center = shape.Center;
        Frame = shape.Frame;
        FrameGeometry = Parse(shape.FramePathData)!;
        MarkStrokeGeometry = Parse(shape.MarkStrokePathData);
        MarkDotGeometry = Parse(shape.MarkDotPathData);
        IsUnknownEchelon = shape.Mark.Kind == UnitMarkKind.Unknown;
        UnknownMarkLeft = shape.MarkAnchor.X - 4;
        UnknownMarkTop = shape.MarkAnchor.Y - 12;

        var c = shape.Center;
        var f = shape.Frame;
        switch (shape.Level)
        {
            case UnitMapLevel.L2:
                StarGeometry = Parse(UnitSymbolGeometry.StarPathData(new Point(c.X + 55, c.Y - 17), 6));
                PinGeometry = Parse(UnitSymbolGeometry.PinPathData(new Point(c.X + 42, c.Y - 18)));
                ErrorGeometry = Parse(UnitSymbolGeometry.TrianglePathData(0, 0, 11));
                SlashGeometry = null;
                BracketsGeometry = null;
                break;
            case UnitMapLevel.L1:
                StarGeometry = Parse(UnitSymbolGeometry.StarPathData(new Point(c.X - 20, c.Y - 15), 5));
                PinGeometry = Parse(UnitSymbolGeometry.PinPathData(new Point(c.X + 20, c.Y + 4)));
                ErrorGeometry = Parse(UnitSymbolGeometry.TrianglePathData(c.X + 12, c.Y - 19, 8));
                SlashGeometry = null;
                BracketsGeometry = Parse(UnitSymbolGeometry.BracketsPathData(new Rect(new Point(c.X - 21, c.Y - 24), new Point(c.X + 21, c.Y + 13)), 6));
                break;
            default:
                StarGeometry = Parse(UnitSymbolGeometry.StarPathData(new Point(f.Left + f.Width / 2, f.Top - 7), 5));
                PinGeometry = null;
                ErrorGeometry = Parse(UnitSymbolGeometry.TrianglePathData(f.Right + 1, f.Top - 5, 6));
                SlashGeometry = Parse(UnitSymbolGeometry.SuspendedSlashPathData(f));
                BracketsGeometry = Parse(UnitSymbolGeometry.BracketsPathData(new Rect(f.Left - 4, f.Top - 4, f.Width + 8, f.Height + 8), 4));
                break;
        }
    }

    public UnitSymbolShape Shape { get; }
    public Size Box { get; }
    public Point Center { get; }
    public Rect Frame { get; }
    public double FrameLeft => Frame.Left;
    public double FrameTop => Frame.Top;
    public Geometry FrameGeometry { get; }
    public Geometry? MarkStrokeGeometry { get; }
    public Geometry? MarkDotGeometry { get; }
    public bool IsUnknownEchelon { get; }
    public double UnknownMarkLeft { get; }
    public double UnknownMarkTop { get; }
    public Geometry? StarGeometry { get; }
    public Geometry? PinGeometry { get; }
    public Geometry? ErrorGeometry { get; }
    public Geometry? SlashGeometry { get; }
    public Geometry? BracketsGeometry { get; }

    /// <summary>L1 짧은 이름의 윗변(틀 아래 + 4) · 가운데 x.</summary>
    public double LabelTop => Frame.Bottom + 3;
    public double LabelCenterX => Center.X;

    private readonly Dictionary<(Geometry, double, PenLineCap), Size> _naturalSizes = new();
    private readonly object _naturalGate = new();

    /// <summary>
    /// 이 기하를 <c>Stretch=None</c> 인 <c>Path</c> 로 그렸을 때의 자연 크기 — <c>Shape.GetNaturalSize</c> 와 같은 식
    /// (펜 포함 렌더 경계의 오른쪽 · 아래, 음수는 0). <see cref="UnitMapNodeSymbol"/> 이 옛 <c>Path</c> 의 픽셀 맞춤 기준선을 되살릴 때 쓴다.
    /// 펜의 브러시는 경계와 무관하다. (기하, 굵기, 끝 모양)마다 한 번 재고 기억한다(노드 200개가 나눠 쓴다).
    /// </summary>
    /// <param name="strokeThickness">0 이면 펜 없음(채움만).</param>
    internal Size NaturalSize(Geometry geometry, double strokeThickness, PenLineCap cap)
    {
        lock (_naturalGate)
        {
            if (_naturalSizes.TryGetValue((geometry, strokeThickness, cap), out var size)) return size;
            var pen = strokeThickness > 0
                ? new Pen(Brushes.Black, strokeThickness) { StartLineCap = cap, EndLineCap = cap, DashCap = PenLineCap.Flat, LineJoin = PenLineJoin.Miter, MiterLimit = 10.0 }
                : null;
            var bounds = geometry.GetRenderBounds(pen);
            size = new Size(Math.Max(bounds.Right, 0.0), Math.Max(bounds.Bottom, 0.0));     // 빈 경계의 Right/Bottom 은 -∞ → 0(Shape 와 같다)
            _naturalSizes[(geometry, strokeThickness, cap)] = size;
            return size;
        }
    }

    public static UnitMapNodeVisuals For(UnitMapLevel level, EnumUnitEchelon? echelon)
    {
        lock (s_gate)
        {
            if (!s_cache.TryGetValue((level, echelon), out var visuals))
            {
                visuals = new UnitMapNodeVisuals(UnitSymbolGeometry.Build(level, echelon));
                s_cache[(level, echelon)] = visuals;
            }
            return visuals;
        }
    }

    private static Geometry? Parse(string data)
    {
        if (string.IsNullOrEmpty(data)) return null;
        var geometry = Geometry.Parse(data);
        if (geometry.CanFreeze) geometry.Freeze();       // 여러 스레드 · 여러 노드가 나눠 쓴다(브러시 아님 — 테마 무관)
        return geometry;
    }
}
