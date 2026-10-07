using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;
/****************************************************************************
   Purpose      : 카메라 연동 칸 프리셋 콤보 — 칩에는 보이는데 콤보는 빈칸이던 것(EVT-DEMO-R26)
   Created By   : Claude
   Created On   : 2026-10-07
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>dummy-cameras seed 처럼 P1..P5 를 돌려주는 가짜(id 11..15).</summary>
internal sealed class SeedPresets : IMappingPresetSource
{
    /// <summary>있으면 응답을 이 문이 열릴 때까지 붙잡는다 — 실서버 지연 사이에 화면이 한 번 그려지는 순서를 만든다.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public async Task<MappingCallResult<IReadOnlyList<MappingPresetInfo>>> ListPresetsAsync(int cameraId, CancellationToken token = default)
    {
        if (Gate is { } gate) await gate.Task;
        await Task.Yield();
        var list = new List<MappingPresetInfo>();
        for (var p = 1; p <= 5; p++) list.Add(new MappingPresetInfo(10 + p, cameraId, $"P{p}", false));
        return MappingCallResult<IReadOnlyList<MappingPresetInfo>>.Ok(list);
    }
}

/// <summary>
/// 실제 XAML 의 프리셋 콤보 두 개를 뷰모델에 묶어(창은 띄우지 않는다) <b>콤보가 실제로 고른 것</b>을 본다.
/// </summary>
/// <remarks>
/// 🔴 <c>SelectedItem</c> 만 보면 결함을 못 잡는다 — 2026-10-04 시연에서 콤보의 <c>SelectedItem</c> 은 P2 였는데
/// <c>SelectedIndex</c> 는 -1, 칸은 빈칸이었다. 항목이 값으로 같으면(record) "불러오는 중" 의 자리 항목 P2 에서 서버 목록의 P2 로
/// 바뀌어도 WPF 가 선택을 다시 하지 않는다. 그래서 고른 번호와 칸에 그린 글자(<c>SelectionBoxItem</c>)를 함께 본다.
/// </remarks>
public class MappingCameraEditorComboBindingTests
{
    private static string ViewXamlPath([CallerFilePath] string here = "")
        => Path.Combine(Path.GetDirectoryName(here)!, "..", "Consoles", "Mapping", "MappingWorkbenchView.xaml");

    /// <summary>실제 XAML 에서 콤보를 떼어 낸다(스타일 · MaterialDesign 첨부 속성만 뺀다 — 바인딩은 그대로).</summary>
    private static ComboBox ComboFromView(string automationId)
    {
        var xaml = File.ReadAllText(ViewXamlPath());
        var at = xaml.IndexOf($"\"{automationId}\"", StringComparison.Ordinal);
        Assert.True(at >= 0, $"{automationId} 를 XAML 에서 찾지 못했다");
        var open = xaml.LastIndexOf("<ComboBox", at, StringComparison.Ordinal);
        var close = xaml.IndexOf("/>", at, StringComparison.Ordinal);
        var element = xaml[open..(close + 2)];
        element = Regex.Replace(element, @"\s(Style|md:[\w.]+)=""[^""]*""", "");
        element = element.Replace("<ComboBox", "<ComboBox xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'");
        return (ComboBox)XamlReader.Parse(element);
    }

    /// <summary>seed 의 LRT-DUMMY-CAM1 줄 — 타깃 P2(12) · 홈 P1(11) · 지연 3초.</summary>
    private static MappingCameraReadDto SeedRow() => new()
    {
        ConfigId = 700, EventMappingId = 1, DelayTime = 3, IsEnable = true, UpdatedAt = "T0",
        Camera = new MappingDeviceRefDto { Id = 370, CategoryDevice = "Camera" },
        TargetPreset = new MappingPresetRefDto { Id = 12, CameraId = 370, PresetIndex = 2, PresetName = "P2" },
        HomePreset = new MappingPresetRefDto { Id = 11, CameraId = 370, PresetIndex = 1, PresetName = "P1" },
    };

    /// <summary>콤보가 고른 것 — 번호가 가리키는 항목의 id 와 칸에 그린 글자.</summary>
    private static string Shown(ComboBox combo)
    {
        var picked = combo.SelectedIndex >= 0 ? (combo.Items[combo.SelectedIndex] as MappingPresetOption)?.Id : null;
        return $"{picked?.ToString() ?? "-"}:{combo.SelectionBoxItem}";
    }

    /// <summary>보드 · 콤보를 세우고 <paramref name="flow"/> 대로 행을 고른 뒤 두 콤보가 보이는 것을 돌려준다.</summary>
    private static (string Target, string Home, string Detail) Run(string flow, Action<MappingWorkbenchViewModel>? after = null)
        => OnSta(async () =>
        {
            var gateway = new CountingGateway();
            gateway.Cameras.Add(SeedRow());
            gateway.Cameras.Add(new MappingCameraReadDto
            {
                ConfigId = 701, EventMappingId = 1, DelayTime = 0, IsEnable = true, UpdatedAt = "T0",
                Camera = new MappingDeviceRefDto { Id = 371, CategoryDevice = "Camera" },
            });
            var presets = new SeedPresets();
            var vm = new MappingWorkbenchViewModel(gateway, new StubDevices(), presets: presets);

            var target = ComboFromView("Integrations.Board.TargetPresetCombo");
            var home = ComboFromView("Integrations.Board.HomePresetCombo");
            // 보드 목록 — 뷰 코드비하인드(OnBoardSelectionChanged)와 같은 길로 선택을 옮긴다
            var list = new ListBox { SelectionMode = SelectionMode.Extended };
            list.SetBinding(ItemsControl.ItemsSourceProperty, "BoardRows");
            list.SelectionChanged += (_, _) =>
            {
                vm.SelectedBoardRows.Clear();
                foreach (var item in list.SelectedItems.OfType<MappingRowViewModel>()) vm.SelectedBoardRows.Add(item);
                vm.OnSelectionChanged();
            };
            // 실제 뷰처럼 칸은 카메라 행을 고르기 전까지 접혀 있다
            var editor = new StackPanel();
            editor.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding("HasCameraEditor") { Converter = new BooleanToVisibilityConverter() });
            editor.Children.Add(target);
            editor.Children.Add(home);
            var panel = new StackPanel { DataContext = vm };
            panel.Children.Add(list);
            panel.Children.Add(editor);

            void Layout() { panel.Measure(new Size(400, 600)); panel.Arrange(new Rect(0, 0, 400, 600)); panel.UpdateLayout(); }
            async Task Settle() { await vm.PresetLoadTask; Layout(); await Dispatcher.Yield(DispatcherPriority.Background); Layout(); }
            MappingRowViewModel Cam(int id) => vm.BoardRows.Single(r => r.Row.DeviceId == id);

            await vm.ReloadAsync();
            Layout();
            switch (flow)
            {
                case "select":
                    list.SelectedItem = Cam(370); await Settle();
                    break;
                case "slow-server":     // 응답을 기다리는 사이 칸이 한 번 그려진다(실서버)
                    presets.Gate = new TaskCompletionSource();
                    list.SelectedItem = Cam(370);
                    Layout(); await Dispatcher.Yield(DispatcherPriority.Background); Layout();
                    presets.Gate.SetResult();
                    await Settle();
                    break;
                case "reselect-cached":
                    list.SelectedItem = Cam(370); await Settle();
                    list.SelectedItem = null; await Settle();
                    list.SelectedItem = Cam(370); await Settle();
                    break;
                case "via-other-camera":
                    list.SelectedItem = Cam(371); await Settle();
                    list.SelectedItem = Cam(370); await Settle();
                    break;
                case "after-reload":
                    list.SelectedItem = Cam(370); await Settle();
                    await vm.ReloadAsync(); await Settle();
                    list.SelectedItem = Cam(370); await Settle();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(flow), flow, null);
            }
            after?.Invoke(vm);
            Layout();

            var row = Cam(370).Row;
            var detail = $"target={Shown(target)} home={Shown(home)} vmT={vm.SelectedTargetPreset?.Id} vmH={vm.SelectedHomePreset?.Id} " +
                         $"row={row.TargetPresetId}/{row.HomePresetId}/{row.State} opts=[{string.Join(",", vm.PresetOptions.Select(o => o.Id?.ToString() ?? "-"))}]";
            return (Shown(target), Shown(home), detail);
        });

    [Theory]
    [InlineData("select")]
    [InlineData("slow-server")]
    [InlineData("reselect-cached")]
    [InlineData("via-other-camera")]
    [InlineData("after-reload")]
    public void should_show_the_saved_target_and_home_presets_in_the_editor_combos(string flow)
    {
        var (target, home, detail) = Run(flow);

        Assert.True(target == "12:P2", $"타깃 콤보가 저장된 P2 를 보이지 않는다 · {detail}");
        Assert.True(home == "11:P1", $"홈 콤보가 저장된 P1 을 보이지 않는다 · {detail}");
    }

    [Fact]
    public void should_keep_the_saved_presets_out_of_the_patch_when_only_the_delay_is_changed()
    {
        // [적용] 은 행 값과 서버 원본을 견준 키만 싣는다 — 콤보가 무엇을 보이든 프리셋을 지우는 키(null)가 실리면 안 된다.
        MappingCameraUpdateDto? body = null;
        var row = default(MappingBoardRow);
        Run("slow-server", vm =>
        {
            vm.EditDelayTime = 5;
            row = vm.BoardRows.Single(r => r.Row.DeviceId == 370).Row;
            body = MappingRequestBuilder.PatchCamera(row, null);
        });

        Assert.Equal(MappingDraftState.Edited, row!.State);
        Assert.Equal((12, 11), (row.TargetPresetId, row.HomePresetId));
        Assert.False(body!.IsTargetPresetIdSpecified, "타깃 프리셋 키가 실렸다(서버 값을 덮는다)");
        Assert.False(body.IsHomePresetIdSpecified, "홈 프리셋 키가 실렸다(서버 값을 덮는다)");
        Assert.Equal(5, body.DelayTime);
    }

    /// <summary>STA 스레드에 Dispatcher 동기화 문맥을 깔고 비동기 본문을 끝까지 돌린다(창은 띄우지 않는다).</summary>
    private static T OnSta<T>(Func<Task<T>> body)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var frame = new DispatcherFrame();
            var task = body();
            task.ContinueWith(_ => frame.Continue = false, TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
            try { result = task.GetAwaiter().GetResult(); }
            catch (Exception ex) { failure = ex; }
            Dispatcher.CurrentDispatcher.InvokeShutdown();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 스레드가 끝나지 않았다");
        if (failure is not null) throw failure;
        return result;
    }
}
