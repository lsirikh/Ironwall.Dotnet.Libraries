using System;
using System.Globalization;
using System.Windows.Data;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Converters;

/// <summary>
/// 세션 종료 사유 코드 → 한글 표시(표시 전용). 서버 logout_reason(EnumUserSessionLogoutReason / SESSION_REVOKED reason).
/// 미지 코드는 "알 수 없음", null/빈값은 "".
/// </summary>
public sealed class LogoutReasonConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value as string) switch
        {
            "DUPLICATE" => "중복 로그인 축출",
            "FORCED" or "FORCE_LOGOUT_BULK" => "강제 종료",
            "PASSWORD_CHANGED" => "비밀번호 변경",
            "USER_LOGOUT" or "LOGOUT" => "로그아웃",
            "SELF_LOGOUT" => "본인 로그아웃",
            "EXPIRED" => "세션 만료",
            "REFRESH_ROTATION" => "토큰 갱신",
            null or "" => string.Empty,
            // 모르는 코드는 원문 대신 "알 수 없음" — 원문은 칸의 툴팁이 보인다(운영자 화면에 영문 코드를 찍지 않는다).
            _ => Consoles.AccountDisplay.Unknown,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
