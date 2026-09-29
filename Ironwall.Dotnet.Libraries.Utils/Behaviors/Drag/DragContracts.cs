using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

/// <summary>끌고 있는 것. 여러 행을 골랐으면 전부 실린다(목록에서의 순서대로).</summary>
public sealed class DragPayload
{
    public DragPayload(ItemsControl source, IReadOnlyList<object> items, string label)
    {
        Source = source;
        Items = items;
        Label = label;
    }

    /// <summary>끌기 시작한 목록.</summary>
    public ItemsControl Source { get; }
    public IReadOnlyList<object> Items { get; }
    /// <summary>고스트에 찍을 글자(첫 항목). 건수는 고스트가 따로 붙인다.</summary>
    public string Label { get; }
    public int Count => Items.Count;

    /// <summary><paramref name="list"/> 안에서 이 항목들의 인덱스(오름차순). 순서 드래그의 <see cref="DragMath.MoveMany{T}"/> 에 그대로 넘긴다.</summary>
    public IReadOnlyList<int> IndexesIn(IList list)
        => Items.Select(list.IndexOf).Where(i => i >= 0).OrderBy(i => i).ToList();
}

/// <summary>놓을 곳.</summary>
/// <param name="ZoneKey">드롭존의 종류 — 예: <c>group</c>, <c>unit</c>, <c>server</c>, <c>board</c>.</param>
/// <param name="ZoneData">그 드롭존이 가리키는 것(보통 칩의 DataContext).</param>
/// <param name="InsertionIndex">순서 드롭존이면 삽입 인덱스(0..Count), 아니면 -1.</param>
public sealed record DropTarget(string ZoneKey, object? ZoneData, int InsertionIndex)
{
    public bool IsReorder => InsertionIndex >= 0;
}

/// <summary>
/// 드롭 판정과 처리 — 창(뷰모델)이 구현한다. 커널은 서버를 모른다.
/// </summary>
/// <remarks>
/// 호출 1회로 끝나는 드롭은 <see cref="Drop"/> 안에서 곧바로 보내고, N회로 번지는 드롭은 Draft 트레이에 쌓는다.
/// </remarks>
public interface IDragDropHandler
{
    /// <summary>여기에 놓을 수 있는가. 끄는 동안 자주 불린다 — 가볍게, 서버 호출 없이.</summary>
    bool CanDrop(DragPayload payload, DropTarget target);

    void Drop(DragPayload payload, DropTarget target);
}

/// <summary>
/// 놓을 수 없는 자리에서 놓았음을 담당에게 알린다 — <b>선택</b> 구현(<see cref="IDragDropHandler"/> 와 같은 객체에 붙인다).
/// </summary>
/// <remarks>
/// 거절은 끄는 동안 형태(사선 해치 · 삽입선 없음)로 보이지만, 놓는 순간에는 아무 일도 일어나지 않아
/// "왜 안 들어가지?" 가 남는다. 이 통지로 창이 사유를 상태줄에 말한다. 출발 목록 자신(또는 그것을 품은 드롭존)에
/// 도로 놓은 것은 그만두기라 알리지 않는다. 서버를 부르지 않는다 — 판정을 다시 해서 사유만 고른다.
/// </remarks>
public interface IDropRefusalHandler
{
    /// <summary><paramref name="target"/> 위에서 놓았는데 <see cref="IDragDropHandler.CanDrop"/> 가 거절했다.</summary>
    void Refused(DragPayload payload, DropTarget target);
}

/// <summary>끄는 동안 드롭존이 보이는 모습. 색이 아니라 <b>형태</b>로 구분한다.</summary>
public enum DropZoneState
{
    /// <summary>끌고 있지 않다.</summary>
    None,
    /// <summary>놓을 수 있다 — 파선 윤곽.</summary>
    Available,
    /// <summary>놓을 수 없다 — 사선 해치.</summary>
    Blocked,
    /// <summary>놓을 수 있고 지금 그 위에 있다 — 굵은 윤곽.</summary>
    Hover,
}

/// <summary>
/// 드롭존 붙임 속성. OLE 를 쓰지 않으므로 <c>AllowDrop</c> · <c>DragOver</c> 가 아니라 <b>좌표 HitTest</b> 로 판정한다.
/// </summary>
public static class DropZone
{
    private static readonly List<WeakReference<FrameworkElement>> Registry = new();

