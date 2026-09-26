using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>부대 콘솔 — 좁은 폭의 미배치 장비 보기에서 상세를 접어 편제 트리에 폭을 준다(감사 D-8 8.10).</summary>
public class UnitDetailRequestConverterTests
{
    [Theory]
    [InlineData(true, false, false, true)]    // 편제 트리 · 서랍 — 상세는 그대로 연다
    [InlineData(true, true, true, true)]      // 미배치 장비 · 도킹(1280+) — 폭이 넉넉하다
    [InlineData(true, true, false, false)]    // 미배치 장비 · 서랍 — 트리가 짓눌리지 않게 접는다
    [InlineData(false, false, true, false)]   // 요청이 없으면 언제나 닫힘
    public void should_fold_detail_only_on_narrow_device_view_when_resolving(bool requested, bool deviceView, bool docked, bool expected)
        => Assert.Equal(expected, UnitDetailRequestConverter.Resolve(requested, deviceView, docked));

    [Fact]
    public void should_treat_missing_values_as_false_when_converting()
        => Assert.Equal(false, new UnitDetailRequestConverter().Convert(new object[] { true, true }, typeof(bool), null!, System.Globalization.CultureInfo.InvariantCulture));
}
