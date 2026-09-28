using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
/****************************************************************************
   Purpose      : 억제 편집 '780 서랍' 의 뷰모델 — 확정 두 칸 폼 + 대상 칩 트레이.
                  서버는 [저장] 한 번에만 불린다. 되돌리기는 호출 0.
   Created By   : GHLee
   Created On   : 2026-09-20
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>저장 한 번의 결과.</summary>
/// <param name="Ok">서버가 받아들였는가.</param>
/// <param name="Message">화면에 남길 한 줄(성공 · 실패 모두 말이 있어야 한다 — 거절이 조용하면 안 된다).</param>
/// <param name="Saved">서버가 확정해 돌려준 스케줄(성공일 때).</param>
public readonly record struct SuppressionSaveOutcome(bool Ok, string Message, EventSuppressionScheduleDto? Saved);

/// <summary>
/// 억제 편집 서랍(정본 SB L738-742 CSS · L2869-2884 마크업 · L1122 결정 E-D7).
/// </summary>
/// <remarks>
/// <para>폭 780 · 오른쪽에 붙고 · 스크림은 <b>머리띠를 막지 않는다</b>. 깨끗하면 ESC 로 닫히고,
/// 미적용 변경이 있으면 막고 흔든다.</para>
/// <para>호출 스레드: UI. 저장 완료가 작업 스레드로 올 수 있어 상태 갱신은 <see cref="Execute.OnUIThread"/> 로 옮긴다.</para>
/// </remarks>
public sealed class SuppressionDrawerViewModel : PropertyChangedBase
{
    private readonly IClock _clock;
    private readonly DeviceProvider? _devices;
    private readonly DeviceGroupProvider? _groups;
    private readonly Func<SuppressionDraft, CancellationToken, Task<SuppressionSaveOutcome>> _save;
    private readonly Func<bool> _canEdit;
    private readonly Func<IReadOnlyList<EventSuppressionScheduleDto>> _others;
    private readonly Action<string, Exception>? _onError;

    private SuppressionDraft _draft;
    private string _openedSignature = string.Empty;
    private bool _isOpen;
    private bool _isSaving;
    private string _statusLine = string.Empty;
    private string _pickerSearch = string.Empty;
    private int _shakeToken;

    public SuppressionDrawerViewModel(IClock clock,
                                      DeviceProvider? devices,
                                      DeviceGroupProvider? groups,
                                      Func<SuppressionDraft, CancellationToken, Task<SuppressionSaveOutcome>> save,
                                      Func<bool>? canEdit = null,
                                      Func<IReadOnlyList<EventSuppressionScheduleDto>>? others = null,
                                      Action<string, Exception>? onError = null)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _devices = devices;
        _groups = groups;
        _save = save ?? throw new ArgumentNullException(nameof(save));
        _canEdit = canEdit ?? (() => true);
        _others = others ?? (() => Array.Empty<EventSuppressionScheduleDto>());
        _onError = onError;

        _draft = SuppressionDraft.NewSchedule(new DateTimeOffset(_clock.Now));
        Tray = new ObservableCollection<SuppressionTargetChip>();
        PickerItems = new ObservableCollection<SuppressionTargetChip>();

        Drop = new SuppressionTargetTrayHandler(
            () => TargetType,
            () => Tray.ToList(),
            _canEdit,
            AcceptPlan);
        Drop.Completed += line => StatusLine = line;
    }

    #region - 열고 닫기 -

    /// <summary>서랍이 열려 있는가.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        private set { _isOpen = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HeaderText)); }
    }

    /// <summary>새 스케줄인가(머리글 · 반복 잠금 판정).</summary>
    public bool IsNew => _draft.IsNew;

    /// <summary>서랍 머리글 — 정본 SB L2869.</summary>
    public string HeaderText => IsNew ? "새 억제 스케줄" : $"억제 스케줄 수정 · {_draft.Name}";

    /// <summary>
    /// <b>떠나도 되는가</b> — 서랍을 닫거나, 다른 초안으로 갈아 끼우거나, 레일을 옮기기 전에 묻는 단 하나의 질문.
    /// </summary>
    /// <remarks>
    /// 정본(window-layout-system-storyboard.html L2372 <c>switchTab</c> → L2665 <c>dirtyBlock</c>)은
    /// <b>탭 전환까지</b> 미적용 변경으로 막는다. 닫기만 막고 레일 전환은 통과시키면
    /// 초안이 <b>말없이 사라진다</b> — 가장 나쁜 실패다.
    /// </remarks>
    public bool CanLeave => !IsOpen || (!IsDirty && !IsSaving);

    /// <summary>떠나도 되면 true. 막았으면 false 를 돌려주고 흔든다. <b>호출부는 false 를 반드시 존중한다.</b></summary>
    public bool TryLeave()
    {
        if (CanLeave) return true;

        StatusLine = IsSaving
            ? SavingText
            : "저장하거나 되돌린 뒤 이동하세요.";
        ShakeToken++;
        NotifyOfPropertyChange(nameof(StatusLine));
        return false;
    }

    /// <summary>새로 만들기로 연다. 미적용 변경이 있으면 <b>열지 않는다</b>.</summary>
    public bool OpenNew()
    {
        if (!TryLeave()) return false;

        Load(SuppressionDraft.NewSchedule(new DateTimeOffset(_clock.Now)));
        StatusLine = OpenNewHintText;
        return true;
    }

    /// <summary>새로 열었을 때 바닥 한 줄.</summary>
    public const string OpenNewHintText = "입력한 뒤 [저장]을 누르세요.";

    /// <summary>반복 규칙을 고칠 수 없다는 안내 — 열 때 바닥 줄과 반복 칸 아래에 같은 문장을 쓴다.</summary>
    public const string RecurrenceLockedText = "반복 규칙은 수정할 수 없습니다. 바꾸려면 새 스케줄을 만드세요.";

    /// <summary>저장이 진행 중일 때의 한 줄.</summary>
    public const string SavingText = "저장하는 중입니다. 끝날 때까지 기다리세요.";

    /// <summary>
    /// 받아 온 스케줄을 고치러 연다 — 초안은 <b>원본에서</b> 채운다(PATCH 는 RFC 7396).
    /// 미적용 변경이 있으면 <b>갈아 끼우지 않는다</b>.
    /// </summary>
    public bool OpenEdit(EventSuppressionScheduleDto dto)
    {
        if (dto is null) return false;
        if (!TryLeave()) return false;

        Load(SuppressionDraft.FromDto(dto, DeviceName, GroupName));
        // 서버 수정 요청에는 반복 칸이 없다 — 까닭은 운영자 몫이 아니라서 화면에는 할 일만 쓴다.
        StatusLine = _draft.CanEditRecurrence ? string.Empty : RecurrenceLockedText;
        return true;
    }

    private void Load(SuppressionDraft draft)
    {
        _draft = draft;
        _windowStartText = _windowEndText = _dailyStartText = _dailyEndText = null;
        _openedSignature = Signature(draft);

        SyncChips(Tray, draft.Targets);
        RebuildPicker();

        IsOpen = true;
        RaiseAll();
        // ESC 는 터널 이벤트라 초점이 서랍 안에 있어야 도착한다 — 여는 쪽이 초점을 넣어 준다.
        Opened?.Invoke();
    }

    /// <summary>서랍이 방금 열렸다 — 뷰가 첫 칸에 초점을 준다.</summary>
    public event System.Action? Opened;

    /// <summary>
    /// 닫기 시도(✕ · 취소 · ESC · 스크림). 미적용 변경이 있으면 <b>닫지 않고</b> 흔든다.
    /// </summary>
    /// <returns>닫혔으면 true.</returns>
    public bool TryClose()
    {
        if (!TryLeave()) return false;

        Close();
        return true;
    }

    /// <summary>[되돌리기] — 연 시점의 값으로 되돌린다. <b>서버 호출 0</b>.</summary>
    public void Revert()
    {
        var baseline = _draft.Baseline;
        Load(baseline is null
            ? SuppressionDraft.NewSchedule(new DateTimeOffset(_clock.Now))
            : SuppressionDraft.FromDto(baseline, DeviceName, GroupName));
        StatusLine = "되돌렸습니다.";
    }

    /// <summary>묻지 않고 닫는다(저장 성공 · 콘솔을 떠남).</summary>
    public void Close()
    {
        IsOpen = false;
        _draft = SuppressionDraft.NewSchedule(new DateTimeOffset(_clock.Now));
        _openedSignature = Signature(_draft);
        SyncChips(Tray, _draft.Targets);
        StatusLine = string.Empty;
        RaiseAll();
    }

    /// <summary>값이 오를 때마다 바닥 막대가 한 번 흔들린다.</summary>
    public int ShakeToken
    {
        get => _shakeToken;
        private set { _shakeToken = value; NotifyOfPropertyChange(); }
    }

    #endregion

    #region - 폼 (확정 두 칸) -

    /// <summary>작업명 * — 정본 SB L2871.</summary>
    public string Name
    {
        get => _draft.Name;
        set { _draft.Name = value ?? string.Empty; RaiseAll(); }
    }

    /// <summary>대상 유형 * — device / group / all(SB L2872).</summary>
    public string TargetType
    {
        get => _draft.TargetType;
        set
        {
            var next = string.IsNullOrWhiteSpace(value) ? SuppressionTargetDrop.ModeDevice : value;
            if (_draft.TargetType == next) return;
            _draft.TargetType = next;

            // 종류가 바뀌면 남은 칩은 보낼 수 없다 — 조용히 실려 나가지 않게 여기서 뺀다.
            var wanted = SuppressionTargetDrop.KindFor(next);
            var dropped = _draft.Targets.RemoveAll(t => wanted is null || t.Kind != wanted.Value);
            if (dropped > 0)
            {
                SyncChips(Tray, _draft.Targets);
                StatusLine = $"대상 유형을 바꿔 담겨 있던 {dropped}개를 뺐습니다.";
            }

            RebuildPicker();
            RaiseAll();
        }
    }

    public bool IsDeviceMode { get => TargetType == SuppressionTargetDrop.ModeDevice; set { if (value) TargetType = SuppressionTargetDrop.ModeDevice; } }
    public bool IsGroupMode { get => TargetType == SuppressionTargetDrop.ModeGroup; set { if (value) TargetType = SuppressionTargetDrop.ModeGroup; } }
    public bool IsAllMode { get => TargetType == SuppressionTargetDrop.ModeAll; set { if (value) TargetType = SuppressionTargetDrop.ModeAll; } }

    /// <summary>개별 대상을 담는 유형인가 — 트레이 · 픽커 표시 조건.</summary>
    public bool AcceptsTargets => SuppressionTargetDrop.AcceptsTargets(TargetType);

    /// <summary>
    /// 억제 범위 * — <c>connection</c> / <c>detection</c> / <c>malfunction</c> / <c>operation</c> / <c>all</c>.
    /// <para>목업(SB L2872)은 3값이지만 서버는 <b>5값</b>이다(<c>app/utils/enums.py:295-302</c>).</para>
    /// <para>⚠ 콤보가 값을 못 찾으면 WPF 가 <c>null</c> 을 되민다. 그것을 <c>"all"</c> 로 바꾸면
    /// <b>억제 범위가 조용히 넓어진다</b> — 안전 방향의 반대다. 그래서 <b>모르는 값은 보존</b>하고
    /// 저장을 막는다(<see cref="IsScopeUnknown"/>).</para>
    /// </summary>
    public string EventScope
    {
        get => _draft.EventScope;
        set
        {
            // null · 빈 값 = "콤보가 매칭에 실패했다" 는 뜻이다. 원래 값을 그대로 둔다.
            if (string.IsNullOrWhiteSpace(value)) { NotifyOfPropertyChange(); return; }
            _draft.EventScope = value;
            RaiseAll();
        }
    }

    /// <summary>화면이 모르는 억제 범위인가 — 그런 스케줄은 범위를 건드리지 않은 채로도 저장할 수 없다.</summary>
    public bool IsScopeUnknown => !SuppressionRequestBuilder.IsKnownScope(_draft.EventScope);

    /// <summary>모르는 범위 안내. 서버 원문 값은 화면에 싣지 않는다(범위 칸 툴팁 · 로그 몫이다).</summary>
    public string UnknownScopeText => IsScopeUnknown
        ? "지원하지 않는 억제 범위라 수정할 수 없습니다."
        : string.Empty;

    /// <summary>감지/감시 — 그룹 · 전체에서만 뜻이 있다.</summary>
    public string TargetSide
    {
        get => _draft.TargetSide;
        set { _draft.TargetSide = string.IsNullOrWhiteSpace(value) ? "both" : value; RaiseAll(); }
    }

    /// <summary>감지/감시 칸을 낼 것인가.</summary>
    public bool ShowTargetSide => TargetType != SuppressionTargetDrop.ModeDevice;

    /// <summary>반복 — 주간 반복 / 단발(SB L2873).</summary>
    public bool IsWeekly
    {
        get => _draft.IsWeekly;
        set
        {
            if (_draft.IsWeekly == value) return;
            if (!_draft.CanEditRecurrence) return;      // 수정에서는 잠긴다(서버 스키마에 반복 칸이 없다)

            _draft.IsWeekly = value;
            if (value)
            {
                if (!SuppressionRules.HasAnyDay(_draft.DaysOfWeekMask))
                    _draft.DaysOfWeekMask = SuppressionRules.DaysWeekdayPreset;

                // 단발 기본 1시간을 그대로 두면 어떤 요일도 그 안에 없어 '영원히 발동하지 않는 창'이 된다.
                var end = _draft.WindowEnd ?? _draft.WindowStart;
                if ((end - _draft.WindowStart).TotalDays < DefaultWeeklySpanDays)
                    _draft.WindowEnd = _draft.WindowStart.AddDays(DefaultWeeklySpanDays);
            }
            else
            {
                // 단발에 무제한은 없다 — 되돌아오면서 반드시 끝을 만든다.
                _draft.WindowEnd ??= _draft.WindowStart.AddHours(1);
            }
            RaiseAll();
        }
    }

    public bool IsOneShot { get => !IsWeekly; set { if (value) IsWeekly = false; } }

    /// <summary>반복 칸을 고칠 수 있는가 — 새 스케줄일 때만.</summary>
    public bool CanEditRecurrence => _draft.CanEditRecurrence;

    /// <summary>반복 잠금 안내 — 왜 못 고치는지.</summary>
    public string RecurrenceLockText => CanEditRecurrence
        ? string.Empty
        : RecurrenceLockedText;

    /// <summary>
    /// 유효기간 시작 * — <b>글자로 받는다</b>(<c>yyyy-MM-dd HH:mm</c>).
    /// </summary>
    /// <remarks>
    /// MahApps 피커를 쓰지 않는 이유가 둘 있다. ① 다크에서 <b>흰 카드에 검은 글자</b>로 떠 테마를 따르지 않았고
    /// (측정: 값 대비 1.40:1) ② 머신 로캘 표기(<c>9/20/2026 2:30:00 PM</c>)가 바로 아래 요약 줄 · 목록 열과
    /// 달라, "유효기간 (KST)" 라고 적힌 폼 안에서 시각이 세 얼굴을 했다. 표기는 하나여야 한다.
    /// <para>읽을 수 없는 글자는 <b>버리지 않는다</b> — 그대로 두고 저장을 막는다(고쳐 쓸 수 있어야 한다).</para>
    /// </remarks>
    public string WindowStartText
    {
        get => _windowStartText ?? SuppressionTimeText.Format(_draft.WindowStart);
        set
        {
            _windowStartText = value;
            if (SuppressionTimeText.TryParseDateTime(value, out var parsed))
            {
                _windowStartText = null;                 // 읽혔으면 초안이 정본이다
                _draft.WindowStart = WithOffset(parsed, _draft.WindowStart.Offset);
            }
            RaiseAll();
        }
    }

    /// <summary>유효기간 끝. 무제한이면 의미가 없다.</summary>
    public string WindowEndText
    {
        get => _windowEndText
               ?? (_draft.WindowEnd is null && !_draft.IsWeekly
                   ? string.Empty
                   : SuppressionTimeText.Format(_draft.WindowEnd ?? _draft.WindowStart.AddHours(1)));
        set
        {
            // 단발에서 끝 칸을 비웠다 = "종료 없음" 이다. 예전엔 '읽을 수 없는 글자'로만 쳐서 초안에 옛 끝 시각이 남았고,
            // 검증이 그 옛 값으로 "종료가 시작보다 뒤여야 합니다." 를 말했다 — 운영자는 끝을 지웠는데 엉뚱한 까닭을 들었다
            // (2026-09-28 헤디드 SC-SUP-007). 비우면 끝을 없애고, 규칙이 "단발 억제에는 종료 시각이 있어야 합니다." 라고 말한다.
            // 주간 반복의 '끝 없음' 은 [기간 제한 없음] 이 맡는다 — 거기서 빈 칸은 예전처럼 읽을 수 없는 글자로 둔다.
            if (string.IsNullOrWhiteSpace(value) && !_draft.IsWeekly)
            {
                _windowEndText = null;
                _draft.WindowEnd = null;
                RaiseAll();
                return;
            }
            _windowEndText = value;
            if (SuppressionTimeText.TryParseDateTime(value, out var parsed))
            {
                _windowEndText = null;
                var offset = _draft.WindowEnd?.Offset ?? _draft.WindowStart.Offset;
                _draft.WindowEnd = WithOffset(parsed, offset);
            }
            RaiseAll();
        }
    }

    /// <summary>읽을 수 없는 시각 글자가 남아 있는가 — 그대로 저장하면 엉뚱한 시각이 나간다.</summary>
    public bool HasUnreadableTime
        => _windowStartText is not null || _windowEndText is not null
           || _dailyStartText is not null || _dailyEndText is not null;

    /// <summary>어느 칸이 읽히지 않았는지.</summary>
    public string UnreadableTimeText => HasUnreadableTime
        ? $"시각을 읽을 수 없습니다. {SuppressionTimeText.DateTimeHint} 형식으로 적으세요."
        : string.Empty;

    private string? _windowStartText;
    private string? _windowEndText;
    private string? _dailyStartText;
    private string? _dailyEndText;

    /// <summary>"기간 제한 없음" — <b>주간 반복에서만</b> 켤 수 있다.</summary>
    /// <remarks>
    /// 단발에서 끝 칸을 비우면 초안의 끝이 없어지지만(검증이 "종료 시각이 있어야 합니다" 라고 말하게) 그것은 '기간 제한 없음' 이 아니다 —
    /// 여기서 참으로 보이면 끝 칸이 접히고 자리표시가 떠 운영자가 끝을 다시 적을 수 없다. 그래서 주간 반복일 때만 참이다.
    /// </remarks>
    public bool IsUnlimited
    {
        get => _draft.IsUnlimited && _draft.IsWeekly;
        set
        {
            if (value == IsUnlimited) return;
            if (value && !_draft.IsWeekly) return;       // 단발 + 무제한은 서버가 422 로 막는다
            _draft.WindowEnd = value ? null : _draft.WindowStart.AddDays(DefaultWeeklySpanDays);
            RaiseAll();
        }
    }

    /// <summary>무제한 체크를 켤 수 있는가.</summary>
    public bool CanUnlimited => _draft.IsWeekly;

    /// <summary>요일 * — 월1 … 일64(서버 원점 월=0).</summary>
    public int DaysOfWeekMask
    {
        get => _draft.DaysOfWeekMask;
        set { if (_draft.DaysOfWeekMask == value) return; _draft.DaysOfWeekMask = value; RaiseAll(); }
    }

    public bool IsMonChecked { get => Day(0); set => SetDay(0, value); }
    public bool IsTueChecked { get => Day(1); set => SetDay(1, value); }
    public bool IsWedChecked { get => Day(2); set => SetDay(2, value); }
    public bool IsThuChecked { get => Day(3); set => SetDay(3, value); }
    public bool IsFriChecked { get => Day(4); set => SetDay(4, value); }
    public bool IsSatChecked { get => Day(5); set => SetDay(5, value); }
    public bool IsSunChecked { get => Day(6); set => SetDay(6, value); }

    /// <summary>일일 시작 * — 벽시계 <c>HH:mm</c>(offset 금지).</summary>
    public string DailyStartText
    {
        get => _dailyStartText ?? SuppressionTimeText.Format(_draft.DailyStart);
        set
        {
            _dailyStartText = value;
            if (SuppressionTimeText.TryParseTime(value, out var parsed)) { _dailyStartText = null; _draft.DailyStart = parsed; }
            RaiseAll();
        }
    }

    /// <summary>일일 끝 * — 시작보다 이르면 자정 넘김.</summary>
    public string DailyEndText
    {
        get => _dailyEndText ?? SuppressionTimeText.Format(_draft.DailyEnd);
        set
        {
            _dailyEndText = value;
            if (SuppressionTimeText.TryParseTime(value, out var parsed)) { _dailyEndText = null; _draft.DailyEnd = parsed; }
            RaiseAll();
        }
    }

    /// <summary>요약 한 줄 — 전송될 내용을 그대로 옮긴다(SB L2877).</summary>
    public string RecapText => SuppressionFormRules.Recap(_draft);

    #endregion

    #region - 대상 칩 트레이 -

    /// <summary>담긴 대상 칩.</summary>
    public ObservableCollection<SuppressionTargetChip> Tray { get; }

    /// <summary>픽커(끌어 오는 곳) — 대상 유형에 맞는 장비 또는 그룹.</summary>
    public ObservableCollection<SuppressionTargetChip> PickerItems { get; }

    /// <summary>드롭 판정 · 처리기. 드래그와 [추가 ▶] 가 이것 하나를 쓴다.</summary>
    public SuppressionTargetTrayHandler Drop { get; }

    /// <summary>픽커 검색어.</summary>
    public string PickerSearch
    {
        get => _pickerSearch;
        set { _pickerSearch = value ?? string.Empty; NotifyOfPropertyChange(); RebuildPicker(); }
    }

    /// <summary>트레이 머리 — 담긴 수.</summary>
    public string TrayCountText => $"{Tray.Count} / {SuppressionTargetDrop.MaxTargets}";

    /// <summary>트레이가 비었는가(안내 문구 조건).</summary>
    public bool IsTrayEmpty => Tray.Count == 0;

    /// <summary>드래그의 키보드 · 버튼 폴백 — 드롭과 <b>같은 함수</b>를 부른다.</summary>
    public void AddSelected(IEnumerable<object>? rows) => Drop.Add(rows);

    /// <summary>칩 하나 빼기(✕ · Delete 키).</summary>
    public void RemoveChip(SuppressionTargetChip? chip)
    {
        if (chip is null) return;
        if (!_draft.Targets.Remove(chip)) return;
        Tray.Remove(chip);
        StatusLine = $"{chip.Label} 을(를) 뺐습니다.";
        RebuildPicker();                            // 뺀 대상은 다시 '담을 수 있는 대상' 으로 돌아온다
        RaiseAll();
    }

    /// <summary>전부 빼기.</summary>
    public void ClearChips()
    {
        if (_draft.Targets.Count == 0) return;
        var n = _draft.Targets.Count;
        _draft.Targets.Clear();
        SyncChips(Tray, _draft.Targets);
        StatusLine = $"대상 {n}개를 모두 뺐습니다.";
        RebuildPicker();
        RaiseAll();
    }

    private void AcceptPlan(SuppressionTargetPlan plan)
    {
        foreach (var chip in plan.Accepted) _draft.Targets.Add(chip);
        SyncChips(Tray, _draft.Targets);
        RebuildPicker();                            // 담은 대상은 '담을 수 있는 대상' 에서 빠진다
        RaiseAll();
    }

    private void RebuildPicker()
    {
        var wanted = SuppressionTargetDrop.KindFor(TargetType);
        var want = new List<SuppressionTargetChip>();

        if (wanted == SuppressionTargetKind.Device && _devices is not null)
            want.AddRange(_devices.CollectionEntity
                .Where(d => d is not null)
                .Select(d => SuppressionTargetCandidateFactory.From(d))
                .Where(c => c is not null)
                .Select(c => c!));
        else if (wanted == SuppressionTargetKind.Group && _groups is not null)
            want.AddRange(_groups.CollectionEntity
                .Where(g => g is not null)
                .Select(g => SuppressionTargetCandidateFactory.From(g))
                .Where(c => c is not null)
                .Select(c => c!));

        // 이미 담은 대상은 '담을 수 있는 대상' 에 남기지 않는다 — 트레이와 목록 양쪽에 같은 이름이 떠
        // 담긴 건지 아닌지 헷갈렸다(실창 검토 #26). 판정은 칩 기록 전체가 아니라 키(종류 + id)로 한다:
        // 수정으로 연 초안의 칩은 이름을 서버 원본에서 풀어 와 부가 설명이 후보와 다를 수 있다.
        var taken = new HashSet<string>(_draft.Targets.Select(t => t.Key), StringComparer.Ordinal);
        want = want.Where(c => !taken.Contains(c.Key)).ToList();

        if (!string.IsNullOrWhiteSpace(_pickerSearch))
        {
            var needle = _pickerSearch.Trim();
            want = want.Where(c => c.Label.Contains(needle, StringComparison.OrdinalIgnoreCase)
                                || c.Detail.Contains(needle, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        SyncChips(PickerItems, want);
        NotifyOfPropertyChange(nameof(PickerCountText));
    }

    public string PickerCountText => $"{PickerItems.Count}개";

    #endregion

    #region - 검증 · 저장 -

    /// <summary>지금 폼의 판정.</summary>
    public SuppressionFormVerdict Verdict => SuppressionFormRules.Validate(_draft, new DateTimeOffset(_clock.Now), _others());

    /// <summary>폼 아래 붉은 한 줄.</summary>
    public string ErrorText => Verdict.FirstErrorText;

    public bool HasError => !string.IsNullOrEmpty(ErrorText);

    /// <summary>막지 않는 경고(중복 창 · 자정 넘김 …).</summary>
    public string WarningText => string.Join("\n", Verdict.Warnings);

    public bool HasWarning => Verdict.Warnings.Count > 0;

    /// <summary>연 시점과 달라졌는가.</summary>
    public bool IsDirty => Signature(_draft) != _openedSignature;

    /// <summary>[저장] 을 켤 것인가.</summary>
    public bool CanSave => _canEdit() && !_isSaving && !IsScopeUnknown && !HasUnreadableTime && Verdict.CanSave && (IsDirty || IsNew);

    /// <summary>[되돌리기] 를 켤 것인가.</summary>
    public bool CanRevert => !_isSaving && IsDirty;

    /// <summary>저장 중인가.</summary>
    public bool IsSaving
    {
        get => _isSaving;
        private set { _isSaving = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanSave)); NotifyOfPropertyChange(nameof(CanRevert)); }
    }

    /// <summary>서랍 아래 알림 한 줄. 거절도 반드시 여기에 남는다(조용한 실패 금지).</summary>
    public string StatusLine
    {
        get => _statusLine;
        set { _statusLine = value ?? string.Empty; NotifyOfPropertyChange(); }
    }

    /// <summary>저장에 성공했다 — 콘솔이 목록을 다시 부른다.</summary>
    public event Action<EventSuppressionScheduleDto?>? Saved;

    /// <summary>
    /// [저장] — 서버를 <b>정확히 한 번</b> 부른다(새 것이면 POST, 고친 것이면 PATCH).
    /// </summary>
    public async Task SaveAsync(CancellationToken token = default)
    {
        // ⚠ 순서가 중요하다 — CanSave 가 !_isSaving 을 품고 있어, 보내는 중에 또 누르면
        //   "바뀐 것이 없습니다" 라는 거짓말이 뜬다(두 번 보내지는 않는다).
        if (IsSaving) { StatusLine = SavingText; return; }

        if (!CanSave)
        {
            StatusLine = !_canEdit()
                ? SuppressionPermissionText.EditDenied
                : IsScopeUnknown ? UnknownScopeText
                : HasUnreadableTime ? UnreadableTimeText
                : Verdict.CanSave ? "바뀐 것이 없습니다." : Verdict.FirstErrorText;
            ShakeToken++;
            return;
        }

        IsSaving = true;
        StatusLine = "저장하는 중…";

        SuppressionSaveOutcome outcome;
        try
        {
            outcome = await _save(_draft, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Execute.OnUIThread(() => { IsSaving = false; StatusLine = "저장을 멈췄습니다."; });
            return;
        }
        catch (Exception ex)
        {
            // 예외 본문은 로그로만 — HttpRequestException 은 서버 호스트 · 포트를 문장에 담는다.
            _onError?.Invoke("억제 스케줄 저장", ex);
            Execute.OnUIThread(() =>
            {
                IsSaving = false;
                StatusLine = "저장하지 못했습니다. 잠시 뒤 다시 시도하세요.";
                ShakeToken++;
            });
            return;
        }

        // ⚠ 완료가 작업 스레드로 올 수 있다 — 화면 상태는 UI 스레드에서만 만진다.
        Execute.OnUIThread(() =>
        {
            IsSaving = false;
            StatusLine = outcome.Message;
            if (!outcome.Ok) { ShakeToken++; RaiseAll(); return; }

            Saved?.Invoke(outcome.Saved);
            Close();
        });
    }

    #endregion

    #region - Helpers -

    /// <summary>주간 반복으로 바꿀 때 넓히는 기본 유효기간(일) — 어떤 요일 조합이든 반드시 한 번은 들어간다.</summary>
    private const int DefaultWeeklySpanDays = 30;

    /// <summary>지금 초안(테스트 · 저장 경로가 읽는다).</summary>
    public SuppressionDraft Draft => _draft;

    /// <summary>
    /// 초안의 지문 — 미적용 변경 판정에 쓴다. <b>순수 함수</b>라 화면 없이 시험할 수 있다.
    /// </summary>
    public static string Signature(SuppressionDraft draft)
    {
        if (draft is null) return string.Empty;
        var targets = string.Join(",", draft.Targets.Select(t => t.Key).OrderBy(k => k, StringComparer.Ordinal));
        return string.Join("|",
            draft.Id?.ToString() ?? "new",
            draft.Name?.Trim() ?? string.Empty,
            draft.Description ?? string.Empty,
            draft.TargetType,
            draft.TargetSide,
            draft.EventScope,
            draft.WindowStart.ToString("O"),
            draft.WindowEnd?.ToString("O") ?? "unlimited",
            draft.IsWeekly ? "weekly" : "none",
            draft.DaysOfWeekMask.ToString(),
            draft.DailyStart.ToString(),
            draft.DailyEnd.ToString(),
            targets);
    }

    /// <summary>
    /// 바인딩된 컬렉션을 <b>지우고 다시 채우지 않고</b> 맞춘다 — Clear()+Add() 는 선택 · 스크롤 · 가상화를 깬다.
    /// </summary>
    internal static void SyncChips(ObservableCollection<SuppressionTargetChip> live, IReadOnlyList<SuppressionTargetChip> want)
    {
        for (var i = live.Count - 1; i >= 0; i--)
            if (!want.Contains(live[i])) live.RemoveAt(i);

        for (var i = 0; i < want.Count; i++)
        {
            var wanted = want[i];
            if (i < live.Count && Equals(live[i], wanted)) continue;

            var at = live.IndexOf(wanted);
            if (at >= 0) live.Move(at, i);
            else live.Insert(i, wanted);
        }

        while (live.Count > want.Count) live.RemoveAt(live.Count - 1);
    }

    private static DateTimeOffset WithOffset(DateTime value, TimeSpan offset)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), offset);

    private bool Day(int index) => (_draft.DaysOfWeekMask & SuppressionRules.BitOf(index)) != 0;

    private void SetDay(int index, bool on)
    {
        if (!_draft.CanEditRecurrence) return;
        var bit = SuppressionRules.BitOf(index);
        var next = on ? _draft.DaysOfWeekMask | bit : _draft.DaysOfWeekMask & ~bit;
        if (next == _draft.DaysOfWeekMask) return;
        _draft.DaysOfWeekMask = next;
        RaiseAll();
    }

    private string DeviceName(int id)
        => _devices?.CollectionEntity.FirstOrDefault(d => d.Id == id)?.DeviceName is { Length: > 0 } name ? name : $"#{id}";

    private string GroupName(int id)
        => _groups?.CollectionEntity.FirstOrDefault(g => g.Id == id)?.Name is { Length: > 0 } name ? name : $"#{id}";

    private void RaiseAll()
    {
        NotifyOfPropertyChange(string.Empty);
        NotifyOfPropertyChange(nameof(TrayCountText));
        NotifyOfPropertyChange(nameof(IsTrayEmpty));
    }

    #endregion
}
