using System;

namespace Ironwall.Dotnet.Libraries.Sounds.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/11/2025 10:04:36 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 큐 상태 정보 클래스
/// </summary>
public class SoundQueueStatus
{
    public int QueueCount { get; set; }
    public int MaxQueueSize { get; set; }
    public bool IsProcessing { get; set; }
    public List<QueueItemInfo> QueueItems { get; set; } = new();
}

/// <summary>
/// 큐 아이템 정보 클래스
/// </summary>
public class QueueItemInfo
{
    public string EventId { get; set; } = string.Empty;
    public string SoundName { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime ScheduledTime { get; set; }
    public string SoundType { get; set; } = string.Empty;
}