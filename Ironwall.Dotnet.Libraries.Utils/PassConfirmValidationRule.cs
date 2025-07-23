using Ironwall.Dotnet.Libraries.Enums;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Utils;

public class PassConfirmValidationRule : ValidationRule
{
    private PassConfirmValidationParameters _parameters = new PassConfirmValidationParameters();
    public PassConfirmValidationParameters Parameters
    {
        get { return _parameters; }
        set { _parameters = value; }
    }

    //public PassConfirmValidationParameters Parameters { get; set; }



    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        string password = value as string;

        if (string.IsNullOrEmpty(password))
        {
            return new ValidationResult(true, "");
        }

        if (password.Length < 8)
        {
            string message = GetMessage(cultureInfo, "Please enter a password of at least 8 characters.", "비밀번호를 8자 이상 입력해주세요.");
            return new ValidationResult(false, message);
        }

        if (!password.Equals(Parameters.BasicString))
        {
            string message = GetMessage(cultureInfo, "It is not the same as the entered password.", "입력된 비밀번호와 동일하지 않습니다.");
            return new ValidationResult(false, message);
        }

        return new ValidationResult(true, "");
    }

    private string GetMessage(CultureInfo cultureInfo, string englishMessage, string koreanMessage)
    {
        return cultureInfo.Name switch
        {
            LanguageConst.ENGLISH => englishMessage,
            LanguageConst.KOREAN => koreanMessage,
            _ => englishMessage
        };
    }
}

public class PassConfirmValidationParameters : DependencyObject
{

    // Using a DependencyProperty as the backing store for Criteria.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty BasicStringProperty =
        DependencyProperty.Register("BasicString", typeof(string), typeof(PassConfirmValidationParameters), new PropertyMetadata(default(string)));

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        Debug.WriteLine($"BasicString : {e.NewValue}");
    }

    public string BasicString
    {
        get { return (string)GetValue(BasicStringProperty); }
        set { SetValue(BasicStringProperty, value); }
    }

}
