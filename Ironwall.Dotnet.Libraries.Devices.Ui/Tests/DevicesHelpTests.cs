using System.Linq;
using System.Text.RegularExpressions;
using Ironwall.Dotnet.Libraries.Devices.Ui.Help;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 장비 화면 "?" 설명 목록(help-callout H-2) — 모듈이 뜨면 전부 등록되고, 운영자 말로만 적는다(내부 식별자 · 영문 API 이름 없음).
/// </summary>
public class DevicesHelpTests
{
    [Fact]
    public void should_register_every_devices_entry_in_the_catalog_when_the_module_loads()
    {
        // Arrange — 이 시험은 Devices.Ui 어셈블리 안에서 돈다 → 모듈 초기화가 이미 돌았다
        var keys = DevicesHelp.Entries.Select(e => e.Key).ToList();

        // Act
        var missing = keys.Where(k => HelpCatalog.Find(k) is null).ToList();

        // Assert
        Assert.Empty(missing);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, k => Assert.StartsWith("Devices.", k));
    }

    [Fact]
    public void should_keep_internal_ids_and_api_names_out_of_every_devices_help_entry_when_operators_read_it()
    {
        // Arrange — PRD · 요구 번호 · 결정 번호 · snake_case API 이름(category_device 등)은 운영자 말이 아니다
        var internalTerm = new Regex(@"\bO-\d+|\bFR-\d+|\bNFR-\d+|PRD|\b[a-z]+_[a-z_]+\b|ViewModel|AutomationId", RegexOptions.CultureInvariant);

        // Act
        var offenders = DevicesHelp.Entries
            .Select(e => (e.Key, Text: e.ToPlainText()))
            .Where(e => internalTerm.IsMatch(e.Text))
            .Select(e => $"{e.Key}: {internalTerm.Match(e.Text).Value}")
            .ToList();

        // Assert
        Assert.Empty(offenders);
    }

    [Fact]
    public void should_give_every_devices_entry_a_title_and_at_least_one_item_when_it_is_shown()
    {
        var empty = DevicesHelp.Entries
            .Where(e => string.IsNullOrWhiteSpace(e.Title) || e.Sections.Sum(s => s.Items.Count) == 0)
            .Select(e => e.Key)
            .ToList();

        Assert.Empty(empty);
    }
}
