using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Consoles;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles.Forms;

/// <summary>상세 폼의 절 하나.</summary>
public sealed class AccountSectionViewModel
{
    public AccountSectionViewModel(AccountFieldSection section, IReadOnlyList<AccountFieldViewModel> fields)
    {
        Section = section;
        Title = AccountFieldCatalog.TitleOf(section);
        AxisName = AccountFieldCatalog.AxisOf(section);
        Fields = fields;
    }

    public AccountFieldSection Section { get; }
    public string Title { get; }
    public string? AxisName { get; }
    public IReadOnlyList<AccountFieldViewModel> Fields { get; }
}

/// <summary>[적용] 이 한 일.</summary>
/// <param name="IsWritten">행에 값을 썼다 — 이제 전송하면 된다.</param>
/// <param name="RowCount">값을 쓴 행 수.</param>
/// <param name="FieldCount">쓴 칸 수.</param>
/// <param name="Message">못 썼을 때의 한 줄.</param>
public sealed record AccountFormCommit(bool IsWritten, int RowCount, int FieldCount, string? Message);

/// <summary>
/// 명세(<see cref="AccountFieldCatalog"/>)에서 <b>만들어지는</b> 사용자 상세 폼.
/// </summary>
/// <remarks>
/// 흐름: 행 선택 → <see cref="Load"/> → 칸 입력(칸 안에만 머문다) → <see cref="Commit"/> 가 검증하고 손댄 칸만 행에 쓴다
/// → 콘솔이 기존 전송 경로(<c>IUserDirectoryGateway.UpdateAccountAsync</c>)를 부른다. 전송은 폼의 일이 아니다.
/// 검증에 하나라도 걸리면 <b>아무 행에도 쓰지 않는다</b>.
/// </remarks>
public sealed class AccountFormViewModel : PropertyChangedBase
{
    private IReadOnlyList<AccountViewModel> _rows = Array.Empty<AccountViewModel>();
    private IReadOnlyList<AccountSectionViewModel> _sections = Array.Empty<AccountSectionViewModel>();

    public AccountFormViewModel(ConsoleDetailPresenter presenter)
        => Presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));

    public ConsoleDetailPresenter Presenter { get; }

    public IReadOnlyList<AccountSectionViewModel> Sections
    {
        get => _sections;
        private set { _sections = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(Fields)); }
    }

    public IEnumerable<AccountFieldViewModel> Fields => _sections.SelectMany(s => s.Fields);

    public IReadOnlyList<AccountViewModel> Rows => _rows;

    /// <summary>고른 행들로 폼을 만든다. 손댄 칸은 비운다 — 버려도 되는지는 부르는 쪽이 먼저 확인한다.</summary>
    public void Load(IReadOnlyList<AccountViewModel> rows, bool isReadOnly)
    {
        _rows = rows ?? Array.Empty<AccountViewModel>();
        Presenter.Tracker.Clear();

        if (_rows.Count == 0)
        {
            Sections = Array.Empty<AccountSectionViewModel>();
            NotifyOfPropertyChange(nameof(Rows));
            return;
        }

        Presenter.Tracker.MarkIdentity(AccountFieldCatalog.All.Where(s => !s.AllowMultiEdit).Select(s => s.Key).ToArray());

        var sections = new List<AccountSectionViewModel>();
        foreach (var section in AccountFieldCatalog.SectionOrder)
        {
            var fields = new List<AccountFieldViewModel>();
            foreach (var spec in AccountFieldCatalog.All.Where(s => s.Section == section))
            {
                var field = new AccountFieldViewModel(spec, Presenter.Tracker);
                field.Load(_rows, isReadOnly);
                fields.Add(field);
            }
            if (fields.Count > 0) sections.Add(new AccountSectionViewModel(section, fields));
        }

        Sections = sections;
        NotifyOfPropertyChange(nameof(Rows));
    }

    public void Clear()
    {
        _rows = Array.Empty<AccountViewModel>();
        Presenter.Tracker.Clear();
        Sections = Array.Empty<AccountSectionViewModel>();
        NotifyOfPropertyChange(nameof(Rows));
    }

    /// <summary>모든 칸을 원래 글로 돌려놓는다.</summary>
    public void Revert()
    {
        // 추적기를 먼저 비운다 — 칸이 다시 그릴 때 "손댄 칸" 표지를 추적기에서 읽는다.
        Presenter.Tracker.Clear();
        foreach (var field in Fields) field.Revert();
    }

    /// <summary>검증하고, 통과하면 <b>손댄 칸만</b> 고른 행 전부에 쓴다.</summary>
    public AccountFormCommit Commit()
    {
        if (_rows.Count == 0) return new AccountFormCommit(false, 0, 0, "고른 계정이 없습니다.");

        var touchedKeys = Presenter.Tracker.ChangesFor(_rows.Count).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        var touched = Fields.Where(f => !f.IsLocked && touchedKeys.Contains(f.Key)).ToList();
        if (touched.Count == 0) return new AccountFormCommit(false, 0, 0, "바꾼 칸이 없습니다.");

        var invalid = touched.Where(f => !f.Validate()).ToList();
        if (invalid.Count > 0)
            return new AccountFormCommit(false, 0, 0,
                $"{invalid[0].Label}: {invalid[0].Error}" + (invalid.Count > 1 ? $" 외 {invalid.Count - 1}건" : string.Empty));

        foreach (var row in _rows)
        {
            foreach (var field in touched) field.WriteTo(row);
            row.RefreshDisplay();
        }
        return new AccountFormCommit(true, _rows.Count, touched.Count, null);
    }

    /// <summary>전송이 끝났다 — 지금 글을 새 원본으로 삼고 손댄 칸을 비운다.</summary>
    public void MarkApplied()
    {
        foreach (var field in Fields) field.MarkApplied();
    }

    /// <summary>재조회로 행 인스턴스가 바뀌었다 — 글과 손댄 표지는 그대로 두고 행만 바꿔 끼운다.</summary>
    public void RebindRows(IReadOnlyList<AccountViewModel> rows)
    {
        if (rows is null || rows.Count != _rows.Count)
            throw new ArgumentException("같은 수의 행으로만 바꿔 끼울 수 있습니다.", nameof(rows));
        _rows = rows;
        NotifyOfPropertyChange(nameof(Rows));
    }
}
