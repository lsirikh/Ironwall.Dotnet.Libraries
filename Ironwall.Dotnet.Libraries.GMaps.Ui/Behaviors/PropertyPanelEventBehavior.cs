using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Microsoft.Xaml.Behaviors;
using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Behaviors{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/1/2025 11:30:20 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class PropertyPanelEventBehavior : Behavior<GMapPropertyBaseControl>
    {
        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject != null)
            {
                AssociatedObject.CloseRequested += OnCloseRequested;
                AssociatedObject.MarkerPropertyChanged += OnMarkerPropertyChanged;
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.CloseRequested -= OnCloseRequested;
                AssociatedObject.MarkerPropertyChanged -= OnMarkerPropertyChanged;
            }

            base.OnDetaching();
        }

        private async void OnCloseRequested(object? sender, EventArgs e)
        {

            if (!(sender is GMapPropertyBaseControl propertyControl)) return;

            // EventAggregator 사용
            var eventAggregator = IoC.Get<IEventAggregator>();
            await eventAggregator.PublishOnUIThreadAsync(new PropertyPanelCloseRequestedEvent());
        }

        private async void OnMarkerPropertyChanged(object? sender, MarkerPropertyChangedEventArgs e)
        {
            
            //if(!(sender is GMapPropertyBaseControl propertyControl)) return;
            //propertyControl.ClearAllBindings();

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