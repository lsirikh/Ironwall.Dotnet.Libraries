using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Events;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Moq;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 조치보고 창 사진 — ① 크게 보기 크기(창 85% 안 · 비율 유지) ② 창을 연 뒤 늦게 온 사진이
                  칸에 반영된다(연 순간 '사진 없음' 으로 굳지 않음) · 창을 닫으면 카드 구독을 끊는다
                  ③ 뷰 XAML 이 사진을 누를 수 있는 단추 + 확대 팝업으로 그리고 Caliburn 지시자를 새로 달지 않는다.
   Created By   : Claude
   Created On   : 2026-10-03
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
[Collection("IoC-Dependent")]   // IoC.GetInstance 는 전역 정적 — 스텁을 쓰는 다른 시험과 병렬로 돌면 서로 덮는다
public class ActionReportThumbnailZoomTests : IDisposable
{
    private readonly Mock<IEventAggregator> _ea = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Func<Type, string, object> _previousGetInstance = IoC.GetInstance;

    public ActionReportThumbnailZoomTests()
    {
        IoC.GetInstance = (type, key) =>
        {
            if (type == typeof(IEventAggregator)) return _ea.Object;
            if (type == typeof(ILogService)) return _log.Object;
            return null!;
        };
    }

    public void Dispose() => IoC.GetInstance = _previousGetInstance;

    #region - ① 크게 보기 크기 -
    [Fact]
    public void should_fit_a_wide_photo_to_85_percent_of_the_window_width_keeping_its_ratio()
    {
        var size = ThumbnailZoomRules.Fit(320, 180, 1600, 1000);

        Assert.Equal(1360, size.Width);      // 1600 × 0.85
        Assert.Equal(765, size.Height);      // 1360 × 180/320
    }

    [Fact]
    public void should_fit_a_tall_photo_to_85_percent_of_the_window_height()
    {
        var size = ThumbnailZoomRules.Fit(300, 600, 1600, 1000);

        Assert.Equal(850, size.Height);
        Assert.Equal(425, size.Width);
    }

    [Fact]
    public void should_enlarge_a_small_thumbnail_beyond_its_150_box()
    {
        var size = ThumbnailZoomRules.Fit(160, 120, 1280, 800);

        Assert.True(size.Width > 150 * 3 && size.Height > 150 * 3);
    }

    [Theory]
    [InlineData(double.NaN, 100)]
    [InlineData(0, 0)]
    [InlineData(-1, 50)]
    public void should_fall_back_to_a_default_window_when_the_window_size_is_unknown(double hostWidth, double hostHeight)
    {
        var size = ThumbnailZoomRules.Fit(400, 300, hostWidth, hostHeight);

        var expected = ThumbnailZoomRules.Fit(400, 300, ThumbnailZoomRules.FallbackHost.Width, ThumbnailZoomRules.FallbackHost.Height);
        Assert.Equal(expected, size);
        Assert.True(size.Width > 0 && size.Height > 0);
    }

    [Fact]
    public void should_assume_4_by_3_when_the_photo_size_is_unknown()
    {
        var size = ThumbnailZoomRules.Fit(0, double.NaN, 1000, 1000);

        Assert.Equal(850, size.Width);
        Assert.Equal(637, size.Height);      // floor(850 × 3/4)
    }
    #endregion

    #region - ② 늦게 온 사진 -
    private static DetectionEventCardViewModel Card(int id, string? thumbnail = null)
    {
        var model = new Mock<IDetectionEventModel>();
        model.SetupGet(m => m.Id).Returns(id);
        model.SetupGet(m => m.MessageType).Returns(EnumEventType.Intrusion);
        model.SetupProperty(m => m.Status);
        model.SetupProperty(m => m.Thumbnail, thumbnail);
        model.SetupProperty(m => m.FrameWidth);
        model.SetupProperty(m => m.FrameHeight);
        return new DetectionEventCardViewModel(model.Object);
    }

