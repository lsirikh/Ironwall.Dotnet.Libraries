using Autofac;
using Ironwall.Dotnet.Libraries.Accounts.Api.Modules;
using Ironwall.Dotnet.Libraries.Accounts.Gateways;
using Ironwall.Dotnet.Libraries.Api.Models;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Tests.LiveApi;

/// <summary>
/// 라이브 API 시험의 <b>어셈블리당 하나</b>인 로그인 세션 — 컨테이너 1개 · 로그인 1회를 모든 시험이 나눠 쓴다.
/// <para><b>왜 하나인가</b>: 서버는 같은 계정의 새 로그인이 이전 세션을 축출(evict)한다. 시험마다 로그인하면
/// 병렬로 도는 시험끼리 서로의 토큰을 무효로 만들어 401 로 깨진다.</para>
/// <para>도메인 모듈(장비 · 이벤트)은 어셈블리마다 <see cref="RegisterDomainModules"/> 를 구현해 <see cref="Name"/> 이름으로 등록한다.
/// 그 모듈의 Bearer 처리기는 여기서 등록한 계정 모듈의 토큰 저장소를 공유한다.</para>
/// <para>로그인이 실패하면 그 사유를 기억해 두고 다시 시도하지 않는다 — 시험마다 재시도하면 잠금 정책에 걸린다.</para>
/// </summary>
internal static partial class LiveApiSession
{
    /// <summary>도메인 모듈 등록 이름 — 시험은 <c>ResolveNamed&lt;T&gt;(LiveApiSession.Name)</c> 로 서비스를 꺼낸다.</summary>
    public const string Name = "Live";

    private const string AccountName = "LiveAccount";

    public static ILogService Log { get; } = new LogService();

    private static readonly SemaphoreSlim _gate = new(1, 1);
    private static IContainer? _container;
    private static string? _failure;

    /// <summary>로그인된 컨테이너. 환경이 없으면(건너뛸 시험에서 잘못 부르면) 예외.</summary>
    public static async Task<IContainer> GetAsync()
    {
        if (_container is not null) return _container;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_container is not null) return _container;
            if (_failure is not null) throw new InvalidOperationException(_failure);

            var skip = LiveApiTarget.ReadSkipReason;
            if (skip is not null)
                throw new InvalidOperationException($"라이브 API 환경이 없는데 세션을 요청했습니다: {skip}");

            var setup = LiveApiTarget.CreateSetup();
            var builder = new ContainerBuilder();
            builder.RegisterModule(new AccountApiModule(Log, setup, AccountName));
            RegisterDomainModules(builder, setup);
            var container = builder.Build();

            try
            {
                container.ResolveNamed<IApiService>(AccountName).Initialize();
                if (container.IsRegisteredWithName<IApiService>(Name))
                    container.ResolveNamed<IApiService>(Name).Initialize();

                // 앱과 같게 서버 계약 세대를 먼저 확보한다 — 없으면 서비스가 6.3 구계약 본문을 보낸다.
                foreach (var probe in container.Resolve<IEnumerable<IServerContractProbe>>())
                    await probe.ResolveAsync().ConfigureAwait(false);

                var (id, password) = LiveApiTarget.ReadCredentials();
                var outcome = await container.Resolve<IAuthGateway>().AuthenticateAsync(id, password).ConfigureAwait(false);
                if (!outcome.Success)
                {
                    // 계정 값은 싣지 않는다 — 서버가 준 사유 코드 · 문구만.
                    _failure = $"전용 계정 로그인 실패({LiveApiTarget.Url}): {outcome.ErrorCode} {outcome.Message}";
                    throw new InvalidOperationException(_failure);
                }
            }
            catch
            {
                container.Dispose();
                throw;
            }

            _container = container;
            return container;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>로그인된 세션에서 <see cref="Name"/> 이름의 서비스를 꺼낸다.</summary>
    public static async Task<T> ResolveAsync<T>() where T : notnull
        => (await GetAsync().ConfigureAwait(false)).ResolveNamed<T>(Name);

    /// <summary>어셈블리별 도메인 모듈 등록(장비 · 이벤트). <see cref="Name"/> 으로 등록해야 한다.</summary>
    static partial void RegisterDomainModules(ContainerBuilder builder, ApiSetupModel setup);
}
