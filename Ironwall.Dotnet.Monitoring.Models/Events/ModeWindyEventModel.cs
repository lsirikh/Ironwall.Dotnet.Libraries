using Ironwall.Dotnet.Libraries.Enums;
using System;

namespace Ironwall.Dotnet.Monitoring.Models.Events;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/25/2025 7:31:08 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ModeWindyEventModel : ExEventModel, IModeWindyEventModel
{
    public ModeWindyEventModel()
    {

    }


    public EnumWindyMode ModeWindy { get; set; }
}