using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Door;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// 심볼 상세 보기 창 규칙 — PRD symbol-detail-and-door-control FR-18~21.
/// <para>핵심 계약 둘: ① <b>심볼 탭은 장비 미연결이어도 활성</b>(심볼 정보는 장비와 무관) ②
/// 액션의 비활성 조건이 <b>기존 컨텍스트 메뉴와 동일</b>(두 벌 규칙 방지).</para>
/// </summary>
public class SymbolDetailRulesTests
{
    private static SymbolDetailContext Ctx(
        EnumDeviceType type,
        bool hasDevice = true,
        bool web = true,
        bool endpoint = true,
        bool ptz = false,
        bool devCtl = true,
        bool broadcast = true,
        bool events = true,
        bool mic = false,
        DoorUiState door = DoorUiState.Closed)
        => new(type, hasDevice, web, endpoint, ptz, devCtl, broadcast, events, mic, door);

    // ── 탭 ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(EnumDeviceType.Controller, true)]
    [InlineData(EnumDeviceType.IpCamera, true)]
    [InlineData(EnumDeviceType.IpSpeaker, true)]
    [InlineData(EnumDeviceType.Lamp, true)]
    [InlineData(EnumDeviceType.Fence, false)]
    [InlineData(EnumDeviceType.Gate, false)]
    [InlineData(EnumDeviceType.Enclosure, false)]
    public void should_show_comm_tab_only_for_networked_types(EnumDeviceType type, bool expected)
        => Assert.False(SymbolDetailRules.VisibleTabs(type).Contains(SymbolDetailTab.Comm),
            $"유형별 탭은 현재 내려둔 상태다(사용자 결정 2026-09-09) — {type} 도 통신 탭이 없어야 한다. expected={expected}");

    [Theory]
    [InlineData(EnumDeviceType.Fence)]
    [InlineData(EnumDeviceType.Gate)]
    [InlineData(EnumDeviceType.IpCamera)]
    public void should_always_show_symbol_tab(EnumDeviceType type)
        => Assert.Contains(SymbolDetailTab.Symbol, SymbolDetailRules.VisibleTabs(type));

    [Fact]
    public void should_enable_only_symbol_tab_when_device_is_not_linked()
    {
        foreach (var tab in SymbolDetailRules.VisibleTabs(EnumDeviceType.IpCamera))
        {
            var enabled = SymbolDetailRules.IsTabEnabled(tab, hasDevice: false);
            Assert.Equal(tab == SymbolDetailTab.Symbol, enabled);
        }
    }

    [Fact]
    public void should_enable_every_tab_when_device_is_linked()
    {
        foreach (var tab in SymbolDetailRules.VisibleTabs(EnumDeviceType.IpCamera))
            Assert.True(SymbolDetailRules.IsTabEnabled(tab, hasDevice: true));
    }

    [Theory]
    [InlineData(EnumDeviceType.IpSpeaker)]
    [InlineData(EnumDeviceType.IpCamera)]
    [InlineData(EnumDeviceType.Gate)]
    [InlineData(EnumDeviceType.SmartSensor)]
    [InlineData(EnumDeviceType.Enclosure)]
    [InlineData(EnumDeviceType.Controller)]
    public void should_expose_only_basic_and_symbol_tabs(EnumDeviceType type)
    {
        // 유형별 탭(통신·방송·상태·최근 이벤트)은 내려둔 상태다(사용자 결정 2026-09-09).
        // 기준은 앱 '센서 설정 패널' 컬럼 — 현장에서 안 채워지는 필드를 늘어놓지 않는다.
        Assert.Equal(new[] { SymbolDetailTab.Basic, SymbolDetailTab.Symbol },
            SymbolDetailRules.VisibleTabs(type));
    }

    [Theory]
    [InlineData(true, SymbolDetailTab.Basic)]
    [InlineData(false, SymbolDetailTab.Symbol)]
    public void should_pick_default_tab_by_link_state(bool hasDevice, SymbolDetailTab expected)
        => Assert.Equal(expected, SymbolDetailRules.DefaultTab(hasDevice));

    // ── 액션 노출 ─────────────────────────────────────────────────────

    [Fact]
    public void should_offer_detection_history_only_for_sensors()
    {
        Assert.Contains(SymbolDetailAction.DetectionHistory, SymbolDetailRules.VisibleActions(EnumDeviceType.Fence));
        Assert.Contains(SymbolDetailAction.DetectionHistory, SymbolDetailRules.VisibleActions(EnumDeviceType.SmartSensor));
        Assert.DoesNotContain(SymbolDetailAction.DetectionHistory, SymbolDetailRules.VisibleActions(EnumDeviceType.IpCamera));
        Assert.DoesNotContain(SymbolDetailAction.DetectionHistory, SymbolDetailRules.VisibleActions(EnumDeviceType.Gate));
    }

