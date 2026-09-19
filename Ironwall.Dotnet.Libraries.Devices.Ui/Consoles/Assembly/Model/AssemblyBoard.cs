using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;

/// <summary>
/// 조립 보드의 순수 모델(FR-02 · FR-05 · FR-08 · FR-16) — 화면도 서버도 모른다.
/// </summary>
/// <remarks>
/// 판정(끼울 자리 · key 제안 · 중복 · 펼치기 · 미저장 계산 · 차이 계산)은 전부 여기에 있고 헤드리스로 검증된다(NFR-02).
/// <b>순서는 보기용이다</b> — 서버 <c>components[]</c> 에 순서 계약이 없어 정렬만 바꾼 것은 미저장 변경으로 세지 않는다(AS L295).
/// UI 스레드 전용 — 잠금이 없다.
/// </remarks>
public sealed class AssemblyBoard
{
    private readonly Func<string, ComponentTypeInfo?> _typeLookup;
    private readonly Stack<List<SlotState>> _undo = new();

    private List<ComponentDefinitionModel> _baseline = new();
    private JObject? _baselineOverrides;
    private int _suspend;
    private bool _validating;

    public AssemblyBoard(EnumDeviceCategory category, Func<string, ComponentTypeInfo?> typeLookup)
    {
        Category = category;
        _typeLookup = typeLookup ?? throw new ArgumentNullException(nameof(typeLookup));
    }

    public EnumDeviceCategory Category { get; }

    /// <summary>화면이 바인딩한다. <b>보드만</b> 고친다.</summary>
    public ObservableCollection<AssemblySlot> Slots { get; } = new();

    /// <summary>바뀔 때마다(슬롯 칸 편집 포함) 한 번.</summary>
    public event EventHandler? Changed;

    #region 불러오기 · 바꿔치기

    /// <summary>서버 · 프리셋의 내용으로 전부 갈고 그것을 <b>baseline</b> 으로 삼는다. 되돌리기 스택은 비운다.</summary>
    public void Load(IEnumerable<ComponentDefinitionModel> components, JObject? componentOverrides)
    {
        _undo.Clear();
        Fill(components, componentOverrides);

        // baseline 은 슬롯을 거쳐 나온 선언으로 잡는다 — 그래야 막 불러온 보드가 "고쳐진 것 없음" 이 된다
        // (유형 코드 대문자화 · InService 의 null→true 같은 정규화가 차이로 새지 않는다).
        _baseline = Slots.Select(s => s.ToDefinition()).ToList();
        _baselineOverrides = CurrentOverrides();

        Raise();
    }

    /// <summary>
    /// <b>지금 보드를 새 baseline 으로 삼는다</b> — 저장이 끝난 자리가 새 원점이다(FR-16).
    /// </summary>
    /// <remarks>
    /// <para><see cref="Load"/> 와 다르다 — 슬롯을 <b>다시 만들지 않는다</b>. 저장 직후에 보드를 다시 채우면
    /// 화면의 선택 · 편집 중이던 칸 · 스크롤이 전부 날아가고, 방금 저장한 사람이 "내가 고르던 게 사라졌다"를 본다.
    /// 여기서는 <see cref="Slots"/> 의 <b>같은 객체들이 같은 차례로</b> 그대로 남는다.</para>
    /// <para>되돌리기 스택은 <b>비운다</b> — 저장된 자리가 원점이면 그 이전으로 되돌릴 자리가 없다.
    /// 스택을 남겨 두면 Undo 한 번이 <b>서버에 이미 있는 것과 다른 상태</b>를 "안 고침"인 양 보여 준다.</para>
    /// <para>이후 <see cref="IsDirty"/> 는 <c>false</c> · <see cref="RemovedKeys"/> 는 비고,
    /// 다음 편집부터는 <b>새 baseline</b> 과 견준다.</para>
    /// </remarks>
    public void MarkBaseline()
    {
        _undo.Clear();

        // Load 와 같은 방식으로 슬롯을 거쳐 나온 선언을 잡는다 — 정규화 차이가 미저장 변경으로 새지 않게.
        _baseline = Slots.Select(s => s.ToDefinition()).ToList();
        _baselineOverrides = CurrentOverrides();

        // 미저장 개수 · 되돌리기 가능 여부가 같이 바뀌었으니 화면에 알린다.
        Raise();
    }

