using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Microsoft.Xaml.Behaviors;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 조립기 시나리오 3회차(2026-10-11 · 카탈로그 §6.8 ASM-003 ~ 022 · 200 ~ 204) — 상태표의 '시험 없음' · '미실행' 줄을 헤드리스로 단언한다.
/// <b>창을 띄우지 않는다</b> — 뷰모델 길 + 조립기 뷰(AssemblyView.xaml)를 느슨하게 읽어 부모 없이 Measure/Arrange 한 것(그 안에 붙은 실제 끌기 · 키보드 행동을 부른다).
/// 서버는 HTTP 가짜(<see cref="CapturingHttp"/>) 위의 진짜 장비 API 서비스로 대신한다.
/// </summary>
public class AssemblyScenarioRound3Tests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ironwall-asm-r3-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
    }

    #region - Rig -
    /// <summary>조립기 시험용 카탈로그 — 함체 · 제어기 · 카메라 · 센서에 달 수 있는 유형을 고루 둔다(서버 없음).</summary>
    private sealed class Round3Catalog : IComponentCatalog
    {
        private static readonly EnumDeviceCategory[] Boxes = { EnumDeviceCategory.Enclosure, EnumDeviceCategory.Controller };

        private readonly List<ComponentTypeInfo> _types = new()
        {
            new() { Code = "DOOR_SENSOR", Label = "문 센서", Family = ComponentFamily.Sensing, AppliesTo = new[] { EnumDeviceCategory.Enclosure, EnumDeviceCategory.Gate }, States = new[] { "OPEN", "CLOSED" } },
            new() { Code = "HEATER", Label = "히터", Family = ComponentFamily.PowerEnvironment, AppliesTo = Boxes, States = new[] { "ON", "OFF" }, OverrideParams = new[] { "enabled", "on_below_c" } },
            new() { Code = "FAN", Label = "팬", Family = ComponentFamily.PowerEnvironment, AppliesTo = Boxes, OverrideParams = new[] { "enabled" } },
            new() { Code = "CONTACT_INPUT", Label = "접점 입력", Family = ComponentFamily.Sensing, AppliesTo = Boxes },
            new() { Code = "PTZ_UNIT", Label = "PTZ 구동부", Family = ComponentFamily.Actuation, AppliesTo = new[] { EnumDeviceCategory.Camera }, States = new[] { "IDLE", "MOVING" } },
            new() { Code = "VIBRATION_SENSOR", Label = "진동 센서", Family = ComponentFamily.Sensing, AppliesTo = new[] { EnumDeviceCategory.Sensor } },
            new() { Code = "NETWORK_INTERFACE", Label = "네트워크", Family = ComponentFamily.Network, AppliesTo = new[] { EnumDeviceCategory.Sensor, EnumDeviceCategory.Controller } },
        };

        public bool IsLoaded => true;
        public event EventHandler? CatalogChanged { add { } remove { } }
        public Task<bool> EnsureLoadedAsync(CancellationToken token = default) => Task.FromResult(true);
        public IReadOnlyList<ComponentTypeInfo> ComponentTypes(EnumDeviceCategory category, bool includeDeprecated = false) => _types.Where(t => t.AppliesToCategory(category)).ToList();
        public ComponentTypeInfo? Find(string? code) => _types.FirstOrDefault(t => string.Equals(t.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    private DevicePresetStore NewStore()
    {
        var store = new DevicePresetStore(Path.Combine(_directory, $"presets-{Guid.NewGuid():N}.json"), () => new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero));
        store.Load();
        return store;
    }

    private async Task<AssemblyViewModel> Compose(EnumDeviceCategory category, AsmFakeDialogs? dialogs = null)
    {
        var vm = AssemblyViewModel.Compose(category, new Round3Catalog(), NewStore(), dialogs ?? new AsmFakeDialogs());
        await ((IActivate)vm).ActivateAsync();
        return vm;
    }

    private static PaletteItemViewModel Palette(AssemblyViewModel vm, string code) => vm.Palette.Single(p => p.Code == code);

    private static DragPayload Payload(params object[] items) => new(null!, items, "test");

    private static ComponentDefinitionModel Part(string key, string type, string? label = null, int? channel = null)
        => new() { Key = key, Type = type, Label = label, Channel = channel };

    private static DeviceAxesModel AxesWith(params ComponentDefinitionModel[] parts)
    {
        var spec = new HardwareSpecModel();
        foreach (var part in parts) spec.Components.Add(part);
        return new DeviceAxesModel { HardwareSpec = spec };
    }

    private static IReadOnlyList<string> Keys(AssemblyViewModel vm) => vm.BoardItems.Select(i => i.Slot.Key).ToList();

    /// <summary>서버가 돌려줄 제어기 한 건(부품 배열이 실린 축 계약 응답).</summary>
    private static ControllerDeviceDto ServerController(IEnumerable<ComponentDefinitionModel> parts)
    {
        var dto = new ControllerDeviceDto
        {
            Id = 7, NumberDevice = 1, NameDevice = "북측 제어기", Status = "ACTIVATED", IsEnable = true, IpAddress = "10.99.7.1", IpPort = 5000,
            HardwareSpec = new HardwareSpecDto { Manufacturer = "Sensorway", Model = "SC-1", Components = parts.Select(PresetRequestBuilder.ToComponentDto).ToList() },
        };
        dto.UseAxisWrite = true;
        dto.HardwareSpec.AllowComponentsWrite = true;
        return dto;
    }

    private static string Envelope(object dto) => new JObject { ["success"] = true, ["data"] = JObject.FromObject(dto), ["meta"] = new JObject() }.ToString();

    private static ComponentApplyService ApplyOver(CapturingHttp http)
        => new(http.Real(), new MockDeviceProviderService(), new MockLogService(), WiringDoubles.AxisPolicy());

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

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static ResourceDictionary Theme(string file)
        => (ResourceDictionary)XamlReader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "Ironwall.Dotnet.Libraries.Theme", "Themes", file)));

    /// <summary>조립기 뷰를 x:Class · 코드 이벤트 없이 읽는다(앱 스타일 병합) — XAML 에 붙은 끌기 · 키보드 행동과 바인딩은 그대로 산다.</summary>
    private static UserControl LooseAssemblyView()
    {
        System.Reflection.Assembly.Load("MahApps.Metro");
        _ = typeof(MaterialDesignThemes.Wpf.PackIcon);
        _ = typeof(Interaction);
        var root = RepoRoot();
        var styles = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Theme", "Themes", "Styles.Console.xaml"));
        styles = System.Text.RegularExpressions.Regex.Replace(styles, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Theme""");
        var stylesPath = Path.Combine(Path.GetTempPath(), $"assembly-r3-styles-{Guid.NewGuid():N}.xaml");
        File.WriteAllText(stylesPath, styles);

        var xaml = File.ReadAllText(Path.Combine(root, "Ironwall.Dotnet.Libraries.Devices.Ui", "Consoles", "Assembly", "AssemblyView.xaml"));
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"\sx:Class=""[^""]*""", string.Empty);
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"xmlns:(\w+)=""clr-namespace:([^"";]+)""", @"xmlns:$1=""clr-namespace:$2;assembly=Ironwall.Dotnet.Libraries.Devices.Ui""");
        xaml = System.Text.RegularExpressions.Regex.Replace(xaml, @"\s(Click|PreviewKeyDown|KeyDown|SelectionChanged|Loaded)=""[^""]*""", string.Empty);
        xaml = xaml.Replace("xmlns:md=\"http://materialdesigninxaml.net/winfx/xaml/themes\"", "xmlns:md=\"clr-namespace:MaterialDesignThemes.Wpf;assembly=MaterialDesignThemes.Wpf\"");
        xaml = xaml.Replace("<UserControl.Resources>",
            $"<UserControl.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceDictionary Source=\"{new Uri(stylesPath).AbsoluteUri}\" /></ResourceDictionary.MergedDictionaries>");
        xaml = xaml.Replace("</UserControl.Resources>", "</ResourceDictionary></UserControl.Resources>");
        try { return (UserControl)XamlReader.Parse(xaml); }
        finally { try { File.Delete(stylesPath); } catch (IOException) { } }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var deeper in Descendants<T>(child)) yield return deeper;
        }
    }

    private static T Find<T>(DependencyObject root, string id) where T : FrameworkElement
        => Descendants<T>(root).First(e => AutomationProperties.GetAutomationId(e) == id);

    private static void Layout(FrameworkElement element, double width = 1280, double height = 760)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

    /// <summary>뷰모델을 묶은 조립기 뷰(부모 없음).</summary>
    private static UserControl ViewOf(AssemblyViewModel vm)
    {
        _ = Application.Current;
        var view = LooseAssemblyView();
        view.Resources.MergedDictionaries.Add(Theme("Tokens.Light.xaml"));
        view.DataContext = vm;
        Layout(view);
        Layout(view);
        return view;
    }

    /// <summary>STA 에서 조립기를 연다(활성화까지) — 동기 대기라 STA 스레드 안에서 부른다.</summary>
    private AssemblyViewModel ComposeSync(EnumDeviceCategory category, AsmFakeDialogs? dialogs = null)
        => Compose(category, dialogs).GetAwaiter().GetResult();
    #endregion

    #region - 열기 · 팔레트 (ASM-003 · 004 · 005 · 020) -
    [Theory]
    [InlineData(EnumDeviceCategory.Controller, "제어기 · 새로 조립", "HEATER")]
    [InlineData(EnumDeviceCategory.Enclosure, "함체 · 새로 조립", "DOOR_SENSOR")]
    [Trait("Scenario", "ASM-003,ASM-004")]
    public async Task should_title_a_new_assembly_with_the_category_and_offer_only_its_parts_ASM003_ASM004(EnumDeviceCategory category, string subject, string offered)
    {
        var vm = await Compose(category);

        Assert.Equal(subject, vm.Subject);                                                  // `Devices.Assembly.Subject`
        Assert.Equal("등록…", vm.PrimaryText);
        Assert.Contains(vm.Palette, p => p.Code == offered);
        Assert.DoesNotContain(vm.Palette, p => p.Code == "PTZ_UNIT");                      // 카메라 전용은 제어기 · 함체 팔레트에 없다
        Assert.True(vm.IsBoardEmpty);
        Assert.False(vm.CanCommit);
        Assert.Equal("보드에 부품을 먼저 다세요.", vm.CommitBlockedReason);
    }

    [Fact]
    [Trait("Scenario", "ASM-005")]
    public async Task should_filter_the_palette_by_code_or_label_and_show_all_again_when_the_search_is_cleared_ASM005()
    {
        var vm = await Compose(EnumDeviceCategory.Enclosure);
        var all = vm.Palette.Select(p => p.Code).ToList();

        vm.PaletteSearch = "DOOR";
        var byCode = vm.Palette.Select(p => p.Code).ToList();
        vm.PaletteSearch = "히터";
        var byLabel = vm.Palette.Select(p => p.Code).ToList();
        vm.PaletteSearch = "없는부품";
        var none = (vm.IsPaletteEmpty, vm.PaletteEmptyText);
        vm.PaletteSearch = "";

        Assert.Equal(new[] { "DOOR_SENSOR" }, byCode);
        Assert.Equal(new[] { "HEATER" }, byLabel);
        Assert.Equal((true, "이 카테고리에 달 수 있는 부품이 없습니다."), none);
        Assert.Equal(all, vm.Palette.Select(p => p.Code));
        Assert.True(all.Count >= 3);
    }

    [Fact]
    [Trait("Scenario", "ASM-020")]
    public async Task should_swap_the_palette_with_the_category_keep_the_board_and_name_parts_that_do_not_fit_then_come_back_ASM020()
    {
        var vm = await Compose(EnumDeviceCategory.Enclosure);
        vm.AddFromPalette(Palette(vm, "DOOR_SENSOR"));
        var enclosure = vm.Palette.Select(p => p.Code).ToList();

        vm.Category = EnumDeviceCategory.Camera;
        var camera = (Palette: vm.Palette.Select(p => p.Code).ToList(), vm.Subject, Board: Keys(vm).ToList(), vm.Problems, vm.CanCommit);
        vm.Category = EnumDeviceCategory.Enclosure;

        Assert.Equal(new[] { "PTZ_UNIT" }, camera.Palette);
        Assert.Equal("카메라 · 새로 조립", camera.Subject);
        Assert.Equal(new[] { "door" }, camera.Board);                                       // 말없이 빼지 않는다
        Assert.Contains("'DOOR_SENSOR'은(는) 이 카테고리에 달 수 없습니다.", camera.Problems);
        Assert.False(camera.CanCommit);
        Assert.Equal(enclosure, vm.Palette.Select(p => p.Code));                            // 원래로 — 팔레트도 문제도 돌아온다
        Assert.Empty(vm.Problems);
        Assert.True(vm.CanCommit);
    }
    #endregion

    #region - 달기 · 끌기 (ASM-006 · 007 · 008 · 012 · 014 · 015) -
    [Fact]
    [Trait("Scenario", "ASM-006")]
    public void should_add_the_part_at_the_board_end_say_so_and_show_its_slot_in_the_view_when_enter_is_pressed_ASM006()
    {
        var result = OnSta(() =>
        {
            var vm = ComposeSync(EnumDeviceCategory.Enclosure);
            vm.AddFromPalette(Palette(vm, "HEATER"));
            var view = ViewOf(vm);

            vm.AddFromPaletteCommand.Execute(Palette(vm, "DOOR_SENSOR"));                   // 팔레트 Enter = 이 명령(키보드 행동)
            Layout(view);
            var slot = Descendants<FrameworkElement>(view).FirstOrDefault(e => AutomationProperties.GetAutomationId(e) == "Devices.Assembly.Slot.door");
            var status = Find<TextBlock>(view, "Devices.Assembly.Status").Text;
            return (Keys: Keys(vm).ToList(), vm.StatusText, Status: status, SlotShown: slot is not null, Inspected: vm.Inspected?.Slot.Key);
        });

        Assert.Equal(new[] { "heater", "door" }, result.Keys);                             // 보드 끝
        Assert.Equal("'문 센서'을(를) 추가했습니다(door).", result.StatusText);
        Assert.Equal(result.StatusText, result.Status);                                     // 화면 상태 줄 `Devices.Assembly.Status`
        Assert.True(result.SlotShown);                                                      // `Devices.Assembly.Slot.door`
        Assert.Equal("door", result.Inspected);                                             // 방금 단 것이 속성 칸에
    }

    [Fact]
    [Trait("Scenario", "ASM-007")]
    public async Task should_build_the_same_board_when_palette_blocks_are_dropped_as_when_enter_is_pressed_ASM007()
    {
        var byKey = await Compose(EnumDeviceCategory.Enclosure);
        byKey.AddFromPalette(Palette(byKey, "DOOR_SENSOR"));
        byKey.AddFromPalette(Palette(byKey, "HEATER"));
        byKey.AddFromPalette(Palette(byKey, "DOOR_SENSOR"));

        var byDrag = await Compose(EnumDeviceCategory.Enclosure);
        foreach (var code in new[] { "DOOR_SENSOR", "HEATER", "DOOR_SENSOR" })
        {
            // 커널은 순서 드롭존(보드)에 놓으면 삽입 틈(0..Count)을 준다 — 맨 아래에 놓은 것
            var target = new DropTarget(AssemblyViewModel.BoardZoneKey, byDrag, byDrag.BoardItems.Count);
            Assert.True(byDrag.CanDrop(Payload(Palette(byDrag, code)), target));
            byDrag.Drop(Payload(Palette(byDrag, code)), target);
        }

        Assert.Equal(Keys(byKey), Keys(byDrag));
        Assert.Equal(byKey.BoardItems.Select(i => i.Slot.TypeCode), byDrag.BoardItems.Select(i => i.Slot.TypeCode));
        Assert.Equal(byKey.Inspected?.Slot.Key, byDrag.Inspected?.Slot.Key);               // 둘 다 마지막에 단 것을 고른다
        Assert.Equal(byKey.FooterText, byDrag.FooterText);
    }

    [Fact]
    [Trait("Scenario", "ASM-008")]
    public void should_treat_five_units_as_a_click_and_twelve_as_a_drag_on_both_drag_sources_of_the_assembler_ASM008()
    {
        var sources = OnSta(() =>
        {
            var vm = ComposeSync(EnumDeviceCategory.Enclosure);
            vm.AddFromPalette(Palette(vm, "DOOR_SENSOR"));
            var view = ViewOf(vm);
            int CountOn(string id) => Interaction.GetBehaviors(Find<ListBox>(view, id)).OfType<CaptureDragBehavior>().Count();
            return (Palette: CountOn("Devices.Assembly.Palette"), Board: CountOn("Devices.Assembly.Board"));
        });

        Assert.Equal(8.0, DragMath.DeadZone);                                               // 전 앱 한 값
        Assert.False(DragMath.IsDrag(5, 0));                                                // 5 DIU = 클릭(속성 칸에 올리기)
        Assert.False(DragMath.IsDrag(3, 4));
        Assert.True(DragMath.IsDrag(12, 0));                                                // 12 DIU = 끌기
        Assert.True(DragMath.IsDrag(0, -12));
        Assert.Equal((1, 1), sources);                                                      // 팔레트 · 보드 둘 다 같은 커널 끌기(같은 데드존)
    }

    [Fact]
    [Trait("Scenario", "ASM-012")]
    public async Task should_remove_the_same_way_and_say_the_same_when_slots_are_dropped_on_the_bin_as_with_the_remove_button_ASM012()
    {
        async Task<AssemblyViewModel> Three()
        {
            var vm = await Compose(EnumDeviceCategory.Enclosure);
            vm.AddFromPalette(Palette(vm, "DOOR_SENSOR"));
            vm.AddFromPalette(Palette(vm, "HEATER"));
            vm.AddFromPalette(Palette(vm, "FAN"));
            return vm;
        }
        var button = await Three();
        button.OnBoardSelectionChanged(new[] { button.BoardItems[0], button.BoardItems[2] });
        button.RemoveSelected();

        var bin = await Three();
        var payload = Payload(bin.BoardItems[0], bin.BoardItems[2]);
        var target = new DropTarget(AssemblyViewModel.BinZoneKey, bin, -1);
        Assert.True(bin.CanDrop(payload, target));
        bin.Drop(payload, target);

        Assert.Equal(new[] { "heater" }, Keys(button));
        Assert.Equal(Keys(button), Keys(bin));
        Assert.Equal("2개를 뺐습니다 — [되돌리기]로 다시 넣을 수 있습니다.", bin.StatusText);
        Assert.Equal(button.StatusText, bin.StatusText);
        Assert.True(bin.CanUndo);
    }

    [Fact]
    [Trait("Scenario", "ASM-014")]
    public async Task should_leave_the_board_untouched_while_a_drag_only_hovers_and_is_then_cancelled_with_esc_ASM014()
    {
        // 끄는 동안 커널은 CanDrop 만 묻는다(놓을 자리 표시). Esc = 끌기 취소 — Drop 을 부르지 않는다(커널 CaptureDragBehavior.OnPreviewKeyDown → CancelDrag).
        // 그러니 '보드 그대로'는 CanDrop 이 아무것도 바꾸지 않는다는 뜻이다.
        var vm = await Compose(EnumDeviceCategory.Enclosure);
        vm.AddFromPalette(Palette(vm, "DOOR_SENSOR"));
        vm.AddFromPalette(Palette(vm, "HEATER"));
        var before = (Keys: Keys(vm).ToList(), vm.StatusText, vm.CanUndo, vm.FooterText, Inspected: vm.Inspected);
        var undoBefore = vm.CanUndo;

        foreach (var index in new[] { 0, 1, 2 })
        {
            vm.CanDrop(Payload(vm.BoardItems[1]), new DropTarget(AssemblyViewModel.BoardZoneKey, vm, index));
            vm.CanDrop(Payload(Palette(vm, "FAN")), new DropTarget(AssemblyViewModel.BoardZoneKey, vm, index));
        }
        vm.CanDrop(Payload(vm.BoardItems[0]), new DropTarget(AssemblyViewModel.BinZoneKey, vm, -1));
        vm.Undo();                                                                          // 되돌릴 것도 끌기 전 그대로(HEATER 추가 한 칸)

        Assert.Equal(new[] { "door", "heater" }, before.Keys);
        Assert.True(undoBefore);
        Assert.Equal(new[] { "door" }, Keys(vm));                                           // 되돌리기 한 칸 = 끌기 전의 마지막 조작
        Assert.Equal("되돌렸습니다.", vm.StatusText);
    }

    [Fact]
    [Trait("Scenario", "ASM-015")]
    public void should_ignore_a_drop_outside_the_board_and_bin_such_as_back_on_the_palette_ASM015()
    {
        var result = OnSta(() =>
        {
            var vm = ComposeSync(EnumDeviceCategory.Enclosure);
            vm.AddFromPalette(Palette(vm, "DOOR_SENSOR"));
            vm.AddFromPalette(Palette(vm, "HEATER"));
            var view = ViewOf(vm);
            var palette = Find<ListBox>(view, "Devices.Assembly.Palette");
            var paletteZone = DropZone.GetKey(palette);
            var before = Keys(vm).ToList();

            var slotOnPalette = vm.CanDrop(Payload(vm.BoardItems[0]), new DropTarget("assembly-palette", vm, -1));
            vm.Drop(Payload(vm.BoardItems[0]), new DropTarget("assembly-palette", vm, -1));
            var blockOnBin = vm.CanDrop(Payload(Palette(vm, "FAN")), new DropTarget(AssemblyViewModel.BinZoneKey, vm, -1));
            vm.Drop(Payload(Palette(vm, "FAN")), new DropTarget(AssemblyViewModel.BinZoneKey, vm, -1));
            return (paletteZone, slotOnPalette, blockOnBin, before, After: Keys(vm).ToList(), vm.StatusText);
        });

        Assert.True(string.IsNullOrEmpty(result.paletteZone));                              // 팔레트는 놓는 곳이 아니다(커널이 Drop 을 부르지 않는다)
        Assert.False(result.slotOnPalette);
        Assert.False(result.blockOnBin);                                                    // 팔레트 블록을 빼는 곳에 — 거절
        Assert.Equal(result.before, result.After);
        Assert.Equal("'히터'을(를) 추가했습니다(heater).", result.StatusText);               // 상태 줄도 그대로
    }
    #endregion

    #region - 순서 · 빼기 · 되돌리기 (ASM-009 · 010 · 011 · 016 · 200) -
    /// <summary>
    /// 보드 목록에 붙은 <see cref="ReorderKeyboardBehavior"/>(Alt+↑/↓)로 고른 칸을 옮긴다 — XAML 의 실제 배선(Handler=조립기)을 지난다.
    /// </summary>
    private static (bool Moved, IReadOnlyList<string> Keys) AltArrow(UserControl view, AssemblyViewModel vm, int index, int direction)
    {
        var board = Find<ListBox>(view, "Devices.Assembly.Board");
        var reorder = Interaction.GetBehaviors(board).OfType<ReorderKeyboardBehavior>().Single();
        board.SelectedItems.Clear();
        board.SelectedItem = vm.BoardItems[index];
        var moved = reorder.MoveSelection(direction);
        Layout(view);
        return (moved, Keys(vm).ToList());
    }

    [Theory]
    [InlineData(2, -1)]                                                                     // 마지막 칸 ↑ — 위 칸의 위쪽 절반에 놓은 것
    [InlineData(0, 1)]                                                                      // 첫 칸 ↓ — 아래 칸의 아래쪽 절반에 놓은 것
    [InlineData(1, 1)]
    [Trait("Scenario", "ASM-009")]
    public void should_reorder_the_same_way_whether_a_slot_handle_is_dropped_on_a_row_half_or_alt_arrow_is_pressed_ASM009(int index, int direction)
    {
        var result = OnSta(() =>
        {
            AssemblyViewModel Three()
            {
                var vm = ComposeSync(EnumDeviceCategory.Enclosure);
                vm.AddFromPalette(Palette(vm, "DOOR_SENSOR"));
                vm.AddFromPalette(Palette(vm, "HEATER"));
                vm.AddFromPalette(Palette(vm, "FAN"));
                return vm;
            }

            // 손잡이 끌기 — 커널은 놓은 행의 위/아래 절반으로 삽입 틈을 정한다(DragMath.InsertionIndex)
            var dragVm = Three();
            var dragView = ViewOf(dragVm);
            var board = Find<ListBox>(dragView, "Devices.Assembly.Board");
            var rows = dragVm.BoardItems.Select(i => (FrameworkElement)board.ItemContainerGenerator.ContainerFromItem(i))
                .Select(r => new Rect(r.TranslatePoint(new Point(0, 0), board), new Size(r.ActualWidth, r.ActualHeight))).ToList();
            var over = rows[index + direction];
            var y = direction < 0 ? over.Top + over.Height * 0.25 : over.Top + over.Height * 0.75;
            var insertion = DragMath.InsertionIndex(rows, y);
            dragVm.Drop(Payload(dragVm.BoardItems[index]), new DropTarget(AssemblyViewModel.BoardZoneKey, dragVm, insertion));

            var keyVm = Three();
            var keyView = ViewOf(keyVm);
            var key = AltArrow(keyView, keyVm, index, direction);
            return (Drag: Keys(dragVm).ToList(), key.Moved, key.Keys, RowsHigh: rows.All(r => r.Height > 10));
        });

        Assert.True(result.RowsHigh, "보드 행이 그려지지 않았다");
        Assert.True(result.Moved);
        Assert.Equal(result.Drag, result.Keys);
        var expected = new List<string> { "door", "heater", "fan" };
        var moving = expected[index];
        expected.RemoveAt(index);
        expected.Insert(index + direction, moving);
        Assert.Equal(expected, result.Keys);
    }

    [Fact]
    [Trait("Scenario", "ASM-010")]
    public void should_move_the_last_slot_one_up_with_alt_up_and_stay_put_at_the_top_ASM010()
    {
        var result = OnSta(() =>
        {
            var vm = ComposeSync(EnumDeviceCategory.Enclosure);
            vm.AddFromPalette(Palette(vm, "DOOR_SENSOR"));
            vm.AddFromPalette(Palette(vm, "HEATER"));
            vm.AddFromPalette(Palette(vm, "FAN"));
            var view = ViewOf(vm);

            var up = AltArrow(view, vm, 2, -1);
            var again = AltArrow(view, vm, 1, -1);
            var top = AltArrow(view, vm, 0, -1);
            return (up, again, top);
        });

        Assert.True(result.up.Moved);
        Assert.Equal(new[] { "door", "fan", "heater" }, result.up.Keys);                    // 한 칸 위
        Assert.Equal(new[] { "fan", "door", "heater" }, result.again.Keys);
        Assert.False(result.top.Moved);                                                     // 맨 위에서는 그대로
        Assert.Equal(result.again.Keys, result.top.Keys);
    }

    [Fact]
    [Trait("Scenario", "ASM-011,ASM-016")]
    public async Task should_say_removed_and_then_undone_and_bring_back_the_same_slots_ASM011_ASM016()
    {
        var vm = await Compose(EnumDeviceCategory.Enclosure);
        vm.AddFromPalette(Palette(vm, "DOOR_SENSOR"));
        vm.AddFromPalette(Palette(vm, "HEATER"));
        var heater = vm.BoardItems[1];
        vm.OnBoardSelectionChanged(new[] { heater });
        var canRemove = vm.CanRemoveSelected;

        vm.RemoveSelected();                                                                // `.RemoveSelected`
        var removed = (Keys: Keys(vm).ToList(), vm.StatusText, vm.FooterText);
        vm.Undo();                                                                          // `.Undo`

        Assert.True(canRemove);
        Assert.Equal(new[] { "door" }, removed.Keys);
        Assert.Equal("1개를 뺐습니다 — [되돌리기]로 다시 넣을 수 있습니다.", removed.StatusText);
        Assert.Equal("부품 1 · 미저장 변경 1", removed.FooterText);                          // 새 조립은 빈 보드가 기준
        Assert.Equal("되돌렸습니다.", vm.StatusText);
        Assert.Equal(new[] { "door", "heater" }, Keys(vm));
        Assert.Same(heater, vm.BoardItems[1]);
    }

    [Fact]
    [Trait("Scenario", "ASM-200")]
    public void should_keep_apply_off_when_editing_device_components_only_reorders_them_with_alt_up_ASM200()
    {
        var result = OnSta(() =>
        {
            var device = new ControllerDeviceModel { Id = 7, DeviceNumber = 1, DeviceName = "북측 제어기", Axes = AxesWith(Part("heater", "HEATER"), Part("fan", "FAN"), Part("nic", "NETWORK_INTERFACE")) };
            var vm = AssemblyViewModel.ForDevice(device, EnumDeviceCategory.Controller, new Round3Catalog(), NewStore(), ApplyOver(new CapturingHttp()), new AsmFakeDialogs());
            ((IActivate)vm).ActivateAsync().GetAwaiter().GetResult();
            var view = ViewOf(vm);

            var moved = AltArrow(view, vm, 2, -1);                                          // EditComponents → Alt+↑
            var commit = Find<Button>(view, "Devices.Assembly.Commit");
            return (moved, vm.CanCommit, vm.CommitBlockedReason, vm.DiffSummary, vm.FooterText, vm.PrimaryText, Enabled: commit.IsEnabled, Tip: commit.ToolTip as string);
        });

        Assert.True(result.moved.Moved);
        Assert.Equal(new[] { "heater", "nic", "fan" }, result.moved.Keys);
        Assert.False(result.CanCommit);                                                     // 서버에 순서 계약이 없다 — 보기 순서일 뿐
        Assert.False(result.Enabled);                                                       // [적용] 꺼짐
        Assert.Equal("바뀐 내용이 없습니다.", result.CommitBlockedReason);
        Assert.Equal(result.CommitBlockedReason, result.Tip);                               // 꺼진 까닭이 툴팁에
        Assert.Equal("바뀐 내용이 없습니다.", result.DiffSummary);
        Assert.Equal("부품 3 · 미저장 변경 0", result.FooterText);
        Assert.Equal("적용", result.PrimaryText);
    }
    #endregion

    #region - 반복 펼치기 · 다시 번호 (ASM-017 · 018 · 019) -
    [Fact]
    [Trait("Scenario", "ASM-017")]
    public async Task should_add_three_numbered_slots_and_say_expanded_and_refuse_a_colliding_second_round_ASM017()
    {
        var dialogs = new AsmFakeDialogs { RepeatAnswer = new RepeatExpandSpec("CONTACT_INPUT", 3, 1, "contact_{02d}") };
        var vm = await Compose(EnumDeviceCategory.Controller, dialogs);
        vm.AddFromPalette(Palette(vm, "HEATER"));

        await vm.RepeatExpandAsync();                                                       // `.RepeatExpand` → 3
        var first = (Keys: Keys(vm).ToList(), Channels: vm.BoardItems.Select(i => i.Slot.Channel).ToList(), vm.StatusText);
        await vm.RepeatExpandAsync();                                                       // 같은 규칙 다시 — key 가 겹친다

        Assert.Equal(new[] { "heater", "contact_01", "contact_02", "contact_03" }, first.Keys);   // +3
        Assert.Equal(new int?[] { null, 1, 2, 3 }, first.Channels);
        Assert.Equal("3개를 펼쳤습니다.", first.StatusText);
        Assert.Equal(first.Keys, Keys(vm));                                                 // 전부 아니면 전무
        Assert.Equal("펼치지 못했습니다. 식별 이름이 겹치거나 규칙이 올바르지 않습니다.", vm.StatusText);
        vm.Undo();
        Assert.Equal(new[] { "heater" }, Keys(vm));                                         // 펼치기 한 번 = 되돌리기 한 칸
    }

    [Fact]
    [Trait("Scenario", "ASM-018")]
    public async Task should_preview_the_keys_and_channels_mark_collisions_and_leave_the_board_alone_when_the_repeat_dialog_is_cancelled_ASM018()
    {
        var palette = new Round3Catalog().ComponentTypes(EnumDeviceCategory.Controller).Select(i => new PaletteItemViewModel(i)).ToList();
        var dialog = new RepeatExpandViewModel(palette, palette.Single(p => p.Code == "CONTACT_INPUT"), new[] { "ci_02" });

        var suggested = dialog.KeyFormat;
        dialog.Count = "3";                                                                 // `.Repeat.Count`
        var withConflict = (Rows: dialog.Preview.Select(r => (r.Key, r.Channel, r.IsConflict)).ToList(), dialog.ConflictCount, dialog.CanExpand);
        dialog.KeyFormat = "relay_{d}";                                                     // `.Repeat.KeyFormat`
        dialog.StartChannel = "5";
        var renamed = (Rows: dialog.Preview.Select(r => (r.Key, r.Channel)).ToList(), dialog.CanExpand);
        dialog.KeyFormat = "relay";                                                         // 번호 자리 없음
        var broken = (dialog.Error, dialog.Preview.Count, dialog.CanExpand);
        await dialog.CancelAsync();

        Assert.Equal("ci_{02d}", suggested);                                                // 유형에 맞춘 제안(AssemblyKeyRules: CONTACT_INPUT → ci)
        Assert.Equal(new[] { ("ci_01", 1, false), ("ci_02", 2, true), ("ci_03", 3, false) }, withConflict.Rows);
        Assert.Equal((1, false), (withConflict.ConflictCount, withConflict.CanExpand));
        Assert.Equal(new[] { ("relay_5", 5), ("relay_6", 6), ("relay_7", 7) }, renamed.Rows);   // 미리보기 키 목록 `.Repeat.Preview`
        Assert.True(renamed.CanExpand);
        Assert.Equal(("식별 이름 규칙에 {d} 또는 {02d} 같은 번호 자리를 넣으세요.", 0, false), broken);
        Assert.Null(dialog.Result);                                                         // 취소 = 규칙 없음

        // 조립기는 취소된 창(null)을 받으면 보드도 상태 줄도 그대로 둔다
        var vm = await Compose(EnumDeviceCategory.Controller, new AsmFakeDialogs { RepeatAnswer = dialog.Result });
        vm.AddFromPalette(Palette(vm, "HEATER"));
        var status = vm.StatusText;
        await vm.RepeatExpandAsync();
        Assert.Equal(new[] { "heater" }, Keys(vm));
        Assert.Equal(status, vm.StatusText);
    }

    [Fact]
    [Trait("Scenario", "ASM-019")]
    public async Task should_renumber_the_channels_from_one_in_board_order_and_say_so_ASM019()
    {
        var dialogs = new AsmFakeDialogs { RepeatAnswer = new RepeatExpandSpec("CONTACT_INPUT", 3, 4, "ci_{02d}") };
        var vm = await Compose(EnumDeviceCategory.Controller, dialogs);
        await vm.RepeatExpandAsync();                                                       // 채널 4 · 5 · 6
        vm.Drop(Payload(vm.BoardItems[2]), new DropTarget(AssemblyViewModel.BoardZoneKey, vm, 0));   // ci_06 을 맨 위로
        vm.AddFromPalette(Palette(vm, "HEATER"));                                           // 채널 없는 부품은 번호를 받지 않는다
        var can = vm.CanRenumberChannels;

        vm.RenumberChannels();                                                              // `.Renumber`

        Assert.True(can);
        Assert.Equal("채널을 보드 순서대로 1번부터 다시 매겼습니다.", vm.StatusText);
        Assert.Equal(new[] { ("ci_06", (int?)1), ("ci_04", 2), ("ci_05", 3), ("heater", null) },
                     vm.BoardItems.Select(i => (i.Slot.Key, i.Slot.Channel)));
    }
    #endregion

    #region - 속성 칸 검증 (ASM-021) -
    [Fact]
    [Trait("Scenario", "ASM-021")]
    public void should_disable_the_primary_button_with_the_reason_as_its_tooltip_when_the_key_is_blank_ASM021()
    {
        var result = OnSta(() =>
        {
            var vm = ComposeSync(EnumDeviceCategory.Controller);
            vm.AddFromPalette(Palette(vm, "HEATER"));
            var view = ViewOf(vm);
            var commit = Find<Button>(view, "Devices.Assembly.Commit");
            var before = commit.IsEnabled;

            vm.Inspected!.Slot.Key = "";                                                    // `.Field.key` 비움
            Layout(view);
            var blank = (commit.IsEnabled, Tip: commit.ToolTip as string, vm.Inspected.ErrorText);
            vm.Inspected.Slot.Key = "heater";
            Layout(view);
            return (before, blank, After: commit.IsEnabled);
        });

        Assert.True(result.before);
        Assert.False(result.blank.IsEnabled);
        Assert.Equal("식별 이름을 입력하세요.", result.blank.Tip);                             // 꺼진 까닭이 툴팁(ShowOnDisabled)
        Assert.Equal("식별 이름을 입력하세요.", result.blank.ErrorText);
        Assert.True(result.After);
    }

    [Fact]
    [Trait("Scenario", "ASM-021")]
    public void should_disable_the_primary_button_with_the_reason_as_its_tooltip_when_the_channel_text_is_not_a_number_ASM021()
    {
        // 「1a」 를 받으면 값은 옛 채널 그대로 두고 까닭을 보인다(BoardSlotViewModel.ChannelInput) — 그런데 그대로 [등록] 하면
        // 화면은 1a 인데 나가는 값은 옛 채널이다. 그래서 주 단추가 꺼지고 툴팁이 그 까닭을 말해야 한다(카탈로그 ASM-021 · SC-ASM-009).
        var result = OnSta(() =>
        {
            var vm = ComposeSync(EnumDeviceCategory.Controller);
            vm.AddFromPalette(Palette(vm, "CONTACT_INPUT"));
            var item = vm.Inspected!;
            item.ChannelInput = "3";
            var view = ViewOf(vm);
            var commit = Find<Button>(view, "Devices.Assembly.Commit");
            var before = (commit.IsEnabled, vm.CanSaveAsPreset);

            item.ChannelInput = "1a";                                                       // `.Field.channel`
            Layout(view);
            var bad = (commit.IsEnabled, vm.CanCommit, Tip: commit.ToolTip as string, vm.CanSaveAsPreset, item.ChannelError, item.Slot.Channel);
            item.ChannelInput = "4";
            Layout(view);
            return (before, bad, After: (commit.IsEnabled, vm.CommitBlockedReason, vm.CanSaveAsPreset));
        });

        Assert.Equal((true, true), result.before);
        Assert.False(result.bad.CanCommit);
        Assert.False(result.bad.IsEnabled);
        Assert.Equal("채널은 0 이상의 정수로 입력하세요. 값은 바꾸지 않았습니다.", result.bad.ChannelError);
        Assert.Equal(result.bad.ChannelError, result.bad.Tip);
        Assert.False(result.bad.CanSaveAsPreset);                                           // 프리셋으로 저장도 같은 까닭으로 막는다
        Assert.Equal(3, result.bad.Channel);
        Assert.Equal((true, (string?)null, true), result.After);
    }
    #endregion

    #region - 도움말 "?" · F1 (ASM-300 · 301) -
    /// <summary>HWND 없는 입력 원천 — 창을 띄우지 않고 "?" 가 바깥 클릭 · Esc 를 들을 뿌리를 준다(Reports 시험과 같은 방법).</summary>
    private sealed class HeadlessSource : PresentationSource
    {
        private Visual? _root;
        public override Visual RootVisual
        {
            get => _root!;
            set { var old = _root; _root = value; RootChanged(old!, value); }
        }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    private static bool PressEscape(HeadlessSource source, UIElement target)
    {
        var args = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, Environment.TickCount, System.Windows.Input.Key.Escape)
        { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);
        return args.Handled;
    }

    private static void ClickOn(UIElement target)
        => target.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
        { RoutedEvent = UIElement.PreviewMouseDownEvent });

    /// <summary>조립기 뷰의 "?" 를 모두 모아 차례로 연다 — 목록 · 열기 · 하나씩 · Esc · 바깥 클릭 · F1.</summary>
    private sealed record HelpProbe(List<string> Keys, List<string> Titles, bool OneAtATime, bool EscClosed, bool EscPassesWhenClosed, bool OutsideClosed,
                                    string? F1FromBoard, string? F1FromInspector, string? F1FromHeader, List<string> Bodies);

    private HelpProbe ProbeAssemblyHelp()
        => OnSta(() =>
        {
            var vm = ComposeSync(EnumDeviceCategory.Controller);
            vm.AddFromPalette(Palette(vm, "HEATER"));                                       // 속성 칸 · 이 부품 설정 칸이 서도록
            var view = ViewOf(vm);
            HelpTip.SetHandlesF1(view, true);                                               // 콘솔 창 뿌리처럼(ConsoleWindow 가 켠다)
            var source = new HeadlessSource { RootVisual = view };
            try
            {
                var tips = Descendants<HelpTip>(view).ToList();
                foreach (var tip in tips) tip.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, tip));   // 렌더 펌프가 없다 — 범위 등록(Loaded)을 직접 울린다

                var oneAtATime = true;
                var bodies = new List<string>();
                foreach (var tip in tips)
                {
                    var previous = HelpTip.Current;
                    tip.Open();
                    oneAtATime &= ReferenceEquals(tip, HelpTip.Current) && (previous is null || ReferenceEquals(previous, tip) || previous.IsChecked != true);
                    bodies.Add(tip.Callout is { } callout ? AutomationProperties.GetAutomationId(callout) : $"Help.{tip.HelpKey}.Body");
                }
                var last = tips[^1];
                var escClosed = PressEscape(source, last) && HelpTip.Current is null && last.IsChecked != true;
                var escPasses = !PressEscape(source, last);

                tips[0].Open();
                ClickOn(Find<ListBox>(view, "Devices.Assembly.Board"));                     // 바깥 클릭
                var outsideClosed = HelpTip.Current is null;

                string? F1(string id)
                {
                    HelpTip.CloseCurrent();
                    var target = Descendants<FrameworkElement>(view).First(e => AutomationProperties.GetAutomationId(e) == id);
                    System.Windows.Input.ApplicationCommands.Help.Execute(null, target);
                    return HelpTip.Current?.HelpKey;
                }
                var fromBoard = F1("Devices.Assembly.Board");
                var fromInspector = F1("Devices.Assembly.Field.label");
                var fromHeader = F1("Devices.Assembly.Subject");
                HelpTip.CloseCurrent();
                return new HelpProbe(tips.Select(t => t.HelpKey).ToList(), tips.Select(t => t.Entry?.Title ?? string.Empty).ToList(),
                    oneAtATime, escClosed, escPasses, outsideClosed, fromBoard, fromInspector, fromHeader, bodies);
            }
            finally { source.RootVisual = null!; }
        });

    [Fact]
    [Trait("Scenario", "ASM-300")]
    public void should_list_open_one_at_a_time_close_on_esc_and_outside_and_answer_f1_by_the_nearest_section_in_the_assembler_ASM300()
    {
        var probe = ProbeAssemblyHelp();

        Assert.Contains("Devices.Assembly", probe.Keys);                                    // 목록 — 창 · 팔레트 · 보드 · 속성
        Assert.Contains("Devices.Assembly.Palette", probe.Keys);
        Assert.Contains("Devices.Assembly.Board", probe.Keys);
        Assert.Contains("Devices.Assembly.Inspector", probe.Keys);
        Assert.All(probe.Titles, t => Assert.False(string.IsNullOrWhiteSpace(t)));          // 모두 열 수 있다(등록된 설명)
        Assert.True(probe.OneAtATime, "말풍선이 둘 이상 열렸다");
        Assert.True(probe.EscClosed, "Esc 가 열린 말풍선을 닫지 않았다");
        Assert.True(probe.EscPassesWhenClosed, "닫힌 뒤의 Esc 를 먹었다(창 · 편집 취소 몫)");
        Assert.True(probe.OutsideClosed, "바깥 클릭이 말풍선을 닫지 않았다");
        Assert.Equal("Devices.Assembly.Board", probe.F1FromBoard);                          // F1 = 포커스에서 가장 가까운 범위
        Assert.Equal("Devices.Assembly.Inspector.Slot", probe.F1FromInspector);              // 라벨 칸 = 「이 부품 정보」 절(속성 칸 안 더 가까운 범위)
        Assert.Equal("Devices.Assembly", probe.F1FromHeader);
        Assert.Contains("Help.Devices.Assembly.Palette.Body", probe.Bodies);               // 말풍선 몸 id
    }

    [Fact]
    [Trait("Scenario", "ASM-301")]
    public void should_open_and_close_every_inspector_question_mark_of_the_assembler_ASM301()
    {
        var result = OnSta(() =>
        {
            var vm = ComposeSync(EnumDeviceCategory.Controller);
            vm.AddFromPalette(Palette(vm, "HEATER"));
            var view = ViewOf(vm);
            var source = new HeadlessSource { RootVisual = view };
            try
            {
                var inspector = Descendants<HelpTip>(view).Where(t => t.HelpKey.StartsWith("Devices.Assembly.Inspector", StringComparison.Ordinal)).ToList();
                var rows = inspector.Select(tip =>
                {
                    tip.Open();
                    var opened = ReferenceEquals(HelpTip.Current, tip) && tip.IsChecked == true;
                    var title = tip.Entry?.Title;
                    tip.Close();
                    return (tip.HelpKey, opened, title, Closed: HelpTip.Current is null && tip.IsChecked != true);
                }).ToList();
                return rows;
            }
            finally { source.RootVisual = null!; }
        });

        Assert.Contains(result, r => r.HelpKey == "Devices.Assembly.Inspector");
        Assert.Contains(result, r => r.HelpKey == "Devices.Assembly.Inspector.Slot");
        Assert.Contains(result, r => r.HelpKey == "Devices.Assembly.Inspector.Overrides");    // 히터 = 재정의 칸 있음
        Assert.Contains(result, r => r.HelpKey == "Devices.Assembly.Inspector.Catalog");
        Assert.All(result, r =>
        {
            Assert.True(r.opened, $"{r.HelpKey} 가 열리지 않았다");
            Assert.False(string.IsNullOrWhiteSpace(r.title), $"{r.HelpKey} 설명이 없다");
            Assert.True(r.Closed, $"{r.HelpKey} 가 닫히지 않았다");
        });
    }
    #endregion

    #region - 기존 장비에 적용 (ASM-022 · 201 · 202 · 203 · 204) -
    private static (AssemblyViewModel Vm, CapturingHttp Http, AsmFakeDialogs Dialogs) OpenController(IReadOnlyList<ComponentDefinitionModel> parts, IReadOnlyList<ComponentDefinitionModel>? serverNow = null)
    {
        var http = new CapturingHttp { GetResponseJson = Envelope(ServerController(serverNow ?? parts)) };
        var device = ServerController(parts).ToControllerDeviceModel();                    // 콘솔이 들고 있는 모델 = 서버 한 건을 바꾼 것
        var dialogs = new AsmFakeDialogs { ConfirmAnswer = true };
        var vm = AssemblyViewModel.ForDevice(device, EnumDeviceCategory.Controller, new Round3Catalog(), new DevicePresetStore(
            Path.Combine(Path.GetTempPath(), $"ironwall-asm-r3-{Guid.NewGuid():N}.json"), () => DateTimeOffset.UnixEpoch), ApplyOver(http), dialogs);
        ((IActivate)vm).ActivateAsync().GetAwaiter().GetResult();
        return (vm, http, dialogs);
    }

    private static IReadOnlyList<(string? Key, string? Type, string? Label)> Sent(CapturingHttp http)
        => ((JArray)http.Calls.Single(c => c.Method == "PATCH").Body!.SelectToken("hardware_spec.components")!)
            .Select(c => ((string?)c["key"], (string?)c["type"], (string?)c["label"])).ToList();

    [Fact]
    [Trait("Scenario", "ASM-201,ASM-022")]
    public async Task should_patch_one_more_part_with_its_label_after_confirming_the_diff_and_say_applied_ASM201_ASM022()
    {
        var (vm, http, dialogs) = OpenController(new[] { Part("heater", "HEATER", "함체 히터"), Part("nic", "NETWORK_INTERFACE") });
        vm.AddFromPalette(Palette(vm, "FAN"));
        vm.Inspected!.Slot.Label = "배기 팬";                                               // `.Field.label`
        var diff = vm.DiffSummary;
        http.Calls.Clear();

        await vm.CommitAsync();                                                             // [적용] → 「추가 1개」 확인 → 예

        Assert.Equal("추가 1개: fan", diff);
        Assert.Contains(dialogs.Confirms, c => c.Title == "부품 구성 적용" && c.Message.StartsWith("추가 1개: fan"));
        Assert.Equal(new[] { "GET", "PATCH" }, http.Calls.Select(c => c.Method));           // 다시 받기 → PATCH 한 건
        Assert.Equal(new (string?, string?, string?)[] { ("heater", "HEATER", "함체 히터"), ("nic", "NETWORK_INTERFACE", null), ("fan", "FAN", "배기 팬") }, Sent(http));
        Assert.Equal("부품 구성을 적용했습니다(부품 3개).", vm.StatusText);
        Assert.Equal("부품 3 · 미저장 변경 0", vm.FooterText);                               // 적용한 것이 새 기준
        Assert.Equal("배기 팬", vm.BoardItems.Single(i => i.Slot.Key == "fan").Slot.Label);   // 라벨은 적용 뒤에도 그대로
    }

    [Fact]
    [Trait("Scenario", "ASM-202")]
    public async Task should_refuse_and_send_nothing_when_the_server_parts_changed_after_the_assembler_was_opened_ASM202()
    {
        var opened = new[] { Part("heater", "HEATER"), Part("nic", "NETWORK_INTERFACE") };
        var serverNow = opened.Append(Part("fan", "FAN")).ToArray();                       // 연 뒤에 다른 곳(SYNC_DEVICE 등)이 팬을 달았다
        var (vm, http, _) = OpenController(opened, serverNow);
        vm.AddFromPalette(Palette(vm, "CONTACT_INPUT"));
        http.Calls.Clear();

        await vm.CommitAsync();

        Assert.DoesNotContain(http.Calls, c => c.Method is "PATCH" or "PUT" or "POST");     // 안 보냄
        Assert.Equal("조립기를 연 뒤에 다른 곳에서 이 장비의 부품 구성이 바뀌었습니다. 저장하지 않았습니다. 조립기를 닫고 다시 여세요.", vm.StatusText);
        Assert.Equal("부품 3 · 미저장 변경 1", vm.FooterText);                               // 보드는 그대로 남는다(다시 열어 비교할 수 있게)
        Assert.True(vm.CanCommit);
    }

    [Fact]
    [Trait("Scenario", "ASM-203")]
    public async Task should_bring_back_keys_types_and_labels_after_apply_refetch_and_reopen_for_a_controller_ASM203()
    {
        // ① 보드 == 서버 ② +1 · 라벨 → 적용 ③ 서버가 받은 배열로 재조회 ④ 다시 열기 — 키 · 종류 · 라벨이 그대로
        var (vm, http, _) = OpenController(new[] { Part("heater", "HEATER", "함체 히터"), Part("nic", "NETWORK_INTERFACE", "본선") });
        var same = (vm.CanCommit, vm.DiffSummary);
        vm.AddFromPalette(Palette(vm, "FAN"));
        vm.Inspected!.Slot.Label = "배기 팬";
        await vm.CommitAsync();
        var stored = Sent(http).Select(s => Part(s.Key!, s.Type!, s.Label)).ToList();

        var (again, _, _) = OpenController(stored);

        Assert.Equal((false, "바뀐 내용이 없습니다."), same);
        Assert.Equal(new (string?, string?, string?)[] { ("heater", "HEATER", "함체 히터"), ("nic", "NETWORK_INTERFACE", "본선"), ("fan", "FAN", "배기 팬") },
                     again.BoardItems.Select(i => ((string?)i.Slot.Key, (string?)i.Slot.TypeCode, i.Slot.Label)));
        Assert.Equal("제어기 · #1 북측 제어기", again.Subject);
        Assert.False(again.HasProblems);
        Assert.False(again.CanCommit);                                                      // 다시 연 보드 == 서버
    }

    [Fact]
    [Trait("Scenario", "ASM-204")]
    public async Task should_bring_back_keys_types_and_labels_after_apply_refetch_and_reopen_for_a_sensor_ASM204()
    {
        SensorDeviceDto Server(IEnumerable<ComponentDefinitionModel> parts)
        {
            var dto = WiringDoubles.ServerSensor(101, 1101, 3, null);
            dto.Id = 101;
            dto.HardwareSpec!.Components = parts.Select(PresetRequestBuilder.ToComponentDto).ToList();
            dto.UseAxisWrite = true;
            dto.HardwareSpec.AllowComponentsWrite = true;
            return dto;
        }
        (AssemblyViewModel, CapturingHttp) Open(IReadOnlyList<ComponentDefinitionModel> parts)
        {
            var http = new CapturingHttp { GetResponseJson = Envelope(Server(parts)) };
            var device = Server(parts).ToSensorDeviceModel();
            var vm = AssemblyViewModel.ForDevice(device, EnumDeviceCategory.Sensor, new Round3Catalog(), NewStore(), ApplyOver(http), new AsmFakeDialogs());
            ((IActivate)vm).ActivateAsync().GetAwaiter().GetResult();
            return (vm, http);
        }

        var (vm, http) = Open(new[] { Part("vib", "VIBRATION_SENSOR", "펜스 진동") });
        var same = vm.CanCommit;
        vm.AddFromPalette(Palette(vm, "NETWORK_INTERFACE"));
        vm.Inspected!.Slot.Label = "센서 통신";
        await vm.CommitAsync();
        var stored = Sent(http).Select(s => Part(s.Key!, s.Type!, s.Label)).ToList();
        var (again, _) = Open(stored);

        Assert.False(same);
        Assert.Equal("부품 구성을 적용했습니다(부품 2개).", vm.StatusText);
        Assert.Equal(new (string?, string?, string?)[] { ("vib", "VIBRATION_SENSOR", "펜스 진동"), ("nic", "NETWORK_INTERFACE", "센서 통신") },
                     again.BoardItems.Select(i => ((string?)i.Slot.Key, (string?)i.Slot.TypeCode, i.Slot.Label)));
        Assert.StartsWith("센서 · #1101", again.Subject);
        Assert.False(again.CanCommit);
    }
    #endregion
}
