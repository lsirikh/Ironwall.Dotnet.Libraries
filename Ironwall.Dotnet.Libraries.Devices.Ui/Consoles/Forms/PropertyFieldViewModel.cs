using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Properties;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Forms;

/// <summary>
/// 속성 폼의 칸 하나 — 명세(<see cref="DevicePropertySpec"/>) 한 줄이 칸 하나가 된다.
/// </summary>
/// <remarks>
/// 칸은 <b>행을 직접 고치지 않는다.</b> 입력은 칸 안에만 머물고, [적용] · [등록] 때 폼이 손댄 칸만 골라 행에 쓴다.
/// 그래서 [되돌리기] 는 칸을 원래 글로 돌려놓는 것으로 끝난다(행을 복원할 일이 없다).
/// </remarks>
public sealed class PropertyFieldViewModel : PropertyChangedBase
{
    public const string MultiIdentityReason = "여러 대를 골랐을 때는 바꿀 수 없다 — 장비마다 달라야 하는 값이다";
    public const string ReadOnlyReason = "편집 권한이 없다";
    public const string CreateOnlyReason = "등록할 때만 정할 수 있다";

    private readonly DirtyFieldTracker _tracker;
    private string _originalText = string.Empty;
    private string _text = string.Empty;
    private PropertyOption? _selectedOption;
    private string? _error;
    private bool _isLoading;

    public PropertyFieldViewModel(DevicePropertySpec spec, DirtyFieldTracker tracker)
    {
        Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public DevicePropertySpec Spec { get; }
    public string Key => Spec.Key;
    public string Label => Spec.Label;
    public string ApiPath => Spec.ApiPath;
    public string? Note => Spec.Note;

    /// <summary>고른 행들의 값이 서로 다르다.</summary>
    public bool IsMixed { get; private set; }

    /// <summary>이 칸의 축을 서버가 보내지 않았다(값이 없는 것과 다르다).</summary>
    public bool IsNotReceived { get; private set; }

    public bool IsLocked { get; private set; }

    /// <summary>잠긴 까닭 — 잠긴 칸은 까닭 없이 보이지 않는다.</summary>
    public string? LockReason { get; private set; }

    public bool IsRequired { get; private set; }

    public IReadOnlyList<PropertyOption> Options { get; private set; } = Array.Empty<PropertyOption>();

    /// <summary>화면이 고를 편집기 — 잠겼으면 무엇이든 읽기 전용으로 그린다.</summary>
    public DevicePropertyEditor EffectiveEditor => IsLocked ? DevicePropertyEditor.ReadOnly : Spec.Editor;

    public string Placeholder => IsMixed ? ConsoleDetailStateMachine.MixedValuesText : string.Empty;

    /// <summary>읽기 전용으로 보일 글(여러 값이면 그 표기).</summary>
    public string DisplayText => IsMixed ? ConsoleDetailStateMachine.MixedValuesText : _text;

    public bool IsTouched => _tracker.IsTouched(Key);

    public string? Error
    {
        get => _error;
        set { if (_error == value) return; _error = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(_error);

    public string Text
    {
        get => _text;
        set
        {
            var next = value ?? string.Empty;
            if (_text == next) return;
            _text = next;
            OnEdited();
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(DisplayText));
            NotifyOfPropertyChange(nameof(BoolValue));
        }
    }

    /// <summary>참/거짓 칸. 여러 값이면 null(가운데 상태).</summary>
    public bool? BoolValue
    {
        get => bool.TryParse(_text, out var parsed) ? parsed : null;
        set { if (value.HasValue) Text = value.Value ? "true" : "false"; }
    }

    public PropertyOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (Equals(_selectedOption, value)) return;
            _selectedOption = value;
            NotifyOfPropertyChange();
            if (_isLoading || value is null) return;
            // 객체로만 쓰는 항목(제어기 · 서버)은 글이 없다 — 보이는 이름을 글로 삼아 "바뀌었다"를 잡는다.
            Text = value.Text ?? value.Display;
        }
    }

    /// <summary>적용 때 객체로 써야 하는 값. 글로 쓰는 칸이면 null.</summary>
    public object? PendingObject => _selectedOption?.Value;

    /// <summary>객체로 쓰는 칸인가(제어기 · 서버).</summary>
    public bool WritesObject => Spec.OptionSource is DevicePropertyOptionSource.Controllers or DevicePropertyOptionSource.Servers;

