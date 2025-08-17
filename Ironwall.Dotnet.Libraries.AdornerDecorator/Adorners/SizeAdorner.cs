using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.AdornerDecorator.Adorners
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 3/30/2023 4:55:28 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class SizeAdorner : Adorner
    {
        #region - Ctors -
        public SizeAdorner(UIElement adornedElement) : base(adornedElement) 
        {
            this.SnapsToDevicePixels = true;
            this.designerItem = adornedElement as ContentControl;
            this.chrome = new SizeChrome();
            this.visuals = new VisualCollection(this);
            this.visuals.Add(this.chrome);
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        protected override int VisualChildrenCount => base.VisualChildrenCount;

        protected override Visual GetVisualChild(int index)
        {
            return base.GetVisualChild(index);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            this.chrome.Arrange(new Rect(new Point(0.0, 0.0), finalSize));
            return finalSize;
        }
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        #endregion
        #region - Attributes -
        private SizeChrome chrome;
        private VisualCollection visuals;
        private ContentControl designerItem;
        #endregion

    }
}
