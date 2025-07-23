using System.Globalization;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils;

public sealed class NotEmptyValidationRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        if (value != null)
        {
            var content = value as string;
            if (content.Length <= 1 && content.Length >= 0)
            {
                return new ValidationResult(false, "최소 2글자 이상 입력해주세요.");
            }
            else
            {
                return ValidationResult.ValidResult;
            }
        }
        else
        {
            return ValidationResult.ValidResult;
        }

        /*
         return string.IsNullOrWhiteSpace((value ?? "").ToString())
            ? new ValidationResult(false, "반드시 내용을 입력해주세요.")
            : ValidationResult.ValidResult;
        */
    }
}
