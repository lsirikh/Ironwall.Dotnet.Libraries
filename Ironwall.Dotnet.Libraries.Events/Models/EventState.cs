using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Events.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 8/28/2025 5:10:36 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class EventState
{
    public EnumEventType EventType { get; set; }
    public DateTime OccurredTime { get; set; }
    public bool IsReported { get; set; }
    public DispatcherTimer? AutoReportTimer { get; set; }
}