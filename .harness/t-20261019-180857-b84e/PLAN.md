# PLAN — 같은 종류 다중 선택 속성 판 = 단일 선택 칸 전부 (PIDS 그룹 펜스에서 시작해 전 종류로 확대)

## 목표(끝의 정의, 2026-10-19 추가 지시로 확대)
1. 같은 종류만 여러 개 고르면 속성 판 = 그 종류 하나 골랐을 때의 칸 전부(순서 · 디자인 그대로).
   공통 값 = 그룹 편집(같으면 값 · 다르면 '여러 값' · 바꾼 필드만 전원 적용 · 마커별 저장 · Undo 1단위 · 잠금 제외),
   심볼마다 고유한 값 = 대표값 + 비활성, 읽기 전용 정보 = 합계 / '—'.
2. 서로 다른 종류가 섞이면 지금 규칙(공통 칸만, GMapPropertyCommonControl) 유지.
3. 전 종류 표 시험 + 수정 전 실패 → 수정 후 통과 증명.

## 종류 전수(PropertyPanelFactory._markerToPanelMap + PIDS DeviceType)
| 종류 | 판 / 스타일 | 다중 선택에서 빠졌던 칸(수정 전) | 처리 |
|---|---|---|---|
| PIDS 그룹(펜스) | PidsGroup / PidsGroupPropertyStyle | 3D 철망 칸 전체(1차 작업에서 수정) | 그룹 편집 · 「기본」 그룹 적용 · 요약 "—" · 포인트 수/총 거리 합계 · 장비 그룹 비활성 |
| PIDS 장비 2D(20종: Controller · Multi · Fence · Underground · Contact · PIR · IoController · Laser · Cable · IpCamera · SmartSensor · SmartSensor2 · SmartCompound · IpSpeaker · Radar · OpticalCable · Lamp · Enclosure · SmartMultisensor2 · Gate) | Pids / PidsPropertyStyle | Gate: 통문 폭 · 문 상태 / Enclosure: 문 상태 | 통문 폭 그룹 편집(「기본」 포함), 문 단추=장비별 명령이라 비활성 |
| PIDS 장비 3D 하우징(같은 20종) | 〃 | 색상 강도 · 설치 높이 (+IpCamera 모델 변형, Gate/Enclosure 문 · 통문) | 설치 높이 · 모델 변형 그룹 편집, 색상 강도 비활성(저장 안 되는 화면 값) |
| 건물 3D | Infra / InfraPropertyStyle | 색상 강도 · 설치 높이 | 〃 + 건물 칸 혼합 표시(종류 · 용도 무선택, 층수 "—", 면적 빈칸) |
| 건물 2D · 도형 · 핀(Custom) · 선 · 군사 심볼 · 오버레이 이미지 | 각 판 | 없음(칸은 다 보였음) | 대표 마커 직접쓰기 → VM 일괄 적용 경로로(대표 Undo 결함), 혼합 표시, Undo 불가였던 공통 칸(도형 투명도 · 이미지 비율유지 · 군사 7칸) 일괄 적용/Undo 등록 |

## 고유 값(대표값 + 비활성) 목록과 근거
- 제목(전 종류) — 사용자 지시 "제목처럼 하나뿐이어야 하는 값". 표시 규칙(같으면 값 · 다르면 빈칸)은 기존대로.
- PIDS 연결디바이스 · [현재위치 적용] — 장비 1개 = 마커 1개(기존).
- PIDS 문 열림/닫힘 — 장비별 제어 명령.
- PIDS · 건물 색상 강도 — 모델에 저장되지 않는 대표 심볼 그림 값(일괄 적용 · Undo 경로 없음).
- PIDS 그룹 장비 그룹 — 구역 ↔ 장치 그룹 1:1.
- 군사 심볼 부대 지시자 · 콜사인 — 부대마다 하나.
- 이미지 파일경로(원래 읽기 전용) · 이미지 표시(그룹 어도너 메뉴 '그룹 표시/숨김' 담당).

## 진행
- [x] 1차: PIDS 그룹 펜스 칸 그룹 편집(PidsGroupFenceGroupEditScenarioTests 11개)
- [x] UndoableCommandBase: MarkerOpacity · MaintainAspectRatio · 군사 7칸(Affiliation · BattleDimension · StandardIdentity · UnitType · UnitSize · HigherFormation · CountryCode) Apply/Read/Replay 추가
- [x] Base: 설치 높이(Altitude) 혼합(NaN), 제목 그룹 비활성(AutomationId GMaps.Property.Title)
- [x] PIDS: 그룹 숨김 트리거 5개 제거, 모델 변형 · 통문 폭 · 「기본」 그룹 편집, 문 단추 비활성, IsMixedDeviceType(장비 종류 섞이면 종류 전용 칸 숨김)
- [x] Infra: 그룹 숨김 트리거 2개 제거, 혼합 표시, IsFloorCountMixed/IsBasementFloorCountMixed
- [x] Line · Geometric · Image · Military · PidsGroup: 그룹이면 대표 직접쓰기 억제, ApplyGroupPendingSpecific(혼합 표시), 고유 칸 비활성
- [x] 시험: tests/GMaps.PropertyPanel.Tests/SameKindGroupPanelParityTests.cs — 48종(2D 27 + 3D 21) × ① 칸 집합 비교 · ②③⑤ 공통 값 적용/혼합/잠금/저장/Undo, ④ 고유 값 7건, 통문 · 모델 변형 · 종류 혼합 · 합계
- [x] 수정 전/후 표(아래)
- [ ] sln 시험

## 수정 전 실패 → 수정 후 통과(① 칸 집합 Theory, 제품 소스만 HEAD 로 되돌려 실행)
- 수정 전 실패 23종: Gate · Enclosure(2D), 3D:전 PIDS 20종, 3D:Infra → 수정 후 48종 전부 통과
- 2D 나머지 25종(도형 · 핀 · 선 · PIDS 그룹 · 건물 · 군사 · 이미지 · PIDS 18종)은 수정 전에도 칸은 같았음(PIDS 그룹은 1차 작업 커밋에 이미 포함 — 1차 증명: should_hide_fence_block… Collapsed→Visible)
- ②(일괄 적용 · Undo) 시험은 새 공개 속성(IsFloorCountMixed 등)을 써서 수정 전 소스로는 컴파일되지 않아 전/후 비교 대신 수정 후 48종 통과만 기록

## 발견
- 판이 그룹 모드에서도 대표 마커에 직접 쓰던 종류(선 · 도형 · 이미지 · 군사 · 건물 · PIDS 그룹 라인)는 VM 이 before 를 이미 바뀐 값으로 읽어 Undo 가 대표 마커를 되돌리지 못했다.
- 군사 심볼 칸 · 도형 투명도 · 이미지 비율유지는 IsReplayableProperty 밖이라 다중 선택에서 대표 하나만 저장되고 Undo 기록도 없었다.
- 시험 장치 함정: Undo 는 지도(Map.Markers)에서 Id 로 마커를 찾는다 — 시험 마커를 지도에 올려야 한다. 3D 전용 행은 Shape 가 3D 하우징일 때만 보인다.
