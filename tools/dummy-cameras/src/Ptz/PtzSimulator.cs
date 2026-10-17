namespace DummyCameras.Ptz;

/// <summary>Pan · tilt in ONVIF generic space [-1, 1], zoom in [0, 1].</summary>
public readonly record struct PtzVector(double Pan, double Tilt, double Zoom)
{
    public static readonly PtzVector Zero = new(0, 0, 0);

    public PtzVector Clamp() => new(Math.Clamp(Pan, -1, 1), Math.Clamp(Tilt, -1, 1), Math.Clamp(Zoom, 0, 1));

    public override string ToString() => $"P{Pan:+0.00;-0.00} T{Tilt:+0.00;-0.00} Z{Zoom:0.00}";
}

public sealed record PtzPreset(string Token, string Name, PtzVector Position);

/// <summary>
/// Shape of the ONVIF preset tokens a camera hands out - real models differ, and the GIS can only store a token as
/// the server <c>preset_index</c> when it is a plain positive integer.
/// </summary>
public enum PresetTokenStyle
{
    /// <summary>"1".."5" (default).</summary>
    Numeric,

    /// <summary>"Preset001".."Preset005" (name-shaped tokens).</summary>
    Named,

    /// <summary>"001".."005" (leading zeros - digits, but not the canonical number).</summary>
    Padded,

    /// <summary>"1", "2", "Preset003", "004", "5" - storable and non-storable tokens on one camera.</summary>
    Mixed,
}

public static class PresetTokens
{
    /// <summary>Token of preset number <paramref name="n"/> (1-based) in <paramref name="style"/>.</summary>
    public static string Format(PresetTokenStyle style, int n)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return style switch
        {
            PresetTokenStyle.Named => "Preset" + n.ToString("000", inv),
            PresetTokenStyle.Padded => n.ToString("000", inv),
            PresetTokenStyle.Mixed => (n % 5) switch
            {
                3 => "Preset" + n.ToString("000", inv),
                4 => n.ToString("000", inv),
                _ => n.ToString(inv),
            },
            _ => n.ToString(inv),
        };
    }

    /// <summary>"numeric" · "named" · "padded" · "mixed" (case-insensitive).</summary>
    public static PresetTokenStyle Parse(string text)
        => Enum.TryParse<PresetTokenStyle>(text?.Trim(), ignoreCase: true, out var s) && Enum.IsDefined(s)
            ? s
            : throw new FormatException($"preset token style must be numeric, named, padded or mixed: '{text}'");

    public static string Name(PresetTokenStyle style) => style.ToString().ToLowerInvariant();
}

/// <summary>Point-in-time PTZ state (GetStatus · overlay · control endpoint · JSONL).</summary>
public sealed record PtzSnapshot(
    PtzVector Position,
    bool PanTiltMoving,
    bool ZoomMoving,
    string Motion,            // "idle" | "continuous" | "goto"
    string? TargetPreset,     // preset token while travelling to a preset / home ("home")
    DateTime? ArrivesAtUtc,
    PtzVector Velocity)
{
    public bool IsMoving => PanTiltMoving || ZoomMoving;
}

/// <summary>
/// Simulated PTZ head (pure: every call takes "now" - no timers, no clock reads - so it is deterministic under test).
/// <list type="bullet">
/// <item>ContinuousMove integrates velocity × rate over time (pan/tilt <see cref="PanTiltRatePerSec"/>, zoom
/// <see cref="ZoomRatePerSec"/> at full speed), clamped to the space, and stops by itself after the ONVIF Timeout
/// (our client sends PT2S - FR-22), or <see cref="DefaultTimeout"/> when none is given.</item>
/// <item>GotoPreset / GotoHomePosition / AbsoluteMove / RelativeMove travel linearly and arrive after exactly
/// <see cref="GotoTravel"/> (the "delay" the event window shows as "이동 중").</item>
/// <item>Stop freezes the requested axes at their current position and cancels a goto.</item>
/// </list>
/// Thread-safe (one lock; no callbacks under the lock).
/// </summary>
public sealed class PtzSimulator
{
    public const double PanTiltRatePerSec = 0.5;   // full sweep -1→+1 in 4 s at velocity 1
    public const double ZoomRatePerSec = 0.25;     // 0→1 in 4 s

