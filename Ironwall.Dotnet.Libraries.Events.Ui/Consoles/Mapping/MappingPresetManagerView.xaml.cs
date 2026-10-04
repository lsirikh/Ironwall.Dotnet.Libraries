using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Mapping;
/****************************************************************************
   Purpose      : 카메라 프리셋 관리 창 코드비하인드 — 버튼 배선만
   Created By   : Claude
   Created On   : 2026-10-04
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>카메라 프리셋 관리 뷰. 판정은 전부 <see cref="MappingPresetManagerViewModel"/> 에 있다.</summary>
public partial class MappingPresetManagerView : UserControl
{
    /// <summary>생성자.</summary>
    public MappingPresetManagerView()
    {
        InitializeComponent();
    }

    private MappingPresetManagerViewModel? Model => DataContext as MappingPresetManagerViewModel;

    private void OnNew(object sender, RoutedEventArgs e) => Model?.BeginNew();

    private void OnSave(object sender, RoutedEventArgs e) => _ = Model?.SaveAsync();

    private void OnDelete(object sender, RoutedEventArgs e) => Model?.RequestDelete();

    private void OnCancelDelete(object sender, RoutedEventArgs e) => Model?.CancelDelete();

    private void OnConfirmDelete(object sender, RoutedEventArgs e) => _ = Model?.ConfirmDeleteAsync();

    private void OnReadCamera(object sender, RoutedEventArgs e) => _ = Model?.ReadCameraPresetsAsync();

    private void OnUseCameraPreset(object sender, RoutedEventArgs e)
        => Model?.UseCameraPreset((sender as FrameworkElement)?.DataContext as MappingCameraPresetRow);

    private void OnClose(object sender, RoutedEventArgs e) => _ = Model?.CloseAsync();
}
