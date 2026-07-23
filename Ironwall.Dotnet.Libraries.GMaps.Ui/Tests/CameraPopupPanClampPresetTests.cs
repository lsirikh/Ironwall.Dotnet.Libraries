using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GMap.NET;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Streaming.Base.Hub;
using Ironwall.Dotnet.Libraries.Streaming.Base.Models;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

/****************************************************************************
   Purpose      : 카메라 팝업 — 패닝 클램프 제거(FR-A1) + ONVIF 프리셋 어댑터/상태(FR-C2/C3) 단위 테스트
   Created By   : Claude Code (CameraPopup_PanClamp_Badge_OnvifPtz)
   Created On   : 2026-07-23
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>테스트용 최소 Hub 스텁 — 팝업 VM 생성에만 필요(스트리밍 미기동, 어떤 메서드도 호출되지 않음).</summary>
file sealed class FakeHub : ISharedCameraStreamHub
{
    public Task<ICameraStreamLease> AcquireAsync(LeaseRequest request, RtspConnectionInfo info, CancellationToken ct = default)
        => throw new NotSupportedException("테스트에서 스트림 획득 없음");
    public Task ReleaseAsync(string cameraId, string leaseId) => Task.CompletedTask;
    public int GetRefCount(string cameraId) => 0;
    public IReadOnlyDictionary<string, int> GetAllRefCounts() => new Dictionary<string, int>();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class CameraPopupPanClampPresetTests
{
    private static CameraStreamPopupViewModel CreateVm(int cameraId = 7)
        => new(cameraId, "테스트카메라", null, new PointLatLng(37.0, 127.0), new FakeHub());

    /*──────────────── FR-A1: CanvasTop 세터 클램프 제거 ────────────────*/

    [Fact]
    public void should_keep_negative_canvastop_when_map_follow_sets_offscreen()
    {
        // Arrange: 맵을 위로 패닝하면 추종 경로(RefreshCameraPopupPositions)가 음수 Y를 세팅한다.
        var vm = CreateVm();

        // Act
        vm.CanvasTop = -180.5;

        // Assert: 과거엔 세터가 0으로 클램프해 "상단에 붙어 딸려오는" 버그(58b3fd7) — 이제 원값 보존.
        Assert.Equal(-180.5, vm.CanvasTop);
    }

    [Fact]
    public void should_keep_symmetry_with_canvasleft_when_offscreen_both_axes()
    {
        // 좌우(CanvasLeft)는 원래 클램프가 없어 정상이었다 — 세로도 동일 대칭이어야 추종이 일관된다.
        var vm = CreateVm();
        vm.CanvasLeft = -300;
        vm.CanvasTop = -300;
        Assert.Equal(vm.CanvasLeft, vm.CanvasTop);
    }

    [Fact]
    public void should_expose_min_canvastop_constant_for_drag_clamp_when_referenced_by_control()
    {
        // OQ-1(b): 상수는 드래그 경계(컨트롤 OnMouseMove)의 하한 단일 진실원으로 유지된다.
        Assert.Equal(0, CameraStreamPopupViewModel.MinCanvasTop);
    }

    /*──────────────── FR-C2: ONVIF 프리셋 표시 어댑터 ────────────────*/

    [Fact]
    public void should_map_token_and_name_when_dto_valid()
    {
        var item = new OnvifPresetDisplayModel(7, "p-01", "정문");
        Assert.Equal("p-01", item.Token);
        Assert.Equal("정문", item.PresetName);
        Assert.Equal(7, item.CameraId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void should_fallback_name_with_token_when_name_empty(string? name)
    {
        var item = new OnvifPresetDisplayModel(7, "tok9", name);
        Assert.Equal("프리셋 tok9", item.PresetName);
    }

    [Fact]
    public void should_not_mark_home_when_onvif_preset()
    {
        // ONVIF엔 per-preset Home 개념이 없다(OQ-6) — 행 Home 아이콘 게이트(IsHome)는 항상 false.
        Assert.False(new OnvifPresetDisplayModel(1, "t", "n").IsHome);
    }

    /*──────────────── FR-C3: 빈 목록 상태 문구 ────────────────*/

    [Fact]
    public void should_update_status_text_when_set_presets_with_reason()
    {
        var vm = CreateVm();
        vm.SetPresets(Array.Empty<Ironwall.Dotnet.Monitoring.Models.Maps.IPtzPresetModel>(), "프리셋 조회 실패");
        Assert.Empty(vm.Presets);
        Assert.Equal("프리셋 조회 실패", vm.PresetStatusText);
    }

    [Fact]
    public void should_keep_previous_status_when_reason_null()
    {
        var vm = CreateVm();
        vm.SetPresets(Array.Empty<Ironwall.Dotnet.Monitoring.Models.Maps.IPtzPresetModel>(), "PTZ 준비 중…");
        vm.SetPresets(Array.Empty<Ironwall.Dotnet.Monitoring.Models.Maps.IPtzPresetModel>(), null);
        Assert.Equal("PTZ 준비 중…", vm.PresetStatusText);
    }

    /*──────────────── FR-C2/OQ-6: Home 커맨드 → ONVIF 이벤트 ────────────────*/

    [Fact]
    public void should_raise_home_set_event_when_sethome_command_executed()
    {
        var vm = CreateVm();
        var raised = 0;
        vm.PresetHomeSetRequested += (_, _) => raised++;
        vm.SetHomeCommand.Execute(null);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void should_raise_home_goto_event_when_gotohome_command_executed()
    {
        var vm = CreateVm();
        var raised = 0;
        vm.PresetHomeGotoRequested += (_, _) => raised++;
        vm.GotoHomeCommand.Execute(null);
        Assert.Equal(1, raised);
    }
}
