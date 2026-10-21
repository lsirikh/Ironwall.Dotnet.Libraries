using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>
/// 카메라가 PTZ 구동(프리셋 이동 · 팬틸트)을 하는지 판정하는 공용 기준.
/// <para>7.0+ 서버는 형태 축 <c>type.code</c> 로 <c>SPEED_DOME</c> 을 보내지만 <see cref="EnumCameraType"/> 에는 그 값이 없어
/// <see cref="ICameraDeviceModel.Category"/> 가 <c>NONE</c> 으로 남는다(GOP API 8.0.15 대조 G14).
/// 그래서 enum 을 넓히지 않고 <c>Category==PTZ</c> 또는 형태 코드 PTZ/SPEED_DOME(대소문자 무시)을 PTZ 로 본다.</para>
/// </summary>
public static class CameraDriveKind
{
    public static bool IsPtzDrive(ICameraDeviceModel? camera)
    {
        if (camera is null) return false;
        if (camera.Category == EnumCameraType.PTZ) return true;
        var code = camera.TypeAxisCode?.Trim();
        return string.Equals(code, "PTZ", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "SPEED_DOME", StringComparison.OrdinalIgnoreCase);
    }
}
