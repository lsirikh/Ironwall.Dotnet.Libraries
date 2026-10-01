using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 결선 펜스 편집기 검토 수정(fence-wiring-editor 검토 1~7 · 정한 의심 · V4 · V5) — 헤드리스 뷰모델 길.
/// </summary>
public class WiringFenceReviewFixTests
{
    /// <summary>경고 줄을 센다.</summary>
    private sealed class CountingLog : ILogService
    {
        public List<string> Warnings { get; } = new();
        public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
        public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Warnings.Add(msg);
        public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
#pragma warning disable CS0067
        public event EventHandler<LogEventArgs>? LogEvent;
#pragma warning restore CS0067
    }

    private sealed record Rig(WiringViewModel Vm, WiringFakeDialogs Dialogs, FakeFenceStore Store, WiringFakeGateway Gateway, CountingLog Log);

    /// <summary>스마트 S · 펜스 F 센서 101…(서버 체인 1…N · 번호 1101…).</summary>
    private static Rig Build(string types = "SSS", FenceLayoutDocument? document = null, FenceLayoutLoadResult? load = null, FakePing? ping = null)
    {
        var gateway = new WiringFakeGateway();
        var seeds = new List<WiringSensorSeed>();
        for (var i = 0; i < types.Length; i++)
        {
            var id = 101 + i;
            var type = types[i] == 'S' ? "SmartSensor2" : "Fence";
            var dto = WiringDoubles.ServerSensor(id, 1101 + i, i + 1, new WiringPlacement(1, i + 1));
            dto.TypeDevice = type;
            gateway.Fetched[id] = dto;
            seeds.Add(new WiringSensorSeed(id, i + 1, new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", type, "북측 7구간"), new WiringPlacement(1, i + 1)));
        }
        var dialogs = new WiringFakeDialogs { Confirm = true };
        var store = new FakeFenceStore { Stored = document, LoadResult = load };
        var log = new CountingLog();
        var apply = new WiringApplyService(gateway, policy: WiringDoubles.AxisPolicy());
        var key = new FenceLayoutKey("10.0.0.5:8000", 10);
        var vm = WiringViewModel.ForController(new WiringControllerInfo(10, 1, "CTRL-북측-01", "10.99.7.1", "SmartController"), seeds,
            new[] { "SmartSensor2", "Fence" }, apply, dialogs,
            fence: new WiringFenceContext(document ?? load?.Document, store, ping, key, load ?? (document is null ? FenceLayoutLoadResult.NotFound : FenceLayoutLoadResult.Loaded(document))),
            log: log);
        return new Rig(vm, dialogs, store, gateway, log);
    }

    private static void EditPanelStyle(WiringViewModel vm, int panel, EnumFenceStyle style)
    {
        vm.FenceSelectPanel(panel);
        vm.ChoosePanelStyle(style);
        vm.ApplyPanelEdit();
    }

    #region - 1. close after save -
    [Fact]
    public async Task should_ask_before_closing_when_something_was_edited_after_a_successful_save()
    {
        // Arrange — 저장으로 모두 깨끗해진 뒤
        var rig = Build();
        rig.Vm.ChooseBandPreset(NumberBandSet.PRESET_TIER3);
        await rig.Vm.SaveAsync();
        Assert.False(rig.Vm.HasChanges, $"after save: {rig.Vm.StatusText} | dirty={rig.Vm.Board.IsDirty} local={rig.Vm.HasLocalChanges}");

        // Act — 그 뒤에 고친다
        EditPanelStyle(rig.Vm, 0, EnumFenceStyle.Brick);
        rig.Dialogs.Confirm = false;
        var closed = await rig.Vm.CanCloseAsync();

        // Assert
        Assert.False(closed);
        Assert.Equal("셋업 창 닫기", rig.Dialogs.Confirms[^1].Title);
    }
    #endregion

    #region - 2. read failure vs no row -
    [Fact]
    public async Task should_offer_an_explicit_overwrite_with_the_row_revision_when_the_stored_layout_was_unreadable()
    {
        // Arrange — 행은 있는데(판 7) 본문이 손상
        var rig = Build(load: new FenceLayoutLoadResult(FenceLayoutLoadStatus.Unreadable, null, 7, "본문 손상"));
        Assert.Equal(WiringViewModel.FENCE_READ_FAILED_NOTICE, rig.Vm.FenceNoticeText);
        EditPanelStyle(rig.Vm, 0, EnumFenceStyle.Concrete);

        // Act
        await rig.Vm.SaveAsync();

        // Assert — 덮어쓰기를 따로 묻고, 판 7 로 덮어쓴다
        Assert.Contains(rig.Dialogs.Confirms, c => c.Title == "펜스 구성 덮어쓰기");
        Assert.Equal(FenceLayoutSaveMode.Overwrite, Assert.Single(rig.Store.Modes));
        Assert.Equal(7, rig.Store.Saved[0].Revision);
        Assert.False(rig.Vm.HasLocalChanges);
        Assert.NotEqual(WiringViewModel.FENCE_READ_FAILED_NOTICE, rig.Vm.FenceNoticeText);
    }

    [Fact]
    public async Task should_not_write_the_local_layout_when_the_overwrite_is_declined()
    {
        // Arrange — DB 읽기 실패(판 모름)
        var rig = Build(load: FenceLayoutLoadResult.Failed("시간 초과"));
        EditPanelStyle(rig.Vm, 0, EnumFenceStyle.Concrete);
        rig.Dialogs.AnswerFor = title => title == "펜스 구성 덮어쓰기" ? false : null;

        // Act
        await rig.Vm.SaveAsync();

        // Assert
        Assert.Empty(rig.Store.Saved);
        Assert.Contains("덮어쓰기 취소", rig.Vm.StatusText);
        Assert.True(rig.Vm.HasLocalChanges);
    }

    [Fact]
    public async Task should_save_normally_without_asking_to_overwrite_when_there_was_no_row()
    {
        var rig = Build();
        EditPanelStyle(rig.Vm, 0, EnumFenceStyle.Concrete);

        await rig.Vm.SaveAsync();

        Assert.DoesNotContain(rig.Dialogs.Confirms, c => c.Title == "펜스 구성 덮어쓰기");
        Assert.Equal(FenceLayoutSaveMode.Normal, Assert.Single(rig.Store.Modes));
        Assert.Equal(new FenceLayoutKey("10.0.0.5:8000", 10), rig.Store.Keys[^1]);
    }
    #endregion

    #region - save order -
    [Fact]
    public async Task should_skip_the_local_layout_and_say_so_when_a_server_write_fails()
    {
        // Arrange — 서버(번호)와 로컬(대역)이 함께 바뀌었는데 101 PATCH 가 실패
        var rig = Build();
        rig.Vm.ChooseBandPreset(NumberBandSet.PRESET_TIER3);
        rig.Gateway.PatchFails.Add(101);

        // Act
        await rig.Vm.SaveAsync();

        // Assert
        Assert.Empty(rig.Store.Saved);
        Assert.Contains("펜스 구성은 저장하지 않았습니다", rig.Vm.StatusText);
        Assert.True(rig.Vm.HasLocalChanges);
    }

    [Fact]
    public void should_count_a_fence_spacing_change_as_a_local_change_when_a_store_exists()
    {
        var rig = Build("SFS");

        rig.Vm.FenceSpacingMetres = 2.5;

        Assert.True(rig.Vm.HasLocalChanges);
        Assert.True(rig.Vm.CanSave);
    }
    #endregion

    #region - 4. panel move fallback -
    [Fact]
    public void should_move_the_selected_sensor_one_panel_and_to_a_typed_panel_when_buttons_or_the_number_field_are_used()
    {
        // Arrange — 스마트 3대: 기둥 0 · 1 · 2
        var rig = Build("SSSS");
        rig.Vm.FenceSelect(101);

        // Act
        var next = rig.Vm.FenceMoveSelectedToNextPanel();
        var afterNext = rig.Vm.FenceLayout.MountOf(101)!.Panel;
        rig.Vm.MountPanelText = "4";
        var typed = rig.Vm.ApplyMountPanel();
        var afterTyped = rig.Vm.FenceLayout.MountOf(101)!.Panel;
        rig.Vm.MountPanelText = "99";
        var refused = rig.Vm.ApplyMountPanel();

        // Assert
        Assert.True(next);
        Assert.Equal(1, afterNext);
        Assert.True(typed);
        Assert.Equal(3, afterTyped);                                                      // "4" = 기둥 4(0부터 3)
        Assert.Equal(new[] { 102, 103, 104, 101 }, rig.Vm.FenceChain.Keys);              // 같은 기둥이면 옮긴 센서가 뒤(끈 방향)
        Assert.False(refused);
        Assert.Contains("번호는 1~", rig.Vm.StatusText);
        Assert.Equal("망 이동", rig.Vm.MountPanelLabel);                             // 기둥 센서도 같은 말
    }
    #endregion

    #region - 5. ping summary -
    [Fact]
    public async Task should_refresh_the_ping_summary_on_every_sample_when_the_level_stays_the_same()
    {
        // Arrange
        var ping = new FakePing();
        ping.Next.Enqueue(new PingSample(true, 10));
        ping.Next.Enqueue(new PingSample(true, 30));
        var rig = Build(ping: ping);
        var raised = 0;
        rig.Vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(WiringViewModel.ControllerSignalText)) raised++; };

        // Act
        await rig.Vm.PingControllerOnceAsync();
        var first = rig.Vm.ControllerSignalText;
        await rig.Vm.PingControllerOnceAsync();
        var second = rig.Vm.ControllerSignalText;

        // Assert
        Assert.Equal(SignalLevel.Ok, rig.Vm.ControllerSignal);
        Assert.NotEqual(first, second);                                                    // 평균 10ms → 20ms
        Assert.True(raised >= 2);
    }
    #endregion

