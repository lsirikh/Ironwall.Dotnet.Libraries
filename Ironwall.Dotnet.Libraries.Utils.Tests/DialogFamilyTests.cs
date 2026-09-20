using Ironwall.Dotnet.Libraries.Utils.Consoles.Dialogs;
using System.Windows.Input;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Utils.Tests;

/// <summary>
/// 다이얼로그 가족 T4 (all-windows-console-redesign N-05) — 폭 규격 · 키 · 첫 포커스 · 식별자.
/// 정본: docs/design/window-layout-system-storyboard.html #h-dlg L1421-1428 · T4 정의 L1502.
/// 전부 순수 함수라 창 없이 돈다.
/// </summary>
public class DialogSizeRulesTests
{
    [Theory]
    [InlineData(DialogSize.Small, 400d)]
    [InlineData(DialogSize.Medium, 560d)]
    [InlineData(DialogSize.Large, 720d)]
    public void should_use_the_three_nominal_widths_when_owner_is_unknown(DialogSize size, double expected)
    {
        Assert.Equal(expected, DialogSizeRules.ResolveWidth(size, 0d));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(0d)]
    [InlineData(-10d)]
    public void should_fall_back_to_nominal_when_owner_width_is_unusable(double ownerWidth)
    {
        Assert.Equal(720d, DialogSizeRules.ResolveWidth(DialogSize.Large, ownerWidth));
    }

    [Fact]
    public void should_keep_nominal_width_when_owner_is_wide_enough()
    {
        // 720 + 여백 48 = 768 — 딱 맞는 경계에서도 규격을 지킨다.
        Assert.Equal(720d, DialogSizeRules.ResolveWidth(DialogSize.Large, 768d));
    }

    [Fact]
    public void should_shrink_below_nominal_when_owner_is_narrow()
    {
        // 창 600 → 쓸 수 있는 폭 552 → 규격 720 을 다 쓸 수 없다.
        Assert.Equal(552d, DialogSizeRules.ResolveWidth(DialogSize.Large, 600d));
    }

    [Fact]
    public void should_not_go_under_min_width_when_owner_is_small_but_wider_than_min()
    {
        // 창 360 → 쓸 수 있는 폭 312 지만 최소 320 을 지킨다(버튼 줄이 겹치지 않게).
        Assert.Equal(DialogSizeRules.MinWidth, DialogSizeRules.ResolveWidth(DialogSize.Small, 360d));
    }

    [Fact]
    public void should_never_be_wider_than_the_owner_when_owner_is_tiny()
    {
        Assert.Equal(200d, DialogSizeRules.ResolveWidth(DialogSize.Small, 200d));
    }

    [Theory]
    [InlineData(DialogSize.Small, 480d)]
    [InlineData(DialogSize.Medium, 680d)]
    [InlineData(DialogSize.Large, 760d)]
    public void should_cap_height_at_the_nominal_maximum_when_owner_is_tall(DialogSize size, double expected)
    {
        Assert.Equal(expected, DialogSizeRules.ResolveMaxHeight(size, 2000d));
    }

    [Fact]
    public void should_shrink_max_height_when_owner_is_short()
    {
        Assert.Equal(452d, DialogSizeRules.ResolveMaxHeight(DialogSize.Large, 500d));
    }

    [Fact]
    public void should_never_be_taller_than_the_owner_when_owner_is_tiny()
    {
        Assert.Equal(120d, DialogSizeRules.ResolveMaxHeight(DialogSize.Medium, 120d));
    }

    [Fact]
    public void should_return_both_axes_when_resolving()
    {
        var metrics = DialogSizeRules.Resolve(DialogSize.Medium, 1920d, 1080d);
        Assert.Equal(560d, metrics.Width);
        Assert.Equal(680d, metrics.MaxHeight);
    }

    [Fact]
    public void should_add_both_gutters_when_sizing_a_window_for_a_dialog()
    {
        Assert.Equal(720d + (2 * DialogSizeRules.Gutter), DialogSizeRules.WindowWidth(DialogSize.Large));
    }
}

public class DialogKeyRulesTests
{
    [Fact]
    public void should_take_primary_when_enter_and_primary_is_enabled()
    {
        Assert.Equal(DialogKeyAction.Primary,
            DialogKeyRules.Decide(Key.Enter, isPrimaryEnabled: true, focusIsMultiLineText: false, isBusy: false));
    }

