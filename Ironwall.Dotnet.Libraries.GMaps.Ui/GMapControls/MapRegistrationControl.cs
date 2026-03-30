using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

/// <summary>
/// 오버레이 맵 등록/진행 임베디드 패널
/// Phase: Register → Progress → Complete/Error
/// </summary>
public class MapRegistrationControl : Control
{
    #region Static Constructor
    static MapRegistrationControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MapRegistrationControl),
            new FrameworkPropertyMetadata(typeof(MapRegistrationControl)));
    }
    #endregion

    #region Phase

    public RegistrationPhase Phase
    {
        get => (RegistrationPhase)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    public static readonly DependencyProperty PhaseProperty =
        DependencyProperty.Register(nameof(Phase), typeof(RegistrationPhase),
            typeof(MapRegistrationControl), new PropertyMetadata(RegistrationPhase.Register));

    #endregion

    #region Register Phase Properties

    public string LayerName
    {
        get => (string)GetValue(LayerNameProperty);
        set => SetValue(LayerNameProperty, value);
    }

    public static readonly DependencyProperty LayerNameProperty =
        DependencyProperty.Register(nameof(LayerName), typeof(string),
            typeof(MapRegistrationControl), new PropertyMetadata("새 오버레이 맵"));

    public string FileName
    {
        get => (string)GetValue(FileNameProperty);
        set => SetValue(FileNameProperty, value);
    }

    public static readonly DependencyProperty FileNameProperty =
        DependencyProperty.Register(nameof(FileName), typeof(string),
            typeof(MapRegistrationControl), new PropertyMetadata(string.Empty));

    public string FileSize
    {
        get => (string)GetValue(FileSizeProperty);
        set => SetValue(FileSizeProperty, value);
    }

    public static readonly DependencyProperty FileSizeProperty =
        DependencyProperty.Register(nameof(FileSize), typeof(string),
            typeof(MapRegistrationControl), new PropertyMetadata(string.Empty));

    public string ImageResolution
    {
        get => (string)GetValue(ImageResolutionProperty);
        set => SetValue(ImageResolutionProperty, value);
    }

    public static readonly DependencyProperty ImageResolutionProperty =
        DependencyProperty.Register(nameof(ImageResolution), typeof(string),
            typeof(MapRegistrationControl), new PropertyMetadata(string.Empty));

    public int MinZoom
    {
        get => (int)GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public static readonly DependencyProperty MinZoomProperty =
        DependencyProperty.Register(nameof(MinZoom), typeof(int),
            typeof(MapRegistrationControl), new PropertyMetadata(10));

    public int MaxZoom
    {
        get => (int)GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    public static readonly DependencyProperty MaxZoomProperty =
        DependencyProperty.Register(nameof(MaxZoom), typeof(int),
            typeof(MapRegistrationControl), new PropertyMetadata(16));

    #endregion

    #region Progress Phase Properties

    public double ProgressPercentage
    {
        get => (double)GetValue(ProgressPercentageProperty);
        set => SetValue(ProgressPercentageProperty, value);
    }

    public static readonly DependencyProperty ProgressPercentageProperty =
        DependencyProperty.Register(nameof(ProgressPercentage), typeof(double),
            typeof(MapRegistrationControl), new PropertyMetadata(0.0));

    public int CurrentZoom
    {
        get => (int)GetValue(CurrentZoomProperty);
        set => SetValue(CurrentZoomProperty, value);
    }

    public static readonly DependencyProperty CurrentZoomProperty =
        DependencyProperty.Register(nameof(CurrentZoom), typeof(int),
            typeof(MapRegistrationControl), new PropertyMetadata(0));

    public int ProcessedTiles
    {
        get => (int)GetValue(ProcessedTilesProperty);
        set => SetValue(ProcessedTilesProperty, value);
    }

    public static readonly DependencyProperty ProcessedTilesProperty =
        DependencyProperty.Register(nameof(ProcessedTiles), typeof(int),
            typeof(MapRegistrationControl), new PropertyMetadata(0));

    public int TotalTiles
    {
        get => (int)GetValue(TotalTilesProperty);
        set => SetValue(TotalTilesProperty, value);
    }

    public static readonly DependencyProperty TotalTilesProperty =
        DependencyProperty.Register(nameof(TotalTiles), typeof(int),
            typeof(MapRegistrationControl), new PropertyMetadata(0));

    public string ElapsedTime
    {
        get => (string)GetValue(ElapsedTimeProperty);
        set => SetValue(ElapsedTimeProperty, value);
    }

    public static readonly DependencyProperty ElapsedTimeProperty =
        DependencyProperty.Register(nameof(ElapsedTime), typeof(string),
            typeof(MapRegistrationControl), new PropertyMetadata("00:00"));

    #endregion

    #region Error Phase Properties

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public static readonly DependencyProperty ErrorMessageProperty =
        DependencyProperty.Register(nameof(ErrorMessage), typeof(string),
            typeof(MapRegistrationControl), new PropertyMetadata(string.Empty));

    #endregion

    #region Commands

    public ICommand RegisterCommand
    {
        get => (ICommand)GetValue(RegisterCommandProperty);
        set => SetValue(RegisterCommandProperty, value);
    }

    public static readonly DependencyProperty RegisterCommandProperty =
        DependencyProperty.Register(nameof(RegisterCommand), typeof(ICommand),
            typeof(MapRegistrationControl), new PropertyMetadata(null));

    public ICommand CancelCommand
    {
        get => (ICommand)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand),
            typeof(MapRegistrationControl), new PropertyMetadata(null));

    public ICommand CloseCommand
    {
        get => (ICommand)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand),
            typeof(MapRegistrationControl), new PropertyMetadata(null));

    #endregion

    #region Events

    public event EventHandler<MapRegistrationEventArgs>? RegisterRequested;
    public event EventHandler? CancelRequested;
    public event EventHandler? CloseRequested;

    internal void RaiseRegisterRequested()
    {
        RegisterRequested?.Invoke(this, new MapRegistrationEventArgs
        {
            LayerName = LayerName,
            MinZoom = MinZoom,
            MaxZoom = MaxZoom,
        });
    }

    internal void RaiseCancelRequested() => CancelRequested?.Invoke(this, EventArgs.Empty);
    internal void RaiseCloseRequested() => CloseRequested?.Invoke(this, EventArgs.Empty);

    #endregion

    #region Drag Support

    private bool _isDragging;
    private Point _lastMousePosition;

    private void InitializeDragSupport()
    {
        MouseLeftButtonDown += OnDragMouseDown;
        MouseMove += OnDragMouseMove;
        MouseLeftButtonUp += OnDragMouseUp;
    }

    private void OnDragMouseDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(this);
        if (position.Y > 42) return; // 헤더 영역만

        var cp = FindParent<ContentPresenter>(this);
        var canvas = cp?.Parent as Canvas;
        if (canvas == null) return;

        _isDragging = true;
        _lastMousePosition = e.GetPosition(canvas);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnDragMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var cp = FindParent<ContentPresenter>(this);
        var canvas = cp?.Parent as Canvas;
        if (canvas == null) return;

        var cur = e.GetPosition(canvas);
        var left = Canvas.GetLeft(cp);
        var top = Canvas.GetTop(cp);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;

        Canvas.SetLeft(cp, left + cur.X - _lastMousePosition.X);
        Canvas.SetTop(cp, top + cur.Y - _lastMousePosition.Y);
        _lastMousePosition = cur;
    }

    private void OnDragMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
            parent = VisualTreeHelper.GetParent(parent);
        return parent as T;
    }

    #endregion

    #region OnApplyTemplate

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        InitializeDragSupport();

        if (GetTemplateChild("PART_RegisterButton") is Button registerBtn)
            registerBtn.Click += (s, e) => RaiseRegisterRequested();

        if (GetTemplateChild("PART_CancelButton") is Button cancelBtn)
            cancelBtn.Click += (s, e) => RaiseCancelRequested();

        if (GetTemplateChild("PART_CloseButton") is Button closeBtn)
            closeBtn.Click += (s, e) => RaiseCloseRequested();

        if (GetTemplateChild("PART_ProgressCancelButton") is Button progressCancelBtn)
            progressCancelBtn.Click += (s, e) => RaiseCancelRequested();

        if (GetTemplateChild("PART_CompleteCloseButton") is Button completeCloseBtn)
            completeCloseBtn.Click += (s, e) => RaiseCloseRequested();

        if (GetTemplateChild("PART_ErrorCloseButton") is Button errorCloseBtn)
            errorCloseBtn.Click += (s, e) => RaiseCloseRequested();
    }

    #endregion
}
