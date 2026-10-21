using System.IO;
using System.Windows;
using System.Windows.Controls;
using GMap.NET;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapControls;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Servers;
using Moq;
using Newtonsoft.Json;
using ScenarioShared;
using Xunit;

namespace GMaps.MapAux.Tests;

/// <summary>
/// 8.0.15 G36 방송 음량(volume 0~100, 0 허용 · 기본값 설정 AppSettings.Broadcast.DefaultVolume) ·
/// G25 음원 그룹 콤보(GET /api/file-groups, 403/404 는 "목록을 볼 수 없음" + 번호 입력 폴백). 창 없음.
/// </summary>
public class BroadcastVolumeFileGroupScenarioTests
{
    private const string GroupsJson = """
        { "success": true, "message": "FileGroup list retrieved",
          "data": [
            { "id": 7, "server_id": 3, "group_id": 2, "group_name": "화재경보", "files": ["fire01.mp3", "fire02.mp3"] },
            { "id": 4, "server_id": 3, "group_id": 1, "group_name": "경계 안내", "files": ["warn.mp3"] }
          ],
          "pagination": { "page": 1, "limit": 100, "total": 2, "total_pages": 1 } }
        """;

    private static ApiListResponse<FileGroupDto> Groups(int status, string json)
    {
        var resp = JsonConvert.DeserializeObject<ApiListResponse<FileGroupDto>>(json)!;
        resp.StatusCode = status;
        return resp;
    }

    // ── 발행 본문 ──

    [Theory]
    [InlineData(0, 0)]       // 0 허용(무음)
    [InlineData(35, 35)]
    [InlineData(150, 100)]   // 범위 밖은 자른다
    public async Task should_put_volume_in_play_body(int volume, int expected)
    {
        var client = new Mock<IBrokerRequestClient>();
        BroadcastPlayBodyDto? sent = null;
        client.Setup(c => c.RequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<BroadcastPlayBodyDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, BroadcastPlayBodyDto, TimeSpan?, CancellationToken>((_, _, b, _, _) => sent = b)
            .ReturnsAsync(BrokerRequestResult.Ok("ok", "r1"));
        var svc = new BroadcastControlService(new Mock<INatsService>().Object, Mock.Of<INatsSetupModel>(s => s.DomainNats == "sensorway" && s.GroupNats == "unit001"), client.Object);

        await svc.PublishPlayAsync(101, 7, 2, volume);

        Assert.NotNull(sent);
        Assert.Equal(expected, sent!.Volume);
        Assert.Contains($"\"volume\":{expected}", JsonConvert.SerializeObject(sent));
    }

    [Fact]
    public async Task should_put_volume_in_tts_body()
    {
        var nats = new Mock<INatsService>();
        string? json = null;
        nats.Setup(n => n.PublishAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, j) => json = j).Returns(Task.CompletedTask);
        var svc = new BroadcastControlService(nats.Object, Mock.Of<INatsSetupModel>(s => s.DomainNats == "d" && s.GroupNats == "g"), new Mock<IBrokerRequestClient>().Object);

        await svc.PublishTtsAsync(4, "대피하십시오", 0);

        Assert.Contains("\"volume\":0", json);
    }

    // ── 기본값 설정 ──

    [Theory]
    [InlineData("""{"AppSettings":{"Broadcast":{"DefaultVolume":40}}}""", 40)]
    [InlineData("""{"AppSettings":{"Broadcast":{"DefaultVolume":0}}}""", 0)]
    [InlineData("""{"AppSettings":{"Broadcast":{"DefaultVolume":250}}}""", 100)]
    [InlineData("""{"AppSettings":{"Broadcast":{"DefaultVolume":"loud"}}}""", 100)]
    [InlineData("""{"AppSettings":{}}""", 100)]
    [InlineData("not json", 100)]
    public void should_read_default_volume_from_settings(string content, int expected)
    {
        var path = Path.Combine(Path.GetTempPath(), $"harness_20261021_113151_39a2_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, content);
            Assert.Equal(expected, BroadcastControlService.LoadDefaultVolume(path));
        }
        finally { File.Delete(path); }
        Assert.Equal(100, BroadcastControlService.LoadDefaultVolume(path));   // 파일 없음
    }

    // ── 목록 해석 ──

    [Fact]
    public void should_list_group_names_when_file_groups_load()
    {
        var choice = BroadcastFileGroupChoice.From(Groups(200, GroupsJson));
        Assert.Null(choice.Status);
        Assert.Equal(new[] { "경계 안내 · 파일 1개", "화재경보 · 파일 2개" }, choice.Options.Select(o => o.Display));
        Assert.Equal(new[] { 4, 7 }, choice.Options.Select(o => o.Id));
    }

    [Theory]
    [InlineData(403, BroadcastFileGroupChoice.UnavailableText)]
    [InlineData(404, BroadcastFileGroupChoice.UnavailableText)]
    [InlineData(500, BroadcastFileGroupChoice.FailedText)]
    public void should_say_list_unavailable_on_forbidden_or_missing(int status, string text)
    {
        var choice = BroadcastFileGroupChoice.From(Groups(status, """{"success":false,"message":"x"}"""));
        Assert.Empty(choice.Options);
        Assert.Equal(text, choice.Status);
    }

    // ── 방송 창(지도 VM → 창 → 발행) ──

