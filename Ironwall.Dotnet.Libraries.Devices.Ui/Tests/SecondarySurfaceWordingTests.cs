using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.ByComponent;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Servers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
/****************************************************************************
   Purpose      : 보조 표면(서버 · 부대 · 결선 · 조립기 · 부품으로 찾기 · 장비 배정) 운영자 문구 회귀 (U-18)
   Created By   : GHLee
   Created On   : 9/27/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 운영자 화면에 개발자용 말(요청 횟수 · HTTP 동사 · 판본 · 권한 키 · 필드명 · "Draft" · "다음 단계")이 다시 새지 않는지 본다.
/// 원장 D-2026-09-26-648420 — 운영자 화면에서 개발자용 문구를 뺀다.
/// </summary>
public class SecondarySurfaceWordingTests
{
    /// <summary>운영자 화면에 나오면 안 되는 말.</summary>
    private static readonly string[] Banned =
    {
        "호출", "Draft", "PATCH", "POST", "422", "410", "판본", "다음 단계", "NATS", "REST",
        "units:", "devices:", "is_enable", "hardware_spec", "health", "server_config", "(6.3)", "트레이", "십시오",
    };

    private static void AssertClean(string text, string where)
    {
        foreach (var word in Banned)
            Assert.False(text.Contains(word, StringComparison.Ordinal), $"{where}: 금지어 '{word}' — \"{text}\"");
    }

    #region - Unit console: 창 제목 · 숨긴 레일 칸 -
    [Fact]
    public void should_title_window_as_unit_organization_when_unit_console_is_created()
    {
        var console = new UnitConsoleViewModel(new StubUnitApi(), new StubDeviceApi());

        Assert.Equal("부대 편제", console.DisplayName);
        Assert.DoesNotContain("ViewModel", console.DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public void should_offer_unit_map_rail_after_tree_when_unit_console_is_created()
    {
        // unit-relationship-map TEST-31 — 자리표시("인접 관계도 · 다음 단계")가 걷히고 「부대 관계도」 칸이 들어섰다.
        // v1.7 — 「미배치 장비」 칸은 서버 회신 Q-1 ⓐ(개념 폐지)로 걷었다.
        var console = new UnitConsoleViewModel(new StubUnitApi(), new StubDeviceApi());

        Assert.Equal(new[] { UnitConsoleViewModel.RAIL_TREE, UnitConsoleViewModel.RAIL_ADJACENCY },
                     console.RailEntries.Select(e => e.Key));
        var map = console.RailEntries.Single(e => e.Key == UnitConsoleViewModel.RAIL_ADJACENCY);
        Assert.Equal("부대 관계도", map.Label);
        Assert.False(map.ShowCount);                                   // 숫자 배지가 부대 수로 읽히지 않게
        Assert.DoesNotContain(console.RailEntries, e => e.Label.Contains("인접 관계도"));
        Assert.False(console.IsAdjacencyView);                         // 첫 칸은 여전히 편제 트리
    }

    [Fact]
    public async Task should_keep_rail_counts_when_adjacency_rail_is_hidden_and_console_reloads()
    {
        // 칸을 숨긴 뒤 순번(RailEntries[2])으로 개수를 쓰던 코드가 편제 적재를 통째로 죽였다 — 키로 쓴다.
        var console = new UnitConsoleViewModel(new StubUnitApi(), new StubDeviceApi(), canView: () => true);
        await ((IActivate)console).ActivateAsync();

        Assert.Equal(1, console.RailEntries.Single(e => e.Key == UnitConsoleViewModel.RAIL_TREE).Count);
        Assert.Equal("부대 1개를 불러왔습니다.", console.StatusText);
    }

    [Fact]
    public void should_speak_without_developer_terms_when_unit_console_shows_its_fixed_lines()
    {
        var console = new UnitConsoleViewModel(new StubUnitApi(), new StubDeviceApi(), canEdit: () => false, canDelete: () => false);
        console.BeginCreate();   // 권한이 없어 열리지 않는다 — 배너는 빈 채로 남는다

        AssertClean(console.RailFooterText, nameof(console.RailFooterText));
        AssertClean(console.AddBlockedReason, nameof(console.AddBlockedReason));
        AssertClean(console.DeleteBlockedReason, nameof(console.DeleteBlockedReason));
        AssertClean(UnitConsoleViewModel.NOT_SUPPORTED, nameof(UnitConsoleViewModel.NOT_SUPPORTED));
        AssertClean(UnitConsoleViewModel.NO_EDIT_PERMISSION, nameof(UnitConsoleViewModel.NO_EDIT_PERMISSION));
        Assert.Equal("같은 단계의 부대는 코드 순으로 표시됩니다.", new UnitConsoleViewModel(new StubUnitApi(), new StubDeviceApi(), myUnitCode: () => null).RailFooterText);
    }

    [Fact]
    public void should_warn_code_is_permanent_without_nats_terms_when_unit_create_begins()
    {
        var console = new UnitConsoleViewModel(new StubUnitApi(), new StubDeviceApi(), canEdit: () => true);

        console.BeginCreate();

        Assert.Equal("부대 코드는 등록 후 바꿀 수 없습니다. 신중히 입력하세요.", console.Detail.CreateBanner);
        AssertClean(console.Form.CodeNote, nameof(console.Form.CodeNote));
    }
    #endregion

    #region - Server console -
    [Fact]
    public void should_not_offer_system_events_rail_when_server_rail_is_declared()
    {
        Assert.DoesNotContain(ServerTypeCatalog.RailOrder, r => r.Key == ServerTypeCatalog.SystemEventsKey);
        Assert.DoesNotContain(ServerTypeCatalog.RailOrder, r => r.Label == "시스템 이벤트");
    }

    [Fact]
    public void should_speak_without_developer_terms_when_server_rules_build_messages()
    {
        var plan = ServerDropRules.Plan(5, EnumServerType.SPEAKER_API, new IBaseDeviceModel[]
        {
            new SpeakerDeviceModel { Id = 1, DeviceName = "s1", CategoryDevice = EnumDeviceCategory.Speaker },
            new SpeakerDeviceModel { Id = 2, DeviceName = "s2", CategoryDevice = EnumDeviceCategory.Speaker },
        });

        AssertClean(ServerDropRules.ConfirmText("방송서버", 2), "ConfirmText");
        AssertClean(ServerDropRules.TrayConfirmText(3), "TrayConfirmText");
        AssertClean(ServerDropRules.CancelledText, "CancelledText");
        AssertClean(ServerDropRules.ResultLine("방송서버", plan, 1, 1), "ResultLine");
        AssertClean(ServerDropRules.RefusalReason(EnumServerType.PROXY, EnumDeviceCategory.Camera) ?? string.Empty, "RefusalReason");
        AssertClean(ServerDropRules.Plan(5, EnumServerType.NVR_API, new IBaseDeviceModel[]
            { new CameraDeviceModel { Id = 3, DeviceName = "c", CategoryDevice = EnumDeviceCategory.Camera } },
            EnumServerContract.V6_3).BlockReason ?? string.Empty, "Plan(6.3)");
        AssertClean(ServerStatusRules.NoTransitionClockNote, "NoTransitionClockNote");
        AssertClean(ServerStatusRules.JustRegisteredNotice, "JustRegisteredNotice");
        AssertClean(ServerWriteGuard.PROXY_ABSORBED_NOTE, "PROXY_ABSORBED_NOTE");
        AssertClean(ServerWriteGuard.UNIT_NOT_IN_CONTRACT_NOTE, "UNIT_NOT_IN_CONTRACT_NOTE");
        AssertClean(ServerWriteGuard.STATUS_IS_OBSERVED_NOTE, "STATUS_IS_OBSERVED_NOTE");
        AssertClean(ServerRequestBuilder.CannotClear("호스트명"), "CannotClear");
    }

    [Theory]
    [InlineData("NORMAL", "일반")]
    [InlineData("REGISTER", "등록")]
    [InlineData("normal", "일반")]
    [InlineData("SOMETHING", "알 수 없음")]
    [InlineData(null, "—")]
    public void should_show_korean_operation_mode_when_code_is_known(string? code, string expected)
        => Assert.Equal(expected, ServerModeDisplay.OperationLabel(code));

    [Fact]
    public void should_keep_code_as_stored_value_when_mode_options_are_listed()
    {
        Assert.Equal(new[] { "NORMAL", "REGISTER" }, ServerModeDisplay.OperationModes.Select(o => o.Code));
        Assert.Equal(new[] { "일반", "등록" }, ServerModeDisplay.OperationModes.Select(o => o.Display));
        Assert.Equal(new[] { "wind0", "wind1", "wind2", "wind3" }, ServerModeDisplay.WindyModes.Select(o => o.Code));
        Assert.Equal("강한 바람", ServerModeDisplay.WindyLabel("wind2"));
        Assert.Equal("받은 값: X9", ServerModeDisplay.UnknownTooltip("X9"));
        Assert.Equal(string.Empty, ServerModeDisplay.UnknownTooltip("wind1"));
    }

    [Fact]
    public async Task should_say_no_records_when_metric_history_is_empty()
    {
        var vm = new ServerMetricHistoryViewModel(new FakeServerConsoleService(), new FixedClock(DateTime.UtcNow), 1, "s");
        await ((IActivate)vm).ActivateAsync();

        Assert.Equal(ServerMetricHistoryViewModel.NoRecordsText, vm.EmptyText);
        Assert.Equal(string.Empty, vm.Note);
    }

    [Fact]
    public async Task should_say_loading_failed_when_metric_history_cannot_be_read()
    {
        var vm = new ServerMetricHistoryViewModel(new FakeServerConsoleService { HistoryFails = true }, new FixedClock(DateTime.UtcNow), 1, "s");
        await ((IActivate)vm).ActivateAsync();

        Assert.Equal(ServerMetricHistoryViewModel.LoadFailedText, vm.EmptyText);
        Assert.NotEqual(ServerMetricHistoryViewModel.NoRecordsText, vm.EmptyText);
    }
    #endregion

    #region - 장비 배정 · 부품으로 찾기 · 조립기 -
    [Fact]
    public void should_speak_without_call_counts_when_assign_delta_builds_messages()
    {
        var plan = AssignDelta.Plan(7, new[] { 1, 2 }, new[] { 2, 3, 4, 0 });

        Assert.Equal("추가 2대 · 제외 1대를 저장합니다. 등록 전 장비 1대는 뺍니다.", AssignDelta.Summary(plan));
        AssertClean(AssignDelta.Summary(plan), "Summary");
        AssertClean(AssignDelta.Drift(new[] { 1 }, new[] { 1, 2 }) ?? string.Empty, "Drift");
        AssertClean(AssignDelta.DriftByCount(1, null) ?? string.Empty, "DriftByCount(null)");
        AssertClean(AssignDelta.ResultLine("그룹", new AssignLegOutcome(3, 2, 1, false), new AssignLegOutcome(1, 0, 0, true)), "ResultLine");
        Assert.EndsWith("니다.", AssignDelta.ResultLine("그룹", new AssignLegOutcome(1, 1, 0, false), null));
    }

    [Theory]
    [InlineData("OPEN", "열림")]
    [InlineData("CLOSED", "닫힘")]
    [InlineData("RUNNING", "구동 중")]
    [InlineData("WEIRD_STATE", "알 수 없음")]
    [InlineData("", "—")]
    public void should_show_korean_state_when_component_state_code_is_given(string code, string expected)
        => Assert.Equal(expected, ByComponentRowViewModel.StateLabel(code));

    [Fact]
    public void should_keep_raw_state_only_in_tooltip_when_state_is_unknown()
    {
        Assert.Equal("받은 값: WEIRD_STATE", ByComponentRowViewModel.UnknownStateTooltip("WEIRD_STATE"));
        Assert.Null(ByComponentRowViewModel.UnknownStateTooltip("OPEN"));
    }

    [Fact]
    public void should_speak_without_version_or_exception_text_when_by_component_fails()
    {
        AssertClean(ByComponentViewModel.NotSupportedText, "NotSupportedText");
        Assert.DoesNotContain("7.0", ByComponentViewModel.NotSupportedText);
        Assert.DoesNotContain("(", ByComponentViewModel.LoadFailedText);
    }

    [Fact]
    public void should_label_component_fields_in_korean_when_diff_is_described()
    {
        Assert.Equal("이름", AssemblyViewModel.FieldLabel("label"));
        Assert.Equal("가동", AssemblyViewModel.FieldLabel("in_service"));
        Assert.Equal("부품 설정", AssemblyViewModel.FieldLabel("overrides"));
        AssertClean(AssemblyViewModel.NoChangeText, "NoChangeText");
        AssertClean(RegisterFromPresetViewModel.BuildFailedProblem, "BuildFailedProblem");
    }
    #endregion

    #region - XAML 문구 전수 -
    /// <summary>검사하는 표면 — 이 패스가 맡은 XAML 전부.</summary>
    public static IEnumerable<object[]> OwnedXaml() => new[]
    {
        "Consoles/Servers", "Consoles/Units", "Consoles/Wiring", "Consoles/Assembly", "Consoles/ByComponent", "Consoles/Dialogs",
    }
    .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(ProjectDir(), dir), "*.xaml", SearchOption.AllDirectories))
    .Append(Path.Combine(ProjectDir(), "Views", "Dialogs", "DeviceAssignDialogView.xaml"))
    .Select(path => new object[] { Path.GetRelativePath(ProjectDir(), path) });

    /// <summary>운영자에게 보이는 속성 — ApiName · AxisName(개발 캡션, 커널 스위치로 숨김)과 AutomationId 는 보지 않는다.</summary>
    private static readonly HashSet<string> VisibleAttributes = new(StringComparer.Ordinal)
    {
        "Text", "Content", "ToolTip", "Header", "Title", "Subtitle", "Kind", "Note", "Hint", "KeyboardFallback",
        "StringFormat", "SearchPlaceholder", "AddText", "PrimaryText", "SecondaryText", "ReloadHint", "SectionName",
    };

    [Theory]
    [MemberData(nameof(OwnedXaml))]
    public void should_not_show_developer_terms_when_owned_xaml_is_rendered(string relativePath)
    {
        var doc = XDocument.Load(Path.Combine(ProjectDir(), relativePath));   // 주석(XComment)은 속성이 아니라 저절로 빠진다

        foreach (var attribute in doc.Descendants().Attributes())
        {
            var name = attribute.Name.LocalName;
            var local = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
            if (!VisibleAttributes.Contains(local)) continue;

            // 바인딩 식은 경로 이름(DraftText 등)이 아니라 그 안의 StringFormat 글만 화면에 나온다.
            var value = attribute.Value;
            if (value.StartsWith("{", StringComparison.Ordinal))
            {
                var at = value.IndexOf("StringFormat=", StringComparison.Ordinal);
                if (at < 0) continue;
                value = value[(at + "StringFormat=".Length)..];
            }
            AssertClean(value, $"{relativePath} {name}");
        }
    }

    private static string ProjectDir([CallerFilePath] string testFile = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, ".."));
    #endregion

    #region - Stubs -
    private sealed class StubUnitApi : IUnitGraphApi
    {
        public bool IsAvailable => true;

        public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitGraphDto>.CreateSuccess(new UnitGraphDto
            {
                Nodes = new List<UnitListDto> { new() { Id = 1, Code = "d01", Name = "d01", EchelonRaw = "Division" } },
            }));

        public Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto { Id = unitId, Code = "d01", Name = "d01", EchelonRaw = "Division" }));

        public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = 2 }));

        public Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = unitId }));

        public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDeleteResultDto>.CreateSuccess(new UnitDeleteResultDto { Id = unitId }));
    }

    private sealed class StubDeviceApi : IUnitDeviceApi
    {
        public bool IsAvailable => true;

        public Task<UnitDeviceLoadResult> LoadAllAsync(CancellationToken token = default)
            => Task.FromResult(new UnitDeviceLoadResult(new List<UnitDeviceItem>(), Array.Empty<string>()));

        public Task<UnitDeviceAssignResult> AssignAsync(UnitDeviceItem device, int unitId, CancellationToken token = default)
            => Task.FromResult(new UnitDeviceAssignResult(true, "바꿨습니다"));
    }
    #endregion
}
