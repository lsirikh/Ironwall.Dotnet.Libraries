using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Fence;

/// <summary>펜스 칩이 무엇인가.</summary>
public enum FenceChipKind
{
    Sensor = 0,
    /// <summary>펜스센서 묶음(FR-18).</summary>
    Group = 1,
    /// <summary>함체(링) · 제어기(가지 · 한 줄).</summary>
    Controller = 2,
    /// <summary>망 한 칸(fence-wiring-editor FR-04) — 키 = 망 번호(0부터).</summary>
    Panel = 3,
    /// <summary>개념도의 센서 노드(FR-12).</summary>
    ConceptNode = 4,
    /// <summary>개념도의 제어기(Ch1 · Ch2 포트).</summary>
    ConceptController = 5,
    /// <summary>개념도의 VBus 표지(FR-21 · 표시 전용 · 끌어 옮김).</summary>
    ConceptVbus = 6,
}

/// <summary>
/// 펜스 위 센서 · 묶음 · 함체 하나 — <see cref="Thumb"/> 파생(UIA peer 실재 · NFR-03). 그림은 캔버스가 준 <see cref="FenceChipPicture"/> 를 그대로 그린다.
/// </summary>
/// <remarks>
/// <para><b>누름 · 끌기는 캔버스가 한다</b> — <c>Thumb</c> 의 자체 캡처 · <c>DragDelta</c> 는 손잡이 기준 좌표라 쓰지 않는다(부대 관계도 노드와 같은 계약).
/// 마우스 가상 함수를 비워 두고, 캔버스가 터널 <c>PreviewMouseDown</c> 에서 누름을 받아 <b>자신이</b> 캡처한다.</para>
/// <para>포커스는 받는다(Tab 으로 센서 사이 이동 · FR-11). 포커스 모양은 캔버스가 선택 윤곽으로 그린다(포커스 = 선택).</para>
/// </remarks>
public sealed class FenceChip : Thumb
{
    public const string SENSOR_ID_PREFIX = "Devices.Wiring.Fence.Sensor.";
    public const string GROUP_ID_PREFIX = "Devices.Wiring.Fence.Group.";
    public const string ENCLOSURE_ID = "Devices.Wiring.Fence.Enclosure";
    public const string PANEL_ID_PREFIX = "Devices.Wiring.Fence.Panel.";
    public const string CONCEPT_NODE_ID_PREFIX = "Devices.Wiring.Fence.Concept.Node.";
    public const string CONCEPT_CONTROLLER_ID = "Devices.Wiring.Fence.Concept.Controller";
    public const string CONCEPT_VBUS_ID = "Devices.Wiring.Fence.Concept.Vbus";

    private FenceChipPicture? _picture;

    static FenceChip()
    {
        FocusableProperty.OverrideMetadata(typeof(FenceChip), new FrameworkPropertyMetadata(true));
        FocusVisualStyleProperty.OverrideMetadata(typeof(FenceChip), new FrameworkPropertyMetadata(null));
    }

    public FenceChip(FenceChipKind kind, int key, IReadOnlyList<int> keys)
    {
        Kind = kind;
        Key = key;
        Keys = keys;
        Template = null;                 // 테마 Thumb 모양을 쓰지 않는다 — OnRender 가 그린다
        Cursor = kind switch
        {
            FenceChipKind.Controller or FenceChipKind.ConceptController or FenceChipKind.ConceptVbus => Cursors.SizeWE,
            FenceChipKind.Panel => Cursors.Arrow,
            _ => Cursors.Hand,
        };
        AutomationProperties.SetAutomationId(this, kind switch
        {
            FenceChipKind.Sensor => SENSOR_ID_PREFIX + key,
            FenceChipKind.Group => GROUP_ID_PREFIX + key,
            FenceChipKind.Panel => PANEL_ID_PREFIX + key,
            FenceChipKind.ConceptNode => CONCEPT_NODE_ID_PREFIX + key,
            FenceChipKind.ConceptController => CONCEPT_CONTROLLER_ID,
            FenceChipKind.ConceptVbus => CONCEPT_VBUS_ID,
            _ => ENCLOSURE_ID,
        });
    }

    public FenceChipKind Kind { get; }

    /// <summary>센서 키 · 묶음의 첫 센서 키 · 제어기는 <see cref="FenceWorld.CONTROLLER_KEY"/>.</summary>
    public int Key { get; }

    /// <summary>이 칩이 대표하는 센서(묶음이면 여럿 · 제어기면 없음).</summary>
    public IReadOnlyList<int> Keys { get; internal set; }

    /// <summary>그림 — 바뀔 때만 다시 그린다.</summary>
    public FenceChipPicture? Picture
    {
        get => _picture;
        internal set
        {
            if (ReferenceEquals(_picture, value)) return;
            _picture = value;
            InvalidateVisual();
        }
    }

    /// <summary>적중 사각형(칩 좌표) — 센서는 몸(아이콘)만. 요소의 배치 사각형 · 선택 윤곽과 같다.</summary>
    public Rect HitBounds => _picture?.Hit ?? Rect.Empty;

    /// <summary>그림 범위(칩 좌표) — 적중 밖에 그리는 번호판까지. 이웃 칩과의 화면 간격만 이것으로 잰다.</summary>
    public Rect FootprintBounds => _picture?.Footprint ?? Rect.Empty;

    /// <summary>이 칩을 그린 횟수(시험 · 성능 확인).</summary>
    internal int RenderCount { get; private set; }

