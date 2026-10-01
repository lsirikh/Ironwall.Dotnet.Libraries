using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 펜스 편집기 뷰모델(fence-wiring-editor FR-01 · FR-03 · FR-05 · FR-07 ~ FR-15) — 헤드리스.
/// 끌기 제스처는 UIA 로 단언할 수 없어 뷰모델 길(키보드 · 메뉴 · 단추가 부르는 같은 길)로 잡는다.
/// </summary>
public class WiringFenceEditorTests
{
    private const string MIXED = "SFSSFFSFSSFS";            // 4차 현장 — 스마트 7 · 펜스 5

    /// <summary>센서 101…(스마트 = IP 센서) · 서버 체인 1…N · 번호 1101….</summary>
    private static (WiringViewModel Vm, WiringFakeDialogs Dialogs, FakeFenceStore Store, WiringFakeGateway Gateway) Build(
        string types = "SSSSSSSSSSSSS", FenceLayoutDocument? document = null, bool withStore = true, FakePing? ping = null, string address = "10.99.7.1")
    {
        var gateway = new WiringFakeGateway();
        var seeds = new List<WiringSensorSeed>();
        for (var i = 0; i < types.Length; i++)
        {
            var id = 101 + i;
            var smart = types[i] == 'S';
            var type = smart ? "SmartSensor2" : "Fence";
            var dto = WiringDoubles.ServerSensor(id, 1101 + i, i + 1, new WiringPlacement(1, i + 1));
            dto.TypeDevice = type;
            gateway.Fetched[id] = dto;
            seeds.Add(new WiringSensorSeed(id, smart ? null : i + 1, new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", type, "북측 7구간"),
                new WiringPlacement(1, i + 1),
                ConnectionType: smart ? "IP_DIRECT" : "RS485",
                IpAddress: smart ? $"192.168.10.{i + 1}" : null,
                LinkHealth: i switch { 0 => "OK", 2 => "DEGRADED", 3 => "FAULT", _ => null }));
        }
        var dialogs = new WiringFakeDialogs { Confirm = true };
        var store = new FakeFenceStore { Stored = document };
        var apply = new WiringApplyService(gateway, policy: WiringDoubles.AxisPolicy());
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL-북측-01", address, "SmartController"), seeds,
            new[] { "SmartSensor2", "Fence" }, apply, dialogs, fence: new WiringFenceContext(document, withStore ? store : null, ping));
        return (vm, dialogs, store, gateway);
    }

    #region - Load (FR-01) -
    [Fact]
    public void should_propose_a_fence_from_the_current_chain_without_marking_anything_dirty_when_nothing_is_stored()
    {
        var (vm, _, _, _) = Build();

        Assert.True(vm.FenceLayout.IsActive);
        Assert.True(vm.FenceLayout.IsProposed);
        Assert.Equal(12, vm.FenceLayout.Panels.Count);                                // 6m 간격 스마트 13대 = 망 12칸
        Assert.Equal(vm.FenceChain.Keys, FenceLayoutMath.PositionOrder(vm.FenceLayout.Mounts.Select(p => (p.Key, p.Value))));
        Assert.False(vm.HasChanges);                                                    // 제안은 저장 대상이 아니다
        Assert.Equal(WiringViewModel.FENCE_PROPOSED_NOTICE, vm.FenceNoticeText);
    }

    [Fact]
    public void should_fit_stored_seats_to_the_server_chain_order_and_say_so_when_another_gis_reordered()
    {
        // Arrange — 로컬에는 101 이 102 뒤(기둥 1)로 저장돼 있지만 서버 순서는 101, 102, 103
        var document = new FenceLayoutDocument
        {
            ControllerId = 10,
            Panels = Enumerable.Repeat(FencePanelSpec.Default(), 3).ToList(),
            Mounts = new Dictionary<int, SensorMountSpec>
            {
                [101] = new(1, FenceMountSpot.PostTop),
                [102] = new(0, FenceMountSpot.PostTop),
                [103] = new(2, FenceMountSpot.PostTop),
            },
            Revision = 4,
        };

        // Act
        var (vm, _, _, _) = Build("SSS", document);

        // Assert — 서버가 순서의 정본: 자리가 순서를 따라간다
        Assert.Equal(new[] { 101, 102, 103 }, vm.FenceChain.Keys);
        Assert.Equal(0, vm.FenceLayout.MountOf(101)!.Panel);
        Assert.Equal(1, vm.FenceLayout.MountOf(102)!.Panel);
        Assert.Equal(WiringViewModel.FENCE_REORDERED_NOTICE, vm.FenceNoticeText);
        Assert.True(vm.HasLocalChanges);                                                // 맞춘 자리는 저장해야 남는다
        Assert.False(vm.FenceLayout.IsProposed);
    }
    #endregion

    #region - Panels (FR-03 · FR-04 · PRD §5) -
    [Fact]
    public void should_apply_only_touched_fields_and_undo_in_one_step_when_a_keyboard_selected_panel_range_is_edited()
    {
        // Arrange
        var (vm, _, _, _) = Build();
        var before = vm.FenceLayout.Panels.ToList();

        // Act — 망 3 에서 Shift+→ 두 번 = 망 3~5
        vm.FenceSelectPanel(2);
        vm.FenceExtendPanelSelection(3);
        vm.FenceExtendPanelSelection(4);
        vm.ChoosePanelStyle(EnumFenceStyle.ChainLinkRazor);
        vm.PanelHeightText = "3.2";
        var title = vm.ApplyPanelEditText;
        var applied = vm.ApplyPanelEdit();
        var after = vm.FenceLayout.Panels.ToList();
        vm.Undo();

        // Assert
        Assert.Equal("선택한 망에 적용 (3칸)", title);
        Assert.True(applied);
        Assert.Equal(new[] { EnumFenceStyle.ChainLinkRazor }, after.Skip(2).Take(3).Select(p => p.Style).Distinct());
        Assert.All(after.Skip(2).Take(3), p => Assert.Equal(3.2, p.HeightM));
        Assert.All(after.Skip(2).Take(3), p => Assert.Equal(6, p.SpanM));              // 거리는 손대지 않았다
        Assert.Equal(before[0], after[0]);
        Assert.Equal(before, vm.FenceLayout.Panels);                                    // 되돌리기 한 번 = 원상
    }

    [Fact]
    public void should_show_many_values_and_refuse_an_out_of_range_height_when_panels_differ()
    {
        var (vm, _, _, _) = Build("SSSS");
        vm.FenceSelectPanel(0);
        vm.ChoosePanelSpan(3);
        vm.ApplyPanelEdit();

        vm.FenceSelectPanels(new[] { 0, 1 });
        var spanHint = vm.PanelSpanHint;
        var spanText = vm.PanelSpanText;
        vm.PanelHeightText = "7";

        Assert.Equal("여러 값", spanHint);
        Assert.Equal(string.Empty, spanText);
        Assert.True(vm.HasPanelEditError);
        Assert.False(vm.CanApplyPanelEdit);
    }

    [Fact]
    public void should_keep_selections_apart_and_show_the_last_chosen_when_sensors_and_panels_are_both_selected()
    {
        var (vm, _, _, _) = Build("SSSS");

        vm.FenceSelect(102);
        vm.FenceSelectPanels(new[] { 1, 2 });
        var paneAfterPanels = (vm.FencePaneKind, vm.HasPanelSelection, vm.HasFenceSelection);
        var sensorsKept = vm.FenceSelectedKeys.ToList();
        vm.FenceSelect(103);

        Assert.Equal((FenceSelectionKind.Panels, true, false), paneAfterPanels);
        Assert.Equal(new[] { 102 }, sensorsKept);
        Assert.Equal(FenceSelectionKind.Sensors, vm.FencePaneKind);
        Assert.Equal(new[] { 1, 2 }, vm.FenceSelectedPanels);                          // 망 선택은 그대로 남는다
        Assert.True(vm.FenceClearSelection());                                          // Esc — 둘 다 푼다
        Assert.False(vm.HasAnySelection);
        Assert.False(vm.FenceClearSelection());                                         // 풀 것이 없으면 Esc 를 흘려보낸다
    }
    #endregion

    #region - Sensors on the fence (FR-05 · FR-07 · FR-09) -
    [Fact]
    public void should_reorder_the_chain_when_a_sensor_is_moved_to_another_panel()
    {
        var (vm, _, _, _) = Build("SSSSS");

        var ok = vm.FenceMoveSensors(new[] { 101 }, 101, targetMetres: 3 * 6.0 + 3);    // 기둥 3 과 4 사이 → 가까운 기둥
        var chain = vm.FenceChain.Keys.ToList();

        Assert.True(ok);
        Assert.Equal(new[] { 102, 103, 104, 101, 105 }, chain);
        Assert.True(vm.HasChanges);
        Assert.Contains("옮김", vm.StatusText);
    }

    [Fact]
    public void should_move_the_selected_sensors_to_the_panel_center_and_back_with_one_undo_when_a_spot_is_chosen()
    {
        var (vm, _, _, _) = Build("SSSS");
        vm.FenceSelectSensors(new[] { 102, 103 });

        vm.ChooseMountSpot(FenceMountSpot.PanelCenter);
        var spots = new[] { 102, 103 }.Select(k => vm.FenceLayout.MountOf(k)!.Spot).ToList();
        vm.Undo();

        Assert.Equal(new[] { FenceMountSpot.PanelCenter, FenceMountSpot.PanelCenter }, spots);
        Assert.All(new[] { 102, 103 }, k => Assert.Equal(FenceMountSpot.PostTop, vm.FenceLayout.MountOf(k)!.Spot));
    }

    [Fact]
    public async Task should_say_how_many_sensors_change_when_a_mount_style_is_spread_to_all()
    {
        // Arrange — 13대 중 3대는 이미 기둥 위 −0.3
        var (vm, dialogs, _, _) = Build();
        vm.FenceSelectSensors(new[] { 101, 102, 103 });
        vm.MountOffsetText = "-0.3";
        vm.ApplyMountOffset();
        vm.FenceSelect(101);

        // Act — 101 에서 오른쪽 클릭 → "이 설치 방식을 이 제어기 모든 센서에 적용"
        var menu = vm.FenceMenu(FenceMenuTargetKind.Sensor, 101);
        await menu.Single(e => e.AutomationId.EndsWith("ApplyAll")).Run!();
        var offsets = vm.FenceLayout.Mounts.Values.Select(m => m.HeightOffsetM).Distinct().ToList();
        vm.Undo();

        // Assert
        Assert.Contains(dialogs.Confirms, c => c.Message.StartsWith("13대 중 10대가 바뀝니다"));
        Assert.Equal(new[] { -0.3 }, offsets);
        Assert.Equal(10, vm.FenceLayout.Mounts.Values.Count(m => m.HeightOffsetM == 0));   // 되돌리기 한 번으로 통째 취소
    }

    [Fact]
    public void should_offer_the_storyboard_menu_items_when_a_sensor_or_a_panel_is_targeted()
    {
        var (vm, _, _, _) = Build(MIXED);

        var sensor = vm.FenceMenu(FenceMenuTargetKind.Sensor, 101).Where(e => !e.IsSeparator).Select(e => e.Text).ToList();
        var panel = vm.FenceMenu(FenceMenuTargetKind.Panel, 0).Select(e => e.Text).ToList();

        Assert.Equal("이 설치 방식을 이 제어기 모든 센서에 적용", sensor[0]);
        Assert.StartsWith("같은 종류 센서에만 적용 (", sensor[1]);
        Assert.Contains("7대", sensor[1]);
        Assert.Contains("방향만 모두 앞으로", sensor);
        Assert.Contains("결선에서 빼기", sensor);
        Assert.Equal(new[] { "망 속성 복사", "선택한 망에 붙여넣기 (0칸)", "이 망 속성을 모든 망에" }, panel);
    }

    [Fact]
    public async Task should_paste_a_copied_panel_to_the_selected_panels_when_confirmed()
    {
        var (vm, dialogs, _, _) = Build("SSSS");
        vm.FenceSelectPanel(0);
        vm.ChoosePanelStyle(EnumFenceStyle.DesignFence);
        vm.ApplyPanelEdit();
        vm.CopyPanel(0);

        vm.FenceSelectPanels(new[] { 1 });
        var pasted = await vm.PastePanelAsync();

        Assert.True(pasted);
        Assert.Equal(new[] { EnumFenceStyle.DesignFence, EnumFenceStyle.DesignFence, EnumFenceStyle.ChainLink },
                     vm.FenceLayout.Panels.Select(p => p.Style));
        Assert.Contains(dialogs.Confirms, c => c.Message.StartsWith("1칸 중 1칸이 바뀝니다"));
    }
    #endregion

    #region - Numbering (FR-09 · FR-10 · FR-11) -
    [Fact]
    public void should_number_the_mixed_ring_one_to_seven_and_101_to_105_when_the_tier4_preset_is_chosen()
    {
        var (vm, _, _, _) = Build(MIXED);

        vm.ChooseBandPreset(NumberBandSet.PRESET_TIER4);
        var numbers = vm.FenceChain.Keys.Select(k => vm.Board.Find(k)!.Facts.Number).ToList();

        Assert.Equal(new[] { 1, 101, 2, 3, 102, 103, 4, 104, 5, 6, 105, 7 }, numbers);
        Assert.Contains("중요시설 4차", vm.BandText);
        Assert.False(WiringValidation.BlocksSave(vm.Issues));
    }

    [Fact]
    public void should_renumber_visibly_but_only_as_draft_when_a_sensor_moves()
    {
        var (vm, _, _, gateway) = Build("SSSSS");
        vm.ChooseBandPreset(NumberBandSet.PRESET_TIER3);

        vm.FenceMoveSensors(new[] { 101 }, 101, targetMetres: 24);                   // 끝 기둥
        var numbers = vm.FenceChain.Keys.ToDictionary(k => k, k => vm.Board.Find(k)!.Facts.Number);

        Assert.Equal(5, numbers[101]);
        Assert.Equal(1, numbers[102]);
        Assert.Equal(0, gateway.PatchCount);                                           // [저장하기] 전에는 서버를 부르지 않는다
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public void should_block_saving_when_the_band_is_too_small()
    {
        var (vm, _, _, _) = Build("SSSS");
        vm.FenceSelectController();
        vm.BandRows.Single(r => r.Category == FenceSensorCategory.Smart).StartText = "1";
        vm.BandRows.Single(r => r.Category == FenceSensorCategory.Smart).EndText = "3";

        vm.ApplyCustomBands();

        Assert.True(WiringValidation.BlocksSave(vm.Issues));
        Assert.False(vm.CanSave);
        Assert.Contains("3대까지", vm.SaveBlockedReason);
    }

    [Fact]
    public async Task should_show_the_before_after_number_table_with_the_warning_and_write_number_device_and_wiring_v3_when_saved()
    {
        // Arrange
        var (vm, dialogs, store, gateway) = Build("SSS");
        vm.ChooseBandPreset(NumberBandSet.PRESET_TIER3);

        // Act
        await vm.SaveAsync();

        // Assert — 번호 표 + 경고
        Assert.Equal(new[] { "북측 1구간 펜스 1101 → 1", "북측 2구간 펜스 1102 → 2", "북측 3구간 펜스 1103 → 3" }, dialogs.LastNumberChanges!.Select(c => c.Text));
        Assert.Equal(WiringViewModel.NUMBER_WARNING, dialogs.LastNumberWarning);
        // 서버: number_device 만 바뀌고(버스 주소는 그대로) 다른 칸은 받은 값으로 되채운다
        Assert.Equal(3, gateway.PatchCount);
        var first = gateway.Patched.Single(p => p.Id == 101).Dto;
        Assert.Equal(1, first.NumberDevice);
        Assert.Equal("북측 1구간 펜스", first.NameDevice);
        Assert.Equal("ACTIVATED", first.Status);
        Assert.Null(first.HardwareSpec);                                              // 자리는 그대로라 spec 을 싣지 않는다
        // 로컬: 대역 · 망 · 자리(서버 id) 가 저장되고 기준이 옮겨졌다
        var saved = Assert.Single(store.Saved);
        Assert.Equal(NumberBandSet.Tier3, saved.Bands);
        Assert.Equal(new[] { 101, 102, 103 }, saved.Mounts.Keys.OrderBy(k => k));
        Assert.False(vm.HasChanges);
        Assert.False(vm.FenceLayout.IsProposed);
    }

    [Fact]
    public async Task should_write_the_v3_wiring_without_shape_when_the_order_changes()
    {
        var (vm, _, _, gateway) = Build("SSS");
        vm.FenceMoveSensors(new[] { 101 }, 101, targetMetres: 12);

        await vm.SaveAsync();

        var wiring = (JObject)gateway.Patched.First(p => p.Id == 101).Dto.HardwareSpec!.Spec!["wiring"]!;
        Assert.Equal(3, (int)wiring["v"]!);
        Assert.Equal(3, (int)wiring["order"]!);
        Assert.Equal(1, (int)wiring["line"]!);
        Assert.Null(wiring["shape"]);
        Assert.Equal("front", (string?)wiring["facing"]);
    }

    [Fact]
    public async Task should_keep_local_changes_and_say_so_when_another_gis_saved_the_fence_first()
    {
        var (vm, _, store, gateway) = Build("SSS");
        store.Next = FenceLayoutSaveStatus.Conflict;
        vm.FenceSelectPanel(0);
        vm.ChoosePanelStyle(EnumFenceStyle.Brick);
        vm.ApplyPanelEdit();

        await vm.SaveAsync();

        Assert.Equal(0, gateway.PatchCount);                                           // 서버에 갈 것은 없었다(모양만 바뀜)
        Assert.True(vm.HasLocalChanges);
        Assert.Contains("먼저 저장", vm.StatusText);
    }

    [Fact]
    public void should_hide_local_saving_but_keep_editing_when_there_is_no_store()
    {
        var (vm, _, _, _) = Build("SSS", withStore: false);
        vm.FenceSelectPanel(0);
        vm.ChoosePanelStyle(EnumFenceStyle.Concrete);

        var applied = vm.ApplyPanelEdit();

        Assert.True(applied);
        Assert.False(vm.HasFenceStore);
        Assert.False(vm.HasChanges);                                                  // 저장할 곳이 없으면 저장 대상도 아니다
        Assert.False(vm.CanSave);
    }
    #endregion

    #region - Concept · selection (FR-12 · FR-13 · FR-15) -
    [Fact]
    public void should_share_one_selection_when_fence_table_or_concept_selects()
    {
        var (vm, _, _, _) = Build("SSSS");

        vm.FenceSelectSensors(new[] { 102, 103 });
        var table = vm.Line1.Where(s => s.IsSelected).Select(s => s.Row!.Key).ToList();
        var concept = vm.ConceptNodes().Where(n => n.IsSelected).Select(n => n.Key).ToList();
        vm.SelectFromTable(new[] { 104 });

        Assert.Equal(new[] { 102, 103 }, table);
        Assert.Equal(new[] { 102, 103 }, concept);
        Assert.Equal(new[] { 104 }, vm.FenceSelectedKeys);
    }

    [Fact]
    public void should_move_the_fence_seat_along_when_a_sensor_is_reordered_in_the_concept_diagram()
    {
        var (vm, _, _, _) = Build("SSSS");
        var seat = vm.FenceLayout.MountOf(101)!;

        vm.ConceptMove(new[] { 101 }, 3);                                             // 103 과 104 사이로

        Assert.Equal(new[] { 102, 103, 101, 104 }, vm.FenceChain.Keys);
        Assert.Equal(2, vm.FenceLayout.MountOf(101)!.Panel);                           // 펜스 위 자리도 따라갔다
        Assert.Equal(seat, vm.FenceLayout.MountOf(102));                                // 자리 묶음은 그대로
    }

    [Fact]
    public void should_show_ip_or_node_address_when_connection_type_differs()
    {
        var (vm, _, _, _) = Build("SF");

        Assert.True(vm.IsIpSensor(101));
        Assert.Equal("192.168.10.1", vm.AddressTextOf(101));
        Assert.Equal("IP 주소", vm.AddressLabelOf(101));
        Assert.False(vm.IsIpSensor(102));
        Assert.Equal("2", vm.AddressTextOf(102));
        Assert.Equal("노드 주소", vm.AddressLabelOf(102));
        Assert.True(vm.HasIpSensors);
        Assert.Equal(new[] { true, false }, vm.ConceptNodes().Select(n => n.IsIp));
    }
    #endregion

    #region - Signals (FR-14) -
    [Fact]
    public async Task should_light_controller_and_sensor_lamps_when_pings_and_network_interface_health_arrive()
    {
        var ping = new FakePing();
        var (vm, _, _, _) = Build("SSSSS", ping: ping);

        var before = vm.ControllerSignal;
        await vm.PingControllerOnceAsync();
        var ok = vm.ControllerSignal;
        for (var i = 0; i < 3; i++) ping.Next.Enqueue(new PingSample(false, 0));
        for (var i = 0; i < 3; i++) await vm.PingControllerOnceAsync();

        Assert.Equal(SignalLevel.Unknown, before);
        Assert.Equal(SignalLevel.Ok, ok);
        Assert.Equal(SignalLevel.Down, vm.ControllerSignal);
        Assert.All(ping.Hosts, h => Assert.Equal("10.99.7.1", h));
        Assert.Equal(new[] { SignalLevel.Ok, SignalLevel.Unknown, SignalLevel.Slow, SignalLevel.Down, SignalLevel.Unknown },
                     vm.ConceptNodes().Select(n => n.Signal));
    }

    [Fact]
    public async Task should_not_ping_when_the_controller_has_no_address()
    {
        var ping = new FakePing();
        var (vm, _, _, _) = Build("S", ping: ping, address: "");

        var sent = await vm.PingControllerOnceAsync();
        vm.StartSignals();

        Assert.False(sent);
        Assert.False(vm.IsPinging);
        Assert.Empty(ping.Hosts);
        Assert.StartsWith("모름", vm.ControllerSignalText);
    }

    [Fact]
    public async Task should_stop_pinging_when_the_window_closes()
    {
        var ping = new FakePing();
        var (vm, _, _, _) = Build("S", ping: ping);

        await ((Caliburn.Micro.IActivate)vm).ActivateAsync();                     // 창이 열리면 ping 이 돈다
        var running = vm.IsPinging;
        await ((Caliburn.Micro.IDeactivate)vm).DeactivateAsync(close: true);

        Assert.True(running);
        Assert.False(vm.IsPinging);
    }
    #endregion
}
