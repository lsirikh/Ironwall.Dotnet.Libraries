using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Tray;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 조치보고 창이 떠 있는 동안 다른 곳에서 문구 목록이 바뀌면(SYNC_ACTION_REPORT_TEMPLATE) —
                  새 목록으로 갈아 끼우되, 사람이 고른 문구가 빠졌으면 고른 것을 말없이 바꾸지 않는다.
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
[Collection("IoC-Dependent")]
public class ActionReportDialogPhraseSyncTests : IDisposable
{
    private ActionReportPhraseSet _served = new(new[] { "야생동물출현", "강풍/폭우" }, true);
    private readonly Mock<IActionReportPhraseSource> _source = new();

    public ActionReportDialogPhraseSyncTests()
    {
        _source.SetupGet(s => s.LastKnown).Returns(() => _served);
        _source.Setup(s => s.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _served);
        IoC.GetInstance = (type, _) => type == typeof(IActionReportPhraseSource) ? _source.Object : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };
        PlatformProvider.Current = new DefaultPlatformProvider();
    }

    public void Dispose()
    {
        IoC.GetInstance = null!;
        IoC.GetAllInstances = null!;
        IoC.BuildUp = null!;
    }

    private static async Task<DetectionReportDialogViewModel> OpenAsync()
    {
        var dialog = new DetectionReportDialogViewModel(new EventAggregator(), new Mock<ILogService>().Object);
        await ((IActivate)dialog).ActivateAsync();
        return dialog;
    }

    private static string?[] Names(DetectionReportDialogViewModel dialog) => dialog.CollectionActionItem!.Select(i => i.Name).ToArray();

    [Fact]
    public async Task should_show_the_new_phrases_when_the_templates_change_while_the_dialog_is_open()
    {
        var dialog = await OpenAsync();
        _served = new ActionReportPhraseSet(new[] { "야생동물출현", "강풍/폭우", "순찰 출동" }, true);

        await dialog.HandleAsync(new ActionReportTemplatesChangedMessage("CREATED", 7), CancellationToken.None);
        await dialog.PhraseRefreshTask;

        Assert.Equal(new[] { "야생동물출현", "강풍/폭우", "순찰 출동" }, Names(dialog));
        Assert.Equal("야생동물출현", dialog.SelectableItemViewModel?.Name);    // 고른 문구는 그대로
    }

    [Fact]
    public async Task should_keep_the_dialog_phrases_when_the_chosen_phrase_was_removed_elsewhere()
    {
        var dialog = await OpenAsync();
        dialog.CollectionActionItem!.Single(i => i.Name == "강풍/폭우").IsSelected = true;
        Assert.Equal("강풍/폭우", dialog.SelectableItemViewModel?.Name);
        _served = new ActionReportPhraseSet(new[] { "야생동물출현" }, true);

        await dialog.HandleAsync(new ActionReportTemplatesChangedMessage("DELETED", 2), CancellationToken.None);
        await dialog.PhraseRefreshTask;

        Assert.Equal(new[] { "야생동물출현", "강풍/폭우" }, Names(dialog));
        Assert.Equal("강풍/폭우", dialog.SelectableItemViewModel?.Name);
    }
}
