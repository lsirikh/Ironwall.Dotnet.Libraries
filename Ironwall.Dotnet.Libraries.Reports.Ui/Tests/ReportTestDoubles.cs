using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>아무 데도 쓰지 않는 로그 — 테스트가 콘솔을 더럽히지 않게.</summary>
public sealed class FakeLogService : ILogService
{
    public void Debug(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) { }
    public void Error(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) { }
    public void Info(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) { }
    public void Warning(string msg, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) { }
    public event EventHandler<LogEventArgs>? LogEvent;
    public void Raise() => LogEvent?.Invoke(this, null!);
}

/// <summary>권한을 시험이 정한다.</summary>
public sealed class FakePermissionService : IPermissionService
{
    public bool View { get; set; } = true;
    public bool Edit { get; set; } = true;

    public EnumUserRole Role => EnumUserRole.ADMIN;
    public bool IsAdmin => true;
    public string? LoginId => "tester";
    public string? Name => "시험";
    public DateTimeOffset? ValidUntil => null;
    public DateTimeOffset? ServerTime => null;
    public TimeSpan ClockSkew => TimeSpan.Zero;

    public bool HasRole(EnumUserRole required) => true;
    /// <summary>모듈별로 view 를 가르고 싶을 때만 준다(없으면 <see cref="View"/> 하나로 답한다).</summary>
    public Func<string, bool>? ViewFor { get; set; }
    public bool CanView(string module) => ViewFor?.Invoke(module) ?? View;
    public bool CanEdit(string module) => Edit;
    public bool CanControl(string module) => Edit;
    /// <summary>delete 를 edit 와 따로 정하고 싶을 때만 준다(없으면 <see cref="Edit"/> 를 따른다).</summary>
    public bool? Delete { get; set; }
    public bool CanDelete(string module) => Delete ?? Edit;
    public bool HasDeviceGroup(int id) => true;
    public IReadOnlyList<int> GetAccessibleDeviceGroups() => Array.Empty<int>();
    public bool CanAccessAuditLogs() => true;
    public void Apply(AuthUserDto user) { }
    public void Refresh(PermissionsSnapshotDto snapshot) { }
    public void Clear() { }

    public event Action? PermissionsChanged;
    public void RaiseChanged() => PermissionsChanged?.Invoke();
}

/// <summary>
/// 가짜 보고서 API — 서버 없이 콘솔을 세운다. 무엇을 돌려줄지 시험이 정한다.
/// </summary>
public sealed class FakeReportApiService : IReportApiService
{
    public List<ReportGenerationDto> Generations { get; } = new();
    public List<ReportTemplateDto> Templates { get; } = new();
    public List<ReportComponentCategoryDto> Components { get; } = new();

    public string? PreviewHtml { get; set; } = "<html><body>미리보기</body></html>";
    public bool FailList { get; set; }
    public bool FailWrite { get; set; }
    public bool DeleteSucceeds { get; set; } = true;

    /// <summary>생성 요청이 끝난 상태 — 폴링이 처음 보는 값(기본: 완료).</summary>
    public string GeneratedStatus { get; set; } = "COMPLETED";
    /// <summary>서버가 싣는 사유(8.0.2 는 한국어 한 줄로 정규화해 보낸다).</summary>
    public string? GeneratedErrorMessage { get; set; }

    // 무엇이 실제로 나갔는가 — 계약 단언에 쓴다.
    public ReportTemplateUpdateDto? LastUpdate { get; private set; }
    public ReportTemplateCreateDto? LastCreate { get; private set; }
    public ReportGenerateRequestDto? LastGenerate { get; private set; }
    public string? LastStatusFilter { get; private set; }
    public int StatusFilterCallCount { get; private set; }
    public List<int> DeletedGenerationIds { get; } = new();
    public List<int> DeletedTemplateIds { get; } = new();
    public List<int> CancelledIds { get; } = new();

    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;

