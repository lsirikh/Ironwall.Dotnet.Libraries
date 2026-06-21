using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Base.Services;
using System;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/11/2025 2:30:02 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseCustomViewModel<T> : SelectableBaseViewModel, IBaseCustomViewModel<T> where T : IBaseModel
{

    #region - Ctors -
    protected BaseCustomViewModel(T model)
    {
        _model = model;
    }
    protected BaseCustomViewModel(T model, IEventAggregator eventAggregator, ILogService log)
        : base(eventAggregator, log)
    {
        _model = model;
    }
    #endregion
    #region - Implementation of Interface -
    public virtual void UpdateModel(T model)
    {
        _model = model;
        Refresh();
    }
    #endregion
    #region - Overrides -
    public abstract void Dispose();
    public virtual void OnLoaded(object sender, SizeChangedEventArgs e) { }

    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public int Id
    {
        get { return _model.Id; }
        set
        {
            _model.Id = value;
            NotifyOfPropertyChange(() => Id);
            NotifyOfPropertyChange(() => IsDraft);
        }
    }

    /// <summary>(PR-C) Temp/미저장 행(Id≤0) — DataGrid 행 시각마커 바인딩용. Save 등록 시 Id&gt;0 → false.</summary>
    public bool IsDraft => _model.Id <= 0;

    public T Model
    {
        get => _model;
        set { _model = value; NotifyOfPropertyChange(() => Model); NotifyOfPropertyChange(() => IsDraft); }
    }

    #endregion
    #region - Attributes -
    protected T _model;
    #endregion
}