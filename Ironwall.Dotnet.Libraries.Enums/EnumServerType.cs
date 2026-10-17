namespace Ironwall.Dotnet.Libraries.Enums;

/// <summary>
/// 서버 카테고리 유형 (§8.2)
/// </summary>
public enum EnumServerType
{
    VMS, NVR_API, STREAMING, TRANSCODER, MEDIA,
    RECORDING, PLAYBACK, STORAGE,
    AI_ANALYSIS, AI_TRAINING, AI_INFERENCE, ANALYTICS,
    DB_API, SPEAKER_API, ENCLOSURE_API, PIDS_API,
    WEB, AUTH, PROXY, BROKER, GATEWAY,
    PUSH, LOG, BACKUP, MONITORING, ETC
}
