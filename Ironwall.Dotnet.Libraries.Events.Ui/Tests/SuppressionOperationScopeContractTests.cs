using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Api.Services;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Suppression;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/****************************************************************************
   Purpose      : G44 — 억제 범위 '운영' 은 서버 계약 ≥V7_0 에서만(EventApiService.HasOperationEvents 와 같은 기준).
                  V6_3 · V8_0 두 계약으로 콤보 노출 · 저장 막기를 본다.
   Created By   : Claude
   Created On   : 2026-10-21
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
****************************************************************************/
public class SuppressionOperationScopeContractTests
{
    private readonly FakeClock _clock = new(new DateTime(2026, 10, 21, 10, 0, 0));
    private readonly List<SuppressionDraft> _saved = new();

    public SuppressionOperationScopeContractTests() => PlatformProvider.Current = new DefaultPlatformProvider();

    private SuppressionDrawerViewModel Drawer(EnumServerContract contract)
        => new(_clock, null, null, Save, () => true, contract: () => contract);

    private Task<SuppressionSaveOutcome> Save(SuppressionDraft draft, CancellationToken token)
    {
        _saved.Add(draft);
        return Task.FromResult(new SuppressionSaveOutcome(true, "저장했습니다.", new EventSuppressionScheduleDto { Id = 1 }));
    }

    [Theory]
    [InlineData(EnumServerContract.V6_3, false)]
    [InlineData(EnumServerContract.V7_0, true)]
    [InlineData(EnumServerContract.V8_0, true)]
    public void 운영_항목은_7_0_이상에서만_보인다(EnumServerContract contract, bool visible)
    {
        Assert.Equal(visible, Drawer(contract).IsOperationScopeAvailable);
        Assert.Equal(visible, SuppressionRequestBuilder.SupportsOperationScope(contract));
    }

    [Fact]
    public async Task 서버_6_3_에서_운영_범위_저장은_보내기_전에_막고_안내한다()
    {
        var drawer = Drawer(EnumServerContract.V6_3);
        drawer.OpenNew();
        drawer.Name = "정문 보수";
        drawer.AddSelected(new object[] { new SuppressionTargetChip(SuppressionTargetKind.Device, 3, "센서-3") });
        drawer.EventScope = "operation";

        await drawer.SaveAsync();

        Assert.Empty(_saved);
        Assert.Equal(SuppressionRequestBuilder.OperationScopeUnsupportedText, drawer.StatusLine);
    }

    [Fact]
    public async Task 서버_8_0_에서는_운영_범위를_그대로_보낸다()
    {
        var drawer = Drawer(EnumServerContract.V8_0);
        drawer.OpenNew();
        drawer.Name = "정문 보수";
        drawer.AddSelected(new object[] { new SuppressionTargetChip(SuppressionTargetKind.Device, 3, "센서-3") });
        drawer.EventScope = "operation";

        await drawer.SaveAsync();

        Assert.Single(_saved);
        Assert.Equal("operation", SuppressionRequestBuilder.BuildCreate(_saved[0]).EventScope);
    }

    [Theory]
    [InlineData("operation", EnumServerContract.V6_3, true)]
    [InlineData("operation", EnumServerContract.V8_0, false)]
    [InlineData("detection", EnumServerContract.V6_3, false)]
    [InlineData("all", EnumServerContract.V6_3, false)]
    public void 빌더_범위_검사(string scope, EnumServerContract contract, bool blocked)
        => Assert.Equal(blocked, SuppressionRequestBuilder.ScopeGuard(scope, contract) is not null);

    [Theory]
    [InlineData("Views/Consoles/SuppressionDrawerView.xaml")]
    [InlineData("Views/Panels/EventSuppressionSchedulePanelView.xaml")]
    public void 두_화면의_운영_항목은_계약_속성에_묶여_있다(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ironwall.Dotnet.Libraries.sln"))) dir = dir.Parent;
        var xaml = File.ReadAllText(Path.Combine(dir!.FullName, "Ironwall.Dotnet.Libraries.Events.Ui", relative));
        Assert.Contains("<ComboBoxItem Content=\"운영\" Tag=\"operation\" Visibility=\"{Binding IsOperationScopeAvailable", xaml);
    }
}
