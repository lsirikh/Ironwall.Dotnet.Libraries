using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Tests.Grants;

/****************************************************************************
   Purpose   : Grant 시뮬레이션 하네스 — 헤드리스(xUnit)에서 VM 구동/캡처 인프라.
   구성      : RecordingEventAggregator(발행순서 캡처) · RecordingLog(Error/Info 캡처)
                · TestGrantPanel(OnActivate 노출) · SimLog(단계 로그) · GrantFixtures(시드).
   근거      : ViewModel.Tests/CrudEnvelopeTests 의 검증된 가짜 EA 패턴 재사용.
****************************************************************************/

/// <summary>발행 메시지를 순서대로 기록하는 가짜 EA(Dispatcher 불요). 구독/해제는 no-op.</summary>
internal sealed class RecordingEventAggregator : IEventAggregator
{
    private readonly object _lock = new();
    public List<object> Published { get; } = new();

    public bool HandlerExistsFor(Type messageType) => false;
    public void Subscribe(object subscriber, Func<Func<Task>, Task> marshal) { }
    public void Unsubscribe(object subscriber) { }

    public Task PublishAsync(object message, Func<Func<Task>, Task> marshal, CancellationToken cancellationToken = default)
    {
        lock (_lock) { Published.Add(message); }
        return Task.CompletedTask;
    }

    // 편의: 특정 타입만/최근 발행 조회
    public IEnumerable<T> OfType<T>() => Published.OfType<T>();
    public T? Last<T>() where T : class => Published.OfType<T>().LastOrDefault();
    public void ClearPublished() { lock (_lock) { Published.Clear(); } }
}

/// <summary>Error/Info 로그를 캡처(예외 경로에서 VM이 로깅했는지 검증).</summary>
internal sealed class RecordingLog : ILogService
{
#pragma warning disable CS0067 // 이벤트 미사용
    public event EventHandler<LogEventArgs>? LogEvent;
#pragma warning restore CS0067
    public List<string> Errors { get; } = new();
    public List<string> Infos { get; } = new();
    public void Error(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Errors.Add(msg);
    public void Info(string msg, string memberName = "", string filePath = "", int lineNumber = 0) => Infos.Add(msg);
    public void Warning(string msg, string memberName = "", string filePath = "", int lineNumber = 0) { }
}

/// <summary>protected OnActivateAsync 를 테스트에 노출(부팅 로드 경로 검증).</summary>
internal sealed class TestGrantPanel : GrantManagementPanelViewModel
{
    public TestGrantPanel(IEventAggregator ea, ILogService log,
        Ironwall.Dotnet.Libraries.Accounts.Api.Services.IAccountApiService api)
        : base(ea, log, api) { }

    /// <summary>OnActivate(=base.OnActivate + LoadAccountsAndGroups + LoadAllGrants) 실행.</summary>
    public Task ActivateForTestAsync() => OnActivateAsync(CancellationToken.None);
}

/// <summary>단계별 PASS/FAIL 로그 + 집계. 2회 실행 결정론 비교의 단위.</summary>
internal sealed class SimLog
{
    public List<string> Lines { get; } = new();
    public int Pass { get; private set; }
    public int Fail { get; private set; }
    /// <summary>결정론 비교 키(순서 보존): "PASS|SCEN-ID" 형태.</summary>
    public List<string> Outcomes { get; } = new();
    public List<string> Failures { get; } = new();
    private int _step;

    public void Section(string title)
    {
        Lines.Add("");
        Lines.Add($"── {title} ──");
    }

    public void Info(string msg) => Lines.Add($"    · {msg}");

    public void Check(string id, string desc, bool ok, string detail = "")
    {
        _step++;
        if (ok) Pass++; else { Fail++; Failures.Add($"{id} — {desc}"); }
        Outcomes.Add($"{(ok ? "PASS" : "FAIL")}|{id}");
        var tag = ok ? "PASS" : "FAIL";
        var tail = detail.Length > 0 ? $"  ⟶ {detail}" : "";
        Lines.Add($"    [{tag}] #{_step:D3} {id} · {desc}{tail}");
    }

    public int Total => Pass + Fail;
}

/// <summary>표준 시드(계정 3·그룹 3) — 시나리오마다 신선한 서버/VM 생성으로 상호격리·결정론 보장.</summary>
internal static class GrantFixtures
{
    public static readonly DateTime T0 = new DateTime(2026, 7, 20, 12, 0, 0);

    public static FakeGopServer NewServer()
    {
        var s = new FakeGopServer { Now = T0 };
        s.Users.Add(AuthUser(1, "admin", "관리자", "ADMIN"));
        s.Users.Add(AuthUser(2, "op1", "운영자일", "OPERATOR"));
        s.Users.Add(AuthUser(3, "viewer1", "열람자", "VIEWER"));
        s.Groups.Add(NewGroup(10, "야간조"));
        s.Groups.Add(NewGroup(11, "주간조"));
        s.Groups.Add(NewGroup(12, "정비조"));
        return s;
    }

    public static (TestGrantPanel vm, RecordingEventAggregator ea, RecordingLog log) NewVm(FakeGopServer s)
    {
        var ea = new RecordingEventAggregator();
        var log = new RecordingLog();
        var vm = new TestGrantPanel(ea, log, s);
        return (vm, ea, log);
    }

    private static Ironwall.Dotnet.Libraries.Messages.Dto.Accounts.AuthUserDto AuthUser(
        int id, string loginId, string name, string role)
        => new() { Id = id, LoginId = loginId, Name = name, Role = role, IsActive = true };

    private static Ironwall.Dotnet.Libraries.Messages.Dto.Accounts.UserGroupDto NewGroup(int id, string name)
        => new() { Id = id, Name = name, IsActive = true };
}
