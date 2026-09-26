using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly;

/// <summary>팔레트의 블록 하나 — 카탈로그의 유형 한 줄.</summary>
public sealed class PaletteItemViewModel
{
    public PaletteItemViewModel(ComponentTypeInfo info) => Info = info ?? throw new ArgumentNullException(nameof(info));

    public ComponentTypeInfo Info { get; }
    public string Code => Info.Code;
    public string Label => Info.Label;
    public ComponentFamily Family => Info.Family;
    public bool ReportsState => Info.ReportsState;

    /// <summary>끌 때 고스트에 찍히는 글.</summary>
    public string Display => Info.Display;

    public override string ToString() => Display;
}

/// <summary>
/// 보드의 슬롯 하나 — 모델(<see cref="AssemblySlot"/>)에 카탈로그의 유형 한 줄을 붙여 화면이 그릴 수 있게 한다.
/// 슬롯의 값은 모델이 쥔다. 여기는 표시용 파생값과 "어느 것이 골라졌나"만 가진다.
/// </summary>
public sealed class BoardSlotViewModel : PropertyChangedBase
{
    private bool _isSelected;

    public BoardSlotViewModel(AssemblySlot slot, ComponentTypeInfo? info)
    {
        Slot = slot ?? throw new ArgumentNullException(nameof(slot));
        Info = info;
        Slot.PropertyChanged += OnSlotPropertyChanged;
    }

    public AssemblySlot Slot { get; }

    /// <summary>카탈로그의 유형. null = 카탈로그에서 사라진 유형(말없이 빼지 않고 표시한다).</summary>
    public ComponentTypeInfo? Info { get; }

    public ComponentFamily Family => Info?.Family ?? ComponentFamily.Other;
    public string Title => string.IsNullOrWhiteSpace(Slot.Label) ? Info?.Label ?? Slot.TypeCode : Slot.Label!;
    public string Subtitle => $"{Slot.Key} · {Slot.TypeCode}";
    public string? ChannelText => Slot.Channel is { } channel ? $"ch {channel.ToString(CultureInfo.InvariantCulture)}" : null;
    public bool ReportsState => Info?.ReportsState ?? true;
    public bool IsOutOfService => !Slot.InService;
    public bool IsUnknownType => Info is null;
    public bool HasError => Slot.HasKeyError || IsUnknownType;
    public string? ErrorText => Slot.KeyError ?? (IsUnknownType ? $"'{Slot.TypeCode}'은(는) 목록에 없는 부품 유형입니다." : null);

    /// <summary>
    /// 채널 입력 — 글로 받는다. 정수 칸에 곧바로 묶으면 "1a" 같은 글은 변환에 실패한 채 화면에만 남고
    /// 모델은 옛 값을 쥔다(사용자는 1a 를 보는데 나가는 것은 1). 못 읽는 글은 까닭을 보이고 값을 바꾸지 않는다.
    /// </summary>
    public string ChannelInput
    {
        get => _channelInput ?? Slot.Channel?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        set
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length == 0) { _channelInput = null; ChannelError = null; Slot.Channel = null; }
            else if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var channel)) { _channelInput = null; ChannelError = null; Slot.Channel = channel; }
            else { _channelInput = text; ChannelError = "채널은 0 이상의 정수로 입력하세요. 값은 바꾸지 않았습니다."; }
            Refresh();
        }
    }

    public string? ChannelError { get; private set; }
    private string? _channelInput;

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; NotifyOfPropertyChange(); }
    }

    /// <summary>끌 때 고스트에 찍히는 글.</summary>
    public string Display => Title;

    public void Detach() => Slot.PropertyChanged -= OnSlotPropertyChanged;

    private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    public override string ToString() => Display;
}

/// <summary>
/// 재정의 칸 한 줄 — <c>device_config.component_overrides.&lt;key&gt;.&lt;name&gt;</c>.
/// 부품 칸(형상)과 <b>갈라 놓는다</b>: <c>enabled</c> 는 부품에 실으면 422 이고 여기서만 정상이다(AS L342).
/// </summary>
public sealed class OverrideRowViewModel : PropertyChangedBase
{
    private readonly AssemblySlot _slot;
    private string _text;

    public OverrideRowViewModel(AssemblySlot slot, string name)
    {
        _slot = slot;
        Name = name;
        _text = Format(slot.Overrides?[name]);
    }

    public string Name { get; }

    /// <summary>빈 글 = 재정의하지 않음(키를 뺀다). <c>true/false</c> · 숫자 · 그 밖은 문자열로 담는다.</summary>
    public string Text
    {
        get => _text;
        set
        {
            var next = value ?? string.Empty;
            if (_text == next) return;
            _text = next;
            Write(next.Trim());
            NotifyOfPropertyChange();
        }
    }

    private void Write(string text)
    {
        var overrides = _slot.Overrides is null ? new JObject() : (JObject)_slot.Overrides.DeepClone();

        if (text.Length == 0) overrides.Remove(Name);
        else overrides[Name] = Parse(text);

        // 새 객체로 바꿔 끼운다 — 슬롯이 "바뀌었다"를 알리고 보드가 미저장 변경을 다시 센다.
        _slot.Overrides = overrides.HasValues ? overrides : null;
    }

    public static JToken Parse(string text)
    {
        if (bool.TryParse(text, out var flag)) return new JValue(flag);
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole)) return new JValue(whole);
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real)) return new JValue(real);
        return new JValue(text);
    }

    private static string Format(JToken? token) => token switch
    {
        null => string.Empty,
        JValue { Type: JTokenType.Null } => string.Empty,
        JValue { Type: JTokenType.Boolean } value => ((bool)value) ? "true" : "false",
        JValue value => Convert.ToString(value.Value, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => token.ToString(Newtonsoft.Json.Formatting.None),
    };
}

/// <summary>조립기를 무엇으로 열었나 — 같은 창이 세 가지 일을 한다.</summary>
public enum AssemblyMode
{
    /// <summary>새로 조립 — [등록] 은 "프리셋으로 등록" 창으로 이어지고, [프리셋으로 저장] 을 쓸 수 있다.</summary>
    Compose,

    /// <summary>프리셋 고치기 — 카테고리 고정 · [프리셋 저장].</summary>
    EditPreset,

    /// <summary>기존 장비의 부품 구성 바꾸기 — 카테고리 고정 · [적용](다시 받기 → 비교 → PATCH 1건).</summary>
    EditDevice,
}

/// <summary>팔레트 검색 · 가족 순서 같은 작은 판정(순수).</summary>
public static class AssemblyPalette
{
    public static bool Matches(ComponentTypeInfo info, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        var needle = search.Trim();
        return info.Code.Contains(needle, StringComparison.OrdinalIgnoreCase) || info.Label.Contains(needle, StringComparison.CurrentCultureIgnoreCase);
    }

    public static string FamilyTitle(ComponentFamily family) => family switch
    {
        ComponentFamily.Sensing => "감지",
        ComponentFamily.Actuation => "구동",
        ComponentFamily.PowerEnvironment => "전원 · 환경",
        ComponentFamily.Network => "네트워크",
        ComponentFamily.Optics => "광학",
        _ => "기타",
    };

    /// <summary>카탈로그 사실을 회색 상자에 보일 한 줄로 — 없으면 "없음".</summary>
    public static string Join(IReadOnlyList<string> names) => names.Count == 0 ? "없음" : string.Join(" · ", names);
}
