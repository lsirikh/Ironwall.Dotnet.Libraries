using System.IO;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers.Detail;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Utils.Converters;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using ScenarioShared;
using Xunit;

namespace GMaps.MapLayer.Tests;

/// <summary>
/// 카탈로그 MAP-142 — 심볼 · SYNC_DEVICE 이름: 자기 장비 이름을 API 로 바꾼 뒤(서버 SYNC_DEVICE UPDATED)
/// 우클릭 「{장치명}페이지」 · 상세 기본 탭은 새 이름 · <b>심볼 제목은 그대로(설계)</b>.
/// SYNC_DEVICE UPDATED 를 받으면 호스트는 DeviceProviderService.FetchDeviceByIdAsync 로 단건 재조회해
/// 공급자 캐시의 <b>같은 인스턴스</b>에 속성을 제자리 복사한다(UpdateDeviceProperties — `existing.DeviceName = newDevice.DeviceName`).
/// 지도 심볼은 그 인스턴스를 LinkedDevice 로 잡고 있으므로, 여기서는 그 제자리 갱신을 같은 인스턴스에 그대로 재현하고
/// 그 뒤 지도 쪽 제품 경로(상세 VM Load · 액션 바 · 심볼 컨트롤 툴팁 · 우클릭 메뉴 생성 소스)를 확인한다.
/// 우클릭 메뉴는 ShowMarkerContextMenu 가 지역 ContextMenu 를 만들어 곧바로 IsOpen(팝업 창) 하므로 띄우지 않고 메뉴 글자를 만드는 소스를 판독한다.
/// 창 · 서버 · NATS · 지도 DB 없음.
/// </summary>
public class SymbolSyncDeviceRenameScenarioTests
{
    private const string OldName = "정문 감시카메라", NewName = "정문 감시카메라-R27", SymbolTitle = "정문-CAM";

    private static (CameraDeviceModel device, GMapPidsMarker marker) Camera()
    {
        var device = new CameraDeviceModel { Id = 9142, DeviceNumber = 142, DeviceName = OldName, DeviceType = EnumDeviceType.IpCamera, Status = EnumDeviceStatus.ACTIVATED };
        var marker = new GMapPidsMarker(new FakeLog(), new PidsSymbolModel
        {
            Id = 1420, Title = SymbolTitle, DeviceType = EnumDeviceType.IpCamera, LinkedDeviceId = device.Id,
            Latitude = 37.5, Longitude = 127.0, Visible = true, Category = EnumMarkerCategory.PIDS_EQUIPMENT, Zoom = 1,
        });
        marker.Width = 60; marker.Height = 60; marker.IsLayerEnabled = true; marker.IsVisible = true;
        marker.LinkedDevice = device;
        return (device, marker);
    }

    /// <summary>호스트 SYNC_DEVICE UPDATED 처리의 공급자 쪽 효과 — 새 응답의 이름을 캐시의 같은 인스턴스에 제자리 복사.</summary>
    private static void ApplySyncDeviceRename(IBaseDeviceModel cached, string newName)
    {
        var fetched = new CameraDeviceModel { Id = cached.Id, DeviceNumber = cached.DeviceNumber, DeviceName = newName, DeviceType = cached.DeviceType, Status = cached.Status };
        cached.DeviceName = fetched.DeviceName;   // DeviceProviderService.UpdateDeviceProperties 첫 줄과 같다(참조 유지)
    }

    private static string Field(SymbolDetailViewModel vm, string label)
        => vm.SelectedTab!.Fields.Single(f => f.Label == label).Value;

