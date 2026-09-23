using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
/****************************************************************************
   Purpose      : 장비 쓰기 DTO 에 unit_id 를 싣는 단일 관문 (서버 8.0 부대 편제)
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 장비 쓰기 DTO 에 <c>unit_id</c> 를 싣는 <b>유일한 관문</b>.
/// </summary>
/// <remarks>
/// <para><b>왜 정적 헬퍼인가</b> — 7개 패널 + 2개 다이얼로그가 공유하고, 패널 VM 은 컨테이너가 만들지만
/// 다이얼로그 VM 은 코드에서 <c>new</c> 된다. 생성자 주입을 추가하면 두 경로가 갈라지고
/// 기존 호출부(테스트 포함)의 시그니처가 깨진다. 같은 이유로 만들어진 선례가
/// <see cref="DevicePermissionGate"/>(권한) · <c>DeviceQueryPolicy.Resolve()</c>(조회 정책)다 — 그 관용구를 따른다.</para>
///
/// <para><b>계약</b> — 8.0 미만(프로브 미확보 포함)에서는 <c>unit_id</c> 를 <b>확실히 지운다</b>.
/// 6.3·7.0 쓰기 스키마에 없는 키이고 7.0 이후는 <c>additionalProperties:false</c> 라
/// 실리는 순간 422 다. <c>DeviceApiService.ShapeWrite</c> 가 같은 소거를 한 번 더 하지만
/// 방어를 두 겹으로 두는 편이 낫다(서버 쓰기 경로가 하나가 아니다).</para>
///
/// <para><b>수정(PUT/PATCH) 시의 의미 — "보존 우선"</b> — 8.0 이상에서는 <b>DTO 에 이미 실린 값을
/// 그대로 둔다</b>. 장비 모델은 읽기 경로(<see cref="Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel.UnitId"/>)에
/// 서버가 준 소속 부대를 그대로 들고 있고, Model→Dto 변환(<c>DtoToModelHelper.ToXxxDeviceDto</c> ·
/// <c>ComponentApplyService.CopyCommon</c> · <c>WiringApplyService.PatchAsync</c>)이 그 값을 미리
/// <c>dto.UnitId</c> 에 옮겨 싣는다 — 그래서 이 관문에 도달했을 때 <c>dto.UnitId</c> 가 <b>이미 채워져 있으면
/// 그 장비가 원래 속한 부대</b>이고, 우리가 임의로 "이 클라이언트의 부대"로 덮어써서는 안 된다(다른 부대
/// 장비를 편집하면 조용히 재귀속되던 결함 — D-13). <c>dto.UnitId</c> 가 <c>null</c> 일 때만
/// (신규 등록 · 소속을 정말 모르는 장비) 이 클라이언트의 부대를 찍는다.</para>
/// </remarks>
internal static class UnitScopeGate
{
    /// <summary>
    /// 쓰기 직전 DTO 의 <c>unit_id</c> 를 정리한다. 8.0 미만이거나 미등록이면 <b>무조건 지운다</b>.
    /// 8.0 이상에서는 <b>DTO 에 이미 값이 있으면 보존</b>하고, 없을 때만(신규 등록 등) 이 클라이언트의
    /// 부대를 해석해 싣는다 — 해석 실패면 <b>싣지 않고</b> 경고를 남긴다.
    /// </summary>
    /// <param name="dto">장비 쓰기 DTO(<see cref="BaseDeviceDto"/> 파생 전부). 호출부가 원본 소속 부대를
    /// 알고 있으면(재조회·모델 보존값) 이 메서드를 부르기 전에 <c>dto.UnitId</c> 에 미리 실어 둔다.</param>
    /// <param name="caller">진단용 호출부 이름(로그에 그대로 찍힌다).</param>
    /// <param name="log">호출부 로거(선택).</param>
    internal static async Task StampAsync(
        BaseDeviceDto? dto,
        string caller,
        ILogService? log = null,
        CancellationToken token = default)
    {
        if (dto == null) return;

        var scope = Resolve();
        if (scope == null || !scope.IsUnitEra)
        {
            // 8.0 미만·미등록 — 키 자체가 나가지 않게 확실히 지운다(운영 6.3.2 무회귀).
            dto.UnitId = null;
            return;
        }

        if (dto.UnitId != null)
        {
            // 호출부가 이미 원래 소속 부대를 실어 놨다 — 이 클라이언트 부대로 덮어쓰지 않고 보존한다.
            return;
        }

        int? unitId;
        try
        {
            unitId = await scope.ResolveAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            log?.Warning($"[{caller}] 부대 id 해석 중 예외 — unit_id 를 생략합니다"
                       + $"(서버가 기본 부대로 귀속시킵니다): {ex.Message}");
            dto.UnitId = null;
            return;
        }

        dto.UnitId = unitId;

        if (unitId == null)
            WarnOmitted(caller, scope.UnitCode, log);
    }

    /// <summary>
    /// <see cref="IUnitScopeService"/> IoC lazy 해석. 미등록(오프라인·DB 모드·테스트) 시 <c>null</c>.
    /// </summary>
    /// <remarks><see cref="DevicePermissionGate.Resolve"/> 와 동일 관용구.</remarks>
    internal static IUnitScopeService? Resolve()
    {
        try { return IoC.Get<IUnitScopeService>(); }
        catch { return null; }
    }

    /// <summary>
    /// 생략 사실을 남긴다 — 서버는 이 누락을 <b>응답으로 알려주지 않으므로</b> 우리 로그가 유일한 관측 지점이다.
    /// </summary>
    /// <remarks>
    /// 저장 1회에 장비 수십 건이 돌 수 있어 <see cref="OMIT_WARN_INTERVAL_MS"/> 로 묶는다
    /// (완전 억제가 아니라 간격 제한 — 사실이 사라지면 안 된다). 시계는 단조 증가하는
    /// <see cref="Environment.TickCount64"/> 를 쓴다.
    /// </remarks>
    private static void WarnOmitted(string caller, string? unitCode, ILogService? log)
    {
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastOmitWarnTick);
        if (last != 0 && now - last < OMIT_WARN_INTERVAL_MS) return;
        Interlocked.Exchange(ref _lastOmitWarnTick, now);

        log?.Warning($"[{caller}] 부대 id 를 해석하지 못해 unit_id 를 생략했습니다"
                   + $"(부대 코드: {unitCode ?? "(미설정)"}). 서버 8.0 은 생략 시 장비를 "
                   + $"기본 부대로 귀속시키고 응답에는 아무 신호도 남기지 않습니다 — 부대 편제·GroupNats 설정을 확인하십시오.");
    }

    /// <summary>생략 경고 최소 간격(ms).</summary>
    private const long OMIT_WARN_INTERVAL_MS = 60_000;

    private static long _lastOmitWarnTick;
}
