using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using System.Linq;
using System.Windows.Input;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 표 보기 정리(2026-09-30) — 체인 표 열(위치 · A/B · 종류 · 방향 · 간격 · 상태)과 함체 자리 구분 띠, 제어기 머리 글, 표 보기의 키 F.
/// </summary>
public class WiringTableRowsTests
{
    [Fact]
    public void should_fill_type_facing_gap_and_port_columns_when_the_chain_mixes_smart_multi_and_fence()
    {
        // Arrange — 스마트 1 · 복합 1(뒤) · 펜스 1
        var vm = Open(("SmartSensor2", WiringFacing.Front), ("Multi", WiringFacing.Back), ("Fence", WiringFacing.Front));

        // Act
        var rows = vm.Line1.Where(s => s.IsFilled).ToList();

        // Assert
        Assert.Equal(new[] { "스마트 복합", "복합", "펜스" }, rows.Select(r => r.TypeText));
        Assert.Equal(new[] { "앞", "뒤", "—" }, rows.Select(r => r.FacingText));            // 펜스센서는 방향 없음
        Assert.Equal(new[] { "—", "6m", "3m" }, rows.Select(r => r.GapText));               // 두 종류 중 작은 값(펜스 3m)
        Assert.Equal("A1 · B3", rows[0].PortText);
        Assert.Equal(new[] { "Radar", "MotionSensor", "Fence" }, rows.Select(r => r.TypeIcon));
        Assert.All(rows, r => Assert.False(r.IsDraft));
    }

    [Fact]
    public void should_put_the_enclosure_split_band_before_the_row_after_the_controller_gap()
    {
        // Arrange — 6대 링 · 함체 틈 기본 = 가운데(3)
        var vm = Open(Enumerable.Range(0, 6).Select(_ => ("SmartSensor2", WiringFacing.Front)).ToArray());

        // Act
        var band = vm.Line1.Where(s => s.EnclosureBefore).ToList();

        // Assert
        var only = Assert.Single(band);
        Assert.Equal(3, only.Index);
        Assert.Contains("A 쪽 1~3", only.EnclosureLabel);
        Assert.Contains("B 쪽 4~6", only.EnclosureLabel);
    }

    [Fact]
    public void should_mark_the_row_unsaved_and_flip_facing_when_f_is_pressed_in_the_table()
    {
        // Arrange
        var vm = Open(("SmartSensor2", WiringFacing.Front), ("SmartSensor2", WiringFacing.Front));
        vm.ShowTableView();
        vm.Line1[1].IsSelected = true;

        // Act
        var handled = WiringView.HandleLineKey(vm, Key.F, Key.None);

        // Assert
        Assert.True(handled);
        Assert.Equal("뒤", vm.Line1[1].FacingText);
        Assert.True(vm.Line1[1].IsDraft);
        Assert.True(vm.Line1[1].IsSelected);                                                  // 다시 그려도 고른 줄 유지
        Assert.Equal("바뀐 줄 1", vm.DraftText);
    }

    [Fact]
    public void should_summarise_the_controller_kind_and_chain_length_in_the_table_header()
    {
        var vm = Open(("SmartSensor2", WiringFacing.Front), ("SmartSensor2", WiringFacing.Front), ("SmartSensor2", WiringFacing.Front));

        Assert.StartsWith("스마트 제어기 · 링", vm.ControllerKindText);
        Assert.Contains("체인 3대 · 길이 약 12m / 기준 200m", vm.ChainSummaryText);
    }

    private static WiringViewModel Open(params (string Type, WiringFacing Facing)[] sensors)
    {
        var seeds = sensors.Select((s, i) => new WiringSensorSeed(
            101 + i, i + 1, new SensorFacts(1101 + i, $"센서 {i + 1}", s.Type, "북측"), new WiringPlacement(1, i + 1, s.Facing)));
        return WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL-북측-01", "10.99.7.1", "SmartController"),
            seeds, sensors.Select(s => s.Type).Distinct().ToList(), null, new WiringFakeDialogs());
    }
}
