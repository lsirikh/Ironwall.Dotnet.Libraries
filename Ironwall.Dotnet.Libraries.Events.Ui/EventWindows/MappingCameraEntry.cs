namespace Ironwall.Dotnet.Libraries.Events.Ui.EventWindows;

/****************************************************************************
   Purpose      : 이벤트 매핑의 카메라 배선 한 줄(판본 무관 모양) — 탐지 트리거 입력 (PRD camera-popup-modes FR-09 · 13)
   Created By   : Claude (T-06)
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 서버 7.0+ 의 <c>MappingCameraReadDto</c>(프리셋 참조 포함)와 6.3 의 <c>EventMappingCameraDto</c>(프리셋 DB id 만)를
/// 한 모양으로 편 것.
/// </summary>
/// <param name="CameraId">카메라 장비 id.</param>
/// <param name="Priority">작을수록 먼저. null 은 뒤(<c>MappingPriority.Sort</c> 와 같은 규칙).</param>
/// <param name="ConfigId">배선 행 id — 우선순위가 같을 때 순서를 고정하는 열쇠.</param>
/// <param name="DelaySeconds">매핑 <c>delay_time</c> — 프리셋 이동 최소 시간(초, FR-13).</param>
/// <param name="IsEnable">배선 켜짐.</param>
/// <param name="TargetPresetIndex">대상 프리셋 번호(<c>preset_index</c>). 모르면 null(자동 이동 없음).</param>
/// <param name="TargetPresetName">대상 프리셋 이름(<c>preset_name</c>).</param>
/// <param name="HomePresetIndex">복귀 프리셋 번호. null 이면 제공자의 Home.</param>
public sealed record MappingCameraEntry(
    int CameraId,
    int? Priority,
    int ConfigId,
    int DelaySeconds,
    bool IsEnable,
    int? TargetPresetIndex = null,
    string? TargetPresetName = null,
    int? HomePresetIndex = null);
