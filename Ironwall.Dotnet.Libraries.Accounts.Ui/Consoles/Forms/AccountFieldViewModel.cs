using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Forms;

/// <summary>
/// 상세 폼의 칸 하나. <b>행을 직접 고치지 않는다</b> — 입력은 칸 안에만 머물고,
/// [적용] 때 폼이 손댄 칸만 골라 행에 쓴다. 그래서 [되돌리기] 는 칸을 원래 글로 돌려놓는 것으로 끝난다.
/// </summary>
public sealed class AccountFieldViewModel : PropertyChangedBase
{
    public const string MultiIdentityReason = "사람마다 달라야 하는 값입니다.";
    public const string ReadOnlyReason = "편집 권한이 없습니다.";

    private readonly DirtyFieldTracker _tracker;
    private string _original = string.Empty;
    private string _text = string.Empty;
    private string? _error;
    private bool _loading;

    public AccountFieldViewModel(AccountFieldSpec spec, DirtyFieldTracker tracker)
    {
        Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public AccountFieldSpec Spec { get; }
    public string Key => Spec.Key;
    public string Label => Spec.Label;
    public string ApiPath => Spec.ApiPath;
    public IReadOnlyList<string> Options => Spec.Options ?? Array.Empty<string>();

    /// <summary>머리글 — 꼭 채워야 하는 칸은 별표를 단다(색이 아니라 글자로).</summary>
    public string HeaderText => Spec.Required ? Spec.Label + " *" : Spec.Label;

    /// <summary>고른 행들의 값이 서로 다르다.</summary>
    public bool IsMixed { get; private set; }

    public bool IsLocked { get; private set; }
    public string? LockReason { get; private set; }

    /// <summary>화면이 고를 편집기 — 잠겼으면 무엇이든 읽기 전용으로 그린다.</summary>
    public AccountFieldEditor EffectiveEditor => IsLocked ? AccountFieldEditor.ReadOnly : Spec.Editor;

    public bool IsTouched => _tracker.IsTouched(Key);

    public string Placeholder => IsMixed ? ConsoleDetailStateMachine.MixedValuesText : string.Empty;

    /// <summary>읽기 전용으로 보일 글(여러 값이면 그 표기).</summary>
    public string DisplayText => IsMixed ? ConsoleDetailStateMachine.MixedValuesText : _text;

    public string Text
    {
        get => _text;
        set
        {
            var next = value ?? string.Empty;
            if (_text == next) return;
            _text = next;
            if (!_loading)
            {
                // 여러 값이면 원래 글이 없다 — 무엇을 넣든 변경으로 친다.
                _tracker.Touch(Key, IsMixed ? null : _original, _text, hasOriginal: !IsMixed);
                Error = null;
            }
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(DisplayText));
            NotifyOfPropertyChange(nameof(IsTouched));
        }
    }

    public string? Error
    {
        get => _error;
        set { if (_error == value) return; _error = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(_error);

    /// <summary>고른 행들로 칸을 채운다.</summary>
    public void Load(IReadOnlyList<AccountViewModel> rows, bool isReadOnly)
    {
        _loading = true;
        try
        {
            var values = rows.Select(Spec.Read).ToList();
            var mixed = MixedValue<string>.Of(values, StringComparer.Ordinal);
            IsMixed = mixed.IsMixed;
            _original = mixed.IsMixed ? string.Empty : (mixed.Value ?? string.Empty);
            _text = _original;

            LockReason = Spec.LockReason
                ?? (Spec.Write is null ? "읽기 전용입니다." : null)
                ?? (isReadOnly ? ReadOnlyReason : null)
                ?? (rows.Count > 1 && !Spec.AllowMultiEdit ? MultiIdentityReason : null);
            IsLocked = LockReason is not null;
            Error = null;
        }
        finally { _loading = false; }

        Refresh();
    }

    /// <summary>원래 글로 되돌린다. 행은 건드린 적이 없으므로 복원할 것이 없다.</summary>
    public void Revert()
    {
        _loading = true;
        try { _text = _original; }
        finally { _loading = false; }
        Error = null;
        Refresh();
    }

    /// <summary>적용 뒤 — 지금 글을 새 원본으로 삼는다.</summary>
    public void MarkApplied()
    {
        _original = _text;
        IsMixed = false;
        Refresh();
    }

    /// <summary>글자 검증. 통과하면 true.</summary>
    public bool Validate()
    {
        if (IsLocked) return true;
        if (Spec.Required && string.IsNullOrWhiteSpace(_text))
        {
            Error = "필수 항목입니다.";
            return false;
        }
        if (Spec.Editor == AccountFieldEditor.Choice && _text.Length > 0 && Options.Count > 0
            && !Options.Contains(_text, StringComparer.Ordinal))
        {
            Error = "고를 수 없는 값입니다.";
            return false;
        }
        Error = null;
        return true;
    }

    /// <summary>행에 쓴다.</summary>
    public void WriteTo(AccountViewModel row) => Spec.Write?.Invoke(row, _text);
}
