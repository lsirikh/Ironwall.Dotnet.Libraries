using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils.Behaviors;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 5/15/2025 2:00:36 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class RegexValidationRule : ValidationRule
{
    public string Pattern { get; set; } = ".*";          // 필수
    public bool AllowEmpty { get; set; }

    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        var text = value as string ?? string.Empty;
        if (AllowEmpty && string.IsNullOrEmpty(text)) return ValidationResult.ValidResult;

        return Regex.IsMatch(text, Pattern)
            ? ValidationResult.ValidResult
            : new ValidationResult(false, $"형식 오류 : {Pattern}");
    }
}