    #region - 6. menu failure -
    [Fact]
    public async Task should_show_a_status_message_and_log_one_line_when_a_menu_action_fails()
    {
        var rig = Build();
        rig.Dialogs.ThrowOnConfirm = new InvalidOperationException("창을 열 수 없음");
        var entry = rig.Vm.FenceMenu(FenceMenuTargetKind.Sensor, 101).First(e => e.AutomationId.EndsWith("ApplyAll") && e.Run is not null);
        // 한 대를 바꿔 둬야 적용할 것이 생긴다
        rig.Vm.FenceSelect(102);
        rig.Vm.ChooseMountSpot(FenceMountSpot.PostMiddle);

        await entry.Run!();

        Assert.Contains("하지 못했습니다", rig.Vm.StatusText);
        Assert.Single(rig.Log.Warnings, w => w.Contains("펜스 메뉴"));
    }
    #endregion

    #region - renumber policy -
    [Fact]
    public void should_keep_a_hand_edited_number_when_only_a_panel_color_changes_and_renumber_with_a_status_when_order_changes()
    {
        // Arrange — 대역 3차로 1 · 2 · 3, 그다음 102 를 손으로 50
        var rig = Build();
        rig.Vm.ChooseBandPreset(NumberBandSet.PRESET_TIER3);
        var row = rig.Vm.Board.Find(102)!;
        row.Facts = row.Facts with { Number = 50 };

        // Act 1 — 망 색만
        rig.Vm.FenceSelectPanel(0);
        rig.Vm.ChoosePanelColor("#4E565E");
        rig.Vm.ApplyPanelEdit();
        var afterColor = row.Facts.Number;

        // Act 2 — 순서가 바뀐다(101 을 끝 기둥으로)
        rig.Vm.FenceMoveSensors(new[] { 101 }, 101, targetMetres: 12);

        // Assert
        Assert.Equal(50, afterColor);
        Assert.Equal(1, row.Facts.Number);                                                 // 순서가 바뀌면 위치대로
        Assert.Contains("번호 ", rig.Vm.StatusText);
        Assert.Contains("대 바뀜", rig.Vm.StatusText);
    }

    [Fact]
    public void should_show_the_missing_band_notice_and_open_the_band_picker_when_no_band_is_chosen()
    {
        var rig = Build();
        Assert.True(rig.Vm.HasNoBandsNotice);
        Assert.StartsWith(WiringViewModel.NO_BANDS_NOTICE, rig.Vm.NoBandsNoticeText);

        rig.Vm.ShowBandPicker();
        var picked = rig.Vm.IsControllerSelected;
        rig.Vm.ChooseBandPreset(NumberBandSet.PRESET_TIER3);

        Assert.True(picked);
        Assert.False(rig.Vm.HasNoBandsNotice);
    }
    #endregion
}
