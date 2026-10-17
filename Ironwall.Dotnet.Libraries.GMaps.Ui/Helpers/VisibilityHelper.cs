using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/3/2025 5:18:06 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// bool 값을 Visibility로 변환하는 헬퍼 클래스
    /// </summary>
    public static class VisibilityHelper
    {
        /// <summary>
        /// bool을 Visibility로 변환
        /// </summary>
        /// <param name="value">변환할 bool 값</param>
        /// <returns>true면 Visible, false면 Collapsed</returns>
        public static Visibility ToVisibility(bool value)
        {
            return value ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// bool을 Visibility로 변환 (Hidden 옵션)
        /// </summary>
        /// <param name="value">변환할 bool 값</param>
        /// <param name="useHidden">false일 때 Hidden 사용 여부</param>
        /// <returns>true면 Visible, false면 Collapsed 또는 Hidden</returns>
        public static Visibility ToVisibility(bool value, bool useHidden)
        {
            if (value) return Visibility.Visible;
            return useHidden ? Visibility.Hidden : Visibility.Collapsed;
        }

        /// <summary>
        /// Visibility를 bool로 변환
        /// </summary>
        /// <param name="visibility">변환할 Visibility</param>
        /// <returns>Visible이면 true, 나머지는 false</returns>
        public static bool ToBool(Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }

        /// <summary>
        /// 역방향 변환 (Inverted)
        /// </summary>
        /// <param name="value">변환할 bool 값</param>
        /// <returns>true면 Collapsed, false면 Visible</returns>
        public static Visibility ToVisibilityInverted(bool value)
        {
            return value ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}