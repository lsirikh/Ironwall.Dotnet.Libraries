using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Messages.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.BulkImport;
/****************************************************************************
   Purpose      : 장비 대량 등록 — 판정된 줄을 서버에 쓴다 (device-bulk-import FR-12~FR-14 · FR-16 · D-1)
   Created By   : GHLee
   Created On   : 10/13/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

public enum BulkWriteStatus { Succeeded, Failed, Cancelled }

/// <summary>부분 실패 4분류(FR-13).</summary>
public enum BulkFailureKind
{
    /// <summary>서버가 값을 받지 않았다(400 · 422) — 칸을 고쳐 다시.</summary>
    Rejected,
    /// <summary>권한이 없다(401 · 403).</summary>
    Permission,
    /// <summary>이미 있거나 없어졌다(404 · 409) · 소속 제어기 등록 실패.</summary>
    Conflict,
    /// <summary>서버 연결 · 서버 오류(0 · 5xx · 예외) — 잠시 후 다시.</summary>
    Connection,
}

public sealed record BulkWriteOutcome(BulkImportRow Row, BulkWriteStatus Status, BulkFailureKind? Kind, string Message, int? DeviceId);

public sealed record BulkWriteProgress(int Done, int Total);

/// <summary>
/// 판정된 줄 → 서버. 줄마다 <b>패널 저장과 같은</b> 변환(<c>ToXxxDeviceDto</c>) · 부대 관문(<see cref="UnitScopeGate"/>) ·
/// 장비 API(<c>Create*/Update*</c> — 7.0+ 수정은 구현이 PATCH 로 보낸다)를 탄다(PRD D-1). 서버에 장비 일괄 엔드포인트는 없다.
/// </summary>
/// <remarks>
/// <para><b>순서(FR-14)</b> — 카테고리 순(제어기 먼저). 같은 파일의 새 제어기 아래 센서는 제어기 등록 뒤 장비를 다시 읽어 새 제어기를 잇는다.</para>
/// <para><b>비밀(FR-16)</b> — 칸 값은 로그에 남기지 않는다(줄 번호 · 카테고리 · 결과만).</para>
/// <para>예외는 던지지 않는다(<see cref="OperationCanceledException"/> 도 줄 결과 "취소"로 바꾼다) — 미리보기 판이 죽으면 결과가 사라진다.</para>
/// </remarks>
public sealed class BulkImportWriter
{
    public BulkImportWriter(IDeviceApiService api, IDeviceProviderService? providerService, Func<IReadOnlyList<IBaseDeviceModel>> devices, ILogService? log = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _providerService = providerService;
        _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        _log = log;
    }

