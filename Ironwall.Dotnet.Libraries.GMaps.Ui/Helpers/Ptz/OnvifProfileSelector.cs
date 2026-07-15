using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Ptz;

/****************************************************************************
   Purpose      : ONVIF 재생 프로파일 선택(순수 로직) — CameraPopup_RtspSource_Priority FR-03
   Created By   : Claude Code
   Created On   : 2026-07-15
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// ONVIF 프로파일 목록에서 재생 대상 토큰을 고른다(WPF/ONVIF 타입 무의존 — 단위테스트 소스링크 대상).
/// <para>
/// 규칙(PRD OQ-01 확정 + VER-01 실카메라 실측 보강):
/// preferSub=true → <b>해상도 최소</b> → 동률이면 <b>비오디오 우선</b> → 원 순서 첫 번째.
/// preferSub=false → 해상도 최대 → 동률 비오디오 → 첫 번째.
/// 전 프로파일 해상도 부재 → 관례 폴백(서브=목록 2번째 / 메인=1번째).
/// </para>
/// <remarks>
/// 비오디오 우선 근거: Hub는 <c>:no-audio</c>로 방어하지만 오디오 포함 스트림은 비디오 프리즈
/// 이력(cam66 <c>video1+audio1</c>)의 근원 — 동일 해상도면 굳이 선택하지 않는다.
/// 실측(cam66, 8프로파일): preferSub → 720x480 4개 동률 중 비오디오 첫 번째 = <c>3_PROFILE</c>(video1s).
/// </remarks>
/// </summary>
public static class OnvifProfileSelector
{
    /// <summary>선택 입력 — ONVIF Profile의 필요 필드만 투영(token / VideoEncoder Resolution / AudioEncoder 유무).</summary>
    public readonly record struct ProfileInfo(string Token, int Width, int Height, bool HasAudio);

    /// <summary>재생 대상 프로파일 토큰 선택. 목록이 비면 null.</summary>
    public static string? Select(IReadOnlyList<ProfileInfo> profiles, bool preferSub = true)
    {
        if (profiles == null || profiles.Count == 0) return null;
        if (profiles.Count == 1) return profiles[0].Token;

        var withRes = profiles.Where(p => p.Width > 0 && p.Height > 0).ToList();
        if (withRes.Count == 0)
            return preferSub ? profiles[1].Token : profiles[0].Token;   // 해상도 정보 없음 — 관례(1=메인, 2=서브)

        // OrderBy는 안정 정렬 — 해상도·오디오 동률이면 원 목록 순서 유지.
        var ordered = preferSub
            ? withRes.OrderBy(p => (long)p.Width * p.Height)
            : withRes.OrderByDescending(p => (long)p.Width * p.Height);
        return ordered.ThenBy(p => p.HasAudio ? 1 : 0).First().Token;
    }
}
