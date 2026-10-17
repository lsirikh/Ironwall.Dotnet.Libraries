using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Db.Services;
using Ironwall.Dotnet.Libraries.GMaps.Providers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Factories;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Undo;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Moq;
using ScenarioShared;
using Xunit;

namespace GMaps.MapEdit.Tests;

/// <summary>
/// W2(t-20261011-094654-8195) 공용 장치 — <see cref="EditRig"/> 와 같이 생성자 없는 <see cref="MapViewModel"/> 에 실제 지도를 꽂되,
/// 지도를 <see cref="AdornerDecorator"/> 안에 두고(MapView.xaml 과 같은 부모 — 어도너 · 드롭 호스트가 붙는다)
/// VM 의 <b>실제 배선 메서드</b> <c>SetupAdornerIntegration</c> 을 불러 지도 통지(마커 클릭 · 배치 클릭 · Ctrl+클릭 · 러버밴드)를
/// 제품과 같은 처리기에 잇는다. 마커 팩토리 · 속성창 팩토리는 실제 것, DB · 권한 · 발행기는 가짜(Moq). 창은 만들지 않는다.
/// </summary>
internal sealed class WiredMapRig
{
    public MapViewModel Vm { get; }
    public GMapCustomControl Map { get; }
    public AdornerDecorator Deco { get; }
    public Mock<IGMapDbSymbolService> Db { get; } = new();
    public Mock<IPermissionService> Permission { get; } = new();
    public Mock<ILogService> Log { get; } = new();
    public DeviceProvider Devices { get; } = new();
    public List<object> Published { get; } = new();
    public UndoService Undo { get; } = new();

    private WiredMapRig(bool canEdit, bool canControl)
    {
        Log.Setup(l => l.Error(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Callback<string, string, string, int>((m, _, _, _) => AllErrors.Add(m));
        Map = EditRig.NewMap();                      // 벤더 정적 템플릿 3종을 이 스레드에서 다시 만든 1200×800 지도(줌 18)
        Deco = new AdornerDecorator { Child = Map };
        Scenario.Layout(Deco, 1200, 800);

        Vm = Scenario.Uninit<MapViewModel>();
        typeof(MapViewModel).GetProperty(nameof(MapViewModel.MainMap))!.SetValue(Vm, Map);
        Map.DataContext = Vm;                         // MapView 의 DataContext — 드롭 처리기(OnDrop)가 이것으로 VM 을 찾는다
        Scenario.SetField(Vm, "_log", Log.Object);
        Scenario.SetField(Vm, "_gMapDbSymbolService", Db.Object);
        Scenario.SetField(Vm, "_symbolProvider", new SymbolProvider());
        Scenario.SetField(Vm, "_markerFactory", new MarkerFactory(Mock.Of<ILogService>(), Devices));
        Scenario.SetField(Vm, "<DeviceProvider>k__BackingField", Devices);
        Scenario.SetField(Vm, "_propertyPanelFactory", new PropertyPanelFactory(Mock.Of<ILogService>(), Devices, new DeviceGroupProvider(Mock.Of<ILogService>())));
        Permission.Setup(p => p.CanEdit("map")).Returns(canEdit);
        Permission.Setup(p => p.CanControl("devices")).Returns(canControl);
        Permission.Setup(p => p.Name).Returns("시험관리자");
        Permission.Setup(p => p.LoginId).Returns("test-admin");   // 제어 명령 requested_by 는 표시이름이 아니라 login_id(브로커 v2.0.12 §7 머리)
        Scenario.SetField(Vm, "_permissionService", Permission.Object);
        Scenario.SetField(Vm, "_permissionResolved", true);
        Scenario.SetField(Vm, "_nudgePersistGate", new SemaphoreSlim(1, 1));

        var ea = new Mock<IEventAggregator>();
        ea.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
          .Callback<object, Func<Func<Task>, Task>, CancellationToken>((m, _, _) => Published.Add(m))
          .Returns(Task.CompletedTask);
        Scenario.SetField(Vm, "_eventAggregator", ea.Object);

        var recorder = new EditRecorder(Undo) { Context = Vm };
        Scenario.SetField(Vm, "_editRecorder", recorder);
        Scenario.SetField(Vm, "_undoService", Undo);
        Scenario.Call(Vm, "InitializeUndoRedo");
        Scenario.Call(Vm, "InitializeEditCommands");

        Scenario.SetField(Vm, "_isEditModeEnabled", true);
        Map.SetEditMode(true);

        // 제품 배선(MapViewModel.SetupAdornerIntegration) — 지도 통지 → VM 처리기. 실패하면 내부에서 Error 로그만 남기므로 그것까지 확인한다.
        Scenario.Call(Vm, "SetupAdornerIntegration");
        Assert.DoesNotContain(Errors, s => s.Contains("통합 실패"));
    }

    /// <summary>VM 이 남긴 Error 로그 전부.</summary>
    public List<string> AllErrors { get; } = new();

    /// <summary>
    /// 삼켜진 예외 진단용 — 시험 범위 밖 한 가지는 뺀다: 심볼 추가 뒤 fire-and-forget 으로 도는 레이어 트리 재구성
    /// (LoadLayersFromDbAsync — 지도 DB 서비스 · 레이어 패널은 이 장치에 없다)의 "레이어 로드 실패".
    /// </summary>
    public List<string> Errors => AllErrors.Where(s => !s.StartsWith("레이어 로드 실패")).ToList();

    /// <summary>STA 스레드 안에서 부른다.</summary>
    public static WiredMapRig Create(bool canEdit = true, bool canControl = true) => new(canEdit, canControl);

    /// <summary>지도 컨트롤 이벤트(field-like event)의 현재 구독 목록.</summary>
    public Delegate? Handlers(string eventName)
        => (Delegate?)typeof(GMapCustomControl).GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Map);

    /// <summary>지도 컨트롤 이벤트에 VM 의 이름 있는 처리기가 실제로 구독돼 있는가(배선 확인).</summary>
    public void AssertWired(string eventName, string vmHandler)
    {
        var d = Handlers(eventName);
        Assert.True(d != null, $"{eventName} 구독자 없음");
        Assert.Contains(d!.GetInvocationList(), h => ReferenceEquals(h.Target, Vm) && h.Method.Name == vmHandler);
    }

    /// <summary>지도 컨트롤이 그 통지를 낼 때와 똑같이 구독자들을 부른다(실제 마우스 좌표 판정은 헤디드 몫).</summary>
    public void Raise(string eventName, params object?[] args)
    {
        var d = Handlers(eventName);
        Assert.True(d != null, $"{eventName} 구독자 없음");
        d!.DynamicInvoke(args);
    }
}
