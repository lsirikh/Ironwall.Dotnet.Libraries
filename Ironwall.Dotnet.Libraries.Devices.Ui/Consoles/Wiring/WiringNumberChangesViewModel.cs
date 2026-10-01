using Caliburn.Micro;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;

/// <summary>
/// 저장 전 "바뀌는 번호 표"(fence-wiring-editor FR-11 · R-1) — 서버 번호(<c>number_device</c>)가 바뀌는 센서를 전 → 후로 보이고
/// "현장 센서의 번호 설정과 같아야 합니다" 를 확인받는다. 취소가 첫 포커스(파괴적이지 않은 쪽).
/// </summary>
public sealed class WiringNumberChangesViewModel : Screen
{
    public WiringNumberChangesViewModel(string title, IReadOnlyList<NumberChange> changes, string warning, string details)
    {
        DisplayName = title;
        Title = title;
        Changes = changes ?? Array.Empty<NumberChange>();
        Warning = warning ?? string.Empty;
        Details = details ?? string.Empty;
    }

    public string Title { get; }
    public IReadOnlyList<NumberChange> Changes { get; }
    public string Warning { get; }
    public string Details { get; }

    /// <summary>"바뀌는 번호 5대".</summary>
    public string Heading => $"바뀌는 번호 {Changes.Count}대";

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
