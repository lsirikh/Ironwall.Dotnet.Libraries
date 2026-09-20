using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>센서 여러 개 만들기의 확정 결과.</summary>
/// <param name="Spec">규칙.</param>
/// <param name="SkipConflicts">이미 있는 번호를 건너뛸 것인가(WS L446).</param>
public sealed record MakeSensorsResult(SensorBulkCreateSpec Spec, bool SkipConflicts);

/// <summary>
/// 창이 사람에게 묻거나 다른 창을 여는 일 — 뷰모델이 <see cref="System.Windows.Window"/> 도 클립보드도 모르게 한다(헤드리스 테스트).
/// </summary>
public interface IWiringDialogs
{
    Task<bool> ConfirmAsync(string title, string message);
    Task<string?> AskTextAsync(string title, string label, string initial);
    Task<MakeSensorsResult?> AskMakeSensorsAsync(IReadOnlyList<string> types, string defaultType, string defaultZone, IReadOnlyCollection<int> existingNumbers, int suggestedStart);

    /// <summary>붙여넣기 보고를 보이고 "이대로 만들까?" 를 묻는다.</summary>
    Task<bool> ShowPasteReportAsync(PasteReport report);

    /// <summary>열 매핑을 바꿨을 때 다시 읽을 재료를 화면 계층에 알려 둔다(W7).</summary>
    void RememberPasteContext(IReadOnlyCollection<int> existingNumbers, string defaultType, string defaultZone);

    /// <summary>클립보드 글자(없거나 읽을 수 없으면 <c>null</c>). 화면 계층에서만 실제 클립보드를 만진다.</summary>
    string? ReadClipboardText();
}

/// <summary>확인 · 저장 미리보기용 한 장(WS L450, L772).</summary>
public sealed class WiringPromptViewModel : Screen
{
    public WiringPromptViewModel(string title, string message, string confirmText = "예", string cancelText = "아니오")
    {
        DisplayName = title;
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }
    public bool Result { get; private set; }

    public async Task ConfirmAsync()
    {
        Result = true;
        await TryCloseAsync(true);
    }

    public async Task CancelAsync()
    {
        Result = false;
        await TryCloseAsync(false);
    }
}

/// <summary>한 칸 묻기 — 연속 번호의 시작 · 이름 규칙(WS L631, L635).</summary>
public sealed class WiringTextPromptViewModel : Screen
{
    private string _text;

    public WiringTextPromptViewModel(string title, string label, string initial)
    {
        DisplayName = title;
        Title = title;
        Label = label;
        _text = initial ?? string.Empty;
    }

    public string Title { get; }
    public string Label { get; }
    public string Text { get => _text; set { _text = value ?? string.Empty; NotifyOfPropertyChange(); } }

    /// <summary>취소했으면 null.</summary>
    public string? Result { get; private set; }

    public async Task ConfirmAsync()
    {
        Result = _text;
        await TryCloseAsync(true);
    }

    public async Task CancelAsync()
    {
        Result = null;
        await TryCloseAsync(false);
    }
}

/// <summary>
/// 센서 여러 개 만들기(WS L157, L446, L649-653) — 규칙을 한 번 정하고 <b>미리보기를 본 뒤</b> 만든다.
/// </summary>
public sealed class MakeSensorsViewModel : Screen
{
    private readonly IReadOnlyCollection<int> _existing;
    private string _count = "12";
    private string _startNumber;
    private string _step = "1";
    private string _nameRule = "북측 {번호}구간 펜스";
    private string _typeText;
    private string _zone;
    private bool _skipConflicts = true;

    public MakeSensorsViewModel(IReadOnlyList<string> types, string defaultType, string defaultZone, IReadOnlyCollection<int> existingNumbers, int suggestedStart)
    {
        Types = types ?? Array.Empty<string>();
        _existing = existingNumbers ?? Array.Empty<int>();
        _typeText = string.IsNullOrEmpty(defaultType) ? Types.FirstOrDefault() ?? string.Empty : defaultType;
        _zone = defaultZone ?? string.Empty;
        _startNumber = Math.Max(1, suggestedStart).ToString(CultureInfo.InvariantCulture);
        Preview = new ObservableCollection<SensorPreviewRow>();
        DisplayName = "센서 여러 개 만들기";
        Rebuild();
    }

    public IReadOnlyList<string> Types { get; }
    public ObservableCollection<SensorPreviewRow> Preview { get; }

