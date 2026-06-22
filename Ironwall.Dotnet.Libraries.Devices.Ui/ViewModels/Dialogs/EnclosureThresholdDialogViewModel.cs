using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.Devices.Ui.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

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
    public async Task<bool> SaveAsync(CancellationToken token = default)
    {
        try
        {
            if (_model is not EnclosureDeviceModel concrete || concrete.Id <= 0)
                return false;

            concrete.ThresholdConfig = _working;   // 작업 복사본 → 모델 반영

            var api = IoC.Get<IDeviceApiService>();
            var resp = await api.UpdateEnclosureAsync(concrete.Id, concrete.ToEnclosureDeviceDto(), token);
            if (resp == null || !resp.Success)
                return false;

            // 단건 재동기(provider in-place 갱신, ThresholdConfig 포함) — NATS 무관
            await IoC.Get<IDeviceProviderService>().FetchDeviceByIdAsync("Enclosure", concrete.Id, token);
            return true;
        }
        catch
        {
            return false;
        }
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
