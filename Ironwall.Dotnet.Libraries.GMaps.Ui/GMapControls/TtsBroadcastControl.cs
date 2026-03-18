using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

public class TtsBroadcastControl : Control
{
    #region Static Constructor
    static TtsBroadcastControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(TtsBroadcastControl),
            new FrameworkPropertyMetadata(typeof(TtsBroadcastControl)));
    }
    #endregion

    #region Dependency Properties

    public string Message
    {
        get { return (string)GetValue(MessageProperty); }
        set { SetValue(MessageProperty, value); }
    }

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register("Message", typeof(string),
            typeof(TtsBroadcastControl), new PropertyMetadata(string.Empty));

    public int LinkedDeviceId
    {
        get { return (int)GetValue(LinkedDeviceIdProperty); }
        set { SetValue(LinkedDeviceIdProperty, value); }
    }

    public static readonly DependencyProperty LinkedDeviceIdProperty =
        DependencyProperty.Register("LinkedDeviceId", typeof(int),
            typeof(TtsBroadcastControl), new PropertyMetadata(0));

    #endregion

    #region Commands

    public ICommand SendCommand
    {
        get { return (ICommand)GetValue(SendCommandProperty); }
        set { SetValue(SendCommandProperty, value); }
    }

    public static readonly DependencyProperty SendCommandProperty =
        DependencyProperty.Register("SendCommand", typeof(ICommand),
            typeof(TtsBroadcastControl));

    public ICommand CancelCommand
    {
        get { return (ICommand)GetValue(CancelCommandProperty); }
        set { SetValue(CancelCommandProperty, value); }
    }

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register("CancelCommand", typeof(ICommand),
            typeof(TtsBroadcastControl));

    #endregion

    #region Events

    public event EventHandler<TtsSendEventArgs>? SendRequested;
    public event EventHandler? CancelRequested;

    #endregion

    #region Constructor

    public TtsBroadcastControl()
    {
        SendCommand = new RelayCommand(_ => OnSendRequested());
        CancelCommand = new RelayCommand(_ => OnCancelRequested());
        InitializeDragSupport();
    }

    #endregion

    #region Event Handlers

    private void OnSendRequested()
    {
        SendRequested?.Invoke(this, new TtsSendEventArgs(Message, LinkedDeviceId));
    }

    private void OnCancelRequested()
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Template

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_CloseButton") is Button closeButton)
            closeButton.Click += (s, e) => OnCancelRequested();
    }

    #endregion

    #region Drag + Center

    private bool _isDragging;
    private Point _lastMousePosition;

    private void InitializeDragSupport()
    {
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(this);
        if (position.Y > 42) return;

        var cp = FindParent<ContentPresenter>(this);
        var canvas = cp?.Parent as Canvas;
        if (canvas == null) return;

        _isDragging = true;
        _lastMousePosition = e.GetPosition(canvas);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
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

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();
    }

    public void CenterInCanvas()
    {
        var cp = FindParent<ContentPresenter>(this);
        var canvas = cp?.Parent as Canvas;
        if (canvas == null || cp == null) return;

        var pw = cp.ActualWidth > 0 ? cp.ActualWidth : 280;
        var ph = cp.ActualHeight > 0 ? cp.ActualHeight : 160;
        Canvas.SetLeft(cp, (canvas.ActualWidth - pw) / 2);
        Canvas.SetTop(cp, (canvas.ActualHeight - ph) / 2);
    }

    private T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
            parent = VisualTreeHelper.GetParent(parent);
        return parent as T;
    }

    #endregion
}
