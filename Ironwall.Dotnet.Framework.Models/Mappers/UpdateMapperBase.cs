using System;

namespace Ironwall.Dotnet.Framework.Models.Mappers
{
    /****************************************************************************
        Purpose      :                                                           
        Created By   : GHLee                                                
        Created On   : 5/15/2023 4:30:29 PM                                                    
        Department   : SW Team                                                   
        Company      : Sensorway Co., Ltd.                                       
        Email        : lsirikh@naver.com                                         
     ****************************************************************************/

    public abstract class UpdateMapperBase : IUpdateMapperBase
    {

        #region - Ctors -
        public UpdateMapperBase()
        {
            UpdateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        protected UpdateMapperBase(IUpdateDetailBaseModel model)
        {
            UpdateTime = model.UpdateTime.ToString("yyyy-MM-dd HH:mm:ss");
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
        public string UpdateTime { get; set; }
        #endregion
        #region - Attributes -
        #endregion
    }
}
