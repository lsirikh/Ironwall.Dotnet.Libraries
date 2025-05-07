using Ironwall.Dotnet.Framework.Models.Devices;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 6/28/2023 3:39:28 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public abstract class OptionMapperBase : BaseModel, IOptionMapperBase
    {

        #region - Ctors -
        public OptionMapperBase()
        {

        }

        public OptionMapperBase(IBaseOptionModel model) : base(model)
        {
            ReferenceId = model.ReferenceId;
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
        public int ReferenceId { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
