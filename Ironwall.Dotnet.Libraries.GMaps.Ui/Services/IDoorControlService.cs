using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : 통문·함체 문 개폐 명령 NATS 발행 서비스 계약.
   Created By   : GHLee
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 문 개폐 <b>명령</b>을 브로커로 직접 발행한다(클라 → 구동 담당 매니저 NATS 직행).
/// <para>브로커 연동설계 v1.6 §7: 서버 REST <c>POST …/{id}/control</c> 은 제거됐고
/// <c>gop_command</c> 채널도 폐지돼, 명령은 서버를 거치지 않는다.</para>
/// <para><b>상태는 이 호출로 바뀌지 않는다</b> — 매니저의 <c>component-status</c> 보고가
/// <c>OPERATION_EVENT</c> 로 돌아올 때만 전이한다.</para>
/// </summary>
public interface IDoorControlService
{
    /// <summary>
    /// 문 개폐 명령 발행. 발행 성공(브로커 수락)만 true — 실제 구동 성공 여부가 아니다.
    /// </summary>
    /// <param name="deviceId">대상 장비 ID(통문 또는 함체).</param>
    /// <param name="deviceType"><see cref="EnumDeviceType.Gate"/> / <see cref="EnumDeviceType.Enclosure"/> 만 지원.</param>
    /// <param name="command">"OPEN" / "CLOSE".</param>
    /// <param name="deviceDescription">사람이 읽는 식별 흔적(없으면 카테고리만으로 생성).</param>
    /// <param name="requestedBy">명령을 낸 운영자 계정(선택).</param>
    Task<bool> PublishDoorCommandAsync(
        int deviceId,
        EnumDeviceType deviceType,
        string command,
        string? deviceDescription = null,
        string? requestedBy = null,
        CancellationToken token = default);
}
