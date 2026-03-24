# PRD: 레이어 패널 트리 구조 재설계

- **Version**: 1.0
- **Date**: 2026-03-19
- **Status**: Draft
- **Language/Framework**: C# / WPF (.NET 8) + Caliburn.Micro + MaterialDesign
- **선행 PRD**: PRD_Layer_Management_System (DB CRUD 완료, UI 미완성)

## 1. Background (배경)

### 현재 상황

PRD_Layer_Management_System에서 DB 스키마/CRUD는 완성되었으나, **레이어 패널 UI의 트리 구조가 올바르게 작동하지 않는다.**

현재 문제점:

1. **트리 계층 구조 미반영** — Section(지도/이미지/심볼) 아래 카테고리→아이템 계층이 시각적으로 깨짐
2. **이벤트 이중 발생** — Group 체크 시 자식 바인딩 + Group 핸들러에서 동일 이벤트 2번 발생
3. **Zoom LOD 무시** — 레이어 ON 시 `Visible`로 강제 설정하여 마커의 Zoom 조건을 덮어씀
4. **아이템 개수 0** — 카테고리별 실제 마커 개수 미반영
5. **빈 카테고리 처리 없음** — 아이템 없는 카테고리도 동일하게 표시

### 동기

- GOP 요구사항: 맵 위 심볼/오버레이를 카테고리별로 ON/OFF 제어
- 카테고리는 **개념(폴더)**이고, 그 안에 **세부 아이템(인스턴스)**이 들어감
- 비어있는 카테고리는 헤더만 표시 (접기 가능)
- Zoom LOD와 레이어 ON/OFF가 AND 조건으로 동작해야 함

## 2. Goals (목표)

### 핵심 목표

- [ ] 카테고리 중심 3-Tier 트리 구조 완성 (Section → Category → Item)
- [ ] 3-state 체크박스 정상 동작 (부모↔자식, indeterminate)
- [ ] 이벤트 이중 발생 버그 수정
- [ ] Zoom LOD AND 조건 통합
- [ ] 컨셉 디자인(블루 헤더 카테고리 바 + 화이트 아이템) 매칭

### 비목표 (Out of Scope)

- DB 스키마 변경 (기존 MapLayers 테이블 유지)
- 레이어 드래그앤드롭 순서 변경
- 오버레이 맵/이미지 추가 UI (아직 V-World MBTiles 미완성)

## 3. Requirements (요구사항)

### 기능 요구사항

| ID | 요구사항 | 우선순위 | 비고 |
|----|---------|---------|------|
| TR-01 | 카테고리 = 블루 라운드 헤더 바, 접기/펼치기 + 3-state 체크 | Must | 컨셉 디자인 |
| TR-02 | 아이템 = 카테고리 아래 들여쓰기, 체크 + 아이콘 + 이름 | Must | |
| TR-03 | 빈 카테고리는 헤더만 표시 (세부 아이템 영역 없음) | Must | |
| TR-04 | 부모(카테고리) 체크 → 자식 전체 ON/OFF | Must | |
| TR-05 | 자식 일부 OFF → 부모 indeterminate (◾) | Must | |
| TR-06 | 이벤트 이중 발생 수정 — Leaf만 이벤트, Group은 자식 위임 | Must | 버그 수정 |
| TR-07 | Zoom AND 조건: `visible = layerON && (currentZoom >= marker.Zoom)` | Must | |
| TR-08 | 카테고리별 마커 개수 배지 표시 (런타임 업데이트) | Should | |
| TR-09 | Section(지도/이미지/심볼)은 접기/펼치기 가능 | Should | |

### 비기능 요구사항

- 성능: 마커 500개 이하에서 레이어 토글 100ms 이내
- 호환성: 기존 MapLayers DB 데이터와 하위 호환

## 4. Technical Approach (기술 접근)

### 트리 구조 정의

```
LayerPanelControl
├── 지도 (Section, 블루 바, 접기 가능)
│   ├── [카테고리: 오버레이 맵] (있을 때만)
│   │   ├── 군사지도_A구역 (Leaf, Opacity 슬라이더)
│   │   └── 작전지도_B구역 (Leaf, Opacity 슬라이더)
│   └── (비어있으면 "등록된 지도가 없습니다" 표시)
│
├── 이미지 (Section, 블루 바, 접기 가능)
│   ├── [이미지 아이템들] (있을 때만)
│   └── (비어있으면 "등록된 이미지가 없습니다" 표시)
│
└── 심볼 (Section, 블루 바, 접기 가능)
    ├── PIDS 장비 (Category, 블루 라운드 바, 접기, 3-state 체크)
    │   ├── 카메라 (Leaf, 체크 + 아이콘 + 이름 + 개수)
    │   ├── 센서 (Leaf)
    │   ├── 스피커 (Leaf)
    │   ├── 컨트롤러 (Leaf)
    │   ├── 조명 (Leaf)
    │   └── 함체 (Leaf)
    ├── PIDS 그룹 (Leaf, 독립)
    ├── 군사 심볼 (Leaf, 독립)
    ├── 기하학 도형 (Leaf, 독립)
    ├── 선/경계 (Leaf, 독립)
    └── 인프라/시설 (Leaf, 독립)
```

