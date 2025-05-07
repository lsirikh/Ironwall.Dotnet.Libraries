using Ironwall.Dotnet.Framework.Models.Devices;
using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/15/2023 4:43:45 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class DeviceInfoTableMapper : UpdateMapperBase, IDeviceInfoTableMapper
    {

        #region - Ctors -
        public DeviceInfoTableMapper()
        {

        }
        public DeviceInfoTableMapper(IDeviceDetailModel model)
            : base(model)
        {
            Controller = model.Controller;
            Sensor = model.Sensor;
            Camera = model.Camera;
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
        public int Controller { get; private set; }
        public int Sensor { get; private set; }
        public int Camera { get; private set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
