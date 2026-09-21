using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 액션 보드 한 행 — 순수 모델(UI·서버 의존 없음)
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 액션 보드의 한 행. <b>서버 응답의 모든 값을 그대로 들고 있는다</b>.
/// </summary>
/// <remarks>
/// <para>🔴 <b>필드를 줄여서 담지 않는다.</b> 화면에 안 쓰는 값이라도 버리면 PATCH 본문을 만들 때
/// 그 자리가 <c>null</c> 이 되고, RFC 7396 에서 <c>null</c> 은 <b>지우라는 뜻</b>이라 서버 값이 소리 없이 사라진다.
/// 그래서 PATCH 는 <b>받아 온 값에서 다시 채운다</b> — 그 원본이 <see cref="OriginalCamera"/> 계열이다.</para>
/// <para>고아(<see cref="IsOrphan"/>)는 <see cref="State"/> 와 <b>직교</b>다. 서버가 장비 삭제 시
/// CASCADE 가 아니라 SET NULL 을 하므로 "값은 멀쩡한데 장비만 없는" 행이 생긴다.</para>
/// </remarks>
public sealed class MappingBoardRow
{
    private MappingBoardRow(MappingActionKind kind)
    {
        Kind = kind;
    }

    #region - 정체 -
    /// <summary>이 행이 속한 종류 축.</summary>
    public MappingActionKind Kind { get; }

    /// <summary>
    /// 배선 행 PK. <b>드롭으로 새로 들어온 행은 0</b> 이다(서버가 아직 모른다).
    /// </summary>
    public int ConfigId { get; private set; }

    /// <summary>연결된 장비 id. <c>null</c> 이면 고아 행이다.</summary>
    public int? DeviceId { get; set; }

    /// <summary>장비가 끊겼는가 — 저장 차단 사유.</summary>
    public bool IsOrphan => DeviceId is null or 0;

    /// <summary>서버가 아는 행인가(해제·수정의 전제).</summary>
    public bool IsPersisted => ConfigId > 0;

    /// <summary>이 행의 Draft 상태.</summary>
    public MappingDraftState State { get; private set; } = MappingDraftState.Pristine;

    /// <summary>마지막으로 본 서버 수정 시각 — 저장 직전 대조에 쓴다.</summary>
    public string? UpdatedAt { get; private set; }
    #endregion

    #region - 공통 값 -
    /// <summary>행 활성 여부.</summary>
    public bool IsEnable { get; set; } = true;

    /// <summary>서버가 준 우선순위. 화면 순서와 다를 수 있다(서버가 정렬을 보장하지 않는다).</summary>
    public int? Priority { get; set; }
    #endregion

    #region - 카메라 값 -
    /// <summary>타깃 프리셋 id.</summary>
    public int? TargetPresetId { get; set; }

    /// <summary>타깃 프리셋 표시 이름(응답 nested 에서 옮겨 온다 — 다시 조회하지 않는다).</summary>
    public string? TargetPresetName { get; set; }

    /// <summary>타깃 프리셋이 감시금지구역인가.</summary>
    public bool TargetPresetRestricted { get; set; }

    /// <summary>홈 프리셋 id.</summary>
    public int? HomePresetId { get; set; }

    /// <summary>홈 프리셋 표시 이름.</summary>
    public string? HomePresetName { get; set; }

    /// <summary>대기 시간(초).</summary>
    public int DelayTime { get; set; }
    #endregion

    #region - 스피커 값 -
    /// <summary>음원그룹 id.</summary>
    public int? FileGroupId { get; set; }

    /// <summary>음원그룹 표시 이름.</summary>
    public string? FileGroupName { get; set; }

    /// <summary>반복 횟수(최소 1).</summary>
    public int RepeatCount { get; set; } = EventMappingRules.REPEAT_COUNT_MIN;
    #endregion

    #region - 경광등 값 -
    /// <summary>점등 색.</summary>
    public EnumLampColor Color { get; set; } = EnumLampColor.Red;

    /// <summary>부저 지속(초).</summary>
    public int BuzzerTime { get; set; } = EventMappingRules.BUZZER_TIME_DEFAULT;

    /// <summary>부저음.</summary>
    public EnumBuzzerSound BuzzerSound { get; set; } = EnumBuzzerSound.PiPiPi;

    /// <summary>점등 모드.</summary>
    public EnumLightMode LightMode { get; set; } = EnumLightMode.Steady;
    #endregion

    #region - 원본(PATCH 재채움 근거) -
    /// <summary>받아 온 카메라 원본. <c>null</c> 이면 새 행이다.</summary>
    public MappingCameraReadDto? OriginalCamera { get; private set; }

    /// <summary>받아 온 스피커 원본.</summary>
    public MappingSpeakerReadDto? OriginalSpeaker { get; private set; }

    /// <summary>받아 온 경광등 원본.</summary>
    public MappingLampReadDto? OriginalLamp { get; private set; }

    private Snapshot _baseline;
    #endregion

