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
        Cursor = kind == FenceChipKind.Controller ? Cursors.SizeWE : Cursors.Hand;
        AutomationProperties.SetAutomationId(this, kind switch
        {
            FenceChipKind.Sensor => SENSOR_ID_PREFIX + key,
            FenceChipKind.Group => GROUP_ID_PREFIX + key,
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

    /// <summary>적중 사각형(칩 좌표).</summary>
    public Rect HitBounds => _picture?.Hit ?? Rect.Empty;

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
        => ((FenceChip)Owner).Kind == FenceChipKind.Controller ? AutomationControlType.Slider : AutomationControlType.Button;

    protected override bool IsKeyboardFocusableCore() => true;
}

/// <summary>
/// 펜스 덧그림 — 삽입 막대 · 알약. <see cref="AdornerLayer"/> 에 얹는다(컨테이너 로컬 값을 쓰지 않는다 · drag-first-ux).
/// 세계 좌표 그림을 뷰포트 변환으로 옮겨 그린다. 그림이 <b>바뀔 때만</b> 다시 그린다.
/// </summary>
public sealed class FenceOverlayAdorner : Adorner
{
    private IReadOnlyList<FenceShape> _shapes = Array.Empty<FenceShape>();
    private Matrix _world = Matrix.Identity;

    public FenceOverlayAdorner(UIElement adorned) : base(adorned)
    {
        IsHitTestVisible = false;
    }

    internal int RenderCount { get; private set; }

    internal IReadOnlyList<FenceShape> Shapes => _shapes;

    public void Show(IReadOnlyList<FenceShape> shapes, Matrix world)
    {
        _shapes = shapes;
        _world = world;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        RenderCount++;
        if (_shapes.Count == 0) return;
        drawingContext.PushClip(new RectangleGeometry(new Rect(AdornedElement.RenderSize)));
        drawingContext.PushTransform(new MatrixTransform(_world));
        FenceRenderer.Draw(drawingContext, (FrameworkElement)AdornedElement, _shapes);
        drawingContext.Pop();
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
