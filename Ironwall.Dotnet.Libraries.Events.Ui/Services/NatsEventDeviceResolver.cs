using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
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

    /// <summary>캐시 미스 단건 GET 을 기다리는 최대 시간 — 넘기면 조회 없이 종전 규칙(<see cref="TryResolve"/>)으로 넘어간다.</summary>
    internal static readonly TimeSpan LOOKUP_TIMEOUT = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 종류 · 그룹을 정하고, <b>캐시에 없는 참조</b>면 단건 GET 1회로 채운다(브로커 §2.4 N-5 · §6.1 · §6.2 — "참조를 못 풀어도
    /// 이벤트를 버리지 않는다. 단건 GET 으로 채우고, 404 면 스냅샷을 표시한다").
    /// </summary>
    /// <remarks>
    /// <para>조회하는 경우: 본문이 v7 참조(<c>type_device</c> 없음) · 카테고리를 앎 · 캐시에 그 id 가 없음 · <paramref name="lookup"/> 주입됨.
    /// 조회는 <see cref="IDeviceProviderService.FetchDeviceByIdAsync"/> — 찾으면 캐시에도 들어간다(SYNC_DEVICE CREATED 와 같은 길).</para>
    /// <para>결과: 찾음 → <see cref="NatsDeviceResolution.Resolved"/>(조회한 장비의 종류 · 그룹) ·
    /// 서버에 없음(404 · 조회 실패) → <see cref="NatsDeviceResolution.NotFound"/>(큐에 넣지 않는다 — 카드는 호스트가 스냅숏으로) ·
    /// 시간 초과 · 조회 안 함 → 종전 규칙(<see cref="TryResolve"/>: 1:1 카테고리면 그 종류, 아니면 <see cref="NatsDeviceResolution.Unresolved"/>).
    /// 종전엔 조회가 없어 sensor 는 종류 NONE · 그룹 없음으로 큐에 들어갔고(프로브 S11.a), controller 는 카테고리만으로
    /// 없는 제어기를 큐에 넣었다(S11.b) — 카테고리마다 규칙이 달랐다.</para>
    /// <para>호출 스레드: NATS 콜백. 기다리는 동안 스레드를 막지 않는다(await). NATS 처리 줄은 그만큼 늦게 다음 봉투로 간다 —
    /// 순서를 지키려는 것이다(뒤따르는 ACTION_REPORT 가 큐에 들어가기 전의 이벤트를 놓치지 않게).</para>
    /// </remarks>
    internal static async Task<(NatsDeviceResolution Outcome, EnumDeviceType Type, List<int>? Groups)> ResolveAsync(
        BaseDeviceDto? device,
        int deviceId,
        IEnumerable<IBaseDeviceModel>? cache,
        IDeviceProviderService? lookup,
        TimeSpan timeout,
        CancellationToken token = default)
    {
        if (device is null) return (NatsDeviceResolution.Unresolved, EnumDeviceType.NONE, null);   // 옛 평면 device_id 만 — 종전 규칙
        var category = DeviceTypeResolver.ResolveCategory(device.CategoryDevice, device.TypeDevice);
        var legacyBody = DeviceTypeResolver.Resolve(device.TypeDevice, null) is not null;   // 옛 전문은 본문이 이긴다 — 조회하지 않는다

        if (!legacyBody && lookup is not null && deviceId > 0 && category != EnumDeviceCategory.None
            && FindCached(cache, deviceId, category) is null)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeout);
            IBaseDeviceModel? fetched;
            try
            {
                fetched = await lookup.FetchDeviceByIdAsync(device.CategoryDevice ?? category.ToString().ToLowerInvariant(), deviceId, cts.Token)
                                      .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                fetched = null;
            }

            if (fetched is not null)
            {
                var type = fetched.DeviceType != EnumDeviceType.NONE ? fetched.DeviceType : DeviceTypeResolver.FromCategory(category);
                if (type is EnumDeviceType resolvedType)
                {
                    var groups = device.GroupIds?.Where(g => g > 0).ToList()
                                 ?? fetched.DeviceGroups?.Where(g => g > 0).ToList();
                    return (NatsDeviceResolution.Resolved, resolvedType, groups);
                }
            }
            else if (!cts.IsCancellationRequested)
            {
                return (NatsDeviceResolution.NotFound, EnumDeviceType.NONE, null);
            }
            // 시간 초과 · 종류 없는 응답 — 아래 종전 규칙으로
        }

        return TryResolve(device, deviceId, cache, out var fallbackType, out var fallbackGroups)
            ? (NatsDeviceResolution.Resolved, fallbackType, fallbackGroups)
            : (NatsDeviceResolution.Unresolved, EnumDeviceType.NONE, null);
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

/// <summary><see cref="NatsEventDeviceResolver.ResolveAsync"/> 의 결과.</summary>
internal enum NatsDeviceResolution
{
    /// <summary>종류(와 그룹)를 정했다 — 큐에 넣는다.</summary>
    Resolved,
    /// <summary>종류를 정하지 못했다(조회 안 함 · 시간 초과) — 종류 NONE 으로 큐에 넣는다(WP-1 ⑱).</summary>
    Unresolved,
    /// <summary>서버에도 없다(404 · 조회 실패) — 큐에 넣지 않는다. 카드는 호스트가 스냅숏으로 띄운다(N-5).</summary>
    NotFound,
}