    private readonly object _gate = new();
    private readonly List<PtzPreset> _presets;
    private PtzVector _home;

    private enum MotionKind { Idle, Continuous, Goto }

    private MotionKind _kind = MotionKind.Idle;
    private PtzVector _origin;          // position at _t0
    private DateTime _t0;
    private PtzVector _velocity;        // continuous: normalized [-1,1] per axis
    private DateTime _tEnd;             // continuous: auto-stop time · goto: arrival time
    private PtzVector _target;          // goto target
    private string? _targetPreset;
    private int _presetSeq;
    private PresetTokenStyle _tokenStyle;

    public PtzSimulator(TimeSpan gotoTravel, TimeSpan defaultTimeout, IEnumerable<PtzPreset>? presets = null, PtzVector? home = null,
        PresetTokenStyle tokenStyle = PresetTokenStyle.Numeric)
    {
        GotoTravel = gotoTravel < TimeSpan.Zero ? TimeSpan.Zero : gotoTravel;
        DefaultTimeout = defaultTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : defaultTimeout;
        _tokenStyle = tokenStyle;
        _presets = (presets ?? DefaultPresets(tokenStyle)).ToList();
        _home = home ?? (_presets.FirstOrDefault()?.Position ?? PtzVector.Zero);
        _origin = _home;
        _presetSeq = _presets.Count;
    }

    public TimeSpan GotoTravel { get; }
    public TimeSpan DefaultTimeout { get; }

    /// <summary>Shape of the tokens this head hands out (default presets and new SetPreset tokens).</summary>
    public PresetTokenStyle TokenStyle { get { lock (_gate) return _tokenStyle; } }

    /// <summary>P1..P5 - tokens "1".."5" (the GIS sends server preset_index as the ONVIF token), P1 = home.</summary>
    public static IReadOnlyList<PtzPreset> DefaultPresets() => DefaultPresets(PresetTokenStyle.Numeric);

    /// <summary>P1..P5 at the same positions, tokens shaped by <paramref name="style"/> (names stay "P1".."P5").</summary>
    public static IReadOnlyList<PtzPreset> DefaultPresets(PresetTokenStyle style) => new[]
    {
        new PtzPreset(PresetTokens.Format(style, 1), "P1", new PtzVector(0.0, 0.0, 0.0)),
        new PtzPreset(PresetTokens.Format(style, 2), "P2", new PtzVector(-0.5, 0.2, 0.3)),
        new PtzPreset(PresetTokens.Format(style, 3), "P3", new PtzVector(0.5, -0.2, 0.5)),
        new PtzPreset(PresetTokens.Format(style, 4), "P4", new PtzVector(0.8, 0.4, 0.8)),
        new PtzPreset(PresetTokens.Format(style, 5), "P5", new PtzVector(-0.8, -0.4, 1.0)),
    };

    public IReadOnlyList<PtzPreset> Presets { get { lock (_gate) return _presets.ToList(); } }

    /// <summary>
    /// Replaces the preset list with the defaults in <paramref name="style"/> (control endpoint: switch a running camera
    /// between token shapes). Home and the current position are kept; a goto to a preset is cancelled.
    /// </summary>
    public void ResetPresets(PresetTokenStyle style, DateTime nowUtc)
    {
        lock (_gate)
        {
            Settle(nowUtc);
            if (_kind == MotionKind.Goto && _targetPreset is not null && _targetPreset != "home") { _kind = MotionKind.Idle; _targetPreset = null; }
            _tokenStyle = style;
            _presets.Clear();
            _presets.AddRange(DefaultPresets(style));
            _presetSeq = _presets.Count;
        }
    }

    public PtzVector Home { get { lock (_gate) return _home; } }

    /// <summary>Finds a preset by token; "P2" is accepted as an alias of "2" (token = number vs name conventions).</summary>
    public PtzPreset? FindPreset(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        lock (_gate) return FindPresetCore(token.Trim());
    }

    private PtzPreset? FindPresetCore(string token)
        => _presets.FirstOrDefault(p => p.Token == token)
           ?? _presets.FirstOrDefault(p => string.Equals(p.Name, token, StringComparison.OrdinalIgnoreCase))
           ?? (token.Length > 1 && (token[0] == 'P' || token[0] == 'p') ? _presets.FirstOrDefault(p => p.Token == token[1..]) : null);

