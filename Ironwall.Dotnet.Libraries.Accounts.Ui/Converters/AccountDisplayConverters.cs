using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Enums;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Converters;

/// <summary>역할(열거형 또는 서버 문자열) → "관리자" · "사용자". 모르면 "알 수 없음".</summary>
public sealed class RoleDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        EnumUserRole role => AccountDisplay.Role(role),
        string raw => AccountDisplay.Role(raw),
        _ => string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>사용 여부 → "사용" · "미사용".</summary>
public sealed class UsedDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is EnumUsedType used ? AccountDisplay.Used(used) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// 서버 코드 → 표시 글. <c>ConverterParameter</c> 로 사전을 고른다:
/// <c>action</c>(감사 동작) · <c>resource</c>(감사 대상) · <c>status</c>(감사 결과) · <c>grant</c>(부여 상태).
/// </summary>
public sealed class ServerCodeDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AccountDisplay.Lookup(Dictionary(parameter as string), value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;

    internal static IReadOnlyDictionary<string, string> Dictionary(string? kind) => kind switch
    {
        "action" => AccountDisplay.AuditActions,
        "resource" => AccountDisplay.AuditResources,
        "status" => AccountDisplay.AuditStatuses,
        "grant" => AccountDisplay.GrantStatuses,
        _ => new Dictionary<string, string>(),
    };
}

/// <summary>사전에 없는 서버 코드면 그 원문(툴팁), 아니면 null(툴팁 없음). 매개는 <see cref="ServerCodeDisplayConverter"/> 와 같다.</summary>
public sealed class UnknownCodeToolTipConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var raw = AccountDisplay.RawIfUnknown(ServerCodeDisplayConverter.Dictionary(parameter as string), value as string);
        return raw is null ? null : $"서버 값: {raw}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>부여 행 → 만료 임박(24시간 안)인가. 판정은 <see cref="AccountDisplay.IsExpiringSoon"/>(시험 가능한 순수 함수).</summary>
public sealed class GrantExpiringSoonConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var soon = value is Ironwall.Dotnet.Libraries.Messages.Dto.Accounts.GrantDto grant
                   && AccountDisplay.IsExpiringSoon(grant.Status, grant.ValidUntil, DateTime.Now);
        return targetType == typeof(System.Windows.Visibility)
            ? (soon ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed)
            : soon;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>서버 시각(ISO 8601) → "yyyy-MM-dd HH:mm:ss"(이 PC 시각). 감사 기록은 초까지 본다.</summary>
public sealed class ServerTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AccountDisplay.Time(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}