    protected override void OnRender(DrawingContext drawingContext)
    {
        RenderCount++;
        if (_picture is null) return;
        // 칩의 배치 사각형 = 적중 사각형 — 그림(앵커 원점 기준)을 그만큼 옮겨 그린다.
        drawingContext.PushTransform(new TranslateTransform(-_picture.Hit.X, -_picture.Hit.Y));
        FenceRenderer.Draw(drawingContext, this, _picture.Shapes);
        drawingContext.Pop();
    }

    /// <summary>
    /// 누르는 자리는 배치 사각형(= 적중 사각형 · 몸)뿐 — 그 밖에 그린 번호판 · "뒤" 표지는 적중하지 않아 누름이 캔버스로 간다
    /// (사용자: "그건 adorner에 안잡히게 해라" · 번호판을 눌러 센서를 고르거나 끌지 않는다).
    /// </summary>
    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        if (_picture is null) return null;
        return new Rect(RenderSize).Contains(hitTestParameters.HitPoint) ? new PointHitTestResult(this, hitTestParameters.HitPoint) : null;
    }

    protected override GeometryHitTestResult? HitTestCore(GeometryHitTestParameters hitTestParameters)
    {
        if (_picture is null) return null;
        var detail = hitTestParameters.HitGeometry.FillContainsWithDetail(new RectangleGeometry(new Rect(RenderSize)));
        return detail == IntersectionDetail.Empty ? null : new GeometryHitTestResult(this, detail);
    }

    // 캔버스가 누름 · 이동 · 뗌을 루트 기준으로 잰다 — Thumb 의 캡처 · DragStarted 를 끈다.
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { }
    protected override void OnMouseMove(MouseEventArgs e) { }

    protected override AutomationPeer OnCreateAutomationPeer() => new FenceChipAutomationPeer(this);

    public override string ToString() => $"{AutomationProperties.GetAutomationId(this)} {AutomationProperties.GetName(this)}";
}

/// <summary>칩의 UIA peer — 이름은 캔버스가 붙인 <c>AutomationProperties.Name</c>, 클래스 이름은 형식 이름.</summary>
public sealed class FenceChipAutomationPeer : ThumbAutomationPeer
{
    public FenceChipAutomationPeer(FenceChip owner) : base(owner) { }

    protected override string GetClassNameCore() => nameof(FenceChip);

    protected override AutomationControlType GetAutomationControlTypeCore()
        => ((FenceChip)Owner).Kind switch
        {
            FenceChipKind.Controller => AutomationControlType.Slider,
            FenceChipKind.Panel or FenceChipKind.ConceptNode => AutomationControlType.ListItem,
            _ => AutomationControlType.Button,
        };

    protected override bool IsKeyboardFocusableCore() => true;
}

/// <summary>
/// 펜스 덧그림 — 삽입 막대 · 알약. <see cref="AdornerLayer"/> 에 얹는다(컨테이너 로컬 값을 쓰지 않는다 · drag-first-ux).
/// 세계 좌표 그림을 뷰포트 변환으로 옮겨 그린다. 그림이 <b>바뀔 때만</b> 다시 그린다.
/// </summary>
public sealed class FenceOverlayAdorner : Adorner
{
    private IReadOnlyList<FenceShape> _shapes = Array.Empty<FenceShape>();
    private IReadOnlyList<FenceShape> _screen = Array.Empty<FenceShape>();
    private Matrix _world = Matrix.Identity;

    public FenceOverlayAdorner(UIElement adorned) : base(adorned)
    {
        IsHitTestVisible = false;
    }

    internal int RenderCount { get; private set; }

    internal IReadOnlyList<FenceShape> Shapes => _shapes;

    /// <summary>화면 좌표로 그리는 것(러버밴드 — 배율과 무관하게 1px 점선).</summary>
    internal IReadOnlyList<FenceShape> ScreenShapes => _screen;

    public void Show(IReadOnlyList<FenceShape> shapes, Matrix world, IReadOnlyList<FenceShape>? screen = null)
    {
        _shapes = shapes;
        _screen = screen ?? Array.Empty<FenceShape>();
        _world = world;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        RenderCount++;
        if (_shapes.Count == 0 && _screen.Count == 0) return;
        drawingContext.PushClip(new RectangleGeometry(new Rect(AdornedElement.RenderSize)));
        drawingContext.PushTransform(new MatrixTransform(_world));
        FenceRenderer.Draw(drawingContext, (FrameworkElement)AdornedElement, _shapes);
        drawingContext.Pop();
        FenceRenderer.Draw(drawingContext, (FrameworkElement)AdornedElement, _screen);
        drawingContext.Pop();
    }
}

/// <summary>정적 층 — 땅 · 망 · 기둥 · 선. 체인 · 보기 방식 · 함체 틈이 바뀔 때만 다시 그린다.</summary>
internal sealed class FenceStaticLayer : FrameworkElement
{
    private IReadOnlyList<FenceShape> _shapes = Array.Empty<FenceShape>();
    private Geometry? _clip;

    public FenceStaticLayer()
    {
        IsHitTestVisible = false;
    }

    internal int RenderCount { get; private set; }

    internal IReadOnlyList<FenceShape> Shapes => _shapes;

    public void Show(IReadOnlyList<FenceShape> shapes, Geometry? rangeClip)
    {
        _shapes = shapes;
        _clip = rangeClip;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        RenderCount++;
        FenceRenderer.Draw(drawingContext, this, _shapes, _clip);
    }
}
