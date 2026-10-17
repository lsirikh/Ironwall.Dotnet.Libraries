using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 진행 막대(<c>ProgressBar</c>)의 <c>Value</c> 바인딩은 <c>Mode=OneWay</c> 를 적는다.
/// <para><b>왜</b>: <c>RangeBase.Value</c> 는 기본이 양방향이라, 뷰모델의 읽기 전용(private set) 속성에 묶으면
/// 창이 뜨는 순간 <c>InvalidOperationException</c>("TwoWay 또는 OneWayToSource 바인딩은 … 읽기 전용 속성 …") 으로 앱이 죽는다.
/// 2026-10-13 실창: 장비 대량 등록 <c>BulkImportView</c> 의 <c>ProgressValue</c> 가 장비 창을 열자마자 GIS 를 종료시켰다
/// — 헤드리스 뷰모델 시험은 뷰를 띄우지 않아 잡지 못했다. 진행 막대는 늘 표시 전용이므로 규칙을 저장소 전체에 건다.</para>
/// </summary>
public class ProgressBarBindingContractTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    private static readonly Regex ProgressBarElement = new(@"<ProgressBar\b[^>]*?/?>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ValueBinding = new(@"\bValue=""\{Binding\b([^""]*)\}""", RegexOptions.Compiled);

    [Fact]
    [Trait("Scenario", "BLK-CRASH-001")]
    public void should_declare_one_way_mode_when_a_progress_bar_value_is_bound()
    {
        var root = RepoRoot();
        var offenders = new List<string>();
        foreach (var dir in Directory.EnumerateDirectories(root, "Ironwall.Dotnet.*", SearchOption.TopDirectoryOnly))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.xaml", SearchOption.AllDirectories))
            {
                var sep = Path.DirectorySeparatorChar;
                if (file.Contains($"{sep}bin{sep}") || file.Contains($"{sep}obj{sep}")) continue;
                var text = File.ReadAllText(file);
                foreach (Match element in ProgressBarElement.Matches(text))
                {
                    var binding = ValueBinding.Match(element.Value);
                    if (!binding.Success) continue;
                    if (binding.Groups[1].Value.Contains("Mode=OneWay", StringComparison.Ordinal)) continue;
                    var line = text[..element.Index].Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetRelativePath(root, file)}:{line} — {binding.Value}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "ProgressBar Value 바인딩에 Mode=OneWay 가 없다(읽기 전용 속성이면 창이 뜰 때 앱이 죽는다):" + Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
    }
}
