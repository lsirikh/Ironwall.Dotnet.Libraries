using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeviceConsolePreview;

/// <summary>
/// 셋업 · 결선 창의 미리보기 — <b>진짜 뷰 + 진짜 뷰모델</b>에 가짜 센서를 올린다(호스트 앱 · 서버 없음).
/// </summary>
internal sealed class WiringPreview
{
    private static readonly string[] Types = { "Fence", "Underground", "PIR", "Laser", "SmartSensor" };

    private static readonly WiringGroupInfo[] Groups =
    {
        new(1, "북측"), new(2, "탄약고"), new(3, "정문"), new(4, "남문"),
    };

    private readonly PreviewWiringDialogs _dialogs = new();

    /// <summary>센서가 한 대도 없는 제어기 — 처음 여는 화면.</summary>
    public (FrameworkElement View, WiringViewModel Vm) Empty() => Build(0, 0);

    /// <summary>센서 8대 — 표 화면.</summary>
    public (FrameworkElement View, WiringViewModel Vm) Table() => Build(8, 0);

    /// <summary>센서 9대 · 왼쪽 가지 5 · 오른쪽 가지 4 — 결선이 끝난 화면(펜스 · 제어기 종류 모름 → 양쪽 가지).</summary>
    /// <remarks>체인 모델(F-2)에서는 목록 끝의 빈 칸이 "끝에 붙이기" 자리 하나뿐이다 — 옛 칸 모델처럼 Line2[0..3] 을 가정하지 않는다.</remarks>
    public (FrameworkElement View, WiringViewModel Vm) Wired()
    {
        var (view, vm) = Build(9, 5);
        for (var i = 0; i < 4; i++) vm.Drop(Payload(vm.Palette[0]), Slot(vm.Line2[^1]));
        vm.GoWiring();
        vm.ShowTableView();                  // 옛 상태 스냅숏(03 · 11)은 표 보기로 찍는다
        return (view, vm);
    }

    #region - Fence scenarios (wiring-fence-view F-4 · 목업 buildSmart/buildPids/buildUg) -
    /// <summary>펜스 뷰 시나리오 — <c>ring</c> · <c>pids</c> · <c>line</c>. 결선 단계 · 펜스 보기로 연다.</summary>
    public (FrameworkElement View, WiringViewModel Vm) Scenario(string name)
    {
        var (view, vm) = name switch
        {
            "pids" => Pids(),
            "line" => UndergroundLine(),
            _ => SmartRing(),
        };
        vm.GoWiring();
        vm.ShowFenceView();
        return (view, vm);
    }

    /// <summary>
    /// 스마트 링 13대 — 저장된 체인 10대(현장에서 103 · 104 가 바꿔 꽂힘) + 저장된 배치가 없는 3대(번호 105 가 둘 — 하나는 "번호 같음 · id순")
    /// → 번호순 제안 배너. 목업의 선택(S-0433)을 그대로 골라 둔다.
    /// </summary>
    private (FrameworkElement View, WiringViewModel Vm) SmartRing()
    {
        var sensors = new (int Id, int No, string Name, int Bus)[]
        {
            (417, 101, "북측 1구간 펜스", 1), (418, 102, "북측 2구간 펜스", 2), (419, 103, "북측 3구간 펜스", 4), (420, 104, "북측 4구간 펜스", 3),
            (421, 105, "북측 5구간 펜스", 5), (433, 105, "북측 5구간 보강", 13), (422, 106, "북측 6구간 펜스", 6), (423, 107, "북측 7구간 펜스", 7),
            (424, 108, "북측 8구간 펜스", 8), (425, 109, "북측 9구간 펜스", 9), (426, 110, "북측 10구간 펜스", 11), (427, 111, "북측 11구간 펜스", 10),
            (428, 112, "북측 12구간 펜스", 12),
        };
        var saved = new[] { 417, 418, 420, 419, 421, 423, 424, 425, 427, 428 };
        var seeds = sensors.Select(s => new WiringSensorSeed(s.Id, s.Bus, new SensorFacts(s.No, s.Name, "SmartSensor2", "북측"),
            Array.IndexOf(saved, s.Id) is var at && at >= 0
                ? new WiringPlacement(1, at + 1, s.Id == 423 ? WiringFacing.Back : WiringFacing.Front)      // 7구간은 펜스 내부를 본다(FR-20)
                : null));
        var vm = Controller(new WiringControllerInfo(1, 1, "CTRL-북측-01", "10.99.7.1", "SmartController"), seeds, new[] { "SmartSensor2" });
        vm.FenceSelect(433);
        return (new WiringView { DataContext = vm }, vm);
    }

