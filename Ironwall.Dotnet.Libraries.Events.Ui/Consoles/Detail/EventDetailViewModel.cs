using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Converters;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Detail;

/// <summary>상세 칸의 한 줄. <see cref="IsEditable"/> 인 칸만 상자로 그린다 — 나머지는 맨 글자(발생 기록).</summary>
public sealed class EventDetailField : PropertyChangedBase
{
    private string _text = string.Empty;

    public EventDetailField(string key, string label, string text, bool isEditable = false,
                            string? lockReason = null, IReadOnlyList<string>? options = null)
    {
        Key = key;
        Label = label;
        _text = text ?? string.Empty;
        Original = _text;
        IsEditable = isEditable;
        LockReason = lockReason;
        Options = options;
    }

    public string Key { get; }
    public string Label { get; }
    public bool IsEditable { get; }

    /// <summary>잠긴 칸의 까닭(툴팁). 잠기지 않았으면 null.</summary>
    public string? LockReason { get; }

    /// <summary>고를 수 있는 값들(콤보). null 이면 글자 입력.</summary>
    public IReadOnlyList<string>? Options { get; }

    public string Original { get; private set; }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == (value ?? string.Empty)) return;
            _text = value ?? string.Empty;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(IsChanged));
            Touched?.Invoke(this);
        }
    }

    public bool IsChanged => IsEditable && !string.Equals(_text, Original, StringComparison.Ordinal);

    /// <summary>골라 쓰는 칸인가(콤보) — XAML 에서 null 검사를 하지 않게 불리언으로 낸다.</summary>
    public bool IsChoice => IsEditable && Options is { Count: > 0 };

    /// <summary>글자로 고치는 칸인가.</summary>
    public bool IsFreeText => IsEditable && Options is not { Count: > 0 };

    /// <summary>상자 없이 맨 글자로 보이는 칸인가(발생 기록).</summary>
    public bool IsPlain => !IsEditable;

    internal Action<EventDetailField>? Touched { get; set; }

    private string? _beforeSettle;

    internal void Settle()
    {
        _beforeSettle = Original;
        Original = _text;
        NotifyOfPropertyChange(nameof(IsChanged));
    }

    /// <summary>
    /// <see cref="Settle"/> 를 되돌린다 — 저장이 실패했을 때 "이게 원래 값" 이 앞서 나가면
    /// [되돌리기] 가 아무것도 못 되돌린다(R8).
    /// </summary>
    internal void Unsettle()
    {
        if (_beforeSettle is null) return;
        Original = _beforeSettle;
        _beforeSettle = null;
        NotifyOfPropertyChange(nameof(IsChanged));
    }

    internal void RevertToOriginal()
    {
        if (_text == Original) return;
        _text = Original;
        NotifyOfPropertyChange(nameof(Text));
        NotifyOfPropertyChange(nameof(IsChanged));
    }

    /// <summary>
    /// 기록 칸(잠긴 칸)의 값을 새 사실로 바꾼다 — 조치보고 뒤 '상태' 처럼 서버가 바꾼 값을 다시 보일 때.
    /// 원래 값도 함께 옮겨 '손댄 칸' 으로 세지 않는다.
    /// </summary>
    internal void ReplaceRecorded(string text)
    {
        _text = text ?? string.Empty;
        Original = _text;
        NotifyOfPropertyChange(nameof(Text));
        NotifyOfPropertyChange(nameof(IsChanged));
    }
}

/// <summary>상세 칸 동작 줄의 단추(정본 SB L2703-2731).</summary>
public enum EventDetailAction
{
    /// <summary>[조치보고] / [조치보고 추가] — 한 건: 조치보고 창.</summary>
    Report,
    /// <summary>[N건 트레이에 담기] — 여러 건: 조치 트레이.</summary>
    QueueToTray,
    /// <summary>[탐지 신호 이력] — 그 센서의 이력 창.</summary>
    DetectionHistory,
    /// <summary>[조치 내역 n건] — 상세 안의 조치 내역 절로 내려간다.</summary>
    JumpToActions,
    /// <summary>[원본 열기] — 조치 행의 원본 이벤트를 그 목록에서 연다.</summary>
    OpenOrigin,
}

