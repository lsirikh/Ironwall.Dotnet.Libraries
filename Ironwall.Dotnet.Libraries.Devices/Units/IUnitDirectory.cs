namespace Ironwall.Dotnet.Libraries.Devices.Units;
/****************************************************************************
   Purpose      : 부대 이름 · 상위 경로 좁은 조회 — 지도(GMaps.Ui)가 심볼의 소속 부대 줄을 그릴 때 쓴다 (FR-46)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 부대 id → "7중대 · 2대대 › 1연대 › 제○○사단" 한 줄. 인터페이스는 지도와 장비 UI 가 <b>둘 다</b> 참조하는 이 프로젝트에 두고,
/// 구현은 <c>Devices.Ui</c>(<c>UnitDirectory</c> — <c>/graph</c> 캐시 + <c>SYNC_UNIT</c> 재조회)에 둔다.
/// </summary>
/// <remarks>
/// <para><see cref="Describe"/> 는 네트워크에 나가지 않는다 — 캐시에 없으면 <c>null</c>(부르는 쪽이 "소속 부대 정보 없음" 또는 줄 숨김을 정한다).</para>
/// <para><see cref="Changed"/> 는 편제를 다시 읽은 뒤 1회 발화한다(여러 알림은 합친다). 발화 스레드는 UI 스레드를 목표로 하지만
/// 받는 쪽은 스레드를 가정하지 않는다.</para>
/// </remarks>
public interface IUnitDirectory
{
    /// <summary>이 서버가 부대 편제를 갖는가(8.0+). 아니면 소속 부대 줄을 숨긴다.</summary>
    bool IsAvailable { get; }

    /// <summary>"이름 · 상위 › 그 상위 › … › 뿌리". 모르는 id · 아직 못 읽음이면 <c>null</c>.</summary>
    string? Describe(int unitId);

    /// <summary>편제를 다시 읽었다(이름 · 경로가 바뀌었을 수 있다).</summary>
    event EventHandler? Changed;
}
