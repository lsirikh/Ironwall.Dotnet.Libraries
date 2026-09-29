using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Overview;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 운영자 화면 문구 — 개요 · 맵핑 워크벤치 · 조치보고/이력 다이얼로그 · 선택 뷰에서
                  개발자용 문구(권한 키 · 서버 규칙 설명 · JSON 경로 · "하십시오" · "배선" · 판본)가
                  다시 나오지 않는지, 개요 계열이 색이 아니라 형태로도 갈리는지 검사한다(UI 완성도 감사 E-3 · E-10 · E-11).
   Created By   : Claude
   Created On   : 2026-09-27
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EventsWordingTests
{
    #region - 도우미 -
    private static string ProjectDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.Events.Ui.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string ReadSource(params string[] parts)
    {
        var path = Path.Combine(new[] { ProjectDir() }.Concat(parts).ToArray());
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
    }

    /// <summary>
    /// C# 문자열 리터럴만(주석 제외) — 사용자에게 나갈 수 있는 글자. 줄 끝 주석까지 지운다.
    /// 리터럴은 한 줄 안에서만 짝짓는다(줄바꿈을 넘지 않는다) — 어긋나도 그 줄 안에서 끝난다.
    /// </summary>
    private static string[] StringLiterals(string source)
    {
        var withoutComments = Regex.Replace(source, @"//.*$", string.Empty, RegexOptions.Multiline);
        return Regex.Matches(withoutComments, "\"(?:[^\"\\\\\\n]|\\\\.)*\"")
                    .Select(m => m.Value)
                    .ToArray();
    }

    /// <summary>XAML 속성 값만(주석 제외).</summary>
    private static string[] XamlAttributeValues(string xaml)
    {
        var withoutComments = Regex.Replace(xaml, "<!--.*?-->", string.Empty, RegexOptions.Singleline);
        return Regex.Matches(withoutComments, "[\\w:.]+=\"[^\"]*\"")
                    .Select(m => m.Value)
                    .ToArray();
    }

    private static EventDashboardDto Dashboard(int operation) => new()
    {
        Summary = new EventSummaryDto { SensorDetection = 2, CameraDetection = 1, Alert = 1, Operation = operation, DaysInRange = 1 },
    };
    #endregion

    #region - 개요 (E-3) -
    [Fact]
    public void should_describe_total_without_server_rule_jargon_when_overview_loads()
    {
        // Arrange
        var vm = new EventOverviewViewModel();

        // Act
        vm.Load(Dashboard(operation: 3), new DateTime(2026, 9, 24), new DateTime(2026, 9, 25));

        // Assert
        Assert.DoesNotContain("총계 밖", vm.TotalNote);
        Assert.DoesNotContain("서버 규칙", vm.TotalNote);
        Assert.DoesNotContain("6종", vm.TotalNote);
        Assert.Contains("운영 3건", vm.TotalNote);                     // 운영 건수는 숨기지 않는다
        Assert.EndsWith("셉니다", vm.TotalNote);
    }

    [Theory]
    [InlineData(OverviewDeviceGroup.Controller)]
    [InlineData(OverviewDeviceGroup.Camera)]
    [InlineData(OverviewDeviceGroup.Facility)]
    public void should_show_one_short_operator_line_when_device_group_changes(OverviewDeviceGroup group)
    {
        // Arrange
        var vm = new EventOverviewViewModel();

        // Act
        vm.DeviceGroup = group;

        // Assert
        Assert.StartsWith("막대를 누르면 그 장비의 내역으로 이동합니다.", vm.DeviceGroupNote);
        Assert.DoesNotContain("총계 밖", vm.DeviceGroupNote);
        Assert.DoesNotContain("—", vm.DeviceGroupNote);
        Assert.DoesNotContain("제어기에 속하지", vm.DeviceGroupNote);
    }

    [Fact]
    public void should_separate_alert_from_sensor_by_colour_and_shape_when_series_are_declared()
    {
        // Arrange
        var sensor = EventSeriesSpec.Sensor;
        var alert = EventSeriesSpec.Alert;
        var camera = EventSeriesSpec.Camera;

        // Assert — 사전 경보는 센서 탐지와 색도 형태도 다르다
        Assert.NotEqual(sensor.BrushKey, alert.BrushKey);
        Assert.NotEqual(sensor.Swatch, alert.Swatch);

        // Assert — 칩 크기에서 비슷해 보이던 셋(센서 · 카메라 · 사전 경보)은 형태가 셋 다 다르다
        Assert.Equal(3, new[] { sensor.Swatch, camera.Swatch, alert.Swatch }.Distinct().Count());
    }

    [Fact]
    public void should_give_every_series_a_distinct_look_when_colour_and_shape_are_combined()
    {
        // Arrange
        var looks = EventSeriesSpec.All
            .Select(s => (s.BrushKey, s.Swatch))
            .ToList();

        // Assert — 색과 형태의 짝이 계열마다 다르다(같은 짝이면 칩에서 구분이 안 된다)
        Assert.Equal(looks.Count, looks.Distinct().Count());
    }

    [Fact]
    public void should_not_share_the_operation_colour_with_alert_when_facility_bars_show_both()
    {
        // 함체 · 통문 막대에는 사전 경보와 운영이 한 막대에 함께 실린다.
        Assert.NotEqual(EventSeriesSpec.Alert.BrushKey, EventSeriesSpec.Operation.BrushKey);
    }

    [Fact]
    public void should_drop_design_rationale_and_long_hint_when_overview_xaml_is_rendered()
    {
        // Arrange
        var values = XamlAttributeValues(ReadSource("Views", "Consoles", "EventOverviewView.xaml"));

        // Assert
        Assert.DoesNotContain(values, v => v.Contains("도넛", StringComparison.Ordinal));
        Assert.DoesNotContain(values, v => v.Contains("기간 칩", StringComparison.Ordinal));
        Assert.Contains(values, v => v == "Text=\"끌어서 기간 선택 (Esc 취소)\"");
    }
    #endregion

    #region - 맵핑 워크벤치 (E-10) -
    [Fact]
    public void should_not_show_permission_key_when_workbench_is_editable()
    {
        // Arrange
        var vm = new MappingWorkbenchViewModel(new CountingGateway(), new StubDevices());

        // Assert
        Assert.Equal("편집 가능", vm.PermissionText);
        Assert.DoesNotContain("integrations", vm.PermissionText);
        Assert.DoesNotContain(":", vm.PermissionText);
    }

    [Fact]
    public void should_say_read_only_when_edit_permission_is_missing()
    {
        // Arrange
        var permissions = new Mock<IPermissionService>();
        permissions.Setup(p => p.CanView(It.IsAny<string>())).Returns(true);
        permissions.Setup(p => p.CanEdit(It.IsAny<string>())).Returns(false);
        permissions.Setup(p => p.CanDelete(It.IsAny<string>())).Returns(false);

        // Act
        var vm = new MappingWorkbenchViewModel(new CountingGateway(), new StubDevices(), permissions.Object);

        // Assert
        Assert.Equal("읽기 전용", vm.PermissionText);
    }

    [Fact]
    public void should_say_no_mappings_registered_when_list_is_empty()
    {
        // Arrange — 아직 한 건도 받지 않은 창
        var vm = new MappingWorkbenchViewModel(new CountingGateway(), new StubDevices());

        // Assert
        Assert.True(vm.IsMappingListEmpty);
        Assert.Equal("등록된 맵핑이 없습니다", vm.MappingEmptyTitle);
        Assert.Contains("[맵핑 등록]", vm.MappingEmptyHint);
    }

    [Fact]
    public async Task should_say_list_failed_instead_of_empty_when_loading_fails()
    {
        // Arrange
        var vm = new MappingWorkbenchViewModel(new CountingGateway { Unsupported = true }, new StubDevices());

        // Act
        await vm.ReloadAsync();

        // Assert — 못 불러온 것을 "없다" 고 말하지 않는다
        Assert.True(vm.IsMappingListEmpty);
        Assert.Equal("맵핑 목록을 불러오지 못했습니다", vm.MappingEmptyTitle);
        Assert.Contains("지원하지", vm.StatusText);
        Assert.DoesNotContain("판본", vm.StatusText);
    }

    [Fact]
    public async Task should_say_no_match_when_search_hides_every_mapping()
    {
        // Arrange
        var vm = new MappingWorkbenchViewModel(new CountingGateway(), new StubDevices());
        await vm.ReloadAsync();

        // Act
        vm.SearchText = "어디에도 없는 이름";

        // Assert
        Assert.True(vm.IsMappingListEmpty);
        Assert.Equal("검색 조건에 맞는 맵핑이 없습니다", vm.MappingEmptyTitle);
    }

    [Fact]
    public async Task should_say_no_matching_device_when_palette_search_finds_nothing()
    {
        // Arrange
        var vm = new MappingWorkbenchViewModel(new CountingGateway(), new StubDevices());
        await vm.ReloadAsync();
        Assert.False(vm.IsPaletteEmpty);

        // Act
        vm.PaletteSearch = "어디에도 없는 장비";

        // Assert
        Assert.True(vm.IsPaletteEmpty);
        Assert.Equal("조건에 맞는 장비가 없습니다", vm.PaletteEmptyTitle);
    }

    [Fact]
    public void should_use_polite_haseyo_tone_when_mapping_sources_produce_text()
    {
        // Arrange
        var dir = Path.Combine(ProjectDir(), "Consoles", "Mapping");
        var offenders = Directory.GetFiles(dir, "*.cs")
            .SelectMany(f => StringLiterals(File.ReadAllText(f)).Select(s => (File: Path.GetFileName(f), Text: s)))
            .Where(x => x.Text.Contains("하십시오", StringComparison.Ordinal)
                     || x.Text.Contains("배선", StringComparison.Ordinal)
                     || x.Text.Contains("판본", StringComparison.Ordinal)
                     || x.Text.Contains("integrations:", StringComparison.Ordinal))
            .Select(x => $"{x.File}: {x.Text}")
            .ToList();

        // Assert
        Assert.Empty(offenders);
    }

    [Fact]
    public void should_keep_developer_words_out_of_the_workbench_markup_when_rendered()
    {
        // Arrange
        var values = XamlAttributeValues(ReadSource("Consoles", "Mapping", "MappingWorkbenchView.xaml"));

        // Assert
        Assert.DoesNotContain(values, v => v.Contains("하십시오", StringComparison.Ordinal));
        Assert.DoesNotContain(values, v => v.Contains("배선", StringComparison.Ordinal));
        Assert.DoesNotContain(values, v => v.Contains("서버 호출", StringComparison.Ordinal));
        Assert.DoesNotContain(values, v => v.Contains("이번 판", StringComparison.Ordinal));
        Assert.DoesNotContain(values, v => v == "ToolTip=\"category_event_mapping\"");
        Assert.Contains(values, v => v == "Loaded=\"OnToolbarLoaded\"");   // 늘 꺼진 [삭제] 를 접는 배선
    }

    [Fact]
    public void should_hide_server_english_from_failure_notes_when_bulk_create_item_fails()
    {
        // Arrange
        var outcome = new MappingCommitOutcome();
        var row = MappingBoardRow.NewFor(MappingActionKind.Camera, 101);

        // Act
        outcome.AcceptCreate(new[] { row }, new MappingBulkCreateResultDto
        {
            FailedItems = new List<MappingBulkFailedItemDto> { new() { Index = 0, Error = "Camera with id 101 not found" } },
        });

        // Assert — 화면에는 한국어만, 원문은 로그용 칸으로
        Assert.DoesNotContain("Camera with id", string.Join(" ", outcome.FailureNotes));
        Assert.Contains(outcome.RawFailureDetails, d => d.Contains("Camera with id 101", StringComparison.Ordinal));
    }
    #endregion

    #region - 다이얼로그 · 선택 뷰 (E-11) -
    [Theory]
    [InlineData("DetectionReportDialogView.xaml", "탐지 조치보고")]
    [InlineData("MalfunctionReportDialogView.xaml", "장애 조치보고")]
    public void should_title_report_dialog_as_action_report_when_opened(string file, string title)
    {
        // Arrange
        var values = XamlAttributeValues(ReadSource("Views", "Dialogs", file));

        // Assert — B5(2c40a6c2, 9-28)부터 제목은 커널 다이얼로그 틀의 Title 이 그린다. 옛 머리 TextBlock(Text=) 이 되살아나면 제목이 두 번 뜬다.
        Assert.Contains(values, v => v == $"Title=\"{title}\"");
        Assert.DoesNotContain(values, v => v == $"Text=\"{title}\"");
        Assert.DoesNotContain(values, v => v.Contains("FallbackValue=테스트", StringComparison.Ordinal));
        Assert.DoesNotContain(values, v => v.Contains("FallbackValue=Etcs", StringComparison.Ordinal));
    }

    [Fact]
    public void should_not_show_json_paths_when_detection_summary_tooltips_open()
    {
        // Arrange
        var values = XamlAttributeValues(ReadSource("Views", "DetectionSelectionView.xaml"));

        // Assert
        Assert.DoesNotContain(values, v => v.Contains("detail.", StringComparison.Ordinal));
        Assert.DoesNotContain(values, v => v.Contains("bbox", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ConnectionSelectionView.xaml", "연결 속성")]
    [InlineData("MalfunctionSelectionView.xaml", "장애 속성")]
    public void should_use_plain_section_title_when_selection_view_is_shown(string file, string title)
    {
        // Arrange
        var values = XamlAttributeValues(ReadSource("Views", file));

        // Assert
        Assert.Contains(values, v => v == $"Text=\"{title}\"");
    }

    [Fact]
    public void should_hint_in_korean_when_action_editor_is_empty()
    {
        // Arrange
        var values = XamlAttributeValues(ReadSource("Views", "ActionSelectionView.xaml"));

        // Assert
        Assert.DoesNotContain(values, v => v == "md:HintAssist.Hint=\"admin\"");
        Assert.DoesNotContain(values, v => v == "md:HintAssist.Hint=\"Action report contents\"");
    }

    [Fact]
    public void should_call_custom_period_direct_when_history_dialog_shows_period_chips()
    {
        // Arrange — 콘솔의 기간 어휘(오늘 · 24시간 · 7일 · 직접)와 맞춘다
        var values = XamlAttributeValues(ReadSource("Views", "Dialogs", "DetectionHistoryDialogView.xaml"));

        // Assert
        Assert.Contains(values, v => v == "Content=\"직접\"");
        Assert.DoesNotContain(values, v => v == "Content=\"기간지정\"");
    }
    #endregion
}
