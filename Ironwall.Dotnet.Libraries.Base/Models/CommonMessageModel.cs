using System;
using System.ComponentModel;

namespace Ironwall.Dotnet.Libraries.Base.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 2/10/2025 6:25:42 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public abstract class CommonMessageModel
        : INotifyPropertyChanged, ICommonMessageModel
{
    private string _title;
    private string _content;
    private IMessageModel _messageModel;

    public string Title
    {
        get { return _title; }
        set
        {
            _title = value;
            OnPropertyChanged("Title");
        }
    }

    public string Explain
    {
        get { return _content; }
        set
        {
            _content = value;
            OnPropertyChanged("Content");
        }
    }

    public IMessageModel MessageModel
    {
        get { return _messageModel; }
        set
        {
            _messageModel = value;
            OnPropertyChanged("MessageModel");
        }
    }

    /// <summary>
    /// 이벤트 Method
    /// </summary>
    /// <param name="PropertyName"></param>
    public void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public event PropertyChangedEventHandler PropertyChanged;

}