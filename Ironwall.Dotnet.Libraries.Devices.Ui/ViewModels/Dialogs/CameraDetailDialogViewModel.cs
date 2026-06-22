using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Helpers;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
/****************************************************************************
   Purpose      : Camera Detail Dialog - 3 tab Conductor (HW Spec, Setting, URLs)
   Created By   : GHLee
   Created On   : 2/25/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
/// <summary>
/// 카메라 상세 다이얼로그 — 3탭(HW Spec/URLs는 읽기전용 표시, Setting은 편집형).
/// <para>Setting은 진입 시 GET <c>/cameras/{id}/settings</c>로 실제값 로드 → 편집 → '저장' 시 PUT. (닫기=취소, 카메라 본체 미변경)</para>
/// </summary>
public class CameraDetailDialogViewModel : Conductor<BasePanelViewModel>.Collection.OneActive
{
    private readonly ICameraDeviceModel _model;
    private CameraSettingModel _setting = new();   // Setting 탭 편집 대상(GET 로드 결과)

    public CameraDetailDialogViewModel(ICameraDeviceModel model)
    {
        _model = model;
        DisplayName = $"{model.DeviceName}";
    }

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        var infoVm = new CameraInfoViewModel(_model.HardwareSpec ?? new CameraInfoModel());
        infoVm.DisplayName = "HW Spec";
        await ActivateItemAsync(infoVm, cancellationToken);

        // Setting: 카메라 setting API에서 실제값 로드(없으면 기본값). 편집은 이 인스턴스, 저장 시에만 PUT.
        _setting = await LoadSettingAsync(cancellationToken);
        var settingVm = new CameraSettingViewModel(_setting);
        settingVm.DisplayName = "Setting";
        await ActivateItemAsync(settingVm, cancellationToken);

        var urlsVm = new CameraUrlsViewModel(_model.Urls ?? new CameraUrlsModel());
        urlsVm.DisplayName = "URLs";
        await ActivateItemAsync(urlsVm, cancellationToken);
    }

    private async Task<CameraSettingModel> LoadSettingAsync(CancellationToken token)
    {
        try
        {
            if (_model.Id > 0)
            {
                var resp = await IoC.Get<IDeviceApiService>().GetCameraSettingAsync(_model.Id, token);
                if (resp?.Success == true && resp.Data != null)
                    return DtoToModelHelper.ToCameraSettingModel(resp.Data);
            }
        }
        catch { /* 로드 실패 시 기본값(아래) */ }
        return new CameraSettingModel { CameraId = _model.Id };
    }

    /// <summary>'저장' — Setting 탭 값을 카메라 setting API(PUT, 전체 교체)로 저장. 성공 시 true.</summary>
    public async Task<bool> SaveAsync(CancellationToken token = default)
    {
        try
        {
            if (_model.Id <= 0) return false;
            _setting.CameraId = _model.Id;
            var resp = await IoC.Get<IDeviceApiService>().UpdateCameraSettingAsync(_model.Id, _setting.ToCameraSettingDto(), token);
            return resp?.Success == true;
        }
        catch
        {
            return false;
        }
    }
}
