using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.Helpers;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Windows.Media;

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

    // ── 탐지 상세(detail) 편집 속성 ────────────────────────────────────────
    // 반드시 SetModelProperty 경유 = 이 세터가 dirty(IsEdited)를 켜는 유일한 경로다.
    // 모델에 직접 대입하면 IsEdited가 false로 남아 저장 루프
    // (DetectionEventPanelViewModel: Where(vm => vm.IsEdited && Id>0))에서 행 자체가 탈락한다.
    // ⚠ 서버발 갱신(NATS SYNC·썸네일 후속 수신)은 사용자의 편집이 아니므로
    //    여기 세터가 아니라 모델 직접 갱신을 유지해야 한다(거짓 dirty → 불필요 PUT 방지).

    /// <summary>탐지 신호 크기(detail.signal). null/0(AI)은 뷰에서 "—" 처리.</summary>
    public int? Signal
    {
        get { return (_model as IDetectionEventModel)!.Signal; }
        set
        {
            if (SetModelProperty(value, (_model as IDetectionEventModel)!.Signal, v => (_model as IDetectionEventModel)!.Signal = v))
                NotifySignalTexts();
        }
    }

    /// <summary>AI 모델명(detail.model).</summary>
    public string? AiModel
    {
        get { return (_model as IDetectionEventModel)!.AiModel; }
        set { SetModelProperty(value, (_model as IDetectionEventModel)!.AiModel, v => (_model as IDetectionEventModel)!.AiModel = v); }
    }

    /// <summary>추론 소요 시간 ms(detail.inference_ms).</summary>
    public int? InferenceMs
    {
        get { return (_model as IDetectionEventModel)!.InferenceMs; }
        set { SetModelProperty(value, (_model as IDetectionEventModel)!.InferenceMs, v => (_model as IDetectionEventModel)!.InferenceMs = v); }
    }

    /// <summary>프레임 가로 px(detail.frame_width) — objects[].bbox 좌표 해석 기준.</summary>
    public int? FrameWidth
    {
        get { return (_model as IDetectionEventModel)!.FrameWidth; }
        set { SetModelProperty(value, (_model as IDetectionEventModel)!.FrameWidth, v => (_model as IDetectionEventModel)!.FrameWidth = v); }
    }

    /// <summary>프레임 세로 px(detail.frame_height).</summary>
    public int? FrameHeight
    {
        get { return (_model as IDetectionEventModel)!.FrameHeight; }
        set { SetModelProperty(value, (_model as IDetectionEventModel)!.FrameHeight, v => (_model as IDetectionEventModel)!.FrameHeight = v); }
    }

    /// <summary>신호 표시 여부 — null 또는 0(AI_DETECT)이면 바/값 숨김.</summary>
    public bool HasSignal => Signal is > 0;

    /// <summary>그리드 표시 문자열 — 천 단위 구분, null/0(AI_DETECT)은 "—".</summary>
    public string SignalText => Signal is > 0 ? Signal!.Value.ToString("N0") : "—";

    /// <summary>Signal 파생 표시값 갱신 — 세터가 모델을 바꿨을 때만 호출(그리드 신호 컬럼·미니바 즉시 반영).</summary>
    private void NotifySignalTexts()
    {
        NotifyOfPropertyChange(nameof(HasSignal));
        NotifyOfPropertyChange(nameof(SignalText));
    }

    /// <summary>썸네일 절대 URI(detail.thumbnail) — 상대경로는 API base 결합(공용 ThumbnailUriResolver).
    /// 없거나 조합 실패면 null → 뷰의 기본 이미지. 실제 로드 실패(자체서명 인증서 등)는 뷰 겹침 default가 폴백.</summary>
    public Uri? ThumbnailUri => ThumbnailUriResolver.Resolve((_model as IDetectionEventModel)!.Thumbnail);

    /// <summary>썸네일 후보 존재 여부(URI 유효). false면 뷰가 기본 이미지를 표시.</summary>
    public bool HasThumbnail => ThumbnailUri != null;

    /// <summary>썸네일 이미지 — 원격 URL을 1회 다운로드→OnLoad 디코드→Freeze 캐시(<see cref="Helpers.ThumbnailImageLoader"/>).
    /// 탭 전환/DataGrid 컨테이너 재활용에도 유지 = Uri 직접 바인딩이 재렌더에 실패해 Default만 남던 문제 해소.
    /// 로드 전/부재/실패면 null → 뷰의 겹침 default.</summary>
    public ImageSource? Thumbnail
    {
        get
        {
            var uri = ThumbnailUri;
            return uri is null ? null : ThumbnailImageLoader.GetOrLoad(uri, () => NotifyOfPropertyChange(nameof(Thumbnail)));
        }
    }

    #endregion
    #region - Attributes -
    #endregion
}