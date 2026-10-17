using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Streaming.Controls;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/30/2025 8:11:46 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 이벤트 영상 팝업 뷰어 CustomControl
/// 순수한 UI 컨테이너 역할만 수행 (비즈니스 로직 없음)
/// </summary>
[TemplatePart(Name = PART_CloseButton, Type = typeof(Button))]
[TemplatePart(Name = PART_CameraContainer, Type = typeof(ItemsControl))]
[TemplatePart(Name = PART_StatusText, Type = typeof(TextBlock))]
public class PopupViewer : Control, IDisposable
{
    private const string PART_CloseButton = "PART_CloseButton";
    private const string PART_CameraContainer = "PART_CameraContainer";
    private const string PART_StatusText = "PART_StatusText";
    private const string PART_HeaderBorder = "PART_HeaderBorder";
    
    // Dispose 메서드는 중복 호출 방지 로직 추가
    private bool _disposed = false;


    private Button? _closeButton;
    private ItemsControl? _cameraContainer;
    private TextBlock? _statusText;
    private Border? _headerBorder;
    
    // CollectionChanged 이벤트 구독 관리용
    private INotifyCollectionChanged? _currentCollection;


    #region Constructor

    static PopupViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(PopupViewer),
            new FrameworkPropertyMetadata(typeof(PopupViewer)));
    }

    public PopupViewer()
    {
        Unloaded += PopupViewer_Unloaded;
    }

    private void PopupViewer_Unloaded(object sender, RoutedEventArgs e)
    {
        // Unloaded될 때 자동으로 Dispose 호출
        Dispose();

        // 이벤트 핸들러 제거
        Unloaded -= PopupViewer_Unloaded;
    }


    #endregion

    #region Dependency Properties

    /// <summary>
    /// 카메라 데이터 소스 (외부에서 바인딩)
    /// </summary>
    public static readonly DependencyProperty CameraSourceProperty =
        DependencyProperty.Register(
            nameof(CameraSource),
            typeof(IEnumerable),
            typeof(PopupViewer),
            new PropertyMetadata(null, OnCameraSourceChanged));

    public IEnumerable CameraSource
    {
        get => (IEnumerable)GetValue(CameraSourceProperty);
        set => SetValue(CameraSourceProperty, value);
    }

    /// <summary>
    /// 팝업 제목
    /// </summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(PopupViewer),
            new PropertyMetadata("이벤트 영상 팝업"));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>
    /// 최대 표시 가능한 카메라 수
    /// </summary>
    public static readonly DependencyProperty MaxItemsProperty =
        DependencyProperty.Register(
            nameof(MaxItems),
            typeof(int),
            typeof(PopupViewer),
            new PropertyMetadata(15));

    public int MaxItems
    {
        get => (int)GetValue(MaxItemsProperty);
        set => SetValue(MaxItemsProperty, value);
    }

    /// <summary>
    /// 상태바 표시 여부
    /// </summary>
    public static readonly DependencyProperty ShowStatusBarProperty =
        DependencyProperty.Register(
            nameof(ShowStatusBar),
            typeof(bool),
            typeof(PopupViewer),
            new PropertyMetadata(true));

    public bool ShowStatusBar
    {
        get => (bool)GetValue(ShowStatusBarProperty);
        set => SetValue(ShowStatusBarProperty, value);
    }


    public static readonly DependencyProperty CurrentCountProperty =
        DependencyProperty.Register(
            "CurrentCount", 
            typeof(int), 
            typeof(PopupViewer), 
            new PropertyMetadata(0, OnCurrentCountChanged));

    
    public int CurrentCount
    {
        get { return (int)GetValue(CurrentCountProperty); }
        set { SetValue(CurrentCountProperty, value); }
    }

    /// <summary>
    /// 빈 상태 메시지
    /// </summary>
    public static readonly DependencyProperty EmptyMessageProperty =
        DependencyProperty.Register(
            nameof(EmptyMessage),
            typeof(string),
            typeof(PopupViewer),
            new PropertyMetadata("대기 중..."));

    public string EmptyMessage
    {
        get => (string)GetValue(EmptyMessageProperty);
        set => SetValue(EmptyMessageProperty, value);
    }

    /// <summary>
    /// 컨트롤 표시 여부 (ImprovedRtspPlayer에 전달)
    /// </summary>
    public static readonly DependencyProperty ShowPlayerControlsProperty =
        DependencyProperty.Register(
            nameof(ShowPlayerControls),
            typeof(bool),
            typeof(PopupViewer),
            new PropertyMetadata(false));

    public bool ShowPlayerControls
    {
        get => (bool)GetValue(ShowPlayerControlsProperty);
        set => SetValue(ShowPlayerControlsProperty, value);
    }

    /// <summary>
    /// 닫기 버튼 표시 여부
    /// </summary>
    public static readonly DependencyProperty ShowCloseButtonProperty =
        DependencyProperty.Register(
            nameof(ShowCloseButton),
            typeof(bool),
            typeof(PopupViewer),
            new PropertyMetadata(true));

    public bool ShowCloseButton
    {
        get => (bool)GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    #endregion

    #region Commands

    /// <summary>
    /// 닫기 명령 (외부에서 바인딩 가능)
    /// </summary>
    public static readonly DependencyProperty CloseCommandProperty =
        DependencyProperty.Register(
            nameof(CloseCommand),
            typeof(ICommand),
            typeof(PopupViewer),
            new PropertyMetadata(null));

    public ICommand CloseCommand
    {
        get => (ICommand)GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    /// <summary>
    /// 닫기 명령 파라미터
    /// </summary>
    public static readonly DependencyProperty CloseCommandParameterProperty =
        DependencyProperty.Register(
            nameof(CloseCommandParameter),
            typeof(object),
            typeof(PopupViewer),
            new PropertyMetadata(null));

    public object CloseCommandParameter
    {
        get => GetValue(CloseCommandParameterProperty);
        set => SetValue(CloseCommandParameterProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// 닫기 요청 이벤트
    /// </summary>
    public static readonly RoutedEvent CloseRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(CloseRequested),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(PopupViewer));

    public event RoutedEventHandler CloseRequested
    {
        add { AddHandler(CloseRequestedEvent, value); }
        remove { RemoveHandler(CloseRequestedEvent, value); }
    }

    /// <summary>
    /// 카메라 소스 변경 이벤트
    /// </summary>
    public static readonly RoutedEvent CameraSourceChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(CameraSourceChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(PopupViewer));

    public event RoutedEventHandler CameraSourceChanged
    {
        add { AddHandler(CameraSourceChangedEvent, value); }
        remove { RemoveHandler(CameraSourceChangedEvent, value); }
    }

    #endregion

    #region Override Methods

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // 이전 이벤트 핸들러 제거
        if (_headerBorder != null)
        {
            _headerBorder.MouseLeftButtonDown -= OnHeaderMouseLeftButtonDown;
        }

        if (_closeButton != null)
        {
            _closeButton.Click -= OnCloseButtonClick;
        }

        // 템플릿 파트 가져오기
        _headerBorder = GetTemplateChild(PART_HeaderBorder) as Border;
        _closeButton = GetTemplateChild(PART_CloseButton) as Button;
        _cameraContainer = GetTemplateChild(PART_CameraContainer) as ItemsControl;
        _statusText = GetTemplateChild(PART_StatusText) as TextBlock;

        // 새 이벤트 핸들러 등록
        if (_headerBorder != null)
        {
            _headerBorder.MouseLeftButtonDown += OnHeaderMouseLeftButtonDown;
            _headerBorder.Cursor = Cursors.SizeAll; // 드래그 가능 커서
        }

        if (_closeButton != null)
        {
            _closeButton.Click += OnCloseButtonClick;

            // CloseCommand가 있으면 바인딩
            if (CloseCommand != null)
            {
                _closeButton.Command = CloseCommand;
                _closeButton.CommandParameter = CloseCommandParameter;
            }
        }

        // ItemsControl에 소스 바인딩
        if (_cameraContainer != null && CameraSource != null)
        {
            _cameraContainer.ItemsSource = CameraSource;
        }
    }

    #endregion
    #region Header Drag Methods

    private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 부모 Window 찾기
        var window = Window.GetWindow(this);
        if (window != null && e.ChangedButton == MouseButton.Left)
        {
            try
            {
                // Close Button 영역 체크 (클릭이 Close Button 위에서 발생했는지)
                if (IsClickOnCloseButton(e))
                {
                    return; // Close Button 위면 드래그하지 않음
                }

                // 창 드래그
                window.DragMove();
            }
            catch
            {
                // DragMove가 실패할 수 있는 경우 처리
            }
        }
    }

    private bool IsClickOnCloseButton(MouseButtonEventArgs e)
    {
        // Close Button이 있고 표시되어 있는지 확인
        if (_closeButton != null && _closeButton.Visibility == Visibility.Visible)
        {
            // 마우스 위치가 Close Button 영역 내인지 확인
            var point = e.GetPosition(_closeButton);
            var rect = new Rect(0, 0, _closeButton.ActualWidth, _closeButton.ActualHeight);
            return rect.Contains(point);
        }
        return false;
    }

    #endregion
    #region Private Methods

    private static void OnCurrentCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PopupViewer viewer)
        {
        }
    }


    private static void OnCameraSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PopupViewer viewer)
        {
            //// 카메라 수 업데이트
            viewer.UpdateCameraCount();

            // 이전 컬렉션의 이벤트 구독 해제
            viewer.UnsubscribeFromCollectionChanged();

            // ItemsControl 업데이트
            if (viewer._cameraContainer != null)
            {
                viewer._cameraContainer.ItemsSource = e.NewValue as IEnumerable;
            }

            // 새 컬렉션의 이벤트 구독
            viewer.SubscribeToCollectionChanged(e.NewValue as IEnumerable);

            // 이벤트 발생
            viewer.RaiseEvent(new RoutedEventArgs(CameraSourceChangedEvent, viewer));

            System.Diagnostics.Debug.WriteLine($"[PopupViewer] CameraSource changed, CurrentCount: {viewer.CurrentCount}");

        }

    }

    /// <summary>
    /// ObservableCollection의 CollectionChanged 이벤트 구독
    /// </summary>
    private void SubscribeToCollectionChanged(IEnumerable? collection)
    {
        if (collection is INotifyCollectionChanged notifyCollection)
        {
            _currentCollection = notifyCollection;
            _currentCollection.CollectionChanged += OnCollectionChanged;

            System.Diagnostics.Debug.WriteLine($"[PopupViewer] Subscribed to CollectionChanged");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[PopupViewer] Collection does not implement INotifyCollectionChanged");
        }
    }

    /// <summary>
    /// CollectionChanged 이벤트 구독 해제
    /// </summary>
    private void UnsubscribeFromCollectionChanged()
    {
        if (_currentCollection != null)
        {
            _currentCollection.CollectionChanged -= OnCollectionChanged;
            _currentCollection = null;

            System.Diagnostics.Debug.WriteLine($"[PopupViewer] Unsubscribed from CollectionChanged");
        }
    }

    /// <summary>
    /// 컬렉션 변경 이벤트 핸들러 (추가/삭제 감지)
    /// </summary>
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[PopupViewer] CollectionChanged: Action={e.Action}, CurrentCount={CurrentCount}");

        // CurrentCount 업데이트 추가
        UpdateCameraCount();

        // 변경 이벤트 발생 (필요시)
        RaiseEvent(new RoutedEventArgs(CameraSourceChangedEvent, this));
    }


    /// <summary>
    /// Row 구조를 고려한 전체 카메라 수 계산
    /// </summary>
    private void UpdateCameraCount()
    {
        int count = 0;

        //CameraSource
        if (CameraSource != null)
        {
            // IEnumerable을 순회하여 카운트
            foreach (var item in CameraSource)
            {
                // Row 구조인 경우 내부 카메라 수 계산
                var itemType = item?.GetType();
                if (itemType != null)
                {
                    var camerasProperty = itemType.GetProperty("Cameras");
                    if (camerasProperty != null)
                    {
                        var cameras = camerasProperty.GetValue(item) as IEnumerable;
                        if (cameras != null)
                        {
                            foreach (var camera in cameras)
                            {
                                count++;
                                if (count >= MaxItems) break;
                            }
                        }
                    }
                    else
                    {
                        // Row 구조가 아닌 경우 단순 카운트
                        count++;
                    }
                }

                if (count >= MaxItems) break;
            }
        }
        CurrentCount = count;
        System.Diagnostics.Debug.WriteLine($"[PopupViewer] CurrentCount updated to: {count}");
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        // CloseCommand 실행 (있는 경우)
        if (CloseCommand?.CanExecute(CloseCommandParameter) == true)
        {
            CloseCommand.Execute(CloseCommandParameter);
        }

        // CloseRequested 이벤트 발생
        RaiseEvent(new RoutedEventArgs(CloseRequestedEvent, this));
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// 프로그래밍 방식으로 닫기 요청
    /// </summary>
    public void RequestClose()
    {
        OnCloseButtonClick(this, new RoutedEventArgs());
    }

  
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // CollectionChanged 이벤트 구독 해제
        UnsubscribeFromCollectionChanged();

        // 이벤트 핸들러 정리
        if (_closeButton != null)
        {
            _closeButton.Click -= OnCloseButtonClick;
        }

        if (_headerBorder != null)
        {
            _headerBorder.MouseLeftButtonDown -= OnHeaderMouseLeftButtonDown;
        }

        System.Diagnostics.Debug.WriteLine($"[PopupViewer] Disposed");
    }

    #endregion
}

