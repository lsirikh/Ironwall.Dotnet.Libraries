using System;
using System.Collections.Generic;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Watchdog.Tests;
/****************************************************************************
   Purpose      : ProcessGuard.KillTree — 와치독이 대상의 자식일 때도 프리즈 대상을 끝낸다.
                  설치판 10-15 17:29~17:32: Kill(entireProcessTree:true) 가 "호출한 프로세스가 든 트리"로 6회 거부돼
                  멈춘 GIS 를 끝내지 못했다(watchdog.log "kill 실패: Cannot be used to terminate a process tree containing the calling process").
****************************************************************************/
public class ProcessGuardKillTests
{
    [Fact]
    public void should_kill_target_only_when_tree_kill_contains_the_watchdog()
    {
        // Arrange — 트리째 끝내기는 실제 .NET 과 같은 예외로 거부한다
        var calls = new List<bool>();
        void Kill(bool entire)
        {
            calls.Add(entire);
            if (entire) throw new InvalidOperationException("Cannot be used to terminate a process tree containing the calling process.");
        }

        // Act
        var wholeTree = ProcessGuard.KillTree(Kill);

        // Assert — 트리 시도 뒤 본체만 끝냈다
        Assert.False(wholeTree);
        Assert.Equal(new[] { true, false }, calls);
    }

    [Fact]
    public void should_kill_whole_tree_when_watchdog_is_outside_the_tree()
    {
        var calls = new List<bool>();

        var wholeTree = ProcessGuard.KillTree(entire => calls.Add(entire));

        Assert.True(wholeTree);
        Assert.Equal(new[] { true }, calls);
    }

    [Fact]
    public void should_propagate_other_failures_when_target_kill_fails_for_another_reason()
    {
        // 권한 등 다른 실패는 삼키지 않는다 — 호출부(KillIfMatches)가 'kill 실패' 로 기록한다
        Assert.Throws<System.ComponentModel.Win32Exception>(() =>
            ProcessGuard.KillTree(_ => throw new System.ComponentModel.Win32Exception(5)));
    }
}
