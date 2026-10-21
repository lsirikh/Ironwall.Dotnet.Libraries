using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Services.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : 스피커 방송 제어 NATS 발행 서비스
                  BROADCAST_PLAY / BROADCAST_STOP은 REQ 발행 + RSP 확인(v1.5.2 §6.4),
                  TTS(비스펙 cmd)는 서버 RSP 지원 확인 전까지 PUB 유지(PRD OQ-1).
                  Subject: "{DomainNats}.{GroupNats}.broadcast_manager.{cmd}"
   Created By   : GHLee
   Created On   : 2026-03-05
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class BroadcastControlService : IBroadcastControlService
{
    #region - Ctors -
    public BroadcastControlService(INatsService natsService, INatsSetupModel natsSetupModel, IBrokerRequestClient brokerClient)
    {
        _natsService = natsService;
        _natsSetup = natsSetupModel;
        _brokerClient = brokerClient;
    }

    /// <summary>TTS(PUB) 본문에도 보낸 사람 · 단말을 싣는 생성자(G7-4). REQ 낱말은 <see cref="IBrokerRequestClient"/> 가 싣는다.</summary>
    public BroadcastControlService(INatsService natsService, INatsSetupModel natsSetupModel, IBrokerRequestClient brokerClient, ICommandRequester? requester)
        : this(natsService, natsSetupModel, brokerClient)
    {
        _requester = requester;
    }
    #endregion

    #region - Implementation of Interface -
    public Task<BrokerRequestResult> PublishPlayAsync(int speakerId, int fileGroupId, int repeat)
        => PublishPlayAsync(speakerId, fileGroupId, repeat, BroadcastPlayBodyDto.VolumeDefault);

    /// <summary>음원 재생 + 음량(0~100, 0 허용 — 범위 밖은 DTO 가 자른다 · 8.0.15 G36).</summary>
    public async Task<BrokerRequestResult> PublishPlayAsync(int speakerId, int fileGroupId, int repeat, int volume)
    {
        var body = new BroadcastPlayBodyDto
        {
            SpeakerIds = [speakerId],
            FileGroupId = fileGroupId,
            Repeat = repeat,
            Volume = volume
        };
        return await _brokerClient.RequestAsync(
            BuildSubject("play"), EnumGopCommand.BROADCAST_PLAY.ToString(), body).ConfigureAwait(false);
    }

    public Task PublishTtsAsync(int speakerId, string message)
        => PublishTtsAsync(speakerId, message, TtsBodyDto.VolumeDefault);

    /// <summary>TTS + 음량(0~100, 0 허용 · 8.0.15 G36).</summary>
    public async Task PublishTtsAsync(int speakerId, string message, int volume)
    {
        var body = new TtsBodyDto
        {
            SpeakerIds = [speakerId],
            Message = message,
            Volume = volume
        };
        var msg = body.ToBrokerPublish("TTS", "GIS");
        var json = JsonConvert.SerializeObject(msg);
        if (_requester != null) json = CommandRequester.Stamp(json, _requester.RequestedBy, _requester.RequestedClientId);
        await _natsService.PublishAsync(BuildSubject("tts"), json).ConfigureAwait(false);
    }

    public async Task<BrokerRequestResult> PublishStopAsync(int speakerId)
    {
        var body = new BroadcastStopBodyDto { SpeakerIds = [speakerId] };
        return await _brokerClient.RequestAsync(
            BuildSubject("stop"), EnumGopCommand.BROADCAST_STOP.ToString(), body).ConfigureAwait(false);
    }

    /// <summary>
    /// 마이크 방송 cmd 지원 여부. <b>서버 회신 전까지 false</b>(PRD §7 / 계획 SETUP-02).
    /// 배선은 아래에 전부 되어 있으므로, 서버가 받아들이면 이 값만 true 로 바꾸면 된다.
    /// </summary>
    public bool IsMicCommandSupported => false;

    public Task<BrokerRequestResult> PublishMicStartAsync(int speakerId) => PublishMicAsync(speakerId, start: true);

    public Task<BrokerRequestResult> PublishMicStopAsync(int speakerId) => PublishMicAsync(speakerId, start: false);

    private async Task<BrokerRequestResult> PublishMicAsync(int speakerId, bool start)
    {
        // 규격 미확정 상태에서 브로커에 모르는 cmd 를 던지면 매니저 로그만 더럽히고 아무 일도 일어나지 않는다.
        // 그래서 여기서 막고, 사유를 그대로 UI 로 돌려준다.
        if (!IsMicCommandSupported)
            return BrokerRequestResult.Fail(EnumBrokerFailure.Invalid, MicNotSupportedMessage);

        var body = new BroadcastMicBodyDto { SpeakerIds = [speakerId] };
        var command = start ? MicStartCommand : MicStopCommand;
        return await _brokerClient.RequestAsync(
            BuildSubject(start ? "mic-start" : "mic-stop"), command, body).ConfigureAwait(false);
    }

    /// <summary>마이크 cmd 이름 — 서버 요청서(PRD §7)에 제안한 그대로.</summary>
    internal const string MicStartCommand = "BROADCAST_MIC_START";
    internal const string MicStopCommand = "BROADCAST_MIC_STOP";
    internal const string MicNotSupportedMessage = "마이크 방송 규격이 확정되지 않았습니다(서버 회신 대기).";
    #endregion

    #region - 기본 음량 설정 -
    /// <summary>appsettings 키 — <c>AppSettings.Broadcast.DefaultVolume</c>(0~100). 없거나 못 읽으면 100.</summary>
    public const string DefaultVolumeSection = "Broadcast";
    public const string DefaultVolumeKey = "DefaultVolume";

    /// <summary>방송 창의 음량 기본값을 appsettings 에서 읽는다(읽기만 · 키가 없으면 100, 범위 밖은 0~100 으로 자름).</summary>
    public static int LoadDefaultVolume(string? appSettingsPath = null)
    {
        try
        {
            var path = appSettingsPath ?? Ironwall.Dotnet.Libraries.Base.Services.AppSettingsLocation.AppSettingsReadPath;
            if (!System.IO.File.Exists(path)) return BroadcastPlayBodyDto.VolumeDefault;
            var token = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(path))
                ["AppSettings"]?[DefaultVolumeSection]?[DefaultVolumeKey];
            return ClampVolume(token is { Type: Newtonsoft.Json.Linq.JTokenType.Integer or Newtonsoft.Json.Linq.JTokenType.Float }
                ? (int)Math.Round(token.ToObject<double>()) : null);
        }
        catch { return BroadcastPlayBodyDto.VolumeDefault; }
    }

    /// <summary>음량 칸 값 정리 — null 은 100, 그 밖은 0~100.</summary>
    public static int ClampVolume(int? volume)
        => volume is { } v ? Math.Clamp(v, BroadcastPlayBodyDto.VolumeMin, BroadcastPlayBodyDto.VolumeMax) : BroadcastPlayBodyDto.VolumeDefault;
    #endregion

    #region - Processes -
    /// <summary>
    /// NATS Subject 빌드: "{DomainNats}.{GroupNats}.broadcast_manager.{cmd}"
    /// </summary>
    public string BuildSubject(string cmd)
        => $"{_natsSetup.DomainNats}.{_natsSetup.GroupNats}.broadcast_manager.{cmd}";
    #endregion

    #region - 음원 그룹 목록 (8.0.15 G25) -
    /// <summary>
    /// 방송 창이 고를 음원 그룹 목록을 읽는다(<c>GET /api/file-groups</c>, 스피커의 방송 서버로 거름).
    /// 장비 API 가 없거나 실패해도 예외를 던지지 않는다 — 안내 문구 + 번호 입력으로 쓴다.
    /// </summary>
    public static async Task<BroadcastFileGroupChoice> LoadFileGroupsAsync(
        Ironwall.Dotnet.Libraries.Devices.Api.Services.IDeviceApiService? deviceApi, int? serverId, CancellationToken token = default)
    {
        if (deviceApi == null) return BroadcastFileGroupChoice.Unavailable;
        try
        {
            var resp = await deviceApi.GetFileGroupsAsync(serverId, 1, 100, token).ConfigureAwait(false);
            return BroadcastFileGroupChoice.From(resp);
        }
        catch { return BroadcastFileGroupChoice.Failed; }
    }
    #endregion

    #region - Attributes -
    private readonly INatsService _natsService;
    private readonly INatsSetupModel _natsSetup;
    private readonly IBrokerRequestClient _brokerClient;
    private readonly ICommandRequester? _requester;
    #endregion
}

