using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using Microsoft.Xaml.Behaviors;
using System;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Behaviors{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 9/1/2025 6:49:57 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public class PreventBindingContaminationBehavior : Behavior<GMapPropertyBaseControl>
    {
        #region Dependency Properties

        /// <summary>
        /// SelectedMarker를 모니터링하는 프록시 속성
        /// </summary>
        public IEditableMarker SelectedMarker
        {
            get { return (IEditableMarker)GetValue(SelectedMarkerProperty); }
            set { SetValue(SelectedMarkerProperty, value); }
        }

        public static readonly DependencyProperty SelectedMarkerProperty =
            DependencyProperty.Register("SelectedMarker", typeof(IEditableMarker),
                typeof(PreventBindingContaminationBehavior),
                new PropertyMetadata(null, OnSelectedMarkerChanging));

        #endregion

        #region Behavior Override

        protected override void OnAttached()
        {
            base.OnAttached();

            // AssociatedObject의 SelectedMarker와 바인딩 설정
            var binding = new Binding("SelectedMarker")
            {
                Source = AssociatedObject,
                Mode = BindingMode.OneWay
            };

            BindingOperations.SetBinding(this, SelectedMarkerProperty, binding);

            System.Diagnostics.Debug.WriteLine("PreventBindingContamination Behavior 연결됨");
        }

        protected override void OnDetaching()
        {
            BindingOperations.ClearBinding(this, SelectedMarkerProperty);
            base.OnDetaching();

            System.Diagnostics.Debug.WriteLine("PreventBindingContamination Behavior 해제됨");
        }

        #endregion

        #region Property Changed Handler

        private static void OnSelectedMarkerChanging(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PreventBindingContaminationBehavior behavior && behavior.AssociatedObject != null)
            {
                behavior.HandleSelectedMarkerChanging(e.OldValue as IEditableMarker, e.NewValue as IEditableMarker);
            }
        }

        private void HandleSelectedMarkerChanging(IEditableMarker oldMarker, IEditableMarker newMarker)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== Behavior: SelectedMarker 변경 감지 ===");
                System.Diagnostics.Debug.WriteLine($"이전 마커: {oldMarker?.Title ?? "null"}");
                System.Diagnostics.Debug.WriteLine($"새 마커: {newMarker?.Title ?? "null"}");

                // 핵심: 새 마커 설정 전에 바인딩 정리
                if (oldMarker != null && newMarker != null && oldMarker != newMarker)
                {
                    System.Diagnostics.Debug.WriteLine("바인딩 오염 방지를 위해 ClearAllBindings 실행");

                    // AssociatedObject에서 ClearAllBindings 호출
                    AssociatedObject.ClearAllBindings();

                    // 추가적으로 이전 마커와의 연결 해제
                    DisconnectFromOldMarker(oldMarker);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Behavior 처리 중 오류: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 이전 마커와의 연결 해제
        /// </summary>
        private void DisconnectFromOldMarker(IEditableMarker oldMarker)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"이전 마커 '{oldMarker.Title}' 연결 해제");

                // 필요시 이전 마커의 PropertyChanged 이벤트 구독 해제
                if (oldMarker is INotifyPropertyChanged notifyObj)
                {
                    // 이미 구독했다면 해제 (WeakEventManager 사용 권장)
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"이전 마커 연결 해제 실패: {ex.Message}");
            }
        }

        #endregion
    }
}