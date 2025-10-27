using Ironwall.Dotnet.Libraries.Streaming.Base.Models;

namespace Ironwall.Dotnet.Libraries.Streaming.Serivces;
public interface IPopupViewerService
{
    /// <summary>
    /// 팝업 윈도우를 닫습니다.
    /// </summary>
    void ClosePopup();

    /// <summary>
    /// 팝업 윈도우에 카메라를 표시합니다.
    /// </summary>
    void ShowPopup(string? eventId = null, params ICameraModel[] connections);

    /// <summary>
    /// 팝업 윈도우의 위치를 설정합니다.
    /// </summary>
    /// <param name="position"></param>
    void SetDisplayPosition(EnumDisplayPosition? position = null);

    /// <summary>
    /// 팝업 윈도우의 마진을 설정합니다.
    /// </summary>
    /// <param name="offset"></param>
    void SetMarginOffset(int offset);

    /// <summary>
    /// 특정 RowId를 가진 Row를 제거합니다.
    /// </summary>
    /// <param name="rowId">제거할 Row의 ID</param>
    /// <returns>제거 성공 여부</returns>
    bool RemoveRowById(string rowId);

    /// <summary>
    /// 여러 RowId를 한 번에 제거합니다.
    /// </summary>
    /// <param name="rowIds">제거할 Row의 ID 배열</param>
    void RemoveRowsByIds(params string[] rowIds);

    /// <summary>
    /// 팝업이 열려있는지 여부
    /// </summary>
    bool IsPopupOpen { get; }

    /// <summary>
    /// 현재 관리 중인 모든 CameraRow의 ID 목록
    /// </summary>
    List<string> ListedCameraRowIds { get; }

    /// <summary>
    /// 삭제된 카메라 Row의 정보를 전달하기 위한 이벤트
    /// </summary>
    event EventHandler<string>? RemoveCameraRow;

    /// <summary>
    /// 창이 닫히는 이벤트도 수신
    /// </summary>
    event EventHandler? RequestClose;

}