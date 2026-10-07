using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Accounts.Ui.ViewTests;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Moq;
using Xunit;

namespace Consoles.ViewTests;

/// <summary>
/// 이벤트 맵핑 <b>실제 뷰</b> — 카메라 행 칩에 "타깃 P2 · 홈 P1" 이 보이면 아래 연동 설정 칸의 콤보도 P2 · P1 을 보여야 한다(EVT-DEMO-R26).
/// </summary>
/// <remarks>
/// <para>창은 띄우지 않는다 — 뷰를 Measure · Arrange 만 해서 MaterialDesign 템플릿까지 입힌 뒤 콤보가 고른 번호와 칸의 글자를 읽는다.</para>
/// <para>🔴 <c>SelectedItem</c> 만 보지 않는다 — 결함 상태에서도 그것은 P2 였다(<c>SelectedIndex</c> -1 · 칸은 빈칸).</para>
/// </remarks>
public class MappingCameraEditorViewTests
{
    /// <summary>dummy-cameras seed 처럼 P1..P5(id 11..15) — 실서버처럼 잠시 뒤에 온다.</summary>
    private sealed class Presets : IMappingPresetSource
    {
        public async Task<MappingCallResult<IReadOnlyList<MappingPresetInfo>>> ListPresetsAsync(int cameraId, CancellationToken token = default)
        {
            await Task.Delay(30, token);
            var list = Enumerable.Range(1, 5).Select(p => new MappingPresetInfo(10 + p, cameraId, $"P{p}", false)).ToList();
            return MappingCallResult<IReadOnlyList<MappingPresetInfo>>.Ok(list);
        }
    }

    /// <summary>응답이 오지 않는 공급원 — "불러오는 중" 의 자리 항목만 선 상태.</summary>
    private sealed class NoAnswer : IMappingPresetSource
    {
        public Task<MappingCallResult<IReadOnlyList<MappingPresetInfo>>> ListPresetsAsync(int cameraId, CancellationToken token = default)
            => new TaskCompletionSource<MappingCallResult<IReadOnlyList<MappingPresetInfo>>>().Task;
    }

    private sealed class SeedDevices : IMappingDeviceSource
    {
        private readonly List<MappingDeviceInfo> _cameras = new() { new(370, "LRT-DUMMY-CAM1", "PTZ", new[] { 3 }, true) };
        public IReadOnlyList<MappingDeviceInfo> Devices(MappingActionKind kind) => kind == MappingActionKind.Camera ? _cameras : Array.Empty<MappingDeviceInfo>();
        public IReadOnlyList<MappingGroupInfo> Groups() => new[] { new MappingGroupInfo(3, "LRT-DUMMY") };
        public MappingDeviceInfo? Find(MappingActionKind kind, int deviceId) => Devices(kind).FirstOrDefault(d => d.Id == deviceId);
    }

    /// <summary>seed 의 LRT-DUMMY-MAP → LRT-DUMMY-CAM1 줄(타깃 P2 · 홈 P1 · 지연 3초).</summary>
    private static IMappingWorkbenchGateway Gateway()
    {
        var mock = new Mock<IMappingWorkbenchGateway>(MockBehavior.Loose);
        mock.Setup(g => g.ListMappingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MappingCallResult<IReadOnlyList<EventMappingReadDto>>.Ok(new[]
            {
                new EventMappingReadDto { Id = 1, NameEvent = "LRT-DUMMY-MAP", CategoryEventMapping = "SENSOR_WITH_CAMERA", DeviceGroupId = 3, Status = true, UpdatedAt = "T0" },
            }));
        mock.Setup(g => g.ListCamerasAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MappingCallResult<IReadOnlyList<MappingCameraReadDto>>.Ok(new[]
            {
                new MappingCameraReadDto
                {
                    ConfigId = 700, EventMappingId = 1, DelayTime = 3, IsEnable = true, UpdatedAt = "T0",
                    Camera = new MappingDeviceRefDto { Id = 370, CategoryDevice = "Camera" },
                    TargetPreset = new MappingPresetRefDto { Id = 12, CameraId = 370, PresetIndex = 2, PresetName = "P2" },
                    HomePreset = new MappingPresetRefDto { Id = 11, CameraId = 370, PresetIndex = 1, PresetName = "P1" },
                },
            }));
        mock.Setup(g => g.ListSpeakersAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MappingCallResult<IReadOnlyList<MappingSpeakerReadDto>>.Ok(Array.Empty<MappingSpeakerReadDto>()));
        mock.Setup(g => g.ListLampsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MappingCallResult<IReadOnlyList<MappingLampReadDto>>.Ok(Array.Empty<MappingLampReadDto>()));
        return mock.Object;
    }

