using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Events;
using Ironwall.Dotnet.Monitoring.Models.Helpers;
using Newtonsoft.Json;
using System;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 6:57:48 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class ActionEventViewModel : BaseEventViewModel<IActionEventModel>, IActionEventViewModel
{
    #region - Ctors -
    public ActionEventViewModel(IActionEventModel model) : base(model)
    {
        _model = model;
    }
    public ActionEventViewModel(IActionEventModel model, IEventAggregator ea, ILogService log) : base(model, ea, log)
    {
        _model = model;
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    public override void Dispose()
    {
        _model = new ActionEventModel();
        GC.Collect();
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public IExEventModel? OriginEvent
    {
        get { return _model.OriginEvent; }
        set
        {
            if (!IsDraft) return;   // from_event_id 불변(서버 E-02): 조치보고 원본은 생성 시에만 지정, 사후 변경 차단 — 전 경로
            SetModelProperty(value, _model.OriginEvent, v => _model.OriginEvent = v);
        }
    }

    public string? User
    {
        get { return _model!.User; }
        set { SetModelProperty(value, _model.User, v => _model.User = v); }
    }

    public string? Content
    {
        get { return _model!.Content; }
        set { SetModelProperty(value, _model.Content, v => _model.Content = v); }
    }

    // ── 원본 이벤트(OriginEvent) 썸네일 — origin=탐지만 노출, 장애/부재=Default. [Action_Report_Origin_Thumbnail] ──
    /// <summary>원본이 탐지 이벤트인가.</summary>
    public bool IsDetectionOrigin => _model.OriginEvent is IDetectionEventModel;

    /// <summary>원본 탐지의 썸네일 절대 URI(host를 API base로 rebase). 장애/부재면 null.</summary>
    public Uri? OriginThumbnailUri => ThumbnailUriResolver.Resolve((_model.OriginEvent as IDetectionEventModel)?.Thumbnail);

    /// <summary>썸네일 후보 존재(URI 유효). false면 뷰가 기본 이미지(Default).</summary>
    public bool HasOriginThumbnail => OriginThumbnailUri != null;

    /// <summary>썸네일 이미지 — 1회 다운로드→OnLoad 캐시(탭 전환/그리드 재활용에도 유지). 로드 전/장애/실패면 null→Default.</summary>
    public ImageSource? OriginThumbnail
    {
        get
        {
            var uri = OriginThumbnailUri;
            return uri is null ? null : ThumbnailImageLoader.GetOrLoad(uri, () => NotifyOfPropertyChange(nameof(OriginThumbnail)));
        }
    }
    #endregion
    #region - Attributes -
    #endregion
}