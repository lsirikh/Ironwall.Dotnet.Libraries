using Microsoft.Xaml.Behaviors;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Matrix;

/// <summary>동작 열이 몇 번째인지(0 조회 · 1 편집 · 2 삭제 · 3 제어) 열에 붙인다.</summary>
public static class PermissionColumn
{
    public static readonly DependencyProperty VerbProperty = DependencyProperty.RegisterAttached(
        "Verb", typeof(int), typeof(PermissionColumn), new PropertyMetadata(-1));

    public static int GetVerb(DependencyObject d) => (int)d.GetValue(VerbProperty);
    public static void SetVerb(DependencyObject d, int value) => d.SetValue(VerbProperty, value);
}

/// <summary>
/// 권한 매트릭스 <b>드래그 페인팅</b> — 칸을 눌러 뒤집고, 누른 채 쓸면 같은 행 · 열에 같은 값을 칠한다.
/// (설계 정본 all-windows-drag-wireframe.html L347-L348 · L422)
/// </summary>
/// <remarks>
/// <para>캡처 드래그다: <c>PreviewMouseLeftButtonDown</c>(압력) → <c>MouseMove</c>(데드존 8.0 DIU 통과) →
/// <c>PreviewMouseLeftButtonUp</c>/<c>LostMouseCapture</c>. 종료는 <see cref="FinishPaint"/> 한 곳으로 모은다.
/// OLE <c>DoDragDrop</c> 은 쓰지 않는다.</para>
/// <para>순서: ① 플래그 ② 구독 해제 ③ 캡처 해제 ④ 통지 — 캡처를 먼저 풀면 재진입한다.</para>
/// <para>판정(어느 축 · 어느 칸)은 <see cref="PermissionPaintMath"/> 의 순수 함수가 한다 — 여기서는 좌표만 고른다.</para>
/// </remarks>
public class PermissionPaintBehavior : Behavior<DataGrid>
{
    private bool _pressed;
    private bool _sweeping;
    private Point _pressPoint;
    private FrameworkElement? _root;

    /// <summary>칠할 대상. 없으면 아무 일도 하지 않는다.</summary>
    public static readonly DependencyProperty MatrixProperty = DependencyProperty.Register(
        nameof(Matrix), typeof(PermissionMatrixConsoleViewModel), typeof(PermissionPaintBehavior));

    public PermissionMatrixConsoleViewModel? Matrix
    {
        get => (PermissionMatrixConsoleViewModel?)GetValue(MatrixProperty);
        set => SetValue(MatrixProperty, value);
    }

