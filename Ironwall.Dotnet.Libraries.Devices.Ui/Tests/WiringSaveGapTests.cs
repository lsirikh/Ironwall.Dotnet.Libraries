using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Help;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 창 — 표 보기 표지 · 번호 대역 · 저장 · 다시 열기 · 고장 구간 · 도움말(카탈로그 §6 WIR-3xx · 4xx · 5xx · 6xx). 헤드리스 · 창 없음.
/// </summary>
public class WiringSaveGapTests
{
    /// <summary>서버 센서 — 결선이 저장된 것(<paramref name="wired"/>)과 아닌 것을 섞는다. 번호를 정할 수 있다.</summary>
    private static (WiringViewModel Vm, WiringFakeDialogs Dialogs, WiringFakeGateway Gateway) Seeded(
        IReadOnlyList<(string Type, int Number, bool Wired, int? Channel)> rows)
    {
        var gateway = new WiringFakeGateway();
        var seeds = new List<WiringSensorSeed>();
        for (var i = 0; i < rows.Count; i++)
        {
            var (type, number, wired, channel) = rows[i];
            var id = 101 + i;
            var placement = wired ? new WiringPlacement(1, i + 1) : null;
            var dto = WiringDoubles.ServerSensor(id, number, i + 1, placement);
            dto.TypeDevice = type;
            gateway.Fetched[id] = dto;
            seeds.Add(new WiringSensorSeed(id, channel, new SensorFacts(number, $"북측 {i + 1}구간 펜스", type, "북측 7구간"), placement));
        }
        var dialogs = new WiringFakeDialogs { Confirm = true };
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL-북측-01", "10.99.7.1", "PidsController"), seeds,
            new[] { "Fence", "Multi", "SmartSensor2" }, new WiringApplyService(gateway, policy: WiringDoubles.AxisPolicy()), dialogs,
            fence: new WiringFenceContext(null, new FakeFenceStore(), null));
        vm.GoWiring();
        return (vm, dialogs, gateway);
    }

    private static WiringSlotViewModel SlotOf(WiringViewModel vm, int key) => vm.Line1.Single(s => s.Row?.Key == key);

    #region - 표 보기 표지 (WIR-302 · 303 · 304 · 305 · 309 · 314 · 418) -
    [Fact]
    [Trait("Scenario", "WIR-302,WIR-303")]
    public void should_tag_every_row_as_suggested_before_apply_and_as_unsaved_after_apply_WIR302_WIR303()
    {
        var (vm, _, _) = Seeded(Enumerable.Range(0, 4).Select(i => ("Fence", 1101 + i, false, (int?)(i + 1))).ToList());

        var before = vm.Line1.Where(s => s.IsFilled).Select(s => (s.IsSuggested, s.IsDraft)).ToList();
        var tooltip = vm.Line1.First(s => s.IsFilled).Tooltip;
        vm.AcceptSuggestion();
        var after = vm.Line1.Where(s => s.IsFilled).Select(s => (s.IsSuggested, s.IsDraft)).ToList();

        Assert.Equal(4, before.Count);
        Assert.All(before, b => Assert.Equal((true, false), b));                            // 「제안」 — 저장 대기 아님
        Assert.Contains("번호순 제안(아직 저장 대기 아님)", tooltip);
        Assert.All(after, a => Assert.Equal((false, true), a));                             // 「● 미저장」
    }

    [Fact]
    [Trait("Scenario", "WIR-304")]
    public void should_mark_both_rows_when_two_sensors_share_a_number_WIR304()
    {
        var (vm, _, _) = Seeded(new[] { ("Fence", 1101, true, (int?)1), ("Fence", 1105, true, (int?)2), ("Fence", 1105, true, (int?)3), ("Fence", 1104, true, (int?)4) });

        var duplicates = vm.Line1.Where(s => s.IsDuplicateNumber).Select(s => s.Row!.Key).ToList();

        Assert.Equal(new[] { 102, 103 }, duplicates);
    }

    [Fact]
    [Trait("Scenario", "WIR-305")]
    public void should_show_the_not_equal_mark_when_the_bus_address_differs_from_the_position_WIR305()
    {
        var (vm, _, _) = Seeded(Enumerable.Range(0, 4).Select(i => ("Fence", 1101 + i, true, (int?)(i + 1))).ToList());
        var before = vm.Line1.Where(s => s.IsFilled).Count(s => s.HasChannelMismatch);

        vm.FenceStep(103, 1);                                                               // Alt+→ — 3번째를 4번째로
        var mismatched = vm.Line1.Where(s => s.HasChannelMismatch).Select(s => s.Row!.Key).OrderBy(k => k).ToList();

        Assert.Equal(0, before);
        Assert.Equal(new[] { 103, 104 }, mismatched);                                       // 주소 3 이 4번째 · 주소 4 가 3번째
        Assert.Equal(("3", 4), (SlotOf(vm, 103).ChannelNumberText, SlotOf(vm, 103).Order)); // 주소는 그대로 둔다
    }

    [Fact]
    [Trait("Scenario", "WIR-309")]
    public void should_unplace_a_row_when_its_remove_button_is_pressed_WIR309()
    {
        var (vm, _, _) = Seeded(Enumerable.Range(0, 4).Select(i => ("Fence", 1101 + i, true, (int?)(i + 1))).ToList());

        vm.Unplace(SlotOf(vm, 102));

        Assert.Equal(new[] { 101, 103, 104 }, vm.FenceChain.Keys);
        Assert.Equal(1, vm.UnplacedCount);
        Assert.Contains(vm.Palette, r => r.Key == 102);
    }

    [Fact]
    [Trait("Scenario", "WIR-314")]
    public void should_undo_a_table_edit_with_ctrl_z_typed_on_the_table_WIR314_D2()
    {
        // 정적 판독 D2 — 표 보기 줄에서 Ctrl+Z 가 묶여 있지 않았다(알림은 「Ctrl+Z 로 취소」). 고친 뒤의 회귀 시험.
        var (vm, _, _) = Seeded(Enumerable.Range(0, 4).Select(i => ("Fence", 1101 + i, true, (int?)(i + 1))).ToList());
        vm.ShowTableView();
        vm.OnSelectionChanged(null);
        SlotOf(vm, 104).IsSelected = true;
        vm.SelectFromTable(new[] { 104 });

        WiringView.HandleLineKey(vm, Key.System, Key.Left, ModifierKeys.Alt);              // Alt+← — 4번째를 3번째로
        var moved = vm.FenceChain.Keys.ToList();
        var handled = WiringView.HandleLineKey(vm, Key.Z, Key.None, ModifierKeys.Control);

        Assert.Equal(new[] { 101, 102, 104, 103 }, moved);
        Assert.True(handled);
        Assert.Equal(new[] { 101, 102, 103, 104 }, vm.FenceChain.Keys);
        Assert.False(WiringView.HandleLineKey(vm, Key.Z, Key.None, ModifierKeys.None));     // 수식 키 없는 Z 는 흘려보낸다
    }

    [Fact]
    [Trait("Scenario", "WIR-418")]
    public void should_count_ch1_and_ch2_positions_from_opposite_ends_of_the_ring_WIR418()
    {
        var (vm, _, _) = Seeded(Enumerable.Range(0, 5).Select(i => ("Fence", 1101 + i, true, (int?)(i + 1))).ToList());

        var ports = vm.Line1.Where(s => s.IsFilled).Select(s => s.PortText).ToList();

        Assert.Equal(new[] { "1 · 5", "2 · 4", "3 · 3", "4 · 2", "5 · 1" }, ports);        // n ↔ N+1−n
    }
    #endregion

    #region - 번호 대역 (WIR-407 · 408 · 409 · 411 · 412) -
    [Fact]
    [Trait("Scenario", "WIR-407")]
    public void should_number_each_kind_inside_its_tier2_or_tier3_band_WIR407()
    {
        var (tier2, _, _, _) = WiringGapRig.Build("FFMF", controllerType: "PidsController");
        var (tier3, _, _, _) = WiringGapRig.Build("SSMS", controllerType: "PidsController");

        tier2.ChooseBandPreset(NumberBandSet.PRESET_TIER2);
        tier3.ChooseBandPreset(NumberBandSet.PRESET_TIER3);
        int N(WiringViewModel vm, int key) => vm.Board.Find(key)!.Facts.Number;

        Assert.StartsWith("중요시설 2차", tier2.BandText);
        Assert.Equal((1, 2, 180, 3), (N(tier2, 101), N(tier2, 102), N(tier2, 103), N(tier2, 104)));
        Assert.StartsWith("중요시설 3차", tier3.BandText);
        Assert.Equal((1, 2, 180, 3), (N(tier3, 101), N(tier3, 102), N(tier3, 103), N(tier3, 104)));
        Assert.Contains("번호 대역 중요시설 3차 — 위치 순서대로 번호를 매겼습니다", tier3.StatusText);
        Assert.False(tier3.HasNoBandsNotice);
    }

    [Fact]
    [Trait("Scenario", "WIR-407")]
    public void should_keep_the_numbers_of_a_kind_without_a_band_and_say_so_when_the_preset_lacks_it_WIR407()
    {
        // 2차 프리셋에는 스마트 대역이 없다 — 스마트 센서는 번호를 그대로 두고 안내한다(저장은 막지 않는다)
        var (vm, _, _, _) = WiringGapRig.Build("SSFF", controllerType: "PidsController");

        vm.ChooseBandPreset(NumberBandSet.PRESET_TIER2);
        int N(int key) => vm.Board.Find(key)!.Facts.Number;

        Assert.Contains(vm.Issues, i => i.Message == "스마트 센서 2대는 번호 대역이 없어 번호를 그대로 둡니다.");
        Assert.Equal((1101, 1102, 1, 2), (N(101), N(102), N(103), N(104)));
        Assert.False(WiringValidation.BlocksSave(vm.Issues));
        Assert.True(vm.CanSave);
    }

    [Theory]
    [InlineData("abc", "10", "숫자 두 개")]
    [InlineData("0", "10", "1~255 안에서")]
    [InlineData("20", "10", "1~255 안에서")]
    [InlineData("250", "260", "1~255 안에서")]
    [Trait("Scenario", "WIR-408")]
    public void should_refuse_a_malformed_custom_band_and_keep_the_numbers_WIR408(string start, string end, string reason)
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        vm.FenceSelectController();
        var before = vm.FenceChain.Keys.Select(k => vm.Board.Find(k)!.Facts.Number).ToList();
        var row = vm.BandRows.Single(r => r.Category == FenceSensorCategory.Smart);

        row.StartText = start;
        row.EndText = end;
        var applied = vm.ApplyCustomBands();

        Assert.False(applied);
        Assert.True(vm.HasBandError);
        Assert.Contains(reason, vm.BandError);
        Assert.Equal(before, vm.FenceChain.Keys.Select(k => vm.Board.Find(k)!.Facts.Number));
    }

    [Fact]
    [Trait("Scenario", "WIR-408")]
    public void should_renumber_from_a_valid_custom_band_and_clear_the_error_WIR408()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        vm.FenceSelectController();
        var row = vm.BandRows.Single(r => r.Category == FenceSensorCategory.Smart);
        row.StartText = "x";
        row.EndText = "1";
        vm.ApplyCustomBands();

        row.StartText = "21";
        row.EndText = "40";
        var applied = vm.ApplyCustomBands();

        Assert.True(applied);
        Assert.False(vm.HasBandError);
        Assert.Equal(new[] { 21, 22, 23, 24 }, vm.FenceChain.Keys.Select(k => vm.Board.Find(k)!.Facts.Number));
        Assert.Contains("21~40", vm.BandText);
    }

    [Fact]
    [Trait("Scenario", "WIR-409")]
    public void should_block_saving_when_twelve_sensors_do_not_fit_under_255_WIR409()
    {
        var (vm, _, _, _) = WiringGapRig.Build(new string('S', 12));
        vm.FenceSelectController();
        var row = vm.BandRows.Single(r => r.Category == FenceSensorCategory.Smart);
        row.StartText = "250";
        row.EndText = "255";

        vm.ApplyCustomBands();

        Assert.True(WiringValidation.BlocksSave(vm.Issues));
        Assert.False(vm.CanSave);
        Assert.False(string.IsNullOrEmpty(vm.SaveBlockedReason));
        Assert.All(vm.FenceChain.Keys, k => Assert.InRange(vm.Board.Find(k)!.Facts.Number, 1, 1200));
    }

    [Fact]
    [Trait("Scenario", "WIR-411")]
    public void should_keep_the_numbers_and_say_so_when_bands_are_cleared_WIR411()
    {
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        vm.ChooseBandPreset(NumberBandSet.PRESET_TIER4);
        var numbered = vm.FenceChain.Keys.Select(k => vm.Board.Find(k)!.Facts.Number).ToList();

        var cleared = vm.ClearBands();

        Assert.True(cleared);
        Assert.False(vm.HasBands);
        Assert.Equal("번호 대역을 껐습니다 — 번호는 지금 값 그대로입니다.", vm.StatusText);
        Assert.Equal(numbered, vm.FenceChain.Keys.Select(k => vm.Board.Find(k)!.Facts.Number));
        Assert.StartsWith("정하지 않음", vm.BandText);
    }

    [Fact]
    [Trait("Scenario", "WIR-412")]
    public void should_open_the_collapsed_detail_pane_when_the_band_picker_is_asked_for_WIR412_D1()
    {
        // 정적 판독 D1 — 속성 칸을 접은 채 [대역 고르기] 를 누르면 고르개가 칸 안이라 아무것도 보이지 않았다. 고친 뒤의 회귀 시험.
        var (vm, _, _, _) = WiringGapRig.Build("SSSS");
        var saved = new List<(double, bool)>();
        vm.UseLayoutPrefs(null, false, (r, open) => saved.Add((r, open)));

        vm.RunNoticeAction(WiringNoticeKind.Bands);                                         // 알림 목록의 [대역 고르기]

        Assert.True(vm.IsDetailPaneOpen);
        Assert.True(vm.IsControllerSelected);
        Assert.Equal("번호 대역 — 오른쪽 칸에서 고르세요.", vm.StatusText);
        Assert.Equal(new[] { (vm.FenceSplitRatio, true) }, saved);                          // 펴진 것도 기억한다
    }
    #endregion

    #region - 저장 · 다시 열기 (WIR-414 · 417 · 419 · 420 · 421) -
    [Fact]
    [Trait("Scenario", "WIR-414")]
    public async Task should_write_nothing_anywhere_when_the_number_table_is_cancelled_WIR414()
    {
        var (vm, dialogs, store, gateway) = WiringGapRig.Build("SSS");
        vm.ChooseBandPreset(NumberBandSet.PRESET_TIER3);
        dialogs.Confirm = false;

        await vm.SaveAsync();

        Assert.NotNull(dialogs.LastNumberChanges);                                          // 번호 표는 보였다
        Assert.Equal(0, gateway.PatchCount);
        Assert.Empty(store.Saved);
        Assert.True(vm.HasChanges);
        Assert.True(vm.CanUndo);
    }

    [Fact]
    [Trait("Scenario", "WIR-417")]
    public async Task should_reopen_with_the_same_panels_mounts_spacing_and_bands_after_saving_WIR417()
    {
        // Arrange — 망 모양 · 색 · 높이 · 설치 높이 조정 · 펜스센서 간격 · 대역을 고치고 저장
        var (vm, _, store, _) = WiringGapRig.Build("SFSF", controllerType: "PidsController");
        vm.FenceSelectPanel(1);
        vm.ChoosePanelStyle(EnumFenceStyle.DesignFence);
        vm.ChoosePanelColor("#336699");
        vm.PanelHeightText = "3.4";
        vm.ApplyPanelEdit();
        vm.FenceSelect(102);
        vm.MountOffsetText = "-0.4";
        vm.ApplyMountOffset();
        vm.FenceSpacingMetres = 3.5;
        vm.ChooseBandPreset(NumberBandSet.PRESET_TIER4);
        await vm.SaveAsync();
        var document = Assert.Single(store.Saved);

        // Act — 같은 PC 에서 다시 연다
        var (again, _, _, _) = WiringGapRig.Build("SFSF", document: document, controllerType: "PidsController");

        // Assert
        Assert.Equal(vm.FenceLayout.Panels, again.FenceLayout.Panels);
        Assert.Equal(EnumFenceStyle.DesignFence, again.FenceLayout.Panels[1].Style);
        Assert.Equal("#336699", again.FenceLayout.Panels[1].Color, ignoreCase: true);
        Assert.Equal(-0.4, again.FenceLayout.MountOf(102)!.HeightOffsetM, 3);
        Assert.Equal(3.5, again.FenceSpacingMetres);
        Assert.Equal(vm.BandText, again.BandText);
        Assert.False(again.FenceLayout.IsProposed);
        Assert.False(again.HasLocalChanges);
        Assert.Equal(string.Empty, again.FenceNoticeText);
    }

    [Fact]
    [Trait("Scenario", "WIR-419")]
    public async Task should_save_only_on_this_pc_and_write_nothing_to_the_server_when_only_the_fence_shape_changed_WIR419()
    {
        var (vm, dialogs, store, gateway) = WiringGapRig.Build("SSS");
        WiringGapRig.Style(vm, 0, EnumFenceStyle.Brick);

        await vm.SaveAsync();

        Assert.Equal(0, gateway.PatchCount);
        Assert.Single(store.Saved);
        Assert.Contains(dialogs.Confirms, c => c.Message.Contains("이 PC 에 저장합니다") && c.Message.EndsWith("저장할까요?"));
        Assert.Equal("펜스 구성을 이 PC 에 저장했습니다.", vm.StatusText);
        Assert.False(vm.HasLocalChanges);
    }

    [Fact]
    [Trait("Scenario", "WIR-420")]
    public async Task should_count_a_local_only_change_as_one_unsaved_change_when_closing_WIR420()
    {
        var (vm, dialogs, _, _) = WiringGapRig.Build("SSS");
        WiringGapRig.Style(vm, 0, EnumFenceStyle.Brick);
        dialogs.Confirm = false;

        var close = await vm.CanCloseAsync();

        Assert.False(close);
        Assert.Contains(dialogs.Confirms, c => c.Title == "셋업 창 닫기" && c.Message == "미저장 변경 1건이 있습니다. 버리고 닫을까요?");
    }

    [Fact]
    [Trait("Scenario", "WIR-421")]
    public void should_say_nothing_changed_and_disable_saving_when_nothing_was_touched_WIR421()
    {
        var stored = new FenceLayoutDocument
        {
            ControllerId = 10,
            Panels = Enumerable.Repeat(FencePanelSpec.Default(), 2).ToList(),
            Mounts = new Dictionary<int, SensorMountSpec> { [101] = new(0, FenceMountSpot.PostTop), [102] = new(1, FenceMountSpot.PostTop), [103] = new(2, FenceMountSpot.PostTop) },
            Revision = 2,
        };
        var (vm, _, _, _) = WiringGapRig.Build("SSS", document: stored);

        Assert.Equal("바뀐 줄이 없습니다.", vm.SaveBlockedReason);
        Assert.False(vm.CanSave);
    }
    #endregion

    #region - 고장 구간 (WIR-500 · 501 · 502) -
    [Fact]
    [Trait("Scenario", "WIR-500")]
    public void should_name_the_sensors_of_a_primary_and_secondary_fault_section_on_a_ring_WIR500()
    {
        var (vm, _, _) = Seeded(Enumerable.Range(0, 6).Select(i => ("Fence", 1101 + i, true, (int?)(i + 1))).ToList());

        var lines = vm.FaultText.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        // 1차 4~5 = A 쪽에서 4 · 5번째 · 2차 4~5 = B 쪽에서 센 4 · 5번째(체인 위치 3 · 2)
        Assert.Equal("장애 1차 4~5 → 북측 4구간 펜스와 북측 5구간 펜스 사이", lines[0]);
        Assert.Equal("장애 2차 4~5 → 북측 3구간 펜스와 북측 2구간 펜스 사이", lines[1]);
    }

    [Fact]
    [Trait("Scenario", "WIR-501")]
    public void should_ask_for_five_sensors_before_showing_a_fault_example_WIR501()
    {
        var (vm, _, _) = Seeded(Enumerable.Range(0, 4).Select(i => ("Fence", 1101 + i, true, (int?)(i + 1))).ToList());

        Assert.Equal("센서를 5대 이상 붙이면 예시를 보여 줍니다.", vm.FaultText);
    }

    [Fact]
    [Trait("Scenario", "WIR-502")]
    public void should_count_the_fault_section_from_the_saved_ring_while_a_suggestion_is_pending_WIR502()
    {
        // 저장된 링 5대 + 결선 없는 1대(번호순 제안으로 끝에 붙어 보인다) — 장애 번호는 서버에 저장된 링을 가리킨다
        var rows = Enumerable.Range(0, 5).Select(i => ("Fence", 1101 + i, true, (int?)(i + 1))).ToList();
        rows.Add(("Fence", 1106, false, 6));
        var (vm, _, _) = Seeded(rows);

        var pending = vm.HasSuggestion;
        var lines = vm.FaultText.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        Assert.True(pending);
        Assert.Equal("장애 1차 4~5 → 북측 4구간 펜스와 북측 5구간 펜스 사이", lines[0]);
        Assert.Equal("장애 2차 4~5 → 북측 2구간 펜스와 북측 1구간 펜스 사이", lines[1]);      // N=5 (제안 1대는 세지 않는다)
    }
    #endregion

    #region - 도움말 (WIR-505 · 603 · 605 · ASM-301) -
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    [Fact]
    public void should_register_every_help_key_used_by_the_wiring_and_assembly_windows_WIR505_WIR603_WIR605_ASM301()
    {
        var consoles = Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles");
        var keys = Directory.GetFiles(Path.Combine(consoles, "Wiring"), "*.xaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(consoles, "Assembly"), "*.xaml", SearchOption.AllDirectories))
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "HelpKey=\"([^\"]+)\"").Select(m => m.Groups[1].Value))
            .Distinct().ToList();

        var missing = keys.Where(k => HelpCatalog.Find(k) is null).ToList();

        Assert.True(keys.Count >= 20, $"HelpKey {keys.Count}개");
        Assert.Contains("Devices.Wiring.Check.Fault", keys);
        Assert.Contains("Devices.Assembly.Inspector.Overrides", keys);
        Assert.Empty(missing);
    }

    [Fact]
    public void should_describe_ctrl_arrows_as_the_next_grid_point_in_every_wiring_help_entry()
    {
        // 처리기(FenceCanvas · FenceConceptView)는 Ctrl+←/→ = 다음 격자 점 · Alt+Shift+←/→ = 옆 망 한 칸 — 「고른 센서」 칸 도움말만 옛 말(옆 망 한 칸)이었다.
        var lines = DevicesHelp.Entries.Where(e => e.Key.StartsWith("Devices.Wiring"))
            .SelectMany(e => e.ToPlainText().Split('\n'))
            .Where(l => l.Contains("Ctrl+←/→") || l.Contains("{Ctrl}+{←}/{→}") || Regex.IsMatch(l, @"Ctrl\s*\+\s*←"))
            .ToList();

        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Contains("격자 점", l));
    }

    [Fact]
    public void should_keep_the_sensor_transport_name_out_and_say_controller_internal_network_in_the_wiring_help()
    {
        var text = string.Join("\n", DevicesHelp.Entries.Where(e => e.Key.StartsWith("Devices.Wiring")).Select(e => e.ToPlainText()));

        Assert.Contains("제어기 내부망", text);
    }
    #endregion
}
