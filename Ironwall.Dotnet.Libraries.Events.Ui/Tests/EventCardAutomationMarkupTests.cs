using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 이벤트 카드 목록 · 카드 · 조치보고 창 XAML — 자동화 식별자(AutomationId)와 Caliburn 지시자 모양(WP-1 ⑫ · ⑭).
                  x:Name 은 Caliburn 바인딩 지시자라 건드리지 않고, 자동화는 AutomationProperties.AutomationId 로만 잡는다.
                  소스 파일을 읽는 시험이라 CallerFilePath 로 찾는다 — 출력 폴더가 저장소 밖이어도 돈다.
   Created By   : Claude
   Created On   : 2026-09-30
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class EventCardAutomationMarkupTests
{
    private static string Xaml(string relative, [CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "..", "Views", relative));

    private const string CardList = "Panels/EventCardListPanelView.xaml";

    [Fact]
    public void should_close_every_caliburn_action_parenthesis_when_the_card_list_attaches_messages()
    {
        var xaml = Xaml(CardList);

        // [Action Name(args)] — 여는 괄호가 있으면 ']' 전에 닫혀야 한다. 종전: "[Action OnClickButtonActionAll($source, $eventArgs]"
        var actions = Regex.Matches(xaml, @"\[Action\s+([^\]]*)\]");
        Assert.NotEmpty(actions);
        foreach (Match action in actions)
        {
            var body = action.Groups[1].Value;
            Assert.Equal(body.Count(c => c == '('), body.Count(c => c == ')'));
        }
    }

    [Fact]
    public void should_mark_the_card_list_root_and_the_report_all_button_for_automation()
    {
        var xaml = Xaml(CardList);

        Assert.Contains("AutomationProperties.AutomationId=\"Events.CardList.Root\"", xaml);
        // [전체 조치보고] — x:Name(Caliburn 지시자)은 그대로 두고 계측 id 만 더한다.
        Assert.Matches(new Regex(@"x:Name=""ButtonActionAll""\s+AutomationProperties\.AutomationId=""Events\.CardList\.ReportAll"""), xaml);
    }

    [Theory]
    [InlineData("Events/DetectionEventCardView.xaml")]
    [InlineData("Events/MalfunctionEventCardView.xaml")]
    public void should_mark_each_card_and_its_report_button_by_kind_and_id(string file)
    {
        var xaml = Xaml(file);

        Assert.Contains("AutomationProperties.AutomationId=\"{Binding AutomationKey, Mode=OneWay}\"", xaml);
        Assert.Contains("AutomationProperties.AutomationId=\"{Binding ReportAutomationKey, Mode=OneWay}\"", xaml);
    }

    [Theory]
    [InlineData("Detection")]
    [InlineData("Malfunction")]
    public void should_mark_the_report_dialog_root_buttons_and_phrases_and_keep_the_caliburn_names(string kind)
    {
        var xaml = Xaml($"Dialogs/{kind}ReportDialogView.xaml");
        var key = $"Events.{kind}ActionReport";

        foreach (var id in new[] { "Root", "Ok", "Cancel", "Etc" })
            Assert.Contains($"AutomationProperties.AutomationId=\"{key}.{id}\"", xaml);
        Assert.Contains($"AutomationProperties.AutomationId=\"{{Binding Id, StringFormat={key}.Phrase.{{0}}}}\"", xaml);
        foreach (var name in new[] { "ClickOk", "ClickCancel", "RadioButtonEtc", "SelectedItemEditor" })
            Assert.Contains($"x:Name=\"{name}\"", xaml);
        // 보내는 중에는 창 틀도 바쁨 — Enter 가 두 번째 조치를 만들지 않게
        Assert.Contains("IsBusy=\"{Binding IsSending", xaml);
        Assert.Contains("Path=\"DialogNotice\"", xaml);
    }
}
