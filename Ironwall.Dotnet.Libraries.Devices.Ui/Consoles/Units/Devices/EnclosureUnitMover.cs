using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Devices;

/// <summary>함체째 부대 옮기기의 계획 — 확인 대화에 보일 내용과 4단계에 쓸 받은 값.</summary>
/// <param name="Enclosure">방금 받은 함체(<c>?include=contents</c>). 좁은 본문의 재료다.</param>
/// <param name="OldUnitId">함체의 지금 부대.</param>
/// <param name="Contents">함체 안에 든 장비(id 오름차순).</param>
/// <param name="TriggerDeviceId">부대를 바꾸려던 장비 — 함체 자신이면 함체 id.</param>
public sealed record EnclosureMovePlan(EnclosureDeviceDto Enclosure, int OldUnitId, IReadOnlyList<EnclosureContentDto> Contents, int TriggerDeviceId)
{
    public int EnclosureId => Enclosure.Id;
    public string EnclosureName => string.IsNullOrWhiteSpace(Enclosure.NameDevice) ? $"#{Enclosure.Id}" : Enclosure.NameDevice;
    public bool TriggeredByContent => TriggerDeviceId != Enclosure.Id;
    public IReadOnlyList<int> ContentIds => Contents.Select(c => c.Id).ToList();

    /// <summary>확인 대화의 글 — 쉬운 말.</summary>
    public string ConfirmText(string unitLabel)
    {
        var head = TriggeredByContent
            ? $"이 장비는 함체 '{EnclosureName}' 안에 들어 있어 혼자서는 다른 부대로 옮길 수 없습니다."
            : $"함체 '{EnclosureName}' 안에 장비 {Contents.Count}대가 들어 있습니다.";
        return $"{head}\n함체와 안에 든 장비 {Contents.Count}대를 함께 {unitLabel}(으)로 옮길까요?";
    }
}

/// <summary>함체째 부대 옮기기의 결과.</summary>
/// <param name="IsSuccess">4단계를 모두 마쳤다.</param>
/// <param name="Message">운영자에게 보일 한 줄(서버 원문 없음).</param>
/// <param name="FailedStep">실패한 단계(1~4). 성공이면 0.</param>
/// <param name="Restored">중간 실패 뒤 원래대로 되돌렸다.</param>
public sealed record EnclosureMoveResult(bool IsSuccess, string Message, int FailedStep, bool Restored);

/// <summary>
/// 함체째 다른 부대로 옮기기(GOP API 8.0.15 대조 G32) — 서버는 한 요청으로 할 수 없다(장비 쪽 · 함체 쪽 둘 다 422).
/// </summary>
/// <remarks>
/// <para><b>4단계</b>(명세 §5.5.6.1 · PM N-1 확정 2026-10-20): ① 함체 <c>device_ids: []</c>(비우기) ② 장비마다 <c>unit_id</c>
/// ③ 함체 <c>unit_id</c> ④ 함체 <c>device_ids</c> 다시 넣기.</para>
/// <para><b>중간 실패</b>(②·③)면 되돌린다 — 옮긴 장비를 원래 부대로 되돌리고, 비웠던 <c>device_ids</c> 를 다시 넣는다.
/// 장비가 새 부대에 남은 채로 목록만 다시 넣으면 "함체와 부대가 다른 장비"로 422 이기 때문에 장비부터 되돌린다.</para>
/// <para>본문은 늘 <see cref="UnitAssignRequestBuilder"/> 의 좁은 본문이다 — 받은 값을 되돌리지 않는다.
/// 8.0.6 미만(함체 안 장비가 없는 서버)이면 <see cref="IsAvailable"/> 이 거짓이고 호출부는 종전 경로를 탄다.</para>
/// </remarks>
public sealed class EnclosureUnitMover
{
    public EnclosureUnitMover(IDeviceApiService api, DeviceQueryPolicy policy, ILogService? log = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _log = log;
    }

    /// <summary>함체 안 장비가 있는 서버(8.0.6 이상)인가.</summary>
    public bool IsAvailable => _policy.HasEnclosureContents;

