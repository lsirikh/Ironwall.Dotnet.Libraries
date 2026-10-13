using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.SensorPresets;

/// <summary>
/// 센서 모델 창(sensor-mount-preset W1) 입구 — 결선 창 밖에서 여는 곳(장비 창 도구줄 [⋯] › 센서 모델 · 설정 창 레일 "센서 모델").
/// 결선 창의 [프리셋 관리] 는 결선 창이 직접 연다(지금 열린 제어기의 저장 전 쓰임을 같이 센다).
/// </summary>
public interface ISensorPresetLauncher
{
    /// <summary>프리셋 저장소가 있어 창을 열 수 있나(GMaps.Db 모듈이 없으면 거짓 — 입구를 감춘다).</summary>
    bool IsSensorModelsAvailable { get; }

    /// <summary>센서 모델 창을 연다(닫힐 때까지 기다린다). 열지 못하면 거짓.</summary>
    Task<bool> OpenSensorModelsAsync();
}
