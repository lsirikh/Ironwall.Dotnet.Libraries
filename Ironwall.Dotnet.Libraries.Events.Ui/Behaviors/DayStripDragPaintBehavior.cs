using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Microsoft.Xaml.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Behaviors;
/****************************************************************************
   Purpose      : 요일 스트립 드래그 범위 페인팅(전사 드래그 우선 방침).
                  컨테이너가 PreviewMouseDown(터널)에서 선점해 ButtonBase 의
                  캡처 강탈을 막고, 스스로 캡처해 구간을 칠하거나 지운다.
                  클릭 폴백 · ESC 취소 · 키보드 경로를 모두 보존한다.
   Created By   : GHLee
   Created On   : 2026-09-08
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 요일 <see cref="ToggleButton"/> 스트립에 드래그 범위 선택을 붙인다.
/// <para><b>왜 터널에서 선점하는가</b> — <see cref="ToggleButton"/> 은 <see cref="ButtonBase"/> 파생이고
/// <c>OnMouseLeftButtonDown</c> 을 재정의하지 않으며 <c>ClickMode</c> 기본값이 <c>Release</c> 라
/// 그대로 두면 버블 단계에서 <c>CaptureMouse()</c> 로 캡처를 빼앗는다(리플렉션 실측).
/// 다만 이미 <c>Handled=true</c> 인 이벤트에서는 그 virtual 이 <b>호출되지 않는다</b>
/// (클래스 핸들러가 <c>handledEventsToo:false</c> — 실측 5회 동일). 그래서 터널 선점이 보호가 된다.</para>
/// <para>⚠ <c>PreviewMouseLeftButtonDown</c> 은 <b>Direct</b> 라우팅이라 자손의 다운에 대해
/// 조상에서 신뢰할 수 없다. 실제로 터널링하는 <see cref="UIElement.PreviewMouseDownEvent"/> 를 쓴다.</para>
/// <para>UIA <c>TogglePattern.Toggle()</c> 과 <c>Space</c> 키는 마우스 경로를 타지 않으므로 그대로 동작한다.</para>
/// </summary>
public class DayStripDragPaintBehavior : Behavior<FrameworkElement>
{
    #region - Dependency Properties -

    /// <summary>요일 비트마스크(월1 … 일64). VM 과 TwoWay 로 묶는다.</summary>
    public static readonly DependencyProperty MaskProperty = DependencyProperty.Register(
        nameof(Mask), typeof(int), typeof(DayStripDragPaintBehavior),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>요일 비트마스크.</summary>
    public int Mask
    {
        get => (int)GetValue(MaskProperty);
        set => SetValue(MaskProperty, value);
    }

    /// <summary>
    /// 제스처 범위 표식(첨부) — 칩 <c>ControlTemplate</c> 이 Trigger 로 읽는다.
    /// <para>⚠ 컨테이너에 <b>로컬 값을 write 하지 않는다</b>. 로컬 값은 Style Trigger 를 이겨
    /// 선택 표시와 충돌하고, 재활용 컨트롤에서는 다른 데이터로 따라간다.
    /// 첨부 속성은 Trigger 가 읽어 갈 뿐이라 그 문제가 없다.</para>
    /// </summary>
    public static readonly DependencyProperty IsInGestureRangeProperty = DependencyProperty.RegisterAttached(
        "IsInGestureRange", typeof(bool), typeof(DayStripDragPaintBehavior),
        new PropertyMetadata(false));

    /// <summary>첨부 속성 읽기.</summary>
    public static bool GetIsInGestureRange(DependencyObject o) => (bool)o.GetValue(IsInGestureRangeProperty);
    /// <summary>첨부 속성 쓰기.</summary>
    public static void SetIsInGestureRange(DependencyObject o, bool v) => o.SetValue(IsInGestureRangeProperty, v);

    #endregion

    #region - Lifecycle -

    protected override void OnAttached()
    {
        base.OnAttached();
        // ⚠ 시각트리 탐색은 Loaded 에서 한다 — OnAttached 시점엔 부모 체인이 없을 수 있다.
        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.Unloaded += OnUnloaded;

        AssociatedObject.PreviewMouseDown += OnPreviewMouseDown;   // ★ 터널 — 여기서 선점한다
        AssociatedObject.MouseMove += OnMouseMove;
        AssociatedObject.MouseLeftButtonUp += OnMouseUp;
        AssociatedObject.LostMouseCapture += OnLostCapture;
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;       // ★ ESC 는 터널에서 선점
    }

    protected override void OnDetaching()
    {
        AssociatedObject.Loaded -= OnLoaded;
        AssociatedObject.Unloaded -= OnUnloaded;
        AssociatedObject.PreviewMouseDown -= OnPreviewMouseDown;
        AssociatedObject.MouseMove -= OnMouseMove;
        AssociatedObject.MouseLeftButtonUp -= OnMouseUp;
        AssociatedObject.LostMouseCapture -= OnLostCapture;
        AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        _cells = null;
        base.OnDetaching();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => _cells = FindCells();
    private void OnUnloaded(object sender, RoutedEventArgs e) => FinishDrag(commit: false);

    #endregion

    #region - Mouse -

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var cells = EnsureCells();
        if (cells.Count == 0) return;

        var pos = e.GetPosition(AssociatedObject);
        var idx = IndexAt(pos, cells);
        if (idx < 0) return;

        _pressed = true;
        _dragging = false;
        _origin = pos;
        _startIndex = idx;
        _currentIndex = idx;
        _maskAtStart = Mask;                                   // ESC 복원 기준
        _paintIntent = DayStripDragMath.DecidePaintIntent(Mask, idx);

        // ★ 선점 — 이걸 안 하면 ButtonBase 가 버블에서 캡처를 빼앗는다.
        e.Handled = true;
        AssociatedObject.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed) return;

        var pos = e.GetPosition(AssociatedObject);
        if (!_dragging)
        {
            // 데드존 — 정확히 8.0 은 드래그가 아니다(클릭으로 폴백).
            if (!DayStripDragMath.IsDrag(pos.X - _origin.X, pos.Y - _origin.Y)) return;
            _dragging = true;
        }

        var cells = EnsureCells();
        var idx = IndexAt(pos, cells);
        if (idx < 0 || idx == _currentIndex) return;           // 후보가 바뀔 때만 갱신

        _currentIndex = idx;
        Mask = DayStripDragMath.ApplyRange(_maskAtStart, _startIndex, _currentIndex, _paintIntent);
        UpdateRangeVisual();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e) => FinishDrag(commit: true);

    /// <summary>캡처를 잃으면(Alt+Tab·RDP 전환 등) 커밋 없이 종료한다.</summary>
    private void OnLostCapture(object sender, MouseEventArgs e) => FinishDrag(commit: false);

    /// <summary>
    /// 종료 경로는 <b>하나</b>다 — Up 과 LostCapture 양쪽에서 부른다.
    /// 순서: ①플래그 clear ②시각 복원 ③캡처 해제 ④커밋.
    /// 캡처를 먼저 풀면 <see cref="OnLostCapture"/> 가 재진입한다.
    /// </summary>
    private void FinishDrag(bool commit)
    {
        if (!_pressed) return;

        var wasDragging = _dragging;
        var startIdx = _startIndex;

        _pressed = false;
        _dragging = false;
        ClearRangeVisual();

        if (AssociatedObject.IsMouseCaptured) AssociatedObject.ReleaseMouseCapture();

        if (!commit)
        {
            Mask = _maskAtStart;                               // 취소 — 시작 상태로 복원
            return;
        }

        if (!wasDragging)
        {
            // 클릭 폴백 — 터널에서 Handled 했으므로 ButtonBase 가 안 돌았다. 내가 직접 토글한다.
            Mask = DayStripDragMath.ApplyRange(
                _maskAtStart, startIdx, startIdx, DayStripDragMath.DecidePaintIntent(_maskAtStart, startIdx));
        }
        // 드래그였다면 MouseMove 에서 이미 반영돼 있다.
    }

    #endregion

    #region - Keyboard -

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // ⚠ 드래그 중일 때만 소비한다. 무조건 소비하면 기존 ESC 동작(선택 해제 등)을 깬다.
        if (e.Key != Key.Escape || !_pressed) return;
        FinishDrag(commit: false);
        e.Handled = true;
    }

    #endregion

    #region - Cells -

    private IReadOnlyList<ToggleButton> EnsureCells() => _cells ??= FindCells();

    private List<ToggleButton> FindCells()
    {
        var list = new List<ToggleButton>();
        Walk(AssociatedObject);
        return list;

        void Walk(DependencyObject o)
        {
            var n = VisualTreeHelper.GetChildrenCount(o);
            for (var i = 0; i < n; i++)
            {
                var c = VisualTreeHelper.GetChild(o, i);
                if (c is ToggleButton tb) list.Add(tb);
                else Walk(c);
            }
        }
    }

    /// <summary>좌표 → 셀 인덱스. 셀 실제 위치로 역산하고, 벗어나면 양 끝으로 clamp 한다.</summary>
    private int IndexAt(Point pos, IReadOnlyList<ToggleButton> cells)
    {
        if (cells.Count == 0) return -1;

        for (var i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (!c.IsVisible) continue;
            var origin = c.TranslatePoint(new Point(0, 0), AssociatedObject);
            if (pos.X >= origin.X && pos.X < origin.X + c.ActualWidth) return i;
        }

        // 스트립 밖으로 끌어도 선택이 끊기지 않아야 한다.
        var first = cells[0].TranslatePoint(new Point(0, 0), AssociatedObject);
        return pos.X < first.X ? 0 : cells.Count - 1;
    }

    private void UpdateRangeVisual()
    {
        var cells = EnsureCells();
        for (var i = 0; i < cells.Count; i++)
            SetIsInGestureRange(cells[i],
                DayStripDragMath.IsInRange(_dragging, _startIndex, _currentIndex, i));
    }

    private void ClearRangeVisual()
    {
        if (_cells is null) return;
        foreach (var c in _cells) SetIsInGestureRange(c, false);
    }

    #endregion

    #region - Attributes -
    private List<ToggleButton>? _cells;
    private bool _pressed;
    private bool _dragging;
    private Point _origin;
    private int _startIndex;
    private int _currentIndex;
    private int _maskAtStart;
    private bool _paintIntent;
    #endregion
}