    /// <summary>PIDS 양쪽 가지 — 가지마다 [복합, 펜스 ×7, 복합, 펜스 ×7, 복합, 펜스 ×6](23대 · 왼쪽 2 · 3번이 바뀜) + 다른 제어기의 스마트 센서 1대(팔레트 · 섞임 경고).</summary>
    private (FrameworkElement View, WiringViewModel Vm) Pids()
    {
        var seeds = new List<WiringSensorSeed>();
        var number = 101;
        var pattern = new[] { 'M' }.Concat(Enumerable.Repeat('F', 7)).Append('M').Concat(Enumerable.Repeat('F', 7)).Append('M').Concat(Enumerable.Repeat('F', 6)).ToArray();
        foreach (var (line, side) in new[] { (1, "서측"), (2, "동측") })
        {
            int multi = 0, fence = 0;
            var ids = new List<(int Id, string Name, bool Multi)>();
            foreach (var t in pattern)
            {
                var isMulti = t == 'M';
                var name = isMulti ? $"{side} 복합 {++multi}" : $"{side} 펜스 {++fence}";
                ids.Add((1000 + number, name, isMulti));
                number++;
            }
            if (line == 1) (ids[1], ids[2]) = (ids[2], ids[1]);
            for (var i = 0; i < ids.Count; i++)
                seeds.Add(new WiringSensorSeed(ids[i].Id, ids[i].Id - 1100, new SensorFacts(ids[i].Id - 1000, ids[i].Name, ids[i].Multi ? "Multi" : "Fence", side),
                    new WiringPlacement(line, i + 1, line == 2 && i == 8 ? WiringFacing.Back : WiringFacing.Front)));   // 동측 복합 2 는 내부를 본다(FR-20)
        }
        seeds.Add(new WiringSensorSeed(499, 1, new SensorFacts(150, "스마트 복합센서 II(다른 제어기)", "SmartSensor2", "서측")));
        var vm = Controller(new WiringControllerInfo(3, 3, "PIDS-서측-03", "10.99.8.3", "Controller"), seeds, new[] { "Multi", "Fence", "SmartSensor2" });
        return (new WiringView { DataContext = vm }, vm);
    }

    /// <summary>지중 한 줄 — 지진동센서 10대(제어기 쪽이 1).</summary>
    private (FrameworkElement View, WiringViewModel Vm) UndergroundLine()
    {
        var seeds = Enumerable.Range(0, 10).Select(i => new WiringSensorSeed(5001 + i, i + 1,
            new SensorFacts(501 + i, $"내부 지중 {i + 1}", "Underground", "내부"), new WiringPlacement(1, i + 1)));
        var vm = Controller(new WiringControllerInfo(2, 2, "UG-내부-02", "10.99.9.2", "Controller"), seeds, new[] { "Underground" });
        return (new WiringView { DataContext = vm }, vm);
    }

    private WiringViewModel Controller(WiringControllerInfo info, IEnumerable<WiringSensorSeed> seeds, IReadOnlyList<string> types)
    {
        var gateway = new DeviceApiSensorGateway(new MockDeviceApiService());
        var apply = new WiringApplyService(gateway, null, null, new DeviceQueryPolicy(new AxisProbe()));
        return WiringViewModel.ForController(info, seeds, types, apply, _dialogs, Groups);
    }
    #endregion

