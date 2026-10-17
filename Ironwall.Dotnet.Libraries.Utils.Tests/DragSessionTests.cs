using Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag;
using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// <see cref="DragSession"/> 의 공개 입구 <see cref="DragSession.Begin"/> — 커널 밖의 캡처 드래그(부대 관계도 캔버스)도
/// "끄는 중" 을 알린다. 표는 프로세스 전역이라 이 반은 다른 시험과 <b>동시에 돌지 않는다</b>(짝 맞춤을 절대값으로 본다).
/// </summary>
[Collection(DragSessionCollection.NAME)]
public class DragSessionTests
{
    [Fact]
    public void should_be_active_while_a_token_is_held_and_inactive_after_it_is_disposed()
    {
        Assert.False(DragSession.IsActive);

        var token = DragSession.Begin();
        var during = DragSession.IsActive;
        token.Dispose();

        Assert.True(during);
        Assert.False(DragSession.IsActive);
    }

    [Fact]
    public void should_exit_only_once_when_the_same_token_is_disposed_twice()
    {
        var outer = DragSession.Begin();
        var inner = DragSession.Begin();

        inner.Dispose();
        inner.Dispose();                       // 두 번째는 아무것도 하지 않는다 — 바깥 끌기를 끝내 버리면 안 된다

        Assert.True(DragSession.IsActive);
        outer.Dispose();
        Assert.False(DragSession.IsActive);
    }

    [Fact]
    public void should_stay_active_until_every_overlapping_token_is_disposed()
    {
        var first = DragSession.Begin();
        var second = DragSession.Begin();

        first.Dispose();
        var afterFirst = DragSession.IsActive;
        second.Dispose();

        Assert.True(afterFirst);
        Assert.False(DragSession.IsActive);
    }

    [Fact]
    public void should_not_go_negative_when_tokens_are_disposed_more_than_begun()
    {
        var token = DragSession.Begin();
        token.Dispose();
        token.Dispose();
        token.Dispose();

        var next = DragSession.Begin();        // 음수로 내려갔다면 이 끌기가 "진행 중 아님" 으로 읽힌다
        var active = DragSession.IsActive;
        next.Dispose();

        Assert.True(active);
        Assert.False(DragSession.IsActive);
    }
}

/// <summary>프로세스 전역 상태를 절대값으로 보는 시험 — 다른 반과 동시에 돌리지 않는다.</summary>
[CollectionDefinition(NAME, DisableParallelization = true)]
public sealed class DragSessionCollection
{
    public const string NAME = "DragSession (process-global)";
}
