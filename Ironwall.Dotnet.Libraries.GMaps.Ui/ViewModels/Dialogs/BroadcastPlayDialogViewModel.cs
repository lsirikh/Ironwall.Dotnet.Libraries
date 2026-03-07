namespace Ironwall.Dotnet.Libraries.GMaps.Ui.ViewModels.Dialogs;

/// <summary>
/// 음원 재생 다이얼로그 ViewModel
/// </summary>
public class BroadcastPlayDialogViewModel
{
    /// <summary>재생할 파일 그룹 ID</summary>
    public int FileGroupId { get; set; }

    /// <summary>반복 횟수 (기본값: 1)</summary>
    public int Repeat { get; set; } = 1;
}
