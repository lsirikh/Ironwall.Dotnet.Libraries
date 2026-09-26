using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
/****************************************************************************
   Purpose      : 장비 그룹 등록 본문 — 이 클라이언트의 부대를 싣는다 (서버 8.0 부대 편제)
   Created By   : GHLee
   Created On   : 9/26/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 장비 그룹 <b>등록</b> 요청 본문을 만든다 — 그룹 패널(<c>DeviceGroupPanelViewModel</c>)의 등록이 쓰는 유일한 사슬.
/// </summary>
/// <remarks>
/// <para>서버 8.0 은 그룹도 부대에 속하고, 구성원 장비는 그룹의 부대이거나 그 예하 부대여야 한다. 예전 등록은
/// <c>unit_id</c> 를 싣지 않아 그룹이 늘 기본 부대(<c>unit001</c>)로 갔고, 기본 부대의 예하가 아닌 이 클라이언트 부대의
/// 장비를 끌어 넣으면 서버가 422 로 거부했다(라이브 하네스 dl.2, 2026-09-26 — "그룹의 부대(1) 또는 그 예하 부대의 장비만").</para>
/// <para>장비 등록과 같은 관문(<see cref="UnitScopeGate"/>)을 탄다 — 8.0 미만이면 <c>unit_id</c> 를 싣지 않는다.
/// 수정(PUT)에는 쓰지 않는다: 빼면 서버가 그룹의 현재 부대를 유지한다.</para>
/// </remarks>
public static class DeviceGroupWriteRequest
{
    /// <summary>새 그룹의 등록 본문 — 이름 · 설명 + (8.0 이상이면) 이 클라이언트의 부대.</summary>
    public static async Task<DeviceGroupDto> ForCreateAsync(IDeviceGroupModel model, ILogService? log = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        var dto = model.ToDeviceGroupDto();
        await UnitScopeGate.StampGroupAsync(dto, nameof(ForCreateAsync), log, token).ConfigureAwait(false);
        return dto;
    }
}
