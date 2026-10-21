using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping.Tests;
/****************************************************************************
   Purpose      : G25 — 스피커 연동 칸 음원 그룹 콤보(목록 성공) · 목록을 볼 수 없을 때 안내 + 번호 입력 폴백
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

public class MappingSpeakerFileGroupTests
{
    /// <summary>스피커 배선 1줄(음원 그룹 5 "정문 경고")을 가진 매핑 1건 — 실서버처럼 한 번 쉬었다가 답한다.</summary>
    private sealed class SpeakerGateway : IMappingWorkbenchGateway
    {
        private static async Task<MappingCallResult<T>> Reply<T>(T value)
        {
            await Task.Yield();
            return MappingCallResult<T>.Ok(value);
        }

        public Task<MappingCallResult<IReadOnlyList<EventMappingReadDto>>> ListMappingsAsync(CancellationToken token = default)
            => Reply<IReadOnlyList<EventMappingReadDto>>(new[]
            {
                new EventMappingReadDto { Id = 1, NameEvent = "울타리 침입", CategoryEventMapping = "FENCE_SENSOR_ONLY", DeviceGroupId = 3, Status = true, UpdatedAt = "T0" },
            });

        public Task<MappingCallResult<EventMappingReadDto>> GetMappingAsync(int mappingId, CancellationToken token = default)
            => Reply(new EventMappingReadDto { Id = mappingId, NameEvent = "울타리 침입", Status = true, UpdatedAt = "T0" });

        public Task<MappingCallResult<IReadOnlyList<MappingCameraReadDto>>> ListCamerasAsync(int mappingId, CancellationToken token = default)
            => Reply<IReadOnlyList<MappingCameraReadDto>>(Array.Empty<MappingCameraReadDto>());

        public Task<MappingCallResult<IReadOnlyList<MappingSpeakerReadDto>>> ListSpeakersAsync(int mappingId, CancellationToken token = default)
            => Reply<IReadOnlyList<MappingSpeakerReadDto>>(new[]
            {
                new MappingSpeakerReadDto
                {
                    ConfigId = 800, EventMappingId = 1, RepeatCount = 2, IsEnable = true, UpdatedAt = "T0",
                    Speaker = new MappingDeviceRefDto { Id = 501, CategoryDevice = "Speaker" },
                    FileGroup = new MappingFileGroupRefDto { Id = 5, GroupName = "정문 경고" },
                },
            });

        public Task<MappingCallResult<IReadOnlyList<MappingLampReadDto>>> ListLampsAsync(int mappingId, CancellationToken token = default)
            => Reply<IReadOnlyList<MappingLampReadDto>>(Array.Empty<MappingLampReadDto>());

        public Task<MappingCallResult<EventMappingReadDto>> CreateMappingAsync(EventMappingCreateDto body, CancellationToken token = default)
            => Reply(new EventMappingReadDto { Id = 2, UpdatedAt = "T0" });

        public Task<MappingCallResult<EventMappingReadDto>> PatchMappingAsync(int mappingId, EventMappingUpdateDto body, CancellationToken token = default)
            => Reply(new EventMappingReadDto { Id = mappingId, UpdatedAt = "T1" });

        public Task<MappingCallResult<MappingBulkCreateResultDto>> BulkCreateAsync(int mappingId, MappingActionKind kind, IReadOnlyList<object> items, CancellationToken token = default)
            => Reply(new MappingBulkCreateResultDto());

        public Task<MappingCallResult<MappingBulkUnassignResultDto>> BulkUnassignAsync(int mappingId, MappingActionKind kind, IReadOnlyList<int> configIds, CancellationToken token = default)
            => Reply(new MappingBulkUnassignResultDto());

        public Task<MappingCallResult<bool>> PatchConfigAsync(int mappingId, MappingActionKind kind, int configId, object body, CancellationToken token = default)
            => Reply(true);
    }

    private sealed class SpeakerDevices : IMappingDeviceSource
    {
        private readonly MappingDeviceInfo[] _speakers = { new(501, "정문 스피커", "IP", new[] { 3 }, true) };
        public IReadOnlyList<MappingDeviceInfo> Devices(MappingActionKind kind)
            => kind == MappingActionKind.Speaker ? _speakers : Array.Empty<MappingDeviceInfo>();
        public IReadOnlyList<MappingGroupInfo> Groups() => new[] { new MappingGroupInfo(3, "1구역") };
        public MappingDeviceInfo? Find(MappingActionKind kind, int deviceId) => Devices(kind).FirstOrDefault(d => d.Id == deviceId);
    }

    private sealed class FileGroups : IMappingFileGroupSource
    {
        private readonly MappingCallResult<IReadOnlyList<MappingFileGroupInfo>> _result;
        public FileGroups(MappingCallResult<IReadOnlyList<MappingFileGroupInfo>> result) => _result = result;
        public int Calls { get; private set; }
        public async Task<MappingCallResult<IReadOnlyList<MappingFileGroupInfo>>> ListFileGroupsAsync(CancellationToken token = default)
        {
            Calls++;
            await Task.Yield();
            return _result;
        }
    }

    /// <summary>매핑을 싣고 스피커 탭에서 그 행 하나를 고른다(뷰 코드비하인드와 같은 길).</summary>
    private static async Task<MappingWorkbenchViewModel> SpeakerSelectedAsync(IMappingFileGroupSource? source)
    {
        var vm = new MappingWorkbenchViewModel(new SpeakerGateway(), new SpeakerDevices()) { FileGroupSource = source };
        await vm.ReloadAsync();
        vm.SelectedRail = vm.RailEntries.First(e => e.Tag is MappingActionKind.Speaker);
        vm.SelectedBoardRows.Clear();
        vm.SelectedBoardRows.Add(vm.BoardRows.Single());
        vm.OnSelectionChanged();
        await vm.FileGroupLoadTask;
        return vm;
    }

    [Fact]
    public async Task 목록을_받으면_콤보에_음원_그룹이_뜨고_고르면_수정으로_잡힌다()
    {
        var source = new FileGroups(MappingCallResult<IReadOnlyList<MappingFileGroupInfo>>.Ok(new[]
        {
            new MappingFileGroupInfo(5, "정문 경고"),
            new MappingFileGroupInfo(9, "후문 경고"),
        }));
        var vm = await SpeakerSelectedAsync(source);

        Assert.True(vm.HasSpeakerEditor);
        Assert.True(vm.ShowFileGroupCombo);
        Assert.False(vm.ShowFileGroupNumber);
        Assert.Equal(new int?[] { null, 5, 9 }, vm.FileGroupOptions.Select(o => o.Id));
        Assert.Equal(5, vm.SelectedFileGroup?.Id);                 // 저장된 값이 콤보에 보인다
        Assert.Same(vm.FileGroupOptions[1], vm.SelectedFileGroup); // 목록 안의 그 항목(빈칸 방지)
        Assert.Equal(string.Empty, vm.FileGroupStatusText);

        vm.SelectedFileGroup = vm.FileGroupOptions.Single(o => o.Id == 9);
        vm.EditRepeatCount = 0;                                    // 최소 1

        var row = vm.BoardRows.Single().Row;
        Assert.Equal(MappingDraftState.Edited, row.State);
        Assert.Equal((9, "후문 경고", 1), (row.FileGroupId, row.FileGroupName, row.RepeatCount));
        var body = MappingRequestBuilder.PatchSpeaker(row, null);
        Assert.Equal(9, body.FileGroupId);
        Assert.True(vm.CanApply);

        vm.SelectedFileGroup = MappingFileGroupOption.None;        // 없음 = 해제
        Assert.Null(row.FileGroupId);
        Assert.Equal(1, source.Calls);                             // 창마다 한 번만 묻는다
    }

    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    public async Task 목록을_볼_수_없으면_안내하고_번호_입력으로_받는다(int status)
    {
        var source = new FileGroups(MappingCallResult<IReadOnlyList<MappingFileGroupInfo>>.Fail(
            DeviceApiMappingFileGroupSource.Unavailable, "FORBIDDEN", status));
        var vm = await SpeakerSelectedAsync(source);

        Assert.True(vm.ShowFileGroupNumber);
        Assert.False(vm.ShowFileGroupCombo);
        Assert.Equal("음원 목록을 볼 수 없습니다. 음원 번호를 직접 넣으세요.", vm.FileGroupStatusText);
        Assert.Equal("5", vm.EditFileGroupNumber);                 // 저장된 번호 그대로

        vm.EditFileGroupNumber = "abc";                            // 숫자가 아니면 받지 않는다
        Assert.Equal(5, vm.BoardRows.Single().Row.FileGroupId);

        vm.EditFileGroupNumber = "12";
        var row = vm.BoardRows.Single().Row;
        Assert.Equal(12, row.FileGroupId);
        Assert.Equal(MappingDraftState.Edited, row.State);
        Assert.Equal(12, MappingRequestBuilder.PatchSpeaker(row, null).FileGroupId);

        // 다른 줄을 골랐다 돌아와도 403 · 404 는 다시 묻지 않는다
        vm.SelectedBoardRows.Clear(); vm.OnSelectionChanged();
        vm.SelectedBoardRows.Add(vm.BoardRows.Single()); vm.OnSelectionChanged();
        await vm.FileGroupLoadTask;
        Assert.Equal(1, source.Calls);
    }

    [Fact]
    public async Task 공급원이_없어도_번호_입력으로_받는다()
    {
        var vm = await SpeakerSelectedAsync(null);

        Assert.True(vm.ShowFileGroupNumber);
        Assert.True(vm.HasFileGroupStatus);
    }

    #region - 장비 API 어댑터 -
    [Fact]
    public async Task 어댑터는_쪽을_넘겨_모으고_이름순으로_준다()
    {
        var api = new Mock<IDeviceApiService>();
        api.Setup(a => a.GetFileGroupsAsync(null, 1, 100, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiListResponse<FileGroupDto>
           {
               Success = true, StatusCode = 200,
               Data = Enumerable.Range(1, 100).Select(i => new FileGroupDto { Id = i, Name = $"그룹{i:000}" }).ToList(),
               Pagination = new PaginationDto { Page = 1, Limit = 100, Total = 101 },
           });
        api.Setup(a => a.GetFileGroupsAsync(null, 2, 100, It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiListResponse<FileGroupDto>
           {
               Success = true, StatusCode = 200,
               Data = new List<FileGroupDto> { new() { Id = 101, Name = "가장 앞" } },
               Pagination = new PaginationDto { Page = 2, Limit = 100, Total = 101 },
           });

        var result = await new DeviceApiMappingFileGroupSource(api.Object).ListFileGroupsAsync();

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(101, result.Value!.Count);
        Assert.Equal("가장 앞", result.Value[0].Name);
        api.Verify(a => a.GetFileGroupsAsync(null, It.IsAny<int>(), 100, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(403, DeviceApiMappingFileGroupSource.Unavailable)]
    [InlineData(404, DeviceApiMappingFileGroupSource.Unavailable)]
    [InlineData(500, DeviceApiMappingFileGroupSource.LoadFailed)]
    public async Task 어댑터는_403_404를_볼_수_없음으로_가른다(int status, string expected)
    {
        var api = new Mock<IDeviceApiService>();
        api.Setup(a => a.GetFileGroupsAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(new ApiListResponse<FileGroupDto> { Success = false, StatusCode = status, Error = new ApiError { Code = "X" } });

        var result = await new DeviceApiMappingFileGroupSource(api.Object).ListFileGroupsAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Message);
        Assert.Equal(status, result.StatusCode);
    }
    #endregion
}
