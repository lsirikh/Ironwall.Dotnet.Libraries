using System;

namespace Ironwall.Dotnet.Libraries.Events.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/25/2025 5:09:34 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class EventSetupModel : IEventSetupModel
{
    #region - Ctors -
    public EventSetupModel(IEventSetupModel model)
    {
        if (model is null)
            throw new ArgumentNullException(nameof(model));

        // ---- 단순 값 타입은 얕은 복사로 충분 ----
        IsAutoEventDiscard  = model.IsAutoEventDiscard;
        IsSound            = model.IsSound;
        TimeDurationSound  = model.TimeDurationSound;
        TimeDiscardSec     = model.TimeDiscardSec;
        LengthMaxEventPrev = model.LengthMaxEventPrev;
        LengthMinEventPrev = model.LengthMinEventPrev;
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
    public bool IsAutoEventDiscard { get; set; }
    public bool IsSound { get; set; }
    public int TimeDurationSound { get; set; }
    public int TimeDiscardSec { get; set; }
    public int LengthMaxEventPrev { get; set; }
    public int LengthMinEventPrev { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}