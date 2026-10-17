using Ironwall.Dotnet.Libraries.Base.Models;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 10/2/2025 12:57:15 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class CameraModel : BaseModel, ICameraModel
    {
        // ========== Primary Key ==========
        /// <summary>
        /// 카메라 고유 식별자 (DB Primary Key)
        /// </summary>
        public string Guid { get; set; } = string.Empty;
        // ========== Core Data ==========
        /// <summary>
        /// RTSP 연결 정보
        /// </summary>
        public RtspConnectionInfo ConnectionInfo { get; set; } = new RtspConnectionInfo();

        /// <summary>
        /// 스트리밍 옵션
        /// </summary>
        public StreamingOptions StreamingOptions { get; set; } = new StreamingOptions();

        /// <summary>
        /// 디스플레이 네임 (사용자 정의)
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// 자동 재생 여부
        /// </summary>
        public bool AutoPlay { get; set; } = true;

        /// <summary>
        /// 컨트롤 표시 여부
        /// </summary>
        public bool ShowControls { get; set; } = false;

    }
}