    public async Task<IReadOnlyList<BulkWriteOutcome>> WriteAsync(IReadOnlyList<BulkImportRow> rows, IProgress<BulkWriteProgress>? progress = null, CancellationToken token = default)
    {
        var order = rows.Where(r => r.Verdict is BulkRowVerdict.New or BulkRowVerdict.Update)
                        .OrderBy(r => IndexOf(r.Category)).ThenBy(r => r.Line).ToList();
        var outcomes = new List<BulkWriteOutcome>();
        // 앞선 회차에 등록한 새 제어기도 기억한다 — [실패 줄 다시] 로 센서만 다시 보낼 때 그 제어기에 잇는다.
        var createdControllers = _createdControllers;
        var refreshedAfterControllers = false;
        var done = 0;
        progress?.Report(new BulkWriteProgress(0, order.Count));

        foreach (var row in order)
        {
            if (token.IsCancellationRequested)
            {
                outcomes.Add(new BulkWriteOutcome(row, BulkWriteStatus.Cancelled, null, "취소해서 보내지 않았습니다.", null));
                continue;
            }

            BulkWriteOutcome outcome;
            try
            {
                IControllerDeviceModel? controller = row.Controller;
                if (row.PendingControllerName is { } pendingName)
                {
                    if (!createdControllers.TryGetValue(pendingName, out var newId))
                    {
                        // 이번 묶음에 그 제어기가 없거나(실패 · 취소 · 다시 시도에서 빠짐) 등록하지 못했다.
                        outcome = new BulkWriteOutcome(row, BulkWriteStatus.Failed, BulkFailureKind.Conflict,
                            $"소속 제어기 '{pendingName}' 를 등록하지 못해 보내지 않았습니다.", null);
                        outcomes.Add(outcome);
                        progress?.Report(new BulkWriteProgress(++done, order.Count));
                        continue;
                    }
                    // 새 제어기는 등록 직후 캐시에 없다 — 한 번만 다시 읽어 완전한 모델을 잇는다(못 읽으면 ID 만 쥔 모델).
                    if (!refreshedAfterControllers)
                    {
                        refreshedAfterControllers = true;
                        await RefreshAsync(token).ConfigureAwait(false);
                    }
                    controller = FindCachedController(newId) ?? new ControllerDeviceModel { Id = newId, DeviceName = pendingName };
                }

                outcome = await WriteRowAsync(row, controller, token).ConfigureAwait(false);
                if (row.Category == EnumDeviceCategory.Controller && row.Existing is null)
                {
                    var name = row.CellOf(BulkColumnCatalog.NameKey)?.Raw ?? string.Empty;
                    if (outcome.Status == BulkWriteStatus.Succeeded && outcome.DeviceId is { } id) createdControllers[name] = id;
                }
            }
            catch (OperationCanceledException)
            {
                outcome = new BulkWriteOutcome(row, BulkWriteStatus.Cancelled, null, "취소해서 보내지 않았습니다.", null);
            }
            catch (Exception ex)
            {
                _log?.Error($"[BulkImport] {row.SheetName} {row.Line}줄 저장 중 예외 — {ex.GetType().Name}");
                outcome = new BulkWriteOutcome(row, BulkWriteStatus.Failed, BulkFailureKind.Connection, "저장하지 못했습니다. 서버 연결을 확인하고 다시 시도하세요.", null);
            }

            outcomes.Add(outcome);
            progress?.Report(new BulkWriteProgress(++done, order.Count));
        }

        var succeeded = outcomes.Count(o => o.Status == BulkWriteStatus.Succeeded);
        _log?.Info($"[BulkImport] 대량 등록 끝 — 보냄 {order.Count} · 성공 {succeeded} · 실패 {outcomes.Count(o => o.Status == BulkWriteStatus.Failed)} · 취소 {outcomes.Count(o => o.Status == BulkWriteStatus.Cancelled)}");
        if (succeeded > 0) await RefreshAsync(CancellationToken.None).ConfigureAwait(false);
        return outcomes;
    }

    /// <summary>한 줄 = 한 호출. 신규는 새 모델, 수정은 서버 장비의 사본에 칸을 쓴다(빈 칸 = 바꾸지 않음).</summary>
    private async Task<BulkWriteOutcome> WriteRowAsync(BulkImportRow row, IControllerDeviceModel? controller, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var creating = row.Existing is null;
        var model = creating ? BulkDeviceRows.NewModel(row.Category) : BulkDeviceRows.Clone(row.Existing!);
        var error = Apply(row, model, controller);
        if (error is not null) return new BulkWriteOutcome(row, BulkWriteStatus.Failed, BulkFailureKind.Rejected, error, null);

        var dto = ToDto(model);
        // 패널과 같은 관문 — 8.0 미만이면 unit_id 를 지우고, 8.0 이상이면 실린 값(원래 부대 · 파일 값)을 보존한다.
        await UnitScopeGate.StampAsync(dto, nameof(BulkImportWriter), _log, token).ConfigureAwait(false);
        var (success, id, status, message) = await SendAsync(row.Category, creating, model.Id, dto, token).ConfigureAwait(false);
        if (success)
            return new BulkWriteOutcome(row, BulkWriteStatus.Succeeded, null, creating ? "등록했습니다." : "수정했습니다.", id ?? model.Id);

        _log?.Warning($"[BulkImport] {row.SheetName} {row.Line}줄 {(creating ? "등록" : "수정")} 실패({status})");
        return new BulkWriteOutcome(row, BulkWriteStatus.Failed, Classify(status), message, null);
    }