    /// <summary>드롭존 종류. 값이 있으면 그 요소가 드롭존이다.</summary>
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(string), typeof(DropZone), new PropertyMetadata(null, OnKeyChanged));
    public static string? GetKey(DependencyObject d) => (string?)d.GetValue(KeyProperty);
    public static void SetKey(DependencyObject d, string? value) => d.SetValue(KeyProperty, value);

    /// <summary>이 드롭존이 가리키는 것. 비워 두면 요소의 DataContext.</summary>
    public static readonly DependencyProperty DataProperty = DependencyProperty.RegisterAttached(
        "Data", typeof(object), typeof(DropZone), new PropertyMetadata(null));
    public static object? GetData(DependencyObject d) => d.GetValue(DataProperty);
    public static void SetData(DependencyObject d, object? value) => d.SetValue(DataProperty, value);

    /// <summary>판정 · 처리 담당. 비워 두면 끌기 시작한 목록의 담당을 쓴다.</summary>
    public static readonly DependencyProperty HandlerProperty = DependencyProperty.RegisterAttached(
        "Handler", typeof(IDragDropHandler), typeof(DropZone), new PropertyMetadata(null));
    public static IDragDropHandler? GetHandler(DependencyObject d) => (IDragDropHandler?)d.GetValue(HandlerProperty);
    public static void SetHandler(DependencyObject d, IDragDropHandler? value) => d.SetValue(HandlerProperty, value);

    /// <summary>순서 드롭존인가 — <see cref="ItemsControl"/> 에 붙이면 행 사이에 삽입선이 뜬다.</summary>
    public static readonly DependencyProperty IsReorderProperty = DependencyProperty.RegisterAttached(
        "IsReorder", typeof(bool), typeof(DropZone), new PropertyMetadata(false));
    public static bool GetIsReorder(DependencyObject d) => (bool)d.GetValue(IsReorderProperty);
    public static void SetIsReorder(DependencyObject d, bool value) => d.SetValue(IsReorderProperty, value);

    /// <summary>끄는 동안의 모습(읽기용) — 스타일 트리거가 이 값을 본다.</summary>
    public static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(DropZoneState), typeof(DropZone), new FrameworkPropertyMetadata(DropZoneState.None, FrameworkPropertyMetadataOptions.Inherits));
    public static DropZoneState GetState(DependencyObject d) => (DropZoneState)d.GetValue(StateProperty);
    internal static void SetState(DependencyObject d, DropZoneState value) => d.SetValue(StateProperty, value);

    private static void OnKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe) return;
        if (e.NewValue is string { Length: > 0 }) Register(fe);
    }

    private static void Register(FrameworkElement fe)
    {
        lock (Registry)
        {
            Registry.RemoveAll(w => !w.TryGetTarget(out var t) || ReferenceEquals(t, fe));
            Registry.Add(new WeakReference<FrameworkElement>(fe));
        }
    }

    /// <summary><paramref name="root"/> 아래에서 지금 화면에 있는 드롭존들.</summary>
    internal static IReadOnlyList<FrameworkElement> ZonesUnder(DependencyObject root)
    {
        var live = new List<FrameworkElement>();
        lock (Registry)
        {
            Registry.RemoveAll(w => !w.TryGetTarget(out _));
            // 장부는 프로세스 전역이다 — 다른 UI 스레드의 드롭존은 읽기만 해도 던진다(끄는 도중 입력 처리 한가운데서).
            // 그 스레드의 드롭존은 어차피 이 창의 자손이 될 수 없으니 건너뛴다.
            foreach (var w in Registry)
                if (w.TryGetTarget(out var fe) && fe.CheckAccess() && fe.IsLoaded && fe.IsVisible && !string.IsNullOrEmpty(GetKey(fe)) && fe.IsDescendantOf(root))
                    live.Add(fe);
        }
        return live;
    }

    internal static DropTarget TargetOf(FrameworkElement zone, int insertionIndex)
        => new(GetKey(zone)!, GetData(zone) ?? zone.DataContext, insertionIndex);
}
