namespace Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

/// <summary>
/// IPC 계약 판. 메시지 모양이 호환되지 않게 바뀌면 <see cref="Current"/> 를 올린다.
/// 필드 추가(선택 필드)는 판을 올리지 않는다 — 모르는 필드는 무시되고 모르는 메시지 종류는 버려진다.
/// </summary>
public static class ProtocolVersion
{
    /// <summary>
    /// 현재 계약 판. 2 = 이벤트 창 계약(T-05): OpenEventWindow 가 물리 픽셀 좌표 · 격자 열/행 · 카메라별 PTZ/권한 ·
    /// 머리 정보로 바뀌고, 닫기 사유가 열거형이 됐다(판 1 의 DIU 좌표 · "3x2" 문자열 · 문자열 사유와 호환되지 않음).
    /// 프레이밍(길이 접두 + 봉투)은 판 1 과 같다.
    /// </summary>
    public const int Current = 2;

    /// <summary>가장 오래된 호환 판. GIS 와 호스트는 같은 설치본으로 함께 배포된다 — 판 1 봉투는 받지 않는다.</summary>
    public const int MinimumSupported = 2;

    /// <summary>상대가 보낸 판을 이 쪽이 처리할 수 있는지.</summary>
    public static bool IsCompatible(int peerVersion)
        => peerVersion >= MinimumSupported && peerVersion <= Current;
}
