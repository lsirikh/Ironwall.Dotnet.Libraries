using System.Reflection;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Nats.Models;
using Ironwall.Dotnet.Libraries.Nats.Services;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Nats.Tests;

/****************************************************************************
   Purpose      : API 계약 결함 회귀 방지 — FR-05(GLOBAL_CMDS 전역 자원 구독).
                  서버 `db_monitor/main.py` GLOBAL_UNIT_TOKEN="global" 로 발행되는
                  5종(SYNC_CATALOG 등)을 우리 기본 구독(`{도메인}.{부대}.{서브시스템}.>`)이
                  받지 못해 배포 당일 조용히 끊기는 문제의 회귀 방지.
                  `_additionalSubjects` 는 protected(공개 접근자 없음) 이므로
                  리플렉션으로 관찰한다(내부 구현 상태 확인 — 관측 가능한 필드 자체가
                  RegisterSubscribers 가 실제 구독하는 대상 목록이라 계약의 일부로 간주).
                  네트워크 I/O 없음 — 동기 Connect()는 NatsConnection 객체만 구성하고
                  실제 연결은 지연 수행되므로 외부 의존 없이 헤드리스로 검증 가능하다.
   Created By   : GHLee
   Created On   : 2026-09-18
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public class NatsServiceGlobalSubjectTests
{
    private static List<string> GetAdditionalSubjects(NatsService service)
    {
        // protected 필드 — 선언 위치가 제네릭 베이스(MessageService<INatsService>)라
        // 계층을 직접 순회해 안전하게 취득한다.
        for (var t = service.GetType(); t is not null; t = t.BaseType)
        {
            var field = t.GetField("_additionalSubjects", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field is not null)
                return (List<string>)field.GetValue(service)!;
        }
        throw new InvalidOperationException("_additionalSubjects field not found — 리플렉션 대상 필드명이 바뀌었는지 확인.");
    }

    private static NatsSetupModel BuildSetupModel(string domain, string group, string subsystem)
        => new()
        {
            IpAddressNats = "127.0.0.1",
            PortNats = 4222,
            DomainNats = domain,
            GroupNats = group,
            SubsystemNats = subsystem,
            ConnectionTimeoutNats = 1000,
        };

    [Fact]
    public void should_add_global_subject_when_domain_and_group_configured()
    {
        // Arrange
        var service = new NatsService(new LogService());
        var setupModel = BuildSetupModel("sensorway", "unit001", "gis");

        // Act — 동기 Connect: NatsConnection 객체 구성만 하고 실제 네트워크 I/O는 지연 수행됨.
        service.Connect(setupModel);

        // Assert
        var subjects = GetAdditionalSubjects(service);
        Assert.Contains("sensorway.global.>", subjects);
    }

    [Fact]
    public void should_add_broadcast_subject_when_domain_and_group_configured()
    {
        // Arrange — 기존 broadcastSubject(all.>) 동작 회귀 방지(같은 가드에서 global과 나란히 추가됨).
        var service = new NatsService(new LogService());
        var setupModel = BuildSetupModel("sensorway", "unit001", "gis");

        // Act
        service.Connect(setupModel);

        // Assert
        var subjects = GetAdditionalSubjects(service);
        Assert.Contains("sensorway.unit001.all.>", subjects);
    }

    [Fact]
    public async Task should_add_global_subject_when_connected_asynchronously()
    {
        // Arrange — ConnectAsync 경로도 동일 가드가 있어야 한다(동기 Connect와 로직 중복 구현).
        var service = new NatsService(new LogService());
        var setupModel = BuildSetupModel("sensorway", "unit001", "gis");

        // Act
        await service.ConnectAsync(setupModel);

        // Assert
        var subjects = GetAdditionalSubjects(service);
        Assert.Contains("sensorway.global.>", subjects);
    }

    [Fact]
    public void should_not_add_additional_subjects_when_domain_missing()
    {
        // Arrange — 가드 조건(도메인/그룹 모두 필요) 회귀 방지: 단일 부대 미구성 배포는 무변화여야 한다.
        var service = new NatsService(new LogService());
        var setupModel = BuildSetupModel(string.Empty, "unit001", "gis");

        // Act
        service.Connect(setupModel);

        // Assert
        var subjects = GetAdditionalSubjects(service);
        Assert.Empty(subjects);
    }

    [Fact]
    public void should_not_duplicate_global_subject_when_reconnecting()
    {
        // Arrange — Connect 재호출 시 _additionalSubjects.Clear() 후 재구성되어 중복 누적되지 않아야 한다.
        var service = new NatsService(new LogService());
        var setupModel = BuildSetupModel("sensorway", "unit001", "gis");

        // Act
        service.Connect(setupModel);
        service.Connect(setupModel);

        // Assert
        var subjects = GetAdditionalSubjects(service);
        Assert.Single(subjects, s => s == "sensorway.global.>");
    }
}
