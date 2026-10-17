# 그룹 심볼 변환(전체 선택 → 회전/확대축소) — 시나리오 기반 분석 v2.0

- **작성일**: 2026-09-04 · **개정**: 2026-09-08(3D 심볼·틸트 도입 재검토) · **상태**: 분석 완료 → PRD v2.0
- **요구(사용자)**: "심볼을 전체 선택해서 전체 심볼을 회전. 맵 편집 모드일 때 드래그로 전체 선택 후 선택영역 중심으로 회전하는 커스텀 컨트롤. 회전 버튼 = 회전, 확대/축소 버튼 = 확대/축소. 단 확대 축소 시 뭔가 깨지면 안 됨(개별 이미지 소스라 깨질 것 같은데). 이게 가능하고 논리적으로 문제가 없는지 철저하게 시나리오와 시뮬레이션으로 검증."
- **산출물**: [시나리오 카탈로그](../tests/group-symbol-transform-scenarios.md) · [시뮬 전량 로그 5,144건](../tests/group-symbol-transform-simulation-log.md)
- **투입**: 영향면 정찰 7축(에이전트 7기, 1.4M 토큰) + 합성/완결성 비판 2기 + **적대 검증 4렌즈**(인용·수학·완결성·아키텍처) + 시뮬레이터 4세대(v1→v4)

---

## 0. 결론 요약

**기능은 구현 가능하다. 다만 "회전 버튼을 만든다"보다 훨씬 큰 작업이며, 착수 전에 정책 결정 15건이 필요하다.**

| 질문 | 답 |
|------|-----|
| 논리적으로 가능한가? | **가능**. 단 변환 수학을 **base 스냅샷 + 1회 합성**으로 해야 하고, 증분 방식은 720스텝에 523m 드리프트로 실패(SIM-A4381). |
| 확대 시 "깨지는가"? | **심볼은 안 깨진다** — 7계열 전부 Viewbox+Path **벡터**(CustomMarkerStyle.xaml:19). **오버레이 이미지만** 래스터라 원본 해상도 초과 시 블러(SIM-Q). 사용자 우려는 절반만 맞다. |
| 진짜 위험은? | ①**이미지 축소 시 단위 오판으로 대륙 크기 폭발**(ISSUE-29) ②부분 저장 시 화면/DB 영구 불일치 ③기존 그룹 속성창과의 의미 충돌 ④카메라 심볼 이동 시 실제 PTZ 조준 어긋남. |

---

## 1. 영향면 전수 (7축 정찰, 전부 file:line)

### ① 선택 모델
| 사실 | 근거 |
|------|------|
| 러버밴드는 `IsEditMode && Shift` 드래그에서만 시작, 3×3px 미만은 무시, 결과가 비어도 항상 통지 | GMapCustomControl.cs:1085, :1257 |
| 선택 필터: `IEditableMarker` ∧ ¬IsDisposed ∧ **¬IsLocked** ∧ 가시성 게이트 ∧ **이미지마커 제외** | :991-1002 |
| 가시성 게이트 = `ZoomLadder.IsVisibleAtEffectiveZoom(EffectiveZoom, marker.Zoom) && IsLayerEnabled` | :764 |
| 선택 SSOT = `GroupSelectionService.Selection`, 2개 이상에서만 어도너 부착, 1개는 단일 편집으로 강등 | GroupSelectionService.cs:49, :62 / MapViewModel.cs:1027 |
| 선택 키는 `(bool isImage, int id)` 튜플 — 리로드로 인스턴스가 바뀌어도 live 재해석 | MapViewModel.cs:1027 |
| 러버밴드는 교체가 아니라 **토글 병합** | :990 |
| **Ctrl+A는 죽은 코드** — 본문이 주석 처리 + `MultiSelectEnabled` 토글의 XAML 바인딩 0건 | GMapCustomControl.cs:2004-2008 / 전역 grep |
| 트래킹·트레일 마커는 `IEditableMarker` 미구현이라 타입으로 자동 배제 | GMapTrackingMarker.cs:26 |
| ⚠ **코드/주석 모순**: XML 주석은 "잠금 포함(FR-MS-07)", 구현은 잠금 제외 | :984 vs :1000 |

