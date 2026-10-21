using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.Messages.Dto.Brokers;

/// <summary>
/// 방송 재생 Body
/// </summary>
public class BroadcastPlayBodyDto : ControlCommandBodyDto
{
    [JsonProperty("speaker_ids")]
    public List<int> SpeakerIds { get; set; } = new();

    [JsonProperty("file_group_id")]
    public int FileGroupId { get; set; }

    [JsonProperty("repeat")]
    public int Repeat { get; set; }

    /// <summary>
    /// 음량(0~100) — <b>늘 싣는다</b>(브로커 명세 §11.5.3 G-21·G-22 · G-36b 음량 명시 의무). <c>0</c> 도 유효한 값이고 "보내지 않음" 과 다르다.
    /// <para>기본값 100 은 칸을 빼먹었을 때 매니저가 채우던 값(명세: 생략 시 최대 음량)과 같다 — 종전 동작을 바꾸지 않는다.
    /// 범위 밖 값은 0~100 으로 잘라낸다.</para>
    /// </summary>
    [JsonProperty("volume")]
    public int Volume
    {
        get => _volume;
        set => _volume = ClampVolume(value);
    }

    /// <summary>음량을 0~100 으로 자른다 — TTS 본문(<see cref="TtsBodyDto"/>)도 같은 규칙을 쓴다.</summary>
    public static int ClampVolume(int value) => Math.Clamp(value, VolumeMin, VolumeMax);

    public const int VolumeMin = 0;
    public const int VolumeMax = 100;
    public const int VolumeDefault = 100;
    private int _volume = VolumeDefault;
}
