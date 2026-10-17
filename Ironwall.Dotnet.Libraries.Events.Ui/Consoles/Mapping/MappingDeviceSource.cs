using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 장비 공급 — 이미 떠 있는 프로바이더 캐시에서 팔레트·이름 조인을 만든다
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// <see cref="IMappingDeviceSource"/> 의 실제 구현 — 로그인 뒤 이미 적재돼 있는 프로바이더를 읽는다.
/// </summary>
/// <remarks>
/// <para>🔴 <b>이 어댑터가 없으면 액션 보드에 장비 이름이 안 나온다.</b> v7.0 에서 연동 응답의
/// 장비 참조가 <c>{id, category_device}</c> 2키로 줄었기 때문이다. 이름은 장비 캐시에서만 나온다.</para>
/// <para>장비를 <b>새로 조회하지 않는다</b> — 팔레트가 어차피 전량을 쓰고, 프로바이더는 이미 전량을 들고 있다.
/// 캐시에 없는 id 는 <c>null</c> 로 돌려주고 화면이 <c>#id</c> 로 적는다(지어내지 않는다).</para>
/// </remarks>
public sealed class MappingDeviceSource : IMappingDeviceSource
{
    private readonly CameraDeviceProvider _cameras;
    private readonly SpeakerDeviceProvider _speakers;
    private readonly LampDeviceProvider _lamps;
    private readonly DeviceGroupProvider _groups;

    /// <summary>생성자.</summary>
    public MappingDeviceSource(
        CameraDeviceProvider cameras,
        SpeakerDeviceProvider speakers,
        LampDeviceProvider lamps,
        DeviceGroupProvider groups)
    {
        _cameras = cameras;
        _speakers = speakers;
        _lamps = lamps;
        _groups = groups;
    }

    /// <inheritdoc/>
    public IReadOnlyList<MappingDeviceInfo> Devices(MappingActionKind kind) => kind switch
    {
        MappingActionKind.Camera => Project(_cameras.OfType<IBaseDeviceModel>()),
        MappingActionKind.Speaker => Project(_speakers.OfType<IBaseDeviceModel>()),
        MappingActionKind.Lamp => Project(_lamps.OfType<IBaseDeviceModel>()),
        _ => Array.Empty<MappingDeviceInfo>(),
    };

    /// <inheritdoc/>
    public IReadOnlyList<MappingGroupInfo> Groups()
        => _groups.OfType<IDeviceGroupModel>()
                  .Select(g => new MappingGroupInfo(g.Id, string.IsNullOrWhiteSpace(g.Name) ? $"#{g.Id}" : g.Name))
                  .OrderBy(g => g.Name, StringComparer.CurrentCulture)
                  .ToList();

    /// <inheritdoc/>
    public MappingDeviceInfo? Find(MappingActionKind kind, int deviceId)
        => Devices(kind).FirstOrDefault(d => d.Id == deviceId);

    private static IReadOnlyList<MappingDeviceInfo> Project(IEnumerable<IBaseDeviceModel> devices)
        => devices
            .Where(d => d is not null)
            .Select(d => new MappingDeviceInfo(
                d.Id,
                string.IsNullOrWhiteSpace(d.DeviceName) ? $"#{d.Id}" : d.DeviceName!,
                TypeTextOf(d),
                d.DeviceGroups?.ToList() ?? new List<int>(),
                d.IsEnable))
            .OrderBy(d => d.Name, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>
    /// 팔레트 부제 — 서버 종류축 원값을 우선 쓴다. 클라 enum 은 서버 어휘를 못 따라간다
    /// (<c>SPEED_DOME</c>·<c>SmartController</c> 같은 값이 enum 에 없다).
    /// </summary>
    private static string TypeTextOf(IBaseDeviceModel device)
    {
        if (!string.IsNullOrWhiteSpace(device.TypeAxisCode)) return device.TypeAxisCode!;
        return device.DeviceType.ToString();
    }
}