    public PtzSnapshot Status(DateTime nowUtc)
    {
        lock (_gate) return StatusCore(nowUtc);
    }

    /// <summary>Continuous velocity (each axis in [-1, 1]; null axis = 0). timeout null → <see cref="DefaultTimeout"/>.</summary>
    public void ContinuousMove(double? pan, double? tilt, double? zoom, TimeSpan? timeout, DateTime nowUtc)
    {
        lock (_gate)
        {
            Settle(nowUtc);
            var v = new PtzVector(Math.Clamp(pan ?? 0, -1, 1), Math.Clamp(tilt ?? 0, -1, 1), Math.Clamp(zoom ?? 0, -1, 1));
            if (v == PtzVector.Zero) { _kind = MotionKind.Idle; _velocity = PtzVector.Zero; return; }
            _kind = MotionKind.Continuous;
            _velocity = v;
            _t0 = nowUtc;
            _tEnd = nowUtc + (timeout is { } t && t > TimeSpan.Zero ? t : DefaultTimeout);
            _targetPreset = null;
        }
    }

    public void Stop(bool panTilt, bool zoom, DateTime nowUtc)
    {
        lock (_gate)
        {
            Settle(nowUtc);
            if (_kind == MotionKind.Goto) { _kind = MotionKind.Idle; _targetPreset = null; return; }   // a stop cancels a goto
            if (_kind != MotionKind.Continuous) return;
            _velocity = new PtzVector(panTilt ? 0 : _velocity.Pan, panTilt ? 0 : _velocity.Tilt, zoom ? 0 : _velocity.Zoom);
            if (_velocity == PtzVector.Zero) _kind = MotionKind.Idle;
            else _t0 = nowUtc;
        }
    }

    /// <summary>false = unknown preset token.</summary>
    public bool GotoPreset(string token, DateTime nowUtc)
    {
        lock (_gate)
        {
            var p = FindPresetCore(token?.Trim() ?? string.Empty);
            if (p is null) return false;
            StartGoto(p.Position, p.Token, nowUtc);
            return true;
        }
    }

    public void GotoHome(DateTime nowUtc)
    {
        lock (_gate) StartGoto(_home, "home", nowUtc);
    }

    public void AbsoluteMove(PtzVector target, DateTime nowUtc)
    {
        lock (_gate) StartGoto(target.Clamp(), null, nowUtc);
    }

    public void RelativeMove(PtzVector delta, DateTime nowUtc)
    {
        lock (_gate)
        {
            var at = PositionAt(nowUtc);
            StartGoto(new PtzVector(at.Pan + delta.Pan, at.Tilt + delta.Tilt, at.Zoom + delta.Zoom).Clamp(), null, nowUtc);
        }
    }

    /// <summary>Stores the current position. token null → a new token. Returns the token.</summary>
    public string SetPreset(string? token, string? name, DateTime nowUtc)
    {
        lock (_gate)
        {
            var at = PositionAt(nowUtc);
            var existing = string.IsNullOrWhiteSpace(token) ? null : FindPresetCore(token!.Trim());
            if (existing is not null)
            {
                int i = _presets.IndexOf(existing);
                _presets[i] = existing with { Name = string.IsNullOrWhiteSpace(name) ? existing.Name : name!, Position = at };
                return existing.Token;
            }
            string newToken;
            // New tokens follow the camera's shape; mixed cameras hand out plain numbers for new presets.
            var newStyle = _tokenStyle == PresetTokenStyle.Mixed ? PresetTokenStyle.Numeric : _tokenStyle;
            do { newToken = PresetTokens.Format(newStyle, ++_presetSeq); }
            while (_presets.Any(p => p.Token == newToken));
            _presets.Add(new PtzPreset(newToken, string.IsNullOrWhiteSpace(name) ? $"P{_presetSeq}" : name!, at));
            return newToken;
        }
    }

    public bool RemovePreset(string token)
    {
        lock (_gate)
        {
            var p = FindPresetCore(token?.Trim() ?? string.Empty);
            return p is not null && _presets.Remove(p);
        }
    }

