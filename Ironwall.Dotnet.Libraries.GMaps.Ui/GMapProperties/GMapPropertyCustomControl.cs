using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapProperties{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 8/28/2025 8:08:56 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// GMapCustomMarker 전용 속성 편집 컨트롤
    /// </summary>
    public class GMapPropertyCustomControl : GMapPropertyBaseControl
    {
        #region Static Constructor
        static GMapPropertyCustomControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GMapPropertyCustomControl),
                new FrameworkPropertyMetadata(typeof(GMapPropertyCustomControl)));
        }
        #endregion

        #region Additional Dependency Properties
        #endregion

        #region Constructor

        public GMapPropertyCustomControl()
        {
            // 기본 클래스 초기화는 자동으로 수행됨
        }
        //protected override FrameworkElement CreateSpecificPropertiesPanel()
        //{
        //}
        protected override void SetupSpecificBindings()
        {
        }

        protected override void ClearSpecificBindings()
        {
        }

        protected override void SetupSpecificPropertiesFromMarker(IEditableMarker marker)
        {
        }

        protected override void UpdateSpecificProperties()
        {
        }

        public override Type GetSupportedMarkerType()
        {
            return typeof(GMapPropertyCustomControl);
        }
        #endregion

        #region Override Methods
        #endregion

        #region Property Changed Callbacks
        #endregion

        #region Fields
        #endregion
    }
}