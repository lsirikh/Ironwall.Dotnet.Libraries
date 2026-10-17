namespace Ironwall.Dotnet.Libraries.Devices.Api.Helpers;
/****************************************************************************
   Purpose      : 축 경로(/devices/{device_type}/…)의 복수형 세그먼트 어휘
   Created By   : GHLee
   Created On   : 9/18/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 서버 7.0+ 공통 축 경로(<c>/api/devices/{device_type}/{id}/config</c>·<c>…/component-status</c>·
/// <c>…/spec</c>)에 쓰이는 <b>URL 복수형</b> 7종.
/// </summary>
/// <remarks>
/// <para>⚠ <b>단수형은 404</b> 다(스웨거 원문: "단수형은 404 입니다"). 그런데 응답의
/// <c>category_device</c> 는 <b>단수</b>(<c>enclosure</c>)라 그 값을 경로에 그대로 끼우면 실패한다 —
/// 실수를 코드 한 곳에 모으려고 <see cref="FromCategory"/> 를 둔다.</para>
/// <para>또 경로 유형과 장비 <b>카테고리가 일치</b>해야 한다 — id 는 전역 유일이라 조회 자체는 되지만
/// <c>/gates/7/config</c> 로 카메라를 고칠 수는 없다(404).</para>
/// </remarks>
public static class DeviceTypePaths
{
    public const string Cameras = "cameras";
    public const string Controllers = "controllers";
    public const string Enclosures = "enclosures";
    public const string Gates = "gates";
    public const string Lamps = "lamps";
    public const string Sensors = "sensors";
    public const string Speakers = "speakers";

    /// <summary>허용된 복수형 7종(순서는 스웨거 <c>enum</c> 과 동일).</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Cameras, Controllers, Enclosures, Gates, Lamps, Sensors, Speakers
    };

    /// <summary>주어진 세그먼트가 허용 어휘인가(대소문자 무시). 아니면 서버가 404 다.</summary>
    public static bool IsValid(string? deviceTypePath)
    {
        if (string.IsNullOrWhiteSpace(deviceTypePath)) return false;
        var v = deviceTypePath.Trim();
        foreach (var item in All)
        {
            if (string.Equals(item, v, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// 응답의 <c>category_device</c>(<b>단수</b>: <c>enclosure</c>)를 경로 세그먼트(<b>복수</b>: <c>enclosures</c>)로 바꾼다.
    /// 모르는 값이면 <c>null</c> — <b>추측해서 s 를 붙이지 않는다</b>(404 를 만들 뿐이다).
    /// </summary>
    public static string? FromCategory(string? categoryDevice)
    {
        if (string.IsNullOrWhiteSpace(categoryDevice)) return null;
        var v = categoryDevice.Trim();

        // 이미 복수형으로 준 경우도 그대로 통과시킨다(호출부 혼용 방어).
        foreach (var item in All)
        {
            if (string.Equals(item, v, StringComparison.OrdinalIgnoreCase)) return item;
        }

        return v.ToLowerInvariant() switch
        {
            "camera" => Cameras,
            "controller" => Controllers,
            "enclosure" => Enclosures,
            "gate" => Gates,
            "lamp" => Lamps,
            "sensor" => Sensors,
            "speaker" => Speakers,
            _ => null,
        };
    }
}
