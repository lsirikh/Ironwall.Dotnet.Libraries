using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Defines.Apis;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DeviceConsolePreview;

/// <summary>
/// 부대 콘솔 미리보기(N-11) — <b>진짜 뷰 + 진짜 뷰모델</b>을 가짜 창구 위에 띄운다(호스트 앱 · 서버 없음).
/// </summary>
/// <remarks>서버에 단 한 줄도 나가지 않는다 — 여기의 창구는 전부 메모리에서 답한다.</remarks>
internal sealed class UnitsPreview
{
    private readonly FakeUnits _units = new();
    private readonly FakeDevices _devices = new();

    public UnitConsoleViewModel Build(bool legacy = false)
    {
        _units.IsAvailable = !legacy;
        _devices.IsAvailable = !legacy;
        return new UnitConsoleViewModel(_units, _devices, log: null, myUnitCode: () => "c0206",
                                        canEdit: () => true, canDelete: () => true);
    }

    /// <summary>다음 쓰기를 거절하게 만든다 — 실패 한 줄이 어떻게 보이는지 찍기 위한 스위치.</summary>
    public void MakeNextWriteFail() => _units.FailNext = true;

    /// <summary>다음 삭제를 409 로 막는다(화면 J).</summary>
    public void MakeDeleteBlocked() => _units.BlockDelete = true;

    #region - Fakes -
    private sealed class FakeUnits : IUnitGraphApi
    {
        private readonly UnitGraphDto _graph = Seed();

        public bool IsAvailable { get; set; } = true;
        public bool FailNext { get; set; }
        public bool BlockDelete { get; set; }

        public Task<ApiResponse<UnitGraphDto>> GetGraphAsync(CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitGraphDto>.CreateSuccess(_graph));

        public Task<ApiResponse<UnitDetailDto>> GetDetailAsync(int unitId, CancellationToken token = default)
        {
            var node = _graph.Nodes.First(n => n.Id == unitId);
            var adjacent = _graph.Edges.AdjacencyPairs
                                 .Where(p => p.Low == unitId || p.High == unitId)
                                 .Select(p => p.Low == unitId ? p.High : p.Low)
                                 .ToList();

            return Task.FromResult(ApiResponse<UnitDetailDto>.CreateSuccess(new UnitDetailDto
            {
                Id = node.Id,
                Code = node.Code,
                Name = node.Name,
                EchelonRaw = node.EchelonRaw,
                ParentId = node.ParentId,
                IsEnable = node.IsEnable,
                Description = unitId == 6 ? "북측 9~12구간 담당" : null,
                AdjacentUnitIds = adjacent,
                Adjacent = adjacent.Select(id => _graph.Nodes.First(n => n.Id == id)).ToList(),
                Children = _graph.Edges.HierarchyPairs.Where(e => e.Parent == unitId)
                                 .Select(e => _graph.Nodes.First(n => n.Id == e.Child)).ToList(),
            }));
        }