    /// <summary>
    /// 같은 일을 키보드 · 버튼으로 하는 길 — 비어 있으면 드래그 전용 UI 가 된다(규칙 위반).
    /// 여기서는 <c>Space</c>(칸) · 머리글 클릭(열 전체) · 행 머리 버튼(행 전체)이다.
    /// </summary>
    public string KeyboardFallback { get; set; } = "Space · 열 머리글 클릭 · 행 [전체] 버튼";

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewMouseLeftButtonDown += OnPress;
        AssociatedObject.MouseMove += OnMove;
        // 뗌은 터널(Preview)에서 받는다 — DataGrid 는 버블 MouseUp 의 클래스 처리기에서 캡처를 먼저 풀어,
        // 버블에서 받으면 LostMouseCapture(= 취소)가 먼저 와 칠한 것이 전부 되돌아갔다(2026-09-27 실창 기록).
        AssociatedObject.PreviewMouseLeftButtonUp += OnRelease;
        AssociatedObject.LostMouseCapture += OnLostCapture;
        AssociatedObject.Unloaded += OnUnloaded;
    }

    protected override void OnDetaching()
    {
        FinishPaint(commit: false);
        AssociatedObject.PreviewMouseLeftButtonDown -= OnPress;
        AssociatedObject.MouseMove -= OnMove;
        AssociatedObject.PreviewMouseLeftButtonUp -= OnRelease;
        AssociatedObject.LostMouseCapture -= OnLostCapture;
        AssociatedObject.Unloaded -= OnUnloaded;
        base.OnDetaching();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => FinishPaint(commit: false);

    private void OnPress(object sender, MouseButtonEventArgs e)
    {
        if (Matrix is null || _pressed) return;
        if (CellAt(e.OriginalSource as DependencyObject) is not { } cell) return;

        bool began;
        using (Matrix.BeginPaintBatch()) began = Matrix.Painter.Begin(cell);
        if (!began) return;

        _pressed = true;
        _sweeping = false;
        _pressPoint = e.GetPosition(AssociatedObject);
        _root = Window.GetWindow(AssociatedObject) as FrameworkElement ?? AssociatedObject;
        _root.PreviewKeyDown += OnRootPreviewKeyDown;

        // 체크박스가 스스로 한 번 더 뒤집지 않게 여기서 소비한다(두 번 뒤집기 방지).
        e.Handled = true;
        AssociatedObject.CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || Matrix is null) return;

        var now = e.GetPosition(AssociatedObject);
        if (!_sweeping)
        {
            if (!DragMath.IsDrag(now.X - _pressPoint.X, now.Y - _pressPoint.Y)) return;  // 데드존 미만 = 클릭(한 칸 토글)
            _sweeping = true;
        }

        if (CellAt(HitTest(now)) is not { } cell) return;
        using (Matrix.BeginPaintBatch()) Matrix.Painter.MoveTo(cell);
    }

    private void OnRelease(object sender, MouseButtonEventArgs e)
    {
        if (Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag.DragTrace.IsOn) Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag.DragTrace.Write($"[paint] release source={Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag.DragTrace.Chain(e.OriginalSource as DependencyObject, 3)}");
        FinishPaint(commit: true);
    }

    /// <summary>
    /// 캡처 상실 = <b>취소</b>다. 커널의 드래그 계약과 <see cref="PermissionPainter.Cancel"/> 의 문서가 그렇게 적혀 있고,
    /// 모달·포커스 도둑질로 캡처를 잃었을 때 "칠한 대로 굳히는" 것은 사용자가 의도한 적 없는 변경이다.
    /// </summary>
    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        if (Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag.DragTrace.IsOn) Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag.DragTrace.Write($"[paint] lost capture now={Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag.DragTrace.Captured()} button={Mouse.LeftButton}");
        FinishPaint(commit: false);
    }

    private void OnRootPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 터널에서, 칠하는 중일 때만 소비한다 — 무조건 소비하면 Esc 로 선택을 푸는 기존 동작이 깨진다.
        if (!_pressed || e.Key != Key.Escape) return;
        e.Handled = true;
        FinishPaint(commit: false);
    }

    /// <summary>끝남 한 곳 — 마우스 업 · 캡처 상실 · Esc · 분리 · 언로드가 전부 이리로 온다.</summary>
    private void FinishPaint(bool commit)
    {
        if (!_pressed) return;

        // ① 플래그
        _pressed = false;
        _sweeping = false;

        // ② 구독 해제
        if (_root is not null)
        {
            _root.PreviewKeyDown -= OnRootPreviewKeyDown;
            _root = null;
        }

        // ③ 캡처 해제
        if (AssociatedObject.IsMouseCaptured) AssociatedObject.ReleaseMouseCapture();

        // ④ 통지
        if (Matrix is null) return;
        using (Matrix.BeginPaintBatch())
        {
            if (commit) Matrix.Painter.Finish();
            else Matrix.Painter.Cancel();
        }
    }

    private DependencyObject? HitTest(Point point)
        => Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag.DragHitTest.Top(AssociatedObject, point);

    /// <summary>
    /// 좌표 아래의 칸 — HitTest → <see cref="DataGridCell"/> → 행은 <c>ItemContainerGenerator</c> 로 역산한다.
    /// y 산술 · <c>ContainerFromIndex</c> 는 쓰지 않는다(행 높이가 다르고 컨테이너가 재활용된다).
    /// </summary>
    private PermissionCell? CellAt(DependencyObject? hit)
    {
        var cell = Ancestor<DataGridCell>(hit);
        if (cell is null) return null;

        var verb = PermissionColumn.GetVerb(cell.Column);
        if (verb < 0) return null;

        var rowContainer = Ancestor<DataGridRow>(cell);
        if (rowContainer is null) return null;

        var item = AssociatedObject.ItemContainerGenerator.ItemFromContainer(rowContainer);
        var index = AssociatedObject.Items.IndexOf(item);
        return index < 0 ? null : new PermissionCell(index, verb);
    }

    private static T? Ancestor<T>(DependencyObject? start) where T : DependencyObject
    {
        for (var d = start; d is not null; d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d))
            if (d is T found) return found;
        return null;
    }
}
