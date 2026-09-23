using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;

/// <summary>
/// 장비의 그룹 소속 — 서버에서 읽은 값을 모델에 싣고, <b>마지막으로 서버와 맞춘 소속</b>(기준선)을 모델 곁에 기억한다.
/// </summary>
/// <remarks>
/// <para><b>왜 필요한가</b> — 서버 7.0+ 는 장비 응답에 소속을 <c>group_ids</c>(int[])로 싣고 <c>device_groups</c> 를 없앴다
/// (api-test-server <c>app/schemas/device.py</c> D12). 예전 매핑은 <c>device_groups</c> 만 읽어 7.0+ 에서 소속이 늘 비었고,
/// 그 빈 목록이 상세 저장의 <c>group_ids</c> 로 되돌아가 서버의 <c>replace_group_mappings</c> 가 <b>다른 소속을 전부 지웠다</b>
/// (라이브 실측 2026-09-24: A·B 소속 센서 → C 로 끌어 넣고 이름만 저장 → 서버 [C] · 재조회를 거치면 []).</para>
/// <para><b>기준선</b> — 소속은 전용 통로(그룹 넣기 · 빼기)가 정본이다. 상세 저장은 사람이 소속을 실제로 바꿨을 때만
/// <c>group_ids</c> 를 보내야 한다. 모델(Monitoring.Models)은 공유 계약이라 칸을 늘리지 않고, 인스턴스 곁에 약한 참조로 붙인다.</para>
/// <para>스레드: <see cref="ConditionalWeakTable{TKey, TValue}"/> 는 스레드 안전하고, 보관하는 목록은 매번 새로 만든 사본이다.</para>
/// </remarks>
public static class GroupMembershipBaseline
{
    private static readonly ConditionalWeakTable<IBaseDeviceModel, HashSet<int>> _baselines = new();

    /// <summary>응답에서 소속을 읽는다 — 7.0+ <c>group_ids</c>, 없으면 6.3 <c>device_groups[].id</c>. 둘 다 없으면 <c>null</c>(받지 못함).</summary>
    public static List<int>? ReadIds(BaseDeviceDto dto)
    {
        if (dto == null) return null;
        if (dto.GroupIds != null) return dto.GroupIds.Distinct().ToList();
        return dto.DeviceGroups?.Select(g => g.Id).Distinct().ToList();
    }

    /// <summary>모델에 실린 소속을 "서버와 맞춘 값"으로 기억한다. 소속을 받지 못했으면(<c>null</c>) 기억하지 않는다.</summary>
    public static void Capture(IBaseDeviceModel model)
    {
        if (model == null) return;
        _baselines.Remove(model);
        if (model.DeviceGroups != null) _baselines.Add(model, new HashSet<int>(model.DeviceGroups));
    }

    /// <summary>재조회한 모델의 기준선을 캐시에 남는 인스턴스로 옮긴다(참조를 유지하는 병합용).</summary>
    public static void CopyFrom(IBaseDeviceModel source, IBaseDeviceModel target)
    {
        if (source == null || target == null) return;
        _baselines.Remove(target);
        if (_baselines.TryGetValue(source, out var set)) _baselines.Add(target, new HashSet<int>(set));
    }

    /// <summary>모델의 소속이 기준선과 같은가 — 기준선이 없으면(새 장비 · 서버에서 읽지 않은 모델) <c>false</c>.</summary>
    public static bool IsUnchanged(IBaseDeviceModel model)
    {
        if (model?.DeviceGroups == null) return false;
        return _baselines.TryGetValue(model, out var set) && set.SetEquals(model.DeviceGroups);
    }

    /// <summary>
    /// 서버가 <b>실제로 해 준</b> 소속 변화(그룹 넣기 · 빼기 성공분)를 모델과 기준선 양쪽에 적는다.
    /// </summary>
    /// <remarks>기준선에도 적어야 다음 상세 저장이 그 변화를 "사람이 고친 것"으로 오인해 <c>group_ids</c> 를 되보내지 않는다.</remarks>
    public static void ApplyConfirmed(IBaseDeviceModel model, int groupId, bool member)
    {
        if (model == null || groupId <= 0) return;
        var wasUnknown = model.DeviceGroups == null;
        model.DeviceGroups ??= new List<int>();
        if (member) { if (!model.DeviceGroups.Contains(groupId)) model.DeviceGroups.Add(groupId); }
        else model.DeviceGroups.Remove(groupId);

        if (_baselines.TryGetValue(model, out var set))
        {
            if (member) set.Add(groupId);
            else set.Remove(groupId);
        }
        else if (wasUnknown)
        {
            // 소속을 받지 못한 모델에 방금 한 칸을 적었다 — 이 목록은 전체가 아니다. "고치지 않음"으로 기억해 두어
            // 다음 상세 저장이 이 부분 목록을 group_ids 로 되보내(나머지 소속을 지우지) 않게 한다.
            _baselines.Add(model, new HashSet<int>(model.DeviceGroups));
        }
    }
}
