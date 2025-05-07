namespace Ironwall.Dotnet.Framework.Models.Symbols
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 4/20/2023 12:59:36 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class ShapePropertyModel : PropertyModel, IShapePropertyModel
    {

        #region - Ctors -
        public ShapePropertyModel()
        {

        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public double ShapeStrokeThick { get; set; }
        public string ShapeStroke { get; set; } = "#FFFF0000";
        public string ShapeFill { get; set; } = "#00FFFFFF";
        #endregion
        #region - Attributes -
        #endregion
    }
}
