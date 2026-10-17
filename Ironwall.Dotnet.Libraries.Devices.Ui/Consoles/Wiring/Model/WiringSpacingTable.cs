using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;

/// <summary>
/// 센서 종류별 기본 간격(m) — <b>한 곳</b>(PRD v0.4 §1-C 간격 표). 펜스센서는 현장마다 2~4m(0.5m 단위)로 바꿀 수 있고 기준은 3m.
/// </summary>
/// <remarks>
/// <para>이웃 두 센서 사이 = 두 종류 간격 중 <b>작은 값</b>(FR-17 — 펜스센서가 복합센서 사이를 채운다).</para>
/// <para><b>현장 펜스 간격</b>은 제어기마다 다르다(사용자 2026-09-30). 지금은 보드 상태로만 쥐고 서버에 싣지 않는다 —
/// 저장 위치는 O-10(제어기 설정 / GIS 로컬). 바꾸면 그림만 다시 놓이고 "바뀐 줄"이 아니다.</para>
/// </remarks>
/// <param name="FenceMetres">펜스센서 간격 — <see cref="FENCE_MIN_M"/>~<see cref="FENCE_MAX_M"/>, <see cref="FENCE_STEP_M"/> 단위.</param>
public sealed record WiringSpacingTable(double FenceMetres = WiringSpacingTable.FENCE_DEFAULT_M)
{
    public const double FENCE_DEFAULT_M = 3.0;
    public const double FENCE_MIN_M = 2.0;
    public const double FENCE_MAX_M = 4.0;
    public const double FENCE_STEP_M = 0.5;
    public const double MULTI_M = 20;
    public const double SMART_M = 6;
    public const double UNDERGROUND_M = 25;
    public const double OTHER_M = 6;

    /// <summary>기준 표(펜스 3m).</summary>
    public static WiringSpacingTable Default { get; } = new();

    /// <summary>고를 수 있는 펜스센서 간격 — 2, 2.5, 3, 3.5, 4.</summary>
    public static IReadOnlyList<double> FenceChoices { get; } = new[] { 2.0, 2.5, 3.0, 3.5, 4.0 };

    /// <summary>종류별 간격(m).</summary>
    public double SpacingOf(EnumDeviceType type) => type switch
    {
        EnumDeviceType.Fence => FenceMetres,
        EnumDeviceType.SmartSensor or EnumDeviceType.SmartSensor2 or
        EnumDeviceType.SmartCompound or EnumDeviceType.SmartMultisensor2 => SMART_M,
        EnumDeviceType.Multi => MULTI_M,
        EnumDeviceType.Underground => UNDERGROUND_M,
        _ => OTHER_M,
    };

    /// <summary>이웃 두 센서 사이 간격 = 두 종류 간격 중 작은 값.</summary>
    public double GapBetween(EnumDeviceType left, EnumDeviceType right) => Math.Min(SpacingOf(left), SpacingOf(right));

    /// <summary>펜스센서 간격을 바꾼 새 표 — 2~4m 로 자르고 0.5m 로 맞춘다. NaN · 무한대는 기준값.</summary>
    public WiringSpacingTable WithFence(double metres)
    {
        if (!double.IsFinite(metres)) return this with { FenceMetres = FENCE_DEFAULT_M };
        var snapped = Math.Round(metres / FENCE_STEP_M, MidpointRounding.AwayFromZero) * FENCE_STEP_M;
        return this with { FenceMetres = Math.Clamp(snapped, FENCE_MIN_M, FENCE_MAX_M) };
    }
}
