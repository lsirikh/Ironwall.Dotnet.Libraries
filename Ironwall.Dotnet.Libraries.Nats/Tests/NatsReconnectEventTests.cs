using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Nats.Tests;

/****************************************************************************
    Purpose      : G53 — NATS 재연결 사건(첫 연결 제외 1회).
                   연결 상태 전이는 가짜로 만든다(OnConnectionOpened 를 직접 부름) — 서버 없이 판정.
    Created By   : Claude
    Created On   : 2026-10-21
    Department   : SW Team
    Company      : Sensorway Co., Ltd.
 ****************************************************************************/
public class NatsReconnectEventTests
{
    [Fact]
    public void 첫_연결은_알리지_않고_다시_열릴_때만_1회_알린다()
    {
        var nats = new NatsService();
        var count = 0;
        nats.Reconnected += (_, _) => count++;

        nats.OnConnectionOpened();          // 첫 연결
        Assert.Equal(0, count);

        nats.OnConnectionOpened();          // 끊겼다가 다시 열림
        Assert.Equal(1, count);

        nats.OnConnectionOpened();          // 또 한 번 재연결
        Assert.Equal(2, count);
    }

    [Fact]
    public void 새_연결을_만들면_다시_첫_연결부터_센다()
    {
        var nats = new NatsService();
        var count = 0;
        nats.Reconnected += (_, _) => count++;

        nats.OnConnectionOpened();
        nats.OnConnectionOpened();
        Assert.Equal(1, count);

        // 재로그인 등으로 연결을 새로 만든다(서버 접속은 하지 않는 동기 Connect) — 새 연결의 첫 열림은 재연결이 아니다.
        nats.Connect(new NatsSetupModel { IpAddressNats = "127.0.0.1", PortNats = 4222, DefaultSubjectNats = "test.>" });
        nats.OnConnectionOpened();
        Assert.Equal(1, count);
    }

    [Fact]
    public void 받는_쪽_예외는_서비스_밖으로_새지_않는다()
    {
        var nats = new NatsService();
        nats.Reconnected += (_, _) => throw new InvalidOperationException("받는 쪽 오류");
        nats.OnConnectionOpened();
        var ex = Record.Exception(() => nats.OnConnectionOpened());
        Assert.Null(ex);
    }

    [Fact]
    public void 인터페이스로도_구독할_수_있다()
    {
        INatsService nats = new NatsService();
        var count = 0;
        nats.Reconnected += (_, _) => count++;
        ((NatsService)nats).OnConnectionOpened();
        ((NatsService)nats).OnConnectionOpened();
        Assert.Equal(1, count);
    }
}
