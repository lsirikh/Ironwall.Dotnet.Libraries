using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Sounds.Models;
using Ironwall.Dotnet.Libraries.Sounds.Providers;
using Ironwall.Dotnet.Libraries.Sounds.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using System;

namespace Ironwall.Dotnet.Libraries.Sounds.Ui.ViewModels;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/11/2025 5:35:38 PM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SoundSettingViewModel : BasePanelViewModel
{
    #region - Ctors -
    public SoundSettingViewModel(IEventAggregator ea
                                , ILogService log
                                , ISoundService soundService
                                , SoundSetupModel soundSetupModel
                                , SoundSourceProvider soundSourceProvider
                                , AudioDeviceInfoProvider audioDeviceInfoProvider)
                                : base(ea,log)
    {
        _soundService = soundService;
        SoundSourceProvider = soundSourceProvider;
        AudioDeviceInfoProvider = audioDeviceInfoProvider;
        _soundSetupModel = soundSetupModel;
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
    public string? DirectoryUri
    {
        get { return _soundSetupModel.DirectoryUri; }
        set { _soundSetupModel.DirectoryUri = value; NotifyOfPropertyChange(() => DirectoryUri); }
    }

    public string? DetectionSound
    {
        get { return _soundSetupModel.DetectionSound; }
        set { _soundSetupModel.DetectionSound = value; NotifyOfPropertyChange(() => DetectionSound); }
    }

    public string? MalfunctionSound
    {
        get { return _soundSetupModel.MalfunctionSound; }
        set { _soundSetupModel.MalfunctionSound = value; NotifyOfPropertyChange(() => MalfunctionSound); }
    }

    public string? ActionReportSound
    {
        get { return _soundSetupModel.ActionReportSound; }
        set { _soundSetupModel.ActionReportSound = value; NotifyOfPropertyChange(() => ActionReportSound); }
    }

    public int DetectionSoundDuration
    {
        get { return _soundSetupModel.DetectionSoundDuration; }
        set { _soundSetupModel.DetectionSoundDuration = value; NotifyOfPropertyChange(() => DetectionSoundDuration); }
    }

    public int MalfunctionSoundDuration
    {
        get { return _soundSetupModel.MalfunctionSoundDuration; }
        set { _soundSetupModel.MalfunctionSoundDuration = value; NotifyOfPropertyChange(() => MalfunctionSoundDuration); }
    }

    public int ActionReportSoundDuration
    {
        get { return _soundSetupModel.ActionReportSoundDuration; }
        set { _soundSetupModel.ActionReportSoundDuration = value; NotifyOfPropertyChange(() => ActionReportSoundDuration); }
    }

    public bool IsDetectionAutoSoundStop
    {
        get { return _soundSetupModel.IsDetectionAutoSoundStop; }
        set { _soundSetupModel.IsDetectionAutoSoundStop = value; NotifyOfPropertyChange(() => IsDetectionAutoSoundStop); }
    }

    public bool IsMalfunctionAutoSoundStop
    {
        get { return _soundSetupModel.IsMalfunctionAutoSoundStop; }
        set { _soundSetupModel.IsMalfunctionAutoSoundStop = value; NotifyOfPropertyChange(() => IsMalfunctionAutoSoundStop); }
    }

    public bool IsActionReportAutoSoundStop
    {
        get { return _soundSetupModel.IsActionReportAutoSoundStop; }
        set { _soundSetupModel.IsActionReportAutoSoundStop = value; NotifyOfPropertyChange(() => IsActionReportAutoSoundStop); }
    }

    public SoundSourceProvider SoundSourceProvider { get; }
    public AudioDeviceInfoProvider AudioDeviceInfoProvider { get; }
    #endregion
    #region - Attributes -
    private SoundSetupModel _soundSetupModel;
    private ISoundService _soundService;
    #endregion
}