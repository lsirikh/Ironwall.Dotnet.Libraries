using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Managers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Symbols;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;

/// <summary>한 그룹을 다시 맞춘 결과.</summary>
public enum ZoneLineRegistrationOutcome
{
    /// <summary>소속 장비가 있고 구역선이 있어 (다시) 등록했다.</summary>
    Registered,
    /// <summary>구역선은 있으나 소속 장비가 없어 내렸다.</summary>
    Unregistered,
    /// <summary>그 그룹을 가리키는 구역선이 지도에 없다 — 남아 있던 등록이 있으면 내렸다.</summary>
    NoZoneLine,
}

/// <summary>
/// 구역선(PidsGroup 심볼) 이벤트 조회표를 <b>주어진 그룹만</b> 현재 소속에 맞춘다.
/// </summary>
/// <remarks>
/// <para>전량 재구성(<c>MapViewModel.InitializeDeviceSymbolIntegration</c>)은 조회표를 비우고 모든 장비를 다시 훑는다 —
/// 부팅 · 전량 재조회용이다. 소속 한 건 바뀔 때마다 부를 것이 아니다. 여기서는 등록 코드
/// (<see cref="SymbolEventManager.RegisterGroupSymbol"/>)를 그대로 쓰되 영향 받은 그룹만 만진다.</para>
/// <para>순서: 내릴 것을 먼저 내리고 그다음 등록한다 — 한 선이 옛 그룹에서 새 그룹으로 옮겨 갈 때 옛 그룹 해제가
/// 새 그룹의 색을 지우지 않게. 등록 직후 큐(EQM)의 실제 상태로 한 번 칠한다(늦게 등록된 선이 이미 살아 있는 이벤트를 놓치지 않게).</para>
/// <para>호출 스레드: UI(지도 마커 목록을 읽는 쪽이 마샬링한다). 조회표 자체는 동시성 사전이다.</para>
/// </remarks>
public static class ZoneLineRegistration
{
    public static IReadOnlyDictionary<int, ZoneLineRegistrationOutcome> Sync(
        SymbolEventManager symbolEventManager,
        IEnumerable<IBaseDeviceModel> devices,
        IEnumerable<IPidsGroupSymbolModel> zoneLines,
        IEnumerable<int> groupIds,
        ILogService? log = null)
    {
        ArgumentNullException.ThrowIfNull(symbolEventManager);
        var deviceList = devices?.Where(d => d is not null).ToList() ?? new List<IBaseDeviceModel>();
        var lineList = zoneLines?.Where(l => l is not null).ToList() ?? new List<IPidsGroupSymbolModel>();
        var targets = groupIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();

        var outcomes = new Dictionary<int, ZoneLineRegistrationOutcome>();
        var toRegister = new List<(int GroupId, IPidsGroupSymbolModel Line, IBaseDeviceModel Representative)>();

        foreach (var groupId in targets)
        {
            var line = lineList.FirstOrDefault(l => l.LinkedDeviceGroup == groupId);
            if (line is null)
            {
                symbolEventManager.UnregisterGroupSymbol(groupId);
                outcomes[groupId] = ZoneLineRegistrationOutcome.NoZoneLine;
                log?.Info($"[구역 소속 동기] Group({groupId}) 연결된 구역선 없음 — 등록 해제");
                continue;
            }

            // 그룹 상태는 심볼 모델만 쓴다 — 장비 인자는 등록 계약을 채우는 대표 장비(LinkedDeviceGroup 변경 경로와 같은 규칙).
            var representative = deviceList.FirstOrDefault(d => d.DeviceGroups?.Contains(groupId) == true);
            if (representative is null)
            {
                symbolEventManager.UnregisterGroupSymbol(groupId);
                outcomes[groupId] = ZoneLineRegistrationOutcome.Unregistered;
                log?.Info($"[구역 소속 동기] Group({groupId}) 소속 장비 없음 — '{line.Title}' 등록 해제");
                continue;
            }

            toRegister.Add((groupId, line, representative));
        }

        foreach (var (groupId, line, representative) in toRegister)
        {
            symbolEventManager.RegisterGroupSymbol(groupId, representative, line);
            symbolEventManager.RefreshGroupSymbol(groupId);   // 큐의 실제 상태로 칠한다(EQM 미주입이면 no-op)
            outcomes[groupId] = ZoneLineRegistrationOutcome.Registered;
            log?.Info($"[구역 소속 동기] Group({groupId}) ↔ '{line.Title}' 등록 (대표 장비 {representative.Id})");
        }

        return outcomes;
    }
}