    /// <summary>보낼 칸을 모델(행 뷰모델 경유)에 쓴다. 못 쓰면 사람이 읽는 사유.</summary>
    internal static string? Apply(BulkImportRow row, IBaseDeviceModel model, IControllerDeviceModel? controller)
    {
        var view = BulkDeviceRows.Wrap(model);
        foreach (var cell in row.Cells)
        {
            switch (cell.Column.Kind)
            {
                case BulkColumnKind.Id:
                    continue;
                case BulkColumnKind.Unit:
                    if (cell.Value is { } unit) model.UnitId = int.Parse(unit, CultureInfo.InvariantCulture);
                    continue;
                case BulkColumnKind.Controller:
                    if (controller is not null && model is SensorDeviceModel sensor) sensor.Controller = controller;
                    continue;
            }

            if (cell.Value is null || cell.Column.Spec is not { } spec) continue;
            if (!DevicePropertyAccessor.TryWrite(view, spec, cell.Value, out var error))
                return $"{cell.Column.Header}: {error}";
        }
        return null;
    }

    internal static BaseDeviceDto ToDto(IBaseDeviceModel model) => model switch
    {
        ControllerDeviceModel m => m.ToControllerDeviceDto(),
        SensorDeviceModel m => m.ToSensorDeviceDto(),
        CameraDeviceModel m => m.ToCameraDeviceDto(),
        SpeakerDeviceModel m => m.ToSpeakerDeviceDto(),
        EnclosureDeviceModel m => m.ToEnclosureDeviceDto(),
        LampDeviceModel m => m.ToLampDeviceDto(),
        GateDeviceModel m => m.ToGateDeviceDto(),
        _ => throw new ArgumentException($"대량 등록이 다루지 않는 장비 형식입니다: {model.GetType().Name}", nameof(model)),
    };

    /// <summary>카테고리 → 생성/수정 호출(패널 · 프리셋 등록과 같은 메서드).</summary>
    private async Task<(bool Success, int? Id, int Status, string Message)> SendAsync(EnumDeviceCategory category, bool creating, int id, BaseDeviceDto dto, CancellationToken token)
    {
        var what = BulkColumnCatalog.CategoryLabel(category);
        switch (category)
        {
            case EnumDeviceCategory.Controller:
                return Read(creating ? await _api.CreateControllerAsync((ControllerDeviceDto)dto, token).ConfigureAwait(false)
                                     : await _api.UpdateControllerAsync(id, (ControllerDeviceDto)dto, token).ConfigureAwait(false), what, creating);
            case EnumDeviceCategory.Sensor:
                return Read(creating ? await _api.CreateSensorAsync((SensorDeviceDto)dto, token).ConfigureAwait(false)
                                     : await _api.UpdateSensorAsync(id, (SensorDeviceDto)dto, token).ConfigureAwait(false), what, creating);
            case EnumDeviceCategory.Camera:
                return Read(creating ? await _api.CreateCameraAsync((CameraDeviceDto)dto, token).ConfigureAwait(false)
                                     : await _api.UpdateCameraAsync(id, (CameraDeviceDto)dto, token).ConfigureAwait(false), what, creating);
            case EnumDeviceCategory.Speaker:
                return Read(creating ? await _api.CreateSpeakerAsync((SpeakerDeviceDto)dto, token).ConfigureAwait(false)
                                     : await _api.UpdateSpeakerAsync(id, (SpeakerDeviceDto)dto, token).ConfigureAwait(false), what, creating);
            case EnumDeviceCategory.Enclosure:
                return Read(creating ? await _api.CreateEnclosureAsync((EnclosureDeviceDto)dto, token).ConfigureAwait(false)
                                     : await _api.UpdateEnclosureAsync(id, (EnclosureDeviceDto)dto, token).ConfigureAwait(false), what, creating);
            case EnumDeviceCategory.Lamp:
                return Read(creating ? await _api.CreateLampAsync((LampDeviceDto)dto, token).ConfigureAwait(false)
                                     : await _api.UpdateLampAsync(id, (LampDeviceDto)dto, token).ConfigureAwait(false), what, creating);
            case EnumDeviceCategory.Gate:
                return Read(creating ? await _api.CreateGateAsync((GateDeviceDto)dto, token).ConfigureAwait(false)
                                     : await _api.UpdateGateAsync(id, (GateDeviceDto)dto, token).ConfigureAwait(false), what, creating);
            default:
                throw new ArgumentOutOfRangeException(nameof(category), category, "대량 등록을 받지 않는 카테고리입니다.");
        }
    }

