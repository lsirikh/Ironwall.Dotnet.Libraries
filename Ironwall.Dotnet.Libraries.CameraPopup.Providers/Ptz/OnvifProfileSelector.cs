using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Providers.Ptz;

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

    /// <summary>
    /// 이만큼까지는 늘려 그려도 된다(화면 크기 ÷ 영상 크기). 640×360 서브는 800×450 상자까지 맡는다.
    /// </summary>
    public const double MaxUpscale = 1.25;

    /// <summary>
    /// 그릴 크기에 맞춘 선택(T-09 T7): 상자를 <b>덮는</b>(늘림 ≤ <see cref="MaxUpscale"/>) 프로파일 중 <b>해상도가 가장 낮은 것</b>,
    /// 덮는 것이 없으면 가장 높은 것. 동률은 비오디오 → 원 순서. 작은 타일은 서브, 크게 보기 · 큰 단독 타일은 메인이 된다.
    /// 그릴 크기를 모르거나(0) 해상도 정보가 없으면 <see cref="Select(IReadOnlyList{ProfileInfo}, bool)"/> 규칙 그대로.
    /// </summary>
    public static string? Select(IReadOnlyList<ProfileInfo> profiles, bool preferSub, int targetWidth, int targetHeight)
    {
        if (profiles == null || profiles.Count == 0) return null;
        if (targetWidth <= 0 || targetHeight <= 0) return Select(profiles, preferSub);
        var withRes = profiles.Where(p => p.Width > 0 && p.Height > 0).ToList();
        if (withRes.Count == 0) return Select(profiles, preferSub);

        // 비율을 지켜 상자 안에 그릴 때의 배율(1 보다 크면 늘려 그린다).
        static double Scale(ProfileInfo p, int w, int h) => System.Math.Min(w / (double)p.Width, h / (double)p.Height);
        var covering = withRes.Where(p => Scale(p, targetWidth, targetHeight) <= MaxUpscale).ToList();
        var ordered = covering.Count > 0
            ? covering.OrderBy(p => (long)p.Width * p.Height)
            : withRes.OrderByDescending(p => (long)p.Width * p.Height);
        return ordered.ThenBy(p => p.HasAudio ? 1 : 0).First().Token;
    }

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
