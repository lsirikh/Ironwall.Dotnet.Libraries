using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Integrations;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 워크벤치 표시 항목 — 매핑 목록 행 · 팔레트 카드 · 보드 행
   Created By   : Claude
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>왼쪽 매핑 목록의 한 행.</summary>
public sealed class MappingListItemViewModel : PropertyChangedBase
{
    private int _cameraCount;
    private int _speakerCount;
    private int _lampCount;

    /// <summary>생성자.</summary>
    /// <param name="dto">서버가 준 매핑 1건.</param>
    public MappingListItemViewModel(EventMappingReadDto dto)
    {
        Dto = dto;
    }

    /// <summary>원본 — 저장 직전 대조와 PATCH 재채움이 쓴다.</summary>
    public EventMappingReadDto Dto { get; private set; }

    /// <summary>매핑 PK.</summary>
    public int Id => Dto.Id;

    /// <summary>표시 이름.</summary>
    public string Name => string.IsNullOrWhiteSpace(Dto.NameEvent) ? $"#{Dto.Id}" : Dto.NameEvent!;

    /// <summary>카테고리 한국어 표기.</summary>
    public string CategoryText => EventMappingRules.CategoryLabel(Dto.CategoryEventMapping);

    /// <summary>운용 중인가 — 중지면 화면이 흐리게 그린다.</summary>
    public bool IsActive => Dto.Status;

    /// <summary>카메라 배선 수.</summary>
    public int CameraCount { get => _cameraCount; set { _cameraCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CountText)); } }

    /// <summary>스피커 배선 수.</summary>
    public int SpeakerCount { get => _speakerCount; set { _speakerCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CountText)); } }

    /// <summary>경광등 배선 수.</summary>
    public int LampCount { get => _lampCount; set { _lampCount = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CountText)); } }

    /// <summary>개수 배지 — 카메라·스피커·경광등 순.</summary>
    public string CountText => $"{CameraCount}·{SpeakerCount}·{LampCount}";

    /// <summary>서버에서 다시 읽은 값으로 갈아끼운다.</summary>
    public void Replace(EventMappingReadDto dto)
    {
        Dto = dto;
        Refresh();
    }
}

/// <summary>오른쪽 팔레트의 한 카드(드래그 소스).</summary>
public sealed class MappingPaletteItemViewModel : PropertyChangedBase
{
    private bool _isRegistered;

    /// <summary>생성자.</summary>
    public MappingPaletteItemViewModel(MappingActionKind kind, MappingDeviceInfo device)
    {
        Kind = kind;
        Device = device;
    }

    /// <summary>종류 축.</summary>
    public MappingActionKind Kind { get; }

    /// <summary>장비 정보.</summary>
    public MappingDeviceInfo Device { get; }

    /// <summary>장비 id.</summary>
    public int Id => Device.Id;

    /// <summary>표시 이름.</summary>
    public string Name => Device.Name;

    /// <summary>부제(종류 + id).</summary>
    public string Subtitle => $"{Device.TypeText} · #{Device.Id}";

    /// <summary>이미 이 맵핑에 들어가 있는가 — 흐리게 + "등록됨" 으로 중복 드래그를 미리 억제한다.</summary>
    public bool IsRegistered { get => _isRegistered; set { _isRegistered = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(StatusText)); } }

    /// <summary>장비 자체가 운용 중인가.</summary>
    public bool IsDeviceEnabled => Device.IsEnabled;

    /// <summary>상태 칸 문구.</summary>
    public string StatusText => IsRegistered ? "등록됨" : IsDeviceEnabled ? string.Empty : "사용 안 함";

    /// <summary>드래그 고스트에 쓰는 이름.</summary>
    public string Display => Name;
}

/// <summary>가운데 액션 보드의 한 행(드래그 소스 + 드롭 타깃).</summary>
public sealed class MappingRowViewModel : PropertyChangedBase
{
    private string _deviceName = string.Empty;

    /// <summary>생성자.</summary>
    /// <param name="row">순수 모델 행.</param>
    public MappingRowViewModel(MappingBoardRow row)
    {
        Row = row;
    }

    /// <summary>순수 모델 행.</summary>
    public MappingBoardRow Row { get; }

    /// <summary>종류 축.</summary>
    public MappingActionKind Kind => Row.Kind;

    /// <summary>자동화 식별자에 쓰는 키 — 새 행은 아직 <c>config_id</c> 가 없어 장비 id 를 쓴다.</summary>
    public string Key => Row.IsPersisted ? Row.ConfigId.ToString() : $"new{Row.DeviceId ?? 0}";

