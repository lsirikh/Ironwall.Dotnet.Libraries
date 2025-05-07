using Ironwall.Dotnet.Framework.Models.Events;
using Ironwall.Dotnet.Framework.Enums;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/25/2024 5:43:00 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class ActionResponseMalfunctionModel
        : ActionBaseResponseModel<MalfunctionEventModel>, IActionResponseMalfunctionModel
    {

        public ActionResponseMalfunctionModel()
        {
            
        }

        public ActionResponseMalfunctionModel(bool success, string msg, IMalfunctionEventModel model)
            : base(success, msg, model is MalfunctionEventModel eventModel
               ? eventModel : throw new InvalidCastException($"model은 {typeof(MalfunctionEventModel)} 타입이어야 합니다."))
        {
        }
    }
}