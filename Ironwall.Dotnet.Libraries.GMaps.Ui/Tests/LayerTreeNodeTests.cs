using Ironwall.Dotnet.Libraries.GMaps.Ui.Models;
using Xunit;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Tests;

public class LayerTreeNodeTests
{
    #region Test 1.1: 부모 OFF → 자식 전부 OFF

    [Fact(DisplayName = "ParentCheck — 부모 OFF → 자식 6개 전부 OFF")]
    public void ParentCheck_False_AllChildrenFalse()
    {
        // Arrange: PIDS 장비 그룹 + 자식 6개
        var group = LayerTreeNode.CreateGroup("PIDS 장비", "CctpIp", isExpanded: true);
        var children = new[]
        {
            CreateLeaf("카메라", "PidsCamera"),
            CreateLeaf("센서", "PidsSensor"),
            CreateLeaf("스피커", "PidsSpeaker"),
            CreateLeaf("컨트롤러", "PidsController"),
            CreateLeaf("조명", "PidsLamp"),
            CreateLeaf("함체", "PidsEnclosure"),
        };
        foreach (var c in children) group.AddChild(c);

        // 초기 상태: 전부 ON
        Assert.True(group.IsChecked);
        Assert.All(children, c => Assert.True(c.IsChecked));

        // Act: 부모 OFF
        group.IsChecked = false;

        // Assert: 자식 전부 OFF
        Assert.False(group.IsChecked);
        Assert.All(children, c => Assert.False(c.IsChecked));
    }

    [Fact(DisplayName = "ParentCheck — 부모 ON → 자식 6개 전부 ON")]
    public void ParentCheck_True_AllChildrenTrue()
    {
        var group = LayerTreeNode.CreateGroup("PIDS 장비");
        var children = new[]
        {
            CreateLeaf("카메라", "PidsCamera"),
            CreateLeaf("센서", "PidsSensor"),
        };
        foreach (var c in children) group.AddChild(c);

        // 부모 OFF 먼저
        group.IsChecked = false;
        Assert.All(children, c => Assert.False(c.IsChecked));

        // Act: 부모 ON
        group.IsChecked = true;

        // Assert
        Assert.True(group.IsChecked);
        Assert.All(children, c => Assert.True(c.IsChecked));
    }

    #endregion

    #region Test 1.2: 자식 일부 OFF → 부모 indeterminate

    [Fact(DisplayName = "ChildMixed — 자식 일부 OFF → 부모 null (indeterminate)")]
    public void ChildMixed_ParentBecomesIndeterminate()
    {
        var group = LayerTreeNode.CreateGroup("PIDS 장비");
        var camera = CreateLeaf("카메라", "PidsCamera");
        var sensor = CreateLeaf("센서", "PidsSensor");
        var speaker = CreateLeaf("스피커", "PidsSpeaker");
        group.AddChild(camera);
        group.AddChild(sensor);
        group.AddChild(speaker);

        // 초기: 전부 ON → 부모 true
        Assert.True(group.IsChecked);

        // Act: 카메라만 OFF
        camera.IsChecked = false;

        // Assert: 부모 null (indeterminate)
        Assert.Null(group.IsChecked);
    }

    [Fact(DisplayName = "ChildAllOff — 자식 전부 OFF → 부모 false")]
    public void ChildAllOff_ParentBecomesFalse()
    {
        var group = LayerTreeNode.CreateGroup("PIDS 장비");
        var camera = CreateLeaf("카메라", "PidsCamera");
        var sensor = CreateLeaf("센서", "PidsSensor");
        group.AddChild(camera);
        group.AddChild(sensor);

        // Act: 자식 전부 OFF
        camera.IsChecked = false;
        sensor.IsChecked = false;

        // Assert: 부모 false
        Assert.False(group.IsChecked);
    }

    [Fact(DisplayName = "ChildAllOn — 자식 전부 다시 ON → 부모 true 복귀")]
    public void ChildAllOn_ParentReturnsTrue()
    {
        var group = LayerTreeNode.CreateGroup("PIDS 장비");
        var camera = CreateLeaf("카메라", "PidsCamera");
        var sensor = CreateLeaf("센서", "PidsSensor");
        group.AddChild(camera);
        group.AddChild(sensor);

        // 자식 OFF → 부모 false
        camera.IsChecked = false;
        sensor.IsChecked = false;
        Assert.False(group.IsChecked);

        // Act: 자식 전부 ON
        camera.IsChecked = true;
        sensor.IsChecked = true;

        // Assert: 부모 true
        Assert.True(group.IsChecked);
    }

    #endregion

    #region Helpers

    private static LayerTreeNode CreateLeaf(string name, string category)
    {
        return new LayerTreeNode
        {
            Name = name,
            Category = category,
            NodeType = LayerNodeType.Leaf,
            IsChecked = true,
        };
    }

    #endregion
}
