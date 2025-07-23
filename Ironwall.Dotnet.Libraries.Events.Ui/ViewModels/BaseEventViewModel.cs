using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Events;
using System;
using System.Runtime.CompilerServices;

namespace Ironwall.Dotnet.Libraries.Events.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 6/22/2025 6:56:47 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class BaseEventViewModel<T> : BaseCustomViewModel<T>
                                            , IBaseEventViewModel<T> where T : IBaseEventModel
{
    #region - Ctors -
    protected BaseEventViewModel(T model) : base(model)
    {
    }
    protected BaseEventViewModel(T model, IEventAggregator ea, ILogService log)
        : base(model, ea, log)
    {
    }

    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        return base.OnActivateAsync(cancellationToken);
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    /// <summary>
    /// 속성 변경을 감지하고 IsEdited 플래그 업데이트하는 헬퍼 메서드
    /// </summary>
    protected bool SetProperty<TProperty>(ref TProperty field, TProperty value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<TProperty>.Default.Equals(field, value))
            return false;

        field = value;
        IsEdited = true;
        NotifyOfPropertyChange(propertyName);
        return true;
    }

    /// <summary>
    /// Model 속성용 변경 헬퍼 메서드
    /// </summary>
    protected bool SetModelProperty<TProperty>(TProperty value, TProperty currentValue, Action<TProperty> setValue, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<TProperty>.Default.Equals(currentValue, value))
            return false;

        setValue(value);
        IsEdited = true;
        NotifyOfPropertyChange(propertyName);
        return true;
    }

    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -
    public int Index
    {
        get { return _index; }
        set { _index = value; NotifyOfPropertyChange(() => Index); }
    }

    public EnumEventType MessageType
    {
        get { return _model.MessageType; }
        set
        {
            SetModelProperty(value, _model.MessageType, v => _model.MessageType = v);
        }
    }

    public DateTime DateTime
    {
        get { return _model.DateTime; }
        set
        {
            SetModelProperty(value, _model.DateTime, v => _model.DateTime = v);
        }
    }

    /// <summary>
    /// 편집 여부 플래그
    /// </summary>
    public bool IsEdited
    {
        get { return _isEdited; }
        private set
        {
            _isEdited = value;
            NotifyOfPropertyChange();
        }
    }
    #endregion
    #region - Attributes -
    private int _index;
    private bool _isEdited;

    #endregion
}