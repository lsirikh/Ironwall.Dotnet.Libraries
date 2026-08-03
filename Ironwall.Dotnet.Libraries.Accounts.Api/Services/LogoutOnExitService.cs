using System;
using System.Threading;
using System.Threading.Tasks;
using Ironwall.Dotnet.Libraries.Base.Services;

namespace Ironwall.Dotnet.Libraries.Accounts.Api.Services;

/// <summary>
/// 앱 종료 시 서버 로그아웃(세션 정리) + 로컬 토큰 폐기를 수행하는 '종료 전용' IService.
/// <para><c>ParentBootstrapper.OnExit</c> 이 모든 <see cref="IService"/>.StopAsync 를 10초 예산으로 병렬 호출한다
/// (WPF 디스패처 밖 Task.Run). 여기서는 best-effort: 미인증이면 skip, 4초 실데드라인(WhenAny),
/// 예외 삼킴, UI/팝업/이벤트 발화 없음.</para>
/// <para>seam 은 <see cref="IAccountApiService.LogoutAsync"/>(순수 POST /auth/logout) + <see cref="ITokenStorageService.Clear"/>.
/// IAuthGateway.LogoutAsync 는 finally 에서 ForceLogoutOnce→ForceLogoutRequested 를 발화해 종료 중
/// UI 구독자(로그인 전환·셸 가림막·PTZ 정지)를 디스패처로 건드려 데드락/화면 깜빡임을 유발하므로 사용하지 않는다.</para>
/// <para>API 모드 전용(AccountApiModule 등록) — DB 모드는 서버 세션이 없어 등록하지 않는다.</para>
/// </summary>
public class LogoutOnExitService : IService
{
    private readonly IAccountApiService _api;
    private readonly ITokenStorageService _tokenStore;
    private readonly ILogService? _log;
    private readonly TimeSpan _deadline;

    /// <summary>종료 로그아웃 기본 실데드라인(초). HTTP 계층이 CancellationToken 을 관측하지 않으므로(IApiService POST 무 ct)
    /// WhenAny 로 실제 상한을 만든다 — 초과 시 호출은 백그라운드로 방치하고 종료를 계속 진행(10초 예산 잠식 방지).</summary>
    private const int DEFAULT_DEADLINE_SEC = 4;

    /// <param name="deadline">종료 로그아웃 실데드라인(기본 4초). 테스트에서 짧게 주입 가능(실대기 회피).</param>
    public LogoutOnExitService(IAccountApiService api, ITokenStorageService tokenStore, ILogService? log = null, TimeSpan? deadline = null)
    {
        _api = api;
        _tokenStore = tokenStore;
        _log = log;
        _deadline = deadline ?? TimeSpan.FromSeconds(DEFAULT_DEADLINE_SEC);
    }

    /// <summary>기동 시 할 일 없음(종료 전용 서비스).</summary>
    public Task ExecuteAsync(CancellationToken token = default) => Task.CompletedTask;

    /// <summary>종료 훅 — 인증 상태면 best-effort 서버 로그아웃 후 로컬 토큰 폐기. 예외 삼킴·무팝업·무이벤트.</summary>
    public async Task StopAsync(CancellationToken token = default)
    {
        if (!_tokenStore.IsAuthenticated)
            return;   // 미인증/이미 로그아웃/revoked → skip

        try
        {
            // 순수 POST /auth/logout (best-effort). HTTP 계층은 ct 미관측 → 아래 WhenAny 로 실상한 강제.
            var logoutTask = _api.LogoutAsync(token);
            var finished = await Task.WhenAny(
                logoutTask,
                Task.Delay(_deadline, token)).ConfigureAwait(false);

            if (finished == logoutTask)
            {
                var res = await logoutTask.ConfigureAwait(false);   // 완료 Task — 결과/예외 관측
                _log?.Info($"[LogoutOnExit] 종료 로그아웃 {(res.Success ? "성공" : "응답실패(무시)")}");
            }
            else
            {
                _log?.Warning($"[LogoutOnExit] 종료 로그아웃 {_deadline.TotalSeconds:F0}s 초과 — 로컬 폐기로 진행(호출은 백그라운드 방치)");
                ObserveAbandoned(logoutTask);   // 방치 Task 의 미관측 예외 삼킴
            }
        }
        catch (OperationCanceledException)
        {
            _log?.Warning("[LogoutOnExit] 종료 예산 취소 — 로컬 폐기로 진행");
        }
        catch (Exception ex)
        {
            _log?.Warning($"[LogoutOnExit] 종료 로그아웃 실패(best-effort 무시): {ex.Message}");
        }
        finally
        {
            try { _tokenStore.Clear(); }   // 방어적 로컬 폐기(이벤트 미발화) — 서버 성공/실패 무관
            catch (Exception ex) { _log?.Warning($"[LogoutOnExit] 토큰 폐기 실패: {ex.Message}"); }
        }
    }

    /// <summary>데드라인 초과로 방치한 Task 의 미관측 예외를 조용히 흡수(UnobservedTaskException 방지).</summary>
    private static void ObserveAbandoned(Task t)
        => t.ContinueWith(x => { _ = x.Exception; }, TaskScheduler.Default);
}
