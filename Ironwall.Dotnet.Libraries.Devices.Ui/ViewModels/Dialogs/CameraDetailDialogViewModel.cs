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
/// 카메라 상세 다이얼로그 — HW Spec(읽기전용 표시), Setting/URLs(편집형).
/// <para>저장: Setting → <c>UpdateCameraSettingAsync</c>(별도 API), URLs → <c>UpdateCameraAsync</c>(카메라 본체 urls). 둘 다 작업 복사본 편집(닫기=취소).</para>
/// </summary>
public class CameraDetailDialogViewModel : Conductor<BasePanelViewModel>.Collection.OneActive
{
    private readonly ICameraDeviceModel _model;
    private CameraSettingModel _setting = new();          // Setting 탭 편집 대상(GET 로드)
    private bool _settingLoaded;                          // GET 성공 시에만 PUT(빈 기본값 덮어쓰기 방지)
    private CameraUrlsModel _workingUrls = new();         // URLs 탭 편집 복사본(닫기=취소)

    public CameraDetailDialogViewModel(ICameraDeviceModel model)
    {
        _model = model;
        DisplayName = $"{model.DeviceName}";
    }

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        // 상세 GET으로 전체 HardwareSpec 로드 — 상세보기는 목록 모델을 받아 HW spec가 부분/누락일 수 있음.
        // → 영속된 MaxDetectionRange가 화면에 표시되고(반영), PATCH 시 전체 spec를 전송(JSONB 전체 교체 대비)한다.
        try
        {
            if (_model.Id > 0)
            {
                var detail = await IoC.Get<IDeviceApiService>().GetCameraByIdAsync(_model.Id, cancellationToken);
                if (detail?.Success == true && detail.Data?.HardwareSpec != null)
                    _model.HardwareSpec = DtoToModelHelper.ToCameraInfoModel(detail.Data.HardwareSpec);
            }
        }
        catch { /* 상세 로드 실패 → 목록 모델 유지 */ }

        // HW Spec 편집값(MaxDetectionRange 등)이 저장되도록 _model.HardwareSpec에 직접 연결(없으면 생성).
        // 과거: `?? new CameraInfoModel()`로 분리 인스턴스를 넘겨 편집값이 _model에 반영 안 돼 저장 누락됐음.
        if (_model.HardwareSpec == null)
            _model.HardwareSpec = new CameraInfoModel();
        var infoVm = new CameraInfoViewModel(_model.HardwareSpec);
        infoVm.DisplayName = "HW Spec";
        await ActivateItemAsync(infoVm, cancellationToken);

        _setting = await LoadSettingAsync(cancellationToken);
        var settingVm = new CameraSettingViewModel(_setting);
        settingVm.DisplayName = "Setting";
        await ActivateItemAsync(settingVm, cancellationToken);

        _workingUrls = CloneUrls(_model.Urls);
        var urlsVm = new CameraUrlsViewModel(_workingUrls);
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
                {
                    _settingLoaded = true;
                    return DtoToModelHelper.ToCameraSettingModel(resp.Data);
                }
            }
        }
        catch { /* 로드 실패 → 기본값 + _settingLoaded=false(저장 시 setting PUT 생략) */ }
        return new CameraSettingModel { CameraId = _model.Id };
    }

    /// <summary>'저장' — Setting(로드 성공 시) PUT + URLs(카메라 본체) PUT. 둘 다 성공 시 true.</summary>
    public async Task<bool> SaveAsync(CancellationToken token = default)
    {
        try
        {
            if (_model.Id <= 0) return false;
            var api = IoC.Get<IDeviceApiService>();

            // 1) Setting — 로드 성공 시에만 저장(빈 기본값으로 서버 덮어쓰기 방지)
            if (_settingLoaded)
            {
                _setting.CameraId = _model.Id;
                var sResp = await api.UpdateCameraSettingAsync(_model.Id, _setting.ToCameraSettingDto(), token);
                if (sResp?.Success != true) return false;
            }

            // 2) URLs — 카메라 본체(urls)에 반영해 PUT (setting과 별개 엔드포인트)
            _model.Urls = _workingUrls;
            if (_model is CameraDeviceModel concrete)
            {
                var cResp = await api.UpdateCameraAsync(_model.Id, concrete.ToCameraDeviceDto(), token);
                if (cResp?.Success != true) return false;
            }

            // 3) HardwareSpec(MaxDetectionRange 등) — 전용 PATCH로 안전 저장(full PUT의 비번/타필드 소거 회피, hardware_spec JSONB 전체 교체)
            if (_model.HardwareSpec != null)
            {
                var hwResp = await api.PatchHardwareSpecAsync(_model.Id, DtoToModelHelper.ToHardwareSpecDto(_model.HardwareSpec), token);
                if (hwResp?.Success != true) return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static CameraUrlsModel CloneUrls(ICameraUrlsModel? src)
        => new()
        {
            HomepageUrl = src?.HomepageUrl,
            OnvifDeviceService = src?.OnvifDeviceService,
            RtspMain = src?.RtspMain,
            RtspSub = src?.RtspSub,
            WebrtcMain = src?.WebrtcMain,
            SnapshotCh1 = src?.SnapshotCh1
        };
}