### 영향받는 컴포넌트

| 컴포넌트 | 변경 유형 | 설명 |
|----------|----------|------|
| `LayerTreeNode.cs` | 수정 | Section 접기 지원 추가 |
| `LayerTreeBuilder.cs` | 수정 | 빈 카테고리 처리, Section 접기, 카테고리 바 스타일 구분 |
| `LayerPanelControl.cs` | 수정 | 이벤트 이중 발생 수정 (Group 핸들러 제거) |
| `LayerPanelStyle.xaml` | 재작성 | 컨셉 디자인 매칭, Section/Category/Leaf 3단계 템플릿 |
| `MapViewModel.cs` | 수정 | Zoom AND 조건 통합, 마커 개수 업데이트 |

### 이벤트 흐름 수정

```
[현재 — 이중 발생]
Group 체크 → 자식 IsChecked 바인딩 변경 → Leaf CheckedEvent → 이벤트 1
           → Group CheckedEvent → 핸들러에서 자식 순회 → 이벤트 2 (중복!)

[수정 후 — Leaf만 이벤트]
Group 체크 → 자식 IsChecked 바인딩 변경 → Leaf CheckedEvent → 이벤트 (1번만)
           → Group CheckedEvent → 아무것도 안 함 (자식이 처리)
```

### Zoom AND 조건

```csharp
// ApplyLayerVisibility 수정
if (!layer.IsVisible)
{
    marker.Shape.Visibility = Collapsed;  // 레이어 OFF → 무조건
}
else
{
    // 레이어 ON → Zoom 조건도 확인
    int markerZoom = (marker.Tag as GMapBaseMarker)?.Zoom ?? 0;
    bool zoomOk = MainMap.Zoom >= markerZoom;
    marker.Shape.Visibility = zoomOk ? Visible : Collapsed;
}
```

**줌 변경 시에도 레이어 상태를 확인해야 함:**
```csharp
// OnMapZoomChanged 이벤트에서
foreach (var leaf in LayerTreeBuilder.Flatten(_layerTreeNodes))
{
    if (leaf.Model != null)
        ApplyLayerVisibility(leaf.Model);  // 레이어 + Zoom AND 조건 재평가
}
```

### 이미지 레이어 모델 계층화

**기존 데이터 소스:**
```
DB: Images 테이블 (IGMapDbSymbolService.FetchImagesAsync)
모델: IImageModel (FilePath, Latitude, Longitude, Opacity, Visibility, Zoom, Title)
마커: GMapImageMarker → GMapControl.Markers
서비스: ImageOverlayService (TIF→오버레이, 활성 오버레이 관리)
```

**레이어 트리 연결:**
```
이미지 (Section)
├── 군사지도_A.tif (Leaf) ← IImageModel에서 생성
├── 드론촬영_B.png (Leaf) ← IImageModel에서 생성
└── (없으면 "등록된 이미지가 없습니다")

연결 경로:
IImageModel (DB) → LayerTreeNode (Leaf, HasOpacitySlider=true)
                  → GMapImageMarker (맵 마커)
                  → Shape.Visibility + Shape.Opacity 제어
```

**이미지 레이어 트리 빌드:**
```csharp
// LayerTreeBuilder에서 이미지 Section 빌드 시
var images = await _symbolDbService.FetchImagesAsync();
foreach (var img in images)
{
    // IImageModel → LayerTreeNode 변환
    var node = new LayerTreeNode
    {
        Name = img.Title ?? Path.GetFileName(img.FilePath),
        IsChecked = img.Visibility,
        Opacity = img.Opacity,
        HasOpacitySlider = true,
        NodeType = LayerNodeType.Leaf,
        // Model은 IMapLayerModel이 아닌 IImageModel 참조 필요
    };
    imageSection.AddChild(node);
}
```

**설계 결정 — IImageModel ↔ MapLayers 동기화:**
- 이미지가 맵에 추가될 때 `MapLayers` 테이블에 `LayerType="OverlayImage"` 행 자동 생성
- `MapLayers.FilePath`에 이미지 경로 저장 → IImageModel과 매핑 키
- 레이어 ON/OFF → `IImageModel.Visibility` + `MapLayers.IsVisible` 동기화
- Opacity 변경 → `IImageModel.Opacity` + `MapLayers.Opacity` 동기화
- 이미지 삭제 → `MapLayers` 행도 자동 삭제

### 심볼 레이어 모델 계층화

**기존 데이터 소스:**
```
DB: Symbols, PidsSymbols, GeometrySymbols, MilitarySymbols, LineSymbols, InfraSymbols, PidsGroupSymbols
마커: GMapPidsMarker, GMapGeometricMarker, GMapMilitarySymbolMarker, GMapLineMarker, GMapInfraMarker, GMapPidsGroupMarker
프로바이더: PidsSymbolProvider, GeometricSymbolProvider, MilitarySymbolProvider 등
```

