using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 이벤트 맵핑 워크벤치 — 스피커 연동 설정 칸(음원 그룹 콤보 · 번호 입력 폴백 · 반복)
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>스피커 연동 칸의 "?" 설명(<see cref="HelpCatalog"/>) — 키를 거는 XAML 과 같은 어셈블리에서 등록한다.</summary>
public static class MappingSpeakerHelp
{
    /// <summary>설명 키.</summary>
    public const string Key = "Events.Mapping.Speaker";

    /// <summary>설명 항목.</summary>
    public static IReadOnlyList<HelpEntry> Entries { get; } = new[]
    {
        // 키는 상수가 아닌 글자 그대로 둔다 — "?" 계약 시험(HelpSourceScan)이 첫 인자의 문자열로 설명 목록을 찾는다.
        HelpEntry.Create("Events.Mapping.Speaker", "스피커 연동 설정",
            "**음원 그룹** — 이벤트가 나면 이 스피커로 내보낼 방송 음원 묶음입니다.",
            "음원 목록을 볼 수 없으면(권한이 없거나 서버가 목록을 주지 않을 때) 음원 그룹 **번호**를 직접 넣습니다. 빈칸이면 음원 없음입니다.",
            "**반복** — 음원을 몇 번 되풀이할지 정합니다(1 이상).",
            "바꾼 값은 **[적용]**을 눌러야 저장됩니다."),
    };

#pragma warning disable CA2255 // 라이브러리의 모듈 초기화 — EventsHelp 와 같은 까닭(설명은 뷰보다 먼저, DI 와 무관).
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Register() => HelpCatalog.Register(Entries);
#pragma warning restore CA2255
}

/// <summary>음원 그룹 콤보 항목 — 맨 위는 늘 "없음".</summary>
public sealed class MappingFileGroupOption
{
    /// <summary>생성자.</summary>
    /// <param name="id">음원 그룹 PK. <c>null</c> = 없음.</param>
    /// <param name="name">표시 이름.</param>
    public MappingFileGroupOption(int? id, string name)
    {
        Id = id;
        Name = name;
    }

    /// <summary>"없음" 항목.</summary>
    public static readonly MappingFileGroupOption None = new(null, "없음");

    /// <summary>음원 그룹 PK. <c>null</c> = 없음.</summary>
    public int? Id { get; }

    /// <summary>표시 이름.</summary>
    public string Name { get; }

    /// <summary>콤보에 그리는 글자.</summary>
    public string Display => Name;

    /// <inheritdoc/>
    public override string ToString() => Display;
}

/// <summary>스피커 연동 설정 칸(GOP API 8.0.15 대조 G25).</summary>
/// <remarks>
/// <para>음원 그룹은 <c>GET /api/file-groups</c> 목록에서 고른다. 목록을 못 받으면(권한 없음 403 · 없는 경로 404 · 공급원 없음)
/// "목록을 볼 수 없음" 을 알리고 <b>번호 입력</b>으로 받는다 — 기능이 막히지 않게.</para>
/// <para>🔴 시나리오 시험은 이 뷰모델을 생성자 없이 만든다 — 이 칸의 상태는 필드 초기화에 기대지 않고 지연 초기화한다.</para>
/// </remarks>
public sealed partial class MappingWorkbenchViewModel
{
    private MappingRowViewModel? _speakerRow;
    private ObservableCollection<MappingFileGroupOption>? _fileGroupOptions;
    private IReadOnlyList<MappingFileGroupInfo>? _fileGroups;     // null = 아직 못 받음
    private bool _fileGroupsBlocked;                               // 403 · 404 — 다시 묻지 않는다
    private bool _fileGroupsUnavailable;
    private bool _rebuildingFileGroups;
    private string? _fileGroupStatusText;
    private Task? _fileGroupLoadTask;

    /// <summary>음원 그룹 후보 공급원 — 런처가 채운다. 비어 있으면 번호 입력으로 받는다.</summary>
    public IMappingFileGroupSource? FileGroupSource { get; set; }

    /// <summary>음원 그룹 콤보 항목.</summary>
    public ObservableCollection<MappingFileGroupOption> FileGroupOptions => _fileGroupOptions ??= new();

    /// <summary>설정 칸이 붙들고 있는 스피커 행(스피커 탭에서 행을 <b>하나</b> 골랐을 때만).</summary>
    public MappingRowViewModel? EditingSpeakerRow => _speakerRow;