### ② 기하 모델 (계열별)
| 계열 | 위치 | 회전 | 크기 | 정점 영속 |
|------|------|------|------|----------|
| 점 심볼 7종 | Latitude/Longitude `double` | `Bearing double`(도) | `Width/Height double`(화면 px) | — |
| 라인 | 〃 | **없음**(정점이 형상) | — | **LinePoints** 테이블 |
| PidsGroup(구역) | 〃 | **없음** | — | **PidsGroupPoints** 테이블(별도!) |
| 이미지 오버레이 | AABB(Left/Top/Right/Bottom) + 중심 | `Rotation double` | Width/Height(원본 px) | — |

DB: `Latitude DECIMAL(10,8)` · `Longitude DECIMAL(11,8)` · `Bearing DECIMAL(6,3)` · `Width/Height DECIMAL(8,3)` (GMapDbSymbolService.cs:270-276), Images는 `DECIMAL(10,3)`/`Rotation DECIMAL(6,3)` (:467-470). 모델 타입은 전부 `double`(ISymbolModel.cs:10-16) — **정수 절단 우려는 반증됨**. 실제 정밀도 손실은 DP `Math.Round(x,2)`(GMapMarkerBaseControl.cs:834, GMapPropertyBaseControl.cs:1090)와 DB DECIMAL.

### ③ 기존 변환 자산 (재사용 가능)
- 그룹 이동: `GroupSelectionAdorner`(멤버별 점선 박스, base 스냅샷 `_origPositions`, dLat/dLng 평행이동) + `OnGroupMoveCompleted`(멤버별 DbUpdateProcess + `BeginBatch` 1매크로 Undo) — MapViewModel.cs:1201-1220
- 그룹 속성 일괄: `ApplyGroupPropertyChangeAsync`(전원 균일 적용 + 1매크로) — :8163-8192
- 좌표 유틸: `LineGeometryUtils.Scale`(픽셀 프레임 스케일, 0.05~20x 클램프) · `RotationMath.AabbOf/DisplayAngle` · `GMapCustomControl.RotateVector` · `ResizeBoundsWithRatio`(회전 불변 크기 산출) · **`SubPixelGeo.Bilinear`**(정수 절단 회피 선례, MarkerEditAdorner.cs:753-764)
- Undo: `BeginBatch` → `MacroCommand`, 단일대상 커맨드는 `isImage` 타입인지 필수(Id 충돌 이력)

### ④ 영속·동기
- 단일 진입점 `DbUpdateProcess`(MapViewModel.cs:7501) — 타입별 Update*Async + 비이미지는 라벨 스타일 1회 추가 = **심볼당 2~3 왕복**
- **멤버 간 트랜잭션 없음**(멤버별 개별 try/catch). 단, **심볼 1건 내부는 트랜잭션 있음**(GMapDbSymbolService.cs 12곳) → 결함 범위는 "멤버 간 원자성"으로 한정
- 라인/구역은 정점 전량 DELETE+INSERT(:2700 / PidsGroupPoints :436)
- **심볼 변경의 서버 푸시·낙관적 동시성 없음** → 다중 클라이언트는 last-write-wins
- **Symbols에 MapId 컬럼 없음** → 심볼은 전 맵 공유 전역 자원(MapRois/MapLayers와 대조)

### ⑤ 투영 수학
- 구면 메르카토르(MercatorProjection), y가 위도 비선형
- 공개 좌표 API가 **정수**(`FromLocalToLatLng(int,int)`, GPoint) — GMapControl.cs:2771-2782
- 디지털 줌은 컨트롤 RenderTransform이라 좌표 API에 미반영(inner 좌표계 공유 — 이중보정 금지)
- 맵 회전 θ가 별도 축으로 존재하며 좌표 API는 **회전 인지**(회전행렬 적용)

