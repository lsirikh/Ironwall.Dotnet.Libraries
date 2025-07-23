using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils;

public class IdValidationRule : ValidationRule
{

    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        if (value != null)
        {

            //최소 알파벳 5자 이상 20자 이하
            if ((value as string).Length >= 5 && (value as string).Length <= 20)
            {

                return new ValidationResult(true, "반드시 내용을 입력해주세요.");
            }
            else
            {
                return new ValidationResult(false, "아이디는 5글자 이상 20글자 이하로 입력해주세요.");
            }
        }
        else
        {
            ///Null인 경우, Validation 결과를 노출하지 않는다.
            ///즉, ValidationResult 는 참
            return ValidationResult.ValidResult;
        }
    }
}