    /// <summary>
    /// 이 장비의 부대를 바꾸려면 함체째 옮겨야 하는가 — 함체 자신이고 안에 장비가 있거나, 함체 안에 든 장비면 계획을, 아니면 <c>null</c>.
    /// 읽기에 실패해도 <c>null</c>(종전 경로 — 서버가 거절하면 그 거절이 진단이다).
    /// </summary>
    public async Task<EnclosureMovePlan?> PlanAsync(int deviceId, EnumDeviceCategory category, CancellationToken token = default)
    {
        if (!IsAvailable || deviceId <= 0) return null;
        try
        {
            var enclosureId = deviceId;
            if (category != EnumDeviceCategory.Enclosure)
            {
                var holder = await _api.GetEnclosuresAsync(deviceId, token).ConfigureAwait(false);
                if (!holder.Success || holder.Data is not { Count: > 0 } found) return null;
                enclosureId = found[0].Id;
            }

            var read = await _api.GetEnclosureByIdAsync(enclosureId, token, include: "contents").ConfigureAwait(false);
            if (!read.Success || read.Data is not { } enclosure) return null;
            var contents = (enclosure.Contents ?? new List<EnclosureContentDto>()).Where(c => c != null && c.Id > 0).OrderBy(c => c.Id).ToList();
            if (contents.Count == 0) return null;                       // 빈 함체는 혼자 옮겨도 된다
            if (enclosure.UnitId is not { } oldUnit || oldUnit <= 0) return null;

            return new EnclosureMovePlan(enclosure, oldUnit, contents, deviceId);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(EnclosureUnitMover)}] 함체 확인 실패 device={deviceId}: {ex.Message}");
            return null;
        }
    }

    /// <summary>4단계로 옮긴다. 중간(②·③)에 실패하면 되돌린다.</summary>
    public async Task<EnclosureMoveResult> MoveAsync(EnclosureMovePlan plan, int newUnitId, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (newUnitId <= 0) throw new ArgumentOutOfRangeException(nameof(newUnitId), newUnitId, "부대 id 는 1 이상이어야 합니다.");
        var name = plan.EnclosureName;

        // ① 함체 비우기
        if (!await PatchEnclosureAsync(plan, plan.OldUnitId, new List<int>(), token).ConfigureAwait(false))
            return new EnclosureMoveResult(false, $"함체 '{name}'을(를) 옮기지 못했습니다. 바뀐 것은 없습니다. 잠시 후 다시 시도하세요.", 1, Restored: false);

        // ② 장비마다 부대 옮기기
        var moved = new List<(EnumDeviceCategory Category, int Id, int OldUnit)>();
        foreach (var content in plan.Contents)
        {
            var category = UnitDeviceCalls.CategoryOf(content.CategoryDevice);
            var fetched = await UnitDeviceCalls.RefetchAsync(_api, category, content.Id, token).ConfigureAwait(false);
            var ok = fetched != null
                     && (await UnitDeviceCalls.PatchAsync(_api, category, content.Id, UnitAssignRequestBuilder.Build(fetched, newUnitId), token).ConfigureAwait(false)).Success;
            if (!ok)
            {
                _log?.Warning($"[{nameof(EnclosureUnitMover)}] ② 장비 {content.Id} 부대 옮기기 실패 — 되돌립니다");
                return await RollBackAsync(plan, moved, 2, token).ConfigureAwait(false);
            }
            moved.Add((category, content.Id, fetched!.UnitId ?? plan.OldUnitId));
        }

        // ③ 함체 부대 옮기기
        if (!await PatchEnclosureAsync(plan, newUnitId, null, token).ConfigureAwait(false))
        {
            _log?.Warning($"[{nameof(EnclosureUnitMover)}] ③ 함체 {plan.EnclosureId} 부대 옮기기 실패 — 되돌립니다");
            return await RollBackAsync(plan, moved, 3, token).ConfigureAwait(false);
        }

        // ④ 다시 넣기
        if (!await PatchEnclosureAsync(plan, newUnitId, plan.ContentIds.ToList(), token).ConfigureAwait(false))
        {
            _log?.Error($"[{nameof(EnclosureUnitMover)}] ④ 함체 {plan.EnclosureId} 목록 다시 넣기 실패 — 장비 {string.Join(",", plan.ContentIds)}");
            return new EnclosureMoveResult(false,
                $"함체 '{name}'와(과) 장비 {plan.Contents.Count}대는 옮겼지만 함체 안 장비 목록을 다시 넣지 못했습니다. 함체 구성에서 다시 넣어 주세요.", 4, Restored: false);
        }

        return new EnclosureMoveResult(true, $"함체 '{name}'와(과) 안에 든 장비 {plan.Contents.Count}대를 함께 옮겼습니다.", 0, Restored: false);
    }

    /// <summary>중간 실패 — 옮긴 장비를 원래 부대로 되돌린 뒤 비웠던 목록을 다시 넣는다.</summary>
    private async Task<EnclosureMoveResult> RollBackAsync(EnclosureMovePlan plan, IReadOnlyList<(EnumDeviceCategory Category, int Id, int OldUnit)> moved, int failedStep, CancellationToken token)
    {
        var restored = true;
        foreach (var (category, id, oldUnit) in moved.Reverse())
        {
            try
            {
                var fetched = await UnitDeviceCalls.RefetchAsync(_api, category, id, CancellationToken.None).ConfigureAwait(false);
                var ok = fetched != null
                         && (await UnitDeviceCalls.PatchAsync(_api, category, id, UnitAssignRequestBuilder.Build(fetched, oldUnit), CancellationToken.None).ConfigureAwait(false)).Success;
                if (!ok) { restored = false; _log?.Error($"[{nameof(EnclosureUnitMover)}] 되돌리기 실패 — 장비 {id} 를 부대 {oldUnit} 로"); }
            }
            catch (Exception ex) { restored = false; _log?.Error($"[{nameof(EnclosureUnitMover)}] 되돌리기 예외 장비 {id}: {ex.Message}"); }
        }

        // 넣었던 device_ids 복원 — 되돌리기는 취소 토큰을 따르지 않는다(반쯤 옮긴 채로 두지 않는다).
        if (!await PatchEnclosureAsync(plan, plan.OldUnitId, plan.ContentIds.ToList(), CancellationToken.None).ConfigureAwait(false))
        {
            restored = false;
            _log?.Error($"[{nameof(EnclosureUnitMover)}] 되돌리기 실패 — 함체 {plan.EnclosureId} 목록 {string.Join(",", plan.ContentIds)}");
        }

        var name = plan.EnclosureName;
        return new EnclosureMoveResult(false, restored
            ? $"함체 '{name}'을(를) 옮기지 못해 원래대로 되돌렸습니다. 잠시 후 다시 시도하세요."
            : $"함체 '{name}'을(를) 옮기지 못했고 일부는 원래대로 되돌리지 못했습니다. 목록을 [갱신]해 함체와 장비의 부대를 확인하세요.",
            failedStep, restored);
    }

    /// <summary>함체 좁은 PATCH — 부대와(선택) 안에 든 장비 목록. <paramref name="deviceIds"/> 가 <c>null</c> 이면 목록 키를 싣지 않는다.</summary>
    private async Task<bool> PatchEnclosureAsync(EnclosureMovePlan plan, int unitId, List<int>? deviceIds, CancellationToken token)
    {
        try
        {
            var body = (EnclosureDeviceDto)UnitAssignRequestBuilder.Build(plan.Enclosure, unitId);
            body.DeviceIds = deviceIds;
            var response = await _api.PatchEnclosureAsync(plan.EnclosureId, body, token).ConfigureAwait(false);
            if (!response.Success)
                _log?.Warning($"[{nameof(EnclosureUnitMover)}] 함체 {plan.EnclosureId} PATCH 거절 — {response.StatusCode} {response.Error?.Message ?? response.Message}");
            return response.Success;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Error($"[{nameof(EnclosureUnitMover)}] 함체 {plan.EnclosureId} PATCH 실패: {ex.Message}");
            return false;
        }
    }

    private readonly IDeviceApiService _api;
    private readonly DeviceQueryPolicy _policy;
    private readonly ILogService? _log;
}
