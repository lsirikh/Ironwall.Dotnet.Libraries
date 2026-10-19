using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/****************************************************************************
   Purpose      : 오버레이 이미지 「이미지 자르기」 판 VM — 다각형 그리기 · 꼭짓점 편집 · 확대/이동 · Undo/Redo · 결과.
   Note         : ★ 꼭짓점 좌표 = 원본 그림 픽셀. 화면 좌표 = 그림 좌표 × Scale + (OffsetX, OffsetY).
                  ★ 누르기 → 8 DIU 넘게 움직이면 끌기(drag-first-ux 데드존), 아니면 클릭으로 본다(Pointer*).
                  ★ Esc 1단계 = 그리던 점 취소, 그릴 것이 없으면 2단계 = 닫기 확인 표시.
                  ★ 결과는 Completion(Apply/Skip/Cancel) — 소유 VM(MapViewModel)이 await 한다.
   Created On   : 2026-10-19 · Sensorway Co., Ltd.
****************************************************************************/
public enum ImageCropOutcome { Apply, Skip, Cancel }

public sealed class ImageCropViewModel : PropertyChangedBase
{
    /// <summary>꼭짓점 · 선분 가운데 손잡이를 잡는 거리(DIU).</summary>
    public const double HandleRadius = 7d;
    public const double MinScale = 0.02;
    public const double MaxScale = 32d;

    private readonly TaskCompletionSource<ImageCropOutcome> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Stack<(Point[] Points, bool Closed)> _undo = new();
    private readonly Stack<(Point[] Points, bool Closed)> _redo = new();
    private List<Point> _points = new();
    private bool _isClosed;
    private double _scale = 1d, _offsetX, _offsetY;
    private string? _error;
    private bool _isCloseConfirmVisible;

    // 누르기 상태
    private Point? _pressScreen;
    private HandleHit _pressHit;
    private bool _dragging;
    private int _dragVertex = -1;

