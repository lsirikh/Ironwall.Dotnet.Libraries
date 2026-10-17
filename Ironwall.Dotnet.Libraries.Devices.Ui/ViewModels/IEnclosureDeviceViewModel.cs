using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;

public interface IEnclosureDeviceViewModel : IDeviceViewModel
{
    string DoorStatus { get; set; }
    bool HeaterEnabled { get; set; }
    bool FanEnabled { get; set; }
    IEnclosureThresholdConfigModel? ThresholdConfig { get; }
    string ThresholdSummary { get; }

    /// <summary>
    /// 히터 토글을 조작할 수 있는가. 6.3(레거시 평면 계약)은 항상 <c>true</c> — 7.0+ 는
    /// 이 함체가 히터 부품을 선언했을 때만(<c>HeaterComponentKey</c> != null) <c>true</c>다.
    /// </summary>
    /// <remarks>
    /// (D-31 후속) 선언 없는 함체에서 토글을 켜도 저장 본문에 표현할 자리가 없다 — 예전엔 그걸
    /// 조용히 무시했다. 이제 토글 자체를 막고 <see cref="HeaterToggleUnavailableReason"/> 로 이유를 보인다.
    /// </remarks>
    bool IsHeaterToggleEnabled { get; }

    /// <summary><see cref="IsHeaterToggleEnabled"/> 가 <c>false</c> 일 때의 한 줄 이유. 가능하면 <c>null</c>.</summary>
    string? HeaterToggleUnavailableReason { get; }

    /// <summary>팬 토글을 조작할 수 있는가 — <see cref="IsHeaterToggleEnabled"/> 와 같은 계약.</summary>
    bool IsFanToggleEnabled { get; }

    /// <summary><see cref="IsFanToggleEnabled"/> 가 <c>false</c> 일 때의 한 줄 이유. 가능하면 <c>null</c>.</summary>
    string? FanToggleUnavailableReason { get; }
}
