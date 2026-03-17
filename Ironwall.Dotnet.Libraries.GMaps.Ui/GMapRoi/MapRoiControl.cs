using Ironwall.Dotnet.Libraries.GMaps.Ui.Args;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Monitoring.Models.Maps;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapRoi;
/****************************************************************************
   Purpose      : 관심지역(ROI) 관리 패널 — Canvas 위 비모달 CustomControl
   Created By   : GHLee
   Created On   : 3/17/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class MapRoiControl : Control
{
    #region Static Constructor
    static MapRoiControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(MapRoiControl),
            new FrameworkPropertyMetadata(typeof(MapRoiControl)));
    }
    #endregion

    #region Dependency Properties

    /// <summary>
    /// 관심지역 목록
    /// </summary>
    public ObservableCollection<IMapRoiModel> RoiItems
    {
        get { return (ObservableCollection<IMapRoiModel>)GetValue(RoiItemsProperty); }
        set { SetValue(RoiItemsProperty, value); }
    }

    public static readonly DependencyProperty RoiItemsProperty =
        DependencyProperty.Register("RoiItems", typeof(ObservableCollection<IMapRoiModel>),
            typeof(MapRoiControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 선택된 관심지역
    /// </summary>
    public IMapRoiModel? SelectedRoi
    {
        get { return (IMapRoiModel?)GetValue(SelectedRoiProperty); }
        set { SetValue(SelectedRoiProperty, value); }
    }

    public static readonly DependencyProperty SelectedRoiProperty =
        DependencyProperty.Register("SelectedRoi", typeof(IMapRoiModel),
            typeof(MapRoiControl),
            new PropertyMetadata(null));

    /// <summary>
    /// 패널 제목
    /// </summary>
    public string PanelTitle
    {
        get { return (string)GetValue(PanelTitleProperty); }
        set { SetValue(PanelTitleProperty, value); }
    }

    public static readonly DependencyProperty PanelTitleProperty =
        DependencyProperty.Register("PanelTitle", typeof(string),
            typeof(MapRoiControl),
            new PropertyMetadata("관심지역"));

    #endregion

    #region Commands

    public ICommand MoveCommand
    {
        get { return (ICommand)GetValue(MoveCommandProperty); }
        set { SetValue(MoveCommandProperty, value); }
    }

    public static readonly DependencyProperty MoveCommandProperty =
        DependencyProperty.Register("MoveCommand", typeof(ICommand),
            typeof(MapRoiControl));

    public ICommand RegisterCommand
    {
        get { return (ICommand)GetValue(RegisterCommandProperty); }
        set { SetValue(RegisterCommandProperty, value); }
    }

    public static readonly DependencyProperty RegisterCommandProperty =
        DependencyProperty.Register("RegisterCommand", typeof(ICommand),
            typeof(MapRoiControl));

    public ICommand DeleteCommand
    {
        get { return (ICommand)GetValue(DeleteCommandProperty); }
        set { SetValue(DeleteCommandProperty, value); }
    }

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register("DeleteCommand", typeof(ICommand),
            typeof(MapRoiControl));

    public ICommand CloseCommand
    {
        get { return (ICommand)GetValue(CloseCommandProperty); }
        set { SetValue(CloseCommandProperty, value); }
    }

    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register("CloseCommand", typeof(ICommand),
            typeof(MapRoiControl));

    #endregion

    #region Events

    public event EventHandler<MapRoiEventArgs>? MoveRequested;
    public event EventHandler? RegisterRequested;
    public event EventHandler<MapRoiEventArgs>? DeleteRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler<MapRoiTitleEditedEventArgs>? TitleEdited;

    #endregion

    #region Constructor

    public MapRoiControl()
    {
        InitializeCommands();
        InitializeDragSupport();
    }

    #endregion

    #region Initialization

    private void InitializeCommands()
    {
        MoveCommand = new RelayCommand(_ => OnMoveRequested(), _ => SelectedRoi != null);
        RegisterCommand = new RelayCommand(_ => OnRegisterRequested());
        DeleteCommand = new RelayCommand(_ => OnDeleteRequested(), _ => SelectedRoi != null);
        CloseCommand = new RelayCommand(_ => OnCloseRequested());
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 닫기 버튼 이벤트 연결
        if (GetTemplateChild("PART_CloseButton") is Button closeButton)
            closeButton.Click += (s, e) => OnCloseRequested();

        // 리스트 더블클릭 인라인 편집
        if (GetTemplateChild("PART_RoiListBox") is ListBox listBox)
            listBox.MouseDoubleClick += OnListBoxMouseDoubleClick;
    }

    #endregion

    #region Inline Title Editing

    private TextBox? _activeEditBox;
    private IMapRoiModel? _editingRoi;

    private void OnListBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox) return;

        // 클릭 위치에서 ListBoxItem 찾기
        var element = e.OriginalSource as DependencyObject;
        while (element != null && element is not ListBoxItem)
            element = VisualTreeHelper.GetParent(element);

        if (element is not ListBoxItem item) return;

        // DataTemplate 내부의 TextBlock/TextBox 찾기
        var titleText = FindChild<TextBlock>(item, "TitleText");
        var titleEdit = FindChild<TextBox>(item, "TitleEdit");

        if (titleText == null || titleEdit == null) return;

        _editingRoi = item.DataContext as IMapRoiModel;
        if (_editingRoi == null) return;

        // TextBlock 숨기고 TextBox 표시
        titleText.Visibility = Visibility.Collapsed;
        titleEdit.Visibility = Visibility.Visible;
        titleEdit.Text = _editingRoi.Title;
        titleEdit.SelectAll();
        titleEdit.Focus();

        _activeEditBox = titleEdit;

        // LostFocus와 Enter 이벤트 등록
        titleEdit.LostFocus += OnTitleEditLostFocus;
        titleEdit.KeyDown += OnTitleEditKeyDown;

        e.Handled = true;
    }

    private void OnTitleEditKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitTitleEdit(sender as TextBox);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelTitleEdit(sender as TextBox);
            e.Handled = true;
        }
    }

    private void OnTitleEditLostFocus(object sender, RoutedEventArgs e)
    {
        CommitTitleEdit(sender as TextBox);
    }

    private void CommitTitleEdit(TextBox? editBox)
    {
        if (editBox == null || _editingRoi == null) return;

        var newTitle = editBox.Text?.Trim();
        if (!string.IsNullOrEmpty(newTitle) && newTitle != _editingRoi.Title)
        {
            _editingRoi.Title = newTitle;

            // TextBlock에 직접 반영 (모델이 INotifyPropertyChanged 미구현이므로)
            var parent = VisualTreeHelper.GetParent(editBox) as Grid;
            if (parent != null)
            {
                var titleText = FindChild<TextBlock>(parent, "TitleText");
                if (titleText != null)
                    titleText.Text = newTitle;
            }

            TitleEdited?.Invoke(this, new MapRoiTitleEditedEventArgs(_editingRoi, newTitle));
        }

        FinishEditing(editBox);
    }

    private void CancelTitleEdit(TextBox? editBox)
    {
        FinishEditing(editBox);
    }

    private void FinishEditing(TextBox? editBox)
    {
        if (editBox == null) return;

        editBox.LostFocus -= OnTitleEditLostFocus;
        editBox.KeyDown -= OnTitleEditKeyDown;

        editBox.Visibility = Visibility.Collapsed;

        // 같은 부모의 TextBlock 복원
        var parent = VisualTreeHelper.GetParent(editBox) as Grid;
        if (parent != null)
        {
            var titleText = FindChild<TextBlock>(parent, "TitleText");
            if (titleText != null)
                titleText.Visibility = Visibility.Visible;
        }

        _activeEditBox = null;
        _editingRoi = null;
    }

    private T? FindChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild && typedChild.Name == childName)
                return typedChild;

            var found = FindChild<T>(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    #endregion

    #region Command Handlers

    private void OnMoveRequested()
    {
        if (SelectedRoi != null)
            MoveRequested?.Invoke(this, new MapRoiEventArgs(SelectedRoi));
    }

    private void OnRegisterRequested()
    {
        RegisterRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeleteRequested()
    {
        if (SelectedRoi != null)
            DeleteRequested?.Invoke(this, new MapRoiEventArgs(SelectedRoi));
    }

    private void OnCloseRequested()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Drag Support

    private void InitializeDragSupport()
    {
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(this);

        // 헤더 영역(상단 45px)만 드래그 가능
        if (position.Y > 45) return;

        var contentPresenter = FindParentOfType<ContentPresenter>(this);
        var canvas = contentPresenter?.Parent as Canvas;

        if (canvas == null) return;

        _isDragging = true;
        _lastMousePosition = e.GetPosition(canvas);
        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var contentPresenter = FindParentOfType<ContentPresenter>(this);
        var canvas = contentPresenter?.Parent as Canvas;

        if (canvas == null) return;

        var currentPosition = e.GetPosition(canvas);
        var deltaX = currentPosition.X - _lastMousePosition.X;
        var deltaY = currentPosition.Y - _lastMousePosition.Y;

        var currentLeft = Canvas.GetLeft(contentPresenter);
        var currentTop = Canvas.GetTop(contentPresenter);

        if (double.IsNaN(currentLeft)) currentLeft = 0;
        if (double.IsNaN(currentTop)) currentTop = 0;

        Canvas.SetLeft(contentPresenter, currentLeft + deltaX);
        Canvas.SetTop(contentPresenter, currentTop + deltaY);

        _lastMousePosition = currentPosition;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;

        _isDragging = false;
        ReleaseMouseCapture();
    }

    private T? FindParentOfType<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject parent = VisualTreeHelper.GetParent(child);

        while (parent != null && !(parent is T))
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        return parent as T;
    }

    #endregion

    #region Center Positioning

    /// <summary>
    /// Canvas 정 가운데에 패널을 배치한다.
    /// Loaded 이후 호출해야 ActualWidth/Height가 유효하다.
    /// </summary>
    public void CenterInCanvas()
    {
        var contentPresenter = FindParentOfType<ContentPresenter>(this);
        var canvas = contentPresenter?.Parent as Canvas;

        if (canvas == null || contentPresenter == null) return;

        var canvasWidth = canvas.ActualWidth;
        var canvasHeight = canvas.ActualHeight;
        var panelWidth = contentPresenter.ActualWidth > 0 ? contentPresenter.ActualWidth : 280;
        var panelHeight = contentPresenter.ActualHeight > 0 ? contentPresenter.ActualHeight : 350;

        Canvas.SetLeft(contentPresenter, (canvasWidth - panelWidth) / 2);
        Canvas.SetTop(contentPresenter, (canvasHeight - panelHeight) / 2);
    }

    #endregion

    #region Fields
    private bool _isDragging;
    private Point _lastMousePosition;
    #endregion
}
