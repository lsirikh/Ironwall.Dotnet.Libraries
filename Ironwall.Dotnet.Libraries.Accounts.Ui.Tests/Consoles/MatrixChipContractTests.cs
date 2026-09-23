using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Consoles;

/// <summary>
/// U-12(Accounts 몫) — 권한 설정의 그룹 칩 · [그룹|구성원] 전환은 커널 Console.Chip / Console.Tab 을 쓴다.
/// 옛 로컬 스타일(Matrix.GroupChip · Matrix.Chip)은 고르면 테두리 1→2 · SemiBold 로 칩 폭이 1px 늘었다(실측 90→91).
/// 커널 칩은 Content 를 글자로 그려 굵은 폭을 미리 잡으므로 라벨은 한 문자열(ChipText)이어야 한다.
/// </summary>
public class MatrixChipContractTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static string ConsoleView()
        => File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Accounts.Ui", "Views", "Panels", "AccountConsolePanelView.xaml"));

    [Fact]
    public void should_compose_name_and_count_when_chip_text_is_read()
    {
        // Arrange
        var row = new PermissionGroupRowViewModel { GroupId = 10, GroupName = "야간 관제", UserCount = 2 };

        // Act
        var text = row.ChipText;

        // Assert
        Assert.Equal("야간 관제 · 2", text);
    }

    [Fact]
    public void should_notify_chip_text_when_name_or_count_changes()
    {
        // Arrange
        var row = new PermissionGroupRowViewModel { GroupId = 10, GroupName = "야간 관제", UserCount = 2 };
        var raised = new List<string?>();
        ((INotifyPropertyChanged)row).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        row.UserCount = 3;
        row.GroupName = "주간 관제";

        // Assert — 칩 라벨이 옛 값으로 굳지 않는다
        Assert.Equal(2, raised.Count(n => n == nameof(PermissionGroupRowViewModel.ChipText)));
        Assert.Equal("주간 관제 · 3", row.ChipText);
    }

    [Fact]
    public void should_use_kernel_chip_styles_when_matrix_tool_row_is_declared()
    {
        // Arrange + Act
        var xaml = ConsoleView();

        // Assert — 로컬 칩 사본이 되살아나면 선택 시 폭 흔들림(U-12)이 재발한다
        Assert.DoesNotContain("x:Key=\"Matrix.GroupChip\"", xaml);
        Assert.DoesNotContain("x:Key=\"Matrix.Chip\"", xaml);
        Assert.DoesNotContain("MaterialDesignOutlinedButton", xaml);
        Assert.Contains("Style=\"{StaticResource Console.Chip}\"", xaml);
        Assert.Contains("Style=\"{StaticResource Console.Tab}\"", xaml);
    }
}
