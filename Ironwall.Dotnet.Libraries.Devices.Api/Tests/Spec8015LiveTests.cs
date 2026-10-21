using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Tests.LiveApi;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Devices.Api.Tests;

/// <summary>
/// GOP API 8.0.15 명세 대조(L2) — 로컬 시험 서버(8.0.11)에서 실제로 읽히는지 확인한다.
/// 만드는 자료 이름에는 <c>harness_20261021_113151_39a2</c> 를 붙이고 <c>finally</c> 에서 지운다.
/// 환경(계정 파일 · 서버)이 없으면 건너뛴다. 쓰기는 loopback · 사설망에서만 돈다.
/// </summary>
[Collection(nameof(DeviceApiCollection))]
[Trait("Scenario", "Spec8015-Live")]
public class Spec8015LiveTests
{
    private const string Tag = "harness_20261021_113151_39a2";
    private readonly IDeviceApiService _svc;
    private readonly ITestOutputHelper _output;

    public Spec8015LiveTests(DeviceApiFixture fixture, ITestOutputHelper output)
    {
        _svc = fixture.Service;
        _output = output;
    }

    [LiveApiWriteFact]
    public async Task G21_라이브_프리셋_12개를_전부_읽는다()
    {
        var cam = await _svc.CreateCameraAsync(new CameraDeviceDto
        {
            NumberDevice = 1, NameDevice = $"{Tag}_cam", TypeDevice = "IpCamera", Status = "ACTIVATED", IsEnable = true,
            IpAddress = "192.168.250.2", IpPort = 554, UserName = "admin", UserPassword = "admin123", Mode = "ONVIF", Category = "PTZ",
        });
        Assert.True(cam.Success, $"카메라 생성 실패: {cam.Error?.Code} {cam.Error?.Message ?? cam.Message} {cam.Error?.Details}");
        var cameraId = cam.Data!.Id;
        try
        {
            for (var i = 1; i <= 12; i++)
            {
                var p = await _svc.CreatePresetAsync(cameraId, new CameraPresetDto { PresetIndex = i, PresetName = $"{Tag}_p{i}", TouringTime = 5 });
                Assert.True(p.Success, $"프리셋 {i} 생성 실패: {p.Error?.Code} {p.Error?.Message ?? p.Message}");
            }

            var list = await _svc.GetPresetsAsync(cameraId);
            _output.WriteLine($"camera={cameraId} presets={list.Data?.Items.Count} total={list.Data?.Total}");

            Assert.True(list.Success, list.Error?.Message);
            Assert.Equal(12, list.Data!.Items.Count);
            Assert.Equal(Enumerable.Range(1, 12), list.Data.Items.Select(p => p.PresetIndex).OrderBy(x => x));
        }
        finally
        {
            var del = await _svc.DeleteCameraAsync(cameraId);
            _output.WriteLine($"cleanup camera={cameraId} deleted={del.Success}");
        }
    }

    [LiveApiFact]
    public async Task G25_라이브_음원그룹_목록이_DTO로_읽힌다()
    {
        var r = await _svc.GetFileGroupsAsync();
        _output.WriteLine($"file-groups status={r.StatusCode} success={r.Success} count={r.Data?.Count} total={r.Pagination?.Total} error={r.Error?.Code}");

        Assert.True(r.Success || r.StatusCode == 403, $"{r.StatusCode} {r.Error?.Code} {r.Error?.Message}");
        if (!r.Success) return;
        Assert.NotNull(r.Pagination);
        foreach (var g in r.Data!)
        {
            _output.WriteLine($"  id={g.Id} server={g.ServerId} group={g.GroupId} name={g.Name} files={g.Files.Count}");
            Assert.True(g.Id > 0);
        }
    }

    [LiveApiFact]
    public async Task G24_라이브_전함체_최신계측이_DTO로_읽힌다()
    {
        var r = await _svc.GetAllEnclosureLatestMetricsAsync();
        _output.WriteLine($"enclosure-metrics status={r.StatusCode} success={r.Success} count={r.Data?.Count}");

        Assert.True(r.Success, $"{r.StatusCode} {r.Error?.Code} {r.Error?.Message}");
        foreach (var row in r.Data!.Take(20))
        {
            var m = row.LatestMetrics;
            _output.WriteLine($"  enc={row.EnclosureId} name={row.EnclosureName} id={m?.Id} temp={m?.TemperatureValue} observed={m?.ObservedAt} created={m?.CreatedAt}");
            Assert.True(row.EnclosureId > 0);
            if (m != null) Assert.True(m.Id > 0);
        }
    }

    [LiveApiWriteFact]
    public async Task G22_라이브_함체_안_장비를_넣고_contents와_역조회로_읽는다()
    {
        var enc = await _svc.CreateEnclosureAsync(new EnclosureDeviceDto
        {
            NumberDevice = 1, NameDevice = $"{Tag}_enc", TypeDevice = "Enclosure", Status = "ACTIVATED", IsEnable = true,
        });
        Assert.True(enc.Success, $"함체 생성 실패: {enc.Error?.Code} {enc.Error?.Message ?? enc.Message} {enc.Error?.Details}");
        var ctl = await _svc.CreateControllerAsync(new ControllerDeviceDto
        {
            NumberDevice = 1, NameDevice = $"{Tag}_ctl", TypeDevice = "Controller", Status = "ACTIVATED", IsEnable = true,
            IpAddress = "192.168.250.1", IpPort = 9001,
        });
        try
        {
            Assert.True(ctl.Success, $"제어기 생성 실패: {ctl.Error?.Code} {ctl.Error?.Message ?? ctl.Message} {ctl.Error?.Details}");
            var enclosureId = enc.Data!.Id;
            var controllerId = ctl.Data!.Id;

            var patch = await _svc.PatchEnclosureAsync(enclosureId, new EnclosureDeviceDto
            {
                NumberDevice = enc.Data.NumberDevice, NameDevice = enc.Data.NameDevice, DeviceIds = new List<int> { controllerId },
            });
            _output.WriteLine($"patch device_ids status={patch.StatusCode} success={patch.Success} error={patch.Error?.Message}");
            Assert.True(patch.Success, $"{patch.StatusCode} {patch.Error?.Message} {patch.Error?.Details}");

            var read = await _svc.GetEnclosureByIdAsync(enclosureId, include: "contents");
            var item = Assert.Single(read.Data!.Contents!);
            _output.WriteLine($"contents: id={item.Id} category={item.CategoryDevice} name={item.NameDevice}");
            Assert.Equal(controllerId, item.Id);
            Assert.Equal($"{Tag}_ctl", item.NameDevice);

            var reverse = await _svc.GetEnclosuresAsync(controllerId);
            _output.WriteLine($"reverse status={reverse.StatusCode} count={reverse.Data?.Count} error={reverse.Error?.Code}");
            Assert.True(reverse.Success, $"{reverse.Error?.Code} {reverse.Error?.Message}");
            Assert.Equal(enclosureId, Assert.Single(reverse.Data!).Id);

            var basic = await _svc.GetEnclosureByIdAsync(enclosureId);
            Assert.Null(basic.Data!.Contents);   // 기본 응답에는 키가 없다
        }
        finally
        {
            if (ctl.Success) _output.WriteLine($"cleanup controller={ctl.Data!.Id} deleted={(await _svc.DeleteControllerAsync(ctl.Data.Id)).Success}");
            _output.WriteLine($"cleanup enclosure={enc.Data!.Id} deleted={(await _svc.DeleteEnclosureAsync(enc.Data.Id)).Success}");
        }
    }
}
