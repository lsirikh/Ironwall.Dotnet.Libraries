using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.BulkImport;
/****************************************************************************
   Purpose      : 장비 대량 등록 — 미리보기 판 (device-bulk-import FR-07~FR-16)
   Created By   : GHLee
   Created On   : 10/13/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>파일 고르기 · 저장 창 — 시험은 가짜로 바꿔 끼운다(창을 띄우지 않는다).</summary>
public interface IBulkFilePicker
{
    /// <summary>올릴 파일. 취소하면 null.</summary>
    string? PickOpen();

    /// <summary>저장할 곳. 취소하면 null.</summary>
    string? PickSave(string suggestedName, BulkFileFormat format);
}

/// <summary>Windows 파일 창.</summary>
public sealed class WpfBulkFilePicker : IBulkFilePicker
{
    public string? PickOpen()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "장비 목록 파일 올리기",
            Filter = "장비 목록(*.csv;*.xlsx)|*.csv;*.xlsx|CSV(*.csv)|*.csv|Excel(*.xlsx)|*.xlsx",
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSave(string suggestedName, BulkFileFormat format)
    {
        var csv = format == BulkFileFormat.Csv;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "파일로 저장",
            FileName = suggestedName,
            DefaultExt = csv ? ".csv" : ".xlsx",
            Filter = csv ? "CSV(*.csv)|*.csv" : "Excel(*.xlsx)|*.xlsx",
            AddExtension = true,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

/// <summary>미리보기 칸 칩 하나.</summary>
public sealed class BulkCellViewModel
{
    public BulkCellViewModel(BulkCell cell) { Cell = cell; }

    public BulkCell Cell { get; }
    public string Header => Cell.Column.Header;
    public string Display => Cell.Display;
    public bool IsChanged => Cell.IsChanged && Cell.Error is null;
    public bool IsError => Cell.Error is not null;
    public string? Error => Cell.Error;
    public string ToolTipText => Cell.Error ?? (Cell.IsChanged ? "바뀌는 칸" : Cell.Column.Header);
}

/// <summary>미리보기 줄 하나 — 판정 + (등록 뒤) 결과.</summary>
public sealed class BulkImportRowViewModel : PropertyChangedBase
{
    public BulkImportRowViewModel(BulkImportRow row)
    {
        Row = row;
        // 빈 칸은 칩으로 늘어놓지 않는다(오류가 달린 빈 칸 = 필수 칸 빔은 보인다).
        Cells = row.Cells.Where(c => c.Column.Kind != BulkColumnKind.Id && (c.Raw.Length > 0 || c.Error is not null))
                         .Select(c => new BulkCellViewModel(c)).ToList();
    }

    public BulkImportRow Row { get; }
    public int Line => Row.Line;
    public string SheetName => Row.SheetName;
    public string Name => Row.Name;
    public string VerdictText => BulkImportWriter.VerdictText(Row.Verdict);
    public BulkRowVerdict Verdict => Row.Verdict;
    public bool IsError => Row.Verdict == BulkRowVerdict.Error;
    public IReadOnlyList<BulkCellViewModel> Cells { get; }

    /// <summary>오류 사유(판정) 또는 결과 사유(등록 뒤).</summary>
    public string ReasonText => Outcome is { Status: not BulkWriteStatus.Succeeded } outcome ? outcome.Message : string.Join(" · ", Row.Errors);

    public BulkWriteOutcome? Outcome
    {
        get => _outcome;
        set
        {
            _outcome = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(ResultText));
            NotifyOfPropertyChange(nameof(ReasonText));
            NotifyOfPropertyChange(nameof(IsFailed));
            NotifyOfPropertyChange(nameof(IsSucceeded));
        }
    }

    public string ResultText => _outcome?.Status switch
    {
        BulkWriteStatus.Succeeded => "완료",
        BulkWriteStatus.Failed => "실패 · " + BulkImportWriter.KindText(_outcome.Kind),
        BulkWriteStatus.Cancelled => "취소",
        _ => string.Empty,
    };

    public bool IsFailed => _outcome?.Status is BulkWriteStatus.Failed or BulkWriteStatus.Cancelled;
    public bool IsSucceeded => _outcome?.Status == BulkWriteStatus.Succeeded;

    private BulkWriteOutcome? _outcome;
}

/// <summary>
/// 올리기 미리보기 판 — 파일 읽기 · 판정은 작업 스레드(취소 가능), 등록은 [등록] 때만(Temp-state Draft, FR-08).
/// </summary>
public sealed class BulkImportViewModel : PropertyChangedBase
{
    public BulkImportViewModel(Func<BulkImportContext> context, Func<BulkImportWriter> writerFactory, ILogService? log = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _writerFactory = writerFactory ?? throw new ArgumentNullException(nameof(writerFactory));
        _log = log;
    }

    #region - Open -
    /// <summary>파일을 읽고 판정해 판을 연다. 읽는 동안 판은 "읽는 중" 이고 [취소] 로 멈춘다.</summary>
    public async Task OpenFileAsync(string path)
    {
        if (IsBusy) return;
        var context = _context();
        Reset();
        FileName = Path.GetFileName(path);
        IsOpen = true;
        IsBusy = true;
        StatusText = "파일을 읽는 중…";
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            var plan = await Task.Run(() =>
            {
                var sheets = BulkFileReader.SelectSheets(BulkFileReader.Read(path, token), context.RailCategory);
                return BulkImportPlanner.Plan(sheets, context, token);
            }, token);

            _plan = plan;
            _writer = _writerFactory();
            Rows.Clear();
            foreach (var row in plan.Rows) Rows.Add(new BulkImportRowViewModel(row));
            StatusText = plan.FileError ?? (plan.Rows.Count == 0 ? "올릴 줄이 없습니다." : string.Empty);
            IgnoredText = plan.IgnoredColumns.Count == 0 ? string.Empty : "모르는 칸은 건너뜀: " + string.Join(", ", plan.IgnoredColumns);
            _log?.Info($"[BulkImport] 파일 판정 — 줄 {plan.Rows.Count} · 신규 {plan.Count(BulkRowVerdict.New)} · 수정 {plan.Count(BulkRowVerdict.Update)} · 같음 {plan.Count(BulkRowVerdict.Same)} · 오류 {plan.Count(BulkRowVerdict.Error)}");
        }
        catch (OperationCanceledException)
        {
            StatusText = "읽기를 취소했습니다.";
        }
        catch (BulkFileException ex)
        {
            StatusText = ex.Message;
        }
        catch (Exception ex)
        {
            _log?.Error($"[BulkImport] 파일 읽기 실패 — {ex.GetType().Name}");
            StatusText = "파일 내용을 읽지 못했습니다 — 형식이 맞는지 확인하세요.";
        }
        finally
        {
            IsBusy = false;
            RefreshSummary();
        }
    }
    #endregion

    #region - Register -
    /// <summary>[등록] — 신규 · 수정 줄을 보낸다(오류 줄은 "오류 줄 빼고" 를 골랐을 때만 빼고 보낸다, FR-11).</summary>
    public Task RegisterAsync() => CanRegister ? RunAsync(Rows.Where(r => r.Verdict is BulkRowVerdict.New or BulkRowVerdict.Update).ToList()) : Task.CompletedTask;

    /// <summary>[실패 줄 다시] — 실패 · 취소된 줄만 다시 보낸다.</summary>
    public Task RetryFailedAsync() => CanRetry ? RunAsync(Rows.Where(r => r.IsFailed).ToList()) : Task.CompletedTask;

    private async Task RunAsync(IReadOnlyList<BulkImportRowViewModel> targets)
    {
        if (_writer is null || targets.Count == 0) return;
        IsBusy = true;
        _hasWritten = true;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        ProgressMaximum = targets.Count;
        ProgressValue = 0;
        StatusText = "보내는 중…";
        var progress = new SyncProgress(p =>
        {
            ProgressValue = p.Done;
            ProgressMaximum = Math.Max(1, p.Total);
            StatusText = $"보내는 중… {p.Done}/{p.Total}";
        });

        IReadOnlyList<BulkWriteOutcome> outcomes = Array.Empty<BulkWriteOutcome>();
        try
        {
            var writer = _writer;
            outcomes = await Task.Run(() => writer.WriteAsync(targets.Select(t => t.Row).ToList(), progress, token));
        }
        catch (Exception ex)
        {
            _log?.Error($"[BulkImport] 등록 중 예외 — {ex.GetType().Name}");
        }
        finally
        {
            IsBusy = false;
        }

        foreach (var outcome in outcomes)
        {
            var row = Rows.FirstOrDefault(r => ReferenceEquals(r.Row, outcome.Row));
            if (row is not null) row.Outcome = outcome;
        }

        var ok = outcomes.Count(o => o.Status == BulkWriteStatus.Succeeded);
        StatusText = ResultSummary();
        RefreshSummary();
        if (ok > 0) Written?.Invoke(this, ok);
    }

    /// <summary>읽기 · 보내기 멈추기 — 보내는 중이면 지금 줄까지 끝내고 나머지는 "취소" 로 남는다.</summary>
    public void Cancel() => _cts?.Cancel();

    /// <summary>[결과 받기] — 줄 · 이름 · 결과 · 분류 · 사유 CSV(비밀번호 없음).</summary>
    public void SaveResult(Stream stream)
        => BulkImportWriter.WriteResultCsv(stream, Rows.Select(r => (r.Row,
            r.Outcome is null ? (r.Verdict == BulkRowVerdict.Same ? "건너뜀" : r.IsError ? "보내지 않음" : string.Empty) : r.ResultText,
            BulkImportWriter.KindText(r.Outcome?.Kind),
            r.ReasonText)));

    /// <summary>판을 닫는다(돌던 일은 멈춘다).</summary>
    public void Close()
    {
        Cancel();
        Reset();
        IsOpen = false;
    }

    private void Reset()
    {
        _plan = null;
        _writer = null;
        _hasWritten = false;
        _excludeErrorRows = false;   // 새 파일마다 다시 고른다 — 지난 파일의 선택이 오류 줄을 말없이 빼지 않게
        NotifyOfPropertyChange(nameof(ExcludeErrorRows));
        Rows.Clear();
        StatusText = string.Empty;
        IgnoredText = string.Empty;
        ProgressValue = 0;
        ProgressMaximum = 1;
        RefreshSummary();
    }
    #endregion

    #region - State -
    public ObservableCollection<BulkImportRowViewModel> Rows { get; } = new();

    public bool IsOpen { get => _isOpen; private set { if (_isOpen == value) return; _isOpen = value; NotifyOfPropertyChange(); } }
    public bool IsBusy { get => _isBusy; private set { if (_isBusy == value) return; _isBusy = value; NotifyOfPropertyChange(); RefreshSummary(); } }
    public string FileName { get => _fileName; private set { _fileName = value; NotifyOfPropertyChange(); } }
    public string StatusText { get => _statusText; private set { _statusText = value ?? string.Empty; NotifyOfPropertyChange(); } }
    public string IgnoredText { get => _ignoredText; private set { _ignoredText = value; NotifyOfPropertyChange(); } }
    public double ProgressValue { get => _progressValue; private set { _progressValue = value; NotifyOfPropertyChange(); } }
    public double ProgressMaximum { get => _progressMaximum; private set { _progressMaximum = value; NotifyOfPropertyChange(); } }

    /// <summary>오류 줄은 빼고 나머지만 등록한다(FR-11).</summary>
    public bool ExcludeErrorRows
    {
        get => _excludeErrorRows;
        set { if (_excludeErrorRows == value) return; _excludeErrorRows = value; NotifyOfPropertyChange(); RefreshSummary(); }
    }

    public int ErrorCount => Rows.Count(r => r.IsError);
    public int SendableCount => Rows.Count(r => r.Verdict is BulkRowVerdict.New or BulkRowVerdict.Update);

    /// <summary>[등록] — 읽은 표가 있고 보낼 줄이 있고, 오류가 0 이거나 "오류 줄 빼고" 를 골랐을 때만. 한 번 보낸 뒤에는 [실패 줄 다시] 만.</summary>
    public bool CanRegister => IsOpen && !IsBusy && !_hasWritten && _plan?.FileError is null && SendableCount > 0 && (ErrorCount == 0 || ExcludeErrorRows);

    public string? RegisterBlockedReason
        => !IsOpen || _plan is null ? "파일을 먼저 올리세요."
        : IsBusy ? "처리 중입니다."
        : _hasWritten ? "이미 보냈습니다 — 실패한 줄은 [실패 줄 다시]로 보내세요."
        : _plan.FileError is not null ? _plan.FileError
        : SendableCount == 0 ? "새로 등록하거나 바꿀 줄이 없습니다."
        : ErrorCount > 0 && !ExcludeErrorRows ? $"오류 {ErrorCount}줄을 고치거나 '오류 줄 빼고 등록'을 고르세요."
        : null;

    public bool CanRetry => !IsBusy && Rows.Any(r => r.IsFailed);
    public bool CanSaveResult => !IsBusy && _hasWritten;
    public bool CanCancel => IsBusy;
    public bool HasRows => Rows.Count > 0;

    public string SummaryText { get => _summaryText; private set { _summaryText = value; NotifyOfPropertyChange(); } }

    /// <summary>한 줄 이상 서버에 썼다 — 대시보드가 목록을 다시 읽는다(인자 = 성공 줄 수).</summary>
    public event EventHandler<int>? Written;

    private void RefreshSummary()
    {
        SummaryText = Rows.Count == 0 ? string.Empty
            : $"신규 {Count(BulkRowVerdict.New)} · 수정 {Count(BulkRowVerdict.Update)} · 같음 {Count(BulkRowVerdict.Same)} · 오류 {Count(BulkRowVerdict.Error)}";
        NotifyOfPropertyChange(nameof(CanRegister));
        NotifyOfPropertyChange(nameof(RegisterBlockedReason));
        NotifyOfPropertyChange(nameof(CanRetry));
        NotifyOfPropertyChange(nameof(CanSaveResult));
        NotifyOfPropertyChange(nameof(CanCancel));
        NotifyOfPropertyChange(nameof(ErrorCount));
        NotifyOfPropertyChange(nameof(SendableCount));
        NotifyOfPropertyChange(nameof(HasRows));

        int Count(BulkRowVerdict verdict) => Rows.Count(r => r.Verdict == verdict);
    }

    /// <summary>결과 한 줄 — 성공 · 실패(4분류) · 취소.</summary>
    public string ResultSummary()
    {
        var outcomes = Rows.Select(r => r.Outcome).OfType<BulkWriteOutcome>().ToList();
        var failed = outcomes.Where(o => o.Status == BulkWriteStatus.Failed).ToList();
        var kinds = failed.GroupBy(o => o.Kind).Select(g => $"{BulkImportWriter.KindText(g.Key)} {g.Count()}");
        var cancelled = outcomes.Count(o => o.Status == BulkWriteStatus.Cancelled);
        return $"완료 {outcomes.Count(o => o.Status == BulkWriteStatus.Succeeded)} · 실패 {failed.Count}"
               + (failed.Count > 0 ? $"({string.Join(" · ", kinds)})" : string.Empty)
               + (cancelled > 0 ? $" · 취소 {cancelled}" : string.Empty);
    }

    /// <summary>결과 파일 이름 제안.</summary>
    public string SuggestedResultName => $"{Path.GetFileNameWithoutExtension(FileName)}_결과_{DateTime.Now.ToString("yyyyMMdd_HHmm", CultureInfo.InvariantCulture)}";

    /// <summary>진행률 — UI 스레드가 있으면 그리로 옮긴다(시험에는 없다 — 그대로 부른다).</summary>
    private sealed class SyncProgress : IProgress<BulkWriteProgress>
    {
        private readonly Action<BulkWriteProgress> _report;
        public SyncProgress(Action<BulkWriteProgress> report) { _report = report; }
        public void Report(BulkWriteProgress value) => Execute.OnUIThread(() => _report(value));
    }

    private readonly Func<BulkImportContext> _context;
    private readonly Func<BulkImportWriter> _writerFactory;
    private readonly ILogService? _log;
    private BulkImportPlan? _plan;
    private BulkImportWriter? _writer;
    private CancellationTokenSource? _cts;
    private bool _hasWritten;
    private bool _isOpen;
    private bool _isBusy;
    private bool _excludeErrorRows;
    private string _fileName = string.Empty;
    private string _statusText = string.Empty;
    private string _ignoredText = string.Empty;
    private string _summaryText = string.Empty;
    private double _progressValue;
    private double _progressMaximum = 1;
    #endregion
}
