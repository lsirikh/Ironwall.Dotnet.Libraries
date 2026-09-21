using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 드롭 유효성 매트릭스 · 경고 · 봉투 파싱 — 순수 함수 전건
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <see cref="MappingEligibility"/> — 와이어프레임 §3-2 드롭 유효성 매트릭스 전건.
/// </summary>
/// <remarks>
/// 드래그 제스처 자체는 UIA 로 단언할 수 없다(.NET 8 WPF 에 드래그 패턴 타입이 없다).
/// 그래서 판정을 UI 에서 떼어 여기서 전건을 본다 — 회귀 단언의 정본이 이 파일이다.
/// </remarks>
public class MappingEligibilityTests
{
    private static readonly MappingActionKind[] _kinds =
    {
        MappingActionKind.Camera, MappingActionKind.Speaker, MappingActionKind.Lamp,
    };

    /// <summary>3×3 매트릭스 — 같은 축만 통과한다.</summary>
    public static IEnumerable<object[]> KindPairs =>
        from source in _kinds
        from zone in _kinds
        select new object[] { source, zone };

    [Theory]
    [MemberData(nameof(KindPairs))]
    public void should_allow_palette_drop_only_when_the_axis_matches(MappingActionKind source, MappingActionKind zone)
    {
        var verdict = MappingEligibility.PaletteToBoard(source, zone, isReadOnly: false, hasMapping: true, newDeviceCount: 2);

        Assert.Equal(source == zone, verdict.IsAllowed);
    }

    [Fact]
    public void should_explain_the_axis_in_korean_when_refusing_a_palette_drop()
    {
        var verdict = MappingEligibility.PaletteToBoard(MappingActionKind.Camera, MappingActionKind.Speaker, false, true, 1);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("스피커", verdict.Reason);
        Assert.Contains("카메라", verdict.Reason);
    }

    [Fact]
    public void should_block_every_drop_when_read_only()
    {
        Assert.False(MappingEligibility.PaletteToBoard(MappingActionKind.Camera, MappingActionKind.Camera, true, true, 5).IsAllowed);
        Assert.False(MappingEligibility.BoardReorder(MappingActionKind.Camera, MappingActionKind.Camera, true, 2).IsAllowed);
        Assert.False(MappingEligibility.BoardToPalette(true, true, 2).IsAllowed);
        Assert.False(MappingEligibility.PresetToRow(201, 201, true).IsAllowed);
    }

    [Fact]
    public void should_block_palette_drop_when_no_mapping_is_selected()
    {
        var verdict = MappingEligibility.PaletteToBoard(MappingActionKind.Camera, MappingActionKind.Camera, false, false, 3);

        Assert.False(verdict.IsAllowed);
        Assert.Equal(MappingEligibility.NoMappingReason, verdict.Reason);
    }

    [Fact]
    public void should_block_palette_drop_when_everything_is_already_registered()
    {
        var verdict = MappingEligibility.PaletteToBoard(MappingActionKind.Camera, MappingActionKind.Camera, false, true, 0);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("이미", verdict.Reason);
    }

    [Theory]
    [MemberData(nameof(KindPairs))]
    public void should_allow_reorder_only_inside_the_same_tab(MappingActionKind source, MappingActionKind zone)
        => Assert.Equal(source == zone, MappingEligibility.BoardReorder(source, zone, false, 1).IsAllowed);

    [Fact]
    public void should_block_release_when_delete_permission_is_missing()
    {
        var verdict = MappingEligibility.BoardToPalette(isReadOnly: false, canDelete: false, rowCount: 2);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("권한", verdict.Reason);
    }

    [Fact]
    public void should_block_release_when_nothing_is_selected()
        => Assert.False(MappingEligibility.BoardToPalette(false, true, 0).IsAllowed);

    [Fact]
    public void should_allow_preset_when_it_belongs_to_the_same_camera()
        => Assert.True(MappingEligibility.PresetToRow(201, 201, false).IsAllowed);

    [Fact]
    public void should_block_preset_when_it_belongs_to_another_camera()
    {
        // 서버가 422(VALUE_NOT_ALLOWED)를 내는 자리를 드롭 전에 구조적으로 막는다.
        var verdict = MappingEligibility.PresetToRow(201, 202, false);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("다른 카메라", verdict.Reason);
    }

    [Fact]
    public void should_block_preset_when_the_row_is_orphaned()
    {
        var verdict = MappingEligibility.PresetToRow(201, null, false);

        Assert.False(verdict.IsAllowed);
        Assert.Contains("장비가 끊긴", verdict.Reason);
    }

    [Fact]
    public void should_report_only_devices_not_on_the_board()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1));

        var fresh = MappingEligibility.NewDeviceIds(board, MappingActionKind.Camera, new[] { 101, 102, 102, 0 });

        Assert.Equal(new[] { 102 }, fresh.ToArray());
    }
}

