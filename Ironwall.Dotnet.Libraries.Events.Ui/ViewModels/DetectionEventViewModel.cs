using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 6:57:04 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DetectionEventViewModel : ExEventViewModel, IDetectionEventViewModel
{
    #region - Ctors -
    public DetectionEventViewModel(IDetectionEventModel model) : base(model)
    {
    }

    public DetectionEventViewModel(IDetectionEventModel model, IEventAggregator ea, ILogService log)
        : base(model, ea, log)
    {
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public EnumDetectionType Result
    {
        get { return (_model as IDetectionEventModel)!.Result; }
        set
        {
            SetModelProperty(value, (_model as IDetectionEventModel)!.Result, v => (_model as IDetectionEventModel)!.Result = v);
        }
    }

    /// <summary>탐지 신호 크기(detail.signal) — 센서 계측값이라 읽기 전용. null/0(AI)은 뷰에서 "—" 처리.</summary>
    public int? Signal => (_model as IDetectionEventModel)!.Signal;

    /// <summary>신호 표시 여부 — null 또는 0(AI_DETECT)이면 바/값 숨김.</summary>
    public bool HasSignal => Signal is > 0;

    /// <summary>그리드 표시 문자열 — 천 단위 구분, null/0(AI_DETECT)은 "—".</summary>
    public string SignalText => Signal is > 0 ? Signal!.Value.ToString("N0") : "—";

    /// <summary>썸네일 절대 URI(detail.thumbnail) — 상대경로는 API base 결합(공용 ThumbnailUriResolver).
    /// 없거나 조합 실패면 null → 뷰의 기본 이미지. 실제 로드 실패(자체서명 인증서 등)는 뷰 겹침 default가 폴백.</summary>
    public Uri? ThumbnailUri => ThumbnailUriResolver.Resolve((_model as IDetectionEventModel)!.Thumbnail);

    /// <summary>썸네일 후보 존재 여부(URI 유효). false면 뷰가 기본 이미지를 표시.</summary>
    public bool HasThumbnail => ThumbnailUri != null;

    #endregion
    #region - Attributes -
    #endregion
}