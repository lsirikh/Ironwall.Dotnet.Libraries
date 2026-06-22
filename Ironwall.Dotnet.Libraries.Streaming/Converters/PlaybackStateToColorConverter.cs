using Ironwall.Dotnet.Libraries.Streaming.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Ironwall.Dotnet.Libraries.Streaming.Converters;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 9/24/2025 3:21:01 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// PlaybackState를 Color로 변환
/// </summary>
public class PlaybackStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PlaybackState state)
        {
            return state switch
            {
                PlaybackState.Playing             => new SolidColorBrush(Colors.LimeGreen),
                PlaybackState.Connecting          => new SolidColorBrush(Colors.DodgerBlue),
                PlaybackState.Buffering           => new SolidColorBrush(Colors.DodgerBlue),
                PlaybackState.Reconnecting        => new SolidColorBrush(Colors.Orange),
                PlaybackState.Paused              => new SolidColorBrush(Colors.Gray),
                PlaybackState.Disconnected        => new SolidColorBrush(Colors.Gray),
                PlaybackState.Error               => new SolidColorBrush(Colors.Red),
                PlaybackState.Restricted          => new SolidColorBrush(Colors.Red),
                PlaybackState.SharedStreamWaiting => new SolidColorBrush(Colors.DodgerBlue),
                PlaybackState.SharedStreamPlaying => new SolidColorBrush(Colors.LimeGreen),
                _                                 => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>PlaybackState → 한글 상태 레이블 (배지 텍스트용)</summary>
public class PlaybackStateToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PlaybackState state)
        {
            return state switch
            {
                PlaybackState.Playing             => "스트리밍",
                PlaybackState.Connecting          => "연결 중",
                PlaybackState.Buffering           => "버퍼링",
                PlaybackState.Reconnecting        => "재연결 중",
                PlaybackState.Paused              => "일시정지",
                PlaybackState.Stopped             => "중단",
                PlaybackState.Disconnected        => "연결 끊김",
                PlaybackState.Error               => "오류",
                PlaybackState.Restricted          => "제한",
                PlaybackState.SharedStreamWaiting => "공유 대기",
                PlaybackState.SharedStreamPlaying => "공유 스트리밍",
                PlaybackState.None                => "",
                _                                 => ""
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}