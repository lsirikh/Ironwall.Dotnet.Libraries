using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;

namespace Ironwall.Dotnet.Libraries.AdornerDecorator.Thumbs
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 3/31/2023 12:05:41 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class RotateThumb : Thumb
    {

        #region - Ctors -
        public RotateThumb()
        {
            DragDelta += new DragDeltaEventHandler(this.RotateThumb_DragDelta);
            DragStarted += new DragStartedEventHandler(this.RotateThumb_DragStarted);
        }


        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        private void RotateThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            this.designerItem = DataContext as ContentControl;

            if(this.designerItem != null)
            {
                this.canvas = FindCanvasParent(this.designerItem);

                if(this.canvas != null)
                {
                    centerPoint = this.designerItem.TranslatePoint(
                        new Point(this.designerItem.Width * this.designerItem.RenderTransformOrigin.X,
                                this.designerItem.Height * this.designerItem.RenderTransformOrigin.Y),
                                this.canvas);

                    Point startPoint = Mouse.GetPosition(this.canvas);
                    this.startVector = Point.Subtract(startPoint, this.centerPoint);

                    this.rotateTransform = this.designerItem.RenderTransform as RotateTransform;
                    if(this.rotateTransform == null)
                    {
                        this.designerItem.RenderTransform = new RotateTransform(0);
                        this.initialAngle = 0;
                    }
                    else
                    {
                        this.initialAngle = this.rotateTransform.Angle;
                    }
                }
            }
        }

        public static Canvas FindCanvasParent(DependencyObject item)
        {

            if (item == null) return null;

            DependencyObject parent = VisualTreeHelper.GetParent(item);
            while (parent != null)
            {
                if (parent is Canvas canvas)
                {
                    return canvas;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;

        }

        private void RotateThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if(this.designerItem != null && this.canvas != null)
            {
                Point currentPoint = Mouse.GetPosition(this.canvas);
                Vector deltaVector = Point.Subtract(currentPoint, this.centerPoint);

                double angle = Vector.AngleBetween(this.startVector, deltaVector);

                RotateTransform rotateTransform = this.designerItem.RenderTransform as RotateTransform;
                rotateTransform.Angle = this.initialAngle + Math.Round(angle, 0);
                this.designerItem.InvalidateMeasure();
            }
        }
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        #endregion
        #region - Attributes -
        private double initialAngle;
        private RotateTransform rotateTransform;
        private Vector startVector;
        private Point centerPoint;
        private ContentControl designerItem;
        private Canvas canvas;
        #endregion
    }
}
