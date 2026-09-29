using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.DataProviders;
using Ironwall.Dotnet.Libraries.ViewModel.Models;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace Ironwall.Dotnet.Libraries.Devices.Providers;
/****************************************************************************
   Purpose      :
   Created By   : GHLee
   Created On   : 5/26/2025 9:04:49 AM
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
[DebuggerDisplay("Count = {CollectionEntity.Count}")]
public class DeviceProvider : BaseProvider<IBaseDeviceModel>, IHandle<DeviceUnitChangedMessage>
{
    #region - Ctors -
    /// <summary>구독 없는 생성자(시험 · 이벤트 버스 없는 호스트).</summary>
    public DeviceProvider()
    {
    }

    /// <summary>
    /// 이벤트 버스를 받으면 <see cref="DeviceUnitChangedMessage"/> 를 구독한다(FR-49) — 콘솔이 장비 소속을 바꾸면
    /// 다음 전량 적재를 기다리지 않고 그 장비 모델의 <c>UnitId</c> 만 고친다.
    /// </summary>
    /// <param name="events">이벤트 버스(Autofac 이 해석한다 — 등록돼 있으면 이 생성자가 쓰인다).</param>
    /// <param name="uiDispatcher">적용할 UI 스레드(시험용). 생략하면 <c>Application.Current.Dispatcher</c>(없으면 받은 스레드).</param>
    public DeviceProvider(IEventAggregator events, Dispatcher? uiDispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        _uiDispatcher = uiDispatcher;
        events.SubscribeOnPublishedThread(this);   // 싱글턴 — 구독을 풀 때가 없다(CM 은 약한 참조로 붙잡는다)
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// 그 장비 모델의 <c>UnitId</c> 만 제자리에서 고친다 — 컬렉션을 다시 만들지 않는다(지도 심볼이 같은 인스턴스를 붙잡고 있다).
    /// </summary>
    /// <returns>바뀐 모델 수(모르는 장비 · 이미 같은 값 · 잘못된 id 면 0).</returns>
    /// <remarks>호출 스레드: UI(<see cref="HandleAsync"/> 가 옮겨 부른다). 8.0 서버는 장비 id 가 카테고리를 넘어 유일하다(probe log V-08).</remarks>
    public int ApplyUnitChange(int deviceId, int unitId)
    {
        if (deviceId <= 0 || unitId <= 0) return 0;

        var changed = 0;
        foreach (var device in CollectionEntity.Where(d => d != null && d.Id == deviceId).ToList())
        {
            if (device.UnitId == unitId) continue;
            device.UnitId = unitId;
            changed++;
            DeviceUnitChanged?.Invoke(this, device);
        }
        return changed;
    }
    #endregion

    #region - IHanldes -
    /// <summary>
    /// 콘솔이 장비 소속을 바꿨다(FR-49). 발행은 어느 스레드에서도 올 수 있다 — 모델은 화면이 바인딩하므로 UI 스레드에서 고친다.
    /// </summary>
    public Task HandleAsync(DeviceUnitChangedMessage message, CancellationToken cancellationToken)
    {
        if (message is null) return Task.CompletedTask;

        var dispatcher = _uiDispatcher ?? System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyUnitChange(message.DeviceId, message.UnitId);
            return Task.CompletedTask;
        }
        return dispatcher.InvokeAsync(() => ApplyUnitChange(message.DeviceId, message.UnitId)).Task;
    }
    #endregion

    #region - Events -
    /// <summary>한 장비의 <c>UnitId</c> 를 고쳤다(UI 스레드).</summary>
    public event EventHandler<IBaseDeviceModel>? DeviceUnitChanged;

    /// <summary>
    /// 캐시에 있던 장비 모델을 <b>제자리에서</b> 고쳤다(SYNC_DEVICE 재조회) — 컬렉션은 그대로라 <c>CollectionChanged</c> 가 울리지 않고,
    /// 모델은 변경 알림이 없는 평범한 객체라 그 모델을 보여 주는 화면(장비 콘솔의 행 · 상세)은 이 신호가 없으면 옛 값을 쥔 채 남는다.
    /// </summary>
    /// <remarks>호출 스레드: 고친 쪽의 스레드(NATS 처리 스레드일 수 있다) — 받는 쪽이 UI 스레드로 옮긴다.</remarks>
    public event EventHandler<IBaseDeviceModel>? DeviceUpdated;

    /// <summary>제자리 갱신을 알린다(<see cref="DeviceUpdated"/>).</summary>
    public void NotifyDeviceUpdated(IBaseDeviceModel device)
    {
        if (device is null) return;
        DeviceUpdated?.Invoke(this, device);
    }
    #endregion

    #region - Attributes -
    private readonly Dispatcher? _uiDispatcher;
    #endregion
}