    /// <summary>스피커 설정 칸을 보일까.</summary>
    public bool HasSpeakerEditor => _speakerRow is not null;

    /// <summary>설정 칸 머리.</summary>
    public string SpeakerEditorTitle => _speakerRow is null ? string.Empty : $"{_speakerRow.DeviceName} 연동 설정";

    /// <summary>설정 칸 값을 바꿀 수 있는가 — 해제 표시 · 끊긴 행은 바꾸지 않는다.</summary>
    public bool CanEditSpeakerRow => CanEdit && IsBoardEnabled && _speakerRow is { IsRemoved: false, IsOrphan: false };

    /// <summary>목록을 못 받아 번호 입력으로 받는가.</summary>
    public bool ShowFileGroupNumber => _fileGroupsUnavailable;

    /// <summary>음원 그룹 콤보를 보일까.</summary>
    public bool ShowFileGroupCombo => !_fileGroupsUnavailable;

    /// <summary>음원 목록 상태(불러오는 중 · 볼 수 없음). 정상이면 빈 문자열.</summary>
    public string FileGroupStatusText => _fileGroupStatusText ?? string.Empty;

    /// <summary>음원 상태 줄을 보일까.</summary>
    public bool HasFileGroupStatus => FileGroupStatusText.Length > 0;

    /// <summary>가장 최근 음원 목록 조회 — 시험이 기다릴 때 쓴다.</summary>
    internal Task FileGroupLoadTask => _fileGroupLoadTask ?? Task.CompletedTask;

    /// <summary>고른 음원 그룹. 목록을 다시 세우는 동안에는 <c>null</c>(프리셋 콤보와 같은 까닭).</summary>
    public MappingFileGroupOption? SelectedFileGroup
    {
        get => !_rebuildingFileGroups && _speakerRow is { } vm ? FileGroupOptionFor(vm.Row.FileGroupId, vm.Row.FileGroupName) : null;
        set
        {
            if (_rebuildingFileGroups || value is null || !CanEditSpeakerRow) return;
            SetFileGroup(value.Id, value.Id is null ? null : value.Name);
        }
    }

    /// <summary>음원 그룹 번호(목록을 못 받았을 때의 입력). 빈칸 = 없음, 숫자가 아니면 받지 않는다.</summary>
    public string EditFileGroupNumber
    {
        get => _speakerRow?.Row.FileGroupId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        set
        {
            if (!CanEditSpeakerRow) return;
            var text = value?.Trim() ?? string.Empty;
            if (text.Length == 0) { SetFileGroup(null, null); return; }
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            {
                NotifyOfPropertyChange();       // 받지 않은 글자는 칸에서 되돌린다
                return;
            }
            var known = _fileGroups?.FirstOrDefault(g => g.Id == id);
            SetFileGroup(id, known?.Name ?? $"#{id}");
        }
    }

    /// <summary>반복 횟수(최소 1).</summary>
    public int EditRepeatCount
    {
        get => _speakerRow?.Row.RepeatCount ?? EventMappingRules.REPEAT_COUNT_MIN;
        set
        {
            if (!CanEditSpeakerRow) return;
            var row = _speakerRow!.Row;
            var next = Math.Max(EventMappingRules.REPEAT_COUNT_MIN, value);
            if (row.RepeatCount == next) { NotifyOfPropertyChange(); return; }
            row.RepeatCount = next;
            CommitSpeakerEdit();
        }
    }

    private void SetFileGroup(int? id, string? name)
    {
        var row = _speakerRow!.Row;
        if (row.FileGroupId == id) { NotifySpeakerValues(); return; }
        row.FileGroupId = id;
        row.FileGroupName = name;
        EnsureFileGroupOption(id, name);
        CommitSpeakerEdit();
    }

    /// <summary>값을 바꿨다 — 행 상태를 다시 판정하고 보드에 알린다(Draft · 칩 · [적용]).</summary>
    private void CommitSpeakerEdit()
    {
        _speakerRow!.Row.Refresh();
        _board.Raise();
        NotifySpeakerValues();
    }

