using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Presets;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Wiring;
using Ironwall.Dotnet.Libraries.Devices.Ui.Tests;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Devices.Ui.Views.Dialogs;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Devices;
using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System.Windows;
using System.Windows.Controls;

namespace DeviceConsolePreview;

/// <summary>
/// 다이얼로그 가족 T4 미리보기(N-05) — 진짜 틀 + 진짜 뷰모델을 가짜 데이터 위에 띄운다.
/// 서버도 호스트도 없다. <c>--dialogs [--dark] [--snapshot &lt;폴더&gt;]</c>.
/// </summary>
internal sealed class DialogPreview
{
    /// <summary>S · M · L 세 규격을 한 장에 — 폭이 세 가지뿐이라는 것이 눈에 보이게.</summary>
    public FrameworkElement Gallery()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(Sample(DialogSize.Small, "S 400", "확인 · 안내 · 한 줄 묻기", "버튼 줄 왼쪽은 안내, 오른쪽은 보조 → 주 동작."));
        panel.Children.Add(Sample(DialogSize.Medium, "M 560", "폼 하나 — 계정 등록 · 조치보고", "높이는 내용이 정하고 상한만 규격이 정한다."));
        panel.Children.Add(Sample(DialogSize.Large, "L 720", "두 칸이 나란히 — 장비 배정", "어떤 경우에도 창보다 넓어지지 않는다."));
        return panel;
    }

    private static FrameworkElement Sample(DialogSize size, string title, string kind, string body)
    {
        var frame = new ConsoleDialogFrame
        {
            Size = size,
            DialogKey = $"Preview.{size}",
            Title = title,
            Kind = kind,
            Message = size == DialogSize.Large ? "보낼 변화 ＋2 · −1 (호출 2회)" : "ESC = 취소 · Enter = 주 동작",
            MessageSeverity = size == DialogSize.Large ? DialogMessageSeverity.Info : DialogMessageSeverity.Normal,
            PrimaryText = "저장",
            SecondaryText = "취소",
            IsPrimaryEnabled = size != DialogSize.Small,
            Margin = new Thickness(8, 0, 8, 0),
            Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, MinHeight = size == DialogSize.Small ? 60 : 120 },
        };
        return frame;
    }

    #region - Migrated dialogs -
    public FrameworkElement ConfirmPrompt()
        => new ConfirmPromptView { DataContext = new ConfirmPromptViewModel("프리셋 삭제", "'함체 · 표준 6부품' 을(를) 지운다. 이 프리셋으로 이미 만든 장비에는 영향이 없다.") };

    public FrameworkElement TextPrompt()
        => new TextPromptView { DataContext = new TextPromptViewModel("이름 바꾸기", "프리셋 이름", "함체 · 표준 6부품") };

    public FrameworkElement RepeatExpand(bool withConflict)
    {
        var catalog = new PreviewComponentCatalog();
        var palette = catalog.ComponentTypes(EnumDeviceCategory.Controller).Select(i => new PaletteItemViewModel(i)).ToList();
        var existing = withConflict ? new[] { "ci_03", "ci_07" } : Array.Empty<string>();
        var vm = new RepeatExpandViewModel(palette, palette.FirstOrDefault(p => p.Code == "CONTACT_INPUT"), existing) { KeyFormat = "ci_{02d}" };
        return new RepeatExpandView { DataContext = vm };
    }

    public FrameworkElement WiringPrompt() => new WiringPreview().SaveConfirm();

    public FrameworkElement MakeSensors(bool withConflict) => new WiringPreview().MakeSensors(withConflict);

    public FrameworkElement PasteReport() => new WiringPreview().PasteReport();

    public FrameworkElement PresetManager(string workDirectory) => new AssemblyPreview(workDirectory).PresetManager();

    public FrameworkElement Register(string workDirectory, bool withProblem) => new AssemblyPreview(workDirectory).Register(withProblem);
    #endregion

    #region - Assign dialog -
    /// <summary>배정 창을 만들고, 원하는 상태까지 몰아 놓는다.</summary>
    public (FrameworkElement View, DeviceAssignDialogViewModel Vm) Assign(AssignState state)
    {
        var models = Devices();
        var api = new MockDeviceApiService();

        if (state == AssignState.PartialFailure)
            api.AssignHook = (_, dto) => ApiResponse<DeviceGroupAssignResultDto>.CreateSuccess(
                new DeviceGroupAssignResultDto { AssignedDeviceIds = dto.DeviceIds.Take(1).ToList(), SkippedDeviceIds = dto.DeviceIds.Skip(1).ToList() });

        var vm = new DeviceAssignDialogViewModel(api, () => models, log: new MockLogService());
        vm.Initialize(10, "동측 1구역", new[] { 101, 104 });

        switch (state)
        {
            case AssignState.MultiSelect:
                vm.SetSelection(AssignSide.Available, vm.Available.Take(3).ToList());
                break;
            case AssignState.Blocked:
                // 오른쪽 행을 오른쪽에 떨어뜨리려 한다 — 까닭이 버튼 줄에 뜬다.
                vm.CanDrop(PreviewPayload(vm.Assigned.Take(1)), new Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag.DropTarget(AssignDelta.AssignedZone, null, -1));
                break;
            case AssignState.Dirty:
                vm.SetSelection(AssignSide.Available, vm.Available.Take(2).ToList());
                vm.AssignSelected();
                vm.SetSelection(AssignSide.Assigned, vm.Assigned.Where(i => i.Id == 101).ToList());
                vm.UnassignSelected();
                break;
            case AssignState.PartialFailure:
                vm.SetSelection(AssignSide.Available, vm.Available.Take(3).ToList());
                vm.AssignSelected();
                vm.SaveAsync().GetAwaiter().GetResult();
                break;
        }

        return (new DeviceAssignDialogView { DataContext = vm }, vm);
    }

    /// <summary>미저장 그룹 — 빈 칸이 아니라 까닭을 낸다.</summary>
    public FrameworkElement AssignUnsavedGroup()
    {
        var vm = new DeviceAssignDialogViewModel(new MockDeviceApiService(), Devices);
        vm.Initialize(0, "새 그룹", Array.Empty<int>());
        return new DeviceAssignDialogView { DataContext = vm };
    }

    private static Ironwall.Dotnet.Libraries.Utils.Behaviors.Drag.DragPayload PreviewPayload(IEnumerable<DeviceAssignItemViewModel> items)
        => new(null!, items.Cast<object>().ToList(), "행");

    private static List<IBaseDeviceModel> Devices()
    {
        var list = new List<IBaseDeviceModel>();
        var names = new[] { "동측 게이트 카메라", "북측 울타리 센서 12", "서측 스피커", "중앙 제어기 2", "남측 경광등", "정문 함체", "후문 통문", "옥상 카메라 3" };
        for (var i = 0; i < names.Length; i++)
        {
            list.Add(new ControllerDeviceModel
            {
                Id = 101 + i,
                DeviceNumber = 1 + i,
                DeviceName = names[i],
                DeviceGroups = i is 0 or 3 ? new List<int> { 10 } : new List<int>(),
            });
        }
        return list;
    }
    #endregion

    #region - Progress -
    public (FrameworkElement View, ProgressDialogViewModel Vm) Progress(bool cancelled)
    {
        var vm = new ProgressDialogViewModel("장비를 등록하는 중", "프리셋 '함체 · 표준 6부품' 으로 16대");
        vm.Report(7, 16, "정문 함체 7");
        if (cancelled)
        {
            vm.RequestCancel();
            vm.Finish();
        }
        return (new ProgressDialogView { DataContext = vm }, vm);
    }
    #endregion
}

internal enum AssignState
{
    Loaded,
    MultiSelect,
    Blocked,
    Dirty,
    PartialFailure,
}
