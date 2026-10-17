using Dapper;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Monitoring.Models.Fences;
using Ironwall.Dotnet.Monitoring.Models.Fences.Presets;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Services;

/// <summary>
/// 센서 모델 · 위치 프리셋 · 속성 프리셋 저장소(sensor-mount-preset D1 · §5.1) — 결선 레이아웃과 같은 로컬 DB(<c>gmap_tiles_db</c>).
/// </summary>
/// <remarks>
/// <para><b>부팅 경로에 들어가지 않는다</b> — <see cref="FenceLayoutStore"/> 와 같이 처음 읽거나 쓸 때 표를 만들고(<c>CREATE TABLE IF NOT EXISTS</c>)
/// 회사 기본 7종(<see cref="SensorPresetCatalog"/>)을 <c>INSERT IGNORE</c> 로 심는다(멱등 — 사용자가 고친 기본 프리셋은 덮지 않는다).</para>
/// <para>쓰임 셈(<see cref="ISensorPresetUsageSource"/>)은 같은 DB 의 <c>WiringFenceLayoutsV2</c> 본문을 읽어 낸다.</para>
/// </remarks>
internal sealed class GisDbSensorPresetRepository : ISensorPresetRepository, ISensorPresetUsageSource
{
    internal const string CREATE_MODELS_SQL = @"
        CREATE TABLE IF NOT EXISTS `SensorModels` (
            `Id`           INT NOT NULL AUTO_INCREMENT,
            `Code`         VARCHAR(64) NOT NULL,
            `Name`         VARCHAR(128) NOT NULL,
            `DeviceType`   VARCHAR(32) NOT NULL,
            `Shape`        VARCHAR(32) NOT NULL,
            `SizeWMm`      DOUBLE NOT NULL DEFAULT 0,
            `SizeHMm`      DOUBLE NOT NULL DEFAULT 0,
            `SizeLMm`      DOUBLE NOT NULL DEFAULT 0,
            `Manufacturer` VARCHAR(128) NULL,
            `AllowedKinds` VARCHAR(128) NOT NULL DEFAULT '',
            `IsBuiltin`    TINYINT(1) NOT NULL DEFAULT 0,
            `IsHidden`     TINYINT(1) NOT NULL DEFAULT 0,
            `UpdatedAt`    DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            PRIMARY KEY (`Id`),
            UNIQUE KEY `UX_SensorModels_Code` (`Code`)
        );";

    internal const string CREATE_MOUNTS_SQL = @"
        CREATE TABLE IF NOT EXISTS `SensorMountPresets` (
            `Id`             INT NOT NULL AUTO_INCREMENT,
            `ModelId`        INT NOT NULL,
            `Name`           VARCHAR(128) NOT NULL,
            `IsDefault`      TINYINT(1) NOT NULL DEFAULT 0,
            `Anchor`         VARCHAR(32) NOT NULL,
            `AlongRatio`     DOUBLE NOT NULL DEFAULT 0.5,
            `HeightM`        DOUBLE NOT NULL DEFAULT 0,
            `DepthM`         DOUBLE NOT NULL DEFAULT 0,
            `YawDeg`         DOUBLE NOT NULL DEFAULT 90,
            `TiltDeg`        DOUBLE NOT NULL DEFAULT 0,
            `SnapPoint`      VARCHAR(64) NULL,
            `BracketM`       DOUBLE NOT NULL DEFAULT 0,
            `PanelWidthM`    DOUBLE NOT NULL DEFAULT 3,
            `FenceHeightM`   DOUBLE NOT NULL DEFAULT 2.4,
            `SampleHasRazor` TINYINT(1) NOT NULL DEFAULT 0,
            `Note`           VARCHAR(512) NULL,
            `UpdatedAt`      DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            PRIMARY KEY (`Id`),
            KEY `IX_SensorMountPresets_Model` (`ModelId`)
        );";

    internal const string CREATE_ATTRS_SQL = @"
        CREATE TABLE IF NOT EXISTS `SensorAttributePresets` (
            `Id`         INT NOT NULL AUTO_INCREMENT,
            `ModelId`    INT NOT NULL,
            `Name`       VARCHAR(128) NOT NULL,
            `IsDefault`  TINYINT(1) NOT NULL DEFAULT 0,
            `ValuesJson` MEDIUMTEXT NOT NULL,
            `UpdatedAt`  DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
            PRIMARY KEY (`Id`),
            KEY `IX_SensorAttributePresets_Model` (`ModelId`)
        );";

