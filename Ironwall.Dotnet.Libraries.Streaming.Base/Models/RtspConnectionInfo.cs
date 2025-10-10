
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
    /// 연결 정보 유효성 검사
    /// </summary>
    public bool IsValid()
    {
        // URL이 있거나 IP 주소가 있으면 유효
        return !string.IsNullOrEmpty(Url) || !string.IsNullOrEmpty(IpAddress);
    }
}