    [Fact]
    public void should_offer_door_buttons_only_for_gate_and_enclosure()
    {
        foreach (var t in new[] { EnumDeviceType.Gate, EnumDeviceType.Enclosure })
        {
            Assert.Contains(SymbolDetailAction.DoorOpen, SymbolDetailRules.VisibleActions(t));
            Assert.Contains(SymbolDetailAction.DoorClose, SymbolDetailRules.VisibleActions(t));
        }
        Assert.DoesNotContain(SymbolDetailAction.DoorOpen, SymbolDetailRules.VisibleActions(EnumDeviceType.IpSpeaker));
    }

    [Fact]
    public void should_offer_broadcast_actions_only_for_speaker()
    {
        var speaker = SymbolDetailRules.VisibleActions(EnumDeviceType.IpSpeaker);
        Assert.Contains(SymbolDetailAction.SoundPlay, speaker);
        Assert.Contains(SymbolDetailAction.Tts, speaker);
        Assert.Contains(SymbolDetailAction.BroadcastStop, speaker);
        Assert.Contains(SymbolDetailAction.MicPtt, speaker);
        Assert.DoesNotContain(SymbolDetailAction.MicPtt, SymbolDetailRules.VisibleActions(EnumDeviceType.Lamp));
    }

    [Fact]
    public void should_always_offer_show_on_map()
    {
        foreach (var t in new[] { EnumDeviceType.Gate, EnumDeviceType.Lamp, EnumDeviceType.Fence, EnumDeviceType.IpCamera })
            Assert.Contains(SymbolDetailAction.ShowOnMap, SymbolDetailRules.VisibleActions(t));
    }

    // ── 액션 활성 ─────────────────────────────────────────────────────

    [Fact]
    public void should_keep_show_on_map_enabled_even_without_device_or_permission()
    {
        var ctx = Ctx(EnumDeviceType.Gate, hasDevice: false, web: false, devCtl: false, broadcast: false, events: false);
        var s = SymbolDetailRules.Evaluate(SymbolDetailAction.ShowOnMap, ctx);
        Assert.True(s.IsEnabled);
        Assert.Null(s.DisabledReason);
    }

    [Fact]
    public void should_disable_device_actions_when_not_linked()
    {
        var ctx = Ctx(EnumDeviceType.IpCamera, hasDevice: false);
        foreach (var a in new[] { SymbolDetailAction.DeviceDetail, SymbolDetailAction.DeviceEdit, SymbolDetailAction.CameraHome })
        {
            var s = SymbolDetailRules.Evaluate(a, ctx);
            Assert.False(s.IsEnabled);
            Assert.Equal("장비 미연결", s.DisabledReason);
        }
        // 목록 페이지는 장비가 없어도 열 수 있다(메뉴와 동일)
        Assert.True(SymbolDetailRules.Evaluate(SymbolDetailAction.DevicePage, ctx).IsEnabled);
    }

    [Fact]
    public void should_disable_web_actions_when_web_server_off()
    {
        var ctx = Ctx(EnumDeviceType.Fence, web: false);
        foreach (var a in new[] { SymbolDetailAction.DevicePage, SymbolDetailAction.DeviceDetail, SymbolDetailAction.DeviceEdit })
            Assert.Equal("웹서버 연동 꺼짐", SymbolDetailRules.Evaluate(a, ctx).DisabledReason);
    }

    [Fact]
    public void should_gate_detection_history_by_events_permission()
    {
        Assert.True(SymbolDetailRules.Evaluate(SymbolDetailAction.DetectionHistory, Ctx(EnumDeviceType.Fence)).IsEnabled);
        var denied = SymbolDetailRules.Evaluate(SymbolDetailAction.DetectionHistory, Ctx(EnumDeviceType.Fence, events: false));
        Assert.False(denied.IsEnabled);
        Assert.Equal("권한 없음(events:view)", denied.DisabledReason);
    }

    [Fact]
    public void should_gate_home_pages_by_endpoint()
    {
        var noEndpoint = Ctx(EnumDeviceType.Controller, endpoint: false);
        Assert.Equal("IP·포트 없음", SymbolDetailRules.Evaluate(SymbolDetailAction.ControllerHome, noEndpoint).DisabledReason);
        Assert.True(SymbolDetailRules.Evaluate(SymbolDetailAction.ControllerHome, Ctx(EnumDeviceType.Controller)).IsEnabled);
    }

    [Fact]
    public void should_gate_aim_location_by_ptz()
    {
        Assert.Equal("PTZ 카메라 아님", SymbolDetailRules.Evaluate(SymbolDetailAction.AimLocation, Ctx(EnumDeviceType.IpCamera)).DisabledReason);
        Assert.True(SymbolDetailRules.Evaluate(SymbolDetailAction.AimLocation, Ctx(EnumDeviceType.IpCamera, ptz: true)).IsEnabled);
    }

