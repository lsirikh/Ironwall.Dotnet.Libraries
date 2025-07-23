using Ironwall.Dotnet.Libraries.Enums;
using System.Globalization;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils;

public class StringLengthValidationRule : ValidationRule
{
    private int _max;

    public int Max
    {
        get { return _max; }
        set { _max = value; }
    }

    private int _min;

    public int Min
    {
        get { return _min; }
        set { _min = value; }
    }



    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {

        string inputData = value as string;

        if (!string.IsNullOrEmpty(inputData))
        {
            if (inputData.Length >= Min && inputData.Length <= Max)
            {
                return ValidationResult.ValidResult;
            }
            else
            {
                switch (cultureInfo.Name)
                {
                    case LanguageConst.ENGLISH:
                        return new ValidationResult(false, $"Please enter at least {Min} characters and no more than {Max} characters.");
                    case LanguageConst.KOREAN:
                        return new ValidationResult(false, $"입력 값은 {Min}글자 이상 {Max}글자 이하로 입력해주세요.");
                    default:
                        return new ValidationResult(false, $"입력 값은 {Min}글자 이상 {Max}글자 이하로 입력해주세요.");
                }
            }
        }
        else
        {
            return ValidationResult.ValidResult;
        }
    }
}