/// <summary>방송 창 음원 그룹 콤보의 한 줄 — <paramref name="Id"/> 는 BROADCAST_PLAY <c>file_group_id</c> 로 보내는 번호.</summary>
public sealed record BroadcastFileGroupOption(int Id, string Name, int FileCount)
{
    /// <summary>화면 글 — 이름 · 파일 수(이름이 없으면 번호).</summary>
    public string Display => string.IsNullOrWhiteSpace(Name)
        ? $"음원 그룹 {Id}"
        : FileCount > 0 ? $"{Name} · 파일 {FileCount}개" : Name;
}

/// <summary>음원 그룹 목록 결과 — 고를 목록과 상태 한 줄(목록이 있으면 null).</summary>
public sealed record BroadcastFileGroupChoice(IReadOnlyList<BroadcastFileGroupOption> Options, string? Status)
{
    public const string LoadingText = "음원 목록을 불러오는 중";
    public const string UnavailableText = "목록을 볼 수 없음 · 번호로 입력하세요";
    public const string FailedText = "목록을 불러오지 못함 · 번호로 입력하세요";
    public const string EmptyText = "등록된 음원 그룹이 없음 · 번호로 입력하세요";

    public static BroadcastFileGroupChoice Unavailable { get; } = new(Array.Empty<BroadcastFileGroupOption>(), UnavailableText);
    public static BroadcastFileGroupChoice Failed { get; } = new(Array.Empty<BroadcastFileGroupOption>(), FailedText);

    /// <summary>
    /// 응답 해석 — 403(권한 없음) · 404(경로 없는 옛 서버) · 지원 안 함은 "목록을 볼 수 없음", 그 밖 실패는 "불러오지 못함".
    /// 어느 쪽이든 번호 입력은 그대로 쓴다.
    /// </summary>
    public static BroadcastFileGroupChoice From(Ironwall.Dotnet.Libraries.Messages.Defines.Apis.ApiListResponse<Ironwall.Dotnet.Libraries.Messages.Dto.Devices.FileGroupDto>? resp)
    {
        if (resp == null) return Failed;
        if (!resp.Success)
            return resp.StatusCode is 403 or 404 || resp.Error?.Code == "NOT_SUPPORTED" ? Unavailable : Failed;
        var options = (resp.Data ?? new())
            .Where(g => g.Id > 0)
            .Select(g => new BroadcastFileGroupOption(g.Id, g.Name?.Trim() ?? string.Empty, g.Files?.Count ?? 0))
            .OrderBy(o => o.Name, StringComparer.CurrentCulture).ThenBy(o => o.Id)
            .ToList();
        return new(options, options.Count == 0 ? EmptyText : null);
    }
}