    public string Count { get => _count; set { _count = value ?? string.Empty; NotifyOfPropertyChange(); Rebuild(); } }
    public string StartNumber { get => _startNumber; set { _startNumber = value ?? string.Empty; NotifyOfPropertyChange(); Rebuild(); } }
    public string Step { get => _step; set { _step = value ?? string.Empty; NotifyOfPropertyChange(); Rebuild(); } }
    public string NameRule { get => _nameRule; set { _nameRule = value ?? string.Empty; NotifyOfPropertyChange(); Rebuild(); } }
    public string TypeText { get => _typeText; set { _typeText = value ?? string.Empty; NotifyOfPropertyChange(); Rebuild(); } }
    public string Zone { get => _zone; set { _zone = value ?? string.Empty; NotifyOfPropertyChange(); Rebuild(); } }

    /// <summary>이미 있는 번호를 건너뛸 것인가 — 끄면 충돌이 하나라도 있을 때 만들 수 없다(전부 아니면 하나도).</summary>
    public bool SkipConflicts
    {
        get => _skipConflicts;
        set { _skipConflicts = value; NotifyOfPropertyChange(); Refresh(); }
    }

    public string? Error { get; private set; }
    public bool HasError => !string.IsNullOrEmpty(Error);
    public int ConflictCount => Preview.Count(r => r.IsConflict);
    public bool HasConflict => ConflictCount > 0;
    public int MakeCount => SkipConflicts ? Preview.Count - ConflictCount : Preview.Count;

    public string Summary => HasError ? Error!
        : HasConflict && SkipConflicts ? $"{MakeCount}줄을 만듭니다 — 이미 있는 번호 {ConflictCount}줄은 건너뜁니다."
        : HasConflict ? $"이미 있는 번호 {ConflictCount}줄이 있습니다 — 건너뛰기를 켜거나 시작 번호를 바꾸세요."
        : $"{MakeCount}줄을 Draft 로 만듭니다 — [저장하기] 를 눌러야 서버에 갑니다.";

    public bool CanMake => !HasError && MakeCount > 0 && (SkipConflicts || !HasConflict);

    public MakeSensorsResult? Result { get; private set; }

    public async Task MakeAsync()
    {
        if (!CanMake || Spec is null) return;
        Result = new MakeSensorsResult(Spec, SkipConflicts);
        await TryCloseAsync(true);
    }

    public async Task CancelAsync()
    {
        Result = null;
        await TryCloseAsync(false);
    }

    private SensorBulkCreateSpec? Spec { get; set; }

    private void Rebuild()
    {
        Preview.Clear();
        Spec = null;
        Error = null;

        if (!int.TryParse(_count.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)) Error = "개수는 숫자로 적어 주세요.";
        else if (!int.TryParse(_startNumber.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var start)) Error = "시작 번호는 숫자로 적어 주세요.";
        else if (!int.TryParse(_step.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var step)) Error = "번호 간격은 숫자로 적어 주세요.";
        else
        {
            var spec = new SensorBulkCreateSpec(count, start, step, _nameRule, _typeText, _zone);
            Error = SensorBulkCreate.Validate(spec);
            if (Error is null)
            {
                Spec = spec;
                foreach (var row in SensorBulkCreate.Preview(spec, _existing)) Preview.Add(row);
            }
        }

        Refresh();
    }

    private void Refresh()
    {
        NotifyOfPropertyChange(nameof(Error));
        NotifyOfPropertyChange(nameof(HasError));
        NotifyOfPropertyChange(nameof(ConflictCount));
        NotifyOfPropertyChange(nameof(HasConflict));
        NotifyOfPropertyChange(nameof(MakeCount));
        NotifyOfPropertyChange(nameof(Summary));
        NotifyOfPropertyChange(nameof(CanMake));
    }
}

/// <summary>열 매핑 콤보 한 칸(W7) — 붙여넣은 글자의 그 열을 무엇으로 읽을지.</summary>
public sealed class PasteColumnViewModel : PropertyChangedBase
{
    private readonly Action<PasteColumnViewModel> _changed;
    private PasteColumn _role;

    public PasteColumnViewModel(int index, string sample, PasteColumn role, Action<PasteColumnViewModel> changed)
    {
        Index = index;
        Sample = sample;
        _role = role;
        _changed = changed;
    }

    public int Index { get; }

    /// <summary>첫 줄의 그 칸 — 무엇을 고르는지 눈으로 알 수 있게.</summary>
    public string Sample { get; }

    public string Header => $"{Index + 1}번째 열";

    public IReadOnlyList<PasteColumn> Roles { get; } = new[]
    {
        PasteColumn.Number, PasteColumn.Name, PasteColumn.Type, PasteColumn.Zone, PasteColumn.Ignore,
    };

    public PasteColumn Role
    {
        get => _role;
        set
        {
            if (_role == value) return;
            _role = value;
            NotifyOfPropertyChange();
            _changed(this);
        }
    }
}

