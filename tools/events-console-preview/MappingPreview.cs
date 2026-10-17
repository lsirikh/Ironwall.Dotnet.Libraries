using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EventsConsolePreview;

/// <summary>
/// 이벤트 맵핑 워크벤치 미리보기 — <b>진짜 뷰 + 진짜 뷰모델</b>을 가짜 게이트웨이 위에 띄운다.
/// </summary>
/// <remarks>서버도 호스트 앱도 없다. 여기서 만드는 상태는 전부 메모리다.</remarks>
internal static class MappingPreview
{
    /// <summary>미리보기용 뷰모델 한 벌.</summary>
    public static MappingWorkbenchViewModel Build(bool readOnly = false)
        => new(new PreviewMappingGateway(), new PreviewDeviceSource(), readOnly ? PreviewPermissions.ReadOnly : null);
}

/// <summary>가짜 장비 캐시.</summary>
internal sealed class PreviewDeviceSource : IMappingDeviceSource
{
    private static readonly string[] _cameraNames =
    {
        "정문 PTZ", "후문 고정", "측면 스피드돔", "초소 1 PTZ", "초소 2 고정",
        "동측 회랑 카메라", "서측 회랑 카메라", "본부 진입로 PTZ",
    };
    private static readonly string[] _speakerNames = { "정문 스피커", "후문 스피커", "초소 방송" };
    private static readonly string[] _lampNames = { "정문 경광등", "후문 경광등", "초소 경광등", "본부 경광등" };

    private readonly Dictionary<MappingActionKind, List<MappingDeviceInfo>> _devices = new();

    public PreviewDeviceSource()
    {
        _devices[MappingActionKind.Camera] = Make(_cameraNames, 370, "PTZ");
        _devices[MappingActionKind.Speaker] = Make(_speakerNames, 620, "HORN");
        _devices[MappingActionKind.Lamp] = Make(_lampNames, 810, "LED");
    }

    private static List<MappingDeviceInfo> Make(string[] names, int baseId, string type)
        => names.Select((n, i) => new MappingDeviceInfo(baseId + i, n, type, new[] { i % 2 == 0 ? 3 : 9 }, i != 4)).ToList();

    public IReadOnlyList<MappingDeviceInfo> Devices(MappingActionKind kind) => _devices[kind];

    public IReadOnlyList<MappingGroupInfo> Groups() => new[]
    {
        new MappingGroupInfo(3, "1구역 센서 그룹"),
        new MappingGroupInfo(9, "2구역 센서 그룹"),
    };

    public MappingDeviceInfo? Find(MappingActionKind kind, int deviceId)
        => _devices[kind].FirstOrDefault(d => d.Id == deviceId);
}

/// <summary>읽기 전용 권한(화면 F 확인용) — 보기는 되고 편집은 안 된다.</summary>
internal sealed class PreviewPermissions : Ironwall.Dotnet.Libraries.Accounts.Api.Services.IPermissionService
{
    public static readonly PreviewPermissions ReadOnly = new();

    public event System.Action? PermissionsChanged { add { } remove { } }

    public EnumUserRole Role => EnumUserRole.USER;
    public bool IsAdmin => false;
    public string? LoginId => "preview";
    public string? Name => "미리보기";
    public DateTimeOffset? ValidUntil => null;
    public DateTimeOffset? ServerTime => null;
    public TimeSpan ClockSkew => TimeSpan.Zero;

    public bool HasRole(EnumUserRole required) => false;
    public bool CanView(string module) => true;
    public bool CanEdit(string module) => false;
    public bool CanControl(string module) => false;
    public bool CanDelete(string module) => false;
    public bool HasDeviceGroup(int id) => true;
    public IReadOnlyList<int> GetAccessibleDeviceGroups() => Array.Empty<int>();
    public bool CanAccessAuditLogs() => false;
    public void Apply(Ironwall.Dotnet.Libraries.Messages.Dto.Accounts.AuthUserDto user) { }
    public void Refresh(Ironwall.Dotnet.Libraries.Messages.Dto.Accounts.PermissionsSnapshotDto snapshot) { }
    public void Clear() { }
}

/// <summary>가짜 서버 — 메모리에만 산다.</summary>
internal sealed class PreviewMappingGateway : IMappingWorkbenchGateway
{
    /// <summary>이 판에서 서버가 실패를 돌려줄 배선 행(부분 실패 화면용).</summary>
    public HashSet<int> FailingConfigIds { get; } = new();

    /// <summary>목록을 아예 비워 빈 상태를 찍을 때.</summary>
    public bool IsEmpty { get; set; }

