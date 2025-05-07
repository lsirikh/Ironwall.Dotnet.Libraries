namespace Ironwall.Dotnet.Framework.Models.Symbols
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 4/20/2023 1:05:49 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class EntityPropertyModel : PropertyModel, IEntityPropertyModel
    {

        #region - Ctors -
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
        public string NameArea { get; set; }
        public int IdController { get; set; }
        public int IdSensor { get; set; }
        public int TypeDevice { get; set; }
        public string NameDevice { get; set; }
        public int TypeShape { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