    /// <summary>그룹 3상태 — 전부 · 하나도 · 줄마다 다름이 한 화면에 있다(W2).</summary>
    public (FrameworkElement View, WiringViewModel Vm) GroupSelection()
    {
        var (view, vm) = Build(8, 0, groups: i => i < 4 ? new[] { 1 } : i < 6 ? new[] { 1, 2 } : Array.Empty<int>());
        SelectInGrid(view, vm, 0, 5);
        vm.OnSelectionChanged(new[] { vm.Rows[0], vm.Rows[5] });
        vm.ToggleGroup(vm.GroupChecks[2]);       // "정문" 을 눌러 둔다 — 적용 미리보기가 뜬다
        return (view, vm);
    }

    /// <summary>칸 하나를 고른 상태 — 키보드로 옮길 때 보이는 선택 표시(C8).</summary>
    public (FrameworkElement View, WiringViewModel Vm) SlotSelected()
    {
        var (view, vm) = Wired();
        vm.Line1[2].IsSelected = true;
        return (view, vm);
    }

    private static DropTarget Slot(WiringSlotViewModel slot) => new(WiringViewModel.SlotZoneKey, slot, -1);

    private static DragPayload Payload(object item) => new(null!, new[] { item }, "preview");

    /// <summary>1차에만 5대 · 3대는 미배치 — 경고와 치명이 같이 있는 화면.</summary>
    public (FrameworkElement View, WiringViewModel Vm) Problems()
    {
        var (view, vm) = Build(8, 5);
        vm.GoWiring();
        vm.Unplace(vm.Line1[1]);          // 1차 가운데를 비운다 — 빈 칸 경고
        return (view, vm);
    }

    /// <summary>표에서 두 줄을 고르고 종류만 손댄 상태 — "여러 값" 과 적용 미리보기.</summary>
    public (FrameworkElement View, WiringViewModel Vm) TableWithSelection()
    {
        var (view, vm) = Build(8, 0);
        vm.Rows[2].TypeText = "PIR";
        vm.Rows[3].Zone = "탄약고 동측";
        SelectInGrid(view, vm, 2, 3);           // 표의 선택이 먼저 — 선택이 바뀌면 손댄 칸이 비워진다(제품 동작)
        vm.OnSelectionChanged(new[] { vm.Rows[2], vm.Rows[3] });
        vm.EditType = "Laser";
        return (view, vm);
    }

    /// <summary>결선이 바뀌어 저장할 것이 쌓인 화면 — 오른쪽 칸의 변경 미리보기.</summary>
    public (FrameworkElement View, WiringViewModel Vm) ChangePreview()
    {
        var (view, vm) = Build(8, 8);
        vm.GoWiring();
        vm.AutoLayout();
        vm.Rows[0].Name = "북측 1구간 펜스(교체)";
        return (view, vm);
    }

    /// <summary>센서 여러 개 만들기 — 번호가 겹치는 줄이 있는 상태.</summary>
    public FrameworkElement MakeSensors(bool withConflict)
    {
        var existing = withConflict ? new[] { 1203, 1207 } : System.Array.Empty<int>();
        var vm = new MakeSensorsViewModel(Types, "Fence", "북측 7구간", existing, 1201) { Count = "12" };
        return new MakeSensorsView { DataContext = vm };
    }

    /// <summary>엑셀 붙여넣기 보고 — 받아들인 줄과 버린 줄이 함께 보인다.</summary>
    public FrameworkElement PasteReport()
    {
        const string text = "번호\t이름\t종류\t구역\n"
                          + "1301\t북측 1구간 펜스\tFence\t북측 8구간\n"
                          + "1302\t북측 2구간 펜스\tFence\t북측 8구간\n"
                          + "1303\t북측 3구간 펜스\tPIR\t북측 8구간\n"
                          + "일이삼\t번호가 숫자가 아님\tFence\t북측 8구간\n"
                          + "1101\t이미 있는 번호\tFence\t북측 8구간";

        var report = TsvPaste.Parse(text, new[] { 1101, 1102 }, "Fence", "북측 7구간");
        return new PasteReportView { DataContext = new PasteReportViewModel(report, new[] { 1101, 1102 }, "Fence", "북측 7구간") };
    }

