using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 창 시나리오 3회차(2026-10-11 · 카탈로그 §6.1 · 6.4 · 6.5 WIR-004 ~ 012 · 106 · 163 · 300 ~ 311 · 400 ~ 416) — 상태표의 '시험 없음' 줄을
/// 뷰모델 길로 단언한다. 시드는 카탈로그와 같은 모양: 펜스 시드 제어기 <c>LRT-UI-FCTRL-1</c>(127.0.0.1) · 스마트 복합센서2 12대.
/// 서버는 <see cref="WiringFakeGateway"/>(보낸 DTO 를 그대로 붙잡는다) · 로컬 펜스 저장소는 <see cref="FakeFenceStore"/>. 창 없음.
/// </summary>
public class WiringScenarioRound3Tests
{
    #region - Rig -
    private const string SeedName = "LRT-UI-FCTRL-1";
    private const string SeedAddress = "127.0.0.1";

    private sealed record Rig(WiringViewModel Vm, WiringFakeDialogs Dialogs, FakeFenceStore Store, WiringFakeGateway Gateway, List<WiringSensorSeed> Seeds);

    /// <summary>
    /// 스마트 복합센서2(IP) <paramref name="count"/> 대 · 번호 1101…. <paramref name="wired"/> 면 서버에 체인 1…N 이 저장돼 있다(없으면 결선 없는 시드).
    /// <paramref name="placements"/> 로 서버 자리(방향 포함)를 직접 줄 수 있다 — 다시 열기 시험.
    /// </summary>
    private static Rig Build(int count = 12, bool wired = false, FenceLayoutDocument? document = null,
                             IReadOnlyDictionary<int, WiringPlacement>? placements = null, IReadOnlyDictionary<int, int>? numbers = null)
    {
        var gateway = new WiringFakeGateway();
        var seeds = new List<WiringSensorSeed>();
        for (var i = 0; i < count; i++)
        {
            var id = 101 + i;
            var number = numbers is not null && numbers.TryGetValue(id, out var n) ? n : 1101 + i;
            var placement = placements is not null && placements.TryGetValue(id, out var p) ? p : wired ? new WiringPlacement(1, i + 1) : null;
            var dto = WiringDoubles.ServerSensor(id, number, i + 1, placement);
            dto.TypeDevice = "SmartSensor2";
            gateway.Fetched[id] = dto;
            seeds.Add(new WiringSensorSeed(id, null, new SensorFacts(number, $"북측 {i + 1}구간 펜스", "SmartSensor2", "북측 7구간"), placement,
                ConnectionType: "IP_DIRECT", IpAddress: $"192.168.10.{i + 1}"));
        }
        var dialogs = new WiringFakeDialogs { Confirm = true };
        var store = new FakeFenceStore { Stored = document };
        var apply = new WiringApplyService(gateway, policy: WiringDoubles.AxisPolicy());
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, SeedName, SeedAddress, "SmartController"), seeds,
            new[] { "SmartSensor2", "Fence" }, apply, dialogs, fence: new WiringFenceContext(document, store, null));
        return new Rig(vm, dialogs, store, gateway, seeds);
    }

    private static int DraftCount(WiringViewModel vm) => int.Parse(vm.DraftText.Split(' ')[2].Trim(), System.Globalization.CultureInfo.InvariantCulture);

    private static JObject WiringOf(SensorDeviceDto dto) => (JObject)dto.HardwareSpec!.Spec!["wiring"]!;

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static string WiringXaml() => File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Wiring", "WiringView.xaml"));
    #endregion

    #region - 열기 · 틀 · 닫기 (WIR-004 · 005 · 006 · 009 · 010 · 011 · 012) -
    [Fact]
    [Trait("Scenario", "WIR-004")]
    public void should_title_the_window_setup_wiring_map_and_name_the_controller_as_the_subject_WIR004()
    {
        var rig = Build();

        Assert.Equal("장비 셋업 · 결선맵", rig.Vm.DisplayName);                              // 창 제목(커널 창 머리)
        Assert.Equal($"{SeedName} · {SeedAddress}", rig.Vm.Subject);                         // `Devices.Wiring.Subject` = 제어기 이름 · 주소
        Assert.Matches("AutomationProperties.AutomationId=\"Devices.Wiring.Subject\"[^>]*Text=\"\\{Binding Subject\\}\"|Text=\"\\{Binding Subject\\}\"[^>]*AutomationProperties.AutomationId=\"Devices.Wiring.Subject\"",
            WiringXaml().Replace("\r", " ").Replace("\n", " "));
    }

    [Fact]
    [Trait("Scenario", "WIR-005,WIR-006")]
    public void should_open_a_seed_without_wiring_in_the_fence_view_suggesting_all_twelve_by_number_with_no_changed_rows_WIR005_WIR006()
    {
        var rig = Build(12, wired: false);
        rig.Vm.GoWiring();

        Assert.True(rig.Vm.IsFenceView);                                                     // 펜스 보기 기본(`Devices.Wiring.Fence.Canvas`)
        Assert.False(rig.Vm.IsTableView);
        Assert.Equal("저장된 배치가 없는 센서 12대를 번호순으로 제안했습니다", rig.Vm.SuggestionText);   // `Devices.Wiring.SuggestionNotice`
        Assert.True(rig.Vm.HasSuggestion);
        Assert.Equal(Enumerable.Range(101, 12), rig.Vm.FenceChain.Keys);                     // 칩 12 — 번호순
        Assert.Equal(12, rig.Vm.FenceSensors().Count);
        Assert.Equal("바뀐 줄 0", rig.Vm.DraftText);                                         // `.DraftCount` — 제안은 저장 대상이 아니다
        Assert.False(rig.Vm.HasChanges);
    }

    [Fact]
    [Trait("Scenario", "WIR-009")]
    public void should_move_the_step_strip_between_the_sensor_and_wiring_steps_WIR009()
    {
        var rig = Build(4, wired: true);
        var vm = rig.Vm;
        var sensors = (vm.IsSensorStep, vm.IsWiringStep, vm.StepControllerGlyph, vm.StepSensorsGlyph, vm.StepWiringGlyph);

        vm.GoWiring();                                                                       // `Devices.Wiring.Step.Wiring`
        var wiring = (vm.IsSensorStep, vm.IsWiringStep, vm.StepSensorsGlyph, vm.StepWiringGlyph, vm.IsSensorStepDone);
        vm.GoSensors();                                                                      // `Devices.Wiring.Step.Sensors`

        Assert.Equal((true, false, "✓", "▸", "✓"), sensors);                                 // 결선이 다 붙어 있으면 ③ 은 ✓
        Assert.Equal((false, true, "✓", "▸", true), wiring);
        Assert.True(vm.IsSensorStep);
        var xaml = WiringXaml();
        foreach (var step in new[] { "Controller", "Sensors", "Wiring", "Confirm" })
            Assert.Contains($"Devices.Wiring.Step.{step}", xaml);
    }

    [Fact]
    [Trait("Scenario", "WIR-010")]
    public void should_show_exactly_one_of_fence_view_and_table_view_WIR010()
    {
        var vm = Build(4, wired: true).Vm;
        vm.GoWiring();
        var states = new List<(bool Fence, bool Table)> { (vm.IsFenceView, vm.IsTableView) };

        vm.ShowTableView();                                                                  // `.View.Table`
        states.Add((vm.IsFenceView, vm.IsTableView));
        vm.ShowFenceView();                                                                  // `.View.Fence`
        states.Add((vm.IsFenceView, vm.IsTableView));

        Assert.Equal(new[] { (true, false), (false, true), (true, false) }, states);
        Assert.All(states, s => Assert.NotEqual(s.Fence, s.Table));                          // 하나만 보임
    }

    [Fact]
    [Trait("Scenario", "WIR-011")]
    public async Task should_count_the_unsaved_changes_and_stay_open_when_closing_is_declined_WIR011()
    {
        var rig = Build(6, wired: true);
        var vm = rig.Vm;
        vm.FenceStep(103, 1);                                                                // 바뀐 줄 2(3 ↔ 4)
        vm.FenceSelectPanel(0);
        vm.ChoosePanelStyle(EnumFenceStyle.Brick);
        vm.ApplyPanelEdit();                                                                 // + 이 PC 의 펜스 구성 1
        rig.Dialogs.Confirm = false;                                                         // ✕ → [아니오]

        var close = await vm.CanCloseAsync();

        Assert.False(close);                                                                 // 유지
        Assert.Equal(("셋업 창 닫기", "미저장 변경 3건이 있습니다. 버리고 닫을까요?"), rig.Dialogs.Confirms.Last());
        Assert.True(vm.HasChanges);
        Assert.Equal(0, rig.Gateway.PatchCount);
    }

    [Fact]
    [Trait("Scenario", "WIR-012")]
    public async Task should_close_without_asking_after_everything_was_saved_WIR012()
    {
        var rig = Build(6, wired: true);
        rig.Vm.FenceStep(103, 1);
        await rig.Vm.SaveAsync();
        var asked = rig.Dialogs.ConfirmCount;

        var close = await rig.Vm.CanCloseAsync();

        Assert.True(close);                                                                  // 묻지 않고 닫힘
        Assert.Equal(asked, rig.Dialogs.ConfirmCount);
        Assert.True(rig.Vm.HasSaved);                                                        // 콘솔은 이 값으로 다시 읽고 「…저장했습니다」 를 띄운다
        Assert.Equal(2, rig.Gateway.PatchCount);
    }
    #endregion

    #region - 망 높이 · 사슬 포트 (WIR-106 · 163) -
    [Theory]
    [InlineData("2.4", true)]
    [InlineData("7", false)]
    [Trait("Scenario", "WIR-106")]
    public void should_apply_a_height_of_2_4_and_refuse_7_with_the_apply_button_off_WIR106(string height, bool ok)
    {
        var vm = Build(6, wired: true).Vm;
        vm.FenceSelectPanel(1);
        vm.PanelHeightText = "3";                                                            // 기본(철조망 2.4m)에서 먼저 바꿔 둔다 — 2.4 가 실제 변경이 되게
        Assert.True(vm.ApplyPanelEdit());
        vm.FenceSelectPanel(1);
        var total = vm.PanelTotalText;

        vm.PanelHeightText = height;                                                         // `.PanelPane.Height`
        var state = (vm.HasPanelEditError, vm.CanApplyPanelEdit, vm.PanelEditError);
        var applied = vm.ApplyPanelEdit();                                                   // `.PanelPane.Apply`

        Assert.Equal(!ok, state.HasPanelEditError);
        Assert.Equal(ok, state.CanApplyPanelEdit);
        Assert.Equal(ok, applied);
        Assert.StartsWith($"망 {vm.FenceLayout.Panels.Count}칸 · 길이 ", vm.PanelTotalText); // `.PanelPane.Total`(합계)
        Assert.Equal(total, vm.PanelTotalText);                                              // 높이는 길이 합계를 바꾸지 않는다
        if (ok) Assert.Equal(2.4, vm.FenceLayout.Panels[1].HeightM, 3);
        else
        {
            Assert.Matches(@"^높이는 [\d.]+~[\d.]+m 로 적어 주세요\.$", state.PanelEditError!);   // `.PanelPane.Error`
            Assert.Equal(3.0, vm.FenceLayout.Panels[1].HeightM, 3);                         // 거부되면 그대로
        }
    }

    [Fact]
    [Trait("Scenario", "WIR-163")]
    public void should_change_the_port_of_the_third_sensor_from_3_10_to_4_9_with_alt_right_WIR163()
    {
        var vm = Build(12, wired: true).Vm;
        vm.FenceSelect(103);
        var before = vm.SelectedPortText;

        var moved = vm.FenceStep(103, 1);                                                    // Alt+→
        vm.FenceSelect(103);

        Assert.True(moved);
        Assert.StartsWith("3 · 10 — ", before);                                              // `.Pane.Port`
        Assert.StartsWith("4 · 9 — ", vm.SelectedPortText);
        Assert.Equal(4, vm.FenceChain.Keys.ToList().IndexOf(103) + 1);
    }
    #endregion

    #region - 표 보기 · 팔레트 (WIR-300 · 307 · 308 · 311) -
    [Fact]
    [Trait("Scenario", "WIR-300")]
    public void should_head_the_table_with_the_controller_name_address_and_a_chain_summary_WIR300()
    {
        var vm = Build(12, wired: true).Vm;
        vm.GoWiring();
        vm.ShowTableView();

        Assert.Equal(SeedName, vm.Controller.Name);                                          // `.Table.ControllerName`
        Assert.Equal(SeedAddress, vm.Controller.Address);
        Assert.StartsWith("체인 12대 · 길이 약 ", vm.ChainSummaryText);                      // `.Table.ChainSummary`
        Assert.Contains(" · 함체 자리 ", vm.ChainSummaryText);
        Assert.Contains($"{vm.EnclosureGapText}", vm.ChainSummaryText);
        var xaml = WiringXaml();
        Assert.Contains("Devices.Wiring.Table.ControllerName", xaml);
        Assert.Contains("Devices.Wiring.Table.ChainSummary", xaml);
    }

    [Fact]
    [Trait("Scenario", "WIR-307")]
    public void should_attach_an_unplaced_chip_at_the_chain_end_and_say_so_when_enter_is_pressed_in_the_palette_WIR307()
    {
        var vm = Build(5, wired: true).Vm;
        vm.GoWiring();
        vm.FenceUnplace(new[] { 102 });
        var chip = vm.Palette.Single(r => r.Key == 102);

        vm.PlaceFromPalette(chip);                                                           // 팔레트 Enter

        Assert.EndsWith("에 붙였습니다", vm.StatusText);
        Assert.StartsWith($"{chip.Display} → ", vm.StatusText);
        Assert.Equal(102, vm.FenceChain.Keys.Last());                                        // 체인 끝
        Assert.Equal(0, vm.UnplacedCount);
    }

    [Fact]
    [Trait("Scenario", "WIR-308")]
    public void should_insert_the_fourth_sensor_into_the_second_place_with_two_alt_left_and_restore_it_with_undo_WIR308()
    {
        var vm = Build(5, wired: true).Vm;
        vm.GoWiring();
        vm.ShowTableView();
        var loop = vm.LoopText;

        vm.FenceStep(104, -1);                                                               // Alt+← ×2
        vm.FenceStep(104, -1);
        var moved = (Keys: vm.FenceChain.Keys.ToList(), Slots: vm.Line1.Where(s => s.IsFilled).Take(3).Select(s => s.Row!.Key).ToList(), vm.LoopText);
        vm.Undo();
        vm.Undo();

        Assert.Equal(new[] { 101, 104, 102, 103, 105 }, moved.Keys);                         // 2번 자리
        Assert.Equal(new[] { 101, 104, 102 }, moved.Slots);                                  // `.Slot.1-3`
        Assert.NotEqual(loop, moved.LoopText);
        Assert.Equal(new[] { 101, 102, 103, 104, 105 }, vm.FenceChain.Keys);                 // 되돌리기 복원
        Assert.Equal(loop, vm.LoopText);
    }

    [Fact]
    [Trait("Scenario", "WIR-311")]
    public void should_lay_every_sensor_out_by_number_in_one_line_and_leave_nothing_unplaced_WIR311()
    {
        var numbers = new Dictionary<int, int> { [101] = 1104, [102] = 1102, [103] = 1101, [104] = 1103 };
        var vm = Build(4, wired: false, numbers: numbers).Vm;
        vm.GoWiring();
        vm.FenceUnplace(new[] { 101, 104 });

        vm.AutoLayout();                                                                     // `.AutoLayout`

        Assert.Equal("번호 순으로 한 줄에 배치했습니다(번호가 같으면 id 순).", vm.StatusText);
        Assert.Equal(new[] { 103, 102, 104, 101 }, vm.FenceChain.Keys);                      // 1101 · 1102 · 1103 · 1104
        Assert.Equal(0, vm.UnplacedCount);                                                   // `.Counts` 미배치 0
        Assert.Contains("미배치 0", vm.FenceCountsText);
    }
    #endregion

    #region - 저장 (WIR-400 · 401 · 402 · 403 · 404 · 405) -
    [Fact]
    [Trait("Scenario", "WIR-400")]
    public void should_turn_all_twelve_suggestions_into_changed_rows_when_the_suggestion_is_accepted_WIR400()
    {
        var vm = Build(12, wired: false).Vm;
        var before = DraftCount(vm);

        vm.AcceptSuggestion();                                                               // `.AcceptSuggestion`

        Assert.Equal(0, before);
        Assert.Equal(12, DraftCount(vm));                                                    // 바뀐 줄 12
        Assert.False(vm.HasSuggestion);
        Assert.Equal("제안을 적용했습니다.", vm.StatusText);
    }

    [Fact]
    [Trait("Scenario", "WIR-401,WIR-404")]
    public async Task should_patch_twelve_rows_with_wiring_v3_on_line_1_in_chain_order_facing_front_and_leave_no_changes_WIR401_WIR404()
    {
        var rig = Build(12, wired: false);
        rig.Vm.AcceptSuggestion();

        await rig.Vm.SaveAsync();                                                            // 저장 → [예]

        Assert.Equal(12, rig.Gateway.PatchCount);
        Assert.Contains(rig.Dialogs.Confirms, c => c.Title == "결선 저장");
        for (var i = 0; i < 12; i++)
        {
            var wiring = WiringOf(rig.Gateway.Patched.Single(p => p.Id == 101 + i).Dto);
            Assert.Equal(3, (int)wiring["v"]!);                                              // wiring v3
            Assert.Equal(1, (int)wiring["line"]!);                                           // line 1
            Assert.Equal(i + 1, (int)wiring["order"]!);                                      // 순번
            Assert.Equal("front", (string?)wiring["facing"]);                                // 면
        }
        Assert.False(rig.Vm.CanUndo);                                                        // WIR-404 — 되돌리기 꺼짐 · 바뀐 줄 0
        Assert.Equal(0, DraftCount(rig.Vm));
        Assert.False(rig.Vm.HasChanges);
    }

    [Fact]
    [Trait("Scenario", "WIR-402")]
    public async Task should_send_nothing_when_saving_is_answered_no_WIR402()
    {
        var rig = Build(12, wired: false);
        rig.Vm.AcceptSuggestion();
        rig.Dialogs.Confirm = false;                                                         // [아니오]

        await rig.Vm.SaveAsync();

        Assert.Equal(1, rig.Dialogs.ConfirmCount);                                           // 물었고
        Assert.Equal(0, rig.Gateway.PatchCount);                                             // 안 보냄
        Assert.Equal(0, rig.Gateway.CreateCount);
        Assert.Empty(rig.Store.Saved);
        Assert.Equal(12, DraftCount(rig.Vm));
        Assert.True(rig.Vm.CanUndo);
    }

    [Fact]
    [Trait("Scenario", "WIR-403")]
    public async Task should_create_four_new_sensors_with_their_wiring_when_saved_WIR403()
    {
        var rig = Build(2, wired: true);
        var vm = rig.Vm;
        rig.Dialogs.MakeResult = new MakeSensorsResult(new SensorBulkCreateSpec(4, 1201, 1, "북측 {번호}구간 펜스", "SmartSensor2", "북측 8구간"), SkipConflicts: false);
        await vm.MakeSensorsAsync();                                                         // 센서 4대 만들기
        vm.GoWiring();
        foreach (var row in vm.Palette.ToList()) vm.PlaceFromPalette(row);                   // 결선 끝에 붙인다

        await vm.SaveAsync();

        Assert.Equal(4, rig.Gateway.CreateCount);                                            // 4대 생성
        Assert.Equal(new[] { 1201, 1202, 1203, 1204 }, rig.Gateway.Created.Select(c => c.NumberDevice));
        Assert.All(rig.Gateway.Created, c => Assert.Equal(10, c.ControllerId));
        Assert.Equal(new[] { 3, 4, 5, 6 }, rig.Gateway.Created.Select(c => (int)WiringOf(c)["order"]!));   // wiring 이 같이 실린다
        Assert.All(rig.Gateway.Created, c => Assert.Equal(1, (int)WiringOf(c)["line"]!));
        Assert.Equal(0, rig.Gateway.PatchCount);                                             // 기존 두 대는 그대로
        Assert.False(vm.HasChanges);
    }

    [Fact]
    [Trait("Scenario", "WIR-405")]
    public async Task should_refuse_to_save_and_keep_the_api_value_when_another_user_renamed_the_sensor_meanwhile_WIR405()
    {
        var rig = Build(4, wired: true);
        var vm = rig.Vm;
        rig.Gateway.Fetched[102].NameDevice = "다른 사람이 바꾼 이름";                       // API 이름 변경(창을 연 뒤)
        vm.OnSelectionChanged(new[] { vm.Rows.Single(r => r.Key == 102) });                  // UI 편집
        vm.EditName = "내가 고친 이름";
        vm.ApplyEdit();

        await vm.SaveAsync();

        Assert.StartsWith("다른 사용자가 ", vm.StatusText);                                  // 「다른 사용자가…」
        Assert.Contains("이름을 바꿨습니다", vm.StatusText);
        Assert.Contains("저장하지 않았습니다", vm.StatusText);
        Assert.Equal(0, rig.Gateway.PatchCount);
        Assert.Equal("다른 사람이 바꾼 이름", rig.Gateway.Fetched[102].NameDevice);          // API 값 유지
        Assert.True(vm.HasChanges);                                                          // 내 편집은 Draft 로 남는다
    }
    #endregion

    #region - 번호 대역 · 번호 저장 (WIR-406 · 413 · 415) -
    [Fact]
    [Trait("Scenario", "WIR-406")]
    public void should_preview_chip_numbers_one_to_twelve_with_the_tier4_band_WIR406()
    {
        var vm = Build(12, wired: true).Vm;

        var chose = vm.ChooseBandPreset(NumberBandSet.PRESET_TIER4);                         // `.Band.Choose` → `.Band.Tier4`

        Assert.True(chose);
        Assert.Equal(Enumerable.Range(1, 12), vm.FenceChain.Keys.Select(k => vm.Board.Find(k)!.Facts.Number));   // 칩 번호 1…12
        Assert.StartsWith("중요시설 4차", vm.BandText);                                     // `.Band.Text`
        Assert.True(vm.HasChanges);                                                          // 미리보기 = Draft(저장 전)
    }

    [Fact]
    [Trait("Scenario", "WIR-413,WIR-415")]
    public async Task should_show_the_number_change_table_with_heading_warning_and_rows_then_write_the_previewed_numbers_and_wiring_v3_WIR413_WIR415()
    {
        var rig = Build(12, wired: false);
        rig.Vm.AcceptSuggestion();
        rig.Vm.ChooseBandPreset(NumberBandSet.PRESET_TIER4);
        var preview = rig.Vm.FenceChain.Keys.ToDictionary(k => k, k => rig.Vm.Board.Find(k)!.Facts.Number);

        await rig.Vm.SaveAsync();                                                            // [구성 저장] → 번호 바뀜 표 → `.NumberChanges.Confirm`

        // WIR-413 — 머리 · 경고 · 줄
        var changes = rig.Dialogs.LastNumberChanges!;
        var table = new WiringNumberChangesViewModel("구성 저장", changes, rig.Dialogs.LastNumberWarning!, rig.Dialogs.Confirms.Last().Message);
        Assert.Equal("바뀌는 번호 12대", table.Heading);
        Assert.Equal(WiringViewModel.NUMBER_WARNING, table.Warning);
        Assert.Equal("북측 1구간 펜스 1101 → 1", changes[0].Text);
        Assert.Equal("북측 12구간 펜스 1112 → 12", changes[^1].Text);
        Assert.Contains(rig.Dialogs.Confirms, c => c.Title == "구성 저장");
        // WIR-415 — number_device = 미리보기 · wiring v3
        Assert.Equal(12, rig.Gateway.PatchCount);
        foreach (var (id, dto) in rig.Gateway.Patched)
        {
            Assert.Equal(preview[id], dto.NumberDevice);
            Assert.Equal(3, (int)WiringOf(dto)["v"]!);
        }
        Assert.Equal(NumberBandSet.Tier4, Assert.Single(rig.Store.Saved).Bands);
    }
    #endregion

    #region - 다시 열기 방향 (WIR-416) -
    [Fact]
    [Trait("Scenario", "WIR-416")]
    public async Task should_reopen_with_the_turned_and_flipped_sensor_kept_and_write_back_and_yaw_to_the_api_WIR416()
    {
        var rig = Build(4, wired: true);
        var vm = rig.Vm;
        vm.FenceSelect(102);
        vm.FenceRotateSelected(1);                                                           // R
        Assert.True(vm.FenceFlipFacing(102));                                                // F
        var yaw = vm.Board.YawOf(102);

        await vm.SaveAsync();
        var sent = rig.Gateway.Patched.Single(p => p.Id == 102).Dto;
        var wiring = WiringOf(sent);
        var document = Assert.Single(rig.Store.Saved);

        // 다시 열기 — 서버가 저장한 자리(spec.wiring)와 이 PC 의 펜스 구성으로
        var placements = rig.Gateway.Fetched.Keys.ToDictionary(id => id,
            id => rig.Gateway.Patched.Where(p => p.Id == id).Select(p => WiringSpec.Read(p.Dto.HardwareSpec?.Spec)).LastOrDefault()
                  ?? WiringSpec.Read(rig.Gateway.Fetched[id].HardwareSpec!.Spec)!);
        var again = Build(4, wired: true, document: document, placements: placements).Vm;

        Assert.Equal("back", (string?)wiring["facing"]);                                     // API back
        Assert.Equal((int)yaw, (int)wiring["yaw"]!);                                         // API yaw
        Assert.NotEqual(WiringYaw.Away, yaw);
        Assert.Equal(WiringFacing.Back, again.Board.FacingOf(102));                          // 면 유지
        Assert.Equal(yaw, again.Board.YawOf(102));                                           // 방향 유지
        Assert.Equal(WiringFacing.Front, again.Board.FacingOf(101));
        Assert.False(again.HasChanges);
    }
    #endregion
}