### ⑥ 화질
- **심볼 7계열은 벡터**(Viewbox+Path) — 확대해도 안 깨짐
- 오버레이 이미지만 `BitmapImage`+`dc.DrawImage`, `BitmapScalingMode`/`DecodePixelWidth` 미지정 (선례는 있음: LayerPanelStyle.xaml:618)
- **원본 픽셀 크기는 보존됨**(Images.Width/Height에 원본 치수 영속, ImageOverlayService.cs:196-207) — 적대검증 L1이 초기 주장을 반증
- TIF 손상 실체는 "2048 다운샘플"이 아니라 **무조건 그레이스케일화 + 보간 미적용**(SetPixel 루프, :998)

### ⑦ UI 호스팅
- 오버레이 패널 표준 = `Control` + `Themes\*.xaml` + `Generic.xaml` 등록
- **어도너 내부에 버튼/핸들을 둘 수 없다** — `GroupSelectionAdorner.HitTestCore`가 선택 멤버 개별 Rect 밖·Ctrl/Shift 시 전체 투과(:185-193). 적대검증 L4가 "핸들로 바꿔도 동일 실패"를 확인
- 대안 = **편집 스트립**(MapView.xaml Grid.Row=1, `GMaps.EditStrip.*`) 5번째 그룹

---

## 2. 시뮬레이션 (5,144건) — 방식 결정

### 2-1. v3.1 결함과 v4 정정 (정직 고지)

초기 시뮬(v3.1)은 **진리값(GT)과 후보(M2E·접평면)가 같은 함수**여서 "M2E 291/291 통과"가 자기참조 항등식이었다. 적대 검증 L2가 독립 측지 계산으로 이를 반증했고, v4에서 ①진리값 2종 독립 구현 ②접평면 곡률반경 정정 ③허용치 단일화 ④뷰포트 필터 분리 ⑤서브픽셀 변형 추가로 재검증했다. **결론이 뒤집혔다.**

### 2-2. 최종 성적 (회전 각 432건)

| 방식 | vs GT-GEO(지면) | vs GT-SCREEN(화면) | 누적 720스텝 | 판정 |
|------|----------------|-------------------|--------------|------|
| M1 정수픽셀 증분 | 30/402 | 46/386 | **523m 드리프트** | 배제 |
| M1sub 서브픽셀 증분 | 348/84 | **432/0** | 0.000m | 유효 |
| **M2 화면공간 합성** | **348/84** | **432/0** | 0.001m | **채택 권고** |
| M2E 접평면 합성 | 243/189 | 249/183 | 0.001m | 배제(v3.1 결론 철회) |
| M3 위경도 평면 | 192/240 | 195/237 | — | 배제 |

- M2/M1sub의 지면 진리값 실패 84건은 **전부 범위 500m 이상** — 사이트 규모(≤500m)에서는 두 관점이 사실상 일치
- 정수 픽셀 왕복만이 치명적이며, **서브픽셀 처리 + base 합성**이면 안전
- 화면공간(M2) 채택 근거는 "정확도 우위"가 아니라 **어도너 박스·마우스 제스처와 좌표계가 일치**(시각 정합) + 기존 `ResizeBoundsWithRatio`/`LineGeometryUtils.Scale` 패턴 승계

---

## 3. 이슈 분류

### 결함(Defect) — 설계로 해소

