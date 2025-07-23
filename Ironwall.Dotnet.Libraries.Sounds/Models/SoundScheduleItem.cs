using System;

namespace Ironwall.Dotnet.Libraries.Sounds.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/11/2025 9:59:33 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
internal class SoundScheduleItem : ISoundScheduleItem
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
    public ISoundModel SoundModel { get; set; } = null!;
    public DateTime ScheduledTime { get; set; }
    public int Priority { get; set; } // 우선순위 (낮을수록 높은 우선순위)
    public string EventId { get; set; } = string.Empty;
    public CancellationToken ExternalToken { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}