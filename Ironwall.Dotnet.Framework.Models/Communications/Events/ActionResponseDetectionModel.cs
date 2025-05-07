using Ironwall.Dotnet.Framework.Models.Events;
using System;

namespace Ironwall.Dotnet.Framework.Models.Communications.Events
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/26/2024 10:30:29 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class ActionResponseDetectionModel : ActionBaseResponseModel<DetectionEventModel>, IActionResponseDetectionModel
    {
        public ActionResponseDetectionModel()
        {

        }

        public ActionResponseDetectionModel(bool success, string msg, IDetectionEventModel model) 
            : base(success, msg, model is DetectionEventModel eventModel
               ? eventModel : throw new InvalidCastException($"model은 {typeof(DetectionEventModel)} 타입이어야 합니다."))
        {
        }
    }
}