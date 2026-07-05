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
