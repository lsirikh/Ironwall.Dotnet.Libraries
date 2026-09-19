using Ironwall.Dotnet.Monitoring.Models.Devices;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Assembly.Model;

/// <summary>
/// 보드 위의 부품 한 개 = 그 장비만의 사실 + 그 부품의 재정의(<c>component_overrides.&lt;key&gt;</c>).
/// </summary>
/// <remarks>
/// Caliburn 의 <c>PropertyChangedBase</c> 가 아니라 <see cref="INotifyPropertyChanged"/> 를 직접 단다 —
/// 순수 모델로 남아야 헤드리스 테스트가 IoC 없이 돌고, 그러면서도 화면이 그대로 바인딩할 수 있다.
/// 유형 공통 사실(<c>states</c> · <c>commands</c> · <c>produces</c> …)은 여기 없다 — 요청에 실으면 422 다(AS L213).
/// </remarks>
public sealed class AssemblySlot : INotifyPropertyChanged
{
    private string _key;
    private string? _label;
    private int? _channel;
    private string? _position;
    private bool _inService = true;
    private string? _manufacturer;
    private string? _model;
    private string? _serial;
    private string? _firmware;
    private string? _hardwareRev;
    private JObject? _spec;
    private string? _installedAt;
    private string? _replacedAt;
    private JObject? _overrides;
    private string? _keyError;

    public AssemblySlot(string typeCode, string key)
    {
        TypeCode = (typeCode ?? string.Empty).Trim().ToUpperInvariant();
        _key = key ?? string.Empty;
    }

    /// <summary>카탈로그 유형 코드(대문자). <b>바뀌지 않는다</b> — 유형을 바꾸려면 빼고 다시 단다.</summary>
    public string TypeCode { get; }

    public string Key
    {
        get => _key;
        set => Set(ref _key, value ?? string.Empty);
    }

    public string? Label
    {
        get => _label;
        set => Set(ref _label, value);
    }

    public int? Channel
    {
        get => _channel;
        set => Set(ref _channel, value);
    }

    public string? Position
    {
        get => _position;
        set => Set(ref _position, value);
    }

    /// <summary>달려 있고 쓰는가. 기본 true — 달렸으나 안 쓰는 부품이 false.</summary>
    public bool InService
    {
        get => _inService;
        set => Set(ref _inService, value);
    }

    public string? Manufacturer
    {
        get => _manufacturer;
        set => Set(ref _manufacturer, value);
    }

    public string? Model
    {
        get => _model;
        set => Set(ref _model, value);
    }

    public string? Serial
    {
        get => _serial;
        set => Set(ref _serial, value);
    }

    public string? Firmware
    {
        get => _firmware;
        set => Set(ref _firmware, value);
    }

    public string? HardwareRev
    {
        get => _hardwareRev;
        set => Set(ref _hardwareRev, value);
    }

    /// <summary>손대지 않고 그대로 들고 나른다(왕복 보존). 안을 고쳤을 땐 객체를 새로 넣어야 알림이 뜬다.</summary>
    public JObject? Spec
    {
        get => _spec;
        set => Set(ref _spec, value);
    }

    /// <summary>그대로 들고 나른다 — 그 장비만의 사실이라 프리셋은 비운다.</summary>
    public string? InstalledAt
    {
        get => _installedAt;
        set => Set(ref _installedAt, value);
    }

    public string? ReplacedAt
    {
        get => _replacedAt;
        set => Set(ref _replacedAt, value);
    }

    /// <summary>
    /// <c>device_config.component_overrides.&lt;key&gt;</c>. <b>선언에 섞지 않는다</b> —
    /// <c>enabled</c> 같은 값은 여기서만 정상이고 선언에 실으면 422 다.
    /// </summary>
    public JObject? Overrides
    {
        get => _overrides;
        set => Set(ref _overrides, value);
    }

    /// <summary>보드가 채운다 — 형식 오류거나 같은 장비 안 중복. null = 정상.</summary>
    public string? KeyError
    {
        get => _keyError;
        internal set
        {
            if (Set(ref _keyError, value)) Notify(nameof(HasKeyError));
        }
    }

    public bool HasKeyError => _keyError is not null;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static AssemblySlot FromDefinition(ComponentDefinitionModel definition, JObject? overrides)
    {
        if (definition is null) throw new ArgumentNullException(nameof(definition));

        return new AssemblySlot(definition.Type, definition.Key)
        {
            _label = definition.Label,
            _channel = definition.Channel,
            _position = definition.Position,
            _inService = definition.InService ?? true,
            _manufacturer = definition.Manufacturer,
            _model = definition.Model,
            _serial = definition.Serial,
            _firmware = definition.Firmware,
            _hardwareRev = definition.HardwareRev,
            _installedAt = definition.InstalledAt,
            _replacedAt = definition.ReplacedAt,
            _spec = definition.Spec?.DeepClone() as JObject,
            _overrides = overrides?.DeepClone() as JObject,
        };
    }

    /// <summary>보낼 선언 한 줄. 부를 때마다 <b>새 객체</b>라 baseline 과 섞이지 않는다.</summary>
    public ComponentDefinitionModel ToDefinition() => new()
    {
        Key = Key,
        Type = TypeCode,
        Label = Label,
        InService = InService,
        Channel = Channel,
        Position = Position,
        Manufacturer = Manufacturer,
        Model = Model,
        Serial = Serial,
        Firmware = Firmware,
        HardwareRev = HardwareRev,
        InstalledAt = InstalledAt,
        ReplacedAt = ReplacedAt,
        Spec = Spec?.DeepClone() as JObject,
    };

    /// <summary>깊은 복사. <see cref="KeyError"/> 는 보드가 다시 채우므로 옮기지 않는다.</summary>
    public AssemblySlot Clone() => new(TypeCode, Key)
    {
        _label = _label,
        _channel = _channel,
        _position = _position,
        _inService = _inService,
        _manufacturer = _manufacturer,
        _model = _model,
        _serial = _serial,
        _firmware = _firmware,
        _hardwareRev = _hardwareRev,
        _installedAt = _installedAt,
        _replacedAt = _replacedAt,
        _spec = _spec?.DeepClone() as JObject,
        _overrides = _overrides?.DeepClone() as JObject,
    };

    public override string ToString() => $"{Key} ({TypeCode})";

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(name);
        return true;
    }

    private void Notify(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
