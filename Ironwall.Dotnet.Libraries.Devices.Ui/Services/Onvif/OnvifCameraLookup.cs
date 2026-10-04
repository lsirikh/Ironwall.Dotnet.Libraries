using Autofac;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Enums;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models;
using Ironwall.Dotnet.Libraries.OnvifSolution.Base.Models.Profiles;
using Ironwall.Dotnet.Libraries.OnvifSolution.Models;
using Ironwall.Dotnet.Libraries.OnvifSolution.Modules;
using Ironwall.Dotnet.Libraries.OnvifSolution.Services;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services.Onvif;

/****************************************************************************
   Purpose      : ONVIF 카메라 조회 — 검색으로 찾은 장치에 계정으로 붙어 장비 정보 · RTSP · PTZ 여부를 읽는다
   Created On   : 2026-10-04
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>조회 결과. 실패면 <see cref="Error"/> 만 뜻이 있다.</summary>
public sealed record OnvifLookupResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public string? Name { get; init; }
    public string? Location { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public string? Firmware { get; init; }
    public string? SerialNumber { get; init; }
    public string? HardwareId { get; init; }
    public string? MacAddress { get; init; }
    public string? OnvifVersion { get; init; }
    public string? DeviceServiceUrl { get; init; }
    public bool IsPtz { get; init; }
    public string? RtspMain { get; init; }
    public string? RtspSub { get; init; }

    public static OnvifLookupResult Fail(string error) => new() { Success = false, Error = error };
}

public interface IOnvifCameraLookup
{
    /// <summary>host:port 의 ONVIF 장치를 계정으로 조회한다. 던지지 않는다(취소 제외) — 실패는 결과에 담는다.</summary>
    Task<OnvifLookupResult> LookupAsync(string host, int port, string? userName, string? password, CancellationToken ct = default);
}

/// <summary>
/// 운영 ONVIF 경로(OnvifSolution <see cref="IOnvifService.InitializeProfileAsync"/>: 장비 정보 · 스코프 · NIC · 서비스 ·
/// Media · PTZ 클라이언트 · 프로파일 + 스트림 주소)로 조회한다. 영상 재생(LibVLC)은 없다 — 이 프로세스에서 돌려도 된다.
/// 서비스가 주어지지 않으면 <see cref="OnvifServiceModule"/> 로 처음 쓸 때 한 번 만든다(앱 DI 등록 불필요 —
/// CameraProviderRegistry.CreateDefault 와 같은 방식).
/// </summary>
public sealed class OnvifCameraLookup : IOnvifCameraLookup
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly Lazy<IOnvifService> _service;
    private readonly ILogService? _log;
    private readonly TimeSpan _timeout;

    public OnvifCameraLookup(IOnvifService? service = null, ILogService? log = null, TimeSpan? timeout = null)
    {
        _log = log;
        _timeout = timeout ?? DefaultTimeout;
        _service = service is not null
            ? new Lazy<IOnvifService>(service)
            : new Lazy<IOnvifService>(() => CreateDefaultService(log), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private static IOnvifService CreateDefaultService(ILogService? log)
    {
        var builder = new ContainerBuilder();
        if (log is not null) builder.RegisterInstance(log).As<ILogService>().SingleInstance();
        builder.RegisterModule(new OnvifServiceModule(log));
        return builder.Build().Resolve<IOnvifService>();
    }

    public async Task<OnvifLookupResult> LookupAsync(string host, int port, string? userName, string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host)) return OnvifLookupResult.Fail("주소가 없습니다.");
        if (port is < 1 or > 65535) return OnvifLookupResult.Fail($"포트가 잘못되었습니다({port}).");

        var conn = new ConnectionModel
        {
            DeviceName = host,
            IpAddress = host,
            PortOnvif = port,
            Username = userName ?? string.Empty,
            Password = password ?? string.Empty,
        };
        try
        {
            // WCF 호출은 토큰을 다 지키지 않는다 — 시간 제한으로 화면을 풀어 준다(뒤에서 끝나는 호출은 버린다).
            var model = await Task.Run(() => _service.Value.InitializeProfileAsync(conn, ct), ct)
                                  .WaitAsync(_timeout, ct).ConfigureAwait(false);
            return FromModel(model, port);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (TimeoutException)
        {
            _log?.Warning($"[ONVIF 조회] {host}:{port} 시간 초과({_timeout.TotalSeconds:0}s)");
            return OnvifLookupResult.Fail($"응답이 없습니다({_timeout.TotalSeconds:0}초).");
        }
        catch (Exception ex)
        {
            _log?.Warning($"[ONVIF 조회] {host}:{port} 실패: {ex.Message}");
            return OnvifLookupResult.Fail("조회하지 못했습니다: " + ex.Message);
        }
    }

    /// <summary>OnvifSolution 모델 → 조회 결과(순수). 장비 정보 단계가 실패하면 계정 거부 · 무응답으로 본다.</summary>
    internal static OnvifLookupResult FromModel(ICameraOnvifModel? model, int port)
    {
        if (model is null || model.CameraStatus == EnumCameraStatus.NOT_AVAILABLE || !model.IsDevicePossible)
            return OnvifLookupResult.Fail("장비 정보를 읽지 못했습니다 — 계정을 확인하세요.");

        var (main, sub) = PickStreams(model.CameraMedia?.Profiles);
        return new OnvifLookupResult
        {
            Success = true,
            Name = model.Name,
            Location = model.Location,
            Manufacturer = model.Manufacturer,
            Model = model.DeviceModel,
            Firmware = model.FirmwareVersion,
            SerialNumber = model.SerialNumber,
            HardwareId = model.HardwareId,
            MacAddress = model.MacAddress,
            OnvifVersion = model.OnvifVersion,
            DeviceServiceUrl = string.IsNullOrWhiteSpace(model.ServiceUri) ? $"http://{model.IpAddress}:{port}/onvif/device_service" : model.ServiceUri,
            IsPtz = model.IsPtzPossible,
            RtspMain = main,
            RtspSub = sub,
        };
    }

    /// <summary>
    /// 스트림 주소가 있는 프로파일 중 해상도 큰 것 = 메인, 작은 것 = 서브(하나뿐이면 서브 없음).
    /// 주소에 계정을 넣지 않는다 — 계정은 장비 행의 사용자 · 비밀번호 칸에 따로 저장된다.
    /// </summary>
    internal static (string? Main, string? Sub) PickStreams(IEnumerable<CameraProfileDto>? profiles)
    {
        var withUri = (profiles ?? Enumerable.Empty<CameraProfileDto>())
            .Where(p => !string.IsNullOrWhiteSpace(p?.MediaUri?.Uri))
            .Select((p, i) => (Uri: p.MediaUri!.Uri, Pixels: (long)(p.VideoEncoderConfig?.Resolution?.Width ?? 0) * (p.VideoEncoderConfig?.Resolution?.Height ?? 0), Order: i))
            .ToList();
        if (withUri.Count == 0) return (null, null);
        var ordered = withUri.OrderByDescending(p => p.Pixels).ThenBy(p => p.Order).ToList();
        var main = ordered[0].Uri;
        var sub = ordered.Count > 1 && !string.Equals(ordered[^1].Uri, main, StringComparison.Ordinal) ? ordered[^1].Uri : null;
        return (main, sub);
    }
}
