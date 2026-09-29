using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Services;
/****************************************************************************
   Purpose      : NATS 이벤트(DETECT · MALFUNCTION) 본문의 장비 → 이벤트 큐가 쓰는 종류 · 그룹
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 이벤트 본문의 <c>device</c> 를 이벤트 큐 키(장비 종류)와 그룹으로 옮긴다 — 판본 관용.
/// </summary>
/// <remarks>
/// <para>v7.0+ 서버(브로커 명세 v2.0.7 §6.1 · §6.2)는 이벤트 속 장비를 <b>참조</b> <c>{id, category_device}</c> 두 키로만 싣는다.
/// 종류(<c>type_device</c>)와 그룹(<c>device_groups</c>)은 본문에 없고 <b>장비 캐시</b>(<c>group_ids</c>)에서 읽는다.
/// 종전 코드는 <c>type_device</c> 만 읽어 7.0+ 의 모든 탐지 · 장애를 "DeviceType 파싱 실패" 로 버렸다(2026-09-30).</para>
/// <para>종류: ① 본문 <c>type_device</c>(옛 서버 전문 — 무회귀) → ② 캐시 장비의 종류 → ③ 카테고리가 1:1 로 정하는 종류.
/// <c>sensor</c> 는 카테고리만으로 종류를 <b>단정하지 않는다</b>(Fence · PIR · Multi … — <see cref="DeviceTypeResolver.FromCategory"/>).</para>
/// <para>그룹: ① 본문 <c>device_groups</c>(옛 서버) → ② 본문 <c>group_ids</c> → ③ 캐시 장비의 그룹.</para>
/// <para>호출 스레드: NATS 콜백. 장비 캐시는 잠금 없이 열거되므로 도중 변경(<see cref="InvalidOperationException"/>)을 한 번 다시 시도한다.</para>
/// </remarks>
internal static class NatsEventDeviceResolver
{
    /// <summary>
    /// 종류 · 그룹을 정한다. 종류를 정하지 못하면 <c>false</c>(추측하지 않는다).
    /// </summary>
    /// <param name="device">본문 장비(참조 또는 옛 전문). <c>null</c> 이면 장비가 지워졌다.</param>
    /// <param name="deviceId">본문에서 읽은 장비 id(<c>device.id</c> → <c>device_id</c>).</param>
    /// <param name="cache">장비 캐시(없으면 <c>null</c>).</param>
    internal static bool TryResolve(
        BaseDeviceDto? device,
        int deviceId,
        IEnumerable<IBaseDeviceModel>? cache,
        out EnumDeviceType deviceType,
        out List<int>? groupIds)
    {
        deviceType = EnumDeviceType.NONE;
        groupIds = null;
        if (device is null || deviceId <= 0) return false;

        var category = DeviceTypeResolver.ResolveCategory(device.CategoryDevice, device.TypeDevice);
        var cached = FindCached(cache, deviceId, category);

        // 종류 — ① 옛 전문의 type_device ② 캐시 ③ 1:1 카테고리
        var resolved = DeviceTypeResolver.Resolve(device.TypeDevice, null)
                       ?? (cached is { DeviceType: not EnumDeviceType.NONE } ? cached.DeviceType : (EnumDeviceType?)null)
                       ?? DeviceTypeResolver.FromCategory(category);
        if (resolved is not EnumDeviceType type) return false;
        deviceType = type;

        // 그룹 — ① 옛 전문의 device_groups ② 본문 group_ids ③ 캐시
        if (device.DeviceGroups is not null)
            groupIds = device.DeviceGroups.Where(g => g.Id > 0).Select(g => g.Id).ToList();
        else if (device.GroupIds is not null)
            groupIds = device.GroupIds.Where(g => g > 0).ToList();
        else if (cached?.DeviceGroups is not null)
            groupIds = cached.DeviceGroups.Where(g => g > 0).ToList();

        return true;
    }

    /// <summary>
    /// 캐시에서 장비를 찾는다 — 7.0+ 는 장비 id 가 카테고리를 가로질러 유일하지만, 옛 서버는 아니었으므로
    /// 카테고리가 맞는 것을 먼저 고른다.
    /// </summary>
    private static IBaseDeviceModel? FindCached(IEnumerable<IBaseDeviceModel>? cache, int deviceId, EnumDeviceCategory category)
    {
        if (cache is null) return null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var candidates = cache.Where(d => d is not null && d.Id == deviceId).ToList();
                if (candidates.Count == 0) return null;
                if (category == EnumDeviceCategory.None) return candidates[0];
                return candidates.FirstOrDefault(d => CategoryOf(d) == category) ?? candidates[0];
            }
            catch (InvalidOperationException) when (attempt == 0)
            {
                // 적재 중인 캐시를 열거했다 — 한 번 더
            }
        }
        return null;
    }

    private static EnumDeviceCategory CategoryOf(IBaseDeviceModel device)
        => device.CategoryDevice != EnumDeviceCategory.None ? device.CategoryDevice : DeviceTypeResolver.CategoryOf(device.DeviceType);
}
