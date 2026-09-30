namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>
/// 이벤트 창 타일 한 칸이 쓰는 카메라 제어(FR-14 · FR-17). 이벤트 창은 ONVIF 를 직접 모르고 이것만 부른다.
/// <para><b>임시 경계(T-05)</b>: 제공자 추상화(T-02a, <c>CameraPopup.Providers</c>)가 아직 커밋되지 않아
/// 이 인터페이스를 먼저 두고 얇은 어댑터(<see cref="TileCameraControlFactory"/>)로 잇는다.
/// T-02a 가 들어오면 어댑터 하나만 추가하면 된다 — 이벤트 창 코드는 바뀌지 않는다.</para>
/// 모든 호출은 호출자가 넘긴 토큰으로 시간 제한된다(FR-26). 실패는 예외 대신 false 로 알려도 된다.
/// </summary>
internal interface ITileCameraControl : IDisposable
{
    /// <summary>제공자가 PTZ 를 못 하면 이유(예 "RTSP 제공자는 PTZ 없음"), 할 수 있으면 null.</summary>
    string? PtzUnavailableReason { get; }

    /// <summary>연속 이동(-1..1). 정지는 <see cref="StopAsync"/>.</summary>
    Task<bool> ContinuousMoveAsync(double pan, double tilt, double zoom, CancellationToken ct);

    Task StopAsync(CancellationToken ct);

    /// <summary>
    /// 영상 위 드래그 → 상대 이동 한 번. <paramref name="viewX"/>/<paramref name="viewY"/> = 타일 영상 크기에 대한 드래그 비율(오른쪽 +, 아래 +).
    /// 더 새 조작에 밀려 안 나간 것도 true(실패가 아니다).
    /// </summary>
    Task<bool> DragMoveAsync(double viewX, double viewY, double viewAspect, CancellationToken ct);

    Task<IReadOnlyList<TilePreset>> GetPresetsAsync(CancellationToken ct);

    Task<bool> GotoPresetAsync(string presetToken, CancellationToken ct);

    /// <summary>복귀 프리셋으로. 토큰이 null 이면 제공자의 Home.</summary>
    Task<bool> GotoHomeAsync(string? homePresetToken, CancellationToken ct);
}
