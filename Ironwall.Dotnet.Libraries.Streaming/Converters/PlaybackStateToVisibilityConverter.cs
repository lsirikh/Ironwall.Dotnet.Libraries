using Ironwall.Dotnet.Libraries.Streaming.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Streaming.Converters;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:21:33 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// PlaybackState를 Visibility로 변환
/// </summary>
public class PlaybackStateToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PlaybackState state && parameter is string param)
        {
            switch (param.ToLower())
            {
                case "loading":
                    return (state == PlaybackState.Connecting ||
                            state == PlaybackState.Buffering ||
                            state == PlaybackState.Reconnecting)
                        ? Visibility.Visible : Visibility.Collapsed;

                case "error":
                    return (state == PlaybackState.Error ||
                            state == PlaybackState.Disconnected)
                        ? Visibility.Visible : Visibility.Collapsed;

                case "playing":
                    return state == PlaybackState.Playing
                        ? Visibility.Visible : Visibility.Collapsed;

                case "canplay":
                    return (state != PlaybackState.Playing &&
                            state != PlaybackState.Connecting &&
                            state != PlaybackState.Buffering &&
                            state != PlaybackState.Reconnecting &&
                            state != PlaybackState.Restricted &&
                            state != PlaybackState.ImageDisplay)  // 이미지 표시 중에는 Play 버튼 숨김
                        ? Visibility.Visible : Visibility.Collapsed;

                case "controls":
                    return (state == PlaybackState.Playing ||
                            state == PlaybackState.Paused ||
                            state == PlaybackState.ImageDisplay)  // 이미지 표시 중에도 컨트롤 표시
                        ? Visibility.Visible : Visibility.Collapsed;

                case "none":
                    return state == PlaybackState.None
                        ? Visibility.Visible : Visibility.Collapsed;

                case "imagedisplay":
                    return state == PlaybackState.ImageDisplay
                        ? Visibility.Visible : Visibility.Collapsed;

                case "videocontent":  // 비디오 재생 중이거나 일시정지 상태
                    return (state == PlaybackState.Playing || state == PlaybackState.Paused)
                        ? Visibility.Visible : Visibility.Collapsed;

                case "restricted":  // 제한구역
                    return state == PlaybackState.Restricted
                        ? Visibility.Visible : Visibility.Collapsed;

                default:
                    return Visibility.Collapsed;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}