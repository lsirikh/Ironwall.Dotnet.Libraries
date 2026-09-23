using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// D-27(Accounts 몫) — 별(*) 열이 <c>MinWidth</c> 없이 좁아지면 WPF 기본 <c>MinWidth</c>(20px)까지
/// 눌려 글자를 못 읽게 된다(보고서 콘솔 실측, 8fa2cb5e). 같은 커밋이 밝힌 두 번째 함정 —
/// 리사이즈가 아주 좁은 과도 폭을 지나가면 <b>고정 열까지</b> 그 순간의 최소값에 영구히 눌어붙는다 —
/// 때문에 고정 열도 제 설계 폭을 <c>MinWidth</c> 로 걸어야 한다.
/// </summary>
/// <remarks>
/// Accounts.Ui 의 열은(Devices/Reports 와 달리) 코드가 아니라 <b>순수 XAML</b> 로 선언된다 —
/// 뺄 순수 함수가 없으므로 XAML 소스를 직접 읽어 계약을 글자로 지킨다
/// (선례: <see cref="Ironwall.Dotnet.Libraries.Utils.Tests.ConsoleStyleContractTests"/>).
/// </remarks>
public class DataGridColumnFloorTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static string ReadView(string fileName)
        => File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "Views", "Panels", fileName));

    private static IEnumerable<XElement> ColumnElements(string xaml)
        => XDocument.Parse(xaml).Descendants()
            .Where(e => e.Name.LocalName is "DataGridTemplateColumn" or "DataGridTextColumn" or "DataGridCheckBoxColumn");

    private static bool IsStarWidth(string? width) => width is not null && width.EndsWith("*", StringComparison.Ordinal);

    public static IEnumerable<object[]> AllGridFiles => new[]
    {
        new object[] { "AccountConsolePanelView.xaml" },   // 실제로 화면에 뜨는 콘솔(탭 호스트) — 사용자·매트릭스·구성원·부여·세션·감사 6개 그리드
        new object[] { "AuditLogPanelView.xaml" },          // 고아 화면(참조처 0) — 그래도 컴파일되므로 같은 계약을 건다
        new object[] { "GrantManagementPanelView.xaml" },   // 고아 화면 — 별 열 없음(전부 고정 폭)
        new object[] { "PermissionMatrixPanelView.xaml" },  // 고아 화면 — 별 열 2종(권한요약 · 빈 채움열)
        new object[] { "UserSessionPanelView.xaml" },        // 고아 화면 — 별 열 없음(전부 고정 폭)
    };

    [Theory]
    [MemberData(nameof(AllGridFiles))]
    public void should_floor_every_readable_star_column_at_140_when_the_view_declares_one(string file)
    {
        // Arrange
        var columns = ColumnElements(ReadView(file))
            .Where(e => IsStarWidth((string?)e.Attribute("Width")))
            .ToList();

        // Act / Assert — 헤더나 바인딩이 있는 별 열(=화면에 글자가 뜨는 열)은 전부 바닥이 있어야 한다.
        // 헤더도 바인딩도 없는 빈 채움 열(순수 여백)은 글자가 없어 눌려도 읽는 사람이 없다 — 의도적으로 제외한다.
        // 순수 "*"(가중치 1, D-27 이 고친 자리)는 커널 바닥 140 을 그대로 요구한다.
        // 가중 별(예: "1.4*")은 세션·감사 그리드에 이미 있던 설계 — 그 열끼리 비율로 나눈 자기 폭에
        // 맞춘 개별 바닥이 있다(예: 96, 70, 120 …). 존재만 확인한다 — 140 을 요구하면 오탐이다.
        foreach (var column in columns)
        {
            var header = (string?)column.Attribute("Header");
            var hasBinding = column.Attribute("Binding") is not null;
            var isBlankFiller = string.IsNullOrEmpty(header) && !hasBinding;
            if (isBlankFiller) continue;

            var width = (string)column.Attribute("Width")!;
            var isPlainStar = width == "*";
            var minWidth = (double?)column.Attribute("MinWidth");

            if (isPlainStar)
                Assert.True(minWidth is >= 140,
                    $"{file}: 별 열(Header='{header}')에 MinWidth>=140 바닥이 없다 — 좁은 폭에서 WPF 기본 MinWidth(20)까지 눌려 글자를 못 읽는다.");
            else
                Assert.True(minWidth is > 0,
                    $"{file}: 가중 별 열(Header='{header}', Width={width})에 자기 바닥(MinWidth)이 없다.");
        }
    }

    [Theory]
    [MemberData(nameof(AllGridFiles))]
    public void should_floor_every_fixed_width_column_at_its_own_design_width(string file)
    {
        // Arrange
        var columns = ColumnElements(ReadView(file)).ToList();

        // Act / Assert — 고정 열도 제 폭을 MinWidth 로 걸어야 한다: 리사이즈가 과도 폭을 스쳐 지나가면
        // 그 순간의 최소값에 영구히 눌어붙는 두 번째 함정(8fa2cb5e 실측)을 막는다.
        foreach (var column in columns)
        {
            var width = (string?)column.Attribute("Width");
            if (width is null || IsStarWidth(width)) continue;
            if (!double.TryParse(width, System.Globalization.CultureInfo.InvariantCulture, out var designWidth)) continue;

            var minWidth = (double?)column.Attribute("MinWidth");
            var header = (string?)column.Attribute("Header");
            Assert.True(minWidth == designWidth,
                $"{file}: 고정 열(Header='{header}', Width={width})의 MinWidth 가 설계 폭과 다르다(현재={minWidth?.ToString() ?? "없음"}) — 과도 폭 고정 방지 바닥이 빠졌다.");
        }
    }

    [Fact]
    public void should_recognize_exactly_two_intentional_blank_filler_columns_in_the_permission_matrix_view()
    {
        // Arrange — PermissionMatrixPanelView.xaml 의 매트릭스·구성원 그리드 각각 끝에 헤더 없는
        // 채움 열이 하나씩 있다(남는 폭을 흡수만 하는 순수 여백 — 글자가 없어 바닥이 필요 없다).
        // 이 표의 개수·모양이 달라지면(헤더나 바인딩이 붙으면) 위 플로어 시험이 그걸 놓치지 않게
        // 여기서 "의도된 예외"의 정체를 못박아 둔다.
        var unfloored = ColumnElements(ReadView("PermissionMatrixPanelView.xaml"))
            .Where(e => IsStarWidth((string?)e.Attribute("Width")) && e.Attribute("MinWidth") is null)
            .ToList();

        // Act / Assert
        Assert.Equal(2, unfloored.Count);
        Assert.All(unfloored, e =>
        {
            Assert.True(string.IsNullOrEmpty((string?)e.Attribute("Header")));
            Assert.Null(e.Attribute("Binding"));
        });
    }
}
