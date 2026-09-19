using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;
using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;

// 이 어셈블리의 lookless 컨트롤이 Themes/Generic.xaml 에서 기본 스타일을 찾게 한다.
// (Utils/Console/ConsoleShell.cs 선례 — 별도 AssemblyInfo.cs 를 만들지 않고 컨트롤 파일 머리에 둔다.)
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Blocks;

/// <summary>
/// 부품 블록 하나를 그리는 lookless 컨트롤(FR-04). <b>표현만</b> 한다 — 카탈로그도 보드도 모른다.
/// </summary>
/// <remarks>
/// <para><b>기본 스타일</b>은 이 어셈블리가 스스로 싣는다(<c>ThemeInfo</c> + <c>Themes/Generic.xaml</c> →
/// <c>Consoles/Assembly/Blocks/ComponentBlock.xaml</c>). 즉 <c>&lt;blocks:ComponentBlock/&gt;</c> 를 아무 뷰에 떨궈도
/// 호스트 앱이 사전을 병합하지 않아도 제대로 그려진다.</para>
/// <para><b>색이 아니라 형태</b>다 — 가족은 <see cref="ComponentBlockGeometry"/> 의 바깥선으로,
/// 선택은 왼쪽 3px 세로 바(집안 어휘), 비가동은 점선 <c>{2,2}</c>(드롭 파선 <c>{6,4}</c> · 그룹 선택 <c>{4,3}</c> ·
/// 러버밴드 <c>{5,3}</c> 와 겹치지 않는다), 키 중복은 ⚠ 글리프 <b>와</b> 윤곽색이 같이 바뀐다(색 단독 금지).</para>
/// <para>브러시는 전부 <c>DynamicResource</c> 토큰이다 — 얼리거나 캐싱하지 않는다(테마 전환 고착 선례).
/// 애니메이션 · 스토리보드는 두지 않는다(RDP Tier 0).</para>
/// <para><b>자동화</b>: <see cref="OnCreateAutomationPeer"/> 로 peer 를 실재시킨다 —
/// <c>Border</c>/<c>Path</c> 는 peer 가 없어 UIA 트리에 안 나온다. 이름은 <see cref="Title"/> 이고
/// 계측 식별자는 호스트가 <c>AutomationProperties.AutomationId</c> 로 붙인다(<c>x:Name</c> 은 CM 바인딩 지시자라 쓰지 않는다).</para>
/// </remarks>
public class ComponentBlock : Control
{
    #region - Ctors -
    static ComponentBlock()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ComponentBlock), new FrameworkPropertyMetadata(typeof(ComponentBlock)));
    }
    #endregion

    #region - Dependency properties -
    /// <summary>부품 가족 — 블록의 <b>형태</b>를 정한다.</summary>
    public static readonly DependencyProperty FamilyProperty = DependencyProperty.Register(
        nameof(Family), typeof(ComponentFamily), typeof(ComponentBlock),
        new FrameworkPropertyMetadata(ComponentFamily.Other, FrameworkPropertyMetadataOptions.AffectsRender, OnShapeChanged));
    public ComponentFamily Family { get => (ComponentFamily)GetValue(FamilyProperty); set => SetValue(FamilyProperty, value); }

    /// <summary>표시 이름(예: "문 센서"). UIA 이름이기도 하다.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ComponentBlock),
        new FrameworkPropertyMetadata(null, propertyChangedCallback: null, coerceValueCallback: CoerceText));
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>둘째 줄 — 부품 <c>key</c> 나 유형 코드(고정폭으로 그린다).</summary>
    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(ComponentBlock),
        new FrameworkPropertyMetadata(null, propertyChangedCallback: null, coerceValueCallback: CoerceText));
    public string? Subtitle { get => (string?)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <summary>채널 꼬리표("ch 3"). 비면 숨는다.</summary>
    public static readonly DependencyProperty ChannelTextProperty = DependencyProperty.Register(
        nameof(ChannelText), typeof(string), typeof(ComponentBlock),
        new FrameworkPropertyMetadata(null, propertyChangedCallback: null, coerceValueCallback: CoerceText));
    public string? ChannelText { get => (string?)GetValue(ChannelTextProperty); set => SetValue(ChannelTextProperty, value); }

    /// <summary>동작 상태를 보고하는 유형인가. <c>false</c> 면 ⊘ 표지를 단다("건강만 보고").</summary>
    public static readonly DependencyProperty ReportsStateProperty = DependencyProperty.Register(
        nameof(ReportsState), typeof(bool), typeof(ComponentBlock), new FrameworkPropertyMetadata(true));
    public bool ReportsState { get => (bool)GetValue(ReportsStateProperty); set => SetValue(ReportsStateProperty, value); }

    /// <summary><c>in_service=false</c> — 윤곽을 점선 <c>{2,2}</c> 로.</summary>
    public static readonly DependencyProperty IsOutOfServiceProperty = DependencyProperty.Register(
        nameof(IsOutOfService), typeof(bool), typeof(ComponentBlock), new FrameworkPropertyMetadata(false));
    public bool IsOutOfService { get => (bool)GetValue(IsOutOfServiceProperty); set => SetValue(IsOutOfServiceProperty, value); }

    /// <summary>키 중복 등 — ⚠ 글리프 <b>와</b> 윤곽색을 같이 바꾼다(색 단독으로 뜻을 싣지 않는다).</summary>
    public static readonly DependencyProperty HasErrorProperty = DependencyProperty.Register(
        nameof(HasError), typeof(bool), typeof(ComponentBlock), new FrameworkPropertyMetadata(false));
    public bool HasError { get => (bool)GetValue(HasErrorProperty); set => SetValue(HasErrorProperty, value); }

    /// <summary>⚠ 의 말풍선 글.</summary>
    public static readonly DependencyProperty ErrorTextProperty = DependencyProperty.Register(
        nameof(ErrorText), typeof(string), typeof(ComponentBlock),
        new FrameworkPropertyMetadata(null, propertyChangedCallback: null, coerceValueCallback: CoerceText));
    public string? ErrorText { get => (string?)GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <summary>선택 — 왼쪽 3px 세로 바(집안 어휘). <b>바탕색을 바꾸지 않는다</b>.</summary>
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(ComponentBlock), new FrameworkPropertyMetadata(false));
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>팔레트 칩(한 줄) 인가 보드 자리(두 줄) 인가.</summary>
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(
        nameof(IsCompact), typeof(bool), typeof(ComponentBlock), new FrameworkPropertyMetadata(false));
    public bool IsCompact { get => (bool)GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }
    #endregion

    #region - Read-only shape properties -
    private static readonly DependencyPropertyKey OutlineGeometryPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(OutlineGeometry), typeof(Geometry), typeof(ComponentBlock), new PropertyMetadata(null));

    /// <summary>현재 가족 · 현재 크기의 바깥선. 템플릿이 <c>Path.Data</c> 와 선택 바의 <c>Clip</c> 에 쓴다.</summary>
    public static readonly DependencyProperty OutlineGeometryProperty = OutlineGeometryPropertyKey.DependencyProperty;
    public Geometry? OutlineGeometry => (Geometry?)GetValue(OutlineGeometryProperty);

    private static readonly DependencyPropertyKey OpticsMarkGeometryPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(OpticsMarkGeometry), typeof(Geometry), typeof(ComponentBlock), new PropertyMetadata(null));

    /// <summary>광학 가족의 원 표지. 다른 가족이면 <c>null</c>(그려지지 않는다).</summary>
    public static readonly DependencyProperty OpticsMarkGeometryProperty = OpticsMarkGeometryPropertyKey.DependencyProperty;
    public Geometry? OpticsMarkGeometry => (Geometry?)GetValue(OpticsMarkGeometryProperty);
    #endregion

    #region - Processes -
    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        UpdateShape();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateShape();
    }

    private static void OnShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => (d as ComponentBlock)?.UpdateShape();

    /// <summary>바깥선을 다시 만든다. <b>크기나 가족이 바뀔 때만</b> 부른다(마우스 이동마다 무효화하지 않는다).</summary>
    private void UpdateShape()
    {
        var w = ActualWidth;
        var h = ActualHeight;

        SetValue(OutlineGeometryPropertyKey, ComponentBlockGeometry.Outline(Family, w, h));

        var mark = ComponentBlockGeometry.OpticsMark(Family, w, h);
        if (mark is null)
        {
            SetValue(OpticsMarkGeometryPropertyKey, null);
            return;
        }

        var circle = new EllipseGeometry(mark.Value.Center, mark.Value.Radius, mark.Value.Radius);
        circle.Freeze();
        SetValue(OpticsMarkGeometryPropertyKey, circle);
    }

    /// <summary>빈 글(공백만인 것 포함)은 <c>null</c> 로 모은다 — 템플릿이 <c>{x:Null}</c> 트리거 하나로 접을 수 있게.</summary>
    private static object? CoerceText(DependencyObject d, object? value)
    {
        var text = value as string;
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
    #endregion

    #region - Automation -
    /// <summary>UIA 에 실재하게 한다 — peer 가 없으면 트리에 아예 나오지 않는다.</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new ComponentBlockAutomationPeer(this);

    /// <summary>블록의 UIA peer. 이름은 <see cref="Title"/>, 종류는 목록 항목이다.</summary>
    public sealed class ComponentBlockAutomationPeer : FrameworkElementAutomationPeer
    {
        public ComponentBlockAutomationPeer(ComponentBlock owner) : base(owner) { }

        protected override string GetClassNameCore() => nameof(ComponentBlock);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override bool IsControlElementCore() => true;

        protected override string GetNameCore()
        {
            // AutomationProperties.Name 이 붙어 있으면 그쪽이 이긴다(base 가 그렇게 처리한다).
            var declared = base.GetNameCore();
            if (!string.IsNullOrWhiteSpace(declared)) return declared;
            return (Owner as ComponentBlock)?.Title ?? string.Empty;
        }

        protected override string GetHelpTextCore()
        {
            if (Owner is not ComponentBlock block) return base.GetHelpTextCore();
            if (block.HasError && !string.IsNullOrWhiteSpace(block.ErrorText)) return block.ErrorText!;
            return base.GetHelpTextCore();
        }
    }
    #endregion
}