    public void SetHome(DateTime nowUtc)
    {
        lock (_gate) _home = PositionAt(nowUtc);
    }

    /// <summary>Back to home instantly, idle (control endpoint / test reset).</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _kind = MotionKind.Idle;
            _origin = _home;
            _velocity = PtzVector.Zero;
            _targetPreset = null;
        }
    }

    // ── core (callers hold _gate) ───────────────────────────────────────────

    private void StartGoto(PtzVector target, string? presetToken, DateTime nowUtc)
    {
        Settle(nowUtc);
        _target = target.Clamp();
        _targetPreset = presetToken;
        _t0 = nowUtc;
        _tEnd = nowUtc + GotoTravel;
        _kind = GotoTravel == TimeSpan.Zero ? MotionKind.Idle : MotionKind.Goto;
        if (_kind == MotionKind.Idle) { _origin = _target; _targetPreset = null; }
    }

    /// <summary>Freezes the motion into _origin at now (so the next command starts from the true position).</summary>
    private void Settle(DateTime nowUtc)
    {
        var at = PositionAt(nowUtc);
        bool finished = _kind switch
        {
            MotionKind.Continuous => nowUtc >= _tEnd,
            MotionKind.Goto => nowUtc >= _tEnd,
            _ => false,
        };
        _origin = at;
        _t0 = nowUtc;
        if (finished) { _kind = MotionKind.Idle; _velocity = PtzVector.Zero; _targetPreset = null; }
    }

    private PtzVector PositionAt(DateTime nowUtc)
    {
        switch (_kind)
        {
            case MotionKind.Continuous:
            {
                var until = nowUtc < _tEnd ? nowUtc : _tEnd;
                var dt = Math.Max(0, (until - _t0).TotalSeconds);
                return new PtzVector(
                    _origin.Pan + _velocity.Pan * PanTiltRatePerSec * dt,
                    _origin.Tilt + _velocity.Tilt * PanTiltRatePerSec * dt,
                    _origin.Zoom + _velocity.Zoom * ZoomRatePerSec * dt).Clamp();
            }
            case MotionKind.Goto:
            {
                var total = (_tEnd - _t0).TotalSeconds;
                var f = total <= 0 ? 1 : Math.Clamp((nowUtc - _t0).TotalSeconds / total, 0, 1);
                return new PtzVector(
                    _origin.Pan + (_target.Pan - _origin.Pan) * f,
                    _origin.Tilt + (_target.Tilt - _origin.Tilt) * f,
                    _origin.Zoom + (_target.Zoom - _origin.Zoom) * f);
            }
            default:
                return _origin;
        }
    }

    private PtzSnapshot StatusCore(DateTime nowUtc)
    {
        var at = PositionAt(nowUtc);
        switch (_kind)
        {
            case MotionKind.Continuous when nowUtc < _tEnd:
            {
                bool panMoving = _velocity.Pan != 0 && !AtLimit(at.Pan, _velocity.Pan, -1, 1);
                bool tiltMoving = _velocity.Tilt != 0 && !AtLimit(at.Tilt, _velocity.Tilt, -1, 1);
                bool zoomMoving = _velocity.Zoom != 0 && !AtLimit(at.Zoom, _velocity.Zoom, 0, 1);
                var moving = panMoving || tiltMoving || zoomMoving;
                return new PtzSnapshot(at, panMoving || tiltMoving, zoomMoving, moving ? "continuous" : "idle", null, null, moving ? _velocity : PtzVector.Zero);
            }
            case MotionKind.Goto when nowUtc < _tEnd:
            {
                bool pt = _target.Pan != _origin.Pan || _target.Tilt != _origin.Tilt;
                bool z = _target.Zoom != _origin.Zoom;
                if (!pt && !z) pt = true;   // same spot: still report the travel window (the head "settles")
                return new PtzSnapshot(at, pt, z, "goto", _targetPreset, _tEnd, PtzVector.Zero);
            }
            default:
                return new PtzSnapshot(at, false, false, "idle", null, null, PtzVector.Zero);
        }
    }

    private static bool AtLimit(double value, double velocity, double min, double max)
        => (velocity > 0 && value >= max) || (velocity < 0 && value <= min);
}
