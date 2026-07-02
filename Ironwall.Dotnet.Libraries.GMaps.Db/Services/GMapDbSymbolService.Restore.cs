using Dapper;
using Ironwall.Dotnet.Monitoring.Models.Symbols;
using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.GMaps.Db.Services;

/// <summary>삭제 취소 복원 시 원래 Id가 이미 점유된 경우(Undo-Delete Id 충돌). 호출부가 새-Id 폴백에 사용.</summary>
public sealed class IdCollisionException : Exception
{
    public int Id { get; }
    public IdCollisionException(int id) : base($"Id {id}가 이미 존재합니다(복원 충돌).") => Id = id;
}

/****************************************************************************
   Purpose      : 삭제 취소용 Id 보존 복원 — Symbols(Id 명시)+타입행+점 재삽입 + AUTO_INCREMENT 리셋.
                  기존 Insert 무변경(가산형). Map_Edit_Undo_Redo FR-06.
   Note         : Id 점유 시 IdCollisionException(호출부 폴백). 스냅샷은 라이브 딥클론(재-fetch 금지).
   Created On   : 2026-07-03 · Sensorway Co., Ltd.
****************************************************************************/
internal partial class GMapDbSymbolService
{
    // ── 공용 헬퍼 ──

    private static async Task<bool> SymbolIdExistsAsync(MySqlConnection conn, IDbTransaction? tx, int id)
        => await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Symbols WHERE Id = @id;", new { id }, tx) > 0;

    /// <summary>Symbols 행을 명시 Id로 삽입(전 타입 공통 컬럼). InsertSymbolAsync와 동일 컬럼 + Id.</summary>
    private static async Task InsertSymbolRowWithIdAsync(MySqlConnection conn, IDbTransaction? tx, ISymbolModel model)
    {
        const string sql = @"
            INSERT INTO Symbols
                (Id, Pid, Title, TitleSize, OperationState, Latitude, Longitude, Altitude, Zoom, Bearing,
                 Width, Height, Category, ShowShape, ShowTitle, IsLocked,
                 FillColor, StrokeColor, StrokeThickness, ZOrder, LabelOffsetX, LabelOffsetY, CreatedBy)
            VALUES (@Id, @Pid, @Title, @TitleSize, @OperationState, @Latitude, @Longitude, @Altitude, @Zoom, @Bearing,
                    @Width, @Height, @Category, @ShowShape, @ShowTitle, @IsLocked,
                    @FillColor, @StrokeColor, @StrokeThickness, @ZOrder, @LabelOffsetX, @LabelOffsetY, @CreatedBy);";
        await conn.ExecuteAsync(sql, new
        {
            model.Id,
            model.Pid,
            model.Title,
            model.TitleSize,
            OperationState = model.OperationState.ToString(),
            model.Latitude,
            model.Longitude,
            model.Altitude,
            model.Zoom,
            model.Bearing,
            model.Width,
            model.Height,
            Category = model.Category.ToString(),
            model.ShowShape,
            model.IsLocked,
            model.ShowTitle,
            FillColor = model.FillColor.ToString(),
            StrokeColor = model.StrokeColor.ToString(),
            model.StrokeThickness,
            model.ZOrder,
            model.LabelOffsetX,
            model.LabelOffsetY,
            CreatedBy = "System"
        }, tx);
    }

    /// <summary>재삽입 Id가 현재 AUTO_INCREMENT 이상이면 카운터를 MAX(Id)+1로 밀어 향후 충돌 방지(best-effort).</summary>
    private async Task ResetSymbolsAutoIncrementAsync(MySqlConnection conn)
    {
        try
        {
            var next = await conn.ExecuteScalarAsync<int>("SELECT COALESCE(MAX(Id),0)+1 FROM Symbols;");
            await conn.ExecuteAsync($"ALTER TABLE Symbols AUTO_INCREMENT = {next};");
        }
        catch (Exception ex) { _log?.Warning($"Symbols AUTO_INCREMENT 리셋 실패(무시): {ex.Message}"); }
    }

    private async Task GuardCollisionAsync(MySqlConnection conn, IDbTransaction? tx, int id)
    {
        if (await SymbolIdExistsAsync(conn, tx, id)) throw new IdCollisionException(id);
    }

    // ── 타입별 복원 ──

    public async Task<int> RestoreSymbolAsync(ISymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await GuardCollisionAsync(conn, null, model.Id);
        await InsertSymbolRowWithIdAsync(conn, null, model);
        await ResetSymbolsAutoIncrementAsync(conn);
        _log?.Info($"Symbol 복원 완료 - Id={model.Id}");
        return model.Id;
    }