    [Trait("Scenario", "MAP-142")]
    [Fact]
    public void should_show_new_device_name_in_detail_basic_tab_but_keep_symbol_title_when_sync_device_renames_linked_device() => Scenario.Sta(() =>
    {
        var (device, marker) = Camera();
        var control = new GMapMarkerPidsControl(marker);

        // 바뀌기 전: 상세 기본 탭 장비명 = 옛 이름
        var before = new SymbolDetailViewModel();
        before.Load(marker, new SymbolDetailContext(EnumDeviceType.IpCamera, HasDevice: true));
        Assert.Equal(SymbolDetailTab.Basic, before.SelectedTab!.Tab);                  // 장비 연결 심볼의 기본 탭
        Assert.Equal(OldName, Field(before, "장비명"));

        ApplySyncDeviceRename(device, NewName);
        Assert.Same(device, marker.LinkedDevice);                                      // 심볼은 같은 인스턴스를 그대로 잡고 있다

        // 바뀐 뒤 상세를 연다: 기본 탭(GMaps.SymbolDetail.Tab.Basic)의 장비명 = 새 이름
        var after = new SymbolDetailViewModel();
        after.Load(marker, new SymbolDetailContext(EnumDeviceType.IpCamera, HasDevice: true));
        Assert.Equal(SymbolDetailTab.Basic, after.SelectedTab!.Tab);
        Assert.Equal("GMaps.SymbolDetail.Tab.Basic", after.SelectedTab.AutomationId);
        Assert.Equal(NewName, Field(after, "장비명"));
        Assert.DoesNotContain(after.Tabs.SelectMany(t => t.Fields), f => f.Value == OldName);   // 옛 이름이 어디에도 남지 않는다

        // 심볼 제목은 지도 DB 값이라 그대로(설계): 모델 · 마커 · 상세 머리 · 심볼 툴팁(UIA HelpText)
        Assert.Equal(SymbolTitle, marker.Title);
        Assert.Equal(SymbolTitle, marker.Model.Title);
        Assert.Equal(SymbolTitle, after.SymbolName);
        Assert.Equal($"상세 보기 — {UiKoreanMap.To(EnumDeviceType.IpCamera)} · {SymbolTitle}", after.HeaderText);
        Assert.StartsWith(SymbolTitle, control.SymbolToolTip);
        Assert.DoesNotContain(NewName, control.SymbolToolTip);
    });

    [Trait("Scenario", "MAP-142")]
    [Fact]
    public void should_keep_device_page_action_labelled_by_kind_not_device_name_after_sync_device_rename() => Scenario.Sta(() =>
    {
        var (device, marker) = Camera();
        ApplySyncDeviceRename(device, NewName);

        var vm = new SymbolDetailViewModel();
        vm.Load(marker, new SymbolDetailContext(EnumDeviceType.IpCamera, HasDevice: true, WebServerEnabled: true));
        var kind = UiKoreanMap.To(EnumDeviceType.IpCamera);

        // 상세 액션 바(우클릭 메뉴와 1:1) 「{장치명}페이지」 의 {장치명} = 장비 종류 이름(r27 2회차 실측 「감시카메라페이지」)
        var page = vm.Actions.Single(a => a.Action == SymbolDetailAction.DevicePage);
        Assert.Equal($"{kind}페이지", page.Header);
        Assert.Equal("GMaps.SymbolDetail.Action.DevicePage", page.AutomationId);
        Assert.All(vm.Actions, a => Assert.DoesNotContain(NewName, a.Header));
        Assert.All(vm.Actions, a => Assert.DoesNotContain(OldName, a.Header));
    });

    [Trait("Scenario", "MAP-142")]
    [Fact]
    public void should_build_right_click_device_page_header_from_device_kind_so_rename_does_not_change_menu()
    {
        // 우클릭 메뉴 = ShowMarkerContextMenu(지역 ContextMenu 를 만들고 곧바로 IsOpen — 팝업 창이라 여기서 띄우지 않는다).
        // 메뉴 글자가 장비 이름(DeviceName)이 아니라 종류(DeviceType switch)에서 만들어짐을 소스로 확인한다.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var src = File.ReadAllText(Path.Combine(dir!.FullName, "Ironwall.Dotnet.Libraries.GMaps.Ui", "ViewModels", "Maps", "MapViewModel.cs"));
        int start = src.IndexOf("public void ShowMarkerContextMenu(IEditableMarker marker, Point screenPosition)", StringComparison.Ordinal);
        Assert.True(start > 0, "ShowMarkerContextMenu 없음");
        int open = src.IndexOf("menu.IsOpen = true", start, StringComparison.Ordinal);
        Assert.True(open > start, "ShowMarkerContextMenu 의 menu.IsOpen 을 찾지 못함");
        var body = src[start..open];

        int devName = body.IndexOf("var devName = pidsMarker.DeviceType switch", StringComparison.Ordinal);
        int header = body.IndexOf("Header = $\"{devName}페이지\"", StringComparison.Ordinal);
        Assert.True(devName > 0, "「{장치명}페이지」의 장치명이 종류(DeviceType)에서 오지 않는다");
        Assert.True(header > devName, "「{장치명}페이지」 머리글이 devName 으로 만들어지지 않는다");
        Assert.Contains("EnumDeviceType.IpCamera    => \"감시카메라\"", body);
        Assert.DoesNotContain("DeviceName", body);                                     // 메뉴 어디에도 장비 이름을 쓰지 않는다 — 이름 변경은 메뉴를 바꾸지 않는다
    }
}
