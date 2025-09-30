using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Collections.Generic;
using System.Collections;

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
public class PopupViewer : Control
{
    private const string PART_CloseButton = "PART_CloseButton";
    private const string PART_CameraContainer = "PART_CameraContainer";
    private const string PART_StatusText = "PART_StatusText";

    private Button? _closeButton;
    private ItemsControl? _cameraContainer;
    private TextBlock? _statusText;

    #region Constructor

    static PopupViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(PopupViewer),
            new FrameworkPropertyMetadata(typeof(PopupViewer)));
    }

    public PopupViewer()
    {
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
            new PropertyMetadata(6));

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
            new PropertyMetadata(0));

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
        if (_closeButton != null)
        {
            _closeButton.Click -= OnCloseButtonClick;
        }

        // 템플릿 파트 가져오기
        _closeButton = GetTemplateChild(PART_CloseButton) as Button;
        _cameraContainer = GetTemplateChild(PART_CameraContainer) as ItemsControl;
        _statusText = GetTemplateChild(PART_StatusText) as TextBlock;

        // 새 이벤트 핸들러 등록
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

    #region Private Methods

    private static void OnCameraSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PopupViewer viewer)
        {
            // 카메라 수 업데이트
            viewer.UpdateCameraCount();

            // ItemsControl 업데이트
            if (viewer._cameraContainer != null)
            {
                viewer._cameraContainer.ItemsSource = e.NewValue as IEnumerable;
            }

            // 이벤트 발생
            viewer.RaiseEvent(new RoutedEventArgs(CameraSourceChangedEvent, viewer));
        }
    }

    private void UpdateCameraCount()
    {
        int count = 0;

        if (CameraSource != null)
        {
            foreach (var item in CameraSource)
            {
                count++;
                if (count >= MaxItems)
                    break;
            }
        }

        CurrentCount = count;
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

    /// <summary>
    /// 카메라 소스 새로고침
    /// </summary>
    public void RefreshCameraSource()
    {
        if (_cameraContainer != null)
        {
            _cameraContainer.Items.Refresh();
        }

        UpdateCameraCount();
    }

    #endregion
}

