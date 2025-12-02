# Phase 20: PidsSymbol FOV BaseBearing - 진행 상황 요약

## 📋 개요

**PRD 문서**: [PRD_PidsSymbol_FOV_BaseBearing.md](Docs/prd/PRD_PidsSymbol_FOV_BaseBearing.md)
**구현 계획**: [phase20_plan.md](phase20_plan.md)
**TDD 방식**: CLAUDE.md 준수 (RED → GREEN → REFACTOR)

## 🎯 목표

PidsSymbol의 FOV(Field of View)가 **BaseBearing(카메라 물리적 설치 방향)**에서 시작하도록 개선

### 핵심 공식
```
DetectionBearing = BaseBearing + 사용자회전각도
```

### 데이터 저장 전략
- ✅ **BaseBearing**: DB 저장 (카메라 물리적 설치 방향, 고정값)
- ❌ **DetectionRange**: 런타임 전용 (DB 저장 안 함)
- ❌ **DetectionAngle**: 런타임 전용 (DB 저장 안 함)
- ❌ **DetectionBearing**: 런타임 전용 (DB 저장 안 함)

## 📊 Phase 별 진행 상태

### Phase 20.1: 데이터 모델 업데이트 (STRUCTURAL - TDD)
**상태**: [ ] 미진행

- [ ] Test 20.1.1: BaseBearing 기본값 검증
- [ ] Test 20.1.2: BaseBearing JSON 직렬화 검증
- [ ] ActionItem 20.1.1: PidsSymbolModel에 BaseBearing 속성 추가

**파일**: `Ironwall.Dotnet.Monitoring.Models/Symbols/PidsSymbolModel.cs`

---

### Phase 20.2: Database Schema 업데이트 (STRUCTURAL)
**상태**: [ ] 미진행

- [ ] ActionItem 20.2.1: createPidsSymbolsSql에 BaseBearing 컬럼 추가
- [ ] ActionItem 20.2.2: Insert 쿼리에 BaseBearing 추가
- [ ] ActionItem 20.2.3: Update 쿼리에 BaseBearing 추가
- [ ] ActionItem 20.2.4: Select 쿼리에 BaseBearing 추가

**파일**: `Ironwall.Dotnet.Libraries.GMaps.Db/Services/GMapDbSymbolService.cs`

---

### Phase 20.3: FOV 생성 로직 수정 (BEHAVIORAL - TDD)
**상태**: [ ] 미진행

- [ ] Test 20.3.1: FOV 생성 시 BaseBearing 반영 검증
- [ ] Test 20.3.2: Symbol 로드 후 DetectionBearing 초기화 검증
- [ ] ActionItem 20.3.1: FOV 생성 코드 탐색
- [ ] ActionItem 20.3.2: FOV 초기 Bearing에 BaseBearing 적용

**파일**: GMapCameraMarker, FovRenderer 또는 관련 ViewModel

---

### Phase 20.4: UI 구현 (BEHAVIORAL)
**상태**: [ ] 미진행

- [ ] ActionItem 20.4.1: PidsPropertyStyle.xaml에 BaseBearing Slider 추가
- [ ] ActionItem 20.4.2: GMapPropertyPidsControl.cs에 BaseBearing 속성 추가

**파일**:
- `Ironwall.Dotnet.Libraries.GMaps.Ui/Themes/PidsPropertyStyle.xaml`
- `Ironwall.Dotnet.Libraries.GMaps.Ui/GMapProperties/GMapPropertyPidsControl.cs`

---

### Phase 20.5: 통합 테스트 및 검증 (BEHAVIORAL)
**상태**: [ ] 미진행

- [ ] ActionItem 20.5.1: 수동 통합 테스트 시나리오
- [ ] ActionItem 20.5.2: 회귀 테스트

---

## 📈 통계

| 항목 | 개수 | 완료 | 진행률 |
|------|------|------|--------|
| **총 Phase** | 5 | 0 | 0% |
| **총 Tests** | 4 | 0 | 0% |
| **총 ActionItems** | 11 | 0 | 0% |
| **총 Tasks** | 15 | 0 | 0% |

## 🔄 다음 단계

### 1단계: TDD 시작 (Phase 20.1)
```
"go" 명령어 입력 시:
1. Test 20.1.1 작성 (RED)
2. BaseBearing 속성 구현 (GREEN)
3. Test 20.1.2 작성 (RED)
4. JSON 직렬화 검증 (GREEN)
5. 리팩토링 (REFACTOR)
6. Commit
```

### 2단계: Database Schema (Phase 20.2)
- createPidsSymbolsSql 수정
- Insert/Update/Select 쿼리 수정

### 3단계: FOV 로직 (Phase 20.3)
- FOV 생성 코드 탐색
- BaseBearing 적용

### 4단계: UI (Phase 20.4)
- PidsPropertyStyle.xaml 수정
- GMapPropertyPidsControl.cs 수정

### 5단계: 테스트 (Phase 20.5)
- 통합 테스트
- 회귀 테스트

## 📝 중요 사항

### TDD 원칙 준수
- ✅ RED: 실패하는 테스트 먼저 작성
- ✅ GREEN: 최소한의 코드로 테스트 통과
- ✅ REFACTOR: 테스트 통과 후 리팩토링
- ✅ Commit: 모든 테스트 통과 + 경고 해결 후 커밋

### Commit 규칙
- STRUCTURAL 변경과 BEHAVIORAL 변경 분리
- 커밋 메시지에 변경 타입 명시
- 모든 테스트 통과 확인

### 하위 호환성
- 기존 Symbol은 `BaseBearing = 0` (정북 방향)
- 기존 기능 정상 동작 보장

## 🎓 참고 문서

- **CLAUDE.md**: TDD 및 Tidy First 방법론
- **PRD_PidsSymbol_FOV_BaseBearing.md**: 기술 사양 및 요구사항
- **plan.md**: Phase 19까지 완료된 기존 계획
- **phase20_plan.md**: Phase 20 상세 계획

---

**작성일**: 2025-12-02
**버전**: 1.0
**상태**: 준비 완료 (구현 대기 중)