    public Task<ApiResponse<List<ReportComponentCategoryDto>>> GetComponentsAsync(CancellationToken token = default)
        => Task.FromResult(new ApiResponse<List<ReportComponentCategoryDto>> { Success = true, Data = Components });

    public Task<ApiResponse<ReportStatusDto>> GetStatusAsync(CancellationToken token = default)
        => Task.FromResult(new ApiResponse<ReportStatusDto> { Success = true, Data = new ReportStatusDto() });

    public int TemplateListCallCount { get; private set; }

    public Task<ApiListResponse<ReportTemplateDto>> GetTemplatesAsync(int page = 1, int limit = 20, CancellationToken token = default)
    {
        TemplateListCallCount++;
        return Task.FromResult(FailList
            ? new ApiListResponse<ReportTemplateDto> { Success = false }
            : new ApiListResponse<ReportTemplateDto> { Success = true, Data = Templates.ToList() });
    }

    public Task<ApiResponse<ReportTemplateDto>> GetTemplateByIdAsync(int id, CancellationToken token = default)
        => Task.FromResult(new ApiResponse<ReportTemplateDto> { Success = true, Data = Templates.FirstOrDefault(t => t.Id == id) });

    public Task<ApiResponse<ReportTemplateDto>> CreateTemplateAsync(ReportTemplateCreateDto dto, CancellationToken token = default)
    {
        LastCreate = dto;
        if (FailWrite) return Task.FromResult(new ApiResponse<ReportTemplateDto> { Success = false });
        var created = new ReportTemplateDto
        {
            Id = Templates.Count == 0 ? 1 : Templates.Max(t => t.Id) + 1,
            Name = dto.Name,
            Description = dto.Description,
            DefaultPeriod = dto.DefaultPeriod,
            Components = dto.Components,
            ComponentCount = dto.Components.Count,
        };
        Templates.Add(created);
        return Task.FromResult(new ApiResponse<ReportTemplateDto> { Success = true, Data = created });
    }

    public Task<ApiResponse<ReportTemplateDto>> UpdateTemplateAsync(int id, ReportTemplateUpdateDto dto, CancellationToken token = default)
    {
        LastUpdate = dto;
        if (FailWrite) return Task.FromResult(new ApiResponse<ReportTemplateDto> { Success = false });

        var target = Templates.FirstOrDefault(t => t.Id == id);
        if (target != null)
        {
            if (dto.Name != null) target.Name = dto.Name;
            if (dto.Description != null) target.Description = dto.Description;
            if (dto.DefaultPeriod != null) target.DefaultPeriod = dto.DefaultPeriod;
            if (dto.Components != null) { target.Components = dto.Components; target.ComponentCount = dto.Components.Count; }
        }
        return Task.FromResult(new ApiResponse<ReportTemplateDto> { Success = true, Data = target });
    }

    public Task<ApiResponse<object>> DeleteTemplateAsync(int id, CancellationToken token = default)
    {
        DeletedTemplateIds.Add(id);
        if (!DeleteSucceeds) return Task.FromResult(new ApiResponse<object> { Success = false });
        Templates.RemoveAll(t => t.Id == id);
        return Task.FromResult(new ApiResponse<object> { Success = true });
    }

    public Task<ApiResponse<ReportGenerationDto>> GenerateAsync(ReportGenerateRequestDto dto, CancellationToken token = default)
    {
        LastGenerate = dto;
        if (FailWrite) return Task.FromResult(new ApiResponse<ReportGenerationDto> { Success = false });
        var created = new ReportGenerationDto
        {
            Id = Generations.Count == 0 ? 1 : Generations.Max(g => g.Id) + 1,
            Title = dto.Title,
            ReportType = dto.ReportType,
            PeriodType = dto.PeriodType,
            Status = GeneratedStatus,
            ErrorMessage = GeneratedErrorMessage,
        };
        Generations.Insert(0, created);
        return Task.FromResult(new ApiResponse<ReportGenerationDto> { Success = true, Data = created });
    }

