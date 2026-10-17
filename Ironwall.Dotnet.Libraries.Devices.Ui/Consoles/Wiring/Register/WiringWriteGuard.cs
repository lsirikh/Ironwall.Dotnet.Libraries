using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;

/// <summary>
/// 결선 쓰기의 계약 가드 — <b>6.3 서버에는 한 줄도 보내지 않는다</b>(PRD FR-04).
/// </summary>
/// <remarks>
/// <para>결선은 <c>hardware_spec.spec.wiring</c> 에 실린다. 그런데 <c>HardwareSpecDto.ShouldSerializeSpec()</c> 은
/// <c>UseAxisWrite</c>(7.0+)일 때만 참이고, <c>DeviceApiService.ShapeWrite</c> 가 그 값을
/// <b>살아 있는 계약으로 덮어쓴다</b> — 호출부가 무엇을 켜 두었든 6.3 서버에서는 <c>spec</c> 이 나가지 않는다.
/// 즉 6.3 에서 저장을 허용하면 "성공했다는 응답 + 아무것도 저장되지 않음" 이 된다.
/// 더 나쁜 것은 그 본문이 <b>평면 레거시 모양</b>으로 재조립되어 우리가 채우지 않은 칸이 기본값으로
/// 덮인다는 점이다(조립기 가드와 같은 까닭).</para>
/// <para>그래서 입구를 감추는 것과 별개로, 보내기 <b>전에</b> 여기서 한 번 더 막는다.</para>
/// </remarks>
internal static class WiringWriteGuard
{
    /// <summary>사람에게 보일 한 줄 — 왜 아무 일도 일어나지 않았는지가 문장에 들어 있어야 한다.</summary>
    internal const string LEGACY_CONTRACT_MESSAGE =
        "현재 서버에서는 결선을 저장할 수 없습니다. 저장하지 않았습니다.";

    /// <summary>축 계약(7.0+)이 아니면 <c>true</c>.</summary>
    internal static bool IsBlocked(DeviceQueryPolicy policy) => !policy.IsAxisContract;
}