    /// <summary>고른 행에 맞춰 스피커 설정 칸을 세운다. 음원 목록은 창마다 한 번 받는다(실패하면 다음 선택 때 다시 묻는다 — 403 · 404 는 빼고).</summary>
    private void UpdateSpeakerEditor()
    {
        var next = SelectedKind == MappingActionKind.Speaker && SelectedBoardRows?.Count == 1 ? SelectedBoardRows[0] : null;
        if (ReferenceEquals(next, _speakerRow))
        {
            NotifySpeakerEditor();
            return;
        }

        _speakerRow = next;
        if (next is not null)
        {
            if (FileGroupSource is null) MarkFileGroupsUnavailable(DeviceApiMappingFileGroupSource.Unavailable);
            // 지금 가진 것으로 먼저 세우고 그 뒤에 조회를 띄운다 — 조회가 끝나 다시 세우는 것과 겹치지 않게.
            ShowFileGroups();
            if (_fileGroups is null && !_fileGroupsBlocked && FileGroupSource is not null
                && (_fileGroupLoadTask is null || _fileGroupLoadTask.IsCompleted))
                _fileGroupLoadTask = LoadFileGroupsAsync();
        }
        NotifySpeakerEditor();
    }

    private async Task LoadFileGroupsAsync()
    {
        _fileGroupStatusText = "음원 목록을 불러오는 중…";
        NotifySpeakerEditor();

        var result = await FileGroupSource!.ListFileGroupsAsync();
        if (!result.IsSuccess)
        {
            if (result.StatusCode is 403 or 404) _fileGroupsBlocked = true;
            MarkFileGroupsUnavailable(result.Message);
        }
        else
        {
            _fileGroups = result.Value ?? Array.Empty<MappingFileGroupInfo>();
            _fileGroupsUnavailable = false;
            _fileGroupStatusText = _fileGroups.Count == 0 ? "등록된 음원 그룹이 없습니다." : string.Empty;
        }
        ShowFileGroups();
        NotifySpeakerEditor();
    }

    private void MarkFileGroupsUnavailable(string message)
    {
        _fileGroupsUnavailable = true;
        _fileGroupStatusText = string.IsNullOrWhiteSpace(message) ? DeviceApiMappingFileGroupSource.LoadFailed : message;
    }

    /// <summary>
    /// 콤보 항목을 다시 세운다. <b>지금 행이 가진 음원 그룹은 목록에 없어도 넣는다</b> — 빠지면 콤보가 빈칸으로 보인다.
    /// </summary>
    private void ShowFileGroups()
    {
        _rebuildingFileGroups = true;
        try
        {
            FileGroupOptions.Clear();
            FileGroupOptions.Add(MappingFileGroupOption.None);
            foreach (var group in _fileGroups ?? Array.Empty<MappingFileGroupInfo>())
                FileGroupOptions.Add(new MappingFileGroupOption(group.Id, group.Name));
            if (_speakerRow is { } vm) EnsureFileGroupOption(vm.Row.FileGroupId, vm.Row.FileGroupName);
        }
        finally
        {
            _rebuildingFileGroups = false;
        }
        NotifySpeakerValues();
    }

    private void EnsureFileGroupOption(int? id, string? name)
    {
        if (id is not int value || FileGroupOptions.Any(o => o.Id == value)) return;
        FileGroupOptions.Add(new MappingFileGroupOption(value, string.IsNullOrWhiteSpace(name) ? $"#{value}" : name!));
    }

    private MappingFileGroupOption FileGroupOptionFor(int? id, string? name)
    {
        if (id is not int value) return FileGroupOptions.FirstOrDefault(o => o.Id is null) ?? MappingFileGroupOption.None;
        return FileGroupOptions.FirstOrDefault(o => o.Id == value)
            ?? new MappingFileGroupOption(value, string.IsNullOrWhiteSpace(name) ? $"#{value}" : name!);
    }

    private void NotifySpeakerEditor()
    {
        NotifyOfPropertyChange(nameof(EditingSpeakerRow));
        NotifyOfPropertyChange(nameof(HasSpeakerEditor));
        NotifyOfPropertyChange(nameof(SpeakerEditorTitle));
        NotifyOfPropertyChange(nameof(CanEditSpeakerRow));
        NotifyOfPropertyChange(nameof(ShowFileGroupNumber));
        NotifyOfPropertyChange(nameof(ShowFileGroupCombo));
        NotifyOfPropertyChange(nameof(FileGroupStatusText));
        NotifyOfPropertyChange(nameof(HasFileGroupStatus));
        NotifySpeakerValues();
    }

    private void NotifySpeakerValues()
    {
        NotifyOfPropertyChange(nameof(SelectedFileGroup));
        NotifyOfPropertyChange(nameof(EditFileGroupNumber));
        NotifyOfPropertyChange(nameof(EditRepeatCount));
    }
}
