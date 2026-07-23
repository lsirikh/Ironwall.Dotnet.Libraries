using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 8:33:57 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class DetectionSelectionViewModel : BasePanelViewModel
{
    #region - Ctors -
    public DetectionSelectionViewModel(IList<DetectionEventViewModel> selection)
    {
        PanelViewModel = IoC.Get<DetectionEventPanelViewModel>();
        DeviceProvider = IoC.Get<DeviceProvider>();
        _selection = selection;
        RefreshAll();
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    public void ApplyButton()
    {
        foreach (var item in _selection)
        {
            item.MessageType = MessageType ?? item.MessageType;
            item.Device = Device ?? item.Device;
            item.Result = Result ?? item.Result;
            item.Status = Status ?? item.Status;
            item.DateTime = DateTime ?? item.DateTime;
        }
    }

    /* 공통값 계산 헬퍼 */
    //int 형 및 Enum 타입의 형식 비교
    private static T? CommonOrNullValue<T>(IEnumerable<DetectionEventViewModel> list, Func<IDetectionEventModel, T> selector) where T : struct
    {
        try
        {
            if (list == null || !list.Any()) return null;

            var firstModel = list.FirstOrDefault()?.Model as IDetectionEventModel;
            if (firstModel == null) return null;

            T firstValue = selector(firstModel);

            bool allSame = list
                .Select(vm => vm.Model as IDetectionEventModel)
                .Where(m => m != null)
                .All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue));

            return allSame ? firstValue : (T?)null;
        }
        catch (Exception)
        {

            throw;
        }
    }

    private static T? CommonOrNullString<T>(IEnumerable<DetectionEventViewModel> list, Func<IDetectionEventModel, T> selector) where T : class?
    {
        try
        {
            if (!list.Any()) return null;

            var models = list.Select(x => x.Model as IDetectionEventModel).ToList();
            var firstModel = list.FirstOrDefault()?.Model as IDetectionEventModel;
            if (firstModel == null) return null;
            T firstValue = selector(firstModel);

            return models.All(m => EqualityComparer<T>.Default.Equals(selector(m), firstValue)) ? firstValue : null;
        }
        catch (Exception)
        {
            throw;
        }
    }

    private static IBaseDeviceModel? CommonOrNullReference(IEnumerable<DetectionEventViewModel> list, DeviceProvider devices, ILogService? log)
    {
        if (!list.Any()) return null;

        var first = list.First()?.Device;
        if (first == null) return null;

        var ret = list
            .Where(m => m?.Device != null)
            .All(m => ReferenceEquals(m!.Device, first))
        ? first
        : null;

        if (ret == null)
            return null;
        else
            return devices.Where(entity => entity.Id == ret.Id)
                .Where(entity => entity.DeviceName == ret.DeviceName).FirstOrDefault();
    }

    public void RefreshAll()
    {
        MessageType = CommonOrNullValue(_selection, m => m.MessageType);
        Device = CommonOrNullReference(_selection, DeviceProvider, _log);
        Result = CommonOrNullValue(_selection, m => m.Result);
        Status = CommonOrNullValue(_selection, m => m.Status);
        DateTime = CommonOrNullValue(_selection, m => m.DateTime);
    }


    #region helpers
    private IBaseDeviceModel? ResolveDevice(IBaseDeviceModel? dev)
        => dev == null
           ? null
           : DeviceProvider.FirstOrDefault(d => d.Id == dev.Id);
    #endregion
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public EnumEventType? MessageType { get; set; }
    public IBaseDeviceModel? Device { get; set; }
    public EnumTrueFalse? Status { get; set; }
    public EnumDetectionType? Result { get; set; }
    public DateTime? DateTime { get; set; }
    public DetectionEventPanelViewModel PanelViewModel { get; }
    public DeviceProvider DeviceProvider { get; }

    // ── 탐지 상세(detail) 읽기 전용 표시 (Detection_Signal_History) ──
    // 계측값이라 편집 대상 아님 — 선택 첫 항목 기준 요약(생성 시점 고정 바인딩, 다이얼로그마다 VM 새로 생성됨)
    private IDetectionEventModel? FirstModel => _selection?.FirstOrDefault()?.Model as IDetectionEventModel;

    /// <summary>신호 크기(detail.signal) — null/0(AI)은 "—".</summary>
    public string SignalText => FirstModel?.Signal is int s and > 0 ? s.ToString("N0") : "—";

    /// <summary>AI 추론 요약 — "yolov8n · 45ms".</summary>
    public string AiSummaryText
    {
        get
        {
            var m = FirstModel;
            if (m == null || (string.IsNullOrEmpty(m.AiModel) && m.InferenceMs is null)) return "—";
            var inference = m.InferenceMs is int ms ? $"{ms}ms" : "-";
            return $"{(string.IsNullOrEmpty(m.AiModel) ? "-" : m.AiModel)} · {inference}";
        }
    }

    /// <summary>AI 탐지 객체 요약 — "person 95% [100,200,50,100]".</summary>
    public string ObjectsText
        => FirstModel?.Objects is { Count: > 0 } objs
            ? string.Join(", ", objs.Select(o =>
                $"{o.Label} {o.Confidence:P0}" + (o.Bbox is { Count: > 0 } b ? $" [{string.Join(",", b)}]" : string.Empty)))
            : "—";

    /// <summary>썸네일 URL(detail.thumbnail).</summary>
    public string ThumbnailText => string.IsNullOrEmpty(FirstModel?.Thumbnail) ? "—" : FirstModel!.Thumbnail!;
    #endregion
    #region - Attributes -
    private IList<DetectionEventViewModel> _selection;
    #endregion
}