    public ImageCropViewModel(BitmapSource image, string title, bool allowSkip)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        Title = title;
        AllowSkip = allowSkip;
        ApplyCommand = new RelayCommand(Apply);
        SkipCommand = new RelayCommand(() => Complete(ImageCropOutcome.Skip), () => AllowSkip);
        CancelCommand = new RelayCommand(() => Complete(ImageCropOutcome.Cancel));
        UndoCommand = new RelayCommand(Undo, () => CanUndo);
        RedoCommand = new RelayCommand(Redo, () => CanRedo);
        FitCommand = new RelayCommand(() => Fit(ViewportWidth, ViewportHeight));
        ConfirmCloseCommand = new RelayCommand(() => Complete(ImageCropOutcome.Cancel));
        KeepEditingCommand = new RelayCommand(() => IsCloseConfirmVisible = false);
    }

    #region 상태

    public BitmapSource Image { get; }
    public int PixelWidth => Image.PixelWidth;
    public int PixelHeight => Image.PixelHeight;
    public string Title { get; }
    /// <summary>「자르지 않고 그대로」 보이기 — 처음 불러올 때만.</summary>
    public bool AllowSkip { get; }

    /// <summary>꼭짓점(그림 픽셀). 바꿀 때마다 새 목록.</summary>
    public IReadOnlyList<Point> Points => _points;
    public bool IsClosed => _isClosed;
    public int PointCount => _points.Count;
    public string PointCountText => _isClosed ? $"꼭짓점 {_points.Count}개 · 닫힘" : $"꼭짓점 {_points.Count}개";

    public double Scale => _scale;
    public double OffsetX => _offsetX;
    public double OffsetY => _offsetY;
    /// <summary>그림 표시 판 크기(DIU) — 뷰가 알려 준다. 「화면에 맞추기」에 쓴다.</summary>
    public double ViewportWidth { get; set; }
    public double ViewportHeight { get; set; }

    /// <summary>적용을 막은 이유 한 줄(없으면 null).</summary>
    public string? Error
    {
        get => _error;
        private set { _error = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasError)); }
    }
    public bool HasError => !string.IsNullOrEmpty(_error);

    public bool IsCloseConfirmVisible
    {
        get => _isCloseConfirmVisible;
        set { _isCloseConfirmVisible = value; NotifyOfPropertyChange(); }
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public Task<ImageCropOutcome> Completion => _completion.Task;
    public bool IsCompleted => _completion.Task.IsCompleted;

    /// <summary>다각형이 바뀌거나 보기가 바뀌면 — 뷰가 다시 그린다.</summary>
    public event EventHandler? Changed;

    public ICommand ApplyCommand { get; }
    public ICommand SkipCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand FitCommand { get; }
    public ICommand ConfirmCloseCommand { get; }
    public ICommand KeepEditingCommand { get; }

    #endregion

    #region 좌표 · 보기

    public Point ScreenToImage(Point screen) => new((screen.X - _offsetX) / _scale, (screen.Y - _offsetY) / _scale);
    public Point ImageToScreen(Point image) => new(image.X * _scale + _offsetX, image.Y * _scale + _offsetY);

    /// <summary>그림 전체가 판 안에 들어오게(가운데 · 여백 4%).</summary>
    public void Fit(double viewWidth, double viewHeight)
    {
        if (viewWidth <= 0 || viewHeight <= 0 || PixelWidth <= 0 || PixelHeight <= 0) return;
        _scale = Math.Clamp(Math.Min(viewWidth / PixelWidth, viewHeight / PixelHeight) * 0.96, MinScale, MaxScale);
        _offsetX = (viewWidth - PixelWidth * _scale) / 2d;
        _offsetY = (viewHeight - PixelHeight * _scale) / 2d;
        RaiseView();
    }

    /// <summary>화면 점 anchor 아래의 그림 점이 그대로 있도록 factor 배 확대/축소.</summary>
    public void ZoomAt(Point anchor, double factor)
    {
        var image = ScreenToImage(anchor);
        _scale = Math.Clamp(_scale * factor, MinScale, MaxScale);
        _offsetX = anchor.X - image.X * _scale;
        _offsetY = anchor.Y - image.Y * _scale;
        RaiseView();
    }

    public void PanBy(double dx, double dy)
    {
        _offsetX += dx;
        _offsetY += dy;
        RaiseView();
    }

    private void RaiseView()
    {
        NotifyOfPropertyChange(nameof(Scale));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region 손잡이 찾기

    public enum HandleKind { None, Vertex, Midpoint }
    public readonly record struct HandleHit(HandleKind Kind, int Index);

    /// <summary>화면 점 근처의 꼭짓점(우선) 또는 선분 가운데 손잡이.</summary>
    public HandleHit HitTest(Point screen)
    {
        for (int i = _points.Count - 1; i >= 0; i--)
            if ((ImageToScreen(_points[i]) - screen).Length <= HandleRadius) return new(HandleKind.Vertex, i);
        foreach (var (index, mid) in Midpoints())
            if ((ImageToScreen(mid) - screen).Length <= HandleRadius) return new(HandleKind.Midpoint, index);
        return new(HandleKind.None, -1);
    }

    /// <summary>선분 가운데 손잡이(선분 i = 꼭짓점 i → i+1). 닫히지 않았으면 마지막 → 첫 선분은 없다.</summary>
    public IEnumerable<(int EdgeIndex, Point Point)> Midpoints()
    {
        int edges = _isClosed ? _points.Count : _points.Count - 1;
        for (int i = 0; i < edges; i++)
        {
            var a = _points[i];
            var b = _points[(i + 1) % _points.Count];
            yield return (i, new Point((a.X + b.X) / 2d, (a.Y + b.Y) / 2d));
        }
    }

    #endregion

    #region 누르기 · 끌기 · 놓기 (왼쪽 단추)

    /// <summary>마지막으로 누른 꼭짓점(Delete 로 지울 점) — 없으면 -1.</summary>
    public int SelectedVertex { get; private set; } = -1;

    /// <summary>끄는 중인 꼭짓점(뷰가 두껍게 그린다) — 없으면 -1.</summary>
    public int DraggingVertex => _dragVertex;
    public bool IsDragging => _dragging;

    public void PointerDown(Point screen)
    {
        IsCloseConfirmVisible = false;
        _pressScreen = screen;
        _pressHit = HitTest(screen);
        _dragging = false;
        _dragVertex = -1;
        SelectedVertex = _pressHit.Kind == HandleKind.Vertex ? _pressHit.Index : -1;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>캡처를 잃었거나 끄는 중 Esc — 끌기 전 모양으로. 끌지 않았으면 누르기만 잊는다. 처리했으면(끄는 중이었으면) true.</summary>
    public bool PointerCancel()
    {
        bool wasDragging = _dragging;
        _pressScreen = null;
        _dragging = false;
        _dragVertex = -1;
        if (!wasDragging) return false;
        Restore(_undo.Pop());
        return true;
    }

    public void PointerMove(Point screen)
    {
        if (_pressScreen is not { } press) return;
        if (!_dragging)
        {
            if (!DragMath.IsDrag(screen.X - press.X, screen.Y - press.Y)) return;
            if (_pressHit.Kind == HandleKind.None) return;   // 빈 곳 끌기는 아무것도 안 한다(이동은 가운데 단추 · Space)
            _dragging = true;
            Snapshot();
            if (_pressHit.Kind == HandleKind.Vertex) _dragVertex = _pressHit.Index;
            else
            {
                _dragVertex = _pressHit.Index + 1;
                _points.Insert(_dragVertex, ScreenToImage(screen));
            }
        }
        _points[_dragVertex] = ClampToImage(ScreenToImage(screen));
        RaisePolygon();
    }

    public void PointerUp(Point screen)
    {
        if (_pressScreen is null) return;
        var hit = _pressHit;
        bool dragged = _dragging;
        _pressScreen = null;
        _dragging = false;
        _dragVertex = -1;
        if (dragged) return;
        if (_isClosed) return;
        if (hit.Kind == HandleKind.Vertex && hit.Index == 0 && _points.Count >= 3) { Close(); return; }
        if (hit.Kind == HandleKind.Vertex) return;   // 다른 꼭짓점 클릭 = 고르기만(Delete 대상) — 같은 자리에 점을 겹쳐 찍지 않는다
        AddPoint(ScreenToImage(screen));
    }

    /// <summary>두 번 누르기 = 닫기(첫 누르기가 이미 점을 놓았다).</summary>
    public void DoubleClick() => Close();

    /// <summary>오른쪽 단추 — 꼭짓점 위면 지운다.</summary>
    public void RightClick(Point screen)
    {
        var hit = HitTest(screen);
        if (hit.Kind == HandleKind.Vertex) RemoveVertex(hit.Index);
    }

    #endregion

    #region 편집

    public void AddPoint(Point image)
    {
        if (_isClosed) return;
        Snapshot();
        _points.Add(ClampToImage(image));
        RaisePolygon();
    }

    /// <summary>다각형 닫기 — 3점 이상일 때만.</summary>
    public bool Close()
    {
        if (_isClosed || _points.Count < 3) return false;
        Snapshot();
        _isClosed = true;
        RaisePolygon();
        return true;
    }

    public void MoveVertex(int index, Point image)
    {
        if (index < 0 || index >= _points.Count) return;
        Snapshot();
        _points[index] = ClampToImage(image);
        RaisePolygon();
    }

    /// <summary>선분 edgeIndex(꼭짓점 edgeIndex → +1) 사이에 점 넣기.</summary>
    public void InsertVertex(int edgeIndex, Point image)
    {
        if (edgeIndex < 0 || edgeIndex >= _points.Count) return;
        Snapshot();
        _points.Insert(edgeIndex + 1, ClampToImage(image));
        RaisePolygon();
    }

    public void RemoveVertex(int index)
    {
        if (index < 0 || index >= _points.Count) return;
        Snapshot();
        _points.RemoveAt(index);
        SelectedVertex = -1;
        if (_points.Count < 3) _isClosed = false;
        RaisePolygon();
    }

    /// <summary>Backspace — 마지막 점 지우기.</summary>
    public void RemoveLast() => RemoveVertex(_points.Count - 1);

    /// <summary>Esc — 그리던 점이 있으면 취소(true), 없으면 닫기 확인을 띄운다. 확인이 떠 있으면 취소로 닫는다.</summary>
    public void Escape()
    {
        if (!_isClosed && _points.Count > 0)
        {
            Snapshot();
            _points.Clear();
            RaisePolygon();
            return;
        }
        if (IsCloseConfirmVisible) { Complete(ImageCropOutcome.Cancel); return; }
        IsCloseConfirmVisible = true;
    }

    /// <summary>창 안 키 — 처리했으면 true.</summary>
    public bool HandleKey(Key key, ModifierKeys modifiers)
    {
        if (modifiers == ModifierKeys.Control && key == Key.Z) { Undo(); return true; }
        if (modifiers == ModifierKeys.Control && key == Key.Y) { Redo(); return true; }
        if (modifiers != ModifierKeys.None) return false;
        switch (key)
        {
            case Key.Escape: Escape(); return true;
            case Key.Enter: if (IsCloseConfirmVisible) Complete(ImageCropOutcome.Cancel); else Close(); return true;
            case Key.Back: RemoveLast(); return true;
            case Key.Delete: RemoveVertex(SelectedVertex); return true;
            default: return false;
        }
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Push(Capture());
        Restore(_undo.Pop());
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Push(Capture());
        Restore(_redo.Pop());
    }

    private (Point[] Points, bool Closed) Capture() => (_points.ToArray(), _isClosed);

    private void Snapshot()
    {
        _undo.Push(Capture());
        _redo.Clear();
    }

    private void Restore((Point[] Points, bool Closed) state)
    {
        _points = state.Points.ToList();
        _isClosed = state.Closed;
        SelectedVertex = -1;
        RaisePolygon();
    }

    private Point ClampToImage(Point p) => new(Math.Clamp(p.X, 0, PixelWidth), Math.Clamp(p.Y, 0, PixelHeight));

    private void RaisePolygon()
    {
        Error = null;
        NotifyOfPropertyChange(nameof(Points));
        NotifyOfPropertyChange(nameof(IsClosed));
        NotifyOfPropertyChange(nameof(PointCount));
        NotifyOfPropertyChange(nameof(PointCountText));
        NotifyOfPropertyChange(nameof(CanUndo));
        NotifyOfPropertyChange(nameof(CanRedo));
        CommandManager.InvalidateRequerySuggested();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region 결과

    /// <summary>「자르기 적용」 — 닫힌 · 겹치지 않는 3점 이상 다각형일 때만.</summary>
    public void Apply()
    {
        var reason = !_isClosed && _points.Count >= 3 ? "다각형을 닫아 주세요 — 첫 점을 누르거나 Enter" : ImageCropMath.Validate(_points);
        if (reason != null) { Error = reason; return; }
        Complete(ImageCropOutcome.Apply);
    }

    private void Complete(ImageCropOutcome outcome)
    {
        IsCloseConfirmVisible = false;
        _completion.TrySetResult(outcome);
    }

    #endregion
}
