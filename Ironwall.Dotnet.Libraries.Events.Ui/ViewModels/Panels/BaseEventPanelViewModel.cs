using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels.Panels{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 7/1/2025 7:38:34 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public abstract class BaseEventPanelViewModel<T> : BasePanelViewModel where T: class
    {
        #region - Ctors -
        protected BaseEventPanelViewModel(IEventAggregator ea
                                          , ILogService log) 
                                        : base(ea, log)
        {
            ViewModelProvider = new ObservableCollection<T>();
        }
        #endregion
        #region - Implementation of Interface -
        #endregion
        #region - Overrides -
        #endregion
        #region - Binding Methods -
        #endregion
        #region - Processes -
        #endregion
        #region - IHanldes -
        #endregion
        #region - Properties -
        public ObservableCollection<T> ViewModelProvider { get; set; }
        public string? NameTabHeader { get; set; }

        public string? NameKind
        {
            get { return _nameKind; }
            set
            {
                _nameKind = value;
                NotifyOfPropertyChange(() => NameKind);
            }
        }
        #endregion
        #region - Attributes -
        protected string? _nameKind;
        #endregion
    }
}