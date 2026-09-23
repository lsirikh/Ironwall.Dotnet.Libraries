using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Newtonsoft.Json.Linq;

namespace LiveApiRoundTrip;

public static partial class Steps
{
    // ================= item 8: N-08 unit create/delete round trip =================
    // Item7_UnitScope (Steps.Core.cs) only ever READS the pre-seeded unit001 to prove
    // client-side scope stamping. It never proves UnitApiService's own write path. This
    // item closes that gap: create a throwaway unit through the REAL service, prove the
    // server actually has it, then delete it through the REAL service and prove it is gone.
    public static async Task Item8_Unit(Bootstrap boot, Recorder rec, Raw raw, IUnitApiService unitApi)
    {
        const string TAG = "N-08";
        int unitId = 0;
        try
        {
            boot.Wire.CurrentTag = TAG + "/create";
            var before = rec.LastSeq();
            var createDto = new UnitCreateDto
            {
                Name = "LRT-N08-UNIT",
                Code = "lrt08unit",
                IsEnable = true,
                // Echelon left at its default (Outpost) - the leaf echelon, so no parent is required.
            };
            var created = await unitApi.CreateUnitAsync(createDto).ConfigureAwait(false);
            if (!created.Success || created.Data == null)
            {
                rec.Add("8.0", TAG, "arrange: UnitApiService.CreateUnitAsync", Verdict.BLOCKED,
                    $"success={created.Success} msg='{created.Message}'", blocked: "fixture setup failed");
                return;
            }
            unitId = created.Data.Id;
            rec.Created("unit", unitId);

            var (status, jo) = await raw.Get($"units/{unitId}").ConfigureAwait(false);
            var gotName = Str(Obj(jo["data"])?["name"]);
            var gotCode = Str(Obj(jo["data"])?["code"]);
            var createOk = status == 200 && gotName == "LRT-N08-UNIT" && gotCode == "lrt08unit";
            rec.Add("8a", TAG, "UnitApiService.CreateUnitAsync 로 만든 부대가 서버에 실제로 존재",
                createOk ? Verdict.PASS : Verdict.FAIL,
                $"HTTP {status}; server name={gotName} code={gotCode}",
                defectAt: createOk ? "" : "Devices.Api/Services/UnitApiService.cs CreateUnitAsync",
                seqs: SeqsSince(rec, before));

            // ---------- 8b: delete through the real service, prove it is really gone ----------
            boot.Wire.CurrentTag = TAG + "/delete";
            before = rec.LastSeq();
            var deleted = await unitApi.DeleteUnitAsync(unitId).ConfigureAwait(false);
            var (delStatus, _) = await raw.Get($"units/{unitId}").ConfigureAwait(false);
            var deleteOk = deleted.Success && delStatus == 404;
            rec.Add("8b", TAG, "UnitApiService.DeleteUnitAsync 로 삭제 후 재조회하면 404",
                deleteOk ? Verdict.PASS : Verdict.FAIL,
                $"delete success={deleted.Success} msg='{deleted.Message}'; re-GET HTTP {delStatus}",
                defectAt: deleteOk ? "" : "Devices.Api/Services/UnitApiService.cs DeleteUnitAsync",
                seqs: SeqsSince(rec, before));
            if (deleteOk) { rec.Deleted("unit", unitId); unitId = 0; }
        }
        catch (Exception ex)
        {
            rec.Add("8!", TAG, "unit 생성/삭제 왕복", Verdict.BLOCKED, ex.Message, blocked: "harness exception");
        }
        finally
        {
            // Safety net only - normal path already deletes+zeroes unitId above via the real service.
            if (unitId > 0)
            {
                var (s, _) = await raw.Delete($"units/{unitId}").ConfigureAwait(false);
                if (s == 200 || s == 204) rec.Deleted("unit", unitId); else rec.Leftover("unit", unitId, $"DELETE {s}");
            }
        }
    }
}
