using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Lists;
/****************************************************************************
   Purpose      :
   Created By   : GHLee
   Created On   : 9/19/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>왼쪽 레일 한 칸의 집계 — 배지 두 개(전체 · 장애)가 여기서 나온다.</summary>
/// <param name="Category">레일 버튼이 가리키는 카테고리.</param>
/// <param name="Total">그 카테고리의 전체 장비 수.</param>
/// <param name="Fault">그중 <see cref="EnumDeviceStatus.ERROR"/> 인 장비 수.</param>
public sealed record DeviceRailCount(EnumDeviceCategory Category, int Total, int Fault);

/// <summary>
/// 좌측 레일의 카테고리별 뱃지(전체·장애) 집계 — 순수 함수다. WPF·Provider·서버 호출 없이
/// 이미 메모리에 있는 <see cref="IBaseDeviceModel"/> 목록만 훑는다(device-console-v8 FR-02).
/// </summary>
/// <remarks>
/// 레일 버튼은 장비가 0건이어도 늘 7개가 같은 순서로 떠 있어야 한다(빈 카테고리도 "지금 비었다"는
/// 정보다 — 버튼이 사라지면 그 카테고리가 존재하는지조차 알 수 없다). 그래서 <see cref="Count"/> 는
/// 입력에 전혀 없는 카테고리도 0/0 으로 채워 넣는다.
/// </remarks>
public static class DeviceRailCounter
{
    /// <summary>레일 버튼이 항상 이 순서로 뜬다(목업 <c>TYPES</c> 배열의 group 제외 순서와 동일).</summary>
    public static readonly IReadOnlyList<EnumDeviceCategory> RailOrder = new[]
    {
        EnumDeviceCategory.Controller,
        EnumDeviceCategory.Sensor,
        EnumDeviceCategory.Camera,
        EnumDeviceCategory.Speaker,
        EnumDeviceCategory.Enclosure,
        EnumDeviceCategory.Lamp,
        EnumDeviceCategory.Gate,
    };

    /// <summary>
    /// 카테고리별 전체/장애 집계. 반환은 <see cref="RailOrder"/> 그대로 <b>항상 7개</b> — 장비가 하나도
    /// 없는 카테고리도 <c>Total=0, Fault=0</c> 으로 자리를 지킨다.
    /// </summary>
    /// <remarks>
    /// <see cref="EnumDeviceCategory.None"/>·<see cref="EnumDeviceCategory.Etc"/> 로 판정된 장비(카탈로그가
    /// 아직 못 알아본 값 등)는 레일에 칸이 없으므로 조용히 건너뛴다 — 여기서 던지면 목록 화면 전체가
    /// 죽는다.
    /// </remarks>
    public static IReadOnlyList<DeviceRailCount> Count(IEnumerable<IBaseDeviceModel> devices)
    {
        if (devices == null) throw new ArgumentNullException(nameof(devices));

        var totals = RailOrder.ToDictionary(c => c, _ => 0);
        var faults = RailOrder.ToDictionary(c => c, _ => 0);

        foreach (var device in devices)
        {
            if (device == null) continue;
            var category = CategoryOf(device);
            if (!totals.ContainsKey(category)) continue;   // 레일에 칸이 없는 카테고리(None/Etc) — 집계 대상 아님

            totals[category]++;
            if (IsFault(device.Status)) faults[category]++;
        }

        return RailOrder.Select(c => new DeviceRailCount(c, totals[c], faults[c])).ToArray();
    }

    /// <summary>레일 상단 합계 배지("전체 N · 장애 M") — <see cref="Count"/> 결과를 그대로 더한다.</summary>
    public static (int Total, int Fault) Totals(IEnumerable<DeviceRailCount> counts)
    {
        if (counts == null) throw new ArgumentNullException(nameof(counts));

        int total = 0, fault = 0;
        foreach (var c in counts)
        {
            total += c.Total;
            fault += c.Fault;
        }
        return (total, fault);
    }

    /// <summary>
    /// 장애 판정 — <see cref="EnumDeviceStatus"/> 3값 중 <c>ERROR</c> 하나만 장애다.
    /// <c>DEACTIVATED</c>(중지)는 운용자가 의도적으로 끈 상태라 장애가 아니다.
    /// </summary>
    public static bool IsFault(EnumDeviceStatus status) => status == EnumDeviceStatus.ERROR;

    /// <summary>
    /// 장비 한 대의 카테고리 판정. 판별자(<see cref="IBaseDeviceModel.CategoryDevice"/>)가 이미 채워져
    /// 있으면(v7.0+ 응답 · 경로 매핑을 거친 모든 정상 경로) 그것이 정본이다. 비어 있으면(과거에 만들어
    /// 판별자를 못 채운 인스턴스 등) CLR 타입에서 되짚는 <see cref="DeviceAxesMapper.CategoryOf"/> 를
    /// 그대로 위임한다 — 카테고리 판정 로직을 두 곳에 따로 두면 한쪽만 고치는 사고가 난다.
    /// </summary>
    public static EnumDeviceCategory CategoryOf(IBaseDeviceModel device) => DeviceAxesMapper.CategoryOf(device);
}
