# TDD Plan: 레이어 패널 트리 구조 재설계

- **PRD**: Docs/prd/PRD_Layer_Panel_Tree_Redesign.md
- **Date**: 2026-03-19
- **Status**: In Progress

---

## Phase 1: 트리 로직 단위 테스트 (Behavioral)

> LayerTreeNode 3-state 체크 + LayerTreeBuilder 계층 빌드 검증
> TR-04, TR-05, TR-03

- [x] **Test 1.1**: TreeNode_ParentCheck_PropagatesToChildren — 부모 OFF → 자식 전부 OFF
  - File: `GMaps.Ui/Tests/LayerTreeNodeTests.cs`
  - Target: `GMaps.Ui/Models/LayerTreeNode.cs`
  - Red: Group 노드 IsChecked=false → 자식 6개 전부 false 검증
  - Green: 기존 IsChecked setter 전파 로직 (이미 구현됨, 테스트만 추가)

- [x] **Test 1.2**: TreeNode_ChildMixed_ParentIndeterminate — 자식 일부 OFF → 부모 null
  - File: `GMaps.Ui/Tests/LayerTreeNodeTests.cs`
  - Red: 6개 자식 중 2개만 false → 부모 IsChecked == null 검증
  - Green: UpdateCheckStateFromChildren (이미 구현됨)

- [x] **Test 1.3**: TreeBuilder_Build_CreatesCorrectHierarchy — 3-Section 트리 구조 검증
  - File: `GMaps.Ui/Tests/LayerTreeBuilderTests.cs`
  - Target: `GMaps.Ui/Models/LayerTreeBuilder.cs`
  - Red: DB 더미 데이터 11개 → Build() → Section 3개, PIDS그룹 6자식, 독립 5개
  - Green: 기존 Build 메서드 검증

- [x] **Test 1.4**: TreeBuilder_EmptyCategory_NoChildren — 빈 Section은 자식 없음
  - File: `GMaps.Ui/Tests/LayerTreeBuilderTests.cs`
  - Red: OverlayMap 0개 → Build() → Section[0].Children.Count == 0
  - Green: 기존 로직 검증

- [x] **Test 1.5**: TreeBuilder_ImageSection_FromImageModels — 이미지 모델에서 트리 빌드
  - File: `GMaps.Ui/Tests/LayerTreeBuilderTests.cs`
  - Target: `GMaps.Ui/Models/LayerTreeBuilder.cs`
  - Red: IImageModel 더미 2개 → BuildImageSection() → Leaf 2개, HasOpacitySlider=true
  - Green: LayerTreeBuilder에 BuildImageSection(IEnumerable<IImageModel>) 추가
  - 빌드 확인

---

## Phase 2: 이벤트 이중 발생 수정 (Behavioral — 버그 수정)

> TR-06

- [x] **2.1**: LayerPanelControl — Group 핸들러에서 자식 순회 제거
  - Target: `GMaps.Ui/GMapControls/LayerPanelControl.cs`
  - 구현: `OnTreeCheckChanged`에서 `node.NodeType == Group` 블록 제거
  - 이유: 자식 CheckBox 바인딩이 IsChecked 변경 시 자동으로 CheckedEvent 발생 → Leaf 핸들러가 처리
  - 빌드 확인

---

## Phase 3: Zoom AND 조건 통합 (Behavioral)

> TR-07

- [x] **Test 3.1**: ApplyVisibility_LayerOff_AlwaysCollapsed — 레이어 OFF → Zoom 무관 숨김
  - File: `GMaps.Ui/Tests/LayerVisibilityTests.cs`
  - Target: `GMaps.Ui/ViewModels/Maps/MapViewModel.cs` (ApplyLayerVisibility)
  - Red: layer.IsVisible=false, markerZoom=15, currentZoom=18 → Collapsed
  - Green: 기존 레이어 OFF 로직 (이미 구현됨)

- [x] **Test 3.2**: ApplyVisibility_LayerOn_ZoomBelow_Collapsed — 레이어 ON + 줌 부족 → 숨김
  - File: `GMaps.Ui/Tests/LayerVisibilityTests.cs`
  - Red: layer.IsVisible=true, markerZoom=18, currentZoom=16 → Collapsed
  - Green: ApplyLayerVisibility에서 Zoom 조건 추가

- [x] **Test 3.3**: ApplyVisibility_LayerOn_ZoomOk_Visible — 레이어 ON + 줌 충분 → 표시
  - File: `GMaps.Ui/Tests/LayerVisibilityTests.cs`
  - Red: layer.IsVisible=true, markerZoom=18, currentZoom=18 → Visible
  - Green: 동일 Zoom 조건 로직

- [x] **3.4**: MapViewModel — OnMapZoomChanged에서 레이어 재평가
  - Target: `GMaps.Ui/ViewModels/Maps/MapViewModel.cs`
  - 구현: 줌 변경 이벤트에서 `ApplyLayerVisibility(leaf.Model)` 전체 Leaf 순회
  - 빌드 확인