    public Task<ApiListResponse<ReportGenerationDto>> GetGenerationsAsync(int page = 1, int limit = 20, string? status = null, CancellationToken token = default)
    {
        LastStatusFilter = status;
        StatusFilterCallCount++;
        if (FailList) return Task.FromResult(new ApiListResponse<ReportGenerationDto> { Success = false });

        var data = status is null ? Generations.ToList() : Generations.Where(g => g.Status == status).ToList();
        return Task.FromResult(new ApiListResponse<ReportGenerationDto> { Success = true, Data = data, Total = data.Count });
    }

    public Task<ApiResponse<ReportGenerationDto>> GetGenerationByIdAsync(int id, CancellationToken token = default)
        => Task.FromResult(new ApiResponse<ReportGenerationDto> { Success = true, Data = Generations.FirstOrDefault(g => g.Id == id) });

    public Task<ApiResponse<object>> DeleteGenerationAsync(int id, CancellationToken token = default)
    {
        DeletedGenerationIds.Add(id);
        if (!DeleteSucceeds) return Task.FromResult(new ApiResponse<object> { Success = false });
        Generations.RemoveAll(g => g.Id == id);
        return Task.FromResult(new ApiResponse<object> { Success = true });
    }

    public Task<ApiResponse<ReportCancelResultDto>> CancelGenerationAsync(int id, CancellationToken token = default)
    {
        CancelledIds.Add(id);
        var target = Generations.FirstOrDefault(g => g.Id == id);
        if (target != null) target.Status = "CANCELLED";
        return Task.FromResult(new ApiResponse<ReportCancelResultDto> { Success = true, Data = new ReportCancelResultDto { Id = id, TaskCancelled = true } });
    }

    public Task<ApiResponse<ReportPreviewDto>> GetPreviewAsync(int id, CancellationToken token = default)
        => Task.FromResult(new ApiResponse<ReportPreviewDto> { Success = true, Data = new ReportPreviewDto { Id = id } });

    public Task<string?> GetPreviewHtmlAsync(int id, CancellationToken token = default) => Task.FromResult(PreviewHtml);

    public Task<ReportPdfResult> DownloadPdfAsync(int id, CancellationToken token = default)
        => Task.FromResult(ReportPdfResult.Fail("시험에서는 내려받지 않습니다."));

    public Task<ReportPdfResult> DownloadDetailCsvAsync(int id, string type, CancellationToken token = default)
        => Task.FromResult(ReportPdfResult.Fail("시험에서는 내려받지 않습니다."));
}

/// <summary>시험용 씨앗.</summary>
public static class ReportSeed
{
    public static ReportGenerationDto Generation(int id, string title, string status = "COMPLETED", int progress = 0)
        => new()
        {
            Id = id,
            Title = title,
            Status = status,
            ReportType = "STANDARD",
            PeriodType = "7d",
            CreatedAt = "2026-09-20T09:00:00+09:00",
            GeneratorName = "관리자",
            ProgressPct = progress,
            ProgressStage = progress > 0 ? "collecting" : null,
            ProgressUpdatedAt = progress > 0 ? "2026-09-20T09:01:00+09:00" : null,
        };

    public static ReportTemplateDto Template(int id, string name, params string[] componentIds)
        => new()
        {
            Id = id,
            Name = name,
            ReportType = "CUSTOM",
            DefaultPeriod = "7d",
            ComponentCount = componentIds.Length,
            Components = componentIds.Select((c, i) => new ReportComponentConfigDto { Id = c, Order = i, Enabled = true }).ToList(),
        };

    public static List<ReportComponentCategoryDto> Catalog(params string[] ids)
        => new()
        {
            new ReportComponentCategoryDto
            {
                Category = "summary",
                Label = "요약",
                Components = ids.Select(id => new ReportComponentItemDto { Id = id, Name = id.ToUpperInvariant(), ChartType = "BAR" }).ToList(),
            },
        };
}
