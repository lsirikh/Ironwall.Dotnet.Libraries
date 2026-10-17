using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Devices.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.ViewModel.ViewModels.Components;
using Ironwall.Dotnet.Monitoring.Models.Devices;
using System;
using System.Linq;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 패널 뷰모델 어댑터(device-console-redesign CD-1 · FR-11 · FR-12).
/// 어댑터는 패널의 기존 버튼 경로를 그대로 부르기만 한다 — 새 전송 경로가 없다는 것과, 끝남을 어떻게 아는지를 고정한다.
/// </summary>
[Collection("CaliburnIoC")]
public class DeviceConsoleSourceTests : IDisposable
{
    private readonly TestIoCScope _ioc = new();
    public void Dispose() => _ioc.Dispose();

    [Fact]
    public void should_return_detached_draft_when_panel_inserts_a_row()
    {
        var panel = new FakePanel();
        var source = new DeviceConsoleSource<LampDeviceViewModel>(panel);

        var draft = source.CreateDraft();

        Assert.NotNull(draft);
        Assert.Empty(panel.ViewModelProvider);   // [등록] 전에는 목록에 아무것도 남지 않는다
        Assert.Equal(1, panel.InsertCalls);
    }

    [Fact]
    public void should_return_null_when_panel_refuses_to_insert()
    {
        var panel = new FakePanel { RefuseInsert = true };
        var source = new DeviceConsoleSource<LampDeviceViewModel>(panel);

        Assert.Null(source.CreateDraft());
    }

    [Fact]
    public void should_put_draft_back_once_when_adopted_twice()
    {
        var panel = new FakePanel();
        var source = new DeviceConsoleSource<LampDeviceViewModel>(panel);
        var draft = source.CreateDraft()!;

        source.AdoptDraft(draft);
        source.AdoptDraft(draft);

        Assert.Single(panel.ViewModelProvider);
    }

    [Fact]
    public void should_call_existing_panel_paths_when_save_delete_reload_are_requested()
    {
        var panel = new FakePanel();
        var source = new DeviceConsoleSource<LampDeviceViewModel>(panel);

        source.Save();
        source.Delete();
        source.Reload();

        Assert.Equal((1, 1, 1), (panel.SaveCalls, panel.DeleteCalls, panel.ReloadCalls));
    }

    [Fact]
    public void should_report_started_only_when_panel_turns_busy_during_the_call()
    {
        // 패널의 버튼 경로는 async void 다 — 권한이 없거나 다른 일을 하는 중이면 말없이 돌아온다.
        var accepting = new FakePanel { TurnsBusyOnSave = true };
        var refusing = new FakePanel();

        Assert.True(new DeviceConsoleSource<LampDeviceViewModel>(accepting).Save());
        Assert.False(new DeviceConsoleSource<LampDeviceViewModel>(refusing).Save());
    }

    [Fact]
    public void should_clear_selected_flag_on_rows_that_are_no_longer_selected()
    {
        var panel = new FakePanel();
        var source = new DeviceConsoleSource<LampDeviceViewModel>(panel);
        var first = new LampDeviceViewModel(new LampDeviceModel { Id = 1 }) { IsSelected = true };
        var second = new LampDeviceViewModel(new LampDeviceModel { Id = 2 });
        panel.ViewModelProvider.Add(first);
        panel.ViewModelProvider.Add(second);

        source.Select(new object[] { second });

        Assert.False(first.IsSelected);
    }

    [Fact]
    public void should_remove_adopted_draft_when_released()
    {
        var panel = new FakePanel();
        var source = new DeviceConsoleSource<LampDeviceViewModel>(panel);
        var draft = source.CreateDraft()!;
        source.AdoptDraft(draft);

        source.ReleaseDraft(draft);

        Assert.Empty(panel.ViewModelProvider);
    }

    [Fact]
    public void should_forward_only_matching_rows_when_selection_is_given()
    {
        var panel = new FakePanel();
        var source = new DeviceConsoleSource<LampDeviceViewModel>(panel);
        var lamp = new LampDeviceViewModel(new LampDeviceModel { Id = 1 });

        source.Select(new object[] { lamp, "not a row" });

        Assert.Equal(new[] { lamp }, panel.SelectedItems.ToArray());
    }

