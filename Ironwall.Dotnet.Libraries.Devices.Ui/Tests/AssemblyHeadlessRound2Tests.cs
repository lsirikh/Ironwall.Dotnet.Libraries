using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Blocks;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 조립기 헤드리스 2회차(2026-10-11 · 카탈로그 §6.8) — 덮어쓰기 칸이 적용 본문(<c>device_config.component_overrides</c>)으로 가는 길 · 뺀 부품의 null ·
/// 사용 안 함 블록의 그림. 보드(<see cref="AssemblyBoard"/>)가 만든 값을 조립기가 그대로 <c>ComponentApplyService.ApplyAsync</c> 에 넘긴다(AssemblyViewModel.CommitAsync).
/// 창 없음 · 서버 없음.
/// </summary>
public class AssemblyHeadlessRound2Tests
{
    private static AssemblyBoard EnclosureBoard()
    {
        var board = new AssemblyBoard(EnumDeviceCategory.Enclosure, _ => null);
        board.Load(new[]
            {
                new ComponentDefinitionModel { Key = "heater", Type = "HEATER" },
                new ComponentDefinitionModel { Key = "fan", Type = "FAN" },
            },
            JObject.Parse("{\"heater\":{\"enabled\":true,\"on_below_c\":5},\"fan\":{\"enabled\":true}}"));
        return board;
    }

    [Fact]
    [Trait("Scenario", "ASM-024")]
    public void should_send_an_edited_override_under_its_component_key_and_keep_the_others_ASM024()
    {
        var board = EnclosureBoard();
        var heater = board.Slots.Single(s => s.Key == "heater");

        heater.Overrides = new JObject { ["enabled"] = true, ["on_below_c"] = 3 };           // 속성 칸 `.Override.on_below_c` = 3
        var sent = board.ToOverrides();

        Assert.NotNull(sent);
        Assert.Equal(3, (int)sent!.SelectToken("heater.on_below_c")!);
        Assert.Equal(true, (bool)sent.SelectToken("heater.enabled")!);
        Assert.Null(heater.ToDefinition().Spec);                                            // 재정의는 부품 정의에 싣지 않는다(422)
        Assert.Equal(1, board.UnsavedChangeCount);
    }

    [Fact]
    [Trait("Scenario", "ASM-207")]
    public void should_send_null_for_the_override_of_a_removed_component_ASM207()
    {
        var board = EnclosureBoard();

        board.Remove(new[] { board.Slots.Single(s => s.Key == "fan") });
        var sent = board.ToOverrides();

        Assert.NotNull(sent);
        Assert.Equal(JTokenType.Null, sent!["fan"]!.Type);                                   // PATCH 병합 — null 이어야 서버의 fan 재정의가 지워진다
        Assert.Equal(new[] { "heater" }, board.ToDefinitions().Select(d => d.Key));
        Assert.Contains("fan", board.Diff().Removed.Select(r => r.Key));
    }

    #region - 사용 안 함 블록 (ASM-028) -
    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static T OnSta<T>(Func<T> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = body(); }
            catch (Exception ex) { failure = ex; }
            finally { StaCleanup.ShutdownDispatcher(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw new AggregateException(failure);
        return result;
    }

    private static byte[] Render(bool outOfService)
    {
        _ = Application.Current;
        var block = new ComponentBlock { Title = "히터", Subtitle = "heater · HEATER", Family = ComponentFamily.PowerEnvironment, Width = 340, Height = 56, IsOutOfService = outOfService };
        block.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", "Tokens.Light.xaml"))));
        // 부모 없는 블록은 테마(Generic) 기본 스타일을 못 찾는다 — 블록 사전을 직접 병합한다(앱은 Generic 으로 같은 사전을 쓴다)
        block.Resources.MergedDictionaries.Add((ResourceDictionary)Application.LoadComponent(
            new Uri("/Ironwall.Dotnet.Libraries.Devices.Ui;component/Consoles/Assembly/Blocks/ComponentBlock.xaml", UriKind.Relative)));
        block.ApplyTemplate();
        block.Measure(new Size(340, 56));
        block.Arrange(new Rect(0, 0, 340, 56));
        block.UpdateLayout();
        var bitmap = new RenderTargetBitmap(340, 56, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(block);
        var pixels = new byte[340 * 56 * 4];
        bitmap.CopyPixels(pixels, 340 * 4, 0);
        return pixels;
    }

    [Fact]
    [Trait("Scenario", "ASM-028")]
    public void should_draw_an_out_of_service_block_differently_from_an_active_one_ASM028()
    {
        var (active, inactive) = OnSta(() => (Render(false), Render(true)));
        var board = EnclosureBoard();
        var slot = board.Slots.Single(s => s.Key == "fan");
        slot.InService = false;

        var differing = 0;
        var painted = 0;
        for (var i = 0; i < active.Length; i += 4)
        {
            if (active[i + 3] > 0) painted++;
            if (active[i] != inactive[i] || active[i + 1] != inactive[i + 1] || active[i + 2] != inactive[i + 2] || active[i + 3] != inactive[i + 3]) differing++;
        }

        Assert.True(painted > 1000, $"블록이 그려지지 않았다({painted}px)");
        Assert.True(differing > 200, $"사용 안 함 블록이 활성 블록과 같아 보인다(다른 픽셀 {differing})");
        Assert.False(slot.ToDefinition().InService);                                        // 저장 값에도 in_service=false
    }
    #endregion
}