    /// <summary>프리셋 내용으로 통째 바꾼다(되돌리기 1칸). baseline 은 그대로 — 서버에 있는 것은 그대로이기 때문이다.</summary>
    public void ReplaceAll(IEnumerable<ComponentDefinitionModel> components, JObject? componentOverrides)
    {
        PushUndo();
        Fill(components, componentOverrides);
        Raise();
    }

    private void Fill(IEnumerable<ComponentDefinitionModel>? components, JObject? componentOverrides)
    {
        _suspend++;
        try
        {
            foreach (var slot in Slots.ToList()) Detach(slot);
            Slots.Clear();

            foreach (var component in components ?? Enumerable.Empty<ComponentDefinitionModel>())
            {
                if (component is null) continue;
                var overrides = componentOverrides is not null
                                && componentOverrides.TryGetValue(component.Key ?? string.Empty, out var token)
                                && token is JObject o
                    ? o
                    : null;

                var slot = AssemblySlot.FromDefinition(component, overrides);
                Slots.Add(slot);
                Attach(slot);
            }

            Validate();
        }
        finally
        {
            _suspend--;
        }
    }

    #endregion

    #region 더하기 · 펼치기

    /// <summary>그 자리에 한 개 끼운다. 카탈로그에 없거나 이 카테고리에 달 수 없는 유형이면 <b>아무것도 하지 않고</b> null.</summary>
    public AssemblySlot? Add(string typeCode, int index = -1)
    {
        var info = Lookup(typeCode);
        if (info is null || !info.AppliesToCategory(Category)) return null;

        var code = (info.Code ?? typeCode).Trim().ToUpperInvariant();
        var slot = new AssemblySlot(code, AssemblyKeyRules.Suggest(code, Slots.Select(s => s.Key)));

        PushUndo();
        var at = index < 0 || index > Slots.Count ? Slots.Count : index;

        _suspend++;
        try
        {
            Slots.Insert(at, slot);
            Attach(slot);
            Validate();
        }
        finally
        {
            _suspend--;
        }

        Raise();
        return slot;
    }

    /// <summary>규칙대로 여럿을 끝에 펼친다. 규칙이 틀렸거나 <b>한 줄이라도</b> 충돌하면 아무것도 하지 않는다(전부 아니면 전무).</summary>
    public IReadOnlyList<AssemblySlot> Expand(RepeatExpandSpec spec)
    {
        var nothing = Array.Empty<AssemblySlot>();
        if (spec is null || RepeatExpand.Validate(spec) is not null) return nothing;

        var info = Lookup(spec.TypeCode);
        if (info is null || !info.AppliesToCategory(Category)) return nothing;

        var rows = RepeatExpand.Preview(spec, Slots.Select(s => s.Key));
        if (rows.Count == 0 || rows.Any(r => r.IsConflict)) return nothing;

        var code = (info.Code ?? spec.TypeCode).Trim().ToUpperInvariant();
        var made = new List<AssemblySlot>(rows.Count);

        PushUndo();
        _suspend++;
        try
        {
            foreach (var row in rows)
            {
                var slot = new AssemblySlot(code, row.Key) { Channel = row.Channel };
                Slots.Add(slot);
                Attach(slot);
                made.Add(slot);
            }

            Validate();
        }
        finally
        {
            _suspend--;
        }

        Raise();
        return made;
    }

    #endregion

    #region 옮기기 · 빼기 · 되돌리기

    /// <summary>
    /// 고른 것들을(지금 순서 그대로) <paramref name="insertionIndex"/> 틈(지금 목록 기준 0..Count)부터 <b>붙여서</b> 놓는다.
    /// 자기 블록 안 틈에 놓는 것은 제자리라 false — 끌어 놓기와 같은 셈법이다.
    /// </summary>
    public bool Move(IReadOnlyList<AssemblySlot> slots, int insertionIndex)
    {
        if (slots is null || slots.Count == 0) return false;

        var moving = slots.Where(s => s is not null)
                          .Distinct()
                          .Select(s => (Slot: s, At: Slots.IndexOf(s)))
                          .Where(x => x.At >= 0)
                          .OrderBy(x => x.At)
                          .Select(x => x.Slot)
                          .ToList();
        if (moving.Count == 0) return false;

        var at = Math.Clamp(insertionIndex, 0, Slots.Count);
        var before = Slots.Take(at).Count(moving.Contains);

        var rest = Slots.Where(s => !moving.Contains(s)).ToList();
        var target = Math.Clamp(at - before, 0, rest.Count);
        rest.InsertRange(target, moving);

        if (rest.SequenceEqual(Slots)) return false;

        PushUndo();
        _suspend++;
        try
        {
            Reorder(rest);
            Validate();
        }
        finally
        {
            _suspend--;
        }

        Raise();
        return true;
    }