    /// <summary>
    /// 고른 행들로 칸을 채운다. 손댄 표시는 폼이 미리 지운다.
    /// </summary>
    public void Load(IReadOnlyList<object> rows, IReadOnlyList<PropertyOption> options, bool isCreating, bool isReadOnly, bool isNotReceived)
    {
        _isLoading = true;
        try
        {
            var texts = rows.Select(row => DevicePropertyAccessor.ReadText(row, Spec)).ToList();
            var mixed = MixedValue<string>.Of(texts);

            IsMixed = mixed.IsMixed;
            _originalText = mixed.IsMixed ? string.Empty : mixed.Value ?? string.Empty;
            _text = _originalText;
            _error = null;
            IsNotReceived = isNotReceived;
            IsRequired = isCreating && Spec.IsRequiredOnCreate;
            Options = options;

            (IsLocked, LockReason) = ResolveLock(rows.Count, isCreating, isReadOnly);

            _selectedOption = IsMixed ? null : MatchOption(rows.Count > 0 ? DevicePropertyAccessor.Read(rows[0], Spec) : null);
        }
        finally { _isLoading = false; }

        Refresh();
    }

    /// <summary>원래 글로 돌려놓는다.</summary>
    public void Revert()
    {
        _isLoading = true;
        try
        {
            _text = _originalText;
            _error = null;
            _selectedOption = IsMixed ? null : Options.FirstOrDefault(o => string.Equals(o.Text ?? o.Display, _originalText, StringComparison.Ordinal));
        }
        finally { _isLoading = false; }

        Refresh();
    }

    /// <summary>값을 검증한다. 문제가 있으면 칸에 적고 false.</summary>
    public bool Validate(bool isCreating)
    {
        if (IsLocked) { Error = null; return true; }

        // 객체로 쓰는 칸은 글 검증이 뜻이 없다 — 등록 때 비어 있으면 안 된다는 것만 본다.
        if (WritesObject)
        {
            Error = isCreating && Spec.IsRequiredOnCreate && PendingObject is null ? $"{Label}을(를) 골라야 한다" : null;
            return !HasError;
        }

        Error = DevicePropertyAccessor.Validate(Spec, _text, isCreating);
        return !HasError;
    }

    /// <summary>행 하나에 이 칸의 값을 쓴다. 못 썼으면 까닭을 칸에 남긴다.</summary>
    public bool WriteTo(object row)
    {
        string? error;
        var written = WritesObject
            ? DevicePropertyAccessor.TryWriteObject(row, Spec, PendingObject, out error)
            : DevicePropertyAccessor.TryWrite(row, Spec, _text, out error);

        if (!written) Error = error ?? "값을 쓸 수 없다";
        return written;
    }

    private void OnEdited()
    {
        if (_isLoading) return;
        if (HasError) Error = null;
        _tracker.Touch(Key, _originalText, _text, hasOriginal: !IsMixed);
        NotifyOfPropertyChange(nameof(IsTouched));
    }

    private (bool, string?) ResolveLock(int rowCount, bool isCreating, bool isReadOnly)
    {
        if (Spec.Writable == DevicePropertyWritable.No || Spec.Editor == DevicePropertyEditor.ReadOnly) return (true, Spec.LockReason);
        if (Spec.Writable == DevicePropertyWritable.CreateOnly && !isCreating) return (true, Spec.LockReason ?? CreateOnlyReason);
        if (isReadOnly) return (true, ReadOnlyReason);
        if (rowCount > 1 && !Spec.AllowMultiEdit) return (true, MultiIdentityReason);
        return (false, null);
    }

    private PropertyOption? MatchOption(object? rawValue)
    {
        if (Options.Count == 0) return null;
        if (rawValue is not null)
        {
            var byValue = Options.FirstOrDefault(o => o.Value is not null && IsSameEntity(o.Value, rawValue));
            if (byValue is not null) return byValue;
        }
        return Options.FirstOrDefault(o => string.Equals(o.Text, _text, StringComparison.Ordinal))
            ?? Options.FirstOrDefault(o => string.Equals(o.Display, _text, StringComparison.Ordinal));
    }

    // 제어기 · 서버는 같은 것이라도 인스턴스가 다를 수 있다(목록은 provider 에서, 행의 값은 조회 응답에서 온다) — Id 로 맞춘다.
    // 이름으로 맞추면 이름이 같은 두 대를 구분하지 못한다.
    private static bool IsSameEntity(object a, object b)
    {
        if (ReferenceEquals(a, b) || Equals(a, b)) return true;
        var idA = a.GetType().GetProperty("Id")?.GetValue(a);
        var idB = b.GetType().GetProperty("Id")?.GetValue(b);
        return idA is int x && idB is int y && x == y && x > 0;
    }
}