| ID | 내용 | 근거 | 대응 FR |
|----|------|------|---------|
| D-1 | 증분 변환 시 정수 픽셀 절단 누적(720스텝 523m) | SIM-A4381 | FR-03 |
| D-2 | **이미지 UpdateSize가 값 ≤10이면 '도(degree)'로 해석** → 축소 시 폭 668km 폭발 | GMapImageMarker.cs:811-843, SIM-S | FR-06 |
| D-3 | 계열 분기 불일치 — RenderTransform 단독은 라인 미회전·비영속, 모델 회전 단독은 Bearing 규약 충돌 | SIM-C, GMapMarkerLineControl.cs:239 | FR-04 |
| D-4 | 멤버 간 트랜잭션 부재 → 부분 커밋 시 화면/DB 영구 불일치 | MapViewModel.cs:1211, SIM-T | FR-08 |
| D-5 | 대량 선택(1000개) 시 2000 순차 왕복 ≈ 8초 UI 블로킹 | MapViewModel.cs:7501-7546, SIM-T | FR-08/NFR-01 |
| D-6 | base 스냅샷이 **인덱스 페어링**이라 변환 중 선택집합 변이 시 멤버가 남의 원점으로 순간이동 | GroupSelectionAdorner.cs:203, :218 | FR-03 |
| D-7 | 어도너 내부 HUD는 HitTestCore 투과로 클릭 도달 불가 | :185-193 | FR-01 |
| D-8 | 크기 하한 10px 클램프 후 역배율 비가역 | MarkerEditAdorner.cs:1015, SIM-S | FR-05 |
| D-9 | 래스터 업스케일(오버레이 이미지 한정) — `BitmapScalingMode` 미지정 | GMapMarkerImageControl.cs:280-312 | FR-07 |
| D-10 | 부가 채널 미갱신 — FOV·라벨·카메라 팝업·표시최소줌·ZOrder | SIM-F | FR-09 |
| D-11 | 생명주기 미정의 — 멤버 삭제·맵 전환·ESC·권한 강등·writer 경합·앵커 이탈·모드 선점 | SIM-X | FR-10 |
| D-12 | **카메라 심볼 좌표 = PTZ 조준 원점** → 이동 시 실제 조준 어긋남(발행된 명령은 Undo 불가) | MapViewModel.cs:671-683, CameraAimRequestBuilder.cs:36-47 | FR-11 |
| D-13 | PidsGroup 정점은 **PidsGroupPoints** 별도 테이블 — 누락 시 구역 회전이 재부팅에 소실 | GMapDbSymbolService.cs:436 | FR-04 |

### 정책 공백(Gap) — 사용자 결정 필요 15건

**GAP-13이 가장 중요**: 변환의 "정본 공간"을 **화면 강체**(보이는 대로 회전, 권고)로 할지 **지면 강체**(측량 정확)로 할지. 사이트 규모 500m 이하면 차이가 0.1m 미만이라 실무상 무의미하지만, 5km급 사이트에서는 갈린다.

나머지 14건은 카탈로그 SIM-G 참조 — 요약: 점 심볼 스케일 의미(위치/아이콘/둘 다), Bearing 동반 회전, 피벗 정의, 잠금 멤버, UI 스텝, 라벨 오프셋, 래스터 캡, 부분 실패 정책, 전체선택 입구, 이미지 포함 여부, 맵 회전 축, 정점 재기록 비용, **기존 그룹 속성창과의 우선순위**, MapId 부재(전역 변환).

### 마이그레이션
**없음** — 신규 기능이며 스키마 변경 불필요(단 GAP-15에서 MapId 신설을 택하면 마이그레이션 발생).

---

## 4. 적대 검증 결과 (4렌즈)

| 렌즈 | 판정 | 주요 발견 |
|------|------|----------|
| L1 인용 | 대체로 정확, **3건 정정** | 원본 픽셀 필드 존재(반증) · TIF 손상 실체는 그레이스케일화 · PidsGroupPoints 별도 테이블 · 심볼 단건 트랜잭션은 존재 · :1385 인용 위치 오류 |
| L2 수학 | **핵심 반박 성공** | GT ≡ M2E 자기참조 · ENU가 등장방형 근사 · 뷰포트 필터가 실패 케이스 제거 · 블록별 허용치 상이 → **v4 전면 재작성** |
| L3 완결성 | **16개 가족 추가** | 그룹 속성창 기존 경로 존재(C1 반증) · 이미지 px/도 판별 · 맵 회전 이중보정 · MapId 부재 · Undo 맵 스코프 사문 · base 인덱스 페어링 · 동시편집 · 카메라 조준 · 측정 선점 · RBAC 재확인 · 성능 · 레이어/ZOrder · 앵커 이탈 · UIA/키보드 · 피벗·프리뷰 부재 |
| L4 아키텍처 | **반박 실패**(원안 지지) | 대안 4종(핸들 확장·이미지 Rotation 재사용·임시 컨테이너·순차 승계) 전부 치명 결함. 단 트랜잭션 범위는 "8개 Update*Async 커넥션 소유권 리팩터링"으로 구체화 요구 |

---

## 5. 권장 아키텍처

