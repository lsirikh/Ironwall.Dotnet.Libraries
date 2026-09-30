namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Ptz;

/// <summary>
/// 누르고 있는 동안만 움직이는 PTZ 버튼(방향 · 줌 · 포커스)의 상태(순수 · WPF 무의존) — 지도 오버레이와 이벤트 창 타일 공용.
/// <list type="bullet">
/// <item><see cref="Press"/> — 이동을 <b>한 번</b> 보내야 하면 true. 키 자동 반복 · 같은 버튼 재누름은 false.</item>
/// <item><see cref="Finish"/> — 뗌 · 캡처 잃음 · 창 비활성 · 닫힘 · 호스트 재시작이 모두 부르는 단일 지점.
/// 눌려 있던 버튼 표식을 <b>한 번만</b> 돌려준다(그때 정지를 보낸다). 눌린 게 없으면 null — 정지를 또 보내지 않는다.</item>
/// </list>
/// UI 스레드 전용.
/// </summary>
public sealed class PtzHoldPress
{
    /// <summary>지금 눌려 있는 버튼 표식(없으면 null).</summary>
    public string? Active { get; private set; }

    public bool IsActive => Active is not null;

    /// <summary>
    /// 누름. <paramref name="isRepeat"/>(키 자동 반복)이거나 같은 버튼이 이미 눌려 있으면 false — 이동을 다시 보내지 않는다.
    /// 다른 버튼이 눌려 있으면 새 버튼으로 바꾸고 true(새 연속 이동이 이전 것을 대체한다 — ONVIF §5.3.2).
    /// </summary>
    public bool Press(string tag, bool isRepeat = false)
    {
        if (string.IsNullOrEmpty(tag) || isRepeat) return false;
        if (string.Equals(Active, tag, StringComparison.Ordinal)) return false;
        Active = tag;
        return true;
    }

    /// <summary>끝 — 눌려 있던 버튼 표식을 돌려주고 비운다. 두 번째 호출부터는 null.</summary>
    public string? Finish()
    {
        var tag = Active;
        Active = null;
        return tag;
    }
}
