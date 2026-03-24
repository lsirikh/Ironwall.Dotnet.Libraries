using Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/****************************************************************************
   Purpose      : 스피커 방송 제어 NATS 발행 서비스
                  BROADCAST_PLAY / TTS / BROADCAST_STOP 메시지를 발행한다.
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
    public BroadcastControlService(INatsService natsService, INatsSetupModel natsSetupModel)
    {
        _natsService = natsService;
        _natsSetup = natsSetupModel;
    }
    #endregion

    #region - Implementation of Interface -
    public async Task PublishPlayAsync(int speakerId, int fileGroupId, int repeat)
    {
        var body = new BroadcastPlayBodyDto
        {
            SpeakerIds = [speakerId],
            FileGroupId = fileGroupId,
            Repeat = repeat
        };
        var msg = body.ToBrokerPublish("BROADCAST_PLAY", "GIS");
        await _natsService.PublishAsync(BuildSubject("play"), JsonConvert.SerializeObject(msg));
    }

    public async Task PublishTtsAsync(int speakerId, string message)
    {
        var body = new TtsBodyDto
        {
            SpeakerIds = [speakerId],
            Message = message
        };
        var msg = body.ToBrokerPublish("TTS", "GIS");
        await _natsService.PublishAsync(BuildSubject("tts"), JsonConvert.SerializeObject(msg));
    }

    public async Task PublishStopAsync(int speakerId)
    {
        var body = new BroadcastStopBodyDto { SpeakerIds = [speakerId] };
        var msg = body.ToBrokerPublish("BROADCAST_STOP", "GIS");
        await _natsService.PublishAsync(BuildSubject("stop"), JsonConvert.SerializeObject(msg));
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// NATS Subject 빌드: "{DomainNats}.{GroupNats}.broadcast_manager.{cmd}"
    /// </summary>
    public string BuildSubject(string cmd)
        => $"{_natsSetup.DomainNats}.{_natsSetup.GroupNats}.broadcast_manager.{cmd}";
    #endregion

    #region - Attributes -
    private readonly INatsService _natsService;
    private readonly INatsSetupModel _natsSetup;
    #endregion
}