    /// <summary>저장 전 확인 — 무엇을 몇 번 보내는지 글로 보인다.</summary>
    public FrameworkElement SaveConfirm()
    {
        var (_, vm) = ChangePreview();
        var message = vm.ChangePreview + System.Environment.NewLine + System.Environment.NewLine
                    + "보내기 직전에 각 센서를 다시 받아 그 사이 바뀌지 않았는지 확인합니다.";
        return new WiringPromptView { DataContext = new WiringPromptViewModel("결선 저장", message, "저장", "취소") };
    }

    #region - Build -
    private (FrameworkElement View, WiringViewModel Vm) Build(int sensors, int placedOnFirst, Func<int, int[]>? groups = null)
    {
        var seeds = new List<WiringSensorSeed>();
        for (var i = 0; i < sensors; i++)
        {
            seeds.Add(new WiringSensorSeed(
                101 + i,
                i + 1,
                new SensorFacts(1101 + i, $"북측 {i + 1}구간 펜스", i < 5 ? "Fence" : "Underground", i < 5 ? "북측 7구간" : "북측 8구간"),
                i < placedOnFirst ? new WiringPlacement(1, i + 1) : null,
                null,
                groups?.Invoke(i)));
        }

        var gateway = new DeviceApiSensorGateway(new MockDeviceApiService());
        var apply = new WiringApplyService(gateway, null, null, new DeviceQueryPolicy(new AxisProbe()));

        var vm = WiringViewModel.ForController(
            new WiringControllerInfo(10, 1, "북측 제어기 B", "10.20.1.103"),
            seeds, Types, apply, _dialogs, Groups);

        return (new WiringView { DataContext = vm }, vm);
    }

    /// <summary>표의 실제 선택까지 맞춘다 — 선택 표시(좌측 바)가 화면에 보이게.</summary>
    private static void SelectInGrid(FrameworkElement view, WiringViewModel vm, params int[] rows)
    {
        view.Measure(new Size(1280, 820));
        view.Arrange(new Rect(0, 0, 1280, 820));
        view.UpdateLayout();

        if (Find(view) is not { } grid) return;
        grid.SelectedItems.Clear();
        foreach (var index in rows.Length == 0 ? new[] { 2, 3 } : rows) grid.SelectedItems.Add(vm.Rows[index]);

        static DataGrid? Find(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is DataGrid grid && System.Windows.Automation.AutomationProperties.GetAutomationId(grid) == "Devices.Wiring.Grid") return grid;
                if (Find(child) is { } found) return found;
            }
            return null;
        }
    }

    private sealed class AxisProbe : IServerContractProbe
    {
        public EnumServerContract Contract => Enum.GetValues<EnumServerContract>().Max();
        public string? RawVersion => "8.0.1";
        public bool IsResolved => true;
        public Task<bool> ResolveAsync(CancellationToken token = default) => Task.FromResult(true);
        public Task<bool> RefreshAsync(CancellationToken token = default) => Task.FromResult(true);
    }

    /// <summary>미리보기에서는 아무것도 묻지 않는다 — 스냅샷은 창을 따로 띄워 찍는다.</summary>
    private sealed class PreviewWiringDialogs : IWiringDialogs
    {
        public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(false);
        public Task<string?> AskTextAsync(string title, string label, string initial) => Task.FromResult<string?>(null);
        public Task<MakeSensorsResult?> AskMakeSensorsAsync(IReadOnlyList<string> types, string defaultType, string defaultZone, IReadOnlyCollection<int> existingNumbers, int suggestedStart)
            => Task.FromResult<MakeSensorsResult?>(null);
        public Task<bool> ShowPasteReportAsync(PasteReport report) => Task.FromResult(false);
        public void RememberPasteContext(IReadOnlyCollection<int> existingNumbers, string defaultType, string defaultZone) { }
        public string? ReadClipboardText() => null;
    }
    #endregion
}
