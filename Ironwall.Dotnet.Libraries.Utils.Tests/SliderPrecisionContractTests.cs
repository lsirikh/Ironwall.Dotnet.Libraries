using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 값에 묶인 슬라이더(<c>Slider Value="{Binding …}"</c>)는 <c>IsSnapToTickEnabled="True"</c> 와 <c>TickFrequency</c>(그 칸 단위)를 적는다.
/// <para><b>왜</b>: 스냅 없는 슬라이더는 끄는 대로 임의 실수(예 11.9999999 · 37.4285714)를 내보낸다. 2026-10-13 사용자 보고
/// "심볼 속성 슬라이더를 움직이면 정수가 아니라 11.9999999… 같은 무한 소수가 나온다". 값 자체의 반올림 · 범위는
/// GMaps.Ui <c>MapNumericFields</c> 명세(docs/prds/map-property-numeric-precision-prd.md)가 맡고, 이 시험은 화면 쪽 첫 관문을 지킨다.</para>
/// </summary>
public class SliderPrecisionContractTests
{
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, ".."));

    private static readonly Regex SliderElement = new(@"<Slider\b[^>]*?/?>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ValueBinding = new(@"\bValue=""\{Binding\s+(?:Path=)?([^,}\s]*)[^""]*\}""", RegexOptions.Compiled);

    /// <summary>예외(파일 이름 · 바인딩 경로) — 근거와 함께만 더한다.</summary>
    private static readonly Dictionary<string, string> Exceptions = new()
    {
        // 재생 위치(0~100 %) — 저장값이 아닌 시간 위치로 바로 찾아가기(seek)용 연속 값. 화면 표시는 TimeLabel(시각 글)이 맡는다.
        ["PlaybackConsoleStyle.xaml|Progress"] = "재생 위치 seek",
        // 영상 음량 — 대상 속성이 int(ImprovedRtspPlayer.Volume)라 바인딩 변환에서 정수가 된다. 지도 밖(Streaming) 칸.
        ["ImprovedCameraItem.xaml|Volume"] = "int 대상",
    };

    [Fact]
    [Trait("Scenario", "NUM-030")]
    public void should_snap_to_tick_when_a_slider_value_is_bound()
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
                foreach (Match element in SliderElement.Matches(text))
                {
                    var binding = ValueBinding.Match(element.Value);
                    if (!binding.Success) continue;
                    if (Exceptions.ContainsKey($"{Path.GetFileName(file)}|{binding.Groups[1].Value}")) continue;
                    bool snaps = element.Value.Contains("IsSnapToTickEnabled=\"True\"", StringComparison.Ordinal)
                                 && element.Value.Contains("TickFrequency=", StringComparison.Ordinal);
                    if (snaps) continue;
                    var line = text[..element.Index].Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetRelativePath(root, file)}:{line} — {binding.Value}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "값에 묶인 Slider 에 IsSnapToTickEnabled=\"True\" + TickFrequency 가 없다(끄는 대로 꼬리 소수가 나온다):" + Environment.NewLine +
            string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    [Trait("Scenario", "NUM-031")]
    public void should_keep_every_exception_pointing_at_a_real_slider()
    {
        // 예외 목록이 낡으면(슬라이더가 사라지거나 고쳐지면) 지운다 — 예외는 실재하는 것만.
        var root = RepoRoot();
        var seen = new HashSet<string>();
        foreach (var dir in Directory.EnumerateDirectories(root, "Ironwall.Dotnet.*", SearchOption.TopDirectoryOnly))
            foreach (var file in Directory.EnumerateFiles(dir, "*.xaml", SearchOption.AllDirectories))
                foreach (Match element in SliderElement.Matches(File.ReadAllText(file)))
                {
                    var binding = ValueBinding.Match(element.Value);
                    if (binding.Success && !element.Value.Contains("IsSnapToTickEnabled=\"True\"", StringComparison.Ordinal))
                        seen.Add($"{Path.GetFileName(file)}|{binding.Groups[1].Value}");
                }

        Assert.All(Exceptions.Keys, key => Assert.Contains(key, seen));
    }
}
