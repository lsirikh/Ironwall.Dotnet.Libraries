using System;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 이벤트 발생 시 지도 이동(줌 + 부드러운 팬) 설정 — event-map-flyto PRD §3.
                  AppSettings.MapEventFlyTo 에 저장. 키 부재 시 기본값, 범위 밖 · 소수 값은 Normalize 가 자른다.
   Note         : IGMapSetupModel 에 넣지 않는다 — 호스트 SetupModel 이 그 인터페이스를 구현해 속성 추가가 호스트 빌드를 깬다.
                  저장: MapSettingsHelper.SaveMapEventFlyToAsync, 로드: MapSettingsHelper.LoadMapEventFlyTo(부팅 DI 싱글톤).
   Created On   : 2026-10-13 · Sensorway Co., Ltd.
 ****************************************************************************/
public class MapEventFlyToModel
{
    /// <summary>이동 시간 상한(ms) — 5초 이내 강제.</summary>
    public const int MaxDurationMs = 5000;
    /// <summary>이동 시간 기본(ms).</summary>
    public const int DefaultDurationMs = 1000;
    /// <summary>이벤트 시 최대 줌 기본.</summary>
    public const int DefaultMaxZoom = 19;

    /// <summary>탐지(침입 · 사전 경보) 시 이동.</summary>
    public bool IsDetectionEnabled { get; set; } = true;

    /// <summary>장애 시 이동.</summary>
    public bool IsFaultEnabled { get; set; } = true;

    /// <summary>이벤트 시 최대 줌(정수, 지도 MinZoom~MaxZoom).</summary>
    public int MaxZoom { get; set; } = DefaultMaxZoom;

    /// <summary>이동 시간(ms, 0~5000 정수). 0 = 순간 이동.</summary>
    public int DurationMs { get; set; } = DefaultDurationMs;

    /// <summary>
    /// 이동 시간 정규화 — 입력 · 저장 · 불러오기 공용. 정수 반올림(AwayFromZero) 후 [0, 5000] 으로 자른다.
    /// 999.9999 → 1000, 9999 → 5000, 음수 → 0, NaN/∞ → 기본값.
    /// </summary>
    public static int NormalizeDurationMs(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return DefaultDurationMs;
        return (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, MaxDurationMs);
    }

    /// <summary>최대 줌 정규화 — 정수 반올림 후 [minZoom, maxZoom] 으로 자른다. NaN/∞ → 기본값(을 범위로 자름).</summary>
    public static int NormalizeMaxZoom(double value, int minZoom, int maxZoom)
    {
        var hi = Math.Max(minZoom, maxZoom);
        if (double.IsNaN(value) || double.IsInfinity(value)) value = DefaultMaxZoom;
        return (int)Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), minZoom, hi);
    }

    /// <summary>제자리 정규화 — 불러온 뒤 지도 줌 범위를 알 때 한 번 부른다.</summary>
    public void Normalize(int minZoom, int maxZoom)
    {
        DurationMs = NormalizeDurationMs(DurationMs);
        MaxZoom = NormalizeMaxZoom(MaxZoom, minZoom, maxZoom);
    }

    public override string ToString()
        => $"MapEventFlyTo[det={IsDetectionEnabled}, fault={IsFaultEnabled}, maxZoom={MaxZoom}, ms={DurationMs}]";
}
