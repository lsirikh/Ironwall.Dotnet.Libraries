namespace Ironwall.Dotnet.Libraries.Messages.Helpers;

/****************************************************************************
   Purpose      : 한국 표준시(KST, UTC+09:00) ISO 8601 Helper
   Created By   : GHLee
   Created On   : 2025-11-28
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 모든 시간 관련 문자열 생성 및 변환을 담당합니다.
                  - GetKoreaTimeIso8601(): 현재 KST ISO 8601 문자열
                  - ToKoreaTimeIso8601(): UTC → KST 변환
                  - ParseToKoreaTime(): ISO 문자열 → KST DateTime
                  - ToKoreaTimeDisplayString(): UI 표시용 문자열
                  - ToUtcIso8601(): KST → UTC 변환
****************************************************************************/

/// <summary>
/// 한국 표준시(KST, UTC+09:00) ISO 8601 Helper
/// <para>모든 시간 관련 문자열 생성 및 변환을 담당합니다.</para>
/// </summary>
public static class KoreaTimeHelper
{
    #region - Constants -
    /// <summary>
    /// 한국 시간 UTC 오프셋 (+09:00)
    /// </summary>
    public static readonly TimeSpan KoreaUtcOffset = TimeSpan.FromHours(9);

    /// <summary>
    /// ISO 8601 with offset 형식
    /// </summary>
    public const string Iso8601WithOffsetFormat = "yyyy-MM-ddTHH:mm:ss.fffzzz";

    /// <summary>
    /// ISO 8601 표준 형식 (UTC)
    /// </summary>
    public const string Iso8601UtcFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
    #endregion

    #region - Public Methods -
    /// <summary>
    /// 현재 한국 시간을 ISO 8601 형식 문자열로 반환
    /// </summary>
    /// <returns>"2025-11-28T18:30:00.000+09:00"</returns>
    public static string GetKoreaTimeIso8601()
    {
        var koreaTime = GetKoreaDateTimeOffset();
        return koreaTime.ToString(Iso8601WithOffsetFormat);
    }

    /// <summary>
    /// 현재 한국 시간을 DateTimeOffset으로 반환
    /// </summary>
    public static DateTimeOffset GetKoreaDateTimeOffset()
    {
        return DateTimeOffset.UtcNow.ToOffset(KoreaUtcOffset);
    }

    /// <summary>
    /// 현재 한국 시간을 DateTime으로 반환
    /// </summary>
    public static DateTime GetKoreaDateTime()
    {
        return DateTimeOffset.UtcNow.ToOffset(KoreaUtcOffset).DateTime;
    }

    /// <summary>
    /// UTC DateTime을 한국 시간 ISO 8601 문자열로 변환
    /// </summary>
    public static string ToKoreaTimeIso8601(DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        var utcOffset = new DateTimeOffset(utcDateTime, TimeSpan.Zero);
        var koreaTime = utcOffset.ToOffset(KoreaUtcOffset);
        return koreaTime.ToString(Iso8601WithOffsetFormat);
    }

    /// <summary>
    /// ISO 8601 문자열을 한국 시간 DateTime으로 파싱
    /// </summary>
    public static DateTime ParseToKoreaTime(string iso8601String)
    {
        var parsed = DateTimeOffset.Parse(iso8601String);
        return parsed.ToOffset(KoreaUtcOffset).DateTime;
    }

    /// <summary>
    /// ISO 8601 문자열을 한국 시간 표시용 문자열로 변환
    /// </summary>
    /// <param name="iso8601String">ISO 8601 문자열</param>
    /// <param name="format">출력 형식 (기본: "yyyy-MM-dd HH:mm:ss")</param>
    public static string ToKoreaTimeDisplayString(string? iso8601String, string format = "yyyy-MM-dd HH:mm:ss")
    {
        if (string.IsNullOrEmpty(iso8601String))
            return string.Empty;

        var koreaTime = ParseToKoreaTime(iso8601String);
        return koreaTime.ToString(format);
    }

    /// <summary>
    /// 한국 시간 ISO 8601을 UTC ISO 8601로 변환
    /// </summary>
    public static string ToUtcIso8601(string koreaTimeIso8601)
    {
        var parsed = DateTimeOffset.Parse(koreaTimeIso8601);
        return parsed.UtcDateTime.ToString(Iso8601UtcFormat);
    }
    #endregion
}
