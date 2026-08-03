using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
/****************************************************************************
   Purpose      : 이벤트 억제(정비 창) 폼 검증·중복 판정 순수 규칙.
                  서버 명세 v2.0 §5-B(겹친 창)/§5-D(기간 상한) 회피 로직을
                  VM에서 분리해 단위 테스트 가능하게 한다.
   Created By   : GHLee
   Created On   : 2026-08-03
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>억제 창 생성 폼의 순수 검증 규칙(부작용 없음 — 단위 테스트 대상).</summary>
public static class SuppressionRules
{
    /// <summary>기본 창 길이 상한(일). 서버는 상한이 없어 오타로 장기 억제가 생성될 수 있다(§5-D).</summary>
    public const int DefaultMaxWindowDays = 30;

    /// <summary>창 길이가 상한 이내인가. (종료 &gt; 시작 여부는 별도 검사)</summary>
    public static bool IsWindowLengthValid(DateTime start, DateTime end, int maxDays = DefaultMaxWindowDays)
        => (end - start).TotalDays <= maxDays;

    /// <summary>
    /// (§5-B) 선택한 대상이 이미 활성 창에 덮여 있는 건수.
    /// device/group 은 대상 배열 교집합, all 은 활성 all 창 수로 판정한다.
    /// </summary>
    public static int CountOverlappingActive(
        IEnumerable<EventSuppressionScheduleDto>? active,
        string targetType,
        IEnumerable<int>? selectedDeviceIds,
        IEnumerable<int>? selectedGroupIds)
    {
        if (active is null) return 0;
        var list = active as IList<EventSuppressionScheduleDto> ?? active.ToList();
        if (list.Count == 0) return 0;

        switch (targetType)
        {
            case "device":
                var devIds = (selectedDeviceIds ?? Enumerable.Empty<int>()).ToHashSet();
                if (devIds.Count == 0) return 0;
                return list.Count(a => a.TargetType == "device"
                                    && (a.TargetDeviceIds ?? new()).Any(devIds.Contains));
            case "group":
                var grpIds = (selectedGroupIds ?? Enumerable.Empty<int>()).ToHashSet();
                if (grpIds.Count == 0) return 0;
                return list.Count(a => a.TargetType == "group"
                                    && (a.TargetGroupIds ?? new()).Any(grpIds.Contains));
            default:   // all
                return list.Count(a => a.TargetType == "all");
        }
    }
}