    /// <summary>장비 표시 이름(캐시 조인). 캐시에 없으면 <c>#id</c>.</summary>
    public string DeviceName { get => _deviceName; set { _deviceName = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(Display)); } }

    /// <summary>드래그 고스트에 쓰는 이름.</summary>
    public string Display => DeviceName;

    /// <summary>장비가 끊긴 행인가 — 저장을 막는다.</summary>
    public bool IsOrphan => Row.IsOrphan;

    /// <summary>해제 표시(취소선).</summary>
    public bool IsRemoved => Row.State == MappingDraftState.Removed;

    /// <summary>새로 들어온 행.</summary>
    public bool IsAdded => Row.State == MappingDraftState.Added;

    /// <summary>값 또는 순서가 바뀐 행.</summary>
    public bool IsEdited => Row.State == MappingDraftState.Edited;

    /// <summary>미저장 변경이 걸린 행(앰버 파선으로 그린다).</summary>
    public bool IsDraft => IsAdded || IsEdited || IsRemoved;

    /// <summary>행 활성 여부.</summary>
    public bool IsEnable => Row.IsEnable;

    /// <summary>마지막 적용에서 실패한 행 — 배지를 단다.</summary>
    public bool HasFailure { get; private set; }

    /// <summary>실패 사유(툴팁).</summary>
    public string? FailureText { get; private set; }

    /// <summary>실패 표시를 세운다.</summary>
    public void MarkFailure(string? text)
    {
        HasFailure = true;
        FailureText = text;
        Refresh();
    }

    /// <summary>실패 표시를 지운다.</summary>
    public void ClearFailure()
    {
        if (!HasFailure) return;
        HasFailure = false;
        FailureText = null;
        Refresh();
    }

    /// <summary>
    /// 설정 칩 문구 — 종류마다 다르다. 값이 없으면 <b>"—"</b> 로 자리를 지킨다
    /// (빈칸으로 두면 카드 폭이 흔들린다).
    /// </summary>
    public IReadOnlyList<string> Chips => Kind switch
    {
        MappingActionKind.Camera => new[]
        {
            Row.TargetPresetName is { Length: > 0 } t ? $"타깃 {t}" : "타깃 —",
            Row.HomePresetName is { Length: > 0 } h ? $"홈 {h}" : "홈 —",
            $"지연 {Row.DelayTime}초",
        },
        MappingActionKind.Speaker => new[]
        {
            Row.FileGroupName is { Length: > 0 } f ? $"음원 {f}" : "음원 —",
            $"반복 ×{Row.RepeatCount}",
        },
        MappingActionKind.Lamp => new[]
        {
            ColorText(Row.Color),
            Row.LightMode == EnumLightMode.Blinking ? "점멸" : "계속",
            $"{BuzzerText(Row.BuzzerSound)} · {Row.BuzzerTime}초",
        },
        _ => System.Array.Empty<string>(),
    };

    /// <summary>경광등 색의 한국어 표기.</summary>
    public static string ColorText(EnumLampColor color) => color switch
    {
        EnumLampColor.Red => "빨강",
        EnumLampColor.Orange => "주황",
        EnumLampColor.Green => "초록",
        EnumLampColor.Blue => "파랑",
        EnumLampColor.White => "하양",
        _ => color.ToString(),
    };

    /// <summary>부저음의 한국어 표기 — 서버 원값(영문)을 그대로 내지 않는다.</summary>
    public static string BuzzerText(EnumBuzzerSound sound) => sound switch
    {
        EnumBuzzerSound.FireAWang => "화재음",
        EnumBuzzerSound.Emergency => "비상음",
        EnumBuzzerSound.Ambulance => "구급음",
        EnumBuzzerSound.PiPiPi => "삐삐삐",
        EnumBuzzerSound.PiContinue => "연속음",
        _ => sound.ToString(),
    };

    /// <summary>표시 값을 전부 다시 읽는다.</summary>
    public void RefreshAll()
    {
        Refresh();
        NotifyOfPropertyChange(nameof(Chips));
        NotifyOfPropertyChange(nameof(IsRemoved));
        NotifyOfPropertyChange(nameof(IsAdded));
        NotifyOfPropertyChange(nameof(IsEdited));
        NotifyOfPropertyChange(nameof(IsDraft));
        NotifyOfPropertyChange(nameof(IsOrphan));
        NotifyOfPropertyChange(nameof(IsEnable));
        NotifyOfPropertyChange(nameof(Key));
    }
}

/// <summary>팔레트 검색 — 순수 판정(이름·부제·id 를 본다).</summary>
public static class MappingPaletteFilter
{
    /// <summary>검색어에 걸리는가. 빈 검색어는 전부 통과한다.</summary>
    public static bool Matches(MappingPaletteItemViewModel item, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        var needle = search.Trim();
        return item.Name.Contains(needle, System.StringComparison.CurrentCultureIgnoreCase)
            || item.Subtitle.Contains(needle, System.StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>검색어로 거른 결과(순서 보존).</summary>
    public static IReadOnlyList<MappingPaletteItemViewModel> Apply(
        IEnumerable<MappingPaletteItemViewModel> items, string? search)
        => items.Where(i => Matches(i, search)).ToList();
}