    [Theory]
    [InlineData(true)]      // 서버 목록이 와서 목록이 두 번째로 선다 — 결함이 나던 길
    [InlineData(false)]     // 응답이 없어 자리 항목만 선다
    public void should_show_the_saved_presets_in_the_editor_combos_of_the_real_view(bool serverAnswers) => AppHost.Run(() =>
    {
        var vm = new MappingWorkbenchViewModel(Gateway(), new SeedDevices(), presets: serverAnswers ? new Presets() : new NoAnswer());
        var view = new MappingWorkbenchView { DataContext = vm, Width = 1400, Height = 900 };
        void Layout() { view.Measure(new Size(1400, 900)); view.Arrange(new Rect(0, 0, 1400, 900)); view.UpdateLayout(); }
        void Settle() { for (var i = 0; i < 20; i++) { AppHost.Pump(DispatcherPriority.ApplicationIdle); Thread.Sleep(10); } Layout(); }

        Wait(vm.ReloadAsync());
        Layout(); Settle();
        var board = Find<ListBox>(view, "Integrations.Board.List");
        Assert.NotNull(board);
        board!.SelectedItem = vm.BoardRows.Single();      // 뷰 코드비하인드(OnBoardSelectionChanged)가 뷰모델에 옮긴다
        Layout(); Settle();

        var target = Find<ComboBox>(view, "Integrations.Board.TargetPresetCombo")!;
        var home = Find<ComboBox>(view, "Integrations.Board.HomePresetCombo")!;
        var row = vm.BoardRows.Single();
        var detail = $"chips=[{string.Join(",", row.Chips)}] target={Shown(target)} home={Shown(home)} " +
                     $"vm={vm.SelectedTargetPreset?.Id}/{vm.SelectedHomePreset?.Id} row={row.Row.TargetPresetId}/{row.Row.HomePresetId}/{row.Row.State} " +
                     $"opts=[{string.Join(",", vm.PresetOptions.Select(o => o.Id?.ToString() ?? "-"))}] status='{vm.PresetStatusText}'";

        Assert.True(vm.HasCameraEditor, detail);
        Assert.True(Shown(target) == "12:P2", "타깃 콤보가 저장된 P2 를 보이지 않는다 · " + detail);
        Assert.True(Shown(home) == "11:P1", "홈 콤보가 저장된 P1 을 보이지 않는다 · " + detail);
        Assert.True(MappingDraftState.Pristine == row.Row.State, "손대지 않았는데 행이 바뀌었다 · " + detail);
    });

    /// <summary>콤보가 고른 것 — 번호가 가리키는 항목의 id 와 칸에 그린 글자.</summary>
    private static string Shown(ComboBox combo)
    {
        var picked = combo.SelectedIndex >= 0 ? (combo.Items[combo.SelectedIndex] as MappingPresetOption)?.Id : null;
        return $"{picked?.ToString() ?? "-"}:{combo.SelectionBoxItem}";
    }

    private static void Wait(Task task)
    {
        while (!task.IsCompleted) AppHost.Pump(DispatcherPriority.Background);
        task.GetAwaiter().GetResult();
    }

    private static T? Find<T>(DependencyObject root, string automationId) where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed && AutomationProperties.GetAutomationId(typed) == automationId) return typed;
            if (Find<T>(child, automationId) is { } nested) return nested;
        }
        return null;
    }
}