    private const string MODEL_COLUMNS = "`Code`, `Name`, `DeviceType`, `Shape`, `SizeWMm`, `SizeHMm`, `SizeLMm`, `Manufacturer`, `AllowedKinds`, `IsBuiltin`, `IsHidden`";
    private const string MODEL_VALUES = "@Code, @Name, @DeviceType, @Shape, @SizeWMm, @SizeHMm, @SizeLMm, @Manufacturer, @AllowedKinds, @IsBuiltin, @IsHidden";
    private const string MOUNT_COLUMNS = "`ModelId`, `Name`, `IsDefault`, `Anchor`, `AlongRatio`, `HeightM`, `DepthM`, `YawDeg`, `TiltDeg`, `SnapPoint`, `BracketM`, `PanelWidthM`, `FenceHeightM`, `SampleHasRazor`, `Note`";
    private const string MOUNT_VALUES = "@ModelId, @Name, @IsDefault, @Anchor, @AlongRatio, @HeightM, @DepthM, @YawDeg, @TiltDeg, @SnapPoint, @BracketM, @PanelWidthM, @FenceHeightM, @SampleHasRazor, @Note";

    private static readonly JsonSerializerSettings Json = new() { Converters = { new StringEnumConverter() }, NullValueHandling = NullValueHandling.Ignore };

    private readonly IGMapDbService _db;
    private readonly ILogService? _log;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private bool _schemaReady;

    public GisDbSensorPresetRepository(IGMapDbService db, ILogService? log = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _log = log;
    }

    public SensorPresetSource Source => SensorPresetSource.GisDb;
    public bool CanWrite => true;