**레이어 트리 연결:**
```
심볼 (Section)
├── PIDS 장비 (Category/Group, 3-state 체크)
│   ├── 카메라     ← MapLayers(Category="PidsCamera") ↔ GMapPidsMarker(DeviceType=IpCamera)
│   ├── 센서       ← MapLayers(Category="PidsSensor") ↔ GMapPidsMarker(DeviceType=SmartSensor/PIR/Fence/...)
│   ├── 스피커     ← MapLayers(Category="PidsSpeaker") ↔ GMapPidsMarker(DeviceType=IpSpeaker)
│   ├── 컨트롤러   ← MapLayers(Category="PidsController") ↔ GMapPidsMarker(DeviceType=Controller)
│   ├── 조명       ← MapLayers(Category="PidsLamp") ↔ GMapPidsMarker(DeviceType=Lamp)
│   └── 함체       ← MapLayers(Category="PidsEnclosure") ↔ GMapPidsMarker(DeviceType=Enclosure)
├── PIDS 그룹      ← MapLayers(Category="PidsGroup") ↔ GMapPidsGroupMarker
├── 군사 심볼       ← MapLayers(Category="Military") ↔ GMapMilitarySymbolMarker
├── 기하학 도형     ← MapLayers(Category="Geometric") ↔ GMapGeometricMarker
├── 선/경계        ← MapLayers(Category="Line") ↔ GMapLineMarker
└── 인프라/시설     ← MapLayers(Category="Infra") ↔ GMapInfraMarker

연결 경로:
MapLayers (DB, Category) → LayerTreeNode (Leaf)
                         → MatchMarkerToCategory(marker, category)
                         → marker.Shape.Visibility 제어
                         → AND 조건: layerON && (currentZoom >= marker.Zoom)
```

**심볼 마커 개수 집계:**
```csharp
// 각 Leaf 노드의 ItemCount를 런타임에 업데이트
foreach (var leaf in LayerTreeBuilder.Flatten(treeNodes))
{
    leaf.ItemCount = MainMap.Markers.Count(m => MatchMarkerToCategory(m, leaf.Category));
}
// Group 노드의 ItemCount = 자식 ItemCount 합계
pidsGroup.ItemCount = pidsGroup.Children.Sum(c => c.ItemCount);
```

### 지도 레이어 (이번 스텝 제외)

> V-World MBTiles 구축 완료 후 별도 PRD에서 처리.
> 현재는 Section 헤더만 표시하고 "준비 중" 메시지 표시.

### 의존성

- 기존 `MapLayers` DB 테이블 (변경 없음)
- `Images` DB 테이블 (IGMapDbSymbolService, 읽기 전용)
- `GMapBaseMarker<T>.Zoom` 프로퍼티 (읽기 전용 참조)
- `GMapControl.Zoom` 현재 줌 레벨
- `ImageOverlayService.ActiveOverlays` (이미지 오버레이 참조)

## 5. Test Strategy (테스트 전략)

### 단위 테스트

| 테스트 | 검증 내용 |
|--------|----------|
| `TreeBuilder_Build_CreatesCorrectHierarchy` | 3-Section, PIDS 그룹 6개 자식, 독립 5개 |
| `TreeNode_ParentCheck_PropagatesToChildren` | 부모 OFF → 자식 전부 OFF |
| `TreeNode_ChildMixed_ParentIndeterminate` | 자식 일부 OFF → 부모 null |
| `TreeBuilder_EmptyCategory_NoChildren` | OverlayMap 0개 → Section만, 자식 없음 |
| `ApplyVisibility_LayerOff_AlwaysCollapsed` | 레이어 OFF → Zoom 무관 숨김 |
| `ApplyVisibility_LayerOn_ZoomCheck` | 레이어 ON + 줌 < 마커Zoom → 숨김 |
| `ApplyVisibility_LayerOn_ZoomOk` | 레이어 ON + 줌 ≥ 마커Zoom → 표시 |

### 검증 기준

- [ ] 모든 단위 테스트 통과
- [ ] 빌드 경고 0개 (nullable 제외)
- [ ] 이벤트 이중 발생 없음 확인
- [ ] 줌 변경 시 레이어 OFF 카테고리는 표시되지 않음

## 6. Risks & Mitigations (리스크)

| 리스크 | 영향 | 완화 방안 |
|--------|------|----------|
| Zoom 변경마다 전체 마커 순회 성능 | Medium | 카테고리별 마커 캐시 Dictionary 사용 |
| CheckBox 3-state WPF 바인딩 복잡성 | Medium | LayerTreeNode에서 전파 로직 완전 처리 |
| 기존 Zoom 로직과 충돌 | High | GMapBaseMarker 내부 줌 로직 확인 후 레이어에서 통합 |

## 7. References (참고)

- [PRD_Layer_Management_System.md](PRD_Layer_Management_System.md) — DB CRUD (완성)
- [PRD_Layer_Management_System.plan.md](PRD_Layer_Management_System.plan.md) — 이전 Plan (15/16)
- [layer_panel_concept.html](../design/layer_panel_concept.html) — 컨셉 디자인
- [project_map_layer_architecture.md](../../.claude/projects/.../memory/project_map_layer_architecture.md) — 레이어 아키텍처 메모리
