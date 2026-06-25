using System;

namespace Ironwall.Dotnet.Libraries.Base.Services;
/****************************************************************************
   Purpose      : IClock 프로덕션 구현 (시스템 시계)
   Created By   : GHLee
   Created On   : 2026-06-25
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 시스템 시계 기반 <see cref="IClock"/> 프로덕션 구현. DI에 싱글톤으로 등록한다.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime Now => DateTime.Now;
    public DateTime UtcNow => DateTime.UtcNow;
}
