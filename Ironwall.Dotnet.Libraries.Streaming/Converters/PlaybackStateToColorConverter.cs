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
                PlaybackState.Playing => new SolidColorBrush(Colors.LimeGreen),
                PlaybackState.Connecting => new SolidColorBrush(Colors.Orange),
                PlaybackState.Buffering => new SolidColorBrush(Colors.Orange),
                PlaybackState.Reconnecting => new SolidColorBrush(Colors.Orange),
                PlaybackState.Paused => new SolidColorBrush(Colors.Gray),
                PlaybackState.Disconnected => new SolidColorBrush(Colors.Gray),
                PlaybackState.Error => new SolidColorBrush(Colors.Red),
                PlaybackState.Restricted => new SolidColorBrush(Colors.Red),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}