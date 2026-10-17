using System;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;

/****************************************************************************
   Purpose      : 지도 계기 인디케이터(강풍·탐지장애) 순수 계산 유틸 (GMap_Map_Instruments) —
                  UI 무관 정적 함수만 두어 헤드리스 테스트 대상(NFR-04).
   Created On   : 2026-07-31 · Sensorway Co., Ltd.
 ****************************************************************************/

/// <summary>강풍 배지 표시 형태(FR-04).</summary>
public enum WindyDisplayStyle { IconLabel, IconOnly }

/// <summary>탐지·장애 클러스터 배치(FR-08).</summary>
public enum InstrumentOrientation { Vertical, Horizontal }

public static class InstrumentMath
{
    // ── 강풍 모드 라벨(WindyConst 준거) ──
    /// <summary>WINDY 모드 → 한글 모드명. (보통/약한/강한/태풍 바람)</summary>
    public static string WindyModeName(EnumWindyMode mode) => mode switch
    {
        EnumWindyMode.wind1 => "약한 바람",
        EnumWindyMode.wind2 => "강한 바람",
        EnumWindyMode.wind3 => "태풍 바람",
        _ => "보통 바람",
    };

    /// <summary>WINDY 모드 → 서브텍스트(enum·감도 힌트).</summary>
    public static string WindyModeDesc(EnumWindyMode mode) => mode switch
    {
        EnumWindyMode.wind1 => "wind1 · 감도 완화(소)",
        EnumWindyMode.wind2 => "wind2 · 감도 완화 · 주의",
        EnumWindyMode.wind3 => "wind3 · 최대 완화 · 경보",
        _ => "wind0 · 표준 감도",
    };

    /// <summary>평상(wind0) 여부 — HideOnNormal 판정용.</summary>
    public static bool IsNormalWind(EnumWindyMode mode) => mode == EnumWindyMode.wind0;

    // ── 관대 파싱(appsettings 오타/부재 → 기본값, 부팅 크래시 금지 NFR-04) ──
    public static WindyDisplayStyle ParseWindyStyle(string? value)
        => Enum.TryParse<WindyDisplayStyle>(value, true, out var v) ? v : WindyDisplayStyle.IconLabel;

    public static InstrumentOrientation ParseOrientation(string? value)
        => Enum.TryParse<InstrumentOrientation>(value, true, out var v) ? v : InstrumentOrientation.Vertical;

    // ── 카운트 배지 텍스트(상한 표기) ──
    /// <summary>활성 건수 → 배지 문자열. 음수=0, 100 이상=99+.</summary>
    public static string CountBadgeText(int count)
    {
        if (count <= 0) return "0";
        return count > 99 ? "99+" : count.ToString();
    }

    /// <summary>강조(활성) 여부 — 건수>0.</summary>
    public static bool IsActive(int count) => count > 0;

    // ── 기본 도킹(마진≥16, HUD 회피 — D-11) ──
    public const double DefaultMargin = 16.0;

    /// <summary>강풍 기본 위치 — 좌상단, 좌표 라벨(상단) 아래로 내려 겹침 회피.</summary>
    public static (double X, double Y) DefaultDockWindy(double canvasW, double canvasH, double margin = DefaultMargin)
        => (margin, margin + 40);   // 좌표 라벨 높이(~40) 아래

    /// <summary>탐지·장애 기본 위치 — 좌측, 강풍 아래(스택). 하단 스케일바 회피 위해 상단 기준 배치.</summary>
    public static (double X, double Y) DefaultDockDetFault(double canvasW, double canvasH, double margin = DefaultMargin)
        => (margin, margin + 96);   // 강풍 배지 아래(~96)

    /// <summary>공통 가장자리 클램프 — 마진 안으로 가둠(음수/초과 방지). 컨트롤 W/H·캔버스 W/H.</summary>
    public static (double X, double Y) ClampWithMargin(double x, double y, double w, double h,
        double canvasW, double canvasH, double margin = DefaultMargin)
    {
        double maxX = Math.Max(margin, canvasW - w - margin);
        double maxY = Math.Max(margin, canvasH - h - margin);
        double cx = double.IsNaN(x) ? margin : Math.Min(Math.Max(margin, x), maxX);
        double cy = double.IsNaN(y) ? margin : Math.Min(Math.Max(margin, y), maxY);
        return (cx, cy);
    }
}
