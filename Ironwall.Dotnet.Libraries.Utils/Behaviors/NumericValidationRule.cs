using System;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/15/2025 2:01:13 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public sealed class NumericValidationRule : RegexValidationRule
{
    public NumericValidationRule() => Pattern = "^[0-9]+$";
}
