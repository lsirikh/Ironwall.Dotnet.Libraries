using System;
using System.Collections.Generic;
using System.Globalization;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/****************************************************************************
   Purpose      : 묶음 회전 컨트롤러 판의 순수 규칙(WPF/GMap 무의존, 단위 테스트 가능) —
                  각도 · 위경도 입력 검증(범위 · 형식), ±1° · ±0.1° 한 칸, 입력칸 표시 글, 종류별 요약 글, 확정 한 줄 확인 글.
   Note         : 각도 = 도, 시계 방향 +(GroupRotationMath 와 같은 부호), 소수 1자리, −180 ~ 180.
                  위도 한계 = 웹 메르카토르 한계(±85.05112878, 회전이 메르카토르 평면에서 돈다), 경도 ±180.
   Created On   : 2026-10-18 · Sensorway Co., Ltd.
****************************************************************************/
internal static class GroupRotationPanelRules
{
    public const double MinAngle = -180.0;
    public const double MaxAngle = 180.0;
    public const double MaxLatitude = 85.05112878;
    public const double MaxLongitude = 180.0;

    /// <summary>각도 입력 해석 — "15" · "+15.5" · "-0.1" · "15°" 허용, 소수 1자리로 반올림. 실패면 false + 사용자용 오류 글.</summary>
    public static bool TryParseAngle(string? text, out double degrees, out string? error)
    {
        degrees = 0;
        var s = (text ?? "").Trim().TrimEnd('°').Trim();
        if (s.Length == 0) { error = "각도를 넣어 주세요."; return false; }
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || double.IsNaN(v) || double.IsInfinity(v))
        { error = "각도는 숫자로 넣어 주세요(예: 15.5)."; return false; }
        v = Math.Round(v, 1, MidpointRounding.AwayFromZero);
        if (v < MinAngle || v > MaxAngle) { error = "각도는 −180 ~ 180 사이로 넣어 주세요."; return false; }
        degrees = v + 0.0;   // -0 제거
        error = null;
        return true;
    }

    /// <summary>위도 입력 해석(십진 도, ±85.05112878).</summary>
    public static bool TryParseLatitude(string? text, out double value, out string? error)
        => TryParseDegrees(text, MaxLatitude, "위도", out value, out error);

    /// <summary>경도 입력 해석(십진 도, ±180).</summary>
    public static bool TryParseLongitude(string? text, out double value, out string? error)
        => TryParseDegrees(text, MaxLongitude, "경도", out value, out error);

    private static bool TryParseDegrees(string? text, double limit, string name, out double value, out string? error)
    {
        value = 0;
        var s = (text ?? "").Trim().TrimEnd('°').Trim();
        if (s.Length == 0) { error = $"{name}를 넣어 주세요."; return false; }
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || double.IsNaN(v) || double.IsInfinity(v))
        { error = $"{name}는 십진 도 숫자로 넣어 주세요(예: 37.5)."; return false; }
        if (Math.Abs(v) > limit)
        { error = $"{name}는 −{limit.ToString("0.##", CultureInfo.InvariantCulture)} ~ {limit.ToString("0.##", CultureInfo.InvariantCulture)} 사이로 넣어 주세요."; return false; }
        value = v;
        error = null;
        return true;
    }

    /// <summary>±1° · ±0.1° 한 칸 — 지금 각에 더하고 (−180,180] 로 감아 소수 1자리.</summary>
    public static double Step(double current, double step)
        => Math.Round(GroupRotationMath.WrapSigned(Math.Round(current + step, 1, MidpointRounding.AwayFromZero)), 1, MidpointRounding.AwayFromZero) + 0.0;   // 먼저 반올림 — 179.9+0.1 의 부동소수 잔차가 −180 으로 감기지 않게

    /// <summary>각도 입력칸 글 — "15.0" · "-0.5"(부호 + 는 붙이지 않음, 입력과 같은 모양).</summary>
    public static string FormatAngleInput(double degrees)
        => (Math.Round(degrees, 1, MidpointRounding.AwayFromZero) + 0.0).ToString("0.0", CultureInfo.InvariantCulture);

    /// <summary>위경도 입력칸 글 — 지도 좌표 칩의 WGS-84 와 같은 소수 8자리.</summary>
    public static string FormatCoordinateInput(double degrees)
        => degrees.ToString("F8", CultureInfo.InvariantCulture);

    /// <summary>종류별 요약 — "5개 · 마커 3 · 선/구역 1 · 이미지 1"(0 인 종류는 뺌), 잠금으로 빠진 것이 있으면 "(잠금 1개 제외)".</summary>
    public static string KindSummary(int markers, int lines, int shapes, int images, int locked)
    {
        var parts = new List<string> { $"{markers + lines + shapes + images}개" };
        if (markers > 0) parts.Add($"마커 {markers}");
        if (lines > 0) parts.Add($"선/구역 {lines}");
        if (shapes > 0) parts.Add($"도형 {shapes}");
        if (images > 0) parts.Add($"이미지 {images}");
        var s = string.Join(" · ", parts);
        return locked > 0 ? $"{s} (잠금 {locked}개 제외)" : s;
    }

    /// <summary>중심 한 줄 글 — 좌표 표시 설정 순서(WGS-84 → MGRS → UTM)에서 처음 켜진 것, 다 꺼졌으면 WGS-84.</summary>
    public static string CenterText(double lat, double lng, string? mgrs, string? utm, bool showWgs84, bool showMgrs, bool showUtm)
    {
        if (!showWgs84 && showMgrs && !string.IsNullOrWhiteSpace(mgrs)) return mgrs!;
        if (!showWgs84 && !showMgrs && showUtm && !string.IsNullOrWhiteSpace(utm)) return utm!;
        return $"{lat.ToString("F6", CultureInfo.InvariantCulture)}, {lng.ToString("F6", CultureInfo.InvariantCulture)}";
    }

    /// <summary>확정 전 한 줄 확인 — "5개를 (중심)에서 +15.0° 돌립니다", 0° 면 아직 안 돌렸다는 글.</summary>
    public static string ConfirmSentence(int count, string center, double angle)
        => Math.Round(GroupRotationMath.WrapSigned(angle), 1) == 0
            ? $"{count}개 · 중심 ({center}) · 아직 돌리지 않았습니다"
            : $"{count}개를 ({center})에서 {GroupRotationMath.FormatAngle(angle)} 돌립니다";
}
