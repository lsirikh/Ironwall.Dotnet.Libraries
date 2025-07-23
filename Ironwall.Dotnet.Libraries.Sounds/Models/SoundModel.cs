using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Libraries.Sounds.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/10/2025 11:32:15 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SoundModel : ISoundModel
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? File { get; set; }
    public EnumEventType? Type { get; set; }
    public bool IsPlaying { get; set; }
}