    [Fact]
    public void should_raise_busy_ended_once_when_saving_flag_falls()
    {
        var panel = new FakePanel();
        var source = new DeviceConsoleSource<LampDeviceViewModel>(panel);
        var ended = 0;
        source.BusyEnded += (_, _) => ended++;

        panel.IsSaving = true;
        Assert.True(source.IsBusy);
        panel.IsSaving = false;

        Assert.False(source.IsBusy);
        Assert.Equal(1, ended);
    }

    [Fact]
    public void should_stay_busy_until_both_flags_clear_when_save_triggers_reload()
    {
        var panel = new FakePanel();
        var source = new DeviceConsoleSource<LampDeviceViewModel>(panel);
        var ended = 0;
        source.BusyEnded += (_, _) => ended++;

        panel.IsSaving = true;
        panel.ReloadButtonEnable = false;
        panel.IsSaving = false;
        Assert.Equal(0, ended);

        panel.ReloadButtonEnable = true;
        Assert.Equal(1, ended);
    }

    [Fact]
    public void should_raise_busy_ended_when_panel_signals_update_action()
    {
        // 삭제는 바쁨 표지를 안 바꾼다 — 패널마다 따로 선언된 UpdateAction 을 람다로 물려야 끝남을 안다.
        var panel = new FakePanel();
        var source = new DeviceConsoleSource<LampDeviceViewModel>(panel, handler => panel.UpdateAction += handler);
        var ended = 0;
        source.BusyEnded += (_, _) => ended++;

        panel.RaiseUpdateAction();

        Assert.Equal(1, ended);
    }

    [Fact]
    public void should_raise_busy_ended_when_initial_load_finishes()
    {
        var panel = new FakePanel(loaded: false);
        var source = new DeviceConsoleSource<LampDeviceViewModel>(panel);
        var ended = 0;
        source.BusyEnded += (_, _) => ended++;
        Assert.True(source.IsBusy);

        panel.ReloadButtonEnable = true;

        Assert.Equal(1, ended);   // 이 신호가 없으면 콘솔의 [추가] 가 첫 로딩 뒤에도 꺼진 채 남는다
    }

    private sealed class FakePanel : BaseDataGridMultiPanelViewModel<LampDeviceViewModel>
    {
        // 실제 패널은 목록을 다 읽으면 [갱신] 을 켠다 — 가짜는 "이미 읽었다"에서 시작한다.
        public FakePanel(bool loaded = true) : base(new EventAggregator(), null!) { ReloadButtonEnable = loaded; }

        public bool RefuseInsert { get; set; }
        public bool TurnsBusyOnSave { get; set; }
        public int InsertCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public int ReloadCalls { get; private set; }

        public event System.Action? UpdateAction;
        public void RaiseUpdateAction() => UpdateAction?.Invoke();

        public override void OnClickInsertButton(object sender, RoutedEventArgs e)
        {
            InsertCalls++;
            if (RefuseInsert) return;
            ViewModelProvider.Add(new LampDeviceViewModel(new LampDeviceModel { DeviceNumber = 1, DeviceName = "새 경고등 1" }));
        }

        public override void OnSelectionChanged(System.Collections.Generic.IList<LampDeviceViewModel> selectedItems) => SelectedItems = selectedItems;

        public override void OnClickDeleteButton(object sender, RoutedEventArgs e) => DeleteCalls++;
        public override void OnClickSaveButton(object sender, RoutedEventArgs e)
        {
            SaveCalls++;
            if (!TurnsBusyOnSave) return;
            IsSaving = true;      // 진짜 패널은 첫 await 전에 켜고, 끝나면 끈다 — 동기로 끝나도 "켜졌던 적"은 남아야 한다
            IsSaving = false;
        }
        public override void OnClickReloadButton(object sender, RoutedEventArgs e) => ReloadCalls++;
    }
}