    private DetectionReportDialogViewModel Open(DetectionEventCardViewModel card)
    {
        var dialog = new DetectionReportDialogViewModel(_ea.Object, _log.Object);
        dialog.UpdateData(card, new Mock<IAccountModel>().Object);
        return dialog;
    }

    [Fact]
    public void should_show_the_photo_that_arrives_after_the_dialog_opened()
    {
        var card = Card(70);
        var dialog = Open(card);
        var editor = Assert.IsType<DetectionSelectionViewModel>(dialog.SelectedItemEditor);
        Assert.False(editor.HasThumbnail);
        var raised = new List<string?>();
        editor.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        card.ApplyThumbnailUpdate("http://127.0.0.1:9/api/thumbnails/images/70.jpg", 1920, 1080);

        Assert.True(editor.HasThumbnail);
        Assert.Contains(nameof(DetectionSelectionViewModel.HasThumbnail), raised);
        Assert.Contains(nameof(DetectionSelectionViewModel.Thumbnail), raised);
    }

    [Fact]
    public async Task should_stop_listening_to_the_card_when_the_dialog_closes()
    {
        var card = Card(71);
        var dialog = Open(card);
        await ((IActivate)dialog).ActivateAsync();
        await ((IDeactivate)dialog).DeactivateAsync(true);
        var editor = (DetectionSelectionViewModel)dialog.SelectedItemEditor!;
        var raised = new List<string?>();
        editor.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        card.ApplyThumbnailUpdate("http://127.0.0.1:9/api/thumbnails/images/71.jpg", null, null);

        Assert.DoesNotContain(nameof(DetectionSelectionViewModel.HasThumbnail), raised);
    }

    [Fact]
    public void should_stop_listening_to_the_previous_card_when_reopened_for_another_event()
    {
        var first = Card(72);
        var dialog = Open(first);
        dialog.UpdateData(Card(73), new Mock<IAccountModel>().Object);
        var editor = (DetectionSelectionViewModel)dialog.SelectedItemEditor!;
        var raised = new List<string?>();
        editor.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        first.ApplyThumbnailUpdate("http://127.0.0.1:9/api/thumbnails/images/72.jpg", null, null);

        Assert.DoesNotContain(nameof(DetectionSelectionViewModel.HasThumbnail), raised);
        Assert.False(editor.HasThumbnail);
    }
    #endregion

    #region - ③ 뷰 모양 -
    private static string Xaml([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "..", "Views", "Dialogs", "DetectionReportDialogView.xaml"));

    [Fact]
    public void should_draw_the_photo_as_a_clickable_button_with_a_zoom_popup()
    {
        var xaml = Xaml();

        Assert.Contains("AutomationProperties.AutomationId=\"Events.DetectionActionReport.ThumbnailZoom\"", xaml);
        Assert.Contains("Click=\"OnThumbnailClick\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"Events.DetectionActionReport.ThumbnailLarge\"", xaml);
        Assert.Contains("<Popup", xaml);
        Assert.Contains("StaysOpen=\"False\"", xaml);
        // 작은 사진 칸은 그대로 남는다(사진 보기)
        Assert.Contains("AutomationProperties.AutomationId=\"Events.DetectionActionReport.Thumbnail\"", xaml);
    }

    [Fact]
    public void should_not_add_new_caliburn_names_outside_templates()
    {
        var xaml = Xaml();

        // 뷰 바깥(템플릿 밖) x:Name 은 Caliburn 지시자 넷뿐 — 템플릿 안 이름(Frame)은 템플릿 이름 범위라 Caliburn 이 보지 않는다.
        var names = System.Text.RegularExpressions.Regex.Matches(xaml, "x:Name=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value).Distinct().OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "ClickCancel", "ClickOk", "Frame", "RadioButtonEtc", "SelectedItemEditor" }, names);
    }
    #endregion
}
