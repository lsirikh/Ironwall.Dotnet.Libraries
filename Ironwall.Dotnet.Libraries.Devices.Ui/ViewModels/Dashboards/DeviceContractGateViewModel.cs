using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dashboards;

/// <summary>
/// 장비 창의 <b>서버 계약 게이트</b> — 새(v7.0+ 축) UI 를 보일지, 판본 확인 실패를 알릴지를 한 곳에서 정한다
/// (device-console-v8 FR-07).
/// </summary>
/// <remarks>
/// <para>XAML 은 이 VM 의 <see cref="IsAxisUi"/>·<see cref="IsLegacyUi"/> 로만 새 요소의 표시를 가른다 —
/// 트리거가 아니라 VM 속성이어야 헤드리스 테스트로 6.3 무회귀를 고정할 수 있다(NFR-01).</para>
/// <para>판정 자체는 <see cref="DeviceQueryPolicy"/> 몫이다. 비교는 <c>==</c> 가 아니라 <c>&gt;=</c> —
/// 다음 판본이 와도 축 화면이 유지된다.</para>
/// </remarks>
public sealed class DeviceContractGateViewModel : PropertyChangedBase
{
    #region - Ctors -
    public DeviceContractGateViewModel(DeviceQueryPolicy? policy = null, ILogService? log = null)
    {
        _policy = policy ?? DeviceQueryPolicy.Resolve();
        _log = log;
    }
    #endregion

    #region - Properties -
    /// <summary>v7.0+ 축 UI(판별자 배지·종류축 콤보·상세 축 절)를 보인다.</summary>
    public bool IsAxisUi => _policy.IsAxisContract;

    /// <summary>6.3 화면 그대로 — 새 요소는 하나도 보이지 않는다.</summary>
    public bool IsLegacyUi => !IsAxisUi;

    /// <summary>
    /// v8.0+ 부대 편제(<c>unit_id</c>) UI 를 보인다(D-14) — "소속 부대" 열·상세 칸. ⚠ <see cref="IsAxisUi"/> 와
    /// 다른 경계다(7.0 vs 8.0) — 비교는 <c>==</c> 가 아니라 <c>&gt;=</c>, 9.0 이 와도 켜진 채 유지된다.
    /// </summary>
    public bool IsUnitEra => _policy.Contract >= EnumServerContract.V8_0;

    /// <summary>판본 확인에 실패해 옛 화면으로 표시 중 — 배너를 띄운다.</summary>
    public bool IsProbeFallback => _policy.IsProbeFallback;

    /// <summary>[다시 확인] 진행 중 — 버튼을 잠근다.</summary>
    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (_isRefreshing == value) return;
            _isRefreshing = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(CanRefreshContract));
        }
    }

    public bool CanRefreshContract => !IsRefreshing;

    /// <summary>화면에 보일 판본 표기 — 확정이면 서버 원문(<c>8.0.1</c>), 폴백이면 <c>6.3 (미확정)</c>.</summary>
    public string ContractText
    {
        get
        {
            var generation = _policy.Contract.ToString().TrimStart('V').Replace('_', '.');   // V6_3 → 6.3
            if (IsProbeFallback) return $"{generation} (미확정)";
            return string.IsNullOrWhiteSpace(_policy.RawVersion) ? generation : _policy.RawVersion!;
        }
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// 판본을 다시 확인하고 화면 게이트를 갱신한다. 이미 진행 중이면 아무것도 하지 않고 <c>false</c>.
    /// </summary>
    public async Task<bool> RefreshContractAsync(CancellationToken token = default)
    {
        if (Interlocked.Exchange(ref _refreshGuard, 1) == 1) return false;

        IsRefreshing = true;
        try
        {
            var ok = await _policy.RefreshContractAsync(token);
            _log?.Info($"[DeviceContractGate] 판본 재확인 — {(ok ? "확정" : "실패")} · {ContractText}");
            return ok;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _log?.Warning($"[DeviceContractGate] 판본 재확인 예외 — {ex.Message}");
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _refreshGuard, 0);
            IsRefreshing = false;
            NotifyOfPropertyChange(nameof(IsAxisUi));
            NotifyOfPropertyChange(nameof(IsLegacyUi));
            NotifyOfPropertyChange(nameof(IsUnitEra));
            NotifyOfPropertyChange(nameof(IsProbeFallback));
            NotifyOfPropertyChange(nameof(ContractText));
        }
    }
    #endregion

    #region - Attributes -
    private readonly DeviceQueryPolicy _policy;
    private readonly ILogService? _log;
    private bool _isRefreshing;
    private int _refreshGuard;
    #endregion
}