    [Fact]
    public void should_ignore_enter_when_primary_is_disabled()
    {
        Assert.Equal(DialogKeyAction.None,
            DialogKeyRules.Decide(Key.Enter, isPrimaryEnabled: false, focusIsMultiLineText: false, isBusy: false));
    }

    [Fact]
    public void should_ignore_enter_when_focus_is_in_a_multi_line_box()
    {
        // 메모 칸에서 줄을 바꾸려던 Enter 가 저장으로 새면 안 된다.
        Assert.Equal(DialogKeyAction.None,
            DialogKeyRules.Decide(Key.Enter, isPrimaryEnabled: true, focusIsMultiLineText: true, isBusy: false));
    }

    [Fact]
    public void should_ignore_enter_when_busy()
    {
        Assert.Equal(DialogKeyAction.None,
            DialogKeyRules.Decide(Key.Enter, isPrimaryEnabled: true, focusIsMultiLineText: false, isBusy: true));
    }

    [Fact]
    public void should_cancel_when_escape_even_while_busy()
    {
        // 탈출구 없는 창을 다시 만들지 않는다(스토리보드 L1450).
        Assert.Equal(DialogKeyAction.Cancel,
            DialogKeyRules.Decide(Key.Escape, isPrimaryEnabled: false, focusIsMultiLineText: true, isBusy: true));
    }

    [Theory]
    [InlineData(Key.Space)]
    [InlineData(Key.Tab)]
    [InlineData(Key.A)]
    [InlineData(Key.Delete)]
    public void should_pass_other_keys_through_when_deciding(Key key)
    {
        Assert.Equal(DialogKeyAction.None,
            DialogKeyRules.Decide(key, isPrimaryEnabled: true, focusIsMultiLineText: false, isBusy: false));
    }
}

public class DialogFocusRulesTests
{
    [Fact]
    public void should_focus_cancel_first_when_a_secondary_button_exists()
    {
        Assert.Equal(DialogFocusTarget.Secondary,
            DialogFocusRules.InitialTarget(hasSecondary: true, hasPrimary: true, hasClose: true));
    }

    [Fact]
    public void should_focus_close_when_there_is_no_secondary_button()
    {
        Assert.Equal(DialogFocusTarget.Close,
            DialogFocusRules.InitialTarget(hasSecondary: false, hasPrimary: true, hasClose: true));
    }

    [Fact]
    public void should_focus_primary_when_it_is_the_only_button()
    {
        Assert.Equal(DialogFocusTarget.Primary,
            DialogFocusRules.InitialTarget(hasSecondary: false, hasPrimary: true, hasClose: false));
    }

    [Fact]
    public void should_stay_on_the_root_when_there_is_no_button_at_all()
    {
        Assert.Equal(DialogFocusTarget.Root,
            DialogFocusRules.InitialTarget(hasSecondary: false, hasPrimary: false, hasClose: false));
    }
}

public class DialogIdsTests
{
    [Fact]
    public void should_build_the_six_parts_when_a_key_is_given()
    {
        Assert.Equal("Dialog.Devices.Assign.Root", DialogIds.Root("Devices.Assign"));
        Assert.Equal("Dialog.Devices.Assign.Title", DialogIds.Title("Devices.Assign"));
        Assert.Equal("Dialog.Devices.Assign.Close", DialogIds.Close("Devices.Assign"));
        Assert.Equal("Dialog.Devices.Assign.Primary", DialogIds.Primary("Devices.Assign"));
        Assert.Equal("Dialog.Devices.Assign.Secondary", DialogIds.Secondary("Devices.Assign"));
        Assert.Equal("Dialog.Devices.Assign.Message", DialogIds.Message("Devices.Assign"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void should_return_null_when_the_key_is_missing(string? key)
    {
        // 식별자가 없으면 붙이지 않는다 — "Dialog..Primary" 같은 가짜 전역 이름을 만들지 않는다.
        Assert.Null(DialogIds.Primary(key));
    }

    [Fact]
    public void should_trim_the_key_when_it_has_stray_spaces()
    {
        Assert.Equal("Dialog.Devices.Assign.Primary", DialogIds.Primary("  Devices.Assign  "));
    }

    [Fact]
    public void should_return_null_when_the_part_is_missing()
    {
        Assert.Null(DialogIds.For("Devices.Assign", " "));
    }
}
