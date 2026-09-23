using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Reports;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Tests;

/// <summary>
/// 가짜 조치보고 문구 API — 서버 없이 콘솔을 세운다. 무엇을 돌려줄지 시험이 정한다.
/// </summary>
public sealed class FakeActionReportTemplateApiService : IActionReportTemplateApiService
{
    public List<ActionReportTemplateDto> Templates { get; } = new();

    public bool? IsSupported { get; private set; } = true;
    public bool FailList { get; set; }
    public bool FailWrite { get; set; }
    public bool FailReorder { get; set; }
    public bool FailDelete { get; set; }
    public int StatusCodeOnFailure { get; set; } = 500;

    /// <summary>실제로 나간 reorder 호출 — "단일 호출·전체 목록" 계약을 단언하는 데 쓴다.</summary>
    public List<List<ActionReportTemplateReorderItemDto>> ReorderCalls { get; } = new();
    public List<ActionReportTemplateCreateDto> CreateCalls { get; } = new();
    public List<int> DeletedIds { get; } = new();

    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken token = default) => Task.CompletedTask;

    public Task<ApiListResponse<ActionReportTemplateDto>> GetTemplatesAsync(CancellationToken token = default)
        => Task.FromResult(FailList
            ? new ApiListResponse<ActionReportTemplateDto> { Success = false, StatusCode = StatusCodeOnFailure }
            : new ApiListResponse<ActionReportTemplateDto> { Success = true, Data = Templates.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id).ToList() });

    public Task<ApiResponse<ActionReportTemplateDto>> GetTemplateByIdAsync(int id, CancellationToken token = default)
        => Task.FromResult(new ApiResponse<ActionReportTemplateDto> { Success = true, Data = Templates.FirstOrDefault(t => t.Id == id) });

    public Task<ApiResponse<ActionReportTemplateDto>> CreateTemplateAsync(ActionReportTemplateCreateDto dto, CancellationToken token = default)
    {
        CreateCalls.Add(dto);
        if (FailWrite) return Task.FromResult(new ApiResponse<ActionReportTemplateDto> { Success = false, StatusCode = StatusCodeOnFailure });
        if (Templates.Any(t => string.Equals(t.Content.Trim(), dto.Content.Trim(), StringComparison.Ordinal)))
            return Task.FromResult(new ApiResponse<ActionReportTemplateDto> { Success = false, StatusCode = 409, Message = $"content '{dto.Content}' already exists" });

        var created = new ActionReportTemplateDto { Id = Templates.Count == 0 ? 1 : Templates.Max(t => t.Id) + 1, Content = dto.Content, DisplayOrder = dto.DisplayOrder };
        Templates.Add(created);
        return Task.FromResult(new ApiResponse<ActionReportTemplateDto> { Success = true, Data = created, StatusCode = 201 });
    }

    public Task<ApiResponse<ActionReportTemplateDto>> UpdateTemplateAsync(int id, ActionReportTemplateUpdateDto dto, CancellationToken token = default)
    {
        if (FailWrite) return Task.FromResult(new ApiResponse<ActionReportTemplateDto> { Success = false, StatusCode = StatusCodeOnFailure });
        var target = Templates.FirstOrDefault(t => t.Id == id);
        if (target is null) return Task.FromResult(new ApiResponse<ActionReportTemplateDto> { Success = false, StatusCode = 404 });
        if (dto.Content != null) target.Content = dto.Content;
        if (dto.DisplayOrder.HasValue) target.DisplayOrder = dto.DisplayOrder.Value;
        return Task.FromResult(new ApiResponse<ActionReportTemplateDto> { Success = true, Data = target, StatusCode = 200 });
    }

    public Task<ApiResponse<ActionReportTemplateDto>> ReplaceTemplateAsync(int id, ActionReportTemplateReplaceDto dto, CancellationToken token = default)
        => Task.FromResult(new ApiResponse<ActionReportTemplateDto> { Success = false, StatusCode = 404 });

    public Task<ApiResponse<object>> DeleteTemplateAsync(int id, CancellationToken token = default)
    {
        DeletedIds.Add(id);
        if (FailDelete) return Task.FromResult(new ApiResponse<object> { Success = false, StatusCode = StatusCodeOnFailure });
        Templates.RemoveAll(t => t.Id == id);
        return Task.FromResult(new ApiResponse<object> { Success = true, StatusCode = 200 });
    }

    public Task<ApiListResponse<ActionReportTemplateDto>> ReorderTemplatesAsync(IEnumerable<ActionReportTemplateReorderItemDto> items, CancellationToken token = default)
    {
        var list = items.ToList();
        ReorderCalls.Add(list);
        if (FailReorder) return Task.FromResult(new ApiListResponse<ActionReportTemplateDto> { Success = false, StatusCode = StatusCodeOnFailure });

        // 요청 id 중 하나라도 모르면 서버는 아무것도 바꾸지 않고 404 를 돌려준다(실제 계약).
        if (list.Any(i => Templates.All(t => t.Id != i.Id)))
            return Task.FromResult(new ApiListResponse<ActionReportTemplateDto> { Success = false, StatusCode = 404 });

        foreach (var entry in list)
        {
            var target = Templates.First(t => t.Id == entry.Id);
            target.DisplayOrder = entry.DisplayOrder;
        }
        var ordered = Templates.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Id).ToList();
        return Task.FromResult(new ApiListResponse<ActionReportTemplateDto> { Success = true, Data = ordered });
    }
}

/// <summary>시험용 씨앗.</summary>
public static class ActionReportTemplateSeed
{
    public static ActionReportTemplateDto Template(int id, string content, int displayOrder)
        => new() { Id = id, Content = content, DisplayOrder = displayOrder };
}
