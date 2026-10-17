namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services;
/****************************************************************************
   Purpose      : 부대 편제를 캐시하는 것들의 공통 무효화 입구 — 서버 SYNC_UNIT 를 받은 호스트가 부른다
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 부대 편제를 캐시하는 서비스(<see cref="UnitNameDirectory"/> · <see cref="UnitScopeService"/>)가 구현한다.
/// 호스트가 서버 <c>SYNC_UNIT</c>(다른 곳에서 편제가 바뀜)를 받으면 <see cref="Invalidate"/> 를 부른다.
/// </summary>
/// <remarks>
/// <para>기존 인터페이스(<see cref="IUnitScopeService"/>)를 넓히지 않고 따로 둔다 — 멤버를 늘리면 그 인터페이스의
/// 가짜 · 목이 전부 깨진다. 부르는 쪽은 <c>as IUnitTopologyCache</c> 로 찾는다.</para>
/// <para>어느 스레드에서 불러도 된다. 네트워크를 기다리지 않고 곧바로 돌아온다.</para>
/// </remarks>
public interface IUnitTopologyCache
{
    /// <summary>캐시가 낡았다고 표시한다 — 다음 사용(또는 곧 있을 배경 재적재)에서 서버를 다시 읽는다.</summary>
    void Invalidate();
}
