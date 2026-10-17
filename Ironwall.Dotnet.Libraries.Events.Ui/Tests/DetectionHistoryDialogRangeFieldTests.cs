using System;
using System.IO;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : 탐지 이력 다이얼로그의 "기간지정" 입력이 두 개의 맨 mah:DateTimePicker
                  (영문 워터마크 · 미국식 포맷 · Teal 무아이콘 블록 · 라이트 텍스트박스)에서
                  c:DateTimeRangeField(Utils/Console/DateTimeRangeField.cs) 팝업 범위 선택으로
                  바뀐 것을 검증한다. 이 다이얼로그는 EventsConsolePreview 스냅샷 대상이 아니라서
                  (호스트 메인솔루션의 DetectionHistoryHostDialogView 가 감싼다) 픽셀로 확인할 수
                  없다 — 대신 XAML 소스에서 새 컨트롤이 VM 의 CustomStart/CustomEnd(둘 다 DateTime,
                  DetectionHistoryDialogViewModel.cs:295-304)에 TwoWay 로 물려 있는지, 그리고 옛
                  mah:DateTimePicker 가 완전히 사라졌는지를 직접 검사한다(UnitTest.cs 의 기존
                  "XAML 텍스트 직접 검사" 관례를 따른다).
   Created By   : GHLee
   Created On   : 2026-09-23
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class DetectionHistoryDialogRangeFieldTests
{
    private static string ReadDialogXaml()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..",
            "Views", "Dialogs", "DetectionHistoryDialogView.xaml");
        return File.ReadAllText(path);
    }

    [Fact]
    public void should_bind_custom_start_and_end_to_date_time_range_field_when_detection_history_dialog_renders()
    {
        // Arrange
        var xaml = ReadDialogXaml();

        // Assert — 옛 두 mah:DateTimePicker 는 남지 않는다(주석 안 언급은 예외).
        Assert.DoesNotContain("<mah:DateTimePicker", xaml);

        // Assert — 새 컨트롤이 VM 의 CustomStart/CustomEnd 에 TwoWay 로 물려 있다.
        Assert.Contains("<c:DateTimeRangeField", xaml);
        Assert.Contains("RangeStart=\"{Binding CustomStart, Mode=TwoWay}\"", xaml);
        Assert.Contains("RangeEnd=\"{Binding CustomEnd, Mode=TwoWay}\"", xaml);

        // Assert — 자동화 식별자가 살아 있다(키보드로 계속 포커스 가능한 트리거).
        Assert.Contains("FromAutomationId=\"Events.DetectionHistory.CustomStart\"", xaml);
        Assert.Contains("ToAutomationId=\"Events.DetectionHistory.CustomEnd\"", xaml);

        // Assert — "적용" 버튼(실제 조회 트리거, ApplyCustomCommand)은 그대로 남는다.
        Assert.Contains("Command=\"{Binding ApplyCustomCommand}\"", xaml);
    }

    [Fact]
    public void should_declare_date_time_range_field_namespace_when_detection_history_dialog_renders()
    {
        // Arrange
        var xaml = ReadDialogXaml();

        // Assert — c: 접두사가 DateTimeRangeField 가 실제로 사는 네임스페이스(Utils.Consoles)를 가리킨다.
        Assert.Contains(
            "xmlns:c=\"clr-namespace:Ironwall.Dotnet.Libraries.Utils.Consoles;assembly=Ironwall.Dotnet.Libraries.Utils\"",
            xaml);
    }
}
