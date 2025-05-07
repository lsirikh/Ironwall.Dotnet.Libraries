using Ironwall.Dotnet.Framework.Models.Devices;
using Newtonsoft.Json;


namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 7/3/2023 2:56:36 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public class MappingTableMapper : BaseModel, IMappingTableMapper
    {

        #region - Ctors -
        public MappingTableMapper()
        {

        }

        public MappingTableMapper(ICameraMappingModel model) : base(model)
        {
            Id = model.Id;
            MappingGroup = model.MappingGroup;
            Sensor = model.Sensor.Id;
            FirstPreset = model?.FirstPreset?.Id;
            SecondPreset = model?.SecondPreset?.Id;
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
        public string MappingGroup { get; set; }
        public int Sensor { get; set; }
        public int? FirstPreset { get; set; }
        public int? SecondPreset { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