---

## Phase 4: LayerPanelStyle.xaml 재작성 (Structural)

> TR-01, TR-02, TR-03, TR-09 — 컨셉 디자인 매칭

- [x] **4.1**: Section 템플릿 — 블루 라운드 바 + Expander 접기/펼치기
  - Target: `GMaps.Ui/Themes/LayerPanelStyle.xaml`
  - 구현:
    - Section = Expander (블루 라운드 바 헤더, 접기 가능)
    - 빈 Section: "등록된 항목이 없습니다" TextBlock (회색 이탤릭)
    - 지도 Section: "준비 중" 메시지

- [x] **4.2**: Category(Group) 템플릿 — 블루 라운드 카드 + 3-state 체크 + 개수 배지
  - Target: `LayerPanelStyle.xaml`
  - 구현:
    - Expander 헤더: 블루 배경 CornerRadius=15, 체크(3-state) + 아이콘 + 이름 + 개수 배지
    - 자식 Leaf 들여쓰기 20px
    - 접기/펼치기 ▶/▼ 아이콘

- [x] **4.3**: Leaf(아이템) 템플릿 — 심볼용 + 오버레이용 분리
  - Target: `LayerPanelStyle.xaml`
  - 구현:
    - 심볼 Leaf: 체크 + 아이콘(14px) + 이름(12px) + 개수 배지
    - 오버레이 Leaf: 체크 + 이름 + Opacity 슬라이더 + 퍼센트
    - OFF 상태: 아이콘/텍스트 흐리게 (Foreground #AAA)
  - TextOptions: ClearType + Display
  - 빌드 확인

---

## Phase 5: MapViewModel 연동 (Behavioral)

> TR-08, 이미지 레이어 연결

- [x] **5.1**: MapViewModel — 마커 개수 집계 + ItemCount 업데이트
  - Target: `GMaps.Ui/ViewModels/Maps/MapViewModel.cs`
  - 구현:
    - `UpdateLayerItemCounts()` 메서드 추가
    - 각 Leaf: `leaf.ItemCount = MainMap.Markers.Count(m => MatchMarkerToCategory(m, leaf.Category))`
    - Group: `group.ItemCount = group.Children.Sum(c => c.ItemCount)`
    - 호출 시점: LoadLayersFromDbAsync 완료 후, 마커 추가/제거 시

- [x] **5.2**: MapViewModel — 이미지 레이어 트리 빌드 연동
  - Target: `GMaps.Ui/ViewModels/Maps/MapViewModel.cs` + `LayerTreeBuilder.cs`
  - 구현:
    - `LayerTreeBuilder.BuildImageSection(images)` 호출
    - ImageOverlayService.ActiveOverlays에서 이미지 목록 가져와 트리에 추가
    - 이미지 Leaf 체크 → GMapImageMarker.Shape.Visibility 제어
    - 이미지 Opacity 슬라이더 → GMapImageMarker.Shape.Opacity 제어

- [x] **5.3**: MapViewModel — 줌 변경 시 레이어+Zoom AND 재평가
  - Target: `GMaps.Ui/ViewModels/Maps/MapViewModel.cs`
  - 구현:
    - 기존 OnMapZoomChanged (또는 줌 변경 핸들러)에 레이어 재평가 추가
    - `foreach (var leaf in Flatten(_layerTreeNodes)) ApplyLayerVisibility(leaf.Model)`
  - 빌드 확인

---

## Phase 6: 최종 검증

- [x] **6.1**: 전체 빌드 확인 — 오류 0개
- [x] **6.2**: 기존 LayerTreeNode/Builder 테스트 + 신규 테스트 전부 통과
- [ ] **6.3**: UI 수동 검증
  - 레이어 패널: 3개 Section (지도=준비중, 이미지=목록/빈상태, 심볼=카테고리 트리)
  - PIDS 장비 그룹: 접기/펼치기 + 3-state 체크 (일부 OFF → ◾)
  - 카메라 OFF → 맵에서 카메라 마커 숨김
  - 줌 17로 이동 → 카메라(Zoom=18) 숨김, 레이어 ON이어도
  - 줌 18로 복귀 → 카메라 레이어 ON이면 다시 표시
  - 앱 재시작 → 이전 레이어 상태 유지

---

## 실행 순서

```
Phase 1 (트리 로직 테스트)
    ↓
Phase 2 (이벤트 버그 수정)
    ↓
Phase 3 (Zoom AND 조건)
    ↓
Phase 4 (XAML 재작성)
    ↓
Phase 5 (ViewModel 연동)
    ↓
Phase 6 (최종 검증)
```

**총 16개 체크박스 | 수정 5개 파일 + 신규 3개 테스트 파일**
