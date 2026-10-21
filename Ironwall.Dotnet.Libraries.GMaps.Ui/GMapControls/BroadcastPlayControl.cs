using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;

public class BroadcastPlayControl : Control
{
    #region Static Constructor
    static BroadcastPlayControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(BroadcastPlayControl),
            new FrameworkPropertyMetadata(typeof(BroadcastPlayControl)));
    }
    #endregion

    #region Dependency Properties

    public int FileGroupId
    {
        get { return (int)GetValue(FileGroupIdProperty); }
        set { SetValue(FileGroupIdProperty, value); }
    }

    public static readonly DependencyProperty FileGroupIdProperty =
        DependencyProperty.Register("FileGroupId", typeof(int),
            typeof(BroadcastPlayControl), new PropertyMetadata(0, (d, e) => ((BroadcastPlayControl)d).SyncSelectionFromId()));

    /// <summary>음량 0~100(0 허용 · 8.0.15 G36). 기본값은 설정 <c>AppSettings.Broadcast.DefaultVolume</c>(만드는 쪽이 넣는다).</summary>
    public int Volume
    {
        get { return (int)GetValue(VolumeProperty); }
        set { SetValue(VolumeProperty, value); }
    }

    public static readonly DependencyProperty VolumeProperty =
        DependencyProperty.Register("Volume", typeof(int),
            typeof(BroadcastPlayControl), new FrameworkPropertyMetadata(100, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                null, (d, v) => Services.BroadcastControlService.ClampVolume(v as int?)));

    /// <summary>음원 그룹 목록(8.0.15 G25 · GET /api/file-groups). 비어 있으면 콤보를 숨기고 번호 입력만 쓴다.</summary>
    public IReadOnlyList<Services.BroadcastFileGroupOption> FileGroups
    {
        get { return (IReadOnlyList<Services.BroadcastFileGroupOption>)GetValue(FileGroupsProperty); }
        private set { SetValue(FileGroupsPropertyKey, value); }
    }

    private static readonly DependencyPropertyKey FileGroupsPropertyKey =
        DependencyProperty.RegisterReadOnly("FileGroups", typeof(IReadOnlyList<Services.BroadcastFileGroupOption>),
            typeof(BroadcastPlayControl), new PropertyMetadata(Array.Empty<Services.BroadcastFileGroupOption>()));
    public static readonly DependencyProperty FileGroupsProperty = FileGroupsPropertyKey.DependencyProperty;

    /// <summary>콤보에서 고른 그룹 — 고르면 <see cref="FileGroupId"/> 가 그 그룹 번호가 된다.</summary>
    public Services.BroadcastFileGroupOption? SelectedFileGroup
    {
        get { return (Services.BroadcastFileGroupOption?)GetValue(SelectedFileGroupProperty); }
        set { SetValue(SelectedFileGroupProperty, value); }
    }

    public static readonly DependencyProperty SelectedFileGroupProperty =
        DependencyProperty.Register("SelectedFileGroup", typeof(Services.BroadcastFileGroupOption),
            typeof(BroadcastPlayControl), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, e) => { if (e.NewValue is Services.BroadcastFileGroupOption o) ((BroadcastPlayControl)d).FileGroupId = o.Id; }));

    /// <summary>콤보를 보이나(목록을 받았고 1개 이상).</summary>
    public bool HasFileGroupList
    {
        get { return (bool)GetValue(HasFileGroupListProperty); }
        private set { SetValue(HasFileGroupListPropertyKey, value); }
    }

    private static readonly DependencyPropertyKey HasFileGroupListPropertyKey =
        DependencyProperty.RegisterReadOnly("HasFileGroupList", typeof(bool), typeof(BroadcastPlayControl), new PropertyMetadata(false));
    public static readonly DependencyProperty HasFileGroupListProperty = HasFileGroupListPropertyKey.DependencyProperty;

    /// <summary>목록 상태 한 줄(불러오는 중 · 볼 수 없음 등). 비어 있으면 숨긴다.</summary>
    public string? FileGroupStatus
    {
        get { return (string?)GetValue(FileGroupStatusProperty); }
        private set { SetValue(FileGroupStatusPropertyKey, value); }
    }

    private static readonly DependencyPropertyKey FileGroupStatusPropertyKey =
        DependencyProperty.RegisterReadOnly("FileGroupStatus", typeof(string), typeof(BroadcastPlayControl), new PropertyMetadata(null));
    public static readonly DependencyProperty FileGroupStatusProperty = FileGroupStatusPropertyKey.DependencyProperty;

    /// <summary>목록을 불러오는 중으로 표시한다.</summary>
    public void ShowFileGroupsLoading()
    {
        FileGroupStatus = Services.BroadcastFileGroupChoice.LoadingText;
    }

    /// <summary>목록 결과를 싣는다 — 목록이 있으면 콤보, 없거나 볼 수 없으면 안내 + 번호 입력(그대로 쓸 수 있다).</summary>
    public void ApplyFileGroups(Services.BroadcastFileGroupChoice choice)
    {
        FileGroups = choice.Options;
        HasFileGroupList = choice.Options.Count > 0;
        FileGroupStatus = choice.Status;
        SyncSelectionFromId();
    }

    // 번호를 직접 넣었을 때 목록에 같은 번호가 있으면 콤보도 그 항목을 가리킨다(없으면 비움).
    private void SyncSelectionFromId()
    {
        var match = FileGroups?.FirstOrDefault(o => o.Id == FileGroupId);
        if (!Equals(SelectedFileGroup, match)) SelectedFileGroup = match;
    }

    public int Repeat
    {
        get { return (int)GetValue(RepeatProperty); }
        set { SetValue(RepeatProperty, value); }
    }

    public static readonly DependencyProperty RepeatProperty =
        DependencyProperty.Register("Repeat", typeof(int),
            typeof(BroadcastPlayControl), new PropertyMetadata(1));

    public int LinkedDeviceId
    {
        get { return (int)GetValue(LinkedDeviceIdProperty); }
        set { SetValue(LinkedDeviceIdProperty, value); }
    }

    public static readonly DependencyProperty LinkedDeviceIdProperty =
        DependencyProperty.Register("LinkedDeviceId", typeof(int),
            typeof(BroadcastPlayControl), new PropertyMetadata(0));

    #endregion

    #region Commands

    public ICommand SendCommand
    {
        get { return (ICommand)GetValue(SendCommandProperty); }
        set { SetValue(SendCommandProperty, value); }
    }

    public static readonly DependencyProperty SendCommandProperty =
        DependencyProperty.Register("SendCommand", typeof(ICommand),
            typeof(BroadcastPlayControl));

    public ICommand CancelCommand
    {
        get { return (ICommand)GetValue(CancelCommandProperty); }
        set { SetValue(CancelCommandProperty, value); }
    }

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register("CancelCommand", typeof(ICommand),
            typeof(BroadcastPlayControl));

    #endregion

    #region Events

    public event EventHandler<BroadcastSendEventArgs>? SendRequested;
    public event EventHandler? CancelRequested;

    #endregion

    #region Constructor

    public BroadcastPlayControl()
    {
        SendCommand = new RelayCommand(_ => OnSendRequested());
        CancelCommand = new RelayCommand(_ => OnCancelRequested());
        InitializeDragSupport();
    }

    #endregion

    #region Event Handlers

    private void OnSendRequested()
    {
        SendRequested?.Invoke(this, new BroadcastSendEventArgs(FileGroupId, Repeat, LinkedDeviceId, Volume));
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

        // 머리(42)가 지도 안에 남도록 자른다(AUX-D7 · MAP-191)
        var (newLeft, newTop) = Helpers.OverlayHeaderClamp.Clamp(left + cur.X - _lastMousePosition.X, top + cur.Y - _lastMousePosition.Y, this, canvas, 42);
        Canvas.SetLeft(cp, newLeft);
        Canvas.SetTop(cp, newTop);
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
        var ph = cp.ActualHeight > 0 ? cp.ActualHeight : 200;
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