```
[편집 스트립 5번째 그룹: 그룹 변환]           ← HUD 위치(어도너 내부 불가, D-7)
  ↻15°  ↺15°  [각도 입력]  ⊕×1.1  ⊖÷1.1  [배율 입력]  [적용] [취소]
        │
        ▼
GroupTransformSession (신규)                   ← base 스냅샷을 Id 키 딕셔너리로 보관(D-6)
  · 시작: 선택 집합 동결 + 멤버별 (Position/Bearing/W/H/정점) 스냅샷
  · 누적: totalDeg, totalScale 만 갱신 → 매번 base에서 1회 합성(D-1)
  · 미리보기: 화면공간 변환으로 즉시 렌더(커밋 전)
  · 커밋: 계열별 분기 적용 → 배치 트랜잭션 저장 → 1매크로 Undo
  · 무효화: 멤버 삭제/맵 전환/ESC/권한 강등/편집모드 OFF 시 세션 폐기(D-11)
        │
        ├─ 점 심볼   : Position 화면공간 회전·스케일 + Bearing += θ(GAP-2)
        ├─ 라인      : LinePoints 전 정점 변환
        ├─ PidsGroup : PidsGroupPoints 전 정점 변환(D-13)
        └─ 이미지    : AABB 4점 변환 + Rotation += θ, **단위 명시 API로 px 고정**(D-2)
        │
        ▼
GMapDbSymbolService.BatchUpdateTransformAsync (신규)
  · 단일 커넥션 + 트랜잭션으로 N건 부분 UPDATE(전체행 UPDATE 금지 — Category 오염 이력)
  · 8개 Update*Async의 커넥션 소유권을 상위 주입으로 리팩터링(L4 지적)
```

---

## 6. 검증 계획
- 단위: 변환 수학 순수 함수(`GroupTransformMath`) — SIM ID를 테스트명에 승계(`should_preserve_shape_when_rotated_45deg` 등)
- 통합: 배치 트랜잭션 부분 실패 롤백(격리 DB `monitor_test_db`)
- 실기: 혼합 그룹(점+라인+구역) 회전/스케일 → 재부팅 후 형상 유지, 이미지 축소 시 폭발 없음, 1000개 성능


---

## 7. v2.0 재검토 (2026-09-08) — 3D 심볼·2.5D 틸트·철망 도입 반영

사용자 요청("3D 심볼까지 도입됐다, 다시 검토")으로 신규 영향면 5축을 재정찰(에이전트 7기, 1.08M 토큰)하고 적대 검증했다.

### 7-1. 판정 요약

| 기존 결론 | 판정 | 근거 |
|----------|------|------|
| C-A 변환 공간 = 화면(메르카토르) + 서브픽셀 | **근거만 정정(Low)** | 좌표 API는 틸트에서도 inner를 반환(디지털줌과 동형, `GMapCustomControl.cs:1948`·`Tilt.cs:337`). 다만 "보이는 대로"는 φ>0에서 거짓 → **"마커·격자·스냅이 공유하는 inner 공간"**으로 근거 교체. outer 공간 수학은 코드가 금지(이중보정) |
| C-B 계열별 분기 | **개정(High)** | 점 심볼이 3갈래(2D회전 / 3D-yaw / 빌보드)로 갈리고, **분기 키를 렌더 플래그로 잡으면 안 된다**(설치별 `Symbol3DFeature`) |
| C-C HUD = 편집 스트립 | **유지** | 스트립은 여전히 4그룹(등록·심볼·선택·기준), 틸트 UI는 운영 툴바·메뉴·단축키로 감 |
| C-D 프리뷰 = 화면 변환만 | **개정(Medium)** | 3D 철망·하우징은 화면 변환 프리뷰가 커밋 결과와 불일치 → 2D 고스트 강제 |
| C-E 확대해도 안 깨짐 | **결론 유지, 근거 정정** | 3D는 Viewbox+Path가 아니라 **무텍스처 단색 메시 + 카메라 재적합**(`HousingObjLoader`가 vt/map_Kd 미파싱) — 오히려 해상도 독립. 단 철망은 삼각형 예산(20000 tri/km) 초과 시 레일 자동 생략 |
| C-F 배치 트랜잭션 영속 | **유지(각주)** | 구역 정점은 DELETE+전량 INSERT 구조라 트랜잭션 확장 필요 |