    public async Task<IReadOnlyList<SensorModel>> GetModelsAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<ModelRow>(new CommandDefinition("SELECT * FROM `SensorModels` ORDER BY `Id`;", cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<IReadOnlyList<SensorMountPreset>> GetMountPresetsAsync(int modelId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<MountRow>(new CommandDefinition(
            "SELECT * FROM `SensorMountPresets` WHERE `ModelId` = @ModelId ORDER BY `Id`;", new { ModelId = modelId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => r.ToPreset()).ToList();
    }

    public async Task<IReadOnlyList<SensorAttributePreset>> GetAttributePresetsAsync(int modelId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var rows = await conn.QueryAsync<AttrRow>(new CommandDefinition(
            "SELECT * FROM `SensorAttributePresets` WHERE `ModelId` = @ModelId ORDER BY `Id`;", new { ModelId = modelId }, cancellationToken: ct)).ConfigureAwait(false);
        return rows.Select(r => r.ToPreset()).ToList();
    }

    public async Task<SensorModel> SaveModelAsync(SensorModel model, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        var args = ModelRow.From(model);
        if (model.Id > 0)
        {
            var builtin = await conn.ExecuteScalarAsync<int?>(new CommandDefinition("SELECT `IsBuiltin` FROM `SensorModels` WHERE `Id` = @Id;", new { model.Id }, cancellationToken: ct)).ConfigureAwait(false);
            var sql = builtin == 1
                ? "UPDATE `SensorModels` SET `Name` = @Name, `IsHidden` = @IsHidden WHERE `Id` = @Id;"       // 기본 모델은 숨기기 · 이름만
                : "UPDATE `SensorModels` SET `Code` = @Code, `Name` = @Name, `DeviceType` = @DeviceType, `Shape` = @Shape, `SizeWMm` = @SizeWMm, `SizeHMm` = @SizeHMm, " +
                  "`SizeLMm` = @SizeLMm, `Manufacturer` = @Manufacturer, `AllowedKinds` = @AllowedKinds, `IsHidden` = @IsHidden WHERE `Id` = @Id;";
            await conn.ExecuteAsync(new CommandDefinition(sql, args, cancellationToken: ct)).ConfigureAwait(false);
            var row = await conn.QuerySingleAsync<ModelRow>(new CommandDefinition("SELECT * FROM `SensorModels` WHERE `Id` = @Id;", new { model.Id }, cancellationToken: ct)).ConfigureAwait(false);
            return row.ToModel();
        }
        args.IsBuiltin = false;
        var id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"INSERT INTO `SensorModels` ({MODEL_COLUMNS}) VALUES ({MODEL_VALUES}); SELECT LAST_INSERT_ID();", args, cancellationToken: ct)).ConfigureAwait(false);
        return model with { Id = id, IsBuiltin = false };
    }

    public async Task<SensorMountPreset> SaveMountPresetAsync(SensorMountPreset preset, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        var args = MountRow.From(preset);
        int id;
        if (preset.Id > 0)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE `SensorMountPresets` SET `ModelId` = @ModelId, `Name` = @Name, `IsDefault` = @IsDefault, `Anchor` = @Anchor, `AlongRatio` = @AlongRatio, `HeightM` = @HeightM, " +
                "`DepthM` = @DepthM, `YawDeg` = @YawDeg, `TiltDeg` = @TiltDeg, `SnapPoint` = @SnapPoint, `BracketM` = @BracketM, `PanelWidthM` = @PanelWidthM, " +
                "`FenceHeightM` = @FenceHeightM, `SampleHasRazor` = @SampleHasRazor, `Note` = @Note WHERE `Id` = @Id;", args, tx, cancellationToken: ct)).ConfigureAwait(false);
            id = preset.Id;
        }
        else
        {
            id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                $"INSERT INTO `SensorMountPresets` ({MOUNT_COLUMNS}) VALUES ({MOUNT_VALUES}); SELECT LAST_INSERT_ID();", args, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        await OneDefaultAsync(conn, tx, "SensorMountPresets", preset.ModelId, preset.IsDefault ? id : null, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        var row = await conn.QuerySingleAsync<MountRow>(new CommandDefinition("SELECT * FROM `SensorMountPresets` WHERE `Id` = @Id;", new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
        return row.ToPreset();
    }

    public async Task<SensorAttributePreset> SaveAttributePresetAsync(SensorAttributePreset preset, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        var args = new { preset.Id, preset.ModelId, preset.Name, preset.IsDefault, ValuesJson = JsonConvert.SerializeObject(preset.Values, Json) };
        int id;
        if (preset.Id > 0)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE `SensorAttributePresets` SET `ModelId` = @ModelId, `Name` = @Name, `IsDefault` = @IsDefault, `ValuesJson` = @ValuesJson WHERE `Id` = @Id;", args, tx, cancellationToken: ct)).ConfigureAwait(false);
            id = preset.Id;
        }
        else
        {
            id = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
                "INSERT INTO `SensorAttributePresets` (`ModelId`, `Name`, `IsDefault`, `ValuesJson`) VALUES (@ModelId, @Name, @IsDefault, @ValuesJson); SELECT LAST_INSERT_ID();",
                args, tx, cancellationToken: ct)).ConfigureAwait(false);
        }
        await OneDefaultAsync(conn, tx, "SensorAttributePresets", preset.ModelId, preset.IsDefault ? id : null, ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        var row = await conn.QuerySingleAsync<AttrRow>(new CommandDefinition("SELECT * FROM `SensorAttributePresets` WHERE `Id` = @Id;", new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);
        return row.ToPreset();
    }

    public async Task DeletePresetAsync(PresetKey key, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct).ConfigureAwait(false);
        switch (key.Kind)
        {
            case PresetKind.Model:
                var builtin = await conn.ExecuteScalarAsync<int?>(new CommandDefinition("SELECT `IsBuiltin` FROM `SensorModels` WHERE `Id` = @Id;", new { key.Id }, cancellationToken: ct)).ConfigureAwait(false);
                if (builtin == 1) throw new InvalidOperationException("회사 기본 모델은 지울 수 없습니다 — 숨기기만 됩니다.");
                await conn.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM `SensorMountPresets` WHERE `ModelId` = @Id; DELETE FROM `SensorAttributePresets` WHERE `ModelId` = @Id; DELETE FROM `SensorModels` WHERE `Id` = @Id;",
                    new { key.Id }, cancellationToken: ct)).ConfigureAwait(false);
                break;
            case PresetKind.Mount:
                await conn.ExecuteAsync(new CommandDefinition("DELETE FROM `SensorMountPresets` WHERE `Id` = @Id;", new { key.Id }, cancellationToken: ct)).ConfigureAwait(false);
                break;
            default:
                await conn.ExecuteAsync(new CommandDefinition("DELETE FROM `SensorAttributePresets` WHERE `Id` = @Id;", new { key.Id }, cancellationToken: ct)).ConfigureAwait(false);
                break;
        }
    }

    public async Task<IReadOnlyList<SensorPresetUsage>> GetUsagesAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = await OpenAsync(ct).ConfigureAwait(false);
            await conn.ExecuteAsync(new CommandDefinition(FenceLayoutStore.CREATE_TABLE_SQL, cancellationToken: ct)).ConfigureAwait(false);
            var rows = await conn.QueryAsync<(string ServerKey, string Body)>(new CommandDefinition(
                "SELECT `ServerKey`, `Body` FROM `WiringFenceLayoutsV2`;", cancellationToken: ct)).ConfigureAwait(false);
            var list = new List<SensorPresetUsage>();
            foreach (var (server, body) in rows)
                if (FenceLayoutJson.Deserialize(body) is { } doc) list.AddRange(SensorPresetUsageMath.FromDocument(server, doc));
            return list;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log?.Warning($"[{nameof(GisDbSensorPresetRepository)}] 프리셋 쓰임 읽기 실패: {ex.Message}");
            return Array.Empty<SensorPresetUsage>();
        }
    }

    #region - schema · seed -
    private async Task<MySqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = await _db.OpenConnectionAsync(ct).ConfigureAwait(false);
        if (_schemaReady) return conn;
        try
        {
            await _schemaGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_schemaReady) return conn;
                await conn.ExecuteAsync(new CommandDefinition(CREATE_MODELS_SQL + CREATE_MOUNTS_SQL + CREATE_ATTRS_SQL, cancellationToken: ct)).ConfigureAwait(false);
                await SeedAsync(conn, ct).ConfigureAwait(false);
                _schemaReady = true;
                return conn;
            }
            finally { _schemaGate.Release(); }
        }
        catch
        {
            await conn.DisposeAsync().ConfigureAwait(false);       // 표 만들기 실패 — 연결을 남기지 않는다(부르는 쪽이 화면 값을 지키고 다시 시도)
            throw;
        }
    }

    /// <summary>회사 기본 7종 · 기본 프리셋 심기 — 이미 있으면(같은 id) 건드리지 않는다.</summary>
    private static async Task SeedAsync(MySqlConnection conn, CancellationToken ct)
    {
        foreach (var m in SensorPresetCatalog.Models)
        {
            var row = ModelRow.From(m);
            await conn.ExecuteAsync(new CommandDefinition($"INSERT IGNORE INTO `SensorModels` (`Id`, {MODEL_COLUMNS}) VALUES (@Id, {MODEL_VALUES});", row, cancellationToken: ct)).ConfigureAwait(false);
        }
        var seeded = await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM `SensorMountPresets`;", cancellationToken: ct)).ConfigureAwait(false);
        if (seeded == 0)
            foreach (var p in SensorPresetCatalog.MountPresets)
                await conn.ExecuteAsync(new CommandDefinition($"INSERT IGNORE INTO `SensorMountPresets` (`Id`, {MOUNT_COLUMNS}) VALUES (@Id, {MOUNT_VALUES});", MountRow.From(p), cancellationToken: ct)).ConfigureAwait(false);
        var attrs = await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM `SensorAttributePresets`;", cancellationToken: ct)).ConfigureAwait(false);
        if (attrs == 0)
            foreach (var p in SensorPresetCatalog.AttributePresets)
                await conn.ExecuteAsync(new CommandDefinition(
                    "INSERT IGNORE INTO `SensorAttributePresets` (`Id`, `ModelId`, `Name`, `IsDefault`, `ValuesJson`) VALUES (@Id, @ModelId, @Name, @IsDefault, @ValuesJson);",
                    new { p.Id, p.ModelId, p.Name, p.IsDefault, ValuesJson = JsonConvert.SerializeObject(p.Values, Json) }, cancellationToken: ct)).ConfigureAwait(false);
    }

    /// <summary>모델당 ★기본 하나 — <paramref name="defaultId"/> 가 있으면 나머지를 내리고, 기본이 없으면 가장 작은 id 를 기본으로.</summary>
    private static async Task OneDefaultAsync(MySqlConnection conn, MySqlTransaction tx, string table, int modelId, int? defaultId, CancellationToken ct)
    {
        if (defaultId is { } id)
            await conn.ExecuteAsync(new CommandDefinition($"UPDATE `{table}` SET `IsDefault` = 0 WHERE `ModelId` = @ModelId AND `Id` <> @Id;", new { ModelId = modelId, Id = id }, tx, cancellationToken: ct)).ConfigureAwait(false);
        var any = await conn.ExecuteScalarAsync<int>(new CommandDefinition($"SELECT COUNT(*) FROM `{table}` WHERE `ModelId` = @ModelId AND `IsDefault` = 1;", new { ModelId = modelId }, tx, cancellationToken: ct)).ConfigureAwait(false);
        if (any == 0)
            await conn.ExecuteAsync(new CommandDefinition($"UPDATE `{table}` SET `IsDefault` = 1 WHERE `ModelId` = @ModelId ORDER BY `Id` LIMIT 1;", new { ModelId = modelId }, tx, cancellationToken: ct)).ConfigureAwait(false);
    }
    #endregion

    #region - rows -
    private sealed class ModelRow
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string Shape { get; set; } = string.Empty;
        public double SizeWMm { get; set; }
        public double SizeHMm { get; set; }
        public double SizeLMm { get; set; }
        public string? Manufacturer { get; set; }
        public string AllowedKinds { get; set; } = string.Empty;
        public bool IsBuiltin { get; set; }
        public bool IsHidden { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public static ModelRow From(SensorModel m) => new()
        {
            Id = m.Id, Code = m.Code, Name = m.Name, DeviceType = m.DeviceType.ToString(), Shape = m.Shape.ToString(),
            SizeWMm = m.SizeMm.W, SizeHMm = m.SizeMm.H, SizeLMm = m.SizeMm.L, Manufacturer = m.Manufacturer,
            AllowedKinds = string.Join(",", m.AllowedKinds), IsBuiltin = m.IsBuiltin, IsHidden = m.IsHidden,
        };

        public SensorModel ToModel() => new()
        {
            Id = Id, Code = Code, Name = Name,
            DeviceType = Enum.TryParse<EnumDeviceType>(DeviceType, out var t) ? t : EnumDeviceType.NONE,
            Shape = Enum.TryParse<SensorShape>(Shape, out var s) ? s : SensorShape.Smart,
            SizeMm = new SensorSizeMm(SizeWMm, SizeHMm, SizeLMm), Manufacturer = Manufacturer,
            AllowedKinds = AllowedKinds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(k => Enum.TryParse<SensorAnchorKind>(k.Trim(), out var v) ? v : (SensorAnchorKind?)null)
                                       .Where(v => v is not null).Select(v => v!.Value).ToList(),
            IsBuiltin = IsBuiltin, IsHidden = IsHidden, UpdatedAt = UpdatedAt,
        };
    }

    private sealed class MountRow
    {
        public int Id { get; set; }
        public int ModelId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public string Anchor { get; set; } = string.Empty;
        public double AlongRatio { get; set; }
        public double HeightM { get; set; }
        public double DepthM { get; set; }
        public double YawDeg { get; set; }
        public double TiltDeg { get; set; }
        public string? SnapPoint { get; set; }
        public double BracketM { get; set; }
        public double PanelWidthM { get; set; }
        public double FenceHeightM { get; set; }
        public bool SampleHasRazor { get; set; }
        public string? Note { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public static MountRow From(SensorMountPreset p) => new()
        {
            Id = p.Id, ModelId = p.ModelId, Name = p.Name, IsDefault = p.IsDefault, Anchor = p.Anchor.ToString(), AlongRatio = p.AlongRatio, HeightM = p.HeightM,
            DepthM = p.DepthM, YawDeg = p.YawDeg, TiltDeg = p.TiltDeg, SnapPoint = p.SnapPoint, BracketM = p.BracketM, PanelWidthM = p.PanelWidthM,
            FenceHeightM = p.FenceHeightM, SampleHasRazor = p.SampleHasRazor, Note = p.Note,
        };

        public SensorMountPreset ToPreset() => new()
        {
            Id = Id, ModelId = ModelId, Name = Name, IsDefault = IsDefault,
            Anchor = Enum.TryParse<SensorMountAnchor>(Anchor, out var a) ? a : SensorMountAnchor.Mesh,
            AlongRatio = AlongRatio, HeightM = HeightM, DepthM = DepthM, YawDeg = YawDeg, TiltDeg = TiltDeg, SnapPoint = SnapPoint, BracketM = BracketM,
            PanelWidthM = PanelWidthM, FenceHeightM = FenceHeightM, SampleHasRazor = SampleHasRazor, Note = Note, UpdatedAt = UpdatedAt,
        };
    }

    private sealed class AttrRow
    {
        public int Id { get; set; }
        public int ModelId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public string ValuesJson { get; set; } = "[]";
        public DateTime? UpdatedAt { get; set; }

        public SensorAttributePreset ToPreset()
        {
            IReadOnlyList<SensorAttributeValue> values;
            try { values = JsonConvert.DeserializeObject<List<SensorAttributeValue>>(ValuesJson, Json) ?? new List<SensorAttributeValue>(); }
            catch (JsonException) { values = Array.Empty<SensorAttributeValue>(); }
            return new SensorAttributePreset { Id = Id, ModelId = ModelId, Name = Name, IsDefault = IsDefault, Values = values, UpdatedAt = UpdatedAt };
        }
    }
    #endregion
}
