using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// GIS 실창 육안 검토 2회차(2026-09-27, visual-review.md 담당 C) — 부대 편제 #38 · #39.
/// 코드("unit001")를 머리 · 레일 바닥에 내지 않는다 · 숨긴 칸의 수("인접 쌍") · 모호한 안내 · 창 테두리에 먹힌 도킹 폭.
/// </summary>
[Collection("CaliburnIoC")]
public class UnitConsoleVisualReviewTests
{
    [Fact]
    public async Task should_name_my_unit_instead_of_its_code_when_the_rail_footer_speaks()
    {
        var console = await OpenAsync(myUnitCode: "unit001");

        Assert.Equal("내 부대 · 기본 부대", console.RailFooterText);
        Assert.DoesNotContain("unit001", console.RailFooterText);
    }

    [Fact]
    public async Task should_leave_the_rail_footer_empty_and_keep_the_ordering_note_in_the_tree_help_when_my_unit_is_not_in_the_tree()
    {
        var console = await OpenAsync(myUnitCode: "elsewhere");

        // help-callout H-2 — 정렬 규칙 설명은 화면 바닥이 아니라 트리 "?" 에 있다
        Assert.Equal(string.Empty, console.RailFooterText);
        Assert.Contains("같은 단계의 부대는 코드 순으로 표시됩니다", Ironwall.Dotnet.Libraries.Utils.Consoles.HelpCatalog.Find("Devices.Units.Tree")!.ToPlainText());
    }

    [Fact]
    public async Task should_show_the_rail_name_as_the_subtitle_instead_of_the_unit_code()
    {
        var console = await OpenAsync(myUnitCode: "unit001");

        Assert.Equal("편제 트리", console.RailSubtitle);
        console.SelectedRail = console.RailEntries.Single(e => e.Key == UnitConsoleViewModel.RAIL_ADJACENCY);
        Assert.Equal("부대 관계도", console.RailSubtitle);
    }

    [Fact]
    public async Task should_not_count_adjacency_pairs_in_the_status_line_while_the_adjacency_rail_is_hidden()
    {
        var console = await OpenAsync();

        Assert.Equal("부대 2", console.ListStatusText);
        Assert.DoesNotContain("인접", console.ListStatusText);
    }

    [Fact]
    public void should_say_what_to_press_on_the_root_drop_line_when_the_view_is_declared()
    {
        var xaml = File.ReadAllText(Path.Combine(UnitsFolder(), "UnitConsoleView.xaml"));

        Assert.DoesNotContain("고르고 누르세요", xaml);
        Assert.Contains("고른 뒤 이 줄을 누르면 최상위로 옮깁니다", xaml);
        Assert.DoesNotContain("Subtitle=\"{Binding MyUnitCode}\"", xaml);
    }

    [Theory]
    [InlineData(1264, 1280, 1920, 16)]    // 테두리(8+8)에 먹힌 몫만큼 넓힌다 — 도킹(상세 칸)이 선다
    [InlineData(1280, 1296, 1920, 0)]     // 이미 도킹 — 그대로
    [InlineData(1000, 1016, 1920, 0)]     // 사용자가 좁힌 창 — 건드리지 않는다
    [InlineData(1264, 1280, 1285, 0)]     // 작업 영역을 넘으면 넓히지 않는다
    public void should_widen_the_unit_window_only_by_the_border_share_when_it_misses_the_docking_width(
        double console, double window, double workArea, double expected)
    {
        Assert.Equal(expected, ConsoleWindowChrome.DockingDeficit(console, window, workArea));
    }

    [Fact]
    public void should_pick_a_dark_caption_for_the_dark_header_and_a_light_one_for_the_light_header()
    {
        Assert.True(ConsoleWindowChrome.IsDark(Color.FromRgb(0x1C, 0x24, 0x2E)));
        Assert.False(ConsoleWindowChrome.IsDark(Color.FromRgb(0xF4, 0xF6, 0xF9)));
        Assert.Equal(0x00332211, ConsoleWindowChrome.ToColorRef(Color.FromRgb(0x11, 0x22, 0x33)));   // COLORREF = 0x00BBGGRR
    }

    /// <summary>
    /// B2 — 부대 편제 창의 겉은 커널 한 곳(<see cref="ConsoleWindowChrome"/>)이 입힌다. 옛 <c>UnitWindowChrome</c> 은
    /// <b>옮기고 지웠다</b>(승격 = 이관 + 원본 삭제 + 호출부 갱신) — 사본이 되살아나면 두 겉이 갈라진다.
    /// </summary>
    [Fact]
    public void should_dress_the_unit_window_with_the_kernel_chrome_when_the_old_copy_was_moved()
    {
        // Arrange
        var codeBehind = File.ReadAllText(Path.Combine(UnitsFolder(), "UnitConsoleView.xaml.cs"));
        var view = File.ReadAllText(Path.Combine(UnitsFolder(), "UnitConsoleView.xaml"));

        // Act
        var oldType = typeof(UnitConsoleViewModel).Assembly.GetTypes().FirstOrDefault(t => t.Name == "UnitWindowChrome");

        // Assert
        Assert.Null(oldType);
        Assert.False(File.Exists(Path.Combine(UnitsFolder(), "UnitWindowChrome.cs")));
        Assert.Contains("ConsoleWindowChrome.Apply(window, shell)", codeBehind);
        Assert.Contains("Loaded=\"OnShellLoaded\"", view);                       // 배선이 살아 있다
        Assert.Contains("<c:ConsoleShell", view);                                // 뿌리가 커널 셸이라 자동으로도 입혀진다
    }

    #region - Fixtures -
    private static async Task<UnitConsoleViewModel> OpenAsync(string? myUnitCode = null)
    {
        var console = new UnitConsoleViewModel(new GraphApi(), new NoDevices(), myUnitCode: () => myUnitCode,
                                               canEdit: () => true, canDelete: () => true, canView: () => true);
        await ((IActivate)console).ActivateAsync();
        return console;
    }

    private static string UnitsFolder([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Consoles", "Units"));

    private sealed class GraphApi : IUnitGraphApi
    {
        public bool IsAvailable => true;

        public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitGraphDto>.CreateSuccess(new UnitGraphDto
            {
                Nodes = new List<UnitListDto>
                {
                    new() { Id = 1, Code = "unit001", Name = "기본 부대", EchelonRaw = "Company" },
                    new() { Id = 2, Code = "unit002", Name = "둘째 부대", EchelonRaw = "Company" },
                },
            }));

        public Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto { Id = unitId, Code = "unit001", Name = "기본 부대", EchelonRaw = "Company" }));

        public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = 3 }));

        public Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = unitId }));

        public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDeleteResultDto>.CreateSuccess(new UnitDeleteResultDto { Id = unitId }));
    }

    private sealed class NoDevices : IUnitDeviceApi
    {
        public bool IsAvailable => true;

        public Task<UnitDeviceLoadResult> LoadAllAsync(CancellationToken token = default)
            => Task.FromResult(new UnitDeviceLoadResult(new List<UnitDeviceItem>(), Array.Empty<string>()));

        public Task<UnitDeviceAssignResult> AssignAsync(UnitDeviceItem device, int unitId, CancellationToken token = default)
            => Task.FromResult(new UnitDeviceAssignResult(true, "바꿨습니다"));
    }
    #endregion
}