    [Fact]
    public void should_gate_broadcast_actions_by_permission()
    {
        var denied = Ctx(EnumDeviceType.IpSpeaker, broadcast: false);
        foreach (var a in new[] { SymbolDetailAction.SoundPlay, SymbolDetailAction.Tts, SymbolDetailAction.BroadcastStop, SymbolDetailAction.MicPtt })
            Assert.Equal("권한 없음(broadcast:control)", SymbolDetailRules.Evaluate(a, denied).DisabledReason);
    }

    [Fact]
    public void should_keep_mic_disabled_until_server_command_is_agreed()
    {
        // 권한이 있어도 서버 cmd 가 없으면 누를 수 없다 — 눌러도 아무 일 없는 버튼 금지(FR-23)
        var s = SymbolDetailRules.Evaluate(SymbolDetailAction.MicPtt, Ctx(EnumDeviceType.IpSpeaker, mic: false));
        Assert.False(s.IsEnabled);
        Assert.Equal("마이크 방송 규격 미확정", s.DisabledReason);

        Assert.True(SymbolDetailRules.Evaluate(SymbolDetailAction.MicPtt, Ctx(EnumDeviceType.IpSpeaker, mic: true)).IsEnabled);
    }

    // ── 개폐 버튼 ─────────────────────────────────────────────────────

    [Fact]
    public void should_gate_door_buttons_by_device_control_permission()
    {
        var denied = Ctx(EnumDeviceType.Gate, devCtl: false);
        Assert.Equal("권한 없음(devices:control)", SymbolDetailRules.Evaluate(SymbolDetailAction.DoorOpen, denied).DisabledReason);
    }

    [Theory]
    [InlineData(DoorUiState.Pending, "응답 대기 중")]
    [InlineData(DoorUiState.Unknown, "상태 미수신")]
    public void should_block_door_buttons_while_pending_or_unknown(DoorUiState state, string reason)
    {
        var ctx = Ctx(EnumDeviceType.Gate, door: state);
        Assert.Equal(reason, SymbolDetailRules.Evaluate(SymbolDetailAction.DoorOpen, ctx).DisabledReason);
        Assert.Equal(reason, SymbolDetailRules.Evaluate(SymbolDetailAction.DoorClose, ctx).DisabledReason);
    }

    [Fact]
    public void should_disable_the_button_matching_current_door_state()
    {
        var closed = Ctx(EnumDeviceType.Gate, door: DoorUiState.Closed);
        Assert.True(SymbolDetailRules.Evaluate(SymbolDetailAction.DoorOpen, closed).IsEnabled);
        Assert.Equal("이미 그 상태", SymbolDetailRules.Evaluate(SymbolDetailAction.DoorClose, closed).DisabledReason);

        var open = Ctx(EnumDeviceType.Gate, door: DoorUiState.Open);
        Assert.True(SymbolDetailRules.Evaluate(SymbolDetailAction.DoorClose, open).IsEnabled);
        Assert.Equal("이미 그 상태", SymbolDetailRules.Evaluate(SymbolDetailAction.DoorOpen, open).DisabledReason);
    }

    // ── 전체 평가 ─────────────────────────────────────────────────────

    [Fact]
    public void should_evaluate_all_visible_actions_in_display_order()
    {
        var ctx = Ctx(EnumDeviceType.IpSpeaker);
        var states = SymbolDetailRules.EvaluateAll(ctx);
        var expected = SymbolDetailRules.VisibleActions(EnumDeviceType.IpSpeaker);

        Assert.Equal(expected.Count, states.Count);
        Assert.Equal(expected, states.Select(s => s.Action).ToList());
        // 모든 비활성 항목은 사유를 갖는다 — 사유 없는 회색 버튼 금지(FR-28)
        Assert.All(states, s => Assert.True(s.IsEnabled || !string.IsNullOrWhiteSpace(s.DisabledReason)));
    }

    [Fact]
    public void should_give_a_reason_for_every_disabled_action_across_all_types()
    {
        foreach (var t in new[] { EnumDeviceType.Controller, EnumDeviceType.Fence, EnumDeviceType.IpCamera,
                                  EnumDeviceType.IpSpeaker, EnumDeviceType.Lamp, EnumDeviceType.Gate, EnumDeviceType.Enclosure })
        {
            foreach (var hasDevice in new[] { true, false })
            {
                var ctx = Ctx(t, hasDevice: hasDevice, web: false, endpoint: false, devCtl: false, broadcast: false, events: false);
                foreach (var s in SymbolDetailRules.EvaluateAll(ctx))
                    Assert.True(s.IsEnabled || !string.IsNullOrWhiteSpace(s.DisabledReason),
                        $"{t}/{hasDevice}/{s.Action} 비활성인데 사유가 없다");
            }
        }
    }
}