    #region - 만들기 -
    /// <summary>드롭·버튼으로 새로 만든 행(<see cref="MappingDraftState.Added"/>).</summary>
    public static MappingBoardRow NewFor(MappingActionKind kind, int deviceId)
    {
        var row = new MappingBoardRow(kind)
        {
            DeviceId = deviceId,
            State = MappingDraftState.Added,
        };
        row._baseline = Snapshot.Empty;
        return row;
    }

    /// <summary>서버에서 읽어 온 카메라 행.</summary>
    public static MappingBoardRow FromDto(MappingCameraReadDto dto)
    {
        var row = new MappingBoardRow(MappingActionKind.Camera)
        {
            ConfigId = dto.ConfigId,
            DeviceId = dto.Camera?.Id,
            TargetPresetId = dto.TargetPreset?.Id,
            TargetPresetName = dto.TargetPreset?.PresetName,
            TargetPresetRestricted = dto.TargetPreset?.IsRestrictedZone ?? false,
            HomePresetId = dto.HomePreset?.Id,
            HomePresetName = dto.HomePreset?.PresetName,
            DelayTime = dto.DelayTime,
            IsEnable = dto.IsEnable,
            Priority = dto.Priority,
            UpdatedAt = dto.UpdatedAt,
            OriginalCamera = dto,
        };
        row.MarkBaseline();
        return row;
    }

    /// <summary>서버에서 읽어 온 스피커 행.</summary>
    public static MappingBoardRow FromDto(MappingSpeakerReadDto dto)
    {
        var row = new MappingBoardRow(MappingActionKind.Speaker)
        {
            ConfigId = dto.ConfigId,
            DeviceId = dto.Speaker?.Id,
            FileGroupId = dto.FileGroup?.Id,
            FileGroupName = dto.FileGroup?.GroupName,
            RepeatCount = dto.RepeatCount,
            IsEnable = dto.IsEnable,
            Priority = dto.Priority,
            UpdatedAt = dto.UpdatedAt,
            OriginalSpeaker = dto,
        };
        row.MarkBaseline();
        return row;
    }

    /// <summary>서버에서 읽어 온 경광등 행.</summary>
    public static MappingBoardRow FromDto(MappingLampReadDto dto)
    {
        var row = new MappingBoardRow(MappingActionKind.Lamp)
        {
            ConfigId = dto.ConfigId,
            DeviceId = dto.Lamp?.Id,
            Color = dto.Color,
            BuzzerTime = dto.BuzzerTime,
            BuzzerSound = dto.BuzzerSound,
            LightMode = dto.LightMode,
            IsEnable = dto.IsEnable,
            Priority = dto.Priority,
            UpdatedAt = dto.UpdatedAt,
            OriginalLamp = dto,
        };
        row.MarkBaseline();
        return row;
    }
    #endregion

    #region - 상태 전이 -
    /// <summary>지금 값을 "서버와 같다" 로 확정한다. 저장 성공 뒤에 부른다.</summary>
    public void MarkBaseline()
    {
        _baseline = Snapshot.Of(this);
        if (State != MappingDraftState.Added || IsPersisted)
            State = MappingDraftState.Pristine;
    }

    /// <summary>저장으로 서버가 id 를 부여했다 — 이제 이 행은 서버가 아는 행이다.</summary>
    public void Settle(int configId)
    {
        if (configId > 0) ConfigId = configId;
        State = MappingDraftState.Pristine;
        MarkBaseline();
    }

    /// <summary>해제 표시. 새 행이면 애초에 서버에 없으니 호출자가 목록에서 <b>빼야</b> 한다.</summary>
    public void MarkRemoved() => State = MappingDraftState.Removed;

    /// <summary>해제 표시를 되돌린다.</summary>
    public void Restore()
    {
        State = IsPersisted ? MappingDraftState.Pristine : MappingDraftState.Added;
        Refresh();
    }

    /// <summary>
    /// 값이 원본과 달라졌는지 다시 판정한다. <b>원래 값으로 되돌리면 다시 Pristine</b> 이 된다.
    /// </summary>
    public void Refresh()
    {
        if (State is MappingDraftState.Removed or MappingDraftState.Added) return;
        State = Snapshot.Of(this).Equals(_baseline) ? MappingDraftState.Pristine : MappingDraftState.Edited;
    }

    /// <summary>값(순서 제외)이 원본과 다른가.</summary>
    public bool HasValueChange => !Snapshot.Of(this).Equals(_baseline);

    /// <summary>원본이 갖고 있던 우선순위(순서 변경 판정의 기준).</summary>
    public int? BaselinePriority => _baseline.Priority;
    #endregion

    private readonly record struct Snapshot(
        int? DeviceId, bool IsEnable, int? Priority,
        int? TargetPresetId, int? HomePresetId, int DelayTime,
        int? FileGroupId, int RepeatCount,
        EnumLampColor Color, int BuzzerTime, EnumBuzzerSound BuzzerSound, EnumLightMode LightMode)
    {
        public static readonly Snapshot Empty = default;

        public static Snapshot Of(MappingBoardRow r) => new(
            r.DeviceId, r.IsEnable, r.Priority,
            r.TargetPresetId, r.HomePresetId, r.DelayTime,
            r.FileGroupId, r.RepeatCount,
            r.Color, r.BuzzerTime, r.BuzzerSound, r.LightMode);
    }
}
