using Ironwall.Dotnet.Libraries.AdornerDecorator.Thumbs;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Controls;
using System;
using System.Windows.Controls.Primitives;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Adorners;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/31/2025 3:55:02 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
/// <summary>
/// 기존 MoveThumb을 상속하여 GMap 좌표 동기화 기능만 추가
/// 기존 로직은 100% 재사용, 좌표 변환만 추가
/// </summary>
public class GMapMoveThumb : MoveThumb
{
    public GMapMoveThumb() : base()
    {
        // 기존 이벤트에 GMap 동기화 로직 추가
        DragCompleted += OnGMapMoveCompleted;
    }

    private void OnGMapMoveCompleted(object sender, DragCompletedEventArgs e)
    {
        // GMapAdornerWrapper인지 확인하고 좌표 동기화
        if (DataContext is GMapAdornerWrapper wrapper)
        {
            // 기존 MoveThumb이 이미 Canvas 위치를 업데이트했으므로
            // 지리적 좌표만 동기화하면 됨
            wrapper.OnAdornerPositionChanged();
        }
    }
}