    /// <summary>뺀다 — 되돌리기는 <b>한 칸</b>(뺀 것들 + 그 자리).</summary>
    public void Remove(IReadOnlyList<AssemblySlot> slots)
    {
        if (slots is null || slots.Count == 0) return;

        var targets = slots.Where(s => s is not null && Slots.Contains(s)).Distinct().ToList();
        if (targets.Count == 0) return;

        PushUndo();
        _suspend++;
        try
        {
            foreach (var slot in targets.OrderByDescending(Slots.IndexOf))
            {
                Detach(slot);
                Slots.Remove(slot);
            }

            Validate();
        }
        finally
        {
            _suspend--;
        }

        Raise();
    }

    public bool CanUndo => _undo.Count > 0;

    /// <summary>한 번에 한 칸. 순서도 객체도 그대로 돌아온다.</summary>
    public void Undo()
    {
        if (_undo.Count == 0) return;

        var snapshot = _undo.Pop();
        _suspend++;
        try
        {
            foreach (var slot in Slots.ToList()) Detach(slot);
            Slots.Clear();

            foreach (var state in snapshot)
            {
                state.Slot.Channel = state.Channel;
                Slots.Add(state.Slot);
                Attach(state.Slot);
            }

            Validate();
        }
        finally
        {
            _suspend--;
        }

        Raise();
    }

    /// <summary>보이는 순서대로 채널을 다시 붙인다 — <b>이미 채널이 있는</b> 부품만.</summary>
    public void RenumberChannels(int start = 1)
    {
        var numbered = Slots.Where(s => s.Channel.HasValue).ToList();
        if (numbered.Count == 0) return;

        PushUndo();
        _suspend++;
        try
        {
            var channel = start;
            foreach (var slot in numbered) slot.Channel = channel++;
        }
        finally
        {
            _suspend--;
        }

        Raise();
    }

    #endregion

    #region 판정

    public bool HasErrors => Slots.Any(s => s.HasKeyError || !TypeIsUsable(s.TypeCode));

    /// <summary>사람이 읽을 문제 목록. 비어 있으면 보낼 수 있다.</summary>
    public IReadOnlyList<string> Problems
    {
        get
        {
            var problems = new List<string>();

            foreach (var slot in Slots)
            {
                var format = AssemblyKeyRules.ValidateFormat(slot.Key);
                if (format is null) continue;
                problems.Add(string.IsNullOrWhiteSpace(slot.Key) ? format : $"key '{slot.Key}' 는 쓸 수 없다 — {format}");
            }

            foreach (var group in Slots.Where(s => AssemblyKeyRules.ValidateFormat(s.Key) is null)
                                       .GroupBy(s => s.Key, StringComparer.Ordinal)
                                       .Where(g => g.Count() > 1))
            {
                problems.Add(DuplicateMessage(group.Key, group.Count()));
            }

            foreach (var code in Slots.Select(s => s.TypeCode).Distinct(StringComparer.Ordinal))
            {
                var info = Lookup(code);
                if (info is null) problems.Add($"'{code}' 는 카탈로그에 없는 유형이다");
                else if (!info.AppliesToCategory(Category)) problems.Add($"'{code}' 은 이 카테고리에 달 수 없다");
            }

            return problems;
        }
    }

    /// <summary>baseline 과 다른 개수 — 더한 것 + 뺀 것 + 고친 것. <b>정렬만 바꾼 것은 0 이다.</b></summary>
    public int UnsavedChangeCount
    {
        get
        {
            var diff = Diff();
            return diff.Added.Count + diff.Removed.Count + diff.Changed.Count;
        }
    }