        public Task<ApiResponse<UnitDto>> CreateAsync(UnitCreateDto dto, CancellationToken token = default)
            => Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = 90, Code = dto.Code, Name = dto.Name }));

        public Task<ApiResponse<UnitDto>> PatchAsync(int unitId, UnitUpdateDto dto, CancellationToken token = default)
        {
            if (FailNext)
            {
                FailNext = false;
                return Task.FromResult(ApiResponse<UnitDto>.CreateError(
                    ApiErrorCodes.ValidationError, "상위 부대의 제대가 하위 부대보다 낮습니다(parent echelon must be higher)."));
            }

            var node = _graph.Nodes.FirstOrDefault(n => n.Id == unitId);
            if (node is not null && dto.IsParentIdSpecified)
            {
                node.ParentId = dto.ParentId;
                _graph.Edges.Hierarchy.RemoveAll(e => e.Count == 2 && e[1] == unitId);
                if (dto.ParentId is int parent) _graph.Edges.Hierarchy.Add(new List<int> { parent, unitId });
            }
            if (node is not null && dto.IsEnable is bool enable) node.IsEnable = enable;
            if (node is not null && dto.Name is not null) node.Name = dto.Name;

            return Task.FromResult(ApiResponse<UnitDto>.CreateSuccess(new UnitDto { Id = unitId }));
        }

        public Task<ApiResponse<UnitDeleteResultDto>> DeleteAsync(int unitId, CancellationToken token = default)
        {
            if (!BlockDelete) return Task.FromResult(ApiResponse<UnitDeleteResultDto>.CreateSuccess(new UnitDeleteResultDto { Id = unitId }));

            BlockDelete = false;
            var response = ApiResponse<UnitDeleteResultDto>.CreateError(ApiErrorCodes.Conflict, "부대에 매달린 것이 있습니다.");
            response.Error!.DetailsToken = JObject.Parse("""{"counts":{"devices":18,"device_groups":2,"events":431}}""");
            return Task.FromResult(response);
        }

        private static UnitGraphDto Seed()
        {
            UnitListDto Node(int id, string code, string name, string echelon, int? parentId, bool isEnable = true)
                => new() { Id = id, Code = code, Name = name, EchelonRaw = echelon, ParentId = parentId, IsEnable = isEnable };

            return new UnitGraphDto
            {
                Nodes = new List<UnitListDto>
                {
                    Node(1, "d0001", "제○○사단", "Division", null),
                    Node(2, "r0101", "1연대", "Regiment", 1),
                    Node(3, "b0102", "2대대", "Battalion", 2),
                    Node(5, "c0205", "5중대", "Company", 3),
                    Node(6, "c0206", "6중대", "Company", 3),
                    Node(7, "c0207", "7중대", "Company", 3),
                    Node(8, "o0203", "3소초", "Outpost", 7),
                    Node(9, "o0204", "4소초", "Outpost", 6),
                    Node(10, "b0103", "3대대", "Battalion", 2, isEnable: false),
                },
                Edges = new UnitGraphEdgesDto
                {
                    Hierarchy = new List<List<int>>
                    {
                        new() { 1, 2 }, new() { 2, 3 }, new() { 3, 5 }, new() { 3, 6 },
                        new() { 3, 7 }, new() { 7, 8 }, new() { 6, 9 }, new() { 2, 10 },
                    },
                    Adjacency = new List<List<int>> { new() { 5, 6 }, new() { 6, 7 } },
                },
            };
        }
    }

    private sealed class FakeDevices : IUnitDeviceApi
    {
        private readonly List<UnitDeviceItem> _items = new()
        {
            new(101, 1, "정문 PTZ", EnumDeviceCategory.Camera, 6),
            new(102, 2, "후문 고정", EnumDeviceCategory.Camera, 6),
            new(103, 3, "측면 스피드돔", EnumDeviceCategory.Camera, 5),
            new(104, 4, "9구간 제어기", EnumDeviceCategory.Controller, 6),
            new(105, 5, "신규 함체 A", EnumDeviceCategory.Enclosure, null),
            new(106, 6, "신규 함체 B", EnumDeviceCategory.Enclosure, null),
            new(107, 7, "신규 통문", EnumDeviceCategory.Gate, null),
            new(108, 8, "예비 경광등", EnumDeviceCategory.Lamp, 777),      // 편제에 없는 부대
        };

        public bool IsAvailable { get; set; } = true;

        public Task<UnitDeviceLoadResult> LoadAllAsync(CancellationToken token = default)
            => Task.FromResult(new UnitDeviceLoadResult(_items.ToList(), System.Array.Empty<string>()));

        public Task<UnitDeviceAssignResult> AssignAsync(UnitDeviceItem device, int unitId, CancellationToken token = default)
            => Task.FromResult(new UnitDeviceAssignResult(true, "바꿨습니다"));
    }
    #endregion
}
