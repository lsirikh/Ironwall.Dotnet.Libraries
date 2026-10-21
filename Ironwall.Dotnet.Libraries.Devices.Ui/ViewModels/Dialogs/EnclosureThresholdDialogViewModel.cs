using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
/****************************************************************************
   Purpose      : 함체 임계값 설정 다이얼로그 (카메라 상세 Conductor 패턴)
   Created By   : GHLee
   Created On   : 6/22/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 함체 임계값 편집 다이얼로그 — CameraDetailDialogViewModel(Conductor.Collection.OneActive) 복제.
/// <para>편집은 <b>작업 복사본</b>에 수행(닫기=취소 시 모델 보존). '저장' 시에만 모델 반영 + API(PUT) 즉시 저장 + 단건 재동기.</para>
/// </summary>
public class EnclosureThresholdDialogViewModel : Conductor<BasePanelViewModel>.Collection.OneActive
{
    private readonly IEnclosureDeviceModel _model;
    private readonly EnclosureThresholdConfigModel _working;   // 편집용 복사본(닫기 시 폐기)

    public EnclosureThresholdDialogViewModel(IEnclosureDeviceModel model)
    {
        _model = model;
        _working = CloneThreshold(model.ThresholdConfig);
        DisplayName = $"{model.DeviceName} 임계값";
    }

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        var settingVm = new EnclosureThresholdSettingViewModel(_working);
        settingVm.DisplayName = "임계값";
        await ActivateItemAsync(settingVm, cancellationToken);
    }

    /// <summary>'저장' — 작업 복사본을 모델에 반영 후 API(PUT)로 즉시 저장하고 단건 재동기. 성공 시 true.</summary>
    public Task<bool> SaveAsync(CancellationToken token = default)
        => SaveAsync(IoC.Get<IDeviceApiService>(), ResolveOptional<IDeviceProviderService>(), DeviceQueryPolicy.Resolve(),
                     ResolveOptional<IEventAggregator>(), token);

    /// <summary>저장 본체 — 의존을 받는 입구(시험은 진짜 <see cref="IDeviceApiService"/> 를 가짜 HTTP 위에 올려 부른다).</summary>
    internal async Task<bool> SaveAsync(IDeviceApiService api, IDeviceProviderService? provider, DeviceQueryPolicy policy,
                                        IEventAggregator? eventAggregator, CancellationToken token = default)
    {
        LastWarningText = null;
        try
        {
            if (_model is not EnclosureDeviceModel concrete || concrete.Id <= 0)
                return false;

            // 받은 임계 — 축 원본이 있으면 그것을, 없으면 반영 전 모델에서 다시 만든다(G16 지운 경계 판정의 기준).
            var received = concrete.Axes?.DeviceConfig?.Thresholds
                           ?? concrete.ToEnclosureDeviceDto().DeviceConfigAxis?.Thresholds;

            concrete.ThresholdConfig = _working;   // 작업 복사본 → 모델 반영

            var eDto = concrete.ToEnclosureDeviceDto();
            // (G16) 7.0+ 는 기존 장비 수정이 PATCH(RFC 7396)라 빠뜨린 키가 서버에 남는다 — 지운 경계를 명시적 null 로 싣는다.
            //   6.3 은 PUT 평면 threshold_config 그대로(이 조각은 축 쓰기에서만 직렬화된다).
            if (policy.IsAxisContract
                && EnclosureThresholdClears.Build(received, eDto.DeviceConfigAxis?.Thresholds) is { } clears)
            {
                eDto.DeviceConfigWrite = new DeviceConfigAxisDto { Thresholds = clears };
                eDto.AllowDeviceConfigWrite = true;
            }
            // (8.0 unit_id) PUT 은 전체 교체다 — 부대를 싣지 않으면 8.0 서버가 기본 부대로 재귀속시킨다(응답에 신호 없음).
            await UnitScopeGate.StampAsync(eDto, nameof(EnclosureThresholdDialogViewModel), token: token);
            var resp = await api.UpdateEnclosureAsync(concrete.Id, eDto, token);
            if (resp == null || !resp.Success)
                return false;

            // (G17) 저장은 됐지만 서버가 남긴 경고(이 함체에 없는 측정 항목 등) — 쉬운 말로 알린다.
            LastWarningText = await SaveWarningNotice.AnnounceAsync(eventAggregator, resp.Warnings);

            // 단건 재동기(provider in-place 갱신, ThresholdConfig 포함) — NATS 무관
            if (provider != null)
                await provider.FetchDeviceByIdAsync("Enclosure", concrete.Id, token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>마지막 저장이 성공했지만 서버가 경고를 남겼으면 그 안내 글(없으면 <c>null</c>).</summary>
    public string? LastWarningText { get; private set; }

    /// <summary>편집 중인 작업 복사본(시험 · 진단용).</summary>
    internal EnclosureThresholdConfigModel Working => _working;

    private static T? ResolveOptional<T>() where T : class
    {
        try { return IoC.GetAllInstances(typeof(T))?.OfType<T>().FirstOrDefault() ?? IoC.Get<T>(); }
        catch { return null; }
    }

    private static EnclosureThresholdConfigModel CloneThreshold(IEnclosureThresholdConfigModel? src)
        => new()
        {
            TempHigh = src?.TempHigh,
            TempLow = src?.TempLow,
            HumidityHigh = src?.HumidityHigh,
            CurrentHigh = src?.CurrentHigh,
            VoltageLow = src?.VoltageLow,
            VibrationHigh = src?.VibrationHigh
        };
}
