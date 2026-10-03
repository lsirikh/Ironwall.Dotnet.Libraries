using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 카메라 프리셋 관리 창 — 만들기 · 고치기 · 지우기
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 한 카메라의 프리셋(서버 <c>/devices/cameras/{id}/presets</c>)을 관리하는 창.
/// </summary>
/// <remarks>
/// <para>워크벤치 카메라 연동 설정의 <b>[프리셋 관리…]</b>로 연다. 저장 · 삭제는 <b>바로 서버로</b> 간다 —
/// 맵핑 속성 폼처럼 호출 1회라 Draft 를 두지 않는다.</para>
/// <para>PTZ 자리를 카메라에 기억시키는 일(ONVIF SetPreset)은 이 창이 하지 않는다 — 서버 프리셋 레코드만 다룬다.</para>
/// <para>이벤트 때 GIS 는 서버 <c>preset_index</c> 를 문자열로 바꿔 ONVIF GotoPreset 토큰으로 보낸다. 그래서 창이 열리면
/// <b>카메라의 ONVIF 프리셋을 함께 읽어</b>(<see cref="IMappingCameraPresetReader"/>) 서버 프리셋을 카메라 것에서 골라 만들게 하고,
/// 카메라에 없는 번호의 서버 프리셋은 경고한다.</para>
/// </remarks>
public sealed class MappingPresetManagerViewModel : Screen
{
    /// <summary>새 프리셋의 투어링 기본값(서버 기본과 같다).</summary>
    public const int DEFAULT_TOURING_TIME = 10;

    private readonly IMappingPresetEditor _editor;
    private readonly IMappingCameraPresetReader? _cameraReader;
    private IReadOnlyList<MappingCameraPreset>? _cameraPresets;
    private string _cameraPresetStatus = string.Empty;
    private bool _isReadingCameraPresets;
    private MappingPresetDetail? _selected;
    private bool _isNew;
    private string _editIndex = string.Empty;
    private string _editName = string.Empty;
    private string _editTouringTime = string.Empty;
    private bool _editRestricted;
    private string _message = string.Empty;
    private DialogMessageSeverity _messageSeverity = DialogMessageSeverity.Normal;
    private bool _isBusy;
    private bool _isConfirmingDelete;

