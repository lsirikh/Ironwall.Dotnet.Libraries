using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Microsoft.Xaml.Behaviors;
using System;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Behaviors{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/1/2025 11:30:20 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    //public class PropertyPanelEventBehavior : Behavior<GMapPropertyBaseControl>
    //{
    //    protected override void OnAttached()
    //    {
    //        base.OnAttached();

    //        if (AssociatedObject != null)
    //        {
    //            AssociatedObject.CloseRequested += OnCloseRequested;
    //            AssociatedObject.MarkerPropertyChanged += OnMarkerPropertyChanged;
    //        }
    //    }

    //    protected override void OnDetaching()
    //    {
    //        if (AssociatedObject != null)
    //        {
    //            AssociatedObject.CloseRequested -= OnCloseRequested;
    //            AssociatedObject.MarkerPropertyChanged -= OnMarkerPropertyChanged;
    //        }

    //        base.OnDetaching();
    //    }

    //    private async void OnCloseRequested(object? sender, EventArgs e)
    //    {

    //        if (!(sender is GMapPropertyBaseControl propertyControl)) return;

    //        // EventAggregator 사용
    //        var eventAggregator = IoC.Get<IEventAggregator>();
    //        await eventAggregator.PublishOnUIThreadAsync(new PropertyPanelCloseRequestedEvent());
    //    }

    //    private async void OnMarkerPropertyChanged(object? sender, MarkerPropertyChangedEventArgs e)
    //    {

    //        //if(!(sender is GMapPropertyBaseControl propertyControl)) return;
    //        //propertyControl.ClearAllBindings();

    //        // EventAggregator 사용
    //        var eventAggregator = IoC.Get<IEventAggregator>();
    //        await eventAggregator.PublishOnUIThreadAsync(new MarkerPropertyChangedEventArgs
    //        {
    //            Marker = e.Marker,
    //            PropertyName = e.PropertyName,
    //            OldValue = e.OldValue,
    //            NewValue = e.NewValue
    //        });
    //    }
    //}

    public class PropertyPanelEventBehavior : Behavior<ContentPresenter>
    {
        private GMapPropertyBaseControl _currentPropertyPanel;

        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded += OnContentPresenterLoaded;

                // Content 변경 감지
                var descriptor = DependencyPropertyDescriptor.FromProperty(
                    ContentPresenter.ContentProperty,
                    typeof(ContentPresenter));
                descriptor?.AddValueChanged(AssociatedObject, OnContentChanged);
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded -= OnContentPresenterLoaded;

                var descriptor = DependencyPropertyDescriptor.FromProperty(
                    ContentPresenter.ContentProperty,
                    typeof(ContentPresenter));
                descriptor?.RemoveValueChanged(AssociatedObject, OnContentChanged);
            }

            DetachCurrentPropertyPanel();
            base.OnDetaching();
        }

        private void OnContentPresenterLoaded(object sender, RoutedEventArgs e)
        {
            AttachToPropertyPanel();
        }

        private void OnContentChanged(object sender, EventArgs e)
        {
            DetachCurrentPropertyPanel();
            AttachToPropertyPanel();
        }

        private void AttachToPropertyPanel()
        {
            if (AssociatedObject.Content is GMapPropertyBaseControl propertyPanel)
            {
                _currentPropertyPanel = propertyPanel;

                // 이벤트 구독
                _currentPropertyPanel.CloseRequested += OnCloseRequested;
                _currentPropertyPanel.MarkerPropertyChanged += OnMarkerPropertyChanged;

                System.Diagnostics.Debug.WriteLine($"PropertyPanel 이벤트 연결: {_currentPropertyPanel.GetType().Name}");

                // 구체적인 타입별 추가 설정
                if (_currentPropertyPanel is GMapPropertyGeometricControl geometricControl)
                {
                    SetupGeometricSpecificHandling(geometricControl);
                }
                else if (_currentPropertyPanel is GMapPropertyCustomControl customControl)
                {
                    SetupCustomSpecificHandling(customControl);
                }
            }
        }

        private void DetachCurrentPropertyPanel()
        {
            if (_currentPropertyPanel != null)
            {
                _currentPropertyPanel.CloseRequested -= OnCloseRequested;
                _currentPropertyPanel.MarkerPropertyChanged -= OnMarkerPropertyChanged;

                System.Diagnostics.Debug.WriteLine($"PropertyPanel 이벤트 해제: {_currentPropertyPanel.GetType().Name}");
                _currentPropertyPanel = null;
            }
        }

        private void SetupGeometricSpecificHandling(GMapPropertyGeometricControl geometricControl)
        {
            // Geometric 전용 처리 (예: 슬라이더 바인딩, 특별한 이벤트 등)
            geometricControl.Loaded += (s, e) =>
            {
                // 추가 초기화 작업
                System.Diagnostics.Debug.WriteLine("GeometricControl 로딩 완료");
            };
        }

        private void SetupCustomSpecificHandling(GMapPropertyCustomControl customControl)
        {
            // Custom 전용 처리
            customControl.Loaded += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("CustomControl 로딩 완료");
            };
        }

        private async void OnCloseRequested(object sender, EventArgs e)
        {
            if (sender is GMapPropertyBaseControl propertyControl)
            {
                System.Diagnostics.Debug.WriteLine($"PropertyPanel 닫기 요청: {propertyControl.GetType().Name}");

                // EventAggregator 사용
                var eventAggregator = IoC.Get<IEventAggregator>();
                await eventAggregator.PublishOnUIThreadAsync(new PropertyPanelCloseRequestedEvent());
            }
        }

        private async void OnMarkerPropertyChanged(object sender, MarkerPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"(Behavior)마커 속성 변경: {e.PropertyName} = {e.OldValue} → {e.NewValue}");

            // EventAggregator 사용
            var eventAggregator = IoC.Get<IEventAggregator>();
            await eventAggregator.PublishOnUIThreadAsync(new MarkerPropertyChangedEventArgs
            {
                Marker = e.Marker,
                PropertyName = e.PropertyName,
                OldValue = e.OldValue,
                NewValue = e.NewValue
            });
        }
    }
}