    private readonly List<EventMappingReadDto> _mappings = new()
    {
        new() { Id = 1, NameEvent = "울타리 침입 A구역", CategoryEventMapping = "FENCE_SENSOR_ONLY", DeviceGroupId = 3, Description = "1구역 전방 울타리", Status = true, UpdatedAt = "2026-09-18T10:00:00.000000+09:00" },
        new() { Id = 2, NameEvent = "2구역 멀티센서 경보", CategoryEventMapping = "MULTI_SENSOR_ONLY", DeviceGroupId = 9, Status = true, UpdatedAt = "2026-09-18T10:00:00.000000+09:00" },
        new() { Id = 3, NameEvent = "AI 판단 자동 추적", CategoryEventMapping = "AI_CAMERA_ONLY", DeviceGroupId = 3, Status = false, UpdatedAt = "2026-09-18T10:00:00.000000+09:00" },
        new() { Id = 4, NameEvent = "근무 교대 알림", CategoryEventMapping = "OPERATION_ONLY", DeviceGroupId = null, Status = true, UpdatedAt = "2026-09-18T10:00:00.000000+09:00" },
    };

    public Task<MappingCallResult<IReadOnlyList<EventMappingReadDto>>> ListMappingsAsync(CancellationToken token = default)
        => Task.FromResult(MappingCallResult<IReadOnlyList<EventMappingReadDto>>.Ok(
            IsEmpty ? Array.Empty<EventMappingReadDto>() : _mappings.ToList()));

    public Task<MappingCallResult<EventMappingReadDto>> GetMappingAsync(int mappingId, CancellationToken token = default)
    {
        var dto = _mappings.FirstOrDefault(m => m.Id == mappingId);
        return Task.FromResult(dto is null
            ? MappingCallResult<EventMappingReadDto>.Fail("맵핑을 찾을 수 없습니다.")
            : MappingCallResult<EventMappingReadDto>.Ok(dto));
    }

    public Task<MappingCallResult<IReadOnlyList<MappingCameraReadDto>>> ListCamerasAsync(int mappingId, CancellationToken token = default)
    {
        if (mappingId != 1) return Task.FromResult(MappingCallResult<IReadOnlyList<MappingCameraReadDto>>.Ok(Array.Empty<MappingCameraReadDto>()));

        var rows = new List<MappingCameraReadDto>
        {
            new()
            {
                ConfigId = 701, EventMappingId = 1, DelayTime = 5, IsEnable = true, Priority = 1,
                Camera = new MappingDeviceRefDto { Id = 370, CategoryDevice = "Camera" },
                TargetPreset = new MappingPresetRefDto { Id = 5, CameraId = 370, PresetName = "정문 정면", IsRestrictedZone = false },
                UpdatedAt = "2026-09-18T10:00:00.000000+09:00",
            },
            new()
            {
                ConfigId = 702, EventMappingId = 1, DelayTime = 0, IsEnable = true, Priority = 2,
                Camera = new MappingDeviceRefDto { Id = 371, CategoryDevice = "Camera" },
                UpdatedAt = "2026-09-18T10:00:00.000000+09:00",
            },
            new()
            {
                ConfigId = 703, EventMappingId = 1, DelayTime = 2, IsEnable = false, Priority = 3,
                Camera = new MappingDeviceRefDto { Id = 372, CategoryDevice = "Camera" },
                TargetPreset = new MappingPresetRefDto { Id = 8, CameraId = 372, PresetName = "감시금지 구역", IsRestrictedZone = true },
                UpdatedAt = "2026-09-18T10:00:00.000000+09:00",
            },
            // 고아 행 — 장비가 지워져 camera 가 null 이다(서버는 CASCADE 가 아니라 SET NULL).
            new()
            {
                ConfigId = 704, EventMappingId = 1, DelayTime = 0, IsEnable = true, Priority = 4,
                Camera = null,
                UpdatedAt = "2026-09-18T10:00:00.000000+09:00",
            },
        };
        return Task.FromResult(MappingCallResult<IReadOnlyList<MappingCameraReadDto>>.Ok(rows));
    }

    public Task<MappingCallResult<IReadOnlyList<MappingSpeakerReadDto>>> ListSpeakersAsync(int mappingId, CancellationToken token = default)
    {
        if (mappingId != 1) return Task.FromResult(MappingCallResult<IReadOnlyList<MappingSpeakerReadDto>>.Ok(Array.Empty<MappingSpeakerReadDto>()));

        var rows = new List<MappingSpeakerReadDto>
        {
            new()
            {
                ConfigId = 801, EventMappingId = 1, RepeatCount = 3, IsEnable = true, Priority = 1,
                Speaker = new MappingDeviceRefDto { Id = 620, CategoryDevice = "Speaker" },
                FileGroup = new MappingFileGroupRefDto { Id = 9, GroupName = "경고 방송 그룹" },
            },
        };
        return Task.FromResult(MappingCallResult<IReadOnlyList<MappingSpeakerReadDto>>.Ok(rows));
    }