    /// <summary>생성자.</summary>
    /// <param name="editor">서버 왕복.</param>
    /// <param name="cameraId">카메라 id.</param>
    /// <param name="cameraName">카메라 이름(창 제목).</param>
    /// <param name="canEdit">편집 권한. <c>false</c> 면 목록만 본다.</param>
    /// <param name="cameraPresets">카메라 ONVIF 프리셋 읽기. 생략하면 "카메라 프리셋" 칸을 감춘다.</param>
    public MappingPresetManagerViewModel(IMappingPresetEditor editor, int cameraId, string cameraName, bool canEdit = true,
                                         IMappingCameraPresetReader? cameraPresets = null)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _cameraReader = cameraPresets;
        CameraId = cameraId;
        CameraName = string.IsNullOrWhiteSpace(cameraName) ? $"#{cameraId}" : cameraName;
        CanEdit = canEdit;
        DisplayName = Title;
    }

    #region - 상태 -
    /// <summary>카메라 id.</summary>
    public int CameraId { get; }

    /// <summary>카메라 이름.</summary>
    public string CameraName { get; }

    /// <summary>창 제목.</summary>
    public string Title => $"{CameraName} 프리셋 관리";

    /// <summary>편집 권한.</summary>
    public bool CanEdit { get; }

    /// <summary>서버에 한 번이라도 쓰기가 성공했는가 — 워크벤치가 프리셋 콤보를 다시 받을지 정한다.</summary>
    public bool HasChanges { get; private set; }

    /// <summary>프리셋 목록(번호 순).</summary>
    public ObservableCollection<MappingPresetDetail> Presets { get; } = new();

    /// <summary>목록이 비었나(못 불러온 것과 구분은 <see cref="Message"/> 가 한다).</summary>
    public bool IsEmpty => Presets.Count == 0;

    /// <summary>가장 최근 목록 조회 — 시험이 기다릴 때 쓴다.</summary>
    internal Task LoadTask { get; private set; } = Task.CompletedTask;

    /// <summary>고른 프리셋. 고르면 폼에 그 값을 채운다.</summary>
    public MappingPresetDetail? SelectedPreset
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            _selected = value;
            NotifyOfPropertyChange();
            IsConfirmingDelete = false;
            if (value is not null)
            {
                _isNew = false;
                FillForm(value.PresetIndex, value.Name, value.TouringTime, value.IsRestrictedZone);
            }
            else if (!_isNew)
            {
                FillForm(null, string.Empty, DEFAULT_TOURING_TIME, false);
            }
            NotifyForm();
        }
    }

    /// <summary>새 프리셋을 적는 중인가.</summary>
    public bool IsNew => _isNew;

    /// <summary>폼을 보일까(고른 것이 있거나 새로 적는 중).</summary>
    public bool HasForm => _isNew || _selected is not null;

    /// <summary>폼 머리.</summary>
    public string FormTitle => _isNew ? "새 프리셋" : _selected is { } p ? $"{p.PresetIndex}. {p.Name}" : string.Empty;

    /// <summary>프리셋 번호(카메라의 프리셋 번호).</summary>
    public string EditIndex
    {
        get => _editIndex;
        set { _editIndex = value ?? string.Empty; NotifyOfPropertyChange(); NotifyValidation(); }
    }

    /// <summary>이름.</summary>
    public string EditName
    {
        get => _editName;
        set { _editName = value ?? string.Empty; NotifyOfPropertyChange(); NotifyValidation(); }
    }

    /// <summary>투어링 시간(초).</summary>
    public string EditTouringTime
    {
        get => _editTouringTime;
        set { _editTouringTime = value ?? string.Empty; NotifyOfPropertyChange(); NotifyValidation(); }
    }

    /// <summary>감시금지구역.</summary>
    public bool EditRestricted
    {
        get => _editRestricted;
        set { _editRestricted = value; NotifyOfPropertyChange(); NotifyValidation(); }
    }

    /// <summary>폼 값이 틀렸으면 그 사유 한 줄, 맞으면 빈 문자열.</summary>
    public string ValidationText => HasForm ? Validate(out _) ?? string.Empty : string.Empty;

    /// <summary>사유 줄을 보일까.</summary>
    public bool HasValidationText => ValidationText.Length > 0;

    /// <summary>버튼 줄 왼쪽 글(결과 · 실패).</summary>
    public string Message
    {
        get => _message;
        private set { _message = value; NotifyOfPropertyChange(); }
    }

    /// <summary>버튼 줄 왼쪽 글의 무게.</summary>
    public DialogMessageSeverity MessageSeverity
    {
        get => _messageSeverity;
        private set { _messageSeverity = value; NotifyOfPropertyChange(); }
    }

    /// <summary>서버와 주고받는 중.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; NotifyOfPropertyChange(); NotifyCommands(); }
    }

    /// <summary>삭제 확인 줄을 보이는 중.</summary>
    public bool IsConfirmingDelete
    {
        get => _isConfirmingDelete;
        private set { _isConfirmingDelete = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(DeleteConfirmText)); }
    }

    /// <summary>삭제 확인 줄 글.</summary>
    public string DeleteConfirmText => _selected is { } p ? $"'{p.Name}' 프리셋을 지울까요?" : string.Empty;

    /// <summary>[새 프리셋] 을 누를 수 있나.</summary>
    public bool CanBeginNew => CanEdit && !IsBusy;

    /// <summary>[저장] 을 누를 수 있나 — 값이 맞고, 기존 프리셋이면 무엇이 바뀌었을 때만.</summary>
    public bool CanSave => CanEdit && !IsBusy && HasForm && Validate(out var draft) is null && (_isNew || IsChanged(draft!));

    /// <summary>[삭제] 를 누를 수 있나.</summary>
    public bool CanDelete => CanEdit && !IsBusy && !_isNew && _selected is not null;

    /// <summary>[저장] 버튼 글.</summary>
    public string SaveText => _isNew ? "만들기" : "저장";
    #endregion

    #region - 카메라 프리셋(ONVIF) -
    /// <summary>카메라 프리셋을 읽을 길이 있나 — 없으면 그 칸을 감춘다.</summary>
    public bool HasCameraPresetReader => _cameraReader is not null;

    /// <summary>카메라가 기억하는 프리셋(토큰 순서 그대로) — 서버 등록 여부를 함께 적는다.</summary>
    public ObservableCollection<MappingCameraPresetRow> CameraPresets { get; } = new();

    /// <summary>카메라 프리셋 칸의 상태 한 줄(읽는 중 · 실패 · 없음).</summary>
    public string CameraPresetStatus
    {
        get => _cameraPresetStatus;
        private set { _cameraPresetStatus = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasCameraPresetStatus)); }
    }

    /// <summary>상태 줄을 보일까.</summary>
    public bool HasCameraPresetStatus => _cameraPresetStatus.Length > 0;

    /// <summary>카메라에서 읽는 중.</summary>
    public bool IsReadingCameraPresets
    {
        get => _isReadingCameraPresets;
        private set { _isReadingCameraPresets = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanReadCameraPresets)); }
    }

    /// <summary>[카메라에서 읽기] 를 누를 수 있나.</summary>
    public bool CanReadCameraPresets => _cameraReader is not null && !_isReadingCameraPresets;

    /// <summary>
    /// 카메라에 없는 번호의 서버 프리셋 경고. 카메라 프리셋을 읽지 못했으면 판정하지 않는다(빈 문자열).
    /// </summary>
    public string CameraMismatchText
    {
        get
        {
            if (_cameraPresets is null) return string.Empty;
            var tokens = new HashSet<string>(_cameraPresets.Select(p => p.Token), StringComparer.Ordinal);
            var missing = Presets.Where(p => !tokens.Contains(p.PresetIndex.ToString(CultureInfo.InvariantCulture)))
                                 .Select(p => p.PresetIndex.ToString(CultureInfo.InvariantCulture))
                                 .ToList();
            return missing.Count == 0
                ? string.Empty
                : $"카메라에 없는 번호: {string.Join(", ", missing)} — 이벤트 때 이 프리셋으로는 PTZ 가 움직이지 않습니다.";
        }
    }

    /// <summary>불일치 경고를 보일까.</summary>
    public bool HasCameraMismatch => CameraMismatchText.Length > 0;

    /// <summary>가장 최근 카메라 프리셋 읽기 — 시험이 기다릴 때 쓴다.</summary>
    internal Task CameraPresetTask { get; private set; } = Task.CompletedTask;

    /// <summary>[카메라에서 읽기] — 카메라의 ONVIF 프리셋을 다시 읽는다.</summary>
    public Task ReadCameraPresetsAsync() => CameraPresetTask = ReadCameraPresetsCoreAsync();

    private async Task ReadCameraPresetsCoreAsync()
    {
        if (!CanReadCameraPresets) return;
        IsReadingCameraPresets = true;
        CameraPresetStatus = "카메라에서 프리셋을 읽는 중…";
        try
        {
            var result = await _cameraReader!.ReadCameraPresetsAsync(CameraId);
            if (!result.IsSuccess)
            {
                // 못 읽은 것을 "없다" 로 말하지 않고, 지난 결과로 경고를 내지도 않는다.
                _cameraPresets = null;
                CameraPresetStatus = result.Message;
            }
            else
            {
                _cameraPresets = result.Value ?? Array.Empty<MappingCameraPreset>();
                CameraPresetStatus = _cameraPresets.Count == 0 ? "카메라에 기억된 프리셋이 없습니다." : string.Empty;
            }
            RefreshCameraRows();
        }
        finally
        {
            IsReadingCameraPresets = false;
        }
    }

    /// <summary>
    /// 카메라 프리셋 한 줄을 골라 서버 프리셋 폼에 옮긴다 — 번호는 토큰, 이름은 카메라 이름.
    /// 같은 번호의 서버 프리셋이 있으면 그것을 고른다(새로 만들지 않는다).
    /// </summary>
    public void UseCameraPreset(MappingCameraPresetRow? row)
    {
        if (row is null || !CanEdit || IsBusy) return;
        var preset = row.Preset;
        if (preset.Index is not int index)
        {
            Say($"카메라 토큰 '{preset.Token}' 은 번호가 아니라 서버 프리셋으로 담을 수 없습니다.", DialogMessageSeverity.Warning);
            return;
        }

        var existing = Presets.FirstOrDefault(p => p.PresetIndex == index);
        if (existing is not null)
        {
            _isNew = false;
            SelectedPreset = existing;
            Say($"{index}번은 이미 서버에 있습니다.", DialogMessageSeverity.Info);
            return;
        }

        _isNew = true;
        _selected = null;
        NotifyOfPropertyChange(nameof(SelectedPreset));
        IsConfirmingDelete = false;
        FillForm(index, string.IsNullOrWhiteSpace(preset.Name) ? $"프리셋 {index}" : preset.Name, DEFAULT_TOURING_TIME, false);
        Say(string.Empty);
        NotifyForm();
    }

    private void RefreshCameraRows()
    {
        CameraPresets.Clear();
        if (_cameraPresets is not null)
        {
            var registered = new HashSet<int>(Presets.Select(p => p.PresetIndex));
            foreach (var p in _cameraPresets)
                CameraPresets.Add(new MappingCameraPresetRow(p, p.Index is int i && registered.Contains(i)));
        }
        NotifyOfPropertyChange(nameof(CameraMismatchText));
        NotifyOfPropertyChange(nameof(HasCameraMismatch));
    }
    #endregion

    #region - 동작 -
    /// <inheritdoc/>
    protected override Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        LoadTask = LoadAsync();
        _ = ReadCameraPresetsAsync();   // 카메라 응답은 수 초 걸릴 수 있다 — 서버 목록을 기다리게 하지 않는다
        return Task.CompletedTask;
    }

    /// <summary>목록을 다시 받는다. <paramref name="selectId"/> 가 있으면 그 프리셋을 고른다.</summary>
    public async Task LoadAsync(int? selectId = null)
    {
        IsBusy = true;
        try
        {
            var result = await _editor.ListPresetDetailsAsync(CameraId);
            if (!result.IsSuccess)
            {
                // 못 불러온 것을 "없다" 로 말하지 않는다.
                Say(result.Message, DialogMessageSeverity.Critical);
                return;
            }

            var keep = selectId ?? _selected?.Id;
            Presets.Clear();
            foreach (var preset in (result.Value ?? Array.Empty<MappingPresetDetail>())
                         .Where(p => EventMappingOwnership(p))
                         .OrderBy(p => p.PresetIndex))
                Presets.Add(preset);
            NotifyOfPropertyChange(nameof(IsEmpty));
            RefreshCameraRows();

            _selected = null;           // 같은 id 의 새 레코드로 바꿔 끼운다(이름이 바뀌었을 수 있다)
            if (!_isNew) SelectedPreset = keep is int id ? Presets.FirstOrDefault(p => p.Id == id) : null;
            else NotifyOfPropertyChange(nameof(SelectedPreset));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>[새 프리셋] — 지금 가장 큰 번호의 다음 번호로 폼을 연다.</summary>
    public void BeginNew()
    {
        if (!CanBeginNew) return;
        var next = Presets.Count == 0 ? 1 : Presets.Max(p => p.PresetIndex) + 1;
        _isNew = true;
        _selected = null;
        NotifyOfPropertyChange(nameof(SelectedPreset));
        IsConfirmingDelete = false;
        FillForm(next, $"프리셋 {next}", DEFAULT_TOURING_TIME, false);
        Say(string.Empty);
        NotifyForm();
    }

    /// <summary>[저장] · [만들기] — 바로 서버로 보낸다.</summary>
    public async Task SaveAsync()
    {
        if (!CanSave || Validate(out var draft) is not null) return;

        IsBusy = true;
        try
        {
            var result = _isNew
                ? await _editor.CreatePresetAsync(CameraId, draft!)
                : await _editor.PatchPresetAsync(CameraId, _selected!.Id, draft!);
            if (!result.IsSuccess)
            {
                // 적은 값은 그대로 둔다 — 고쳐서 다시 누를 수 있게.
                Say(result.Message, DialogMessageSeverity.Critical);
                return;
            }

            HasChanges = true;
            var created = _isNew;
            _isNew = false;
            IsBusy = false;
            await LoadAsync(result.Value!.Id);
            Say(created ? $"'{result.Value.Name}' 프리셋을 만들었습니다." : $"'{result.Value.Name}' 프리셋을 저장했습니다.", DialogMessageSeverity.Info);
        }
        finally
        {
            IsBusy = false;
            NotifyForm();
        }
    }

    /// <summary>[삭제] — 같은 창 안에서 한 번 더 묻는다.</summary>
    public void RequestDelete()
    {
        if (!CanDelete) return;
        IsConfirmingDelete = true;
    }

    /// <summary>삭제 확인 줄의 [취소].</summary>
    public void CancelDelete() => IsConfirmingDelete = false;

    /// <summary>삭제 확인 줄의 [삭제].</summary>
    public async Task ConfirmDeleteAsync()
    {
        if (!IsConfirmingDelete || !CanDelete) return;
        var target = _selected!;
        IsConfirmingDelete = false;

        IsBusy = true;
        try
        {
            var result = await _editor.DeletePresetAsync(CameraId, target.Id);
            if (!result.IsSuccess)
            {
                Say(result.Message, DialogMessageSeverity.Critical);
                return;
            }

            HasChanges = true;
            _selected = null;
            IsBusy = false;
            await LoadAsync();
            Say($"'{target.Name}' 프리셋을 지웠습니다.", DialogMessageSeverity.Info);
        }
        finally
        {
            IsBusy = false;
            NotifyForm();
        }
    }

    /// <summary>[닫기] · ✕ · ESC.</summary>
    public Task CloseAsync() => TryCloseAsync(HasChanges);
    #endregion

    #region - 판정 -
    /// <summary>폼 값을 읽는다. 틀렸으면 사유를 돌려주고 <paramref name="draft"/> 는 <c>null</c>.</summary>
    internal string? Validate(out MappingPresetDraft? draft)
    {
        draft = null;
        if (!int.TryParse(_editIndex.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index < 1)
            return "번호는 1 이상의 숫자로 적으세요.";
        if (Presets.Any(p => p.PresetIndex == index && (_isNew || p.Id != _selected?.Id)))
            return $"{index}번 프리셋이 이미 있습니다.";
        var name = _editName.Trim();
        if (name.Length == 0)
            return "이름을 적으세요.";
        if (!int.TryParse(_editTouringTime.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var touring) || touring < 0)
            return "투어링 시간은 0 이상의 숫자(초)로 적으세요.";

        draft = new MappingPresetDraft(index, name, touring, _editRestricted);
        return null;
    }

    private bool IsChanged(MappingPresetDraft draft)
        => _selected is not { } p
           || p.PresetIndex != draft.PresetIndex
           || !string.Equals(p.Name, draft.Name, StringComparison.Ordinal)
           || p.TouringTime != draft.TouringTime
           || p.IsRestrictedZone != draft.IsRestrictedZone;

    /// <summary>다른 카메라 것은 목록에 넣지 않는다(서버가 섞어 주더라도).</summary>
    private bool EventMappingOwnership(MappingPresetDetail p)
        => Ironwall.Dotnet.Libraries.Messages.Dto.Integrations.EventMappingRules.IsPresetOwnedBy(p.CameraId, CameraId);
    #endregion

    #region - 알림 -
    private void FillForm(int? index, string name, int touring, bool restricted)
    {
        _editIndex = index?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _editName = name;
        _editTouringTime = touring.ToString(CultureInfo.InvariantCulture);
        _editRestricted = restricted;
    }

    private void Say(string text, DialogMessageSeverity severity = DialogMessageSeverity.Normal)
    {
        MessageSeverity = severity;
        Message = text;
    }

    private void NotifyForm()
    {
        NotifyOfPropertyChange(nameof(IsNew));
        NotifyOfPropertyChange(nameof(HasForm));
        NotifyOfPropertyChange(nameof(FormTitle));
        NotifyOfPropertyChange(nameof(SaveText));
        NotifyOfPropertyChange(nameof(EditIndex));
        NotifyOfPropertyChange(nameof(EditName));
        NotifyOfPropertyChange(nameof(EditTouringTime));
        NotifyOfPropertyChange(nameof(EditRestricted));
        NotifyValidation();
    }

    private void NotifyValidation()
    {
        NotifyOfPropertyChange(nameof(ValidationText));
        NotifyOfPropertyChange(nameof(HasValidationText));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        NotifyOfPropertyChange(nameof(CanBeginNew));
        NotifyOfPropertyChange(nameof(CanSave));
        NotifyOfPropertyChange(nameof(CanDelete));
    }
    #endregion
}