    public bool IsDirty => UnsavedChangeCount > 0;

    public AssemblyDiff Diff() => AssemblyDiff.Compute(_baseline, _baselineOverrides, ToDefinitions(), CurrentOverrides());

    #endregion

    #region 내보내기

    public IReadOnlyList<ComponentDefinitionModel> ToDefinitions() => Slots.Select(s => s.ToDefinition()).ToList();

    /// <summary>
    /// 보낼 <c>component_overrides</c>. 보드의 재정의에 더해, <b>빠진 baseline key 에는 JSON null 을 명시</b>한다 —
    /// 안 보내면 서버가 주인 없는 재정의를 그대로 들고 있는다.
    /// </summary>
    public JObject? ToOverrides()
    {
        var result = CurrentOverrides() ?? new JObject();

        foreach (var key in RemovedKeys)
        {
            if (!result.ContainsKey(key)) result[key] = JValue.CreateNull();
        }

        return result.HasValues ? result : null;
    }

    /// <summary>baseline 에 있었는데 지금은 없는 key.</summary>
    public IReadOnlyList<string> RemovedKeys
    {
        get
        {
            var now = new HashSet<string>(Slots.Select(s => s.Key), StringComparer.Ordinal);
            var gone = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var component in _baseline)
            {
                var key = component.Key ?? string.Empty;
                if (now.Contains(key) || !seen.Add(key)) continue;
                gone.Add(key);
            }

            return gone;
        }
    }

    #endregion

    #region 속

    /// <summary>지금 보드의 재정의만 — 빠진 key 의 null 은 넣지 않는다(차이 계산용).</summary>
    private JObject? CurrentOverrides()
    {
        JObject? result = null;
        foreach (var slot in Slots)
        {
            if (slot.Overrides is null) continue;
            result ??= new JObject();
            result[slot.Key] = slot.Overrides.DeepClone();
        }
        return result;
    }

    private ComponentTypeInfo? Lookup(string? typeCode)
    {
        var code = (typeCode ?? string.Empty).Trim().ToUpperInvariant();
        return code.Length == 0 ? null : _typeLookup(code);
    }

    private bool TypeIsUsable(string typeCode)
    {
        var info = Lookup(typeCode);
        return info is not null && info.AppliesToCategory(Category);
    }

    /// <summary>형식이 먼저, 그 다음 중복. 같은 key 를 쓰는 슬롯은 <b>모두</b> 표시한다.</summary>
    private void Validate()
    {
        _validating = true;
        try
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var slot in Slots)
            {
                counts.TryGetValue(slot.Key, out var n);
                counts[slot.Key] = n + 1;
            }

            foreach (var slot in Slots)
            {
                var format = AssemblyKeyRules.ValidateFormat(slot.Key);
                slot.KeyError = format ?? (counts[slot.Key] > 1 ? DuplicateMessage(slot.Key, counts[slot.Key]) : null);
            }
        }
        finally
        {
            _validating = false;
        }
    }

    private static string DuplicateMessage(string key, int count)
    {
        var times = count switch { 2 => "두 번", 3 => "세 번", _ => $"{count}번" };
        return $"key '{key}' 가 {times} 쓰였다";
    }

    private void Attach(AssemblySlot slot) => slot.PropertyChanged += OnSlotPropertyChanged;

    private void Detach(AssemblySlot slot) => slot.PropertyChanged -= OnSlotPropertyChanged;

    private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_validating) return;
        if (e.PropertyName == nameof(AssemblySlot.Key)) Validate();
        Raise();
    }

    private void PushUndo() => _undo.Push(Slots.Select(s => new SlotState(s, s.Channel)).ToList());

    /// <summary>같은 객체를 그대로 두고 자리만 옮긴다 — 화면의 선택이 날아가지 않는다.</summary>
    private void Reorder(IReadOnlyList<AssemblySlot> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            var at = Slots.IndexOf(ordered[i]);
            if (at != i) Slots.Move(at, i);
        }
    }

    private void Raise()
    {
        if (_suspend > 0 || _validating) return;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>되돌리기 한 칸 — 자리 + 채널(다시 번호 붙이기도 이것으로 되돌아온다).</summary>
    private readonly record struct SlotState(AssemblySlot Slot, int? Channel);

    #endregion
}
