using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
/****************************************************************************
   Purpose      : CameraUrlsModel passthrough ViewModel
   Created By   : GHLee
   Created On   : 2/25/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/
public sealed class CameraUrlsViewModel : BasePanelViewModel
{
    private readonly ICameraUrlsModel _model;

    public CameraUrlsViewModel(ICameraUrlsModel model)
    {
        _model = model;
    }

    public string? HomepageUrl
    {
        get => _model.HomepageUrl;
        set { _model.HomepageUrl = value; NotifyOfPropertyChange(() => HomepageUrl); }
    }

    public string? OnvifDeviceService
    {
        get => _model.OnvifDeviceService;
        set { _model.OnvifDeviceService = value; NotifyOfPropertyChange(() => OnvifDeviceService); }
    }

    public string? RtspMain
    {
        get => _model.RtspMain;
        set { _model.RtspMain = value; NotifyOfPropertyChange(() => RtspMain); }
    }

    public string? RtspSub
    {
        get => _model.RtspSub;
        set { _model.RtspSub = value; NotifyOfPropertyChange(() => RtspSub); }
    }

    public string? WebrtcMain
    {
        get => _model.WebrtcMain;
        set { _model.WebrtcMain = value; NotifyOfPropertyChange(() => WebrtcMain); }
    }

    public string? SnapshotCh1
    {
        get => _model.SnapshotCh1;
        set { _model.SnapshotCh1 = value; NotifyOfPropertyChange(() => SnapshotCh1); }
    }
}
