using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.AdornerDecorator.Adorners
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 3/31/2023 12:38:20 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class ResizeRotateAdorner : Adorner
    {

        #region - Ctors -
        public ResizeRotateAdorner(UIElement adornedElement) : base(adornedElement)
        {
            SnapsToDevicePixels = true;
            this.chrome = new ResizeRotateChrome();
            this.chrome.DataContext = adornedElement;
            this.visuals = new VisualCollection(this);
            this.visuals.Add(this.chrome);
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        protected override Size ArrangeOverride(Size finalSize)
        {
            this.chrome.Arrange(new Rect(finalSize));
            return finalSize;
        }

        protected override Visual GetVisualChild(int index)
        {
            return this.visuals[index];
        }

        protected override int VisualChildrenCount => this.visuals.Count;
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
        private VisualCollection visuals;
        private ResizeRotateChrome chrome;
        #endregion

    }
}