    public async Task<int> RestoreGeometrySymbolAsync(IGeometricSymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var tx = await conn.BeginTransactionAsync(token);
        try
        {
            await GuardCollisionAsync(conn, tx, model.Id);
            await InsertSymbolRowWithIdAsync(conn, tx, model);
            await conn.ExecuteAsync(
                "INSERT INTO GeometrySymbols (SymbolId, ShapeType, Opacity) VALUES (@SymbolId, @ShapeType, @Opacity);",
                new { SymbolId = model.Id, ShapeType = model.ShapeType.ToString(), model.Opacity }, tx);
            await tx.CommitAsync(token);
            await ResetSymbolsAutoIncrementAsync(conn);
            return model.Id;
        }
        catch { await tx.RollbackAsync(token); throw; }
    }

    public async Task<int> RestorePidsSymbolAsync(IPidsSymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var tx = await conn.BeginTransactionAsync(token);
        try
        {
            await GuardCollisionAsync(conn, tx, model.Id);
            await InsertSymbolRowWithIdAsync(conn, tx, model);
            await conn.ExecuteAsync(
                "INSERT INTO PidsSymbols (SymbolId, LinkedDeviceId, DeviceType, ShowFOV, FOVColor, FOVOpacity, EventStatus, BaseBearing) VALUES (@SymbolId, @LinkedDeviceId, @DeviceType, @ShowFOV, @FOVColor, @FOVOpacity, @EventStatus, @BaseBearing);",
                new { SymbolId = model.Id, model.LinkedDeviceId, DeviceType = model.DeviceType.ToString(), model.ShowFOV, FOVColor = model.FOVColor.ToString(), model.FOVOpacity, EventStatus = model.EventStatus.ToString(), model.BaseBearing }, tx);
            await tx.CommitAsync(token);
            await ResetSymbolsAutoIncrementAsync(conn);
            return model.Id;
        }
        catch { await tx.RollbackAsync(token); throw; }
    }

    public async Task<int> RestoreMilitarySymbolAsync(IMilitarySymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var tx = await conn.BeginTransactionAsync(token);
        try
        {
            await GuardCollisionAsync(conn, tx, model.Id);
            await InsertSymbolRowWithIdAsync(conn, tx, model);
            await conn.ExecuteAsync(
                "INSERT INTO MilitarySymbols (SymbolId, Affiliation, BattleDimension, StandardIdentity, UnitType, UnitSize, UnitDesignator, HigherFormation, CallSign, CountryCode) VALUES (@SymbolId, @Affiliation, @BattleDimension, @StandardIdentity, @UnitType, @UnitSize, @UnitDesignator, @HigherFormation, @CallSign, @CountryCode);",
                new { SymbolId = model.Id, Affiliation = model.Affiliation.ToString(), BattleDimension = model.BattleDimension.ToString(), StandardIdentity = model.StandardIdentity.ToString(), UnitType = model.UnitType.ToString(), UnitSize = model.UnitSize.ToString(), model.UnitDesignator, model.HigherFormation, model.CallSign, model.CountryCode }, tx);
            await tx.CommitAsync(token);
            await ResetSymbolsAutoIncrementAsync(conn);
            return model.Id;
        }
        catch { await tx.RollbackAsync(token); throw; }
    }

    public async Task<int> RestoreLineSymbolAsync(ILineSymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var tx = await conn.BeginTransactionAsync(token);
        try
        {
            await GuardCollisionAsync(conn, tx, model.Id);
            await InsertSymbolRowWithIdAsync(conn, tx, model);
            await conn.ExecuteAsync(
                "INSERT INTO LineSymbols (SymbolId, LineOpacity, IsClosedPath, ShowArrowHead, LinePattern) VALUES (@SymbolId, @LineOpacity, @IsClosedPath, @ShowArrowHead, @LinePattern);",
                new { SymbolId = model.Id, model.LineOpacity, model.IsClosedPath, model.ShowArrowHead, LinePattern = model.LinePattern.ToString() }, tx);
            if (model.LinePoints?.Any() == true)
            {
                var pts = model.LinePoints.Select((p, i) => new { LineSymbolId = model.Id, SequenceOrder = i, Latitude = (decimal)p.Latitude, Longitude = (decimal)p.Longitude, Altitude = (float)p.Altitude });
                await conn.ExecuteAsync("INSERT INTO LinePoints (LineSymbolId, SequenceOrder, Latitude, Longitude, Altitude) VALUES (@LineSymbolId, @SequenceOrder, @Latitude, @Longitude, @Altitude);", pts, tx);
            }
            await tx.CommitAsync(token);
            await ResetSymbolsAutoIncrementAsync(conn);
            return model.Id;
        }
        catch { await tx.RollbackAsync(token); throw; }
    }

