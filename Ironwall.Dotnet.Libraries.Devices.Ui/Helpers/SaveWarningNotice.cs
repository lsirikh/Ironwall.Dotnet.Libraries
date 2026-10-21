using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Api.Helpers;
using Ironwall.Dotnet.Libraries.Messages.Dto.Bases;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;

/// <summary>
/// 저장은 됐지만 서버가 남긴 경고(<c>warnings[]</c>)를 운영자에게 알리는 공용 입구(GOP API 8.0.15 대조 G17).
/// </summary>
/// <remarks>
/// <para>해석은 <see cref="SaveWarnings"/>(<c>UNMATCHED_THRESHOLD</c> · <c>SERVER_ID_MISSING</c> 만, 쉬운 말)에 맡기고,
/// 여기서는 기존 안내 창 통로(<see cref="OpenInfoPopupMessageModel"/> — 장비 패널 · 삭제 결과와 같은 길)로 띄우기만 한다.
/// 직접 메시지 상자를 띄우지 않는다.</para>
/// </remarks>
internal static class SaveWarningNotice
{
    /// <summary>안내 창 제목.</summary>
    internal const string Title = "저장 안내";

    /// <summary>알릴 경고가 있으면 안내 창을 띄우고 그 글을 돌려준다. 없으면 <c>null</c>(아무것도 띄우지 않는다).</summary>
    public static async Task<string?> AnnounceAsync(IEventAggregator? eventAggregator, IEnumerable<ResponseWarningDto>? warnings, ILogService? log = null)
    {
        var text = SaveWarnings.ToText(SaveWarnings.Collect(warnings));
        if (text == null || eventAggregator == null) return text;
        try
        {
            await eventAggregator.PublishOnCurrentThreadAsync(new OpenInfoPopupMessageModel { Title = Title, Explain = text });
        }
        catch (Exception ex)
        {
            // 알림 실패는 저장 결과를 바꾸지 않는다 — 서버에는 이미 저장됐다.
            log?.Warning($"[{nameof(SaveWarningNotice)}] 저장 안내를 띄우지 못했습니다: {ex.Message}");
        }
        return text;
    }
}