/// <summary><see cref="MappingWarnings"/> — 서버가 검사하지 않는 것들.</summary>
public class MappingWarningsTests
{
    private sealed class FakeDevices : IMappingDeviceSource
    {
        private readonly Dictionary<int, MappingDeviceInfo> _cameras = new();

        public FakeDevices Add(int id, params int[] groups)
        {
            _cameras[id] = new MappingDeviceInfo(id, $"장비{id}", "PTZ", groups, true);
            return this;
        }

        public IReadOnlyList<MappingDeviceInfo> Devices(MappingActionKind kind)
            => kind == MappingActionKind.Camera ? _cameras.Values.ToList() : new List<MappingDeviceInfo>();

        public IReadOnlyList<MappingGroupInfo> Groups() => new List<MappingGroupInfo>();

        public MappingDeviceInfo? Find(MappingActionKind kind, int deviceId)
            => kind == MappingActionKind.Camera && _cameras.TryGetValue(deviceId, out var info) ? info : null;
    }

    private static EventMappingReadDto Mapping(int id, int? groupId, string category = "FENCE_SENSOR_ONLY", bool status = true)
        => new() { Id = id, NameEvent = $"맵핑{id}", DeviceGroupId = groupId, CategoryEventMapping = category, Status = status };

    [Fact]
    public void should_warn_when_the_mapping_has_no_device_group()
    {
        var current = Mapping(1, null);

        var warnings = MappingWarnings.For(current, new[] { current }, new MappingBoard(), null);

        Assert.Contains(warnings, w => w.Text.Contains("매칭되지 않습니다"));
    }

    [Fact]
    public void should_warn_when_another_mapping_has_the_same_condition()
    {
        // 서버는 이 중복을 막지 않는다 — 이벤트가 나면 둘이 동시에 실행된다.
        var current = Mapping(1, 3);
        var twin = Mapping(2, 3);

        var warnings = MappingWarnings.For(current, new[] { current, twin }, new MappingBoard(), null);

        Assert.Contains(warnings, w => w.Text.Contains("동시에 실행"));
    }

    [Fact]
    public void should_not_warn_about_duplicates_when_the_condition_differs()
    {
        var current = Mapping(1, 3);
        var other = Mapping(2, 3, "CAMERA_ONLY");

        var warnings = MappingWarnings.For(current, new[] { current, other }, new MappingBoard(), null);

        Assert.DoesNotContain(warnings, w => w.Text.Contains("동시에 실행"));
    }

    [Fact]
    public void should_block_saving_when_an_orphan_row_exists()
    {
        var board = new MappingBoard();
        var dto = MappingRowFactory.Camera(10, 101);
        dto.Camera = null;
        board.Load(MappingActionKind.Camera, new[] { MappingBoardRow.FromDto(dto) });

        var warnings = MappingWarnings.For(Mapping(1, 3), new[] { Mapping(1, 3) }, board, null);

        Assert.True(MappingWarnings.Blocks(warnings));
        Assert.Contains("끊긴", MappingWarnings.Summarize(warnings));
    }

    [Fact]
    public void should_warn_when_a_device_is_outside_the_mapping_group()
    {
        var board = MappingRowFactory.WithCameras((10, 101, 1));
        var devices = new FakeDevices().Add(101, 9);       // 그룹 3 이 아니다

        var warnings = MappingWarnings.For(Mapping(1, 3), new[] { Mapping(1, 3) }, board, devices);

        Assert.Contains(warnings, w => w.Text.Contains("속하지 않는"));
    }

    [Fact]
    public void should_stay_quiet_when_the_device_is_not_in_the_cache()
    {
        // 모르는 것을 경고하지 않는다 — 캐시 미스를 "그룹 밖" 으로 읽으면 거짓 경고가 쏟아진다.
        var board = MappingRowFactory.WithCameras((10, 101, 1));

        var warnings = MappingWarnings.For(Mapping(1, 3), new[] { Mapping(1, 3) }, board, new FakeDevices());

        Assert.DoesNotContain(warnings, w => w.Text.Contains("속하지 않는"));
    }

    [Fact]
    public void should_note_when_the_mapping_itself_is_stopped()
    {
        var current = Mapping(1, 3, status: false);

        var warnings = MappingWarnings.For(current, new[] { current }, new MappingBoard(), null);

        Assert.Contains(warnings, w => w.Level == MappingWarningLevel.Info && w.Text.Contains("중지"));
    }

    [Fact]
    public void should_note_restricted_zone_presets()
    {
        var board = new MappingBoard();
        var dto = MappingRowFactory.Camera(10, 101, 1);
        dto.TargetPreset = new MappingPresetRefDto { Id = 5, CameraId = 101, IsRestrictedZone = true };
        board.Load(MappingActionKind.Camera, new[] { MappingBoardRow.FromDto(dto) });

        var warnings = MappingWarnings.For(Mapping(1, 3), new[] { Mapping(1, 3) }, board, null);

        Assert.Contains(warnings, w => w.Text.Contains("감시금지구역"));
    }

