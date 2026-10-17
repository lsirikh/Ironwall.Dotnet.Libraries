namespace Ironwall.Dotnet.Libraries.Watchdog;
/****************************************************************************
   Purpose      : 시간 변환 헬퍼(UTC → Unix epoch ms).
   Created By   : GHLee
   Created On   : 2026-07-13
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
internal static class TimeUtil
{
    public static long ToUnixMs(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
}
