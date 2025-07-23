using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Collections.ObjectModel;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/19/2025 10:10:48 AM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public sealed class CameraPresetsViewModel : BasePanelViewModel
    {
        /* 실제 Preset 수정이 필요 없으면 ReadOnlyCollection 으로 교체 */
        public ObservableCollection<ICameraPresetModel> Presets { get; } = new ObservableCollection<ICameraPresetModel>();

        public CameraPresetsViewModel(IEnumerable<ICameraPresetModel> presets = default)
        {
            if(presets != null) 
            {
                Presets = new ObservableCollection<ICameraPresetModel>(presets);
            }
        }

        /*──────────── 버튼 액션 예시 ───────────*/
        public void OnClickGotoPreset(ICameraPresetModel? preset)
        {
            if (preset is null) return;
            _log?.Info($"GotoPreset {preset.Preset}:{preset.Name}");
            /* PTZClient.GotoPresetAsync(...) 호출은 외부 서비스에 위임 */
        }

        public void OnClickAddPreset()
        {
            Presets.Add(new CameraPresetModel { Preset = 0, Name = "New", Description = "" });
        }

        public void OnClickRemovePreset(ICameraPresetModel? preset)
        {
            if (preset is null) return;
            Presets.Remove(preset);
        }
    }
}