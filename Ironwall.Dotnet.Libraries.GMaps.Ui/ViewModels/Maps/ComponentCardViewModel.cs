using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Components;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Utils;
using Ironwall.Dotnet.Libraries.Utils.Converters;
using Ironwall.Dotnet.Monitoring.Models.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;

/// <summary>
/// 지도 조립 카드(L3, component-display-unify FR-05) — 아이콘을 고르면 옆에 뜨는 부품 표. 장비 콘솔 "부품 상태" 절을 줄여 쓴다:
/// 부품 · 상태 · 건강 · 사유(마지막 변화는 머리), 고장 먼저.
/// </summary>
/// <remarks>
/// <para>축 미수신(부품 축을 못 받음)이면 표 대신 "부품 정보 없음" 한 줄(FR-08). 6.3 서버(축 자체가 없음)에서는 카드를 아예 열지 않는다 —
/// 그 판단은 <see cref="ComponentCardService"/> 가 한다.</para>
/// <para>항법 단추는 <see cref="IDeviceConsoleNavigator"/> 가 등록돼 있을 때만 보인다. 결선은 센서일 때만.</para>
/// </remarks>
public sealed class ComponentCardViewModel : PropertyChangedBase
{
    private readonly IDeviceConsoleNavigator? _navigator;

    public ComponentCardViewModel(GMapPidsMarker marker, IDeviceConsoleNavigator? navigator, DateTime? today)
    {
        Marker = marker ?? throw new ArgumentNullException(nameof(marker));
        _navigator = navigator;
        _today = today;
        OpenConsoleCommand = new RelayCommand(_ => { if (CanOpenConsole) _navigator!.OpenDevice(DeviceId); });
        OpenWiringCommand = new RelayCommand(_ => { if (CanOpenWiring) _navigator!.OpenWiring(DeviceId); });
        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke());
        Refresh();
    }

    private readonly DateTime? _today;

    /// <summary>[닫기] · Esc — 서비스가 카드를 걷는다.</summary>
    public event System.Action? CloseRequested;

    public GMapPidsMarker Marker { get; }

    public int DeviceId => Marker.LinkedDeviceId;

    /// <summary>머리 — "북측 5구간 보강 · 스마트센서2".</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>머리 둘째 줄 — "마지막 변화 09:41:07 · 레이더". 없으면 빈 글.</summary>
    public string SubTitle { get; private set; } = string.Empty;

    /// <summary>부품 표(고장 먼저).</summary>
    public ComponentTableModel Table { get; private set; } = new(ComponentSnapshot.None, ComponentSortMode.FaultFirst, null);

    /// <summary>표를 보일 수 있는가 — false 면 <see cref="NoInfoText"/>.</summary>
    public bool HasTable => Table.IsAvailable && Table.HasRows;

    /// <summary>표가 없을 때의 한 줄 — "부품 정보 없음" / "부품 없음".</summary>
    public string NoInfoText => Table.IsAvailable ? ComponentDisplay.NoComponentsText : ComponentDisplay.NoInfoText;

    public bool CanOpenConsole => _navigator != null && DeviceId > 0 && SafeCan(() => _navigator.CanOpenDevice(DeviceId));

    public bool CanOpenWiring => _navigator != null && DeviceId > 0 && IsSensor && SafeCan(() => _navigator.CanOpenWiring(DeviceId));

    /// <summary>항법 단추 줄을 보이는가.</summary>
    public bool HasActions => CanOpenConsole || CanOpenWiring;

    public RelayCommand OpenConsoleCommand { get; }
    public RelayCommand OpenWiringCommand { get; }
    public RelayCommand CloseCommand { get; }

    private bool IsSensor => Marker.LinkedDevice is ISensorDeviceModel
        || Marker.LinkedDevice?.CategoryDevice == Ironwall.Dotnet.Libraries.Enums.EnumDeviceCategory.Sensor;

    /// <summary>마커의 부품 요약이 바뀌었다(SYNC_DEVICE) — 표를 다시 만든다. 호출 스레드: UI.</summary>
    public void Refresh()
    {
        var summary = Marker.ComponentSummary ?? ComponentHealthSummary.None;
        var name = string.IsNullOrWhiteSpace(Marker.Title) ? UiKoreanMap.To(Marker.DeviceType) : Marker.Title!.Trim();
        Title = $"{name} · {UiKoreanMap.To(Marker.DeviceType)}";
        Table = new ComponentTableModel(summary.Snapshot, ComponentSortMode.FaultFirst, _today);
        SubTitle = Table.LastChangeText;
        NotifyOfPropertyChange(string.Empty);
    }

    private static bool SafeCan(Func<bool> probe)
    {
        try { return probe(); }
        catch (Exception) { return false; }   // 호스트 입구가 실패하면 단추를 숨긴다
    }
}
