using Ironwall.Dotnet.Libraries.AdornerDecorator.Thumbs;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Controls;
using System;
using System.Windows.Controls.Primitives;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/31/2025 3:59:30 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 기존 ResizeThumb을 상속하여 GMap 좌표 동기화 기능만 추가
/// </summary>
public class GMapResizeThumb : ResizeThumb
{
    public GMapResizeThumb() : base()
    {
        DragCompleted += OnGMapResizeCompleted;
    }

    private void OnGMapResizeCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is GMapAdornerWrapper wrapper)
        {
            // 기존 ResizeThumb이 이미 크기를 업데이트했으므로
            // 지리적 경계만 동기화하면 됨
            wrapper.OnAdornerSizeChanged();
        }
    }
}