### 7-2. 신규 결함

| ID | 내용 | 근거 | 대응 |
|----|------|------|------|
| **D-14** | **설치별 3D 플래그로 DB 쓰기 분기** — `Symbol3DFeature`가 개발 ON/배포 OFF라, `IsBillboard`로 Bearing 가산을 분기하면 같은 장비가 설치본마다 다른 값을 쓴다. 반대로 무조건 가산하면 2D에서 보이지 않는 상태가 저장돼 3D를 켜는 순간 전 심볼이 과거 회전량만큼 일제히 돈다 | `Symbol3DFeature.cs:33-39` · `GMapMarker3DHousingControl.cs:16,49` | FR-04 데이터 기준 분기 · GAP-21 |
| **D-15** | **뷰 프레임 변화로 base 스냅샷 stale** — 오버스캔이 컨트롤 높이를 `H/cosφ`로 바꿔 RenderOffset이 재계산되고 전 마커 로컬 px가 Δ 평행이동(φ=20°·H=800 → 25.7px ≈ 12m @z18). 틸트는 줌 18.0/17.5 히스테리시스로 **자동** 전이. 줌·팬·리사이즈도 동일 축 | `TiltOverscanMath.cs:53-66` · `Core.cs:453-478,521-527` · `TiltMath.cs:132` | FR-03 투영픽셀 스냅샷 · FR-10/19 |
| **D-16** | **3D 프리뷰 불일치** — 철망은 화면고정 35° 카메라·미터 기준 높이·둘레 파생 기둥 수라, RenderTransform 프리뷰를 걸면 기둥이 기울고 높이가 배율만큼 늘어 커밋 결과와 다르다 | `FenceRunVisual.cs:87-89,141-166` | FR-02 2D 고스트 |
| **D-17** | **3D 심볼이 러버밴드에 그대로 포함** — 3D 컨트롤은 2D 컨트롤의 sealed 파생이라 배제 조건(disposed/locked/invisible/이미지)에 걸리지 않는다 → "3D는 Out of Scope" 선언이 무력 | `GMapCustomControl.cs:1019-1036` | 범위 정정 · FR-04ⓑ/FR-16/FR-18 |

### 7-3. 적대 검증이 폐기시킨 판정 (자기교정 2회차)

v5 시뮬에 넣었던 **"틸트 중 제스처 입력은 1/cosφ 역보정 필수"(ISSUE-42)는 오판**이었다.
WPF가 `e.GetPosition(this)`에 `RenderTransform.Inverse`를 자동 적용하므로 좌표는 이미 inner이고,
수동 보정을 더하면 **이중보정 버그**다 — 코드가 명시적으로 금지한 바로 그 함정(`GMapCustomControl.Tilt.cs:336-341`).
v6에서 반대 방향(ISSUE-44: 역보정 추가가 오답)으로 정정했다.

또한 적대 검증은 다음 3건도 **개정 사유가 아님**을 입증했다:
- "서브픽셀 불가" → PRD 전제 5장이 이미 `SubPixelGeo` 우회를 명시, 구현체·사용처 실재
- "`BatchUpdateTransformAsync` 미구현" → FR-08이 **신설을 선언한 산출물**이므로 결함이 아님
- "회전 시 철망 매 프레임 전량 Rebuild" → FR-01 HUD가 **이산 스텝**(±15°·×1.1)이라 클릭당 1회. 드래그 회전 핸들 도입 시에만 High로 복귀

### 7-4. 시뮬 v6 결과

5,255건(PASS 3,069 / ISSUE 2,166 / GAP 20). 신규 계열: SIM-TL(틸트 왜곡 72건) · SIM-VF(뷰 프레임 34건).
- **TS-OUTER(화면 기준 회전) 20/25 실패** — φ=35°에서 지면 비등방 1.327(정사각형이 찌그러짐)
- **TS-INNER(inner 기준) 8/25** — 지면 강체는 보존, 화면 관측각만 최대 5.7° 차이(φ=20° 기본에서는 1.8°)
- **SIM-VF**: 로컬 px 스냅샷은 φ=35°·H=1080에서 119px(≈56m @z18) 평행 오차
