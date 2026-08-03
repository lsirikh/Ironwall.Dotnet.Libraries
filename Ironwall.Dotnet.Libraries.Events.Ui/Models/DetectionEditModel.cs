using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Models;
/****************************************************************************
   Purpose      : 탐지 이벤트 편집 버퍼 모델 — 속성창(SelectionViewModel)의 입력 상태를
                  하나의 모델로 정규화한다.
   Created By   : GHLee
   Created On   : 2026-08-03
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 탐지 이벤트 속성창의 편집 입력을 담는 <b>단일 편집 모델</b>.
/// 종전에는 공통값 필드(<c>MessageType</c>/<c>Result</c>…)와 상세 편집 필드(<c>SignalEdit</c>…)가
/// 명명·알림·적용 경로가 제각각이었고, 상세는 모델에 직접 대입돼 dirty가 켜지지 않았다.
/// 여기서 세 가지를 통일한다.
///
/// <list type="number">
///   <item><b>값 의미</b> — 모든 필드가 <c>null</c> = "변경 없음"(기존 값 유지), 값 있음 = "이 값으로 변경".</item>
///   <item><b>알림</b> — 모든 필드가 PropertyChanged를 발생시킨다(무-알림 auto-property 금지).</item>
///   <item><b>적용 경로</b> — <see cref="ApplyTo"/> 가 <b>행 ViewModel 세터만</b> 사용한다.
///         모델 직접 대입은 <c>IsEdited</c>(dirty)를 켜지 못해 저장 대상에서 조용히 탈락한다.</item>
/// </list>
///
/// <para><b>서버 계약 주의</b>: 기존 행(Id&gt;0)에서 실제로 바뀌는 건 <c>result</c> 와 <c>detail</c> 뿐이다.
/// <c>MessageType</c>(type_event) · <c>DateTime</c>(created_at) · <c>Status</c>(action_reported) 는
/// 행 VM 세터가 <c>IsDraft</c> 가드로 차단하므로 여기서 값을 넣어도 기존 행에는 반영되지 않는다
/// (신규 Draft 행에서만 반영). UI 비활성화는 PRD Event_Edit_Save_Pipeline FR-08(배치 2) 소관.</para>
/// </summary>
public class DetectionEditModel : PropertyChangedBase
{
    #region - 공통 필드 (다중 선택에서도 일괄 적용) -
    private EnumEventType? _messageType;
    /// <summary>이벤트 유형(type_event). 기존 행은 서버 정책상 불변 — Draft 에서만 반영.</summary>
    public EnumEventType? MessageType
    {
        get => _messageType;
        set { _messageType = value; NotifyOfPropertyChange(); }
    }

    private EnumTrueFalse? _status;
    /// <summary>조치 여부(action_reported). 시스템 자동관리 — Draft 에서만 반영.</summary>
    public EnumTrueFalse? Status
    {
        get => _status;
        set { _status = value; NotifyOfPropertyChange(); }
    }

    private EnumDetectionType? _result;
    /// <summary>탐지 결과(result). <b>기존 행에서도 변경 가능한 유일한 공통 필드.</b></summary>
    public EnumDetectionType? Result
    {
        get => _result;
        set { _result = value; NotifyOfPropertyChange(); }
    }

    private DateTime? _dateTime;
    /// <summary>발생 시각(created_at). 기존 행은 서버 정책상 불변 — Draft 에서만 반영.</summary>
    public DateTime? DateTime
    {
        get => _dateTime;
        set { _dateTime = value; NotifyOfPropertyChange(); }
    }
    #endregion

    #region - 상세(detail) 필드 (단일 선택에서만 적용) -
    private int? _signal;
    /// <summary>신호 크기(detail.signal).</summary>
    public int? Signal
    {
        get => _signal;
        set { _signal = value; NotifyOfPropertyChange(); }
    }

    private string? _aiModel;
    /// <summary>AI 모델명(detail.model). 공백만 입력하면 "변경 없음"으로 정규화된다.</summary>
    public string? AiModel
    {
        get => _aiModel;
        set { _aiModel = string.IsNullOrWhiteSpace(value) ? null : value.Trim(); NotifyOfPropertyChange(); }
    }

    private int? _inferenceMs;
    /// <summary>추론 소요 시간 ms(detail.inference_ms).</summary>
    public int? InferenceMs
    {
        get => _inferenceMs;
        set { _inferenceMs = value; NotifyOfPropertyChange(); }
    }

    private int? _frameWidth;
    /// <summary>프레임 가로 px(detail.frame_width).</summary>
    public int? FrameWidth
    {
        get => _frameWidth;
        set { _frameWidth = value; NotifyOfPropertyChange(); }
    }

    private int? _frameHeight;
    /// <summary>프레임 세로 px(detail.frame_height).</summary>
    public int? FrameHeight
    {
        get => _frameHeight;
        set { _frameHeight = value; NotifyOfPropertyChange(); }
    }
    #endregion

    #region - Processes -
    /// <summary>
    /// 선택된 행들의 <b>공통값</b>으로 편집 버퍼를 시딩한다(값이 서로 다르면 null = "변경 없음").
    /// 상세(detail)는 이벤트마다 다른 계측값이라 <b>단일 선택일 때만</b> 시딩한다 —
    /// 다중 선택에서 첫 항목 값을 대표값처럼 보여 오인시키지 않기 위함.
    /// </summary>
    public void LoadFrom(IReadOnlyList<DetectionEventViewModel>? selection)
    {
        var models = selection?.Select(vm => vm.Model).OfType<IDetectionEventModel>().ToList()
                     ?? new List<IDetectionEventModel>();

        MessageType = CommonOrNull(models, m => m.MessageType);
        Status      = CommonOrNull(models, m => m.Status);
        Result      = CommonOrNull(models, m => m.Result);
        DateTime    = CommonOrNull(models, m => m.DateTime);

        var single = models.Count == 1 ? models[0] : null;
        Signal      = single?.Signal;
        AiModel     = single?.AiModel;
        InferenceMs = single?.InferenceMs;
        FrameWidth  = single?.FrameWidth;
        FrameHeight = single?.FrameHeight;
    }

    /// <summary>
    /// 편집 버퍼를 행 ViewModel에 적용한다. <b>반드시 행 VM 세터를 경유</b>하므로
    /// 값이 실제로 바뀐 필드만 <c>IsEdited</c>(dirty)를 켜고, 그 행이 저장 대상(PUT)에 포함된다.
    /// </summary>
    /// <param name="row">대상 행 ViewModel(= DataGrid 항목).</param>
    /// <param name="includeDetail">
    /// 상세(detail) 적용 여부. 다중 선택에서는 대상이 모호하므로 false 로 호출한다.
    /// </param>
    public void ApplyTo(DetectionEventViewModel? row, bool includeDetail)
    {
        if (row?.Model is not IDetectionEventModel) return;

        // 공통 필드 — null 이면 손대지 않는다(기존 값 유지).
        // MessageType/Status/DateTime 은 기존 행에서 VM 세터의 IsDraft 가드가 차단한다(서버 정책).
        if (MessageType is EnumEventType type)      row.MessageType = type;
        if (Status      is EnumTrueFalse status)    row.Status      = status;
        if (Result      is EnumDetectionType res)   row.Result      = res;
        if (DateTime    is DateTime dt)             row.DateTime    = dt;

        if (!includeDetail) return;

        // 상세 필드 — Thumbnail/Objects 는 편집 대상이 아니며 모델 값이 그대로 남아
        // 저장 시 DtoToModelHelper.BuildDetectionDetail 이 함께 재구성해 보존한다.
        if (Signal      is int sig)     row.Signal      = sig;
        if (AiModel     is string ai)   row.AiModel     = ai;
        if (InferenceMs is int inf)     row.InferenceMs = inf;
        if (FrameWidth  is int fw)      row.FrameWidth  = fw;
        if (FrameHeight is int fh)      row.FrameHeight = fh;
    }

    /// <summary>선택 전체가 같은 값을 가질 때만 그 값을, 하나라도 다르면 null(= 변경 없음)을 돌려준다.</summary>
    private static T? CommonOrNull<T>(IReadOnlyList<IDetectionEventModel> models, Func<IDetectionEventModel, T> selector)
        where T : struct
    {
        if (models.Count == 0) return null;
        var first = selector(models[0]);
        return models.All(m => EqualityComparer<T>.Default.Equals(selector(m), first)) ? first : null;
    }
    #endregion
}
