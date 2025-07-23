using System;

namespace Ironwall.Dotnet.Libraries.Sounds.Models;
/****************************************************************************
   Purpose      :                                                          
   Created By   : GHLee                                                
   Created On   : 7/10/2025 11:33:07 AM                                                    
   Department   : SW Team                                                   
   Company      : Sensorway Co., Ltd.                                       
   Email        : lsirikh@naver.com                                         
****************************************************************************/
public class SoundSetupModel : ISoundSetupModel
{
    #region - Ctors -
    public SoundSetupModel()
    {
    }
    public SoundSetupModel(ISoundSetupModel model)
    {
        if (model is null)
            throw new ArgumentNullException(nameof(model));

        // ---- 단순 값 타입은 얕은 복사로 충분 ----
        DirectoryUri = model.DirectoryUri;
        DetectionSound = model.DetectionSound;
        MalfunctionSound = model.MalfunctionSound;
        ActionReportSound = model.ActionReportSound;
        DetectionSoundDuration = model.DetectionSoundDuration;
        MalfunctionSoundDuration = model.MalfunctionSoundDuration;
        ActionReportSoundDuration = model.ActionReportSoundDuration;
        IsDetectionAutoSoundStop = model.IsDetectionAutoSoundStop;
        IsMalfunctionAutoSoundStop = model.IsMalfunctionAutoSoundStop;
        IsActionReportAutoSoundStop = model.IsActionReportAutoSoundStop;
        AudioDevice = model.AudioDevice;
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
    public string? DirectoryUri { get; set; }
    public string? DetectionSound { get; set; }
    public string? MalfunctionSound { get; set; }
    public string? ActionReportSound { get; set; }
    public int DetectionSoundDuration { get; set; }
    public int MalfunctionSoundDuration { get; set; }
    public int ActionReportSoundDuration { get; set; }
    public bool IsDetectionAutoSoundStop { get; set; }
    public bool IsMalfunctionAutoSoundStop { get; set; }
    public bool IsActionReportAutoSoundStop { get; set; }
    public string? AudioDevice { get; set; }
    #endregion
    #region - Attributes -
    #endregion
}