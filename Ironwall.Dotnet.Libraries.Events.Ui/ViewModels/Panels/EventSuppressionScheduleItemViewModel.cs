using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
/****************************************************************************
   Purpose      : 억제 스케줄 목록 행 표시 VM — DTO를 화면용 문자열/상태로 투영.
                  대상 요약(장비/그룹 이름), 상태 배지, 시간창 KST 표시, 취소가능 여부.
   Created By   : GHLee
   Created On   : 2026-08-01
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>억제 스케줄 DataGrid 행. 서버 DTO + Device/Group Provider로 표시명 해석.</summary>
public class EventSuppressionScheduleItemViewModel
{
    public EventSuppressionScheduleItemViewModel(
        EventSuppressionScheduleDto dto,
        DeviceProvider? deviceProvider,
        DeviceGroupProvider? groupProvider)
    {
        Id = dto.Id;
        Name = dto.Name;
        Status = dto.Status ?? "pending";
        TargetSummary = BuildTargetSummary(dto, deviceProvider, groupProvider);
        ScopeText = MapScope(dto.EventScope);
        WindowStartText = ToDisplay(dto.WindowStart);
        WindowEndText = ToDisplay(dto.WindowEnd);
        // 취소(soft-cancel) 가능 = 아직 취소 안 됐고 종료되지 않음(예정/진행중).
        IsCancellable = string.IsNullOrEmpty(dto.RevokedAt)
                        && Status is not ("expired" or "cancelled");
    }

    /// <summary>스케줄 DB Id.</summary>
    public int Id { get; }
    /// <summary>작업명/사유.</summary>
    public string Name { get; }
    /// <summary>파생 상태(pending/active/expired/cancelled) — 배지 DataTrigger 키.</summary>
    public string Status { get; }
    /// <summary>상태 한글 표기.</summary>
    public string StatusText => Status switch
    {
        "active" => "진행중",
        "pending" => "예정",
        "expired" => "종료",
        "cancelled" => "취소",
        _ => Status,
    };
    /// <summary>대상 요약(예: "장비 3 · FN-0312 외 2" / "그룹 2 · A·B" / "전체(both)").</summary>
    public string TargetSummary { get; }
    /// <summary>억제 범위 한글 표기.</summary>
    public string ScopeText { get; }
    /// <summary>시작(KST 표시).</summary>
    public string WindowStartText { get; }
    /// <summary>종료(KST 표시).</summary>
    public string WindowEndText { get; }
    /// <summary>취소 버튼 활성 여부.</summary>
    public bool IsCancellable { get; }

    #region - Helpers -
    private static string MapScope(string scope) => scope switch
    {
        "connection" => "연결",
        "detection" => "탐지",
        "malfunction" => "장애",
        "all" => "전체",
        _ => scope,
    };

    private static string ToDisplay(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return "—";
        // 서버 ISO8601(+09:00) → 로컬(KST) 표시. 파싱 실패 시 원문.
        return DateTime.TryParse(iso, out var dt) ? dt.ToString("yyyy-MM-dd HH:mm") : iso!;
    }

    private static string BuildTargetSummary(
        EventSuppressionScheduleDto dto, DeviceProvider? dp, DeviceGroupProvider? gp)
    {
        switch (dto.TargetType)
        {
            case "device":
            {
                var ids = dto.TargetDeviceIds ?? new();
                if (ids.Count == 0) return "장비 0";
                var first = dp?.CollectionEntity.FirstOrDefault(x => x.Id == ids[0])?.DeviceName
                            ?? $"#{ids[0]}";
                return ids.Count > 1 ? $"장비 {ids.Count} · {first} 외 {ids.Count - 1}"
                                     : $"장비 1 · {first}";
            }
            case "group":
            {
                var ids = dto.TargetGroupIds ?? new();
                if (ids.Count == 0) return "그룹 0";
                var names = ids.Select(id => gp?.CollectionEntity.FirstOrDefault(x => x.Id == id)?.Name ?? $"#{id}");
                return $"그룹 {ids.Count} · {string.Join("·", names.Take(2))}{(ids.Count > 2 ? " 외 " + (ids.Count - 2) : string.Empty)}";
            }
            default:
                return $"전체({dto.TargetSide})";
        }
    }
    #endregion
}
