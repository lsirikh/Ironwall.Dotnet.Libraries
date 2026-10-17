namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Models;

/// <summary>
/// 오버레이 맵 등록 패널 상태
/// Register → Progress → Complete/Error
/// </summary>
public enum RegistrationPhase
{
    Register,
    Progress,
    Complete,
    Error
}