/// <summary>붙여넣기 보고 — 받은 줄 · 만들 줄 · 버린 줄과 까닭(WS L368, L499) + 열 매핑(WS L371 · W7).</summary>
public sealed class PasteReportViewModel : Screen
{
    /// <summary>한 번에 보여 주는 줄 수 — 500줄을 다 그리면 창이 멈춘다.</summary>
    public const int SHOW_LIMIT = 50;

    private readonly IReadOnlyCollection<int> _existingNumbers;
    private readonly string _defaultType;
    private readonly string _defaultZone;
    private bool _rebuilding;

    public PasteReportViewModel(PasteReport report, IReadOnlyCollection<int>? existingNumbers = null,
                                string defaultType = "", string defaultZone = "")
    {
        Report = report ?? throw new ArgumentNullException(nameof(report));
        _existingNumbers = existingNumbers ?? Array.Empty<int>();
        _defaultType = defaultType;
        _defaultZone = defaultZone;

        DisplayName = "엑셀에서 붙여넣기";
        Accepted = new ObservableCollection<PasteRow>();
        Rejected = new ObservableCollection<PasteRow>();
        Columns = new ObservableCollection<PasteColumnViewModel>();
        Rebuild(report);
    }

    public PasteReport Report { get; private set; }
    public ObservableCollection<PasteRow> Accepted { get; }
    public ObservableCollection<PasteRow> Rejected { get; }

    /// <summary>열 매핑 콤보(W7) — 바꾸면 곧바로 다시 읽는다.</summary>
    public ObservableCollection<PasteColumnViewModel> Columns { get; }

    public bool HasColumns => Columns.Count > 0;

    public string Summary => Report.Summary;
    public string ColumnText => "열마다 무엇으로 읽을지 고르세요 — 머리글이 있으면 자동으로 맞춰 둡니다.";
    public bool HasRejected => Report.Rejected.Count > 0;
    public string MoreAcceptedText => Report.Accepted.Count > SHOW_LIMIT ? $"… 외 {Report.Accepted.Count - SHOW_LIMIT}줄" : string.Empty;
    public string MoreRejectedText => Report.Rejected.Count > SHOW_LIMIT ? $"… 외 {Report.Rejected.Count - SHOW_LIMIT}줄" : string.Empty;
    public bool CanApply => Report.HasRows;
    public string ApplyText => $"{Report.Accepted.Count}줄 만들기";
    public bool HasFatal => Report.FatalError is not null;

    public bool Result { get; private set; }

    public async Task ApplyAsync()
    {
        if (!CanApply) return;
        Result = true;
        await TryCloseAsync(true);
    }

    public async Task CancelAsync()
    {
        Result = false;
        await TryCloseAsync(false);
    }

    /// <summary>콤보가 바뀌었다 — 같은 글자를 새 매핑으로 다시 읽는다.</summary>
    private void OnColumnChanged(PasteColumnViewModel column)
    {
        if (_rebuilding) return;

        var mapping = new PasteColumnMap(Columns.Select(c => c.Role).ToList()).With(column.Index, column.Role);
        Rebuild(TsvPaste.Parse(Report.Text, _existingNumbers, _defaultType, _defaultZone, mapping, Report.HeaderDetected));
    }

    private void Rebuild(PasteReport report)
    {
        _rebuilding = true;
        try
        {
            Report = report;

            Accepted.Clear();
            foreach (var row in report.Accepted.Take(SHOW_LIMIT)) Accepted.Add(row);
            Rejected.Clear();
            foreach (var row in report.Rejected.Take(SHOW_LIMIT)) Rejected.Add(row);

            var samples = TsvPaste.Split(report.Text);
            var first = samples.Count > 0 ? samples[0] : Array.Empty<string>();
            var count = Math.Max(report.Columns.Columns.Count, first.Count);

            if (Columns.Count != count)
            {
                Columns.Clear();
                for (var i = 0; i < count; i++)
                    Columns.Add(new PasteColumnViewModel(i, i < first.Count ? first[i] : string.Empty, report.Columns.At(i), OnColumnChanged));
            }
            else
            {
                for (var i = 0; i < count; i++) Columns[i].Role = report.Columns.At(i);
            }
        }
        finally { _rebuilding = false; }

        NotifyOfPropertyChange(nameof(Summary));
        NotifyOfPropertyChange(nameof(HasRejected));
        NotifyOfPropertyChange(nameof(MoreAcceptedText));
        NotifyOfPropertyChange(nameof(MoreRejectedText));
        NotifyOfPropertyChange(nameof(CanApply));
        NotifyOfPropertyChange(nameof(ApplyText));
        NotifyOfPropertyChange(nameof(HasColumns));
        NotifyOfPropertyChange(nameof(HasFatal));
    }
}