    public async Task<int> RestoreInfraSymbolAsync(IInfraSymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var tx = await conn.BeginTransactionAsync(token);
        try
        {
            await GuardCollisionAsync(conn, tx, model.Id);
            await InsertSymbolRowWithIdAsync(conn, tx, model);
            await conn.ExecuteAsync(
                "INSERT INTO InfraSymbols (SymbolId, BuildingType, BuildingUsage, FloorCount, BasementFloorCount, BuildingArea) VALUES (@SymbolId, @BuildingType, @BuildingUsage, @FloorCount, @BasementFloorCount, @BuildingArea);",
                new { SymbolId = model.Id, BuildingType = model.BuildingType.ToString(), BuildingUsage = model.BuildingUsage.ToString(), model.FloorCount, model.BasementFloorCount, model.BuildingArea }, tx);
            await tx.CommitAsync(token);
            await ResetSymbolsAutoIncrementAsync(conn);
            return model.Id;
        }
        catch { await tx.RollbackAsync(token); throw; }
    }

    public async Task<int> RestorePidsGroupSymbolAsync(IPidsGroupSymbolModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        await using var tx = await conn.BeginTransactionAsync(token);
        try
        {
            await GuardCollisionAsync(conn, tx, model.Id);
            await InsertSymbolRowWithIdAsync(conn, tx, model);
            await conn.ExecuteAsync(
                "INSERT INTO PidsGroupSymbols (SymbolId, LinkedDeviceGroup, EventStatus, LineOpacity, IsClosedPath, ShowArrowHead, LinePattern) VALUES (@SymbolId, @LinkedDeviceGroup, @EventStatus, @LineOpacity, @IsClosedPath, @ShowArrowHead, @LinePattern);",
                new { SymbolId = model.Id, model.LinkedDeviceGroup, EventStatus = model.EventStatus.ToString(), model.LineOpacity, model.IsClosedPath, model.ShowArrowHead, LinePattern = model.LinePattern.ToString() }, tx);
            if (model.LinePoints?.Any() == true)
            {
                var pts = model.LinePoints.Select((p, i) => new { GroupSymbolId = model.Id, SequenceOrder = i, Latitude = (decimal)p.Latitude, Longitude = (decimal)p.Longitude, Altitude = (float)p.Altitude });
                await conn.ExecuteAsync("INSERT INTO PidsGroupPoints (GroupSymbolId, SequenceOrder, Latitude, Longitude, Altitude) VALUES (@GroupSymbolId, @SequenceOrder, @Latitude, @Longitude, @Altitude);", pts, tx);
            }
            await tx.CommitAsync(token);
            await ResetSymbolsAutoIncrementAsync(conn);
            return model.Id;
        }
        catch { await tx.RollbackAsync(token); throw; }
    }

    public async Task<int> RestoreImageAsync(IImageModel model, CancellationToken token = default)
    {
        await using var conn = await OpenConnectionAsync(token);
        if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Images WHERE Id = @Id;", new { model.Id }) > 0)
            throw new IdCollisionException(model.Id);
        const string sql = @"
            INSERT INTO Images
                (Id, Title, FilePath, `Left`, Top, `Right`, Bottom, Latitude, Longitude, Altitude, Zoom, Width, Height,
                 Opacity, Rotation, Visibility, IsLocked, HasGeoReference, CoordinateSystem)
            VALUES (@Id, @Title, @FilePath, @Left, @Top, @Right, @Bottom, @Latitude, @Longitude, @Altitude, @Zoom, @Width, @Height,
                    @Opacity, @Rotation, @Visibility, @IsLocked, @HasGeoReference, @CoordinateSystem);";
        await conn.ExecuteAsync(sql, new
        {
            model.Id, model.Title, model.FilePath, model.Left, model.Top, model.Right, model.Bottom,
            model.Latitude, model.Longitude, model.Altitude, model.Zoom, model.Width, model.Height,
            model.Opacity, model.Rotation, model.Visibility, model.IsLocked, model.HasGeoReference, model.CoordinateSystem
        });
        try
        {
            var next = await conn.ExecuteScalarAsync<int>("SELECT COALESCE(MAX(Id),0)+1 FROM Images;");
            await conn.ExecuteAsync($"ALTER TABLE Images AUTO_INCREMENT = {next};");
        }
        catch (Exception ex) { _log?.Warning($"Images AUTO_INCREMENT 리셋 실패(무시): {ex.Message}"); }
        _log?.Info($"Image 복원 완료 - Id={model.Id}");
        return model.Id;
    }
}
