using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;
/****************************************************************************
   Purpose      : 지도 메뉴 '종료'(Ctrl+E) 가 앱의 정상 종료 경로를 타는지 (완성도 P2 — SC-GIS-003)
   Created By   : GHLee
   Created On   : 2026-09-27
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 종전: <c>ExecuteExitApplication</c> 본문이 TODO 뿐이라 메뉴 '종료' · Ctrl+E 가 아무 일도 하지 않았다.
/// 지금: 타이틀바 ✕ 와 같은 종료 확인 팝업을 열고, [확인] 의 메시지로 호스트 셸이 앱을 닫는다.
/// </summary>
public class MapExitCommandTests
{
    private static (Mock<IEventAggregator> Mock, List<object> Published) Aggregator()
    {
        var published = new List<object>();
        var mock = new Mock<IEventAggregator>();
        mock.Setup(e => e.PublishAsync(It.IsAny<object>(), It.IsAny<Func<Func<Task>, Task>>(), It.IsAny<CancellationToken>()))
            .Callback<object, Func<Func<Task>, Task>, CancellationToken>((message, _, _) => published.Add(message))
            .Returns(Task.CompletedTask);
        return (mock, published);
    }

    [Fact]
    public void should_open_the_exit_confirm_popup_when_the_map_menu_exit_is_invoked()
    {
        // Arrange — 무거운 생성자를 건너뛰고 종료 처리기만 돌린다(처리기는 이벤트 집계기 하나만 쓴다).
        var (mock, published) = Aggregator();
        var vm = (MapViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MapViewModel));
        typeof(BasePanelViewModel)
            .GetField("_eventAggregator", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(vm, mock.Object);
        var handler = typeof(MapViewModel).GetMethod("ExecuteExitApplication", BindingFlags.Instance | BindingFlags.NonPublic)!;

        // Act
        handler.Invoke(vm, new object?[] { null });

        // Assert
        var popup = Assert.IsType<OpenConfirmPopupMessageModel>(Assert.Single(published));
        Assert.IsType<ExitProgramMessageModel>(popup.MessageModel);
        Assert.Equal("프로그램을 종료하시겠습니까?", popup.Explain);
        Assert.Equal("종료 확인", popup.Title);
    }

    [Fact]
    public async Task should_ask_for_confirmation_instead_of_exiting_when_exit_is_requested()
    {
        var (mock, published) = Aggregator();

        await MapViewModel.RequestApplicationExitAsync(mock.Object);

        // 곧바로 끄는 메시지(ExitProgramMessageModel)를 직접 보내지 않는다 — 확인 팝업 안에 싣는다.
        var only = Assert.Single(published);
        Assert.IsNotType<ExitProgramMessageModel>(only);
        Assert.IsType<ExitProgramMessageModel>(Assert.IsType<OpenConfirmPopupMessageModel>(only).MessageModel);
    }

    [Fact]
    public async Task should_do_nothing_when_there_is_no_event_aggregator()
    {
        await MapViewModel.RequestApplicationExitAsync(null);   // 던지지 않는다
    }
}
