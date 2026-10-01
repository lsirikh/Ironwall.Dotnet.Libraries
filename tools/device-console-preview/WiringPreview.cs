using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Register;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Signals;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
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

    #region - Fence scenarios (wiring-fence-view F-4 · fence-wiring-editor 시나리오) -
    /// <summary>
    /// 펜스 뷰 시나리오 — <c>ring</c>(스마트 링 13 · 센서마다 IP · 신호등 · VBus) · <c>pids</c>(섞인 PIDS 50 · 4차 두 줄) ·
    /// <c>lanes4</c>(4차 두 줄 6 + 6 · 제어기 왼쪽 · 참고 그림 ①) · <c>ctrlright</c>(한 줄 22 + 리턴선 · 제어기 오른쪽 · 참고 그림 ④) ·
    /// <c>line</c>(지중 10) · <c>wall</c>(담 구간이 낀 펜스). 결선 단계 · 펜스 보기로 연다.
    /// </summary>
    public (FrameworkElement View, WiringViewModel Vm) Scenario(string name)
    {
        var (view, vm) = name switch
        {
            "pids" => Pids(),
            "lanes4" => Lanes4(),
            "ctrlright" => SingleLaneControllerRight(),
            "line" => UndergroundLine(),
            "wall" => WallSection(),
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
        // 스마트복합센서2 — 센서마다 IP(제어기 뒤 내부망 · FR-15). 매니저 보고(NETWORK_INTERFACE) 는 몇 대만 — 나머지는 모름(FR-14).
        var health = new Dictionary<int, string> { [417] = "OK", [418] = "OK", [419] = "DEGRADED", [420] = "FAULT", [421] = "OK" };
        var seeds = sensors.Select((s, i) => new WiringSensorSeed(s.Id, null, new SensorFacts(s.No, s.Name, "SmartSensor2", "북측"),
            Array.IndexOf(saved, s.Id) is var at && at >= 0
                ? new WiringPlacement(1, at + 1, s.Id == 423 ? WiringFacing.Back : WiringFacing.Front)      // 7구간은 펜스 내부를 본다(FR-20)
                : null,
            ConnectionType: "IP_DIRECT", IpAddress: $"192.168.10.{i + 11}", LinkHealth: health.TryGetValue(s.Id, out var h) ? h : null));
        var vm = Controller(new WiringControllerInfo(1, 1, "CTRL-북측-01", "10.99.7.1", "SmartController"), seeds, new[] { "SmartSensor2" },
                            new WiringFenceContext(null, new PreviewFenceStore(), new PreviewPing()));
        vm.FenceSelect(433);
        vm.StartSignals();                   // 미리보기 ping 은 가짜(12ms 성공) — 신호등이 켜지는 모양만 본다
        return (new WiringView { DataContext = vm }, vm);
    }

    /// <summary>
    /// 중요시설 4차 현장(fence-wiring-editor §1-A) — 한 링에 판망 구간(스마트센서 · 6m 망 · 기둥 위)과 윤형 구간(펜스센서 · 3m 망 가운데)이 번갈아
    /// 5대씩 × 5 = 센서 50대. 저장된 구성(로컬)에 4차 대역(스마트 1~99 · 펜스 101~199)이 있어, 센서를 옮기면 번호가 다시 매겨진다.
    /// 번호는 대역과 맞게 심어 두고, 한 대(스마트 13)만 현장 번호가 어긋난 채로 둔다(저장 전 번호 표에 한 줄).
    /// </summary>
    private (FrameworkElement View, WiringViewModel Vm) Pids()
    {
        var seeds = new List<WiringSensorSeed>();
        var panels = new List<FencePanelSpec>();
        var mounts = new Dictionary<int, SensorMountSpec>();
        int order = 1, smart = 0, fence = 0;
        for (var section = 0; section < 5; section++)
        {
            // 판망 — 스마트 5대, 기둥마다(6m)
            for (var i = 0; i < 5; i++, order++)
            {
                var id = 2000 + order;
                smart++;
                var number = smart == 13 ? 31 : smart;                                  // 현장 번호가 어긋난 한 대
                seeds.Add(new WiringSensorSeed(id, order, new SensorFacts(number, $"판망 스마트 {smart}", "SmartSensor2", section % 2 == 0 ? "서측" : "동측"),
                    new WiringPlacement(1, order)));
                mounts[id] = new SensorMountSpec(panels.Count, FenceMountSpot.PostTop);
                panels.Add(FencePanelSpec.Default(EnumFenceStyle.ChainLink, 6));
            }
            // 윤형 — 펜스센서 5대, 3m 망 가운데
            for (var i = 0; i < 5; i++, order++)
            {
                var id = 2000 + order;
                fence++;
                seeds.Add(new WiringSensorSeed(id, order, new SensorFacts(100 + fence, $"윤형 펜스 {fence}", "Fence", section % 2 == 0 ? "서측" : "동측"),
                    new WiringPlacement(1, order)));
                mounts[id] = new SensorMountSpec(panels.Count, FenceMountSpot.PanelCenter);
                panels.Add(FencePanelSpec.Default(EnumFenceStyle.ChainLinkRazor, 3));
            }
        }
        panels.Add(FencePanelSpec.Default(EnumFenceStyle.ChainLink, 6));                  // 끝 기둥 뒤 한 칸
        // 4차 두 줄(v0.3 §1-0b) — 펜스센서는 윤형과 같이: 위 줄 · 윤형 코일(망 가운데). 서버 사슬 순서도 두 줄 규칙(아래 줄 왼쪽 → 오른쪽, 위 줄 오른쪽 → 왼쪽)으로 심는다.
        foreach (var id in mounts.Keys.ToList())
            if (seeds.First(s => s.Id == id).Facts.TypeText == "Fence") mounts[id] = mounts[id] with { Spot = FenceMountSpot.RazorCoil, Lane = FenceLane.Upper };
        var lanesOrder = FenceLayoutMath.ChainOrder(mounts.Select(p => (p.Key, p.Value)), FenceControllerEnd.Left);
        seeds = seeds.Select(s => s with { Placement = new WiringPlacement(1, lanesOrder.ToList().IndexOf(s.Id) + 1) }).ToList();
        var document = new FenceLayoutDocument { ControllerId = 3, Panels = panels, Mounts = mounts, Bands = NumberBandSet.Tier4, Revision = 1 };
        var vm = Controller(new WiringControllerInfo(3, 3, "PIDS-서측-03", "10.99.8.3", "Controller"), seeds, new[] { "SmartSensor2", "Fence" },
                            new WiringFenceContext(document, new PreviewFenceStore(), new PreviewPing()));
        vm.FenceSelectPanels(new[] { 5, 6, 7 });
        return (new WiringView { DataContext = vm }, vm);
    }

    /// <summary>
    /// 모양 5종이 한 화면에 — 스마트 8대: 철조망 1칸 · 윤형철조망 2칸 · 벽돌담 2칸 · 시멘트담 1칸 · 디자인펜스 2칸. 담 위 센서는 "담 위", 담 사이에는 기둥이 없다.
    /// RS485 노드 주소(IP 아님)라 주소 칸은 노드 번호.
    /// </summary>
    private (FrameworkElement View, WiringViewModel Vm) WallSection()
    {
        var styles = new[]
        {
            // 모양 5종을 한 화면에(fence-style-art) — 철조망 · 윤형철조망 · 벽돌담 · 시멘트담 · 디자인펜스
            EnumFenceStyle.ChainLink, EnumFenceStyle.ChainLinkRazor, EnumFenceStyle.ChainLinkRazor, EnumFenceStyle.Brick, EnumFenceStyle.Brick,
            EnumFenceStyle.Concrete, EnumFenceStyle.DesignFence, EnumFenceStyle.DesignFence,
        };
        var panels = styles.Select(s => FencePanelSpec.Default(s, s is EnumFenceStyle.Brick or EnumFenceStyle.Concrete ? 4.5 : 6)).ToList();
        var seeds = new List<WiringSensorSeed>();
        var mounts = new Dictionary<int, SensorMountSpec>();
        for (var i = 0; i < 8; i++)
        {
            var id = 3001 + i;
            seeds.Add(new WiringSensorSeed(id, i + 1, new SensorFacts(i + 1, $"남측 {i + 1}구간", "SmartSensor2", "남측"), new WiringPlacement(1, i + 1),
                ConnectionType: "RS485"));
            mounts[id] = styles[i] is EnumFenceStyle.Brick or EnumFenceStyle.Concrete
                ? new SensorMountSpec(i, FenceMountSpot.WallTop)
                : new SensorMountSpec(i, FenceMountSpot.PostTop);
        }
        var document = new FenceLayoutDocument { ControllerId = 4, Panels = panels, Mounts = mounts, Bands = NumberBandSet.Tier3, Revision = 1 };
        var vm = Controller(new WiringControllerInfo(4, 4, "CTRL-남측-04", "10.99.7.4", "SmartController"), seeds, new[] { "SmartSensor2" },
                            new WiringFenceContext(document, new PreviewFenceStore(), new PreviewPing()));
        vm.FenceSelect(3004);
        return (new WiringView { DataContext = vm }, vm);
    }

    /// <summary>
    /// 참고 그림 ① — 중요시설 4차 두 줄: 아래 줄 판망 스마트 1~6(기둥 위) · 위 줄 윤형 펜스센서 101~106(윤형 코일 · 기둥 사이 망 가운데), 제어기 왼쪽 끝.
    /// 사슬 = 아래 1 → 6 → 먼 끝에서 꺾여 → 위 106 → 101 → Ch2. 번호는 줄마다 제어기에서 멀어지며 커진다(§1-0b 가정).
    /// </summary>
    private (FrameworkElement View, WiringViewModel Vm) Lanes4()
    {
        var panels = Enumerable.Range(0, 6).Select(_ => FencePanelSpec.Default(EnumFenceStyle.ChainLinkRazor, 6)).ToList();
        var mounts = new Dictionary<int, SensorMountSpec>();
        var facts = new Dictionary<int, SensorFacts>();
        for (var i = 0; i < 6; i++)
        {
            mounts[4101 + i] = new SensorMountSpec(i, FenceMountSpot.PostTop);
            facts[4101 + i] = new SensorFacts(1 + i, $"판망 스마트 {i + 1}", "SmartSensor2", "북측");
            mounts[4201 + i] = new SensorMountSpec(i, FenceMountSpot.RazorCoil, Lane: FenceLane.Upper);         // 펜스센서는 윤형과 같이
            facts[4201 + i] = new SensorFacts(101 + i, $"윤형 펜스 {i + 1}", "Fence", "북측");
        }
        var order = FenceLayoutMath.ChainOrder(mounts.Select(p => (p.Key, p.Value)), FenceControllerEnd.Left).ToList();
        var seeds = order.Select((id, i) => new WiringSensorSeed(id, i + 1, facts[id], new WiringPlacement(1, i + 1), ConnectionType: "RS485")).ToList();
        var document = new FenceLayoutDocument { ControllerId = 6, Panels = panels, Mounts = mounts, Bands = NumberBandSet.Tier4, Revision = 1 };
        var vm = Controller(new WiringControllerInfo(6, 6, "PIDS-북측-06", "10.99.8.6", "Controller"), seeds, new[] { "SmartSensor2", "Fence" },
                            new WiringFenceContext(document, new PreviewFenceStore(), new PreviewPing()));
        vm.FenceSelect(4203);
        return (new WiringView { DataContext = vm }, vm);
    }

    /// <summary>
    /// 참고 그림 ④ — 한 줄 센서 22대 + 리턴선, 제어기 <b>오른쪽</b> 끝: Ch1 이 오른쪽 끝에서 아래 줄을 왼쪽으로 지나 왼쪽 끝에서 꺾여 위 줄 점선으로 돌아온다.
    /// 눈금은 왼쪽부터 1 · 6 · 11 · 16 · 21.
    /// </summary>
    private (FrameworkElement View, WiringViewModel Vm) SingleLaneControllerRight()
    {
        var panels = Enumerable.Range(0, 21).Select(_ => FencePanelSpec.Default(EnumFenceStyle.ChainLink, 6)).ToList();
        var mounts = Enumerable.Range(0, 22).ToDictionary(i => 5101 + i, i => new SensorMountSpec(i, FenceMountSpot.PostTop));
        var order = FenceLayoutMath.ChainOrder(mounts.Select(p => (p.Key, p.Value)), FenceControllerEnd.Right).ToList();
        var seeds = order.Select((id, i) => new WiringSensorSeed(id, i + 1, new SensorFacts(i + 1, $"동측 {i + 1}구간", "SmartSensor", "동측"),
            new WiringPlacement(1, i + 1), ConnectionType: "RS485")).ToList();
        var document = new FenceLayoutDocument
        {
            ControllerId = 7, Panels = panels, Mounts = mounts, Bands = NumberBandSet.Tier3, ControllerEnd = FenceControllerEnd.Right, Revision = 1,
        };
        var vm = Controller(new WiringControllerInfo(7, 7, "CTRL-동측-07", "10.99.7.7", "SmartController"), seeds, new[] { "SmartSensor" },
                            new WiringFenceContext(document, new PreviewFenceStore(), new PreviewPing()));
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

    private WiringViewModel Controller(WiringControllerInfo info, IEnumerable<WiringSensorSeed> seeds, IReadOnlyList<string> types, WiringFenceContext? fence = null)
    {
        var gateway = new DeviceApiSensorGateway(new MockDeviceApiService());
        var apply = new WiringApplyService(gateway, null, null, new DeviceQueryPolicy(new AxisProbe()));
        return WiringViewModel.ForController(info, seeds, types, apply, _dialogs, Groups, fence);
    }

    /// <summary>미리보기 로컬 저장소 — 메모리에만 쥔다(로컬 DB 없음).</summary>
    private sealed class PreviewFenceStore : IFenceLayoutStore
    {
        private readonly Dictionary<FenceLayoutKey, FenceLayoutDocument> _documents = new();

        public Task<FenceLayoutLoadResult> LoadAsync(FenceLayoutKey key, CancellationToken token = default)
            => Task.FromResult(_documents.TryGetValue(key, out var d) ? FenceLayoutLoadResult.Loaded(d) : FenceLayoutLoadResult.NotFound);

        public Task<FenceLayoutSaveResult> SaveAsync(FenceLayoutKey key, FenceLayoutDocument document, FenceLayoutSaveMode mode = FenceLayoutSaveMode.Normal,
                                                     CancellationToken token = default)
        {
            _documents[key] = document;
            return Task.FromResult(new FenceLayoutSaveResult(FenceLayoutSaveStatus.Saved, document.Revision + 1, "펜스 구성을 미리보기 메모리에 저장했습니다."));
        }
    }

    /// <summary>미리보기 ping — 늘 12ms 성공(ICMP 를 보내지 않는다).</summary>
    private sealed class PreviewPing : IPingProbe
    {
        public Task<PingSample> SendAsync(string host, TimeSpan timeout, CancellationToken token = default) => Task.FromResult(new PingSample(true, 12));
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
