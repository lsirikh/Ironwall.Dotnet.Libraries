using Ironwall.Dotnet.Libraries.Base.Models;
using Ironwall.Dotnet.Libraries.Events.Ui.Consoles.Lists;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Tests;

/// <summary>목록 다시 읽기 뒤 고른 행의 짝 찾기(헤디드 SC-EVT-028) — 같은 형식 · 같은 서버 id 여야 짝이다.</summary>
/// <remarks>행 뷰모델은 생성자에서 IoC 를 보므로, 같은 모양(Model.Id)의 가짜 행 두 종류로 판정만 본다.</remarks>
public class EventSelectionTwinTests
{
    private sealed class FakeModel : IBaseModel { public int Id { get; set; } }
    private sealed class DetectionRow { public DetectionRow(int id) => Model = new FakeModel { Id = id }; public IBaseModel Model { get; } }
    private sealed class MalfunctionRow { public MalfunctionRow(int id) => Model = new FakeModel { Id = id }; public IBaseModel Model { get; } }

    [Fact]
    public void should_pair_rows_when_a_reload_brings_the_same_event_as_a_new_instance()
        => Assert.True(EventSelectionTwin.IsSameEvent(new MalfunctionRow(7), new MalfunctionRow(7)));

    [Fact]
    public void should_not_pair_rows_when_a_detection_and_a_malfunction_share_an_id()
        => Assert.False(EventSelectionTwin.IsSameEvent(new DetectionRow(7), new MalfunctionRow(7)));

    [Fact]
    public void should_not_pair_rows_when_the_ids_differ()
        => Assert.False(EventSelectionTwin.IsSameEvent(new MalfunctionRow(7), new MalfunctionRow(8)));

    [Fact]
    public void should_not_pair_rows_when_the_event_is_not_saved_yet()
        => Assert.False(EventSelectionTwin.IsSameEvent(new MalfunctionRow(0), new MalfunctionRow(0)));
}
