using System.Reflection;
using Ironwall.Dotnet.Libraries.GMaps.Ui.Helpers;
using Xunit;

namespace GMaps.Ui.Tests;

/// <summary>
/// C12 — 드로잉/배치/조준 등 독점 입력 모드 중 라벨·편집핸들·그룹선택 어도너가 눌림을 가로채지 않도록
/// 세 어도너가 공유하는 투과 게이트(<see cref="ExclusiveInputModeGate"/>) 판정 회귀.
/// 목록 = GMapMarkerBaseControl D-19 가드(배치·조준·홈배치·앵커·라인드로잉) + 측정.
/// </summary>
public class ExclusiveInputModeGateTests
{
    private sealed class FakeSource : IExclusiveInputModeSource
    {
        public bool IsSymbolPlacementMode { get; set; }
        public bool IsTargetAimMode { get; set; }
        public bool IsHomePlacementMode { get; set; }
        public bool IsAnchorDrawMode { get; set; }
        public bool IsMeasuring { get; set; }
        public bool IsLineDrawing { get; set; }
    }

    [Fact]
    public void should_not_pass_through_when_no_exclusive_mode_active()
    {
        // Arrange — 편집모드 단순 선택 상태(모든 독점 모드 off)
        var map = new FakeSource();

        // Act
        bool active = ExclusiveInputModeGate.IsActive(map);

        // Assert — 어도너는 종전대로 라벨/핸들/그룹 드래그를 받는다
        Assert.False(active);
    }

    [Fact]
    public void should_not_pass_through_when_source_is_null()
    {
        // Arrange — MarkerEditAdorner 는 GMapControl(벤더 베이스)만 갖는 경로가 있어 null 허용
        IExclusiveInputModeSource? map = null;

        // Act
        bool active = ExclusiveInputModeGate.IsActive(map);

        // Assert
        Assert.False(active);
    }

    [Fact]
    public void should_pass_through_when_line_drawing_even_with_selection_alive()
    {
        // Arrange — C12 재현 조건: 직전 선택(편집핸들/그룹박스/라벨)이 살아있는 채 라인 드로잉만 시작
        var map = new FakeSource { IsLineDrawing = true };

        // Act
        bool active = ExclusiveInputModeGate.IsActive(map);

        // Assert — 눌림은 어도너가 아니라 맵의 IsLineDrawing 분기(_linePress/CaptureMouse)가 받아야 한다
        Assert.True(active);
    }

    [Theory]
    [InlineData(nameof(IExclusiveInputModeSource.IsSymbolPlacementMode))]
    [InlineData(nameof(IExclusiveInputModeSource.IsTargetAimMode))]
    [InlineData(nameof(IExclusiveInputModeSource.IsHomePlacementMode))]
    [InlineData(nameof(IExclusiveInputModeSource.IsAnchorDrawMode))]
    [InlineData(nameof(IExclusiveInputModeSource.IsMeasuring))]
    [InlineData(nameof(IExclusiveInputModeSource.IsLineDrawing))]
    public void should_pass_through_when_single_exclusive_mode_active(string mode)
    {
        // Arrange — D-19 목록(+측정) 각 모드 단독 활성
        var map = new FakeSource();
        typeof(FakeSource).GetProperty(mode)!.SetValue(map, true);

        // Act
        bool active = ExclusiveInputModeGate.IsActive(map);

        // Assert
        Assert.True(active);
    }

    [Fact]
    public void should_pass_through_when_multiple_exclusive_modes_active()
    {
        // Arrange
        var map = new FakeSource { IsLineDrawing = true, IsSymbolPlacementMode = true, IsMeasuring = true };

        // Act
        bool active = ExclusiveInputModeGate.IsActive(map);

        // Assert
        Assert.True(active);
    }

    [Fact]
    public void should_cover_every_flag_when_source_interface_grows()
    {
        // Arrange — 인터페이스에 플래그를 추가하고 게이트 갱신을 잊으면 여기서 잡힌다(비대칭 재발 방지)
        var flags = typeof(IExclusiveInputModeSource)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(bool))
            .ToArray();
        Assert.Equal(6, flags.Length);

        foreach (var flag in flags)
        {
            var map = new FakeSource();
            typeof(FakeSource).GetProperty(flag.Name)!.SetValue(map, true);

            // Act
            bool active = ExclusiveInputModeGate.IsActive(map);

            // Assert
            Assert.True(active, $"{flag.Name} 단독 활성이 게이트에 반영되지 않음");
        }
    }
}