    [Fact]
    public void should_return_nothing_when_no_mapping_is_selected()
        => Assert.Empty(MappingWarnings.For(null, new List<EventMappingReadDto>(), new MappingBoard(), null));

    [Fact]
    public void should_put_the_blocking_warning_first_in_the_summary()
    {
        var board = new MappingBoard();
        var dto = MappingRowFactory.Camera(10, 101);
        dto.Camera = null;
        board.Load(MappingActionKind.Camera, new[] { MappingBoardRow.FromDto(dto) });
        var current = Mapping(1, null);        // 그룹 없음 경고도 함께 난다

        var summary = MappingWarnings.Summarize(MappingWarnings.For(current, new[] { current }, board, null));

        Assert.Contains("끊긴", summary);
    }

    [Fact]
    public void should_return_empty_summary_when_there_is_nothing_to_say()
        => Assert.Equal(string.Empty, MappingWarnings.Summarize(new List<MappingWarning>()));
}

/// <summary>봉투 파싱 — <c>data:[]</c> 와 <c>data:{items,total}</c> 를 둘 다 받는다.</summary>
public class MappingGatewayEnvelopeTests
{
    [Fact]
    public void should_read_rows_when_data_is_a_flat_array()
    {
        // 문서 v4 판은 경광등 목록을 data:[] 로 적고 있다.
        var envelope = JObject.Parse(@"{""success"":true,""data"":[{""id"":1},{""id"":2}]}");

        var rows = MappingWorkbenchGateway.ReadList<MappingLampReadDto>(envelope);

        Assert.Equal(new[] { 1, 2 }, rows.Select(r => r.ConfigId).ToArray());
    }

    [Fact]
    public void should_read_rows_when_data_has_items_and_total()
    {
        // 배포 v8 서버는 셋 다 {items,total} 이다.
        var envelope = JObject.Parse(@"{""success"":true,""data"":{""items"":[{""id"":7}],""total"":1}}");

        var rows = MappingWorkbenchGateway.ReadList<MappingCameraReadDto>(envelope);

        Assert.Single(rows);
        Assert.Equal(7, rows[0].ConfigId);
    }

    [Fact]
    public void should_return_empty_when_data_is_missing()
        => Assert.Empty(MappingWorkbenchGateway.ReadList<MappingCameraReadDto>(JObject.Parse(@"{""success"":true}")));

    [Fact]
    public void should_return_empty_when_envelope_is_null()
        => Assert.Empty(MappingWorkbenchGateway.ReadList<MappingCameraReadDto>(null));

    [Fact]
    public void should_read_one_when_data_is_an_object()
    {
        var envelope = JObject.Parse(@"{""success"":true,""data"":{""id"":10,""name_event"":""침입"",""status"":true}}");

        var dto = MappingWorkbenchGateway.ReadOne<EventMappingReadDto>(envelope);

        Assert.NotNull(dto);
        Assert.Equal(10, dto!.Id);
        Assert.Equal("침입", dto.NameEvent);
    }

    [Fact]
    public void should_return_null_when_data_is_an_array_but_one_was_expected()
        => Assert.Null(MappingWorkbenchGateway.ReadOne<EventMappingReadDto>(JObject.Parse(@"{""data"":[]}")));
}

/// <summary>팔레트 검색 — 이름과 부제를 본다.</summary>
public class MappingPaletteFilterTests
{
    private static MappingPaletteItemViewModel Item(int id, string name, string type = "PTZ")
        => new(MappingActionKind.Camera, new MappingDeviceInfo(id, name, type, new List<int>(), true));

    [Fact]
    public void should_keep_everything_when_search_is_blank()
    {
        var items = new[] { Item(1, "정문 PTZ"), Item(2, "후문 고정") };

        Assert.Equal(2, MappingPaletteFilter.Apply(items, "  ").Count);
    }

    [Fact]
    public void should_match_on_name()
        => Assert.True(MappingPaletteFilter.Matches(Item(1, "정문 PTZ"), "정문"));

    [Fact]
    public void should_match_on_type_in_the_subtitle()
        => Assert.True(MappingPaletteFilter.Matches(Item(1, "정문", "SPEED_DOME"), "speed"));

    [Fact]
    public void should_match_on_the_id_in_the_subtitle()
        => Assert.True(MappingPaletteFilter.Matches(Item(379, "정문"), "#379"));

    [Fact]
    public void should_not_match_when_nothing_contains_the_needle()
        => Assert.False(MappingPaletteFilter.Matches(Item(1, "정문 PTZ"), "경광등"));

    [Fact]
    public void should_preserve_order_when_filtering()
    {
        var items = new[] { Item(1, "가카메라"), Item(2, "나카메라"), Item(3, "가스피커") };

        var filtered = MappingPaletteFilter.Apply(items, "카메라");

        Assert.Equal(new[] { 1, 2 }, filtered.Select(i => i.Id).ToArray());
    }
}