    public Task<MappingCallResult<IReadOnlyList<MappingLampReadDto>>> ListLampsAsync(int mappingId, CancellationToken token = default)
    {
        if (mappingId != 1) return Task.FromResult(MappingCallResult<IReadOnlyList<MappingLampReadDto>>.Ok(Array.Empty<MappingLampReadDto>()));

        var rows = new List<MappingLampReadDto>
        {
            new()
            {
                ConfigId = 901, Priority = 1, IsEnable = true, Color = EnumLampColor.Red,
                BuzzerSound = EnumBuzzerSound.PiPiPi, BuzzerTime = 5, LightMode = EnumLightMode.Blinking,
                Lamp = new MappingDeviceRefDto { Id = 810, CategoryDevice = "Lamp" },
                EventMapping = new MappingParentRefDto { Id = 1, NameEvent = "울타리 침입 A구역" },
            },
            new()
            {
                ConfigId = 902, Priority = 2, IsEnable = true, Color = EnumLampColor.Orange,
                BuzzerSound = EnumBuzzerSound.FireAWang, BuzzerTime = 3, LightMode = EnumLightMode.Steady,
                Lamp = new MappingDeviceRefDto { Id = 811, CategoryDevice = "Lamp" },
                EventMapping = new MappingParentRefDto { Id = 1, NameEvent = "울타리 침입 A구역" },
            },
        };
        return Task.FromResult(MappingCallResult<IReadOnlyList<MappingLampReadDto>>.Ok(rows));
    }

    public Task<MappingCallResult<EventMappingReadDto>> CreateMappingAsync(EventMappingCreateDto body, CancellationToken token = default)
    {
        var dto = new EventMappingReadDto
        {
            Id = _mappings.Max(m => m.Id) + 1,
            NameEvent = body.NameEvent,
            CategoryEventMapping = body.CategoryEventMapping,
            DeviceGroupId = body.DeviceGroupId,
            Description = body.Description,
            Status = body.Status,
            UpdatedAt = "2026-09-18T12:00:00.000000+09:00",
        };
        _mappings.Add(dto);
        return Task.FromResult(MappingCallResult<EventMappingReadDto>.Ok(dto));
    }

    public Task<MappingCallResult<EventMappingReadDto>> PatchMappingAsync(int mappingId, EventMappingUpdateDto body, CancellationToken token = default)
        => GetMappingAsync(mappingId, token);

    public Task<MappingCallResult<MappingBulkCreateResultDto>> BulkCreateAsync(
        int mappingId, MappingActionKind kind, IReadOnlyList<object> items, CancellationToken token = default)
    {
        var result = new MappingBulkCreateResultDto
        {
            MappingId = mappingId,
            CreatedIds = Enumerable.Range(1000, Math.Max(0, items.Count - FailingConfigIds.Count)).ToList(),
            FailedItems = FailingConfigIds.Count == 0
                ? new List<MappingBulkFailedItemDto>()
                : new List<MappingBulkFailedItemDto> { new() { Index = items.Count - 1, Error = "Camera with id 999 not found" } },
            SkippedConfigIds = new List<int>(),
            NotFoundConfigIds = new List<int>(),
        };
        return Task.FromResult(MappingCallResult<MappingBulkCreateResultDto>.Ok(result));
    }

    public Task<MappingCallResult<MappingBulkUnassignResultDto>> BulkUnassignAsync(
        int mappingId, MappingActionKind kind, IReadOnlyList<int> configIds, CancellationToken token = default)
        => Task.FromResult(MappingCallResult<MappingBulkUnassignResultDto>.Ok(new MappingBulkUnassignResultDto
        {
            MappingId = mappingId,
            RemovedConfigIds = configIds.ToList(),
            SkippedConfigIds = new List<int>(),
            NotFoundConfigIds = new List<int>(),
        }));

    public Task<MappingCallResult<bool>> PatchConfigAsync(
        int mappingId, MappingActionKind kind, int configId, object body, CancellationToken token = default)
        => Task.FromResult(FailingConfigIds.Contains(configId)
            ? MappingCallResult<bool>.Fail("서버가 이 배선을 거절했습니다.", "422 VALUE_NOT_ALLOWED", 422)
            : MappingCallResult<bool>.Ok(true));
}
