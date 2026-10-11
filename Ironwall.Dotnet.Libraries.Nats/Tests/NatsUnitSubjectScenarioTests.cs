using System.Reflection;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Nats.Tests;

/****************************************************************************
   Purpose      : 카탈로그 §2 EVM-097 — 다른 부대(unit002)의 ACTION_REPORT 는 이 GIS(unit001)에 닿지 않는다.
                  라이브러리 몫은 "무엇을 구독하는가" 다: 동기 Connect() 가 만든 구독 목록(기본 subject + 추가 subject)이
                  자기 부대 · 전역(global)만 덮고 남의 부대 subject 와는 NATS 와일드카드 규칙으로도 겹치지 않는지 본다.
                  실제 거름(배달 여부)은 NATS 서버가 한다 — 네트워크 I/O 없음(Connect 는 연결 객체만 만든다).
   Created By   : Claude
   Created On   : 2026-10-11
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class NatsUnitSubjectScenarioTests
{
    private static IReadOnlyList<string> SubscribedSubjects(NatsService service)
    {
        // 기본 subject 는 공개 Subject, 추가 subject 는 보호 필드(_additionalSubjects) — RegisterSubscribers 가 구독하는 바로 그 목록.
        for (var t = service.GetType(); t is not null; t = t.BaseType)
        {
            var field = t.GetField("_additionalSubjects", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field is not null)
                return new[] { service.Subject }.Concat((List<string>)field.GetValue(service)!).ToList();
        }
        throw new InvalidOperationException("_additionalSubjects 필드를 찾지 못했다 — 이름이 바뀌었는지 확인.");
    }

    /// <summary>NATS subject 와일드카드 규칙 — <c>*</c> 는 토큰 하나, <c>&gt;</c> 는 끝에서 토큰 하나 이상.</summary>
    private static bool Matches(string pattern, string subject)
    {
        var p = pattern.Split('.');
        var s = subject.Split('.');
        for (var i = 0; i < p.Length; i++)
        {
            if (p[i] == ">") return s.Length > i;
            if (i >= s.Length) return false;
            if (p[i] != "*" && !string.Equals(p[i], s[i], StringComparison.Ordinal)) return false;
        }
        return p.Length == s.Length;
    }

    [Theory]
    [Trait("Scenario", "EVM-097")]
    [InlineData("sensorway.unit002.all.event.action-report", false)]   // 다른 부대 조치보고 — 구독 밖
    [InlineData("sensorway.unit002.all.event.detect", false)]
    [InlineData("sensorway.unit002.gis.event.action-report", false)]
    [InlineData("sensorway.unit001.all.event.action-report", true)]    // 자기 부대 — 받는다
    [InlineData("sensorway.unit001.gis.event.detect", true)]
    [InlineData("sensorway.global.all.sync.catalog", true)]            // 전역 자원은 부대 토큰이 아니라 global
    public void should_subscribe_only_own_unit_and_global_so_other_unit_action_report_never_arrives_evm097(string published, bool expected)
    {
        var service = new NatsService(new LogService());
        service.Connect(new NatsSetupModel
        {
            IpAddressNats = "127.0.0.1", PortNats = 4222,
            DomainNats = "sensorway", GroupNats = "unit001", SubsystemNats = "gis",
            ConnectionTimeoutNats = 1000,
        });

        var subjects = SubscribedSubjects(service);

        Assert.Equal(new[] { "sensorway.unit001.gis.>", "sensorway.unit001.all.>", "sensorway.global.>" }, subjects);
        Assert.Equal(expected, subjects.Any(pattern => Matches(pattern, published)));
    }
}
