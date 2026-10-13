# PLAN — 지도 속성창 숫자 칸 정밀도 · 범위 정리 (t-20261013-162416-53b9)

## 목표
지도 심볼 속성창 · 지도 겹창의 슬라이더/숫자 칸에서 부동소수 꼬리(11.9999999 · 0.30000000000000004)가
화면 · 모델 · 저장값에 나오지 않게 하고, 정수 칸은 정수로만, 음수가 의미 없는 칸은 최소 아래로 못 가게 한다.
명세 원본 = `docs/prds/map-property-numeric-precision-prd.md` 의 표.

## 결정
- 공용 도우미 = `GMaps.Ui/Helpers/Numerics/NumericFieldSpec.cs`(최소 · 최대 · 단위 step → Normalize/Coerce/Format)
  + `MapNumericFields.cs`(표의 코드판). 반올림은 decimal 로 계산(이진 잔차 없음), 중간값은 0 에서 먼 쪽.
- 정규화 지점 = 속성창 DP 의 CoerceValueCallback(슬라이더 · 텍스트 · 마커에서 불러온 값 · 그룹 편집 이벤트 값이 모두 여기를 지난다).
  NaN(그룹 Pending 표지)은 그대로 통과. 겹창(VM 세터)은 세터에서 같은 spec.Normalize 호출.
- 슬라이더는 전부 IsSnapToTickEnabled + TickFrequency = 그 칸 단위(정수 칸은 1). 재생 진행 막대(Progress)만 예외(시간 위치 · 저장값 아님).
- "잘라 안내" = 범위 밖 입력은 잘라 칸에 되돌려 보이고(보이는 값 = 저장 값), 칸 툴팁에 범위를 적는다. 새 알림 창은 만들지 않는다.
- 철망 · 통문(기둥 간격 · 높이 · 문 폭)은 이미 FenceMath.Quantize 로 정상 — spec 으로 옮겨 같은 도우미를 쓰게 한다.

## 진행
- [x] PRD 표 작성 — docs/prds/map-property-numeric-precision-prd.md
- [x] NumericFieldSpec(+IValueConverter) · MapNumericFields
- [x] 속성창 DP 강제 함수 + 칸→마커 바인딩 변환기(Base · Geometric · Image · Infra · Line · Pids · PidsGroup · TintStrength)
- [x] XAML 슬라이더 스냅/눈금/표시 형식(P2→P0), 크기 · 제목 크기 글상자 spec 변환기
- [x] 겹창(추적 설정 VM 세터 · 격자 크기)
- [x] 시험: tests/GMaps.PropertyPanel.Tests/NumericPrecisionScenarioTests.cs(NUM-001~020, 21건 통과), Utils.Tests SliderPrecisionContractTests(NUM-030/031 통과)
- [x] GMaps.PropertyPanel.Tests 전체 179 통과
- [x] dotnet test sln(종료 0) · MapEdit 55 · MapAux 76 · Housing 312 · MapLayer 63 · MapNav 47 · GMaps.Ui.Tests 757 모두 통과
- 기존 시험 1곳 조정: MapAux AUX-015 의 "TTL 0 · 음수를 검증 없이 그대로 받는다" 단언 → 이번 명세(음수 · 0 금지, 최소 1 초)로 바뀐 값 1 을 단언.
  소비처 3 초 하한 검사는 그대로 둠(규칙이 바뀐 동작을 고정하던 단언이라 명세를 따름).
- 참고: docs/ 는 .gitignore 대상(docs/prds 기존 PRD 와 같음) — PRD 파일은 작업 트리에만 있다. 표 요약은 MapNumericFields.cs 가 코드판.

## 남은 일
- 없음. (실창 확인은 하지 않음 — 사용자 PC 화면 사용 금지 규칙에 따라 헤드리스 시험만)

## 결정(추가)
- 크기(너비/높이)는 칸 강제 함수를 정수로 바꾸지 않음 — 이미지 겹침 심볼의 너비/높이는 줌에서 계산된 픽셀이라
  선택만 해도 반올림 값이 되써져 지리 경계가 바뀐다. 글상자 변환기로 표시 정수 · 입력 정수만 보장(PRD ※).
- 수정 전 빨강: NUM-011 첫 실행(강제 함수만 고친 중간본)에서 marker.Bearing = 11.999999900000001 — 칸→마커 TwoWay 바인딩이
  강제 전 원시값을 쓰는 것이 사용자 보고의 실제 경로. 바인딩에 spec 변환기를 걸어 해결.

## 발견
- WPF 슬라이더 스냅은 `Minimum + round((v-Minimum)/Tick)*Tick` 을 double 로 계산 → 1.5+26×0.1 = 4.1000000000000005 같은 꼬리.
- 스냅 없는 슬라이더(설치 높이 · 투명도 · 탐지 거리/각/방향 · 색상 강도 등)는 끄는 대로 임의 실수(예 37.4285714) → 기존 CoerceDoubleValue 가 소수 2자리로만 잘라 정수 칸에 37.43 이 저장됐다.
- 제목 크기(TitleSize) 는 강제 함수가 없어 꼬리 그대로 표시 · 저장.
