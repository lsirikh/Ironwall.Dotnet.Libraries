using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Maps;
/****************************************************************************
   Purpose      : GMaps 전용 확인 팝업 콜백 메시지 (EventAggregator 패턴)
   Created By   : GHLee
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 그룹(다중) 심볼 삭제 확인 콜백. <c>OpenConfirmPopupMessageModel.MessageModel</c> 로 실려 발행되며,
/// 사용자가 "확인"을 누르면 팝업 인프라가 이 메시지를 재발행 → <c>MapViewModel.HandleAsync</c> 가 실제 삭제 수행.
/// (raw <c>MessageBox.Show</c> 대신 프로젝트 표준 EventAggregator 확인 팝업 패턴 — ROI 삭제와 동일 방식)
/// </summary>
public class CallDeleteGroupSymbolsProcessMessageModel : IMessageModel { }

/// <summary>
/// 단일 선택(마커/오버레이 이미지) 삭제 확인 콜백. 사용자가 "확인"을 누르면 <c>MapViewModel.HandleAsync</c> 가
/// 실제 삭제 수행. 오버레이 이미지 삭제는 PNG 파일까지 영구 삭제되고 Undo가 불가하므로 확인 없이 삭제되던
/// 위험(Delete 키 오입력 시 데이터 손실)을 차단하기 위해 도입.
/// </summary>
public class CallDeleteSelectedProcessMessageModel : IMessageModel { }

/// <summary>
/// 카메라 영상 팝업 전체 닫기 확인 콜백(FR-B2). 맵 우하단 카운터 위젯의 ✕ → 표준 확인 팝업
/// (<c>OpenConfirmPopupMessageModel</c>) → "확인" 시 팝업 인프라가 이 메시지를 재발행 →
/// <c>MapViewModel.HandleAsync</c> 가 열린 모든 카메라 팝업을 순차 닫는다(Hub Lease/PTZ 정지 포함).
/// </summary>
public class CallCloseAllCameraPopupsProcessMessageModel : IMessageModel { }

/// <summary>프리셋 삭제 확인 콜백(파괴적 동작 — 되돌릴 수 없음). <c>OpenConfirmPopupMessageModel</c> "확인" 시
/// 팝업 인프라가 이 메시지를 재발행 → <c>MapViewModel.HandleAsync</c> 가 대기 대상(_pendingPresetDelete)을
/// ONVIF RemovePreset 후 목록 재조회. raw MessageBox 금지 — 표준 EventAggregator 확인 패턴.</summary>
public class CallDeletePresetProcessMessageModel : IMessageModel { }