/// <summary>상세 칸의 한 절 — 머리 + 잠금 꼬리표 + 줄들.</summary>
public sealed class EventDetailSection
{
    public EventDetailSection(string title, string? lockNote, IReadOnlyList<EventDetailField> fields, string? note = null)
    {
        Title = title;
        LockNote = lockNote;
        Fields = fields;
        Note = note;
    }

    public string Title { get; }
    /// <summary>"측정값 · 수정 불가" 같은 꼬리표.</summary>
    public string? LockNote { get; }
    public string? Note { get; }
    public IReadOnlyList<EventDetailField> Fields { get; }
    public bool HasNote => !string.IsNullOrEmpty(Note);
    public bool HasLockNote => !string.IsNullOrEmpty(LockNote);
}

/// <summary>
/// 이벤트 상세 칸 — <b>읽기가 기본</b>이고 고칠 수 있는 것은 판정 하나뿐이다.
/// </summary>
/// <remarks>
/// <para>정본 window-layout-system-storyboard.html L1084-1095(편집 경계 표) · L2703-2757(상세 렌더).</para>
/// <para>[적용]은 새 전송 경로를 만들지 않는다 — 행 뷰모델의 세터(<c>SetModelProperty</c>)가 <c>IsEdited</c> 를 켜고,
/// 패널의 기존 저장 경로가 그것을 보낸다.</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public sealed class EventDetailViewModel : PropertyChangedBase
{
    private readonly ConsoleDetailPresenter _presenter;
    private EventDetailKind _kind = EventDetailKind.Detection;
    private IReadOnlyList<object> _rows = Array.Empty<object>();
    private bool _canEdit = true;
    private bool _canReport = true;
    private int _actionCount;

    public EventDetailViewModel(ConsoleDetailPresenter presenter, EventActionHistoryViewModel? actions = null)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        Sections = new ObservableCollection<EventDetailSection>();
        SelectedSummary = new ObservableCollection<string>();
        Actions = actions ?? new EventActionHistoryViewModel(() => null);
    }

    /// <summary>조치 내역 — 열 때 한 번 부른다(정본 E-D4).</summary>
    public EventActionHistoryViewModel Actions { get; }

    /// <summary>탐지 스냅샷 — 없으면 기본 그림이 드러난다.</summary>
    public ImageSource? Snapshot { get; private set; }

    public bool HasSnapshot => Snapshot is not null;

    /// <summary>스냅샷 칸을 보일까 — 탐지와 조치(원본이 탐지일 때)에서만.</summary>
    public bool ShowSnapshotBox { get; private set; }

    public ObservableCollection<EventDetailSection> Sections { get; }

    /// <summary>여러 건을 골랐을 때 보이는 목록 — 판정 칸 대신 이것을 보인다(이벤트는 기록이다).</summary>
    public ObservableCollection<string> SelectedSummary { get; }

    public EventDetailKind Kind => _kind;
    public bool IsMultiple => _rows.Count > 1;
    public bool IsEmpty => _rows.Count == 0;
    public bool HasSections => Sections.Count > 0;

    /// <summary>선택 없음 상태의 안내 한 줄.</summary>
    public string EmptyHint => EventDetailProjection.EmptyHint(_kind);

    /// <summary>주 버튼 문구 — 중복 조치보고가 허용이라 조치가 있어도 꺼지지 않는다.</summary>
    public string ReportButtonText => EventDetailProjection.ReportButtonText(_rows.Count, _actionCount);

    /// <summary>탐지 · 장애에서만 조치보고가 뜬다(연결에는 <c>action_reported</c> 가 없다).</summary>
    public bool CanShowReport => _kind is EventDetailKind.Detection or EventDetailKind.Malfunction
                                 && _rows.Count > 0 && _canReport;

    /// <summary>
    /// 상세 칸 위의 안내 한 줄. 커널의 기본 문구(“값이 다른 칸은 — 여러 값 —”)는
    /// 이벤트에는 <b>틀린 말</b>이다 — 여기서는 일괄 편집을 하지 않고 조치를 추가한다.
    /// </summary>
    public string BannerText => !_canEdit && _rows.Count > 0
        ? ConsoleDetailPresenter.ReadOnlyBanner
        : IsMultiple
            ? (_kind is EventDetailKind.Detection or EventDetailKind.Malfunction && _canReport
                ? $"{_rows.Count}건을 골랐습니다. 조치 트레이에 담아 한꺼번에 조치보고할 수 있습니다."
                : $"{_rows.Count}건을 골랐습니다. 여러 건은 한꺼번에 고치지 않습니다.")
            : string.Empty;

    /// <summary>범례 — 고칠 수 있는 칸과 발생 기록을 형태로 가른다.</summary>
    public bool ShowLegend => Sections.Any(s => s.Fields.Any(f => f.IsEditable));

    #region - 동작 줄 (정본 SB L2703-2731) -
    // 동작이 있는 단추만 보인다 — 구현이 없는 [지도에서 보기] 는 두지 않는다(지도 쪽에 장비로 옮기는 메시지가 없다).

    /// <summary>단추를 눌렀다 — 대시보드가 실제 일을 한다(이 칸은 전송 경로를 갖지 않는다).</summary>
    public event Action<EventDetailAction>? ActionRequested;

    /// <summary>뷰가 부른다.</summary>
    public void Request(EventDetailAction action) => ActionRequested?.Invoke(action);

    private ExEventViewModel? SingleReportable
        => _rows.Count == 1 && _rows[0] is ExEventViewModel ex && ex.Model is { Id: > 0 }
           && _kind is EventDetailKind.Detection or EventDetailKind.Malfunction ? ex : null;

    /// <summary>[조치보고] — 한 건 · 저장된 탐지/장애 · 권한이 있을 때.</summary>
    public bool ShowReportAction => _canReport && SingleReportable is not null;

    /// <summary>[N건 트레이에 담기] — 여러 건을 골랐을 때.</summary>
    public bool ShowQueueAction => CanShowReport && _rows.Count > 1;

    /// <summary>[탐지 신호 이력] — 한 건의 탐지이고 그 장비가 센서일 때(이력 창은 센서 기준으로 조회한다).</summary>
    public bool ShowHistoryAction
        => _rows.Count == 1 && _rows[0] is DetectionEventViewModel { Device: Ironwall.Dotnet.Monitoring.Models.Devices.ISensorDeviceModel { Id: > 0 } };

    /// <summary>[원본 열기] — 한 건의 조치이고 원본을 안다.</summary>
    public bool ShowOpenOriginAction => _rows.Count == 1 && _rows[0] is ActionEventViewModel { OriginEvent: not null };

    /// <summary>동작 줄을 그릴 것이 하나라도 있는가.</summary>
    public bool HasActionRow => ShowReportAction || ShowHistoryAction || ShowOpenOriginAction;

    /// <summary>
    /// 조치보고가 방금 만들어졌다 — '상태' 와 조치 내역을 다시 보인다(목록 행은 대시보드가 다시 그린다).
    /// 손댄 판정 칸은 건드리지 않는다: 통째로 다시 불러오면 적용 전 편집이 소리 없이 사라진다.
    /// </summary>
    public void RefreshAfterReport(int actionCount)
    {
        _actionCount = Math.Max(0, actionCount);
        foreach (var field in Sections.SelectMany(s => s.Fields).Where(f => f.Key == "status" && !f.IsEditable))
            field.ReplaceRecorded(EventDetailProjection.StatusText(_actionCount));

        NotifyOfPropertyChange(nameof(ReportButtonText));
        NotifyOfPropertyChange(nameof(ShowReportAction));

        // 조치 내역은 서버에 다시 묻는다 — 방금 만든 줄이 서버에 정말 있는지는 서버만 안다.
        if (SingleReportable is { } ex)
            _ = Actions.LoadAsync(_kind, ex.Model!.Id, ex.IsActionReported || _actionCount > 0);
    }
    #endregion

    public void Load(EventDetailKind kind, IReadOnlyList<object> rows, bool canEdit, bool canReport, int actionCount)
    {
        _kind = kind;
        _rows = rows ?? Array.Empty<object>();
        _canEdit = canEdit;
        _canReport = canReport;
        _actionCount = actionCount;

        Sections.Clear();
        SelectedSummary.Clear();
        _presenter.Tracker.Clear();
        Snapshot = null;
        ShowSnapshotBox = false;

        _presenter.TypeName = EventDetailProjection.KindLabel(kind);
        _presenter.IsReadOnly = !canEdit;
        _presenter.SelectedCount = _rows.Count;

        if (_rows.Count == 0)
        {
            _presenter.SingleTitle = string.Empty;
            _presenter.SingleNumber = string.Empty;
        }
        else if (_rows.Count > 1)
        {
            foreach (var row in _rows) SelectedSummary.Add(SummaryOf(row));
        }
        else
        {
            BuildSingle(_rows[0]);
        }

        RaiseAll();

        // 조치 내역은 한 건을 골랐을 때만, 그리고 조치 여부가 켜져 있을 때만 부른다(정본 E-D4).
        if (_rows.Count == 1 && _rows[0] is ExEventViewModel ex && kind is EventDetailKind.Detection or EventDetailKind.Malfunction)
            _ = Actions.LoadAsync(kind, ex.Model?.Id ?? 0, ex.IsActionReported);
        else
            Actions.Clear();
    }

    /// <summary>[적용] 이 손대진 칸을 행에 쓴 결과.</summary>
    /// <param name="Written">실제로 쓴 칸 수(0 이면 저장을 부르지 않는다).</param>
    /// <param name="Rejected">값을 읽지 못해 쓰지 못한 칸 이름들 — 조용히 삼키지 않는다(R8).</param>
    public readonly record struct WriteBackResult(int Written, IReadOnlyList<string> Rejected);

    /// <summary>[적용] — 손대진 칸을 행 뷰모델에 쓴다.</summary>
    public WriteBackResult WriteBack()
    {
        _lastWritten.Clear();
        if (_rows.Count != 1 || !_canEdit) return new WriteBackResult(0, Array.Empty<string>());

        var written = 0;
        var rejected = new List<string>();
        foreach (var field in Sections.SelectMany(s => s.Fields).Where(f => f.IsChanged))
        {
            if (!Write(_rows[0], field))
            {
                // 값을 못 읽었다(목록에 없는 글자 등) — 그대로 알린다.
                rejected.Add(field.Label);
                continue;
            }
            _lastWritten.Add(field);
            field.Settle();
            written++;
        }
        return new WriteBackResult(written, rejected);
    }

    /// <summary>
    /// 방금 쓴 칸을 다시 <b>손대진 것으로</b> 되돌린다 — 저장이 실패했을 때
    /// Original 이 앞서 나가 버려 [되돌리기] 가 무력해지는 것을 막는다(R8).
    /// </summary>
    public void RollbackWriteBack()
    {
        foreach (var field in _lastWritten) field.Unsettle();
        _lastWritten.Clear();
    }

    private readonly List<EventDetailField> _lastWritten = new();

    /// <summary>[되돌리기] — 화면만 되돌린다(서버 호출 0).</summary>
    public void RevertEdits()
    {
        foreach (var field in Sections.SelectMany(s => s.Fields)) field.RevertToOriginal();
        _presenter.Tracker.Clear();
    }

    private bool Write(object row, EventDetailField field)
    {
        switch (row)
        {
            case DetectionEventViewModel detection when field.Key == EventDetailProjection.FieldResult:
                var result = ParseKorean<EnumDetectionType>(field.Text);
                if (result is null) return false;
                detection.Result = result.Value;
                return true;

            case MalfunctionEventViewModel malfunction when field.Key == EventDetailProjection.FieldReason:
                var reason = ParseKorean<EnumFaultType>(field.Text);
                if (reason is null) return false;
                malfunction.Reason = reason.Value;
                return true;

            case ActionEventViewModel action when field.Key == EventDetailProjection.FieldContent:
                action.Content = field.Text;
                return true;

            default:
                return false;
        }
    }

    private void BuildSingle(object row)
    {
        switch (row)
        {
            case DetectionEventViewModel detection: BuildDetection(detection); break;
            case MalfunctionEventViewModel malfunction: BuildMalfunction(malfunction); break;
            case ConnectionEventViewModel connection: BuildConnection(connection); break;
            case ActionEventViewModel action: BuildAction(action); break;
        }
    }

    private void BuildDetection(DetectionEventViewModel row)
    {
        _presenter.SingleTitle = row.DeviceLabel ?? "(장비 없음)";
        // 머리 윗줄은 "탐지 · {구역}"(정본 SB L2705) — 번호는 아래 '번호' 칸에 이미 있다. 구역을 모르면 번호로 둔다.
        var zone = ZoneTextOf(row.Device);
        _presenter.SingleNumber = zone != "—" ? zone : row.Model?.Id.ToString() ?? string.Empty;

        // 빈 상태 안내가 약속한 "스냅샷" — 없으면 없다고 보이고, 있으면 그림을 낸다(R5).
        ShowSnapshotBox = true;
        Snapshot = row.Thumbnail;

        Add(new EventDetailSection("탐지 속성", null, new[]
        {
            Locked("datetime", "발생시각", row.DateTime.ToString("yyyy-MM-dd HH:mm:ss")),
            Locked("device", "장비", row.DeviceLabel ?? "—"),
            Locked("zone", "구역", ZoneTextOf(row.Device)),
            Locked("kind", "종류", row.DeviceTypeName ?? "—"),
            Locked("number", "번호", row.Model?.Id.ToString() ?? "—"),
            Locked("status", "상태", EventDetailProjection.StatusText(_actionCount)),
            Editable(EventDetailProjection.FieldResult, "결과", EnumKoreanMap.To(row.Result), KoreanOptions<EnumDetectionType>()),
        }));

        Add(new EventDetailSection("측정값", "센서 · AI 가 잰 값 — 수정 불가", new[]
        {
            Locked("signal", "신호", row.SignalText),
            Locked("ai_model", "AI 모델", string.IsNullOrWhiteSpace(row.AiModel) ? "—" : row.AiModel!),
            Locked("inference", "추론", row.InferenceMs is > 0 ? $"{row.InferenceMs} ms" : "—"),
            Locked("frame", "프레임", row.FrameWidth is > 0 ? $"{row.FrameWidth} × {row.FrameHeight}" : "—"),
        }, "잰 값이 틀렸다면 조치보고 메모로 남기세요."));
    }

    private void BuildMalfunction(MalfunctionEventViewModel row)
    {
        _presenter.SingleTitle = row.DeviceLabel ?? "(장비 없음)";
        _presenter.SingleNumber = row.Model?.Id.ToString() ?? string.Empty;

        Add(new EventDetailSection("장애 속성", null, new[]
        {
            Locked("datetime", "발생시각", row.DateTime.ToString("yyyy-MM-dd HH:mm:ss")),
            Locked("device", "장비", row.DeviceLabel ?? "—"),
            Locked("kind", "종류", row.DeviceTypeName ?? "—"),
            Locked("number", "번호", row.Model?.Id.ToString() ?? "—"),
            Locked("status", "상태", EventDetailProjection.StatusText(_actionCount)),
            Editable(EventDetailProjection.FieldReason, "사유", EnumKoreanMap.To(row.Reason), KoreanOptions<EnumFaultType>()),
        }));

        Add(new EventDetailSection("고장 구간", "루프 위 위치 · 수정 불가", new[]
        {
            Locked("fault_section", "1차 선", row.FirstStart == 0 && row.FirstEnd == 0 ? "— 해당 없음" : $"{row.FirstStart} → {row.FirstEnd}"),
            Locked("fault_section", "2차 선", row.SecondStart == 0 && row.SecondEnd == 0 ? "— 해당 없음" : $"{row.SecondStart} → {row.SecondEnd}"),
        }, "제어기 선은 1차로 나가 센서를 거쳐 2차로 들어오는 루프입니다. 숫자는 그 선 위의 지점이며 시각이 아닙니다."));
    }

    private void BuildConnection(ConnectionEventViewModel row)
    {
        _presenter.SingleTitle = row.DeviceLabel ?? "(장비 없음)";
        _presenter.SingleNumber = row.Model?.Id.ToString() ?? string.Empty;

        Add(new EventDetailSection("연결 속성", "전부 발생 기록", new[]
        {
            Locked("datetime", "발생시각", row.DateTime.ToString("yyyy-MM-dd HH:mm:ss")),
            Locked("device", "장비", row.Device?.DeviceName ?? "—"),
            Locked("kind", "종류", row.DeviceTypeName ?? "—"),
            Locked("number", "번호", row.Model?.Id.ToString() ?? "—"),
            Locked("status", "상태", EnumKoreanMap.To(row.MessageType)),
        }, "연결 이벤트에는 조치보고가 없습니다."));
    }

    private void BuildAction(ActionEventViewModel row)
    {
        _presenter.SingleTitle = string.IsNullOrWhiteSpace(row.Content) ? "(내용 없음)" : row.Content!;
        _presenter.SingleNumber = row.Model?.Id.ToString() ?? string.Empty;

        ShowSnapshotBox = row.IsDetectionOrigin;
        Snapshot = row.OriginThumbnail;

        var origin = row.OriginEvent;
        var originKind = origin is Ironwall.Dotnet.Monitoring.Models.Events.IDetectionEventModel
            ? EventDetailKind.Detection : EventDetailKind.Malfunction;

        Add(new EventDetailSection("원본", "발생 기록", new[]
        {
            // 원본 매칭은 Id 와 타입이 둘 다 맞아야 한다 — Id 만으로 맞추면 탐지 3번과 장애 3번이 섞인다.
            Locked("origin", "원본", origin is null ? "불러온 범위 밖"
                : $"{EventDetailProjection.KindLabel(originKind)} · {origin.Id}"),
            Locked("origin_time", "발생", origin?.DateTime.ToString("yyyy-MM-dd HH:mm:ss") ?? "—"),
            Locked("device", "원본 장비", origin is null ? "—" : Helpers.EventDeviceSnapshot.ShortLabel(origin.Device, origin) ?? "—"),
        }));

        Add(new EventDetailSection("조치 속성", null, new[]
        {
            Locked("user", "사용자", string.IsNullOrWhiteSpace(row.User) ? "—" : row.User!),
            Editable(EventDetailProjection.FieldContent, "내용", row.Content ?? string.Empty, null),
            Locked("datetime", "시각", row.DateTime.ToString("yyyy-MM-dd HH:mm:ss")),
        }, "판단이 바뀌었으면 내용을 고치지 말고 원본에 조치보고를 한 건 더 추가하세요."));
    }

    private EventDetailField Locked(string key, string label, string text)
        => new(key, label, text, false, EventDetailProjection.LockReason(_kind, key, _canEdit));

    private EventDetailField Editable(string key, string label, string text, IReadOnlyList<string>? options)
    {
        var can = EventDetailProjection.CanEdit(_kind, key, _canEdit);
        var field = new EventDetailField(key, label, text, can,
            can ? null : EventDetailProjection.LockReason(_kind, key, _canEdit), options);
        field.Touched = OnFieldTouched;
        return field;
    }

    private void OnFieldTouched(EventDetailField field)
        => _presenter.Tracker.Touch(field.Key, field.Original, field.Text);

    private void Add(EventDetailSection section) => Sections.Add(section);

    private static string SummaryOf(object row) => row switch
    {
        DetectionEventViewModel d => $"{d.DateTime:MM-dd HH:mm} · {d.DeviceLabel ?? "—"} · {EnumKoreanMap.To(d.Result)}",
        MalfunctionEventViewModel m => $"{m.DateTime:MM-dd HH:mm} · {m.DeviceLabel ?? "—"} · {EnumKoreanMap.To(m.Reason)}",
        ConnectionEventViewModel c => $"{c.DateTime:MM-dd HH:mm} · {c.DeviceLabel ?? "—"}",
        ActionEventViewModel a => $"{a.DateTime:MM-dd HH:mm} · {a.User} · {a.Content}",
        _ => row?.ToString() ?? string.Empty,
    };

    /// <summary>장비 소속 구역(그룹) 이름 — 기존 선택 편집기(<c>DetectionSelectionViewModel.DeviceZoneText</c>)와 같은 규칙.</summary>
    private static string ZoneTextOf(Ironwall.Dotnet.Monitoring.Models.Devices.IBaseDeviceModel? device)
    {
        var groups = device?.DeviceGroups;
        if (groups == null || groups.Count == 0) return "—";
        try
        {
            var provider = IoC.Get<Ironwall.Dotnet.Libraries.Devices.Providers.DeviceGroupProvider>();
            return string.Join(", ", groups.Select(id =>
                provider.OfType<Ironwall.Dotnet.Monitoring.Models.Devices.DeviceGroupModel>()
                        .FirstOrDefault(g => g.Id == id)?.Name ?? id.ToString()));
        }
        catch (Exception)
        {
            // 프로바이더가 없는 자리(단위 테스트 · 미리보기)에서는 Id 를 그대로 보인다 — 화면을 비우지 않는다.
            return string.Join(", ", groups);
        }
    }

    private static IReadOnlyList<string> KoreanOptions<TEnum>() where TEnum : struct, Enum
        => Enum.GetValues<TEnum>().Select(v => EnumKoreanMap.To(v)).Distinct().ToList();

    private static TEnum? ParseKorean<TEnum>(string text) where TEnum : struct, Enum
    {
        foreach (var value in Enum.GetValues<TEnum>())
            if (string.Equals(EnumKoreanMap.To(value), text, StringComparison.Ordinal)) return value;
        return null;
    }

    private void RaiseAll()
    {
        NotifyOfPropertyChange(nameof(Kind));
        NotifyOfPropertyChange(nameof(IsMultiple));
        NotifyOfPropertyChange(nameof(IsEmpty));
        NotifyOfPropertyChange(nameof(HasSections));
        NotifyOfPropertyChange(nameof(EmptyHint));
        NotifyOfPropertyChange(nameof(ReportButtonText));
        NotifyOfPropertyChange(nameof(CanShowReport));
        NotifyOfPropertyChange(nameof(ShowLegend));
        NotifyOfPropertyChange(nameof(BannerText));
        NotifyOfPropertyChange(nameof(Snapshot));
        NotifyOfPropertyChange(nameof(HasSnapshot));
        NotifyOfPropertyChange(nameof(ShowSnapshotBox));
        NotifyOfPropertyChange(nameof(ShowReportAction));
        NotifyOfPropertyChange(nameof(ShowQueueAction));
        NotifyOfPropertyChange(nameof(ShowHistoryAction));
        NotifyOfPropertyChange(nameof(ShowOpenOriginAction));
        NotifyOfPropertyChange(nameof(HasActionRow));
    }
}