    private sealed record Rig(MapViewModel Vm, Mock<IBroadcastControlService> Broadcast, Mock<IDeviceApiService> Api);

    private static Rig MakeRig(ApiListResponse<FileGroupDto> groups)
    {
        var map = new GMapCustomControl { Width = 640, Height = 480, Position = new PointLatLng(37.5, 127.0), Zoom = 15 };
        var speaker = new SpeakerDeviceModel { Id = 55, DeviceType = EnumDeviceType.IpSpeaker, Server = new ServerModel { Id = 3 } };
        map.Markers.Add(Parts.Marker(speaker, EnumDeviceType.IpSpeaker, "스피커-55"));

        var api = new Mock<IDeviceApiService>();
        api.Setup(a => a.GetFileGroupsAsync(It.IsAny<int?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(groups);
        var broadcast = new Mock<IBroadcastControlService>();
        broadcast.Setup(b => b.PublishPlayAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(BrokerRequestResult.Ok("ok", "r1"));
        var permission = new Mock<IPermissionService>();
        permission.Setup(p => p.CanControl("broadcast")).Returns(true);

        var vm = Scenario.Uninit<MapViewModel>();
        typeof(MapViewModel).GetProperty(nameof(MapViewModel.MainMap))!.SetValue(vm, map);
        Scenario.SetField(vm, "_deviceApiService", api.Object);        Scenario.SetField(vm, "_deviceApiResolved", true);
        Scenario.SetField(vm, "_broadcastControlService", broadcast.Object);
        Scenario.SetField(vm, "_permissionService", permission.Object);
        Scenario.SetField(vm, "_permissionResolved", true);
        return new Rig(vm, broadcast, api);
    }

    private static void Style(BroadcastPlayControl panel)
    {
        panel.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Light.xaml", UriKind.Relative) });
        panel.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Ironwall.Dotnet.Libraries.GMaps.Ui;component/Themes/BroadcastPlayStyle.xaml", UriKind.Relative) });
        panel.Style = (System.Windows.Style)panel.Resources[typeof(BroadcastPlayControl)];
        Scenario.Layout(panel, 280, 400);
    }

    [Fact]
    public void should_pick_group_from_combo_and_send_with_volume() => OneSta.StaAsync(async () =>
    {
        var rig = MakeRig(Groups(200, GroupsJson));
        rig.Vm.ShowBroadcastPlayPanel(55);
        var panel = rig.Vm.BroadcastPlayPanel!;
        for (var i = 0; i < 50 && !panel.HasFileGroupList; i++) await Task.Delay(10);

        rig.Api.Verify(a => a.GetFileGroupsAsync(3, 1, 100, It.IsAny<CancellationToken>()), Times.Once());   // 스피커의 방송 서버로 거름
        Assert.True(panel.HasFileGroupList);
        Assert.Null(panel.FileGroupStatus);
        Assert.Equal(100, panel.Volume);   // 설정 없음 → 100

        Style(panel);
        var combo = (ComboBox)panel.Template.FindName("PART_FileGroupCombo", panel);
        Assert.True(combo.IsVisible || ((FrameworkElement)combo.Parent).Visibility == Visibility.Visible);
        Assert.Equal(new[] { "경계 안내 · 파일 1개", "화재경보 · 파일 2개" }, combo.Items.Cast<BroadcastFileGroupOption>().Select(o => o.Display));

        combo.SelectedIndex = 1;   // 화재경보
        Assert.Equal(7, panel.FileGroupId);
        panel.Volume = 0;
        panel.SendCommand.Execute(null);
        for (var i = 0; i < 50 && rig.Vm.BroadcastPlayPanel != null; i++) await Task.Delay(10);

        rig.Broadcast.Verify(b => b.PublishPlayAsync(55, 7, 1, 0), Times.Once());
        Assert.Null(rig.Vm.BroadcastPlayPanel);   // 성공 → 창 닫힘
    });

    [Fact]
    public void should_show_unavailable_and_keep_number_entry_when_list_forbidden() => OneSta.StaAsync(async () =>
    {
        var rig = MakeRig(Groups(403, """{"success":false,"message":"Forbidden"}"""));
        rig.Vm.ShowBroadcastPlayPanel(55);
        var panel = rig.Vm.BroadcastPlayPanel!;
        for (var i = 0; i < 50 && panel.FileGroupStatus == BroadcastFileGroupChoice.LoadingText; i++) await Task.Delay(10);

        Assert.False(panel.HasFileGroupList);
        Assert.Equal("목록을 볼 수 없음 · 번호로 입력하세요", panel.FileGroupStatus);

        Style(panel);
        var combo = (ComboBox)panel.Template.FindName("PART_FileGroupCombo", panel);
        Assert.Equal(Visibility.Collapsed, ((FrameworkElement)combo.Parent).Visibility);
        var status = (TextBlock)panel.Template.FindName("PART_FileGroupStatus", panel);
        Assert.Equal("목록을 볼 수 없음 · 번호로 입력하세요", status.Text);
        Assert.Equal(Visibility.Visible, status.Visibility);

        panel.FileGroupId = 12;   // 번호로 직접
        panel.Volume = 60;
        panel.SendCommand.Execute(null);
        for (var i = 0; i < 50 && rig.Vm.BroadcastPlayPanel != null; i++) await Task.Delay(10);
        rig.Broadcast.Verify(b => b.PublishPlayAsync(55, 12, 1, 60), Times.Once());
    });
}
