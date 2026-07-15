
using Ironwall.Dotnet.Libraries.Base.Models;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ironwall.Dotnet.Libraries.Streaming.Base.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 2:24:30 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// RTSP 연결 정보 DTO (순수 데이터 객체)
/// </summary>
public class RtspConnectionInfo : BaseModel
{
    public string Url { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 554;
    public string Protocol { get; set; } = "rtsp";
    public string StreamPath { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CameraName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int StreamType { get; set; } = 0; // 0: Main Stream, 1: Sub Stream
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 전체 RTSP URL 생성
    /// </summary>
    public string GetFullUrl()
    {
        // 이미 완전한 URL이 있으면 그대로 반환
        if (!string.IsNullOrEmpty(Url))
            return Url;

        // 구성 요소로부터 URL 빌드
        return BuildUrl();
    }

    private string BuildUrl()
    {
        if (string.IsNullOrEmpty(IpAddress))
            return string.Empty;

        var auth = string.Empty;
        if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
        {
            auth = $"{Username}:{Password}@";
        }

        var path = string.IsNullOrEmpty(StreamPath) ? "" : $"/{StreamPath.TrimStart('/')}";

        return $"{Protocol}://{auth}{IpAddress}:{Port}{path}";
    }

    /// <summary>
    /// Hub가 생성한 로컬 relay URL인 경우 true.
    /// ImprovedRtspStreamingService는 이 경우 sout relay 생성을 건너뜀.
    /// </summary>
    public bool IsLocalRelay { get; set; } = false;

    /// <summary>
    /// Hub relay URL로부터 RtspConnectionInfo를 생성한다.
    /// 예: "rtsp://127.0.0.1:15554/192.168.1.1:554/ch0"
    /// </summary>
    public static RtspConnectionInfo FromRelayUrl(string relayUrl)
    {
        var uri = new Uri(relayUrl);
        return new RtspConnectionInfo
        {
            IpAddress    = uri.Host,
            Port         = uri.Port,
            StreamPath   = uri.AbsolutePath.TrimStart('/'),
            Protocol     = uri.Scheme,
            IsLocalRelay = true
        };
    }

    /// <summary>
    /// 객체 복제
    /// </summary>
    public RtspConnectionInfo Clone()
    {
        return new RtspConnectionInfo
        {
            Id  = this.Id,
            Url = this.Url,
            Username = this.Username,
            Password = this.Password,
            IpAddress = this.IpAddress,
            Port = this.Port,
            Protocol = this.Protocol,
            StreamPath = this.StreamPath,
            ChannelId = this.ChannelId,
            Description = this.Description,
            CameraName = this.CameraName,
            Location = this.Location,
            StreamType = this.StreamType,
            IsEnabled = this.IsEnabled
        };
    }

    public override string ToString()
    {
        return !string.IsNullOrEmpty(Description) ? Description : GetFullUrl();
    }

    /// <summary>
    /// 카메라 고유 키 — credential만 제외, host:port/path[?query] 형식(쿼리 포함).
    /// Hub / SharedSession / Row 전체에서 동일한 키를 사용하기 위한 표준 메서드.
    /// ※ 쿼리를 제외하면 <c>?channel=</c>류로만 구분되는 서로 다른 카메라가 같은 디코더로
    ///   병합되어 두 팝업이 동일 영상을 공유한다(A/B 동일영상 버그) — 파생 로직은
    ///   <see cref="RtspCameraKey.Derive"/>(순수 헬퍼, 단위테스트 소스링크 대상) 참조.
    /// </summary>
    public string GetCameraKey()
        => RtspCameraKey.Derive(GetFullUrl(), IpAddress, Port, StreamPath);

    /// <summary>
    /// 연결 정보 유효성 검사
    /// </summary>
    public bool IsValid()
    {
        // URL이 있거나 IP 주소가 있으면 유효
        return !string.IsNullOrEmpty(Url) || !string.IsNullOrEmpty(IpAddress);
    }
}