    /// <summary>응답 → (성공 · 새 Id · 상태 코드 · 사람이 읽는 문장). 422 다필드는 줄바꿈으로 전건(패널과 같은 규칙).</summary>
    private static (bool Success, int? Id, int Status, string Message) Read<T>(ApiResponse<T> response, string what, bool creating) where T : BaseDeviceDto
    {
        var ok = response.Success && (!creating || response.Data != null);
        if (ok) return (true, response.Data?.Id, response.StatusCode, response.Message ?? string.Empty);
        var message = ApiErrorTextHelper.FromFieldErrorsMultiline(response.Error)
                      ?? ApiErrorTextHelper.Resolve(response.Error, response.Message, $"{what} {(creating ? "등록" : "수정")}에 실패했습니다.");
        return (false, null, response.StatusCode, message);
    }

    internal static BulkFailureKind Classify(int status) => status switch
    {
        401 or 403 => BulkFailureKind.Permission,
        404 or 409 or 410 => BulkFailureKind.Conflict,
        >= 400 and < 500 => BulkFailureKind.Rejected,
        _ => BulkFailureKind.Connection,
    };

    public static string KindText(BulkFailureKind? kind) => kind switch
    {
        BulkFailureKind.Rejected => "입력 거부",
        BulkFailureKind.Permission => "권한 없음",
        BulkFailureKind.Conflict => "충돌 · 없음",
        BulkFailureKind.Connection => "연결 · 서버 오류",
        _ => string.Empty,
    };

    private async Task RefreshAsync(CancellationToken token)
    {
        if (_providerService is null) return;
        try { await _providerService.FetchAllDevicesAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log?.Warning($"[BulkImport] 장비 다시 읽기 실패 — {ex.GetType().Name}"); }
    }

    /// <summary>
    /// 장비 캐시에서 새 제어기 찾기 — 이 쓰기기는 작업 스레드에서 돌고 캐시는 UI 스레드가 다시 채울 수 있다.
    /// 읽는 도중 바뀌어 실패하면 ID 만 쥔 모델로 잇는다(본문의 controller_id 는 같다).
    /// </summary>
    private IControllerDeviceModel? FindCachedController(int id)
    {
        try { return _devices().OfType<IControllerDeviceModel>().FirstOrDefault(c => c.Id == id); }
        catch (InvalidOperationException) { return null; }
    }

    private static int IndexOf(EnumDeviceCategory category)
    {
        for (var i = 0; i < BulkColumnCatalog.Categories.Count; i++)
            if (BulkColumnCatalog.Categories[i] == category) return i;
        return int.MaxValue;
    }

    /// <summary>결과 CSV(FR-13) — 줄 · 시트 · 이름 · 판정 · 결과 · 분류 · 사유. 비밀번호는 싣지 않는다.</summary>
    public static void WriteResultCsv(Stream stream, IEnumerable<(BulkImportRow Row, string Result, string Kind, string Reason)> lines)
    {
        var headers = new[] { "줄", "시트", "이름", "판정", "결과", "분류", "사유" };
        var rows = lines.Select(l => (IReadOnlyList<string>)new[]
        {
            l.Row.Line.ToString(CultureInfo.InvariantCulture), l.Row.SheetName, l.Row.Name, VerdictText(l.Row.Verdict), l.Result, l.Kind, l.Reason,
        });
        BulkFileWriter.WriteCsv(stream, headers, null, rows);
    }

    public static string VerdictText(BulkRowVerdict verdict) => verdict switch
    {
        BulkRowVerdict.New => "신규",
        BulkRowVerdict.Same => "같음",
        BulkRowVerdict.Update => "수정",
        _ => "오류",
    };

    /// <summary>이 쓰기기(올린 파일 하나)로 등록한 새 제어기 — 이름 → 새 ID.</summary>
    private readonly Dictionary<string, int> _createdControllers = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDeviceApiService _api;
    private readonly IDeviceProviderService? _providerService;
    private readonly Func<IReadOnlyList<IBaseDeviceModel>> _devices;
    private readonly ILogService? _log;
}
