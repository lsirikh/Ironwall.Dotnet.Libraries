using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Ironwall.Dotnet.Libraries.AdornerDecorator.Adorners;

namespace Ironwall.Dotnet.Libraries.AdornerDecorator.Thumbs
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 3/31/2023 11:12:42 AM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class DesignerItemDecorator : Control
    {

        #region - Ctors -
        public DesignerItemDecorator()
        {
            Unloaded += DesignerItemDecorator_Unloaded;
        }

        private void DesignerItemDecorator_Unloaded(object sender, RoutedEventArgs e)
        {
            if(this.adorner != null)
            {
                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this);
                if(adornerLayer != null)
                {
                    adornerLayer.Remove(this.adorner);
                }

                this.adorner = null;
            }
        }
        private void ShowAdorner()
        {
            if(this.adorner == null)
            {
                AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(this);

                if(adornerLayer != null)
                {
                    ContentControl designerItem = this.DataContext as ContentControl;
                    Canvas canvas = VisualTreeHelper.GetParent(designerItem) as Canvas;
                    this.adorner = new ResizeRotateAdorner(designerItem);
                    adornerLayer.Add(this.adorner);

                    if (this.ShowDecorator)
                    {
                        this.adorner.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        this.adorner.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else
            {
                this.adorner.Visibility = Visibility.Visible;
            }
        }

        private void HideAdorner()
        {
            if(this.adorner != null)
            {
                this.adorner.Visibility = Visibility.Hidden;
            }
        }

        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        private static void ShowDecoratorProperty_Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            DesignerItemDecorator decorator = d as DesignerItemDecorator;
            bool showDecorator = (bool)e.NewValue;

            if (showDecorator)
            {
                decorator.ShowAdorner();
            }
            else
            {
                decorator.HideAdorner();
            }
        }

        
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -


        public bool ShowDecorator
        {
            get { return (bool)GetValue(ShowDecoratorProperty); }
            set { SetValue(ShowDecoratorProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ShowDecorator.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ShowDecoratorProperty =
            DependencyProperty.Register("ShowDecorator", typeof(bool), typeof(DesignerItemDecorator), new FrameworkPropertyMetadata(false, new PropertyChangedCallback(ShowDecoratorProperty_Changed)));

        


        #endregion
        #region - Attributes -
        private Adorner adorner;
        #endregion
    }
}
