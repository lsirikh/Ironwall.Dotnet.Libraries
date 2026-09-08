<!-- auto-section-start -->
# 프로젝트 문서 인덱스

- **마지막 갱신**: 2026-08-06 (advance-phase 자동)
- **총 문서 수**: 486개

---

## 분석 (docs/analyses/)

| 파일 | 분석 대상 | 날짜 |
|------|---------|------|
| [map-tilt-25d-scenario-analysis.md](analyses/map-tilt-25d-scenario-analysis.md) | 지도 카드 틸트 **시나리오 기반 분석**(사용자 결정 ①살짝 비스듬 ②줌 게이트 ③마커 정립) — 영향면 6축 전수(file:line), V-02 헤드리스 실측(Viewport3D 비등방 승계 확정), 시뮬 4,096+22 전량(정책 불변식 위반 0), 결함 D1~D6 · 결정③ R3 · 정책 공백 G1~G10(제안값 승인) → PRD 입력 | 2026-09-08 |
| [map-25d-tilt-feasibility-analysis.md](analyses/map-25d-tilt-feasibility-analysis.md) | 지도 자체 2.5D(틸트) 표시 타당성 — 아핀 카드 틸트(A) · `Viewport2DVisual3D` 원근(B) · **하이브리드(C, 권고: 높이 과장·피치 SSOT·스냅샷 틸트 스파이크)**. RenderTransform 은 아핀 전용(원근 불가)·필수-060 단일 축척·RDP Tier 0 충돌, 조사 4축 + 설계 3안 + 적대 검증 | 2026-09-08 |
| [gopdb-0907-deploy-impact-analysis.md](analyses/gopdb-0907-deploy-impact-analysis.md) | GOPDB 09-07 배포 2건(조치보고 `POST /reorder` · 억제 주간반복) 클라 영향 — **"제일 중요"라던 해제 타이머는 고칠 코드 0건**(`window_end` 소비처=표시 2곳). 🔴 마감: 권한 키 소멸(운영서버 6.3.3 확인 후 동시 배포 필요) · 서버측 결함 1건(단발창 `suppressing` 오발행) · 구현 함정 7종. 적대검증 2기 | 2026-09-07 |
| [drag-reorder-infra-analysis.md](analyses/drag-reorder-infra-analysis.md) | 공용 드래그 정렬 인프라 타당성 v1.1 — **적대검증 3기 전원 fatal**. 구조는 생존, 동작 3대 축(Button 핸들 자멸·DataTemplate measure 크래시·로컬값 삽입선 지터) 재설계 필요. 서버 블로커는 `/reorder` 신설로 **해소**. 키보드 `Key.System`/`SystemKey` 실측 · UIA 드래그 패턴 부재 | 2026-09-07 |
| [window-architecture-analysis.md](analyses/window-architecture-analysis.md) | **창(Window) 아키텍처 전면 재설계 기획서 — Surface Kernel** · 실측 8축 + 설계 4안 + 심사 12렌즈 + 적대검증 4렌즈(29 에이전트, 8.07M 토큰). 근본원인 확정: 모달 3층이 `LeftMenuSectionView.xaml:271` 자식이라 좌표 부여 지점 0 · 층당 1개 + 풀스크린 스크림(#88000000) 3장. **P0 7건**(Progress 팝업 닫기 요소 0개 → 앱 하드락 · 확인 팝업 '취소' 미전달 · `_pending*` 13파일 워크어라운드 · Info-finally 즉시닫기 2건). 표면 6분류(Modal/Float/Detached/Toast/Cover/MapAnchored) + 전수 배치표 24종 · `IDialogService.ConfirmAsync` 전문 · 마이그 252 편집점 · 로드맵 Phase 0~4(A 4~5주 / B 11~14주 / C 17~21주) · 리스크 24 · **GAP 11(사용자 결정 대기)**. 부록 B 독립 재검증 5/5 + **자산 발견: `LayerPanelControl` 이 이미 드래그 패널 구현** | 2026-09-06 |
 [action-report-custom-template-scenario-analysis.md](analyses/action-report-custom-template-scenario-analysis.md) | 조치보고 커스텀 문구 시나리오 기반 분석 v1.0 — 영향면 6축 · 시나리오 235건 · **이슈 42종**(결함 32·정책공백 6·마이그 4) · 적대검증으로 가짜이슈 3건 교정. **선행 결함 2건이 핵심**: 클라 `events:control` vs 서버 `events:edit` 불일치(OPERATOR 조치보고 403) + 매트릭스에서 control 부여 불가 | 2026-08-07 |
| [current-problems-audit-20260903.md](analyses/current-problems-audit-20260903.md) | "현재 문제 없나?" 전수 감사 — 6차원 조사+차원별 반박 검증+완결성 비평(13 에이전트, 읽기 전용): 68건 → 확정 58·하향 9·미검증 1·반박 0 + 비평 8. 🔴 E2E 3종 운영 API/NATS 기본값(VF-07, 오늘 커밋 포함) · 🔴 라벨오프셋 D-2+마이그레이션 부재 · 🟠 **신규** LiveCharts XAxes 예외 매 실행(VF-12 유력 원인) · 🟠 설치본 Url=localhost(08-31~, 의도 확인) · 🟠 업그레이드 설치본 ClientId 미적용 · 정정: VF-04 17/17 재현(FlaUI 타임아웃) · 장부 소음 원인 확정(훅 첫 매치 4종) | 2026-09-03 |
| [symbol-3d-housing-storyboard.html](design/symbol-3d-housing-storyboard.html) | **v2.0** — **사용자 확정 방향(군대부호 제외 · 사실적 형태 3D 유지 · 단순화 · 회전 동기)** 스토리보드 v1.0 — 하우징 8종(돔/PTZ/스마트센서/다중/스피커/제어기/조명/함체)+펜스 표식을 **박스·실린더·절두체 프리미티브 코드 합성**으로 CSS-3D 렌더, θ 슬라이더로 yaw=Bearing−θ 회전·화면 고정 광원 플랫 셰이딩·피치 슬라이더·30/50/100 사다리·지도 위 미리보기(다크/라이트) · WGS-84/Web Mercator 등각성 답변 · 모델 스펙 표 · 구현 배선 표(검증 완료) · **v1.2 §1-B 확장 심볼 24종**(차량 5·건물/인프라 11·아트 없던 PIDS 8) · **v1.5 §0 SVG 원본 대조 7종**(스마트센서·돔 카메라·혼 스피커·함체·제어기·경광등 = 원본 비율/실측 색, 펜스 = 현행 XAML 기준) yaw 0/45/90/180 + 30/50px · 체인링크 메쉬 패턴 · 경광등 타입 신설 후보 · **CSS-3D 피치 부호 교정**(종전엔 아래서 올려다보는 뷰라 돔이 사발로 보였음) — 원본 사본 `docs/assets/svgref/` | 2026-09-04 · **§6 심볼 팔레트(탭 5종 전환 — PIDS 17·기반시설 12·도형 8·기본 2·차량 5 비노출, 미리보기 선택 + 드래그 배치) · §6-B 드래그 4단계 · §6-D 속성 ↔ 3D 매칭 · **§6-E DB 컬럼 전수 매핑(안 쓰이는 필드 0) · **§8 실물 와이어프레임**(실제 XAML 구조·Tokens.Dark 실측 — 편집 스트립 현행/신규 · 속성 패널 5섹션 · 팔레트 크롬 · 드래그 상태 전이) · §7 라이브 에디터**(7-A PIDS 점 심볼 · 7-B PIDS 그룹 철책 · 7-C 기반시설, 슬라이더 조작 → 3D 즉시 반영, 브라우저 검증 30/30)**(2026-09-04 추가) |
| [pidsgroup-3d-fence-gate-storyboard.html](design/pidsgroup-3d-fence-gate-storyboard.html) | **PIDS 그룹 3D 철망(기둥 간격 2/3/5m) · 통문 개폐 · 함체 개폐 와이어프레임 스토리보드 v0.1 Draft** — 사용자 요청(2026-09-07 "선을 드래그해서 그리면 철망이 3D 로 이어지고, 통문·함체는 이벤트로 열림/닫힘"). 인터랙티브 기둥 배치·LOD·상태 머신(ContactOn/Off→DoorState)·DB/계약 영향·결정 D1~D9(D2 확정: 통문=신규 enum `Gate`+전용 3D) · 사용자 확인 중 | 2026-09-07 |
| [symbol-design-system.html](design/symbol-design-system.html) | 지도 심볼 디자인 개선안 v1.0 — **현행 XAML 실물 vs 제안 픽토그램을 30/50/100px 다크/라이트로 나란히** · 진단 7건(일러스트 vs 아이콘·시점 5종 혼재·하드코딩 색으로 다크에서 검정 실루엣 실종·Trigger 199 상태 덮어쓰기·제어기 250×90→50×50 2.8배 왜곡·방향 노즈 부재·SVG 소스 부재) · 원칙 6(24-grid 2px 실루엣 · 배지+글리프 2층 · 토큰 바인딩 · 톱다운 통일 · 노즈 · SVG→XAML 생성기) · 상태 5종×크기 3 배지 · 30px 지도 미리보기 · 단계 D-1~D-4 | 2026-08-07 |
| [symbol-current-inventory.html](design/symbol-current-inventory.html) | 현행 PIDS 심볼 6종 **실물 인벤토리** — `PidsMarkerStyle.xaml` Path/Shape 를 파서로 SVG 변환해 30/50/100/200px 다크/라이트 렌더(도형 수·숨김·경고 표기). 생성기 `scratchpad/extract_symbols.py`, 데이터 `design/symbol-current.js` | 2026-08-07 |
| [symbol-3d-storyboard.html](design/symbol-3d-storyboard.html) | 지도 심볼 3D화 와이어프레임·스토리보드 v1.1(반증 반영) — 심볼 12종 현행 2D→제안 2.5D 카탈로그(회전 프레임 태그) · **bearing 슬라이더로 직접 돌려보는 지도 목업(다크/라이트, 5축 분해: 발판·FOV 지오고정 / 압출·그림자·라벨 화면고정 / 아이콘 현행)** · 0/45/90/180 4프레임 · 3D 턴테이블 픽커 와이어프레임 · 렌더 경로 E1~E5 판정 · 단계 A/0/1/2 · 결정 질문 Q1~Q4 | 2026-08-07 |
| [suppression-schedule-form-redesign.html](design/suppression-schedule-form-redesign.html) | 억제 스케줄 생성 폼 디자인 개선안 v1.0 — 진단 8종(입력 언어 2종 혼재·좌우 높이 불일치·카드 경계 소실·주액션 유령·위계 부재·빈 트레이 구멍·액션바 부재·선택행 투박) + 다크/라이트 목업 + 적용범위 7항 (⑦ 목록 열 통합은 사용자 확인 대기) | 2026-08-07 |
| [group-symbol-transform-scenario-analysis.md](analyses/group-symbol-transform-scenario-analysis.md) | 그룹 심볼 변환 **시나리오 기반 분석 v2.0** — 영향면 7축(9기 1.4M)+3D/틸트 재정찰 5축(7기 1.08M) + 시뮬 6세대 5,255건 + **적대검증 4+2렌즈**. ⚠자기교정 2회: v3.1 진리값≡후보 자기참조(L2 반증) · v5 "1/cos 역보정 필요"가 실은 이중보정 버그. 결함 17 · GAP 22 | 2026-09-08 |
| [symbol-3d-wip-propagation-analysis.md](analyses/symbol-3d-wip-propagation-analysis.md) | **3D 심볼 WIP 반영 실패 진단 + §8-5 팔레트 40종 드래그 순회·D-18/19(어도너 위 드롭)·D-21(하프스텝 줌 소실)** — 7렌즈 추적 + 렌즈별 적대 반증(15에이전트·872툴콜), 132항목. BROKEN 5 · PARTIAL 9 · RISK 17 · 정상확인 21. **최상위 공통원인 D-1** = `GMapBaseMarker.cs:231` 로컬값 대입이 3D 의 OneWay W/H 바인딩을 영구 파괴 (어도너·그룹편집·Undo 3발화원, 재바인딩 경로 0, 테스트가 되레 잠금). D-2 FOV Canvas 가 IpCamera MultiTrigger 로만 열려 비카메라 8종 영구 불가(3D OFF 에도 폴백 경로로 2D 회귀) · D-3 DetectionBearing 컬럼 0건 미영속 · D-4 층수 텍스트 소멸 · D-5 안테나 등 재질 토큰 부재로 색 미도달 · D-12 Infra3D 상태색이 사용자색 바인딩 영구 파괴. **3D 플래그는 bin/Debug 에만 존재 → 다음 빌드에 꺼짐**, 배포본 키 없음. 수정 순서 12단계 + 금기 6건. **§8 적용 완료(09-07)**: D-1·2·3·4·5·7·9·10·12 + R-4·10·17 = 12건, 14파일, 빌드 2종 ✅ · 테스트 23/23 ✅(회귀 테스트 신설) · 롤백 `wip-symbol-3d-20260907-before-fix`(322f8c5). **§8-2 보류 8건 결정·구현 완료**(D-6 층수→높이+슬라이더 게이트 · D-8 면적→XZ 배율 · **D-11 고정/돔 헤드 제거, PTZ 만** · 나머지 의도 명시) · appsettings 소스 ON/템플릿 OFF · 테스트 **39/39** · **§8-3 실기 구동 PASS**(하네스 UiDiagnostic: 안양발전소 이동 → 55/55 3D 컨트롤 · 설치 높이→DB 60 · 비카메라 FOV 표시 · Undo 후 너비 반영 · 예외 0) · **§8-4 속성 전수 스윕 3회**(크기·색·링·회전·설치높이·FOV 4종·탐지 3종·모델변형 3종·건물종류 6종·층수/지하/용도·어도너→속성창·지도 줌 = 전부 3D 반영 확인, 면적/기준방향은 미세 한계) · **§8-4-1 사용자 지적 2건 해결**(제목 탐색 = 하네스 결함 수정 후 DB 왕복 확인 · **링=마커 박스 비례**로 변경, 대조표 검증) | 2026-09-07 |
| [pidsgroup-3d-fence-gate-scenario-analysis.md](analyses/pidsgroup-3d-fence-gate-scenario-analysis.md) | **PIDS 그룹 3D 철망·통문(Gate)·함체 개폐 시나리오 분석** — 6축 코드 조사(144 fact) → 결론 12(2.5D 지면 단축 보정, `gate` 키 충돌→`fencegate`, 서버 정본 정렬 R1, 접점 이벤트 큐 오염, 팬 경로 재생성 금지, COLUMN_SPECS 부팅 크래시 함정), 영향면 6절, 데스크 시뮬 22, 이슈 20, 결정 D1~D9/R1~R5 | 2026-09-07 |
| [symbol-3d-housing-handoff-review-analysis.md](analyses/symbol-3d-housing-handoff-review-analysis.md) | **인계 문서 검토·검증** — 7축 병렬 + 축별 적대 재검증(15에이전트·555툴콜), 주장 62건 대조(CONFIRMED 44·결함 17·불가 6). **판정: 그대로 넘기면 안 됨 — 수정 후 가능**. high 5건 — D-1 "코드 변경 0"이 거짓(미커밋 신규 19경로+수정 27파일, 베이스 3멤버 이미 랜딩, HEAD 히트 0) · D-2 "드래그 0건"이 자기 인용 규칙과 배치(캡처 드래그 22파일, OLE 만 0) + `SymbolPaletteView.cs:158` DoDragDrop 이 Must Never 위반 · D-3 가산성 이미 깨짐(군대부호 DP 이관·W/H 바인딩 모드 조건부) · D-4 금지문이 안전한 GMaps.Db 지목, 진짜 위험은 Accounts.Db DROP DATABASE · D-5 성능 계측 실재(0.100ms@N=100 오프스크린). 누락 최상위 = window-arch PRD 불가침 경계(§12 D-1 "즉시 동결") 미반영. §3 앵커 14/14·정정 E-1~E-4·결정 15건·수치 2,032/중복0 은 **검증 통과** | 2026-09-07 |
| [symbol-3d-housing-scenario-analysis.md](analyses/symbol-3d-housing-scenario-analysis.md) | 3D 심볼 하우징 **시나리오 기반 분석** — 영향면 6축 103건(차단 19) · 정본 §7-2 오류 4건(E-1~E-4) 정정 · 시뮬 3회(1,299→1,812→1,933건) · 정책 P-1~P-16 · Gap G-1~G-15(전건 확정) · PRD 적대적 검증 55건 반영 · §4-B 팔레트/드래그 배치·속성 매칭 | 2026-09-04 |
| [symbol-3d-catalog-analysis.md](analyses/symbol-3d-catalog-analysis.md) | 심볼 3D **전수 카탈로그**(군대부호 제외 42종) — 실측: PIDS 21종 중 전용 아트 6종뿐 · `VEHICLES` 카테고리는 아트 0 + **배치 경로 미구현**(MapViewModel 4340, VF-15) · 건물 `Factory` 1종 · 처리 등급 A 하우징/B 2.5D 리본/C 그림자/D 제외 · 신규 24종(차량 5 · 건물/인프라 11 · PIDS 8) 모델 스펙 · OBJ 자산 규약 · 결정 4건 | 2026-09-04 |
| [symbol-3d-feasibility-analysis.md](analyses/symbol-3d-feasibility-analysis.md) | 지도 심볼 3D화 실현성 — 6축 조사 + 반증 5건 **전부 뒤집힘**: 마커 템플릿 내부 Viewport3D 하우징은 히트·어도너·드래그 계약 0줄 유지+회전 자동 상속(Shape 교체만 금지) · 단일 3D 층은 타일과만 정합(마커 ±1px) · 스프라이트 기각은 양자화라 연속회전 3D엔 무관(조준 정확도=FOV 벡터) · (A) 임계경로=코드(enum 절차 합성→3D 합성기) · 회전 기본값 결정 이미 확정. 권고: A-1 2D 회전 갤러리(자산 0) → PoC-E1′(N=100/350/500 계측) → Phase 1 2.5D+하우징 6종. **§7 사용자 방향 확정(사실적 3D 하우징)+WGS-84 답+배선 직접 검증** | 2026-08-07 |
| [report-template-is-public-analysis.md](analyses/report-template-is-public-analysis.md) | 보고서 템플릿 공개/비공개(is_public) — 설계 의도=**읽기전용 공유**(소유자+공개만 목록, 비공개 상세 403, 수정·삭제는 소유자 전용)이나 **3계층 전부 미집행**(서버 목록 WHERE 없음·owner_id 영구 NULL·클라 분기 0건) → 현재는 순수 메타데이터. 부수결함 I-1 PATCH lost update · I-2 생성응답 is_public 침묵 false · I-3 RBAC 게이팅 미배선. **§8 서버 준비도**: 인프라 완비(코드 ~10줄)·실차단 2건(기존 owner NULL·클라 breaking). **→ D안 UI 제거 적용 완료** | 2026-08-07 |
| [event-mapping-workbench-scenario-analysis.md](analyses/event-mapping-workbench-scenario-analysis.md) | 이벤트 매핑 워크벤치 시나리오 기반 분석 v1.1 — 영향면 6축(파생객체 SET NULL 고아 7경로 포함) · **결함 DF-1~28**(D1 config PK 부재·D2 nested 침묵오염 실증) · **정책공백 G-1~10**(사용자 결정) · 마이그레이션 MG-1~5 · 적대 검증 3기로 자체 오진 3건 정정 | 2026-08-07 |
| [single-instance-guard-scenario-analysis.md](analyses/single-instance-guard-scenario-analysis.md) | 뮤텍스 가드(v2.8.1) 시나리오 사후검증 — 카탈로그 44행·적대 8가설(전부 기각)·실측 10/10 PASS. **P1 확정: 빠른 재기동 데드존(SIM-M005a 재현)→v2.8.2 WaitOne 인수인계 수정**, P2 백로그 5건(와치독 강행 체인·크로스유저 fail-open 등) | 2026-08-07 |
| [redundant-execution-boot-race-analysis.md](analyses/redundant-execution-boot-race-analysis.md) | 재부팅 후 Redundant 2창 — 최소 3기동(1생존+2자멸) 타임라인 확정, 구조 결함=뮤텍스 없는 이름카운트+모달 블로킹(상호 자멸), 잉여 주체는 계측 부재로 미확정, 권고 R1~R5 | 2026-08-07 |
| [verified-findings-backlog.md](analyses/verified-findings-backlog.md) | verified-findings-backlog.md | 2026-08-06 |
| [detection-history-bughunt-scenario-analysis.md](analyses/detection-history-bughunt-scenario-analysis.md) | detection-history-bughunt-scenario | 2026-08-06 |
| [gmap-label-offset-domain-mismatch-analysis.md](analyses/gmap-label-offset-domain-mismatch-analysis.md) | gmap-label-offset-domain-mismatch | 2026-08-06 |
| [zoom-float-halfstep-scenario-analysis.md](analyses/zoom-float-halfstep-scenario-analysis.md) | zoom-float-halfstep-scenario | 2026-08-06 |
| [event-panel-silent-load-failure-analysis.md](analyses/event-panel-silent-load-failure-analysis.md) | event-panel-silent-load-failure | 2026-08-04 |
| [harness-customization-manifest.md](analyses/harness-customization-manifest.md) | harness-customization-manifest.md | 2026-08-04 |
| [theme-contrast-audit-analysis.md](analyses/theme-contrast-audit-analysis.md) | theme-contrast-audit | 2026-08-04 |
| [map-25d-rotation-sync-analysis.md](analyses/map-25d-rotation-sync-analysis.md) | map-25d-rotation-sync | 2026-08-03 |
| [logout-on-exit-analysis.md](analyses/logout-on-exit-analysis.md) | logout-on-exit | 2026-08-03 |
| [map-3d-visualization-analysis.md](analyses/map-3d-visualization-analysis.md) | map-3d-visualization | 2026-08-03 |
| [ui-automation-scenario-spec-analysis.md](analyses/ui-automation-scenario-spec-analysis.md) | ui-automation-scenario-spec | 2026-08-03 |
| [playwright-self-verification-analysis.md](analyses/playwright-self-verification-analysis.md) | playwright-self-verification | 2026-08-03 |
| [session-scenario-simulation-analysis.md](analyses/session-scenario-simulation-analysis.md) | session-scenario-simulation | 2026-08-03 |
| [Controller_Blackout_Propagation-analysis.md](analyses/Controller_Blackout_Propagation-analysis.md) | Controller_Blackout_Propagation | 2026-07-31 |
| [GIS_Nats_Spec_Gap-analysis.md](analyses/GIS_Nats_Spec_Gap-analysis.md) | GIS_Nats_Spec_Gap | 2026-07-30 |
| [GMap_Rotation_P0_Spikes_V02_V08-analysis.md](analyses/GMap_Rotation_P0_Spikes_V02_V08-analysis.md) | GMap_Rotation_P0_Spikes_V02_V08 | 2026-07-30 |
| [GMap_Rotation_Full_Sync_PRD_Adversarial_Review-analysis.md](analyses/GMap_Rotation_Full_Sync_PRD_Adversarial_Review-analysis.md) | GMap_Rotation_Full_Sync_PRD_Adversarial_Review | 2026-07-28 |
| [GMap_Rotation_Enable_BugInventory_v2-analysis.md](analyses/GMap_Rotation_Enable_BugInventory_v2-analysis.md) | GMap_Rotation_Enable_BugInventory_v2 | 2026-07-28 |
| [CameraPopup_PanClamp_Badge_OnvifPtz-analysis.md](analyses/CameraPopup_PanClamp_Badge_OnvifPtz-analysis.md) | CameraPopup_PanClamp_Badge_OnvifPtz | 2026-07-23 |
| [Overlay_Title_ZoomStyle-analysis.md](analyses/Overlay_Title_ZoomStyle-analysis.md) | Overlay_Title_ZoomStyle | 2026-07-23 |
| [GMap_OverlayWindow_Symbol_Visibility_Restore-analysis.md](analyses/GMap_OverlayWindow_Symbol_Visibility_Restore-analysis.md) | GMap_OverlayWindow_Symbol_Visibility_Restore | 2026-07-20 |
| [VSCode_Terminal_Tangling-analysis.md](analyses/VSCode_Terminal_Tangling-analysis.md) | VSCode_Terminal_Tangling | 2026-07-20 |
| [Grant_Enforcement_Client_Analysis.md](analyses/Grant_Enforcement_Client_Analysis.md) | Grant_Enforcement_Client_Analysis.md | 2026-07-20 |
| [Grant_Enforcement_Server_Analysis.md](analyses/Grant_Enforcement_Server_Analysis.md) | Grant_Enforcement_Server_Analysis.md | 2026-07-20 |
| [GMap_Anchor_Boundary_Bounce_Flicker_Jerking-analysis.md](analyses/GMap_Anchor_Boundary_Bounce_Flicker_Jerking-analysis.md) | GMap_Anchor_Boundary_Bounce_Flicker_Jerking | 2026-07-20 |
| [GMap_Pan_Overlay_Temporal_Desync-analysis.md](analyses/GMap_Pan_Overlay_Temporal_Desync-analysis.md) | GMap_Pan_Overlay_Temporal_Desync | 2026-07-19 |
| [GMap_EditMode_ArrowKey_FocusSelectionBox-analysis.md](analyses/GMap_EditMode_ArrowKey_FocusSelectionBox-analysis.md) | GMap_EditMode_ArrowKey_FocusSelectionBox | 2026-07-19 |
| [GMap_Rotation_FullSync_Design-analysis.md](analyses/GMap_Rotation_FullSync_Design-analysis.md) | GMap_Rotation_FullSync_Design | 2026-07-19 |
| [GMap_Rotation_FullSync_Design_Adversarial_Review-analysis.md](analyses/GMap_Rotation_FullSync_Design_Adversarial_Review-analysis.md) | GMap_Rotation_FullSync_Design_Adversarial_Review | 2026-07-18 |
| [GMap_Rotation_Overlay_Desync-analysis.md](analyses/GMap_Rotation_Overlay_Desync-analysis.md) | GMap_Rotation_Overlay_Desync | 2026-07-18 |
| [GMap_UI_Edit_Undo_Redo_MultiSelection-analysis.md](analyses/GMap_UI_Edit_Undo_Redo_MultiSelection-analysis.md) | GMap_UI_Edit_Undo_Redo_MultiSelection | 2026-07-15 |
| [Rtsp_Popup_Streaming_Ptz-analysis.md](analyses/Rtsp_Popup_Streaming_Ptz-analysis.md) | Rtsp_Popup_Streaming_Ptz | 2026-07-15 |
| [GIS_Nats_Simulation_Verification.md](analyses/GIS_Nats_Simulation_Verification.md) | GIS_Nats_Simulation_Verification.md | 2026-07-13 |
| [GMap_RDP_Overlay_Desync-analysis.md](analyses/GMap_RDP_Overlay_Desync-analysis.md) | GMap_RDP_Overlay_Desync | 2026-07-07 |
| [GOP_Server_API_rbac_matrix_gate_NOTIFY.md](analyses/GOP_Server_API_rbac_matrix_gate_NOTIFY.md) | GOP_Server_API_rbac_matrix_gate_NOTIFY.md | 2026-07-07 |
| [Map_Edit_Undo_Redo-analysis.md](analyses/Map_Edit_Undo_Redo-analysis.md) | Map_Edit_Undo_Redo | 2026-07-02 |
| [GMap_Adorner_System-analysis.md](analyses/GMap_Adorner_System-analysis.md) | GMap_Adorner_System | 2026-07-02 |
| [Tracking_Playback_DataSource_Toggle-analysis.md](analyses/Tracking_Playback_DataSource_Toggle-analysis.md) | Tracking_Playback_DataSource_Toggle | 2026-06-26 |
| [Tracking_API_vs_LocalDB-analysis.md](analyses/Tracking_API_vs_LocalDB-analysis.md) | Tracking_API_vs_LocalDB | 2026-06-26 |
| [Http_To_Https_Migration_Impact-analysis.md](analyses/Http_To_Https_Migration_Impact-analysis.md) | Http_To_Https_Migration_Impact | 2026-06-25 |
| [GOP_Restful_Api_v4.9_Review-analysis.md](analyses/GOP_Restful_Api_v4.9_Review-analysis.md) | GOP_Restful_Api_v4.9_Review | 2026-06-24 |
| [nats-tracking-message-flow-analysis.md](analyses/nats-tracking-message-flow-analysis.md) | nats-tracking-message-flow | 2026-06-23 |
| [server_api_grounding.md](analyses/server_api_grounding.md) | server_api_grounding.md | 2026-06-22 |
| [Client_API_Conformance_Audit-analysis.md](analyses/Client_API_Conformance_Audit-analysis.md) | Client_API_Conformance_Audit | 2026-06-19 |
| [DeviceGroup_BatchRemove_API_Request.md](analyses/DeviceGroup_BatchRemove_API_Request.md) | DeviceGroup_BatchRemove_API_Request.md | 2026-06-19 |
| [GOP_API_v4_Changes_ClientImpact-analysis.md](analyses/GOP_API_v4_Changes_ClientImpact-analysis.md) | GOP_API_v4_Changes_ClientImpact | 2026-06-19 |
| [OverlayImage_Rotation_Zoom_AABB_RootCause-analysis.md](analyses/OverlayImage_Rotation_Zoom_AABB_RootCause-analysis.md) | OverlayImage_Rotation_Zoom_AABB_RootCause | 2026-06-12 |
| [RapidEventBurst_Stutter_Analysis.md](analyses/RapidEventBurst_Stutter_Analysis.md) | RapidEventBurst_Stutter_Analysis.md | 2026-06-04 |
| [Multisensor_Symbol_Bug_Analysis.md](analyses/Multisensor_Symbol_Bug_Analysis.md) | Multisensor_Symbol_Bug_Analysis.md | 2026-06-04 |
| [EventStateSyncArchitecture_Analysis.md](analyses/EventStateSyncArchitecture_Analysis.md) | EventStateSyncArchitecture_Analysis.md | 2026-06-04 |
| [BatchReport_SymbolLeak_Analysis.md](analyses/BatchReport_SymbolLeak_Analysis.md) | BatchReport_SymbolLeak_Analysis.md | 2026-06-04 |
| [OverlayMap-Performance-Analysis.md](analyses/OverlayMap-Performance-Analysis.md) | OverlayMap-Performance-Analysis.md | 2026-05-27 |
| [EVENT_PROCESS_VISUALIZATION.md](analyses/EVENT_PROCESS_VISUALIZATION.md) | EVENT_PROCESS_VISUALIZATION.md | 2026-05-22 |
| [ANALYSIS_Skillset_Issues_And_Improvements.md](analyses/ANALYSIS_Skillset_Issues_And_Improvements.md) | ANALYSIS_Skillset_Issues_And_Improvements.md | 2026-05-19 |
| [ANALYSIS_View_Architecture.md](analyses/ANALYSIS_View_Architecture.md) | ANALYSIS_View_Architecture.md | 2026-05-18 |
| [ANALYSIS_Detection_Action_Process_Flow.md](analyses/ANALYSIS_Detection_Action_Process_Flow.md) | ANALYSIS_Detection_Action_Process_Flow.md | 2026-05-18 |
| [NATS_Detection_Redis_Flow.md](analyses/NATS_Detection_Redis_Flow.md) | NATS_Detection_Redis_Flow.md | 2026-05-15 |
| [ANALYSIS_GatewayEvent_Group_NtoN_Migration.md](analyses/ANALYSIS_GatewayEvent_Group_NtoN_Migration.md) | ANALYSIS_GatewayEvent_Group_NtoN_Migration.md | 2026-05-15 |

## 요구사항 정의서 (docs/prds/)

| 파일 | 내용 | 상태 | 날짜 |
|------|------|------|------|
| [map-tilt-25d-prd.md](prds/map-tilt-25d-prd.md) | **지도 카드 틸트(2.5D)** — 사용자 결정 ①살짝 비스듬(아핀 ScaleY=cosφ, 각도 조절, 오버스캔) ②줌 게이트(MinZoom 18.0, 히스테리시스 0.5) ③베어링 회전 시 아이콘 정립(빌보드). 근거=[시나리오 분석](analyses/map-tilt-25d-scenario-analysis.md)(시뮬 4,096+22·V-02 헤드리스 확정). **FR-01~16**: 뷰 변환 단일 빌더·`TiltMath.Decide` 순수 판정·게이트 3지점 배선·레이아웃 오버스캔(150 ms 커밋)·InnerToOuter 확장·스냅샷 TiltCos·설정/영속(메인 SetupModel 1줄)·UI(툴바 토글+슬라이더·Ctrl+Shift+T·Ctrl+↑/↓)·축척 배지·`IsBillboard` 계약+FOV 1회 합성+히트 파리티·Tier0 강등·3D 정책(i)·테스트·문서. 결정 G1~G10 승인값 §0 | **Review** | 2026-09-08 |
| [window-architecture-prd.md](prds/window-architecture-prd.md) | **창 아키텍처 재설계 — Surface Kernel(범위 B)** · 근거=[analysis](analyses/window-architecture-analysis.md) · 동반 [스토리보드](design/window-surface-kernel-storyboard.html). **FR-01~40**(구조·API·테마, 150점) + **§11 권한 FR-41~66**(전수감사 → 대장 181행, 164점) + **§12 맵 OverlayWindow 불가침 경계**(사용자 지시). §12 = 3중 판정식(시각트리 부모 ∩ 디렉터리 ∩ Themes 프리픽스) · **동결 59파일**(구현 29+스타일 29+`PropertyPanelCanvas` 서브트리) · `MapView.xaml` 3구역(A수정허용/B개별승인/C전면금지) · **침범 FR 24건**(직접 9·간접 9·문서모순 6) + 수정지시문 A-01~A-24 · 무침범 확인 41건 · 집행 7겹(제안, 미적용) · 부작용 9종 · 타PRD 충돌 매트릭스 · 별건 후보 B-1~B-7. **최대 위험 A-06**: `SurfaceHostView`가 `Background` 규정 없이 ColumnSpan=4 → **표면 0개 상태에서 오버레이 전멸**(파일 무수정 경로). **A-05**: 드로어 400px가 `MapZoomControl` `Margin=5,5,300,5` 하드코딩을 덮음 → 범위 200~300 축소. 실측 정정 4건(무음차단 6→7 · 무음실패 59 · 감사공수 178→164 · 경계목록 누락 3계열+`LineDrawingHud` 오분류). 공수 **314→316점**(FR-24 6→8), 계획선 12.5→**13.0주**. 결정 대기: 범위 Tier(§11-8) · Q-01~14 · **D-1~D-7** | **Review** | 2026-09-07 |
| [action-report-custom-template-prd.md](prds/action-report-custom-template-prd.md) | Draft **v1.1** | 조치보고 커스텀 문구 — 하드코딩 5종 → 서버 템플릿 + 문구 관리 패널 + 정형문구 자유편집. FR-18 · NFR-07 · V-07 · **미결 G1~G8** | 2026-08-07 |
| [event-suppression-recurrence-prd.md](prds/event-suppression-recurrence-prd.md) | **이벤트 억제 주간 반복 대응**(서버 API 6.3.3) — 베이스라인 [event-suppression-schedule-prd](prds/event-suppression-schedule-prd.md) v1.4 의 증분. FR-01~18 · NFR-01~07 · **D-1~D-8**(D-7/D-8만 사용자 결정) · V-01~06 · **함정 13종**. ★ 서버가 "제일 중요"라 한 `window_end`→`occurrence_end` 타이머는 **GIS에 고칠 코드 0건**(소비처=표시 2곳) — 실제 무게는 **표시 정직성**(`status`≠억제중, 유효기간의 62.2%가 active면서 미억제) + **반복 생성 UI** + **기존 결함 B-1~B-6 정리**(사문 Monitor·stale 배너·fail-open 부재·중복경고 시간미비교·SYNC 핸들러 부재·PATCH UI 부재). **FR-18 = POST/PATCH DTO 공유 잠복함정**(반복필드 추가 시 전 PATCH 422). ⚠ 메인솔루션 1케이스(FR-17, 사전통지). **UI 와이어프레임·스토리보드 동반**(사용자 지시) | **Approved** 2026-09-07 | 2026-09-07 |
| [group-symbol-transform-prd.md](prds/group-symbol-transform-prd.md) | **그룹 심볼 변환**(러버밴드 전체선택 → 선택영역 중심 회전/확대·축소) — inner 공간 base 스냅샷 1회 합성 · 계열별 분기 · 배치 트랜잭션 · 세션 가드. **v2.0(9/8): 3D 심볼·틸트 도입 재검토** — 빌보드 분기를 데이터 기준으로(설치별 3D 플래그), base 스냅샷을 투영픽셀로(오버스캔이 로컬 px를 Δ 이동), 3D 프리뷰는 2D 고스트. FR-01~19 · NFR 6 · V-01~05 · GAP 22(착수 전 결정 10건) | **Draft** | 2026-09-08 |
| [symbol-3d-housing-prd.md](prds/symbol-3d-housing-prd.md) | 지도 심볼 **3D 하우징** — 베이스 seam·`RotatesIn2D` 플래그 분리·FOV 동기 보정·W/H 3채널 차단·N3 정규화·OBJ 로더·kill-switch. **v2.7** FR-01~20(예상 태스크 56) · **미결 0**(G-1~15 전부 코드 근거로 확정, G-15 철회) — 3D 하우징 + **팔레트 미리보기·드래그 배치**(FR-16~18) + 속성 ↔ 3D 매칭(FR-19) · V-01~15 · 채택 G-1/2/7/9/10/**13** + 미사용 필드 6종 신규 매핑 · 형상 재설계 2회(고정형/돔/PTZ 분리 · 스피커 실물 비율) · 인계: [symbol-3d-housing-handoff.md](coordination/symbol-3d-housing-handoff.md) | **Draft** | 2026-09-04 |
| [pidsgroup-3d-fence-gate-prd.md](prds/pidsgroup-3d-fence-gate-prd.md) | **PIDS 그룹 3D 철망 · 통문 개폐 · 함체 개폐 PRD v1.0 Draft** — FR-01~20(Phase 1: 드래그 드로잉·FenceLayout 순수함수·슬라이더 지연 커밋·기둥/센서 형태·FenceRunVisual·LOD(px 기준)·Gate enum 21 전수·fencegate/enclosure 관절·DoorState 비영속 3채널 수렴(SYNC_DEVICE/OPERATION_EVENT/ContactOn 폴백)·접점 큐 제외·DB 자가치유·설정·하네스·서버 5싱크) + Phase 2(구간 상태·정점 편집·차량) · V-01~08 · 리스크 7 · DoD | 2026-09-07 |
| [event-mapping-workbench-prd.md](prds/event-mapping-workbench-prd.md) | 이벤트 매핑 워크벤치 — EventMapping 4리소스(본체/카메라/스피커/경광등) CRUD + **Drag&Drop 멀티셀렉션 + Draft 커밋**. 착수 전 결함 D1~D10 해소 · 실측 8항목 선행 · **결정 10건(G-1~G-10) + 메인솔루션 4파일 승인 대기** | **Draft** | 2026-08-07 |
| [map-topbar-trafficlight-prd.md](prds/map-topbar-trafficlight-prd.md) | map-topbar-trafficlight | Approved | 2026-08-06 |
| [zoom-float-halfstep-prd.md](prds/zoom-float-halfstep-prd.md) | zoom-float-halfstep | Approved | 2026-08-06 |
| [pidsgroup-rightclick-prd.md](prds/pidsgroup-rightclick-prd.md) | pidsgroup-rightclick | Approved | 2026-08-06 |
| [UI_Automation_Instrumentation_Phase2-prd.md](prds/UI_Automation_Instrumentation_Phase2-prd.md) | UI_Automation_Instrumentation_Phase2 | Draft | 2026-08-05 |
| [Overlay_Image_Canonical_Join-prd.md](prds/Overlay_Image_Canonical_Join-prd.md) | Overlay_Image_Canonical_Join | Draft | 2026-08-05 |
| [Event_Silent_Failure_Elimination-prd.md](prds/Event_Silent_Failure_Elimination-prd.md) | Event_Silent_Failure_Elimination | Draft | 2026-08-05 |
| [installer-prd.md](prds/installer-prd.md) | installer | Approved | 2026-08-04 |
| [GMap_Schema_Migration_Idempotency-prd.md](prds/GMap_Schema_Migration_Idempotency-prd.md) | GMap_Schema_Migration_Idempotency | Approved | 2026-08-04 |
| [UI_Functional_Testing_Layer-prd.md](prds/UI_Functional_Testing_Layer-prd.md) | UI_Functional_Testing_Layer | Approved | 2026-08-04 |
| [Event_Edit_Save_Pipeline-prd.md](prds/Event_Edit_Save_Pipeline-prd.md) | Event_Edit_Save_Pipeline | Draft | 2026-08-03 |
| [UI_Automation_FlaUI_Smoke-prd.md](prds/UI_Automation_FlaUI_Smoke-prd.md) | UI_Automation_FlaUI_Smoke | Approved | 2026-08-03 |
| [GMap_PidsGroup_DoubleClick_ActionReport-prd.md](prds/GMap_PidsGroup_DoubleClick_ActionReport-prd.md) | GMap_PidsGroup_DoubleClick_ActionReport | Approved | 2026-08-03 |
| [logout-on-exit-prd.md](prds/logout-on-exit-prd.md) | logout-on-exit | Draft | 2026-08-03 |
| [Event_Enum_Korean_Display-prd.md](prds/Event_Enum_Korean_Display-prd.md) | Event_Enum_Korean_Display | Approved | 2026-08-03 |
| [GMap_Rotation_Full_Sync-prd.md](prds/GMap_Rotation_Full_Sync-prd.md) | GMap_Rotation_Full_Sync | Approved | 2026-08-03 |
| [event-suppression-schedule-prd.md](prds/event-suppression-schedule-prd.md) | event-suppression-schedule | Draft | 2026-08-03 |
| [session-management-overhaul-prd.md](prds/session-management-overhaul-prd.md) | session-management-overhaul | Approved | 2026-08-03 |
| [Controller_Fault_AutoRecovery_Extension-prd.md](prds/Controller_Fault_AutoRecovery_Extension-prd.md) | Controller_Fault_AutoRecovery_Extension | Draft | 2026-07-31 |
| [GMap_Controller_Blackout_Runtime_Fix-prd.md](prds/GMap_Controller_Blackout_Runtime_Fix-prd.md) | GMap_Controller_Blackout_Runtime_Fix | Draft | 2026-07-31 |
| [action-report-origin-thumbnail-prd.md](prds/action-report-origin-thumbnail-prd.md) | action-report-origin-thumbnail | Draft | 2026-07-31 |
| [detection-sync-thumbnail-prd.md](prds/detection-sync-thumbnail-prd.md) | detection-sync-thumbnail | Approved | 2026-07-31 |
| [GMap_Controller_Blackout-prd.md](prds/GMap_Controller_Blackout-prd.md) | GMap_Controller_Blackout | Draft | 2026-07-31 |
| [malfunction-autoreport-setting-prd.md](prds/malfunction-autoreport-setting-prd.md) | malfunction-autoreport-setting | Approved | 2026-07-31 |
| [GMap_Map_Instruments-prd.md](prds/GMap_Map_Instruments-prd.md) | GMap_Map_Instruments | Draft | 2026-07-31 |
| [GOP_Logging_Observability_P0-prd.md](prds/GOP_Logging_Observability_P0-prd.md) | GOP_Logging_Observability_P0 | Draft | 2026-07-31 |
| [GOP_Nats_Req_Failure_UX-prd.md](prds/GOP_Nats_Req_Failure_UX-prd.md) | GOP_Nats_Req_Failure_UX | Approved | 2026-07-30 |
| [GMap_Compass_Control-prd.md](prds/GMap_Compass_Control-prd.md) | GMap_Compass_Control | Draft | 2026-07-30 |
| [GIS_Nats_v152_Req_Transition-prd.md](prds/GIS_Nats_v152_Req_Transition-prd.md) | GIS_Nats_v152_Req_Transition | Approved | 2026-07-30 |
| [GIS.md](prds/GIS.md) | GIS.md | Draft | 2026-07-30 |
| [line-drawing-hud-redesign-prd.md](prds/line-drawing-hud-redesign-prd.md) | line-drawing-hud-redesign | Draft | 2026-07-28 |
| [Measure_Tools-prd.md](prds/Measure_Tools-prd.md) | Measure_Tools | Approved | 2026-07-27 |
| [CameraPopup_ControlHub-prd.md](prds/CameraPopup_ControlHub-prd.md) | CameraPopup_ControlHub | Approved | 2026-07-27 |
| [Overlay_Title_ZoomStyle-prd.md](prds/Overlay_Title_ZoomStyle-prd.md) | Overlay_Title_ZoomStyle | Approved | 2026-07-27 |
| [Detection_Signal_History-prd.md](prds/Detection_Signal_History-prd.md) | Detection_Signal_History | Approved | 2026-07-27 |
| [CameraPopup_PanClamp_Badge_OnvifPtz-prd.md](prds/CameraPopup_PanClamp_Badge_OnvifPtz-prd.md) | CameraPopup_PanClamp_Badge_OnvifPtz | Approved | 2026-07-23 |
| [Symbol_ContextMenu_ViewMode_Lock-prd.md](prds/Symbol_ContextMenu_ViewMode_Lock-prd.md) | Symbol_ContextMenu_ViewMode_Lock | Approved | 2026-07-23 |
| [ContextMenu_DisplayRules-prd.md](prds/ContextMenu_DisplayRules-prd.md) | ContextMenu_DisplayRules | Superseded | 2026-07-23 |
| [GOP_SessionGrant_Pagination-prd.md](prds/GOP_SessionGrant_Pagination-prd.md) | GOP_SessionGrant_Pagination | Draft | 2026-07-23 |
| [GOP_AuditLog_DateFilter_Pagination-prd.md](prds/GOP_AuditLog_DateFilter_Pagination-prd.md) | GOP_AuditLog_DateFilter_Pagination | Draft | 2026-07-23 |
| [GMap_Symbol_Visibility_Master-prd.md](prds/GMap_Symbol_Visibility_Master-prd.md) | GMap_Symbol_Visibility_Master | Draft | 2026-07-20 |
| [MyPage_SelfPhoto_Delete_Fix-prd.md](prds/MyPage_SelfPhoto_Delete_Fix-prd.md) | MyPage_SelfPhoto_Delete_Fix | Draft | 2026-07-20 |
| [GMap_Symbol_Visibility_Restore-prd.md](prds/GMap_Symbol_Visibility_Restore-prd.md) | GMap_Symbol_Visibility_Restore | Draft | 2026-07-20 |
| [Admin_Photo_Upload-prd.md](prds/Admin_Photo_Upload-prd.md) | Admin_Photo_Upload | Draft | 2026-07-20 |
| [Grant_LiveCutoff_NATS_Push-prd.md](prds/Grant_LiveCutoff_NATS_Push-prd.md) | Grant_LiveCutoff_NATS_Push | Draft | 2026-07-20 |
| [Grant_LiveCutoff_Client-prd.md](prds/Grant_LiveCutoff_Client-prd.md) | Grant_LiveCutoff_Client | Draft | 2026-07-20 |
| [GrantList_TopLevelTotal_Fix-prd.md](prds/GrantList_TopLevelTotal_Fix-prd.md) | GrantList_TopLevelTotal_Fix | Draft | 2026-07-20 |
| [EventChart_Tooltip_ThemeSync-prd.md](prds/EventChart_Tooltip_ThemeSync-prd.md) | EventChart_Tooltip_ThemeSync | Approved | 2026-07-20 |
| [GMap_Anchor_Viewport_Lock-prd.md](prds/GMap_Anchor_Viewport_Lock-prd.md) | GMap_Anchor_Viewport_Lock | Approved | 2026-07-19 |
| [GMap_PidsCamera_FOV_Toggle_Persistence-prd.md](prds/GMap_PidsCamera_FOV_Toggle_Persistence-prd.md) | GMap_PidsCamera_FOV_Toggle_Persistence | Approved | 2026-07-18 |
| [GMap_Lock_Selection_ZOrder_Integrity-prd.md](prds/GMap_Lock_Selection_ZOrder_Integrity-prd.md) | GMap_Lock_Selection_ZOrder_Integrity | Approved | 2026-07-15 |
| [DeviceStatusSync_ActionReportPropagation-prd.md](prds/DeviceStatusSync_ActionReportPropagation-prd.md) | DeviceStatusSync_ActionReportPropagation | Draft | 2026-07-15 |
| [CameraPopup_RtspSource_Priority-prd.md](prds/CameraPopup_RtspSource_Priority-prd.md) | CameraPopup_RtspSource_Priority | Approved | 2026-07-15 |
| [GIS_Nats_Full_Integration-prd.md](prds/GIS_Nats_Full_Integration-prd.md) | GIS_Nats_Full_Integration | Draft | 2026-07-13 |
| [GMap_SystemResource_Indicator-prd.md](prds/GMap_SystemResource_Indicator-prd.md) | GMap_SystemResource_Indicator | Approved | 2026-07-13 |
| [GOP_Server_API_GIS_v6.3_전달통지.md](prds/GOP_Server_API_GIS_v6.3_전달통지.md) | GOP_Server_API_GIS_v6.3_전달통지.md | Draft | 2026-07-13 |
| [LineArea_Symbol_Resize-prd.md](prds/LineArea_Symbol_Resize-prd.md) | LineArea_Symbol_Resize | Approved | 2026-07-13 |
| [FullScreen_F11_Toggle-prd.md](prds/FullScreen_F11_Toggle-prd.md) | FullScreen_F11_Toggle | Approved | 2026-07-13 |
| [MapSymbol_Shortcut_CopyPasteDelete-prd.md](prds/MapSymbol_Shortcut_CopyPasteDelete-prd.md) | MapSymbol_Shortcut_CopyPasteDelete | Approved | 2026-07-13 |
| [LeftMenu_IntegratedWeb_Button-prd.md](prds/LeftMenu_IntegratedWeb_Button-prd.md) | LeftMenu_IntegratedWeb_Button | Approved | 2026-07-13 |
| [GMap_Zoom_Anchor_Home-prd.md](prds/GMap_Zoom_Anchor_Home-prd.md) | GMap_Zoom_Anchor_Home | Draft | 2026-07-13 |
| [Watchdog_Modern_Rebuild-prd.md](prds/Watchdog_Modern_Rebuild-prd.md) | Watchdog_Modern_Rebuild | Approved | 2026-07-13 |
| [Startup_Unresolved_Fault_Reconciliation-prd.md](prds/Startup_Unresolved_Fault_Reconciliation-prd.md) | Startup_Unresolved_Fault_Reconciliation | Draft | 2026-07-11 |
| [GMap_RDP_Overlay_Desync-prd.md](prds/GMap_RDP_Overlay_Desync-prd.md) | GMap_RDP_Overlay_Desync | Draft | 2026-07-11 |
| [EventCard_Detection_Malfunction_Refinement-prd.md](prds/EventCard_Detection_Malfunction_Refinement-prd.md) | EventCard_Detection_Malfunction_Refinement | Draft | 2026-07-11 |
| [Action_Report_Nats_FullDto_Contract-prd.md](prds/Action_Report_Nats_FullDto_Contract-prd.md) | Action_Report_Nats_FullDto_Contract | Draft | 2026-07-11 |
| [Device_Event_API_NATS_SSOT_Sync-prd.md](prds/Device_Event_API_NATS_SSOT_Sync-prd.md) | Device_Event_API_NATS_SSOT_Sync | Draft | 2026-07-07 |
| [Account_Lock_Management-prd.md](prds/Account_Lock_Management-prd.md) | Account_Lock_Management | Approved | 2026-07-07 |
| [Account_Permission_CRUD_Hardening-prd.md](prds/Account_Permission_CRUD_Hardening-prd.md) | Account_Permission_CRUD_Hardening | Draft | 2026-07-06 |
| [GOP_Force_Logout_Propagation-prd.md](prds/GOP_Force_Logout_Propagation-prd.md) | GOP_Force_Logout_Propagation | Approved | 2026-07-05 |
| [DataGridPanel_CRUD_Standard_Convention-prd.md](prds/DataGridPanel_CRUD_Standard_Convention-prd.md) | DataGridPanel_CRUD_Standard_Convention | Draft | 2026-07-05 |
| [Report_Client_v6_Integration-prd.md](prds/Report_Client_v6_Integration-prd.md) | Report_Client_v6_Integration | Draft | 2026-07-05 |
| [GMap_Edit_Integration_Manual-prd.md](prds/GMap_Edit_Integration_Manual-prd.md) | GMap_Edit_Integration_Manual | Draft | 2026-07-05 |
| [GMap_Edit_Integration_Sim-prd.md](prds/GMap_Edit_Integration_Sim-prd.md) | GMap_Edit_Integration_Sim | Draft | 2026-07-05 |
| [Device_Delete_Standard_Process-prd.md](prds/Device_Delete_Standard_Process-prd.md) | Device_Delete_Standard_Process | Draft | 2026-07-05 |
| [Save_Spinner_Permission_Fix-prd.md](prds/Save_Spinner_Permission_Fix-prd.md) | Save_Spinner_Permission_Fix | Approved | 2026-07-04 |
| [Panel_Design_Unification-prd.md](prds/Panel_Design_Unification-prd.md) | Panel_Design_Unification | Approved | 2026-07-03 |
| [Report_Generation_Feature-prd.md](prds/Report_Generation_Feature-prd.md) | Report_Generation_Feature | Draft | 2026-07-02 |
| [Map_Edit_Undo_Redo-prd.md](prds/Map_Edit_Undo_Redo-prd.md) | Map_Edit_Undo_Redo | Approved | 2026-07-02 |
| [Symbol_Label_Decouple-prd.md](prds/Symbol_Label_Decouple-prd.md) | Symbol_Label_Decouple | Approved | 2026-07-02 |
| [GOP_Permission_Group_Management-prd.md](prds/GOP_Permission_Group_Management-prd.md) | GOP_Permission_Group_Management | Draft | 2026-07-02 |
| [GMap_RubberBand_MultiSelect-prd.md](prds/GMap_RubberBand_MultiSelect-prd.md) | GMap_RubberBand_MultiSelect | Approved | 2026-07-02 |
| [Camera_Aim_Overlay_Animation-prd.md](prds/Camera_Aim_Overlay_Animation-prd.md) | Camera_Aim_Overlay_Animation | Approved | 2026-07-02 |
| [GMap_Delete_EditMode_BugFix-prd.md](prds/GMap_Delete_EditMode_BugFix-prd.md) | GMap_Delete_EditMode_BugFix | Approved | 2026-07-02 |
| [Grant_Scheduling_Client-prd.md](prds/Grant_Scheduling_Client-prd.md) | Grant_Scheduling_Client | Approved | 2026-07-01 |
| [Login_Gated_GIS_Init-prd.md](prds/Login_Gated_GIS_Init-prd.md) | Login_Gated_GIS_Init | Draft | 2026-06-30 |
| [GUIDE_Grant_Scheduling_Client_v5.2.md](prds/GUIDE_Grant_Scheduling_Client_v5.2.md) | GUIDE_Grant_Scheduling_Client_v5.2.md | Draft | 2026-06-30 |
| [Symbol_Lock_And_RenameSync-prd.md](prds/Symbol_Lock_And_RenameSync-prd.md) | Symbol_Lock_And_RenameSync | Draft | 2026-06-30 |
| [LayerPanel_SymbolNesting_Resize-prd.md](prds/LayerPanel_SymbolNesting_Resize-prd.md) | LayerPanel_SymbolNesting_Resize | Draft | 2026-06-30 |
| [tracking-ptz-publisher-prd.md](prds/tracking-ptz-publisher-prd.md) | tracking-ptz-publisher | Draft | 2026-06-29 |
| [GatewayEvent_Group_Resurrection_Fix-prd.md](prds/GatewayEvent_Group_Resurrection_Fix-prd.md) | GatewayEvent_Group_Resurrection_Fix | Approved | 2026-06-29 |
| [CameraPopup_PTZ_Responsiveness_Speed-prd.md](prds/CameraPopup_PTZ_Responsiveness_Speed-prd.md) | CameraPopup_PTZ_Responsiveness_Speed | Draft | 2026-06-29 |
| [GOP_Session_Settings_Admin-prd.md](prds/GOP_Session_Settings_Admin-prd.md) | GOP_Session_Settings_Admin | Draft | 2026-06-29 |
| [Symbol_Apply_DeviceLocation_Api-prd.md](prds/Symbol_Apply_DeviceLocation_Api-prd.md) | Symbol_Apply_DeviceLocation_Api | Approved | 2026-06-29 |
| [CameraPopup_PressHold_PtzZoomFocus-prd.md](prds/CameraPopup_PressHold_PtzZoomFocus-prd.md) | CameraPopup_PressHold_PtzZoomFocus | Draft | 2026-06-29 |
| [Camera_PTZ_AimLocation_Nats-prd.md](prds/Camera_PTZ_AimLocation_Nats-prd.md) | Camera_PTZ_AimLocation_Nats | Approved | 2026-06-29 |
| [GOP_Permission_Enforcement-prd.md](prds/GOP_Permission_Enforcement-prd.md) | GOP_Permission_Enforcement | Draft | 2026-06-29 |
| [GOP_Permission_Gate_Feature-prd.md](prds/GOP_Permission_Gate_Feature-prd.md) | GOP_Permission_Gate_Feature | Draft | 2026-06-29 |
| [Tracking_Playback_DataSource_Toggle-prd.md](prds/Tracking_Playback_DataSource_Toggle-prd.md) | Tracking_Playback_DataSource_Toggle | Draft | 2026-06-26 |
| [GOP_Profile_Photo_Upload-prd.md](prds/GOP_Profile_Photo_Upload-prd.md) | GOP_Profile_Photo_Upload | Draft | 2026-06-26 |
| [Tracking_GIS_Visualization_Playback-prd.md](prds/Tracking_GIS_Visualization_Playback-prd.md) | Tracking_GIS_Visualization_Playback | Draft | 2026-06-25 |
| [GOP_Account_Auth_Integration-prd.md](prds/GOP_Account_Auth_Integration-prd.md) | GOP_Account_Auth_Integration | Draft | 2026-06-25 |
| [UI_ModernTheme_DesignSystem-prd.md](prds/UI_ModernTheme_DesignSystem-prd.md) | UI_ModernTheme_DesignSystem | Draft | 2026-06-24 |
| [CameraPopup_PTZ_Control-prd.md](prds/CameraPopup_PTZ_Control-prd.md) | CameraPopup_PTZ_Control | Draft | 2026-06-23 |
| [Accounts_Ui_Library_Extraction-prd.md](prds/Accounts_Ui_Library_Extraction-prd.md) | Accounts_Ui_Library_Extraction | Draft | 2026-06-23 |
| [CameraPopup_DigitalZoom_Alignment-prd.md](prds/CameraPopup_DigitalZoom_Alignment-prd.md) | CameraPopup_DigitalZoom_Alignment | Draft | 2026-06-23 |
| [CameraPopup_Snapshot_UX-prd.md](prds/CameraPopup_Snapshot_UX-prd.md) | CameraPopup_Snapshot_UX | Draft | 2026-06-23 |
| [CameraPopup_Streaming_Settings-prd.md](prds/CameraPopup_Streaming_Settings-prd.md) | CameraPopup_Streaming_Settings | Draft | 2026-06-23 |
| [Event_FollowupAction_ContextMenu-prd.md](prds/Event_FollowupAction_ContextMenu-prd.md) | Event_FollowupAction_ContextMenu | Draft | 2026-06-22 |
| [EventPanel_Immutable_Guard-prd.md](prds/EventPanel_Immutable_Guard-prd.md) | EventPanel_Immutable_Guard | Draft | 2026-06-22 |
| [Rtsp_Map_Popup-prd.md](prds/Rtsp_Map_Popup-prd.md) | Rtsp_Map_Popup | Approved | 2026-06-22 |
| [EventPanel_CRUD_Api_Alignment-prd.md](prds/EventPanel_CRUD_Api_Alignment-prd.md) | EventPanel_CRUD_Api_Alignment | Draft | 2026-06-22 |
| [BaseMap_NoData_DefaultTile-prd.md](prds/BaseMap_NoData_DefaultTile-prd.md) | BaseMap_NoData_DefaultTile | Approved | 2026-06-22 |
| [EnclosureThresholdDialog-prd.md](prds/EnclosureThresholdDialog-prd.md) | EnclosureThresholdDialog | Draft | 2026-06-22 |
| [SpeakerServerAssignment-prd.md](prds/SpeakerServerAssignment-prd.md) | SpeakerServerAssignment | Draft | 2026-06-22 |
| [DevicePropertyPanel_Layout_Redesign-prd.md](prds/DevicePropertyPanel_Layout_Redesign-prd.md) | DevicePropertyPanel_Layout_Redesign | Draft | 2026-06-22 |
| [DevicePanel_TempState_Unification-prd.md](prds/DevicePanel_TempState_Unification-prd.md) | DevicePanel_TempState_Unification | Draft | 2026-06-20 |
| [GOP_UserSession_AuditLog_UI-prd.md](prds/GOP_UserSession_AuditLog_UI-prd.md) | GOP_UserSession_AuditLog_UI | Draft | 2026-06-20 |
| [GOP_PreAuth_Overlay_NatsGate-prd.md](prds/GOP_PreAuth_Overlay_NatsGate-prd.md) | GOP_PreAuth_Overlay_NatsGate | Draft | 2026-06-20 |
| [GOP_Menu_Role_Visibility-prd.md](prds/GOP_Menu_Role_Visibility-prd.md) | GOP_Menu_Role_Visibility | Draft | 2026-06-20 |
| [DataGridPanel_Delete_Centralization-prd.md](prds/DataGridPanel_Delete_Centralization-prd.md) | DataGridPanel_Delete_Centralization | Draft | 2026-06-20 |
| [GOP_Session_Resilience_Lifecycle-prd.md](prds/GOP_Session_Resilience_Lifecycle-prd.md) | GOP_Session_Resilience_Lifecycle | Draft | 2026-06-20 |
| [GOP_MyPage_UI-prd.md](prds/GOP_MyPage_UI-prd.md) | GOP_MyPage_UI | Draft | 2026-06-20 |
| [GOP_AccountManager_UI-prd.md](prds/GOP_AccountManager_UI-prd.md) | GOP_AccountManager_UI | Draft | 2026-06-20 |
| [Client_API_v46_Conformance-prd.md](prds/Client_API_v46_Conformance-prd.md) | Client_API_v46_Conformance | Approved | 2026-06-19 |
| [NATS-Tracking-Geolocation-메시지정리.md](prds/NATS-Tracking-Geolocation-메시지정리.md) | NATS-Tracking-Geolocation-메시지정리.md | Draft | 2026-06-19 |
| [DevicePanel_CRUD_API_Sync-prd.md](prds/DevicePanel_CRUD_API_Sync-prd.md) | DevicePanel_CRUD_API_Sync | Draft | 2026-06-17 |
| [EventProcess_ContaminationFix-prd.md](prds/EventProcess_ContaminationFix-prd.md) | EventProcess_ContaminationFix | Draft | 2026-06-15 |
| [GridSnap_System-prd.md](prds/GridSnap_System-prd.md) | GridSnap_System | Approved | 2026-06-15 |
| [DigitalZoom_RenderTransform-prd.md](prds/DigitalZoom_RenderTransform-prd.md) | DigitalZoom_RenderTransform | Draft | 2026-06-15 |
| [GMap_Zoom_Improvements-prd.md](prds/GMap_Zoom_Improvements-prd.md) | GMap_Zoom_Improvements | Approved | 2026-06-12 |
| [MarkerHitTest_AABB_Fix-prd.md](prds/MarkerHitTest_AABB_Fix-prd.md) | MarkerHitTest_AABB_Fix | Completed | 2026-06-10 |
| [OverlayImage_Rotation_Editing-prd.md](prds/OverlayImage_Rotation_Editing-prd.md) | OverlayImage_Rotation_Editing | Approved | 2026-06-10 |
| [OverlayImage_ZOrder_Independence-prd.md](prds/OverlayImage_ZOrder_Independence-prd.md) | OverlayImage_ZOrder_Independence | Approved | 2026-06-10 |
| [ZOrder_PropertyPanel_Integration-prd.md](prds/ZOrder_PropertyPanel_Integration-prd.md) | ZOrder_PropertyPanel_Integration | Draft | 2026-06-10 |
| [LayerVisibility_Persistence_Fix-prd.md](prds/LayerVisibility_Persistence_Fix-prd.md) | LayerVisibility_Persistence_Fix | Approved | 2026-06-08 |
| [DeviceApi_ProviderPropagation_Fix-prd.md](prds/DeviceApi_ProviderPropagation_Fix-prd.md) | DeviceApi_ProviderPropagation_Fix | Draft | 2026-06-08 |
| [SplashScreen_MonitoringSolution-prd.md](prds/SplashScreen_MonitoringSolution-prd.md) | SplashScreen_MonitoringSolution | Draft | 2026-06-08 |
| [SplashScreen_LibraryComponent-prd.md](prds/SplashScreen_LibraryComponent-prd.md) | SplashScreen_LibraryComponent | Draft | 2026-06-08 |
| [SymbolTextSeparation_LabelPositioning-prd.md](prds/SymbolTextSeparation_LabelPositioning-prd.md) | SymbolTextSeparation_LabelPositioning | Approved | 2026-06-05 |
| [WebServer_Enable_Feature-prd.md](prds/WebServer_Enable_Feature-prd.md) | WebServer_Enable_Feature | Completed | 2026-06-05 |
| [MapSetup_Panel_Refactor-prd.md](prds/MapSetup_Panel_Refactor-prd.md) | MapSetup_Panel_Refactor | Approved | 2026-06-05 |
| [SettingPanel_BrokerLabel_Rename-prd.md](prds/SettingPanel_BrokerLabel_Rename-prd.md) | SettingPanel_BrokerLabel_Rename | Approved | 2026-06-05 |
| [RemoteDesktop_PanFollowBug_Fix-prd.md](prds/RemoteDesktop_PanFollowBug_Fix-prd.md) | RemoteDesktop_PanFollowBug_Fix | Approved | 2026-06-04 |
| [MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd.md](prds/MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd.md) | MapSymbol_DispatcherFreeze_And_LogNoise_Fix | Superseded | 2026-06-04 |
| [SymbolUpdate_DispatcherFreeze_Fix-prd.md](prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md) | SymbolUpdate_DispatcherFreeze_Fix | Completed | 2026-06-04 |
| [Multisensor_Symbol_Fix-prd.md](prds/Multisensor_Symbol_Fix-prd.md) | Multisensor_Symbol_Fix | Completed | 2026-06-04 |
| [BatchReport_SymbolRestore_Fix-prd.md](prds/BatchReport_SymbolRestore_Fix-prd.md) | BatchReport_SymbolRestore_Fix | Approved | 2026-06-04 |
| [EventCardPerformance-prd.md](prds/EventCardPerformance-prd.md) | EventCardPerformance | Draft | 2026-06-04 |
| [OverlayMap_MBTiles_Provider-prd.md](prds/OverlayMap_MBTiles_Provider-prd.md) | OverlayMap_MBTiles_Provider | Approved | 2026-06-02 |
| [RedisDomainService_DoubleStop_Fix-prd.md](prds/RedisDomainService_DoubleStop_Fix-prd.md) | RedisDomainService_DoubleStop_Fix | Approved | 2026-06-01 |
| [NatsShutdown_SubscriptionHang_Fix-prd.md](prds/NatsShutdown_SubscriptionHang_Fix-prd.md) | NatsShutdown_SubscriptionHang_Fix | Approved | 2026-06-01 |
| [AppShutdown_Blocking_Fix-prd.md](prds/AppShutdown_Blocking_Fix-prd.md) | AppShutdown_Blocking_Fix | Approved | 2026-06-01 |
| [DetectionPulse_Ripple_Enlargement-prd.md](prds/DetectionPulse_Ripple_Enlargement-prd.md) | DetectionPulse_Ripple_Enlargement | Approved | 2026-05-28 |
| [PRD_SplashScreen_Startup_Gating.md](prds/PRD_SplashScreen_Startup_Gating.md) | PRD_SplashScreen_Startup_Gating.md | Draft | 2026-05-27 |
| [SymbolUpdate_Threading_And_LeakFix-prd.md](prds/SymbolUpdate_Threading_And_LeakFix-prd.md) | SymbolUpdate_Threading_And_LeakFix | Draft | 2026-05-27 |
| [OverlayMap_Performance_Optimization-prd.md](prds/OverlayMap_Performance_Optimization-prd.md) | OverlayMap_Performance_Optimization | Draft | 2026-05-27 |
| [MalfunctionCard_ControllerNumber_BindingFix-prd.md](prds/MalfunctionCard_ControllerNumber_BindingFix-prd.md) | MalfunctionCard_ControllerNumber_BindingFix | Approved | 2026-05-27 |
| [MapSymbol_PulseAnimation_Performance_Fix-prd.md](prds/MapSymbol_PulseAnimation_Performance_Fix-prd.md) | MapSymbol_PulseAnimation_Performance_Fix | Approved | 2026-05-26 |
| [Event_Performance_Optimization-prd.md](prds/Event_Performance_Optimization-prd.md) | Event_Performance_Optimization | Draft | 2026-05-22 |
| [AutoActionReport_DualPath_Fix-prd.md](prds/AutoActionReport_DualPath_Fix-prd.md) | AutoActionReport_DualPath_Fix | Draft | 2026-05-22 |
| [BatchReport_Sound_Stop_Fix-prd.md](prds/BatchReport_Sound_Stop_Fix-prd.md) | BatchReport_Sound_Stop_Fix | Draft | 2026-05-21 |
| [PRD_PidsSymbol_Transparency_Blink.md](prds/PRD_PidsSymbol_Transparency_Blink.md) | PRD_PidsSymbol_Transparency_Blink.md | Draft | 2026-05-20 |
| [SoundTypeSwitch_ImmediateStop_Fix-prd.md](prds/SoundTypeSwitch_ImmediateStop_Fix-prd.md) | SoundTypeSwitch_ImmediateStop_Fix | Draft | 2026-05-20 |
| [Device_CompositeState_SSOT_And_FaultAutoRecovery-prd.md](prds/Device_CompositeState_SSOT_And_FaultAutoRecovery-prd.md) | Device_CompositeState_SSOT_And_FaultAutoRecovery | Draft | 2026-05-19 |
| [FenceGroup_Blink_And_Sound_DualPlay_Fix-prd.md](prds/FenceGroup_Blink_And_Sound_DualPlay_Fix-prd.md) | FenceGroup_Blink_And_Sound_DualPlay_Fix | Draft | 2026-05-19 |
| [Malfunction_CompositeState_And_FenceGroup_Visualization-prd.md](prds/Malfunction_CompositeState_And_FenceGroup_Visualization-prd.md) | Malfunction_CompositeState_And_FenceGroup_Visualization | Completed | 2026-05-19 |
| [BatchReport_DualInsert_And_MalfunctionRestore_Fix-prd.md](prds/BatchReport_DualInsert_And_MalfunctionRestore_Fix-prd.md) | BatchReport_DualInsert_And_MalfunctionRestore_Fix | Completed | 2026-05-19 |
| [GatewayEvent_Group_NtoN_Migration-prd.md](prds/GatewayEvent_Group_NtoN_Migration-prd.md) | GatewayEvent_Group_NtoN_Migration | Completed | 2026-05-19 |
| [Detection_Sound_And_DualPath_Fix-prd.md](prds/Detection_Sound_And_DualPath_Fix-prd.md) | Detection_Sound_And_DualPath_Fix | Completed | 2026-05-18 |
| [GMapCustomControl_ImageDrag_BugFix-prd.md](prds/GMapCustomControl_ImageDrag_BugFix-prd.md) | GMapCustomControl_ImageDrag_BugFix | Completed | 2026-05-15 |
| [LayerPanel_ContextMenu_Enhancement-prd.md](prds/LayerPanel_ContextMenu_Enhancement-prd.md) | LayerPanel_ContextMenu_Enhancement | Completed | 2026-05-14 |
| [PRD_ImageOverlay_FileCopy_On_Register.md](prds/PRD_ImageOverlay_FileCopy_On_Register.md) | PRD_ImageOverlay_FileCopy_On_Register.md | Completed | 2026-05-13 |

## 구현 플랜 (docs/plans/)

| 파일 | 연관 PRD | 진행률 | 날짜 |
|------|---------|--------|------|
| [map-tilt-25d-prd-plan.md](plans/map-tilt-25d-prd-plan.md) | **지도 카드 틸트 구현 플랜 v1.0** — [PRD](prds/map-tilt-25d-prd.md) Approved · 44태스크: Phase 0 VER-01~07(V-02 확정)/RISK 2 → SETUP 3 → 구현 2-A 판정 · 2-B 오버스캔 수식/정착기 · 2-C 설정(+메인 SetupModel 1줄·템플릿 키, 사전 통지) · 2-D 스냅샷 · 2-E Tier → 2-F 맵 컨트롤 배선 · 2-G VM/XAML/배지 · 2-H 3D 정책 · 2-I 빌보드(TDD) → TEST 5 → DOC 3. 롤백 태그 `before-map-tilt-25d` + WIP 백업 패치. 기반 5그룹 병렬 착수(2026-09-08 07:3x) | 2/44 | 2026-09-08 |
| [event-suppression-recurrence-prd-plan.md](plans/event-suppression-recurrence-prd-plan.md) | **억제 주간반복 구현 플랜 v1.0** — 61태스크/52h · **2단 게이트**(A 라이브러리 55 / B 실기·메인 6). S0 게이트+V-08 스파이크 → S1 헤드리스 3트랙 → S2 서비스·투영 → **S3 패널VM 순차(718행)** → **S4 XAML 순차(963행)** → S5 테스트·하네스 → S6 메인·문서. **적대검증 fatal 6 반영**: DTO 개명이 15h간 빌드 불가(T-B04 병합) · `IsDrag` Utils 이관이 NU1201로 불가(DEFER) · `FormErrorText` 미신설로 기존 30일 경고 **조용히 소멸**(T-E05b) · stale 배너가 폴링 실패 시 통째 소멸(HasActiveBanner) · **메인 편집 4건**(통지 확장). major 반영 10건(ToggleButton Style 부재·FR-06 출력 태스크 부재·RowHeight 고정·운영 쓰기 가드 부재 등). **착수 전 사용자 확인 3건**(worktree deviation·메인 4건·목록 열 폭) | 2026-09-07 |
| [pidsgroup-3d-fence-gate-prd-plan.md](plans/pidsgroup-3d-fence-gate-prd-plan.md) | 3D 철망·통문·함체 구현 플랜 — 46 태스크(VER 8·리스크 3·순수함수 A·Enum/모델/DB B·3D C·이벤트 D·컨트롤/속성창/드로잉 E·메인 M·서버 S·테스트·문서), 병렬 순서 S0~S5, 의존 그래프(순환 없음) | 2026-09-07 |
| [map-topbar-trafficlight-prd-plan.md](plans/map-topbar-trafficlight-prd-plan.md) | [PRD](prds/map-topbar-trafficlight-prd.md) | 6/21 | 2026-08-06 |
| [zoom-float-halfstep-prd-plan.md](plans/zoom-float-halfstep-prd-plan.md) | [PRD](prds/zoom-float-halfstep-prd.md) | 28/32 | 2026-08-06 |
| [pidsgroup-rightclick-prd-plan.md](plans/pidsgroup-rightclick-prd-plan.md) | [PRD](prds/pidsgroup-rightclick-prd.md) | 36/50 | 2026-08-06 |
| [installer-prd-plan.md](plans/installer-prd-plan.md) | [PRD](prds/installer-prd.md) | 29/42 | 2026-08-04 |
| [GMap_Schema_Migration_Idempotency-prd-plan.md](plans/GMap_Schema_Migration_Idempotency-prd-plan.md) | [PRD](prds/GMap_Schema_Migration_Idempotency-prd.md) | 19/47 | 2026-08-04 |
| [UI_Functional_Testing_Layer-prd-plan.md](plans/UI_Functional_Testing_Layer-prd-plan.md) | [PRD](prds/UI_Functional_Testing_Layer-prd.md) | 16/16 | 2026-08-04 |
| [UI_Automation_FlaUI_Smoke-prd-plan.md](plans/UI_Automation_FlaUI_Smoke-prd-plan.md) | [PRD](prds/UI_Automation_FlaUI_Smoke-prd.md) | 38/49 | 2026-08-04 |
| [GMap_PidsGroup_DoubleClick_ActionReport-prd-plan.md](plans/GMap_PidsGroup_DoubleClick_ActionReport-prd-plan.md) | [PRD](prds/GMap_PidsGroup_DoubleClick_ActionReport-prd.md) | 23/44 | 2026-08-03 |
| [session-management-overhaul-prd-plan.md](plans/session-management-overhaul-prd-plan.md) | [PRD](prds/session-management-overhaul-prd.md) | 3/40 | 2026-08-03 |
| [Controller_Fault_AutoRecovery_Extension-prd-plan.md](plans/Controller_Fault_AutoRecovery_Extension-prd-plan.md) | [PRD](prds/Controller_Fault_AutoRecovery_Extension-prd.md) | 0/9 | 2026-07-31 |
| [GMap_Controller_Blackout_Runtime_Fix-prd-plan.md](plans/GMap_Controller_Blackout_Runtime_Fix-prd-plan.md) | [PRD](prds/GMap_Controller_Blackout_Runtime_Fix-prd.md) | 0/26 | 2026-07-31 |
| [Event_Enum_Korean_Display-prd-plan.md](plans/Event_Enum_Korean_Display-prd-plan.md) | [PRD](prds/Event_Enum_Korean_Display-prd.md) | 3/24 | 2026-07-31 |
| [action-report-origin-thumbnail-prd-plan.md](plans/action-report-origin-thumbnail-prd-plan.md) | [PRD](prds/action-report-origin-thumbnail-prd.md) | 7/8 | 2026-07-31 |
| [detection-sync-thumbnail-prd-plan.md](plans/detection-sync-thumbnail-prd-plan.md) | [PRD](prds/detection-sync-thumbnail-prd.md) | 14/14 | 2026-07-31 |
| [malfunction-card-controller-sensor-prd-plan.md](plans/malfunction-card-controller-sensor-prd-plan.md) | [PRD](prds/malfunction-card-controller-sensor-prd.md) | 0/0 | 2026-07-31 |
| [malfunction-autoreport-setting-prd-plan.md](plans/malfunction-autoreport-setting-prd-plan.md) | [PRD](prds/malfunction-autoreport-setting-prd.md) | 18/27 | 2026-07-31 |
| [GMap_Map_Instruments-prd-plan.md](plans/GMap_Map_Instruments-prd-plan.md) | [PRD](prds/GMap_Map_Instruments-prd.md) | 16/16 | 2026-07-31 |
| [GMap_Compass_Control-prd-plan.md](plans/GMap_Compass_Control-prd-plan.md) | [PRD](prds/GMap_Compass_Control-prd.md) | 12/12 | 2026-07-30 |
| [GOP_Nats_Req_Failure_UX-prd-plan.md](plans/GOP_Nats_Req_Failure_UX-prd-plan.md) | [PRD](prds/GOP_Nats_Req_Failure_UX-prd.md) | 14/16 | 2026-07-30 |
| [GMap_Rotation_Full_Sync-prd-plan.md](plans/GMap_Rotation_Full_Sync-prd-plan.md) | [PRD](prds/GMap_Rotation_Full_Sync-prd.md) | 53/56 | 2026-07-30 |
| [GIS_Nats_v152_Req_Transition-prd-plan.md](plans/GIS_Nats_v152_Req_Transition-prd-plan.md) | [PRD](prds/GIS_Nats_v152_Req_Transition-prd.md) | 24/33 | 2026-07-30 |
| [line-drawing-hud-redesign-prd-plan.md](plans/line-drawing-hud-redesign-prd-plan.md) | [PRD](prds/line-drawing-hud-redesign-prd.md) | 15/21 | 2026-07-28 |
| [Measure_Tools-prd-plan.md](plans/Measure_Tools-prd-plan.md) | [PRD](prds/Measure_Tools-prd.md) | 0/21 | 2026-07-27 |
| [CameraPopup_ControlHub-prd-plan.md](plans/CameraPopup_ControlHub-prd-plan.md) | [PRD](prds/CameraPopup_ControlHub-prd.md) | 19/19 | 2026-07-27 |
| [CameraPopup_PanClamp_Badge_OnvifPtz-prd-plan.md](plans/CameraPopup_PanClamp_Badge_OnvifPtz-prd-plan.md) | [PRD](prds/CameraPopup_PanClamp_Badge_OnvifPtz-prd.md) | 23/23 | 2026-07-23 |
| [Overlay_Title_ZoomStyle-prd-plan.md](plans/Overlay_Title_ZoomStyle-prd-plan.md) | [PRD](prds/Overlay_Title_ZoomStyle-prd.md) | 32/37 | 2026-07-23 |
| [Detection_Signal_History-prd-plan.md](plans/Detection_Signal_History-prd-plan.md) | [PRD](prds/Detection_Signal_History-prd.md) | 37/44 | 2026-07-23 |
| [GOP_SessionPanel_Cleanup-prd-plan.md](plans/GOP_SessionPanel_Cleanup-prd-plan.md) | [PRD](prds/GOP_SessionPanel_Cleanup-prd.md) | 0/0 | 2026-07-23 |
| [Symbol_ContextMenu_ViewMode_Lock-prd-plan.md](plans/Symbol_ContextMenu_ViewMode_Lock-prd-plan.md) | [PRD](prds/Symbol_ContextMenu_ViewMode_Lock-prd.md) | 12/13 | 2026-07-23 |
| [GOP_SessionGrant_Pagination-prd-plan.md](plans/GOP_SessionGrant_Pagination-prd-plan.md) | [PRD](prds/GOP_SessionGrant_Pagination-prd.md) | 0/0 | 2026-07-23 |
| [GOP_AuditLog_DateFilter_Pagination-prd-plan.md](plans/GOP_AuditLog_DateFilter_Pagination-prd-plan.md) | [PRD](prds/GOP_AuditLog_DateFilter_Pagination-prd.md) | 0/0 | 2026-07-23 |
| [Admin_Photo_Upload-prd-plan.md](plans/Admin_Photo_Upload-prd-plan.md) | [PRD](prds/Admin_Photo_Upload-prd.md) | 5/26 | 2026-07-20 |
| [EventChart_Tooltip_ThemeSync-prd-plan.md](plans/EventChart_Tooltip_ThemeSync-prd-plan.md) | [PRD](prds/EventChart_Tooltip_ThemeSync-prd.md) | 6/7 | 2026-07-20 |
| [GMap_Anchor_Viewport_Lock-prd-plan.md](plans/GMap_Anchor_Viewport_Lock-prd-plan.md) | [PRD](prds/GMap_Anchor_Viewport_Lock-prd.md) | 6/7 | 2026-07-19 |
| [GMap_PidsCamera_FOV_Toggle_Persistence-prd-plan.md](plans/GMap_PidsCamera_FOV_Toggle_Persistence-prd-plan.md) | [PRD](prds/GMap_PidsCamera_FOV_Toggle_Persistence-prd.md) | 7/8 | 2026-07-18 |
| [DeviceStatusSync_ActionReportPropagation-prd-plan.md](plans/DeviceStatusSync_ActionReportPropagation-prd-plan.md) | [PRD](prds/DeviceStatusSync_ActionReportPropagation-prd.md) | 0/30 | 2026-07-15 |
| [CameraPopup_RtspSource_Priority-prd-plan.md](plans/CameraPopup_RtspSource_Priority-prd-plan.md) | [PRD](prds/CameraPopup_RtspSource_Priority-prd.md) | 24/27 | 2026-07-15 |
| [GMap_SystemResource_Indicator-prd-plan.md](plans/GMap_SystemResource_Indicator-prd-plan.md) | [PRD](prds/GMap_SystemResource_Indicator-prd.md) | 0/45 | 2026-07-13 |
| [LineArea_Symbol_Resize-prd-plan.md](plans/LineArea_Symbol_Resize-prd-plan.md) | [PRD](prds/LineArea_Symbol_Resize-prd.md) | 0/26 | 2026-07-13 |
| [LeftMenu_IntegratedWeb_Button-prd-plan.md](plans/LeftMenu_IntegratedWeb_Button-prd-plan.md) | [PRD](prds/LeftMenu_IntegratedWeb_Button-prd.md) | 7/13 | 2026-07-13 |
| [GMap_Zoom_Anchor_Home-prd-plan.md](plans/GMap_Zoom_Anchor_Home-prd-plan.md) | [PRD](prds/GMap_Zoom_Anchor_Home-prd.md) | 6/40 | 2026-07-13 |
| [MapSymbol_Shortcut_CopyPasteDelete-prd-plan.md](plans/MapSymbol_Shortcut_CopyPasteDelete-prd-plan.md) | [PRD](prds/MapSymbol_Shortcut_CopyPasteDelete-prd.md) | 0/27 | 2026-07-13 |
| [Watchdog_Modern_Rebuild-prd-plan.md](plans/Watchdog_Modern_Rebuild-prd-plan.md) | [PRD](prds/Watchdog_Modern_Rebuild-prd.md) | 0/46 | 2026-07-13 |
| [Action_Report_Nats_FullDto_Contract-prd-plan.md](plans/Action_Report_Nats_FullDto_Contract-prd-plan.md) | [PRD](prds/Action_Report_Nats_FullDto_Contract-prd.md) | 10/10 | 2026-07-11 |
| [Account_Lock_Management-prd-plan.md](plans/Account_Lock_Management-prd-plan.md) | [PRD](prds/Account_Lock_Management-prd.md) | 2/26 | 2026-07-07 |
| [GMap_Edit_Integration_Sim-prd-plan.md](plans/GMap_Edit_Integration_Sim-prd-plan.md) | [PRD](prds/GMap_Edit_Integration_Sim-prd.md) | 0/27 | 2026-07-05 |
| [GOP_Force_Logout_Propagation-prd-plan.md](plans/GOP_Force_Logout_Propagation-prd-plan.md) | [PRD](prds/GOP_Force_Logout_Propagation-prd.md) | 0/5 | 2026-07-03 |
| [Panel_Design_Unification-prd-plan.md](plans/Panel_Design_Unification-prd-plan.md) | [PRD](prds/Panel_Design_Unification-prd.md) | 1/19 | 2026-07-03 |
| [Map_Edit_Undo_Redo-prd-plan.md](plans/Map_Edit_Undo_Redo-prd-plan.md) | [PRD](prds/Map_Edit_Undo_Redo-prd.md) | 0/22 | 2026-07-02 |
| [Symbol_Label_Decouple-prd-plan.md](plans/Symbol_Label_Decouple-prd-plan.md) | [PRD](prds/Symbol_Label_Decouple-prd.md) | 0/15 | 2026-07-02 |
| [GMap_RubberBand_MultiSelect-prd-plan.md](plans/GMap_RubberBand_MultiSelect-prd-plan.md) | [PRD](prds/GMap_RubberBand_MultiSelect-prd.md) | 0/17 | 2026-07-02 |
| [Camera_Aim_Overlay_Animation-prd-plan.md](plans/Camera_Aim_Overlay_Animation-prd-plan.md) | [PRD](prds/Camera_Aim_Overlay_Animation-prd.md) | 0/15 | 2026-07-02 |
| [GMap_Delete_EditMode_BugFix-prd-plan.md](plans/GMap_Delete_EditMode_BugFix-prd-plan.md) | [PRD](prds/GMap_Delete_EditMode_BugFix-prd.md) | 5/22 | 2026-07-02 |
| [Login_Gated_GIS_Init-prd-plan.md](plans/Login_Gated_GIS_Init-prd-plan.md) | [PRD](prds/Login_Gated_GIS_Init-prd.md) | 0/43 | 2026-07-02 |
| [Grant_Scheduling_Client-prd-plan.md](plans/Grant_Scheduling_Client-prd-plan.md) | [PRD](prds/Grant_Scheduling_Client-prd.md) | 7/32 | 2026-07-01 |
| [GatewayEvent_Group_Resurrection_Fix-prd-plan.md](plans/GatewayEvent_Group_Resurrection_Fix-prd-plan.md) | [PRD](prds/GatewayEvent_Group_Resurrection_Fix-prd.md) | 0/29 | 2026-06-29 |
| [Symbol_Apply_DeviceLocation_Api-prd-plan.md](plans/Symbol_Apply_DeviceLocation_Api-prd-plan.md) | [PRD](prds/Symbol_Apply_DeviceLocation_Api-prd.md) | 14/16 | 2026-06-29 |
| [CameraPopup_PressHold_PtzZoomFocus-prd-plan.md](plans/CameraPopup_PressHold_PtzZoomFocus-prd-plan.md) | [PRD](prds/CameraPopup_PressHold_PtzZoomFocus-prd.md) | 28/29 | 2026-06-29 |
| [GOP_Permission_Enforcement-prd-plan.md](plans/GOP_Permission_Enforcement-prd-plan.md) | [PRD](prds/GOP_Permission_Enforcement-prd.md) | 5/11 | 2026-06-29 |
| [Camera_PTZ_AimLocation_Nats-prd-plan.md](plans/Camera_PTZ_AimLocation_Nats-prd-plan.md) | [PRD](prds/Camera_PTZ_AimLocation_Nats-prd.md) | 0/34 | 2026-06-29 |
| [GOP_Permission_Gate_Feature-prd-plan.md](plans/GOP_Permission_Gate_Feature-prd-plan.md) | [PRD](prds/GOP_Permission_Gate_Feature-prd.md) | 10/22 | 2026-06-29 |
| [Tracking_Playback_DataSource_Toggle-prd-plan.md](plans/Tracking_Playback_DataSource_Toggle-prd-plan.md) | [PRD](prds/Tracking_Playback_DataSource_Toggle-prd.md) | 0/43 | 2026-06-26 |
| [Tracking_GIS_Visualization_Playback-prd-plan.md](plans/Tracking_GIS_Visualization_Playback-prd-plan.md) | [PRD](prds/Tracking_GIS_Visualization_Playback-prd.md) | 8/63 | 2026-06-26 |
| [GOP_Profile_Photo_Upload-prd-plan.md](plans/GOP_Profile_Photo_Upload-prd-plan.md) | [PRD](prds/GOP_Profile_Photo_Upload-prd.md) | 2/21 | 2026-06-26 |
| [GOP_Account_Auth_Integration-prd-plan.md](plans/GOP_Account_Auth_Integration-prd-plan.md) | [PRD](prds/GOP_Account_Auth_Integration-prd.md) | 28/62 | 2026-06-25 |
| [UI_ModernTheme_DesignSystem-prd-plan.md](plans/UI_ModernTheme_DesignSystem-prd-plan.md) | [PRD](prds/UI_ModernTheme_DesignSystem-prd.md) | 30/75 | 2026-06-24 |
| [CameraPopup_PTZ_Control-prd-plan.md](plans/CameraPopup_PTZ_Control-prd-plan.md) | [PRD](prds/CameraPopup_PTZ_Control-prd.md) | 16/48 | 2026-06-24 |
| [Accounts_Ui_Library_Extraction-prd-plan.md](plans/Accounts_Ui_Library_Extraction-prd-plan.md) | [PRD](prds/Accounts_Ui_Library_Extraction-prd.md) | 24/40 | 2026-06-23 |
| [CameraPopup_DigitalZoom_Alignment-prd-plan.md](plans/CameraPopup_DigitalZoom_Alignment-prd-plan.md) | [PRD](prds/CameraPopup_DigitalZoom_Alignment-prd.md) | 21/21 | 2026-06-23 |
| [CameraPopup_Streaming_Settings-prd-plan.md](plans/CameraPopup_Streaming_Settings-prd-plan.md) | [PRD](prds/CameraPopup_Streaming_Settings-prd.md) | 0/0 | 2026-06-23 |
| [Event_FollowupAction_ContextMenu-prd-plan.md](plans/Event_FollowupAction_ContextMenu-prd-plan.md) | [PRD](prds/Event_FollowupAction_ContextMenu-prd.md) | 0/0 | 2026-06-22 |
| [Rtsp_Map_Popup-prd-plan.md](plans/Rtsp_Map_Popup-prd-plan.md) | [PRD](prds/Rtsp_Map_Popup-prd.md) | 0/37 | 2026-06-22 |
| [EventPanel_CRUD_Api_Alignment-prd-plan.md](plans/EventPanel_CRUD_Api_Alignment-prd-plan.md) | [PRD](prds/EventPanel_CRUD_Api_Alignment-prd.md) | 0/0 | 2026-06-22 |
| [BaseMap_NoData_DefaultTile-prd-plan.md](plans/BaseMap_NoData_DefaultTile-prd-plan.md) | [PRD](prds/BaseMap_NoData_DefaultTile-prd.md) | 1/21 | 2026-06-22 |
| [DataGrid_Column_Curation-plan.md](plans/DataGrid_Column_Curation-plan.md) | [PRD](prds/DataGrid_Column_Curation-prd.md) | 0/0 | 2026-06-22 |
| [SpeakerServerAssignment-prd-plan.md](plans/SpeakerServerAssignment-prd-plan.md) | [PRD](prds/SpeakerServerAssignment-prd.md) | 0/14 | 2026-06-22 |
| [DevicePropertyPanel_Layout_Redesign-prd-plan.md](plans/DevicePropertyPanel_Layout_Redesign-prd-plan.md) | [PRD](prds/DevicePropertyPanel_Layout_Redesign-prd.md) | 0/14 | 2026-06-22 |
| [DevicePanel_TempState_Unification-prd-plan.md](plans/DevicePanel_TempState_Unification-prd-plan.md) | [PRD](prds/DevicePanel_TempState_Unification-prd.md) | 0/18 | 2026-06-20 |
| [DataGridPanel_Delete_Centralization-prd-plan.md](plans/DataGridPanel_Delete_Centralization-prd-plan.md) | [PRD](prds/DataGridPanel_Delete_Centralization-prd.md) | 0/0 | 2026-06-20 |
| [Client_API_v46_Conformance-prd-plan.md](plans/Client_API_v46_Conformance-prd-plan.md) | [PRD](prds/Client_API_v46_Conformance-prd.md) | 0/0 | 2026-06-19 |
| [EventProcess_ContaminationFix-prd-plan.md](plans/EventProcess_ContaminationFix-prd-plan.md) | [PRD](prds/EventProcess_ContaminationFix-prd.md) | 0/14 | 2026-06-15 |
| [GridSnap_System-prd-plan.md](plans/GridSnap_System-prd-plan.md) | [PRD](prds/GridSnap_System-prd.md) | 11/32 | 2026-06-15 |
| [DigitalZoom_RenderTransform-prd-plan.md](plans/DigitalZoom_RenderTransform-prd-plan.md) | [PRD](prds/DigitalZoom_RenderTransform-prd.md) | 0/12 | 2026-06-15 |
| [GMap_Zoom_Improvements-prd-plan.md](plans/GMap_Zoom_Improvements-prd-plan.md) | [PRD](prds/GMap_Zoom_Improvements-prd.md) | 0/5 | 2026-06-12 |
| [OverlayImage_Rotation_Editing-prd-plan.md](plans/OverlayImage_Rotation_Editing-prd-plan.md) | [PRD](prds/OverlayImage_Rotation_Editing-prd.md) | 0/33 | 2026-06-10 |
| [OverlayImage_ZOrder_Independence-plan.md](plans/OverlayImage_ZOrder_Independence-plan.md) | [PRD](prds/OverlayImage_ZOrder_Independence-prd.md) | 0/25 | 2026-06-10 |
| [ZOrder_PropertyPanel_Integration-prd-plan.md](plans/ZOrder_PropertyPanel_Integration-prd-plan.md) | [PRD](prds/ZOrder_PropertyPanel_Integration-prd.md) | 0/0 | 2026-06-10 |
| [LayerVisibility_Persistence_Fix-prd-plan.md](plans/LayerVisibility_Persistence_Fix-prd-plan.md) | [PRD](prds/LayerVisibility_Persistence_Fix-prd.md) | 3/14 | 2026-06-08 |
| [SplashScreen_MonitoringSolution-prd-plan.md](plans/SplashScreen_MonitoringSolution-prd-plan.md) | [PRD](prds/SplashScreen_MonitoringSolution-prd.md) | 0/0 | 2026-06-08 |
| [SymbolTextSeparation_LabelPositioning-prd-plan.md](plans/SymbolTextSeparation_LabelPositioning-prd-plan.md) | [PRD](prds/SymbolTextSeparation_LabelPositioning-prd.md) | 20/38 | 2026-06-05 |
| [RemoteDesktop_PanFollowBug_Fix-prd-plan.md](plans/RemoteDesktop_PanFollowBug_Fix-prd-plan.md) | [PRD](prds/RemoteDesktop_PanFollowBug_Fix-prd.md) | 8/16 | 2026-06-04 |
| [MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd-plan.md](plans/MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd-plan.md) | [PRD](prds/MapSymbol_DispatcherFreeze_And_LogNoise_Fix-prd.md) | 7/15 | 2026-06-04 |
| [SymbolUpdate_DispatcherFreeze_Fix-prd-plan.md](plans/SymbolUpdate_DispatcherFreeze_Fix-prd-plan.md) | [PRD](prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md) | 20/20 | 2026-06-04 |
| [Multisensor_Symbol_Fix-prd-plan.md](plans/Multisensor_Symbol_Fix-prd-plan.md) | [PRD](prds/Multisensor_Symbol_Fix-prd.md) | 20/20 | 2026-06-04 |
| [EventCardPerformance-prd-plan.md](plans/EventCardPerformance-prd-plan.md) | [PRD](prds/EventCardPerformance-prd.md) | 16/29 | 2026-06-04 |
| [OverlayMap_MBTiles_Provider-prd-plan.md](plans/OverlayMap_MBTiles_Provider-prd-plan.md) | [PRD](prds/OverlayMap_MBTiles_Provider-prd.md) | 22/23 | 2026-06-02 |
| [DetectionPulse_Ripple_Enlargement-prd-plan.md](plans/DetectionPulse_Ripple_Enlargement-prd-plan.md) | [PRD](prds/DetectionPulse_Ripple_Enlargement-prd.md) | 9/9 | 2026-06-02 |
| [RedisDomainService_DoubleStop_Fix-prd-plan.md](plans/RedisDomainService_DoubleStop_Fix-prd-plan.md) | [PRD](prds/RedisDomainService_DoubleStop_Fix-prd.md) | 2/3 | 2026-06-01 |
| [NatsShutdown_SubscriptionHang_Fix-prd-plan.md](plans/NatsShutdown_SubscriptionHang_Fix-prd-plan.md) | [PRD](prds/NatsShutdown_SubscriptionHang_Fix-prd.md) | 0/8 | 2026-06-01 |
| [AppShutdown_Blocking_Fix-prd-plan.md](plans/AppShutdown_Blocking_Fix-prd-plan.md) | [PRD](prds/AppShutdown_Blocking_Fix-prd.md) | 4/7 | 2026-06-01 |
| [OverlayMap_Performance_Optimization-prd-plan.md](plans/OverlayMap_Performance_Optimization-prd-plan.md) | [PRD](prds/OverlayMap_Performance_Optimization-prd.md) | 56/67 | 2026-05-27 |
| [PRD_SplashScreen_Startup_Gating-prd-plan.md](plans/PRD_SplashScreen_Startup_Gating-prd-plan.md) | [PRD](prds/PRD_SplashScreen_Startup_Gating-prd.md) | 22/36 | 2026-05-27 |
| [MalfunctionCard_ControllerNumber_BindingFix-prd-plan.md](plans/MalfunctionCard_ControllerNumber_BindingFix-prd-plan.md) | [PRD](prds/MalfunctionCard_ControllerNumber_BindingFix-prd.md) | 4/8 | 2026-05-27 |
| [MapSymbol_PulseAnimation_Performance_Fix-prd-plan.md](plans/MapSymbol_PulseAnimation_Performance_Fix-prd-plan.md) | [PRD](prds/MapSymbol_PulseAnimation_Performance_Fix-prd.md) | 20/24 | 2026-05-26 |
| [Event_Performance_Optimization-prd-plan.md](plans/Event_Performance_Optimization-prd-plan.md) | [PRD](prds/Event_Performance_Optimization-prd.md) | 49/60 | 2026-05-22 |
| [SoundTypeSwitch_ImmediateStop_Fix-plan.md](plans/SoundTypeSwitch_ImmediateStop_Fix-plan.md) | [PRD](prds/SoundTypeSwitch_ImmediateStop_Fix-prd.md) | 0/0 | 2026-05-20 |
| [Device_CompositeState_SSOT_And_FaultAutoRecovery-plan.md](plans/Device_CompositeState_SSOT_And_FaultAutoRecovery-plan.md) | [PRD](prds/Device_CompositeState_SSOT_And_FaultAutoRecovery-prd.md) | 0/0 | 2026-05-19 |
| [FenceGroup_Blink_And_Sound_DualPlay_Fix-plan.md](plans/FenceGroup_Blink_And_Sound_DualPlay_Fix-plan.md) | [PRD](prds/FenceGroup_Blink_And_Sound_DualPlay_Fix-prd.md) | 0/9 | 2026-05-19 |
| [Malfunction_CompositeState_And_FenceGroup_Visualization-plan.md](plans/Malfunction_CompositeState_And_FenceGroup_Visualization-plan.md) | [PRD](prds/Malfunction_CompositeState_And_FenceGroup_Visualization-prd.md) | 35/35 | 2026-05-19 |
| [BatchReport_DualInsert_And_MalfunctionRestore_Fix-plan.md](plans/BatchReport_DualInsert_And_MalfunctionRestore_Fix-plan.md) | [PRD](prds/BatchReport_DualInsert_And_MalfunctionRestore_Fix-prd.md) | 26/26 | 2026-05-19 |
| [Detection_Sound_And_DualPath_Fix-prd-plan.md](plans/Detection_Sound_And_DualPath_Fix-prd-plan.md) | [PRD](prds/Detection_Sound_And_DualPath_Fix-prd.md) | 21/21 | 2026-05-19 |
| [GatewayEvent_Group_NtoN_Migration-plan.md](plans/GatewayEvent_Group_NtoN_Migration-plan.md) | [PRD](prds/GatewayEvent_Group_NtoN_Migration-prd.md) | 43/43 | 2026-05-19 |
| [GMapCustomControl_ImageDrag_BugFix-plan.md](plans/GMapCustomControl_ImageDrag_BugFix-plan.md) | [PRD](prds/GMapCustomControl_ImageDrag_BugFix-prd.md) | 9/9 | 2026-05-15 |
| [LayerPanel_ContextMenu_Enhancement-prd-plan.md](plans/LayerPanel_ContextMenu_Enhancement-prd-plan.md) | [PRD](prds/LayerPanel_ContextMenu_Enhancement-prd.md) | 31/31 | 2026-05-14 |
| [PRD_ImageOverlay_FileCopy_On_Register-prd-plan.md](plans/PRD_ImageOverlay_FileCopy_On_Register-prd-plan.md) | [PRD](prds/PRD_ImageOverlay_FileCopy_On_Register-prd.md) | 9/9 | 2026-05-13 |

## 테스트 결과 (docs/tests/)

| 파일 | 통과율 | 커버리지 | 날짜 |
|------|--------|---------|------|
| [map-tilt-25d-scenarios.md](tests/map-tilt-25d-scenarios.md) | 지도 카드 틸트 시나리오 카탈로그 4,096(기계: 8줌×플래그×앵커×베어링×각도4×Tier × 입력 8) + 교차 22 | 정책 파라미터 제안값(35°/20°/게이트 18.0/히스테리시스 0.5) | 2026-09-08 |
| [map-tilt-25d-simulation-log.md](tests/map-tilt-25d-simulation-log.md) | 전량 로그 — 제안 정책 불변식 위반 0 · 현행 결함 D1~D4 · 결정③ 차이 R3 · 정책 공백 G1~G8 | 생성기 scratchpad/sim/map_tilt_sim.py | 2026-09-08 |
 [action-report-custom-template-simulation-log.md](tests/action-report-custom-template-simulation-log.md) | 시뮬레이션 전량 로그 — PASS 167 / ISSUE·GAP 68 / 고유 42종 | 2026-08-07 |
 [action-report-custom-template-scenarios.md](tests/action-report-custom-template-scenarios.md) | 시나리오 카탈로그 235건 (L75/C28/E29/O12/P28/S10/D14/M6/X10/U23) | 2026-08-07 |
| [pidsgroup-3d-fence-gate-scenarios.md](tests/pidsgroup-3d-fence-gate-scenarios.md) | 3D 철망·통문·함체 시나리오 카탈로그 40건(정상 12·경계 10·실패 12·회귀 6, 검증 방식 H/S/U/D) | 2026-09-07 |
| [group-symbol-transform-scenarios.md](tests/group-symbol-transform-scenarios.md) | 그룹 심볼 변환 시나리오 카탈로그 v4 — 5,144건(회전 4,320·누적 192·스케일 452·래스터 48·계열 45·부가 24·영속 30·생명주기 18·GAP 15), 진리값 2종×후보 5종 성적표 | 2026-09-04 |
| [group-symbol-transform-simulation-log.md](tests/group-symbol-transform-simulation-log.md) | 그룹 심볼 변환 시뮬 **전량 로그 v4**(5,144건, PASS 3,012·ISSUE 2,117·GAP 15) | 2026-09-04 |
| [symbol-3d-housing-scenarios.md](tests/symbol-3d-housing-scenarios.md) | 3D 하우징 시나리오 카탈로그 — 라운드 3 최종 **2,032건** · 패밀리 85(+ DND/PAL/PROP/R2) · SIM ID 유일(생성기 검증) · PRD FR 역참조 | - | 2026-09-04 |
| [symbol-3d-housing-simulation-log.md](tests/symbol-3d-housing-simulation-log.md) | 3D 하우징 시뮬 전량 로그 — 라운드 3(PASS 1,348 · ISSUE 628 · GAP 40 · MEASURE 16) + 라운드 1 재실행본(1,299) | - | 2026-09-04 |
| [event-mapping-workbench-scenarios.md](tests/event-mapping-workbench-scenarios.md) | 시나리오 카탈로그 v1.1 — **633건**(P/E/D/C/B/N/M/R/G/O/T + v1.1 신설 X·L·Z) · 실기 검증 V-1~V-8 | - | 2026-08-07 |
| [event-mapping-workbench-simulation-log.md](tests/event-mapping-workbench-simulation-log.md) | 시뮬 전량 로그 — **PASS 523 / ISSUE 110 / 이슈 33종**. 시뮬레이터 [event-mapping-workbench-simulator.py](tests/event-mapping-workbench-simulator.py) 부속 | 82.6% | 2026-08-07 |
| [gmap-control-capability-interaction.md](tests/gmap-control-capability-interaction.md) | -% | -% | 2026-08-07 |
| [gmap-control-capability-modes.md](tests/gmap-control-capability-modes.md) | -% | -% | 2026-08-07 |
| [gmap-control-capability-drawing.md](tests/gmap-control-capability-drawing.md) | -% | -% | 2026-08-07 |
| [gmap-control-capability-viewport.md](tests/gmap-control-capability-viewport.md) | -% | -% | 2026-08-07 |
| [gmap-control-capability-markers.md](tests/gmap-control-capability-markers.md) | -% | -% | 2026-08-07 |
| [gmaps-xaml-capability-overlays.md](tests/gmaps-xaml-capability-overlays.md) | -% | -% | 2026-08-07 |
| [gis-test-catalog-account-auth.md](tests/gis-test-catalog-account-auth.md) | -% | -% | 2026-08-07 |
| [gis-test-catalog-data-integrity.md](tests/gis-test-catalog-data-integrity.md) | -% | -% | 2026-08-07 |
| [gis-test-catalog-overlay-image.md](tests/gis-test-catalog-overlay-image.md) | -% | -% | 2026-08-07 |
| [gis-test-catalog-map-core.md](tests/gis-test-catalog-map-core.md) | -% | -% | 2026-08-07 |
| [gis-test-catalog-label-adorner.md](tests/gis-test-catalog-label-adorner.md) | -% | -% | 2026-08-07 |
| [gis-test-catalog-device-crud.md](tests/gis-test-catalog-device-crud.md) | -% | -% | 2026-08-07 |
| [gis-test-master-checklist.md](tests/gis-test-master-checklist.md) | -% | -% | 2026-08-07 |
| [gmaps-xaml-capability-mapview.md](tests/gmaps-xaml-capability-mapview.md) | -% | -% | 2026-08-07 |
| [gmaps-xaml-capability-markers.md](tests/gmaps-xaml-capability-markers.md) | -% | -% | 2026-08-07 |
| [gmaps-xaml-capability-overlays.md](tests/gmaps-xaml-capability-overlays.md) | -% | -% | 2026-08-07 |
| [gmaps-xaml-capability-property.md](tests/gmaps-xaml-capability-property.md) | -% | -% | 2026-08-07 |
| [gmaps-xaml-capability-instruments.md](tests/gmaps-xaml-capability-instruments.md) | -% | -% | 2026-08-07 |
| [gis-test-catalog-drawing-measure.md](tests/gis-test-catalog-drawing-measure.md) | -% | -% | 2026-08-07 |
| [gis-test-catalog-events-panel.md](tests/gis-test-catalog-events-panel.md) | -% | -% | 2026-08-07 |
| [gis-test-catalog-layer-panel.md](tests/gis-test-catalog-layer-panel.md) | -% | -% | 2026-08-07 |
| [gis-test-catalog-symbol-crud.md](tests/gis-test-catalog-symbol-crud.md) | -% | -% | 2026-08-07 |
| [gis-test-catalog-symbol-property.md](tests/gis-test-catalog-symbol-property.md) | -% | -% | 2026-08-07 |
| [detection-history-bughunt-scenarios.md](tests/detection-history-bughunt-scenarios.md) | -% | -% | 2026-08-06 |
| [detection-history-bughunt-simulation-log.md](tests/detection-history-bughunt-simulation-log.md) | -% | -% | 2026-08-06 |
| [zoom-float-halfstep-scenarios.md](tests/zoom-float-halfstep-scenarios.md) | -% | -% | 2026-08-06 |
| [zoom-float-halfstep-simulation-log.md](tests/zoom-float-halfstep-simulation-log.md) | -% | -% | 2026-08-06 |
| [gmap-scenario-spec.md](tests/gmap-scenario-spec.md) | -% | -% | 2026-08-04 |
| [GMap_Rotation_E2E2-scenarios.md](tests/GMap_Rotation_E2E2-scenarios.md) | -% | -% | 2026-07-30 |
| [GMap_Anchor_Viewport_Lock-test-result.md](tests/GMap_Anchor_Viewport_Lock-test-result.md) | -% | -% | 2026-07-19 |
| [GMap_PidsCamera_FOV_Toggle_Persistence-test-result.md](tests/GMap_PidsCamera_FOV_Toggle_Persistence-test-result.md) | -% | -% | 2026-07-18 |
| [GMap_Edit_Integration_Sim-test-result.md](tests/GMap_Edit_Integration_Sim-test-result.md) | -% | -% | 2026-07-05 |
| [OverlayImage_Undo_TestScenarios.md](tests/OverlayImage_Undo_TestScenarios.md) | -% | -% | 2026-07-05 |
| [TEST_SCENARIOS_GOP_Account_RBAC.md](tests/TEST_SCENARIOS_GOP_Account_RBAC.md) | -% | -% | 2026-07-04 |
| [TEST_ImageOverlay_FileCopy_On_Register.md](tests/TEST_ImageOverlay_FileCopy_On_Register.md) | -% | -% | 2026-05-13 |

## 완료 리포트 (docs/reports/)

| 파일 | 문서 연결 체인 | 날짜 |
| [map-tilt-25d-report.md](reports/map-tilt-25d-report.md) | **지도 카드 틸트 완료 리포트** — [PRD v1.2](prds/map-tilt-25d-prd.md) → [플랜](plans/map-tilt-25d-prd-plan.md) 45/47 → 기반 5 + 배선 5 그룹(적대 리뷰 10 PASS) → 헤드리스 566/150/4/94 → **실기 6차 틸트 ✅12/ⓘ3/✖0 + 3D 회귀 ✅26/✖0**(OFF 픽셀 diff 0 · 게이트 왕복 · Tier 0x20000). 편차 7 · 잔여 VER-06/07 · 코드 미커밋(태그 `before-map-tilt-25d`) | 2026-09-08 |
| [pidsgroup-3d-fence-gate-report.md](reports/pidsgroup-3d-fence-gate-report.md) | 3D 철망·통문·함체 구현 리포트 1차(2026-09-07) — 라이브러리·메인·서버·하네스 green, 실기 대조표(화면 잠금으로 미실행)·PRD 편차·통지 |
|------|------------|------|
| [session-management-overhaul-report.md](reports/session-management-overhaul-report.md) | [PRD](prds/session-management-overhaul-prd.md) → [Plan](plans/session-management-overhaul-prd-plan.md) | 2026-08-03 |
| [Detection_Signal_History-report.md](reports/Detection_Signal_History-report.md) | [PRD](prds/Detection_Signal_History-prd.md) → [Plan](plans/Detection_Signal_History-prd-plan.md) | 2026-07-27 |
| [CameraPopup_PanClamp_Badge_OnvifPtz-report.md](reports/CameraPopup_PanClamp_Badge_OnvifPtz-report.md) | [PRD](prds/CameraPopup_PanClamp_Badge_OnvifPtz-prd.md) → [Plan](plans/CameraPopup_PanClamp_Badge_OnvifPtz-prd-plan.md) | 2026-07-23 |
| [Overlay_Title_ZoomStyle-report.md](reports/Overlay_Title_ZoomStyle-report.md) | [PRD](prds/Overlay_Title_ZoomStyle-prd.md) → [Plan](plans/Overlay_Title_ZoomStyle-prd-plan.md) | 2026-07-23 |
| [grant-verification-report.md](reports/grant-verification-report.md) | [PRD](prds/grant-verification-prd.md) → [Plan](plans/grant-verification-prd-plan.md) | 2026-07-20 |
| [ANALYSIS_DevicePanel_Intermittent_Empty_API_Load.md](reports/ANALYSIS_DevicePanel_Intermittent_Empty_API_Load.md) | [PRD](prds/ANALYSIS_DevicePanel_Intermittent_Empty_API_Load.md-prd.md) → [Plan](plans/ANALYSIS_DevicePanel_Intermittent_Empty_API_Load.md-prd-plan.md) | 2026-07-15 |
| [ANALYSIS_GMap_IpCamera_FOV_Add_Bug.md](reports/ANALYSIS_GMap_IpCamera_FOV_Add_Bug.md) | [PRD](prds/ANALYSIS_GMap_IpCamera_FOV_Add_Bug.md-prd.md) → [Plan](plans/ANALYSIS_GMap_IpCamera_FOV_Add_Bug.md-prd-plan.md) | 2026-07-15 |
| [ANALYSIS_GMap_PidsGroup_Blink_StrokeThickness_FieldBuild.md](reports/ANALYSIS_GMap_PidsGroup_Blink_StrokeThickness_FieldBuild.md) | [PRD](prds/ANALYSIS_GMap_PidsGroup_Blink_StrokeThickness_FieldBuild.md-prd.md) → [Plan](plans/ANALYSIS_GMap_PidsGroup_Blink_StrokeThickness_FieldBuild.md-prd-plan.md) | 2026-07-15 |
| [ANALYSIS_GMap_PidsGroup_Lock_MultiMonitor_Disappearance.md](reports/ANALYSIS_GMap_PidsGroup_Lock_MultiMonitor_Disappearance.md) | [PRD](prds/ANALYSIS_GMap_PidsGroup_Lock_MultiMonitor_Disappearance.md-prd.md) → [Plan](plans/ANALYSIS_GMap_PidsGroup_Lock_MultiMonitor_Disappearance.md-prd-plan.md) | 2026-07-15 |
| [Event_CRUD_Standard_Simulation-report.md](reports/Event_CRUD_Standard_Simulation-report.md) | [PRD](prds/Event_CRUD_Standard_Simulation-prd.md) → [Plan](plans/Event_CRUD_Standard_Simulation-prd-plan.md) | 2026-07-05 |
| [Device_CRUD_Standard_Simulation-report.md](reports/Device_CRUD_Standard_Simulation-report.md) | [PRD](prds/Device_CRUD_Standard_Simulation-prd.md) → [Plan](plans/Device_CRUD_Standard_Simulation-prd-plan.md) | 2026-07-05 |
| [TEST_VERIFICATION_CHECKLIST_2026-07-04.md](reports/TEST_VERIFICATION_CHECKLIST_2026-07-04.md) | [PRD](prds/TEST_VERIFICATION_CHECKLIST_2026-07-04.md-prd.md) → [Plan](plans/TEST_VERIFICATION_CHECKLIST_2026-07-04.md-prd-plan.md) | 2026-07-04 |
| [Permission_Simulation_Round2-report.md](reports/Permission_Simulation_Round2-report.md) | [PRD](prds/Permission_Simulation_Round2-prd.md) → [Plan](plans/Permission_Simulation_Round2-prd-plan.md) | 2026-07-02 |
| [Permission_Simulation_Round1-report.md](reports/Permission_Simulation_Round1-report.md) | [PRD](prds/Permission_Simulation_Round1-prd.md) → [Plan](plans/Permission_Simulation_Round1-prd-plan.md) | 2026-07-02 |
| [GOP_Force_Logout_Client_Phase1-report.md](reports/GOP_Force_Logout_Client_Phase1-report.md) | [PRD](prds/GOP_Force_Logout_Client_Phase1-prd.md) → [Plan](plans/GOP_Force_Logout_Client_Phase1-prd-plan.md) | 2026-06-29 |
| [2026-06-23_Event_Domain_Session_Report.md](reports/2026-06-23_Event_Domain_Session_Report.md) | [PRD](prds/2026-06-23_Event_Domain_Session_Report.md-prd.md) → [Plan](plans/2026-06-23_Event_Domain_Session_Report.md-prd-plan.md) | 2026-06-22 |
| [API_Group_DeviceCount_Cascade-report.md](reports/API_Group_DeviceCount_Cascade-report.md) | [PRD](prds/API_Group_DeviceCount_Cascade-prd.md) → [Plan](plans/API_Group_DeviceCount_Cascade-prd-plan.md) | 2026-06-21 |
| [API_Delete_Response_Inconsistency-report.md](reports/API_Delete_Response_Inconsistency-report.md) | [PRD](prds/API_Delete_Response_Inconsistency-prd.md) → [Plan](plans/API_Delete_Response_Inconsistency-prd-plan.md) | 2026-06-21 |
| [DevicePanel_TempState_QA-checklist.md](reports/DevicePanel_TempState_QA-checklist.md) | [PRD](prds/DevicePanel_TempState_QA-checklist.md-prd.md) → [Plan](plans/DevicePanel_TempState_QA-checklist.md-prd-plan.md) | 2026-06-21 |
| [DevicePanel_TempState_Unification-report.md](reports/DevicePanel_TempState_Unification-report.md) | [PRD](prds/DevicePanel_TempState_Unification-prd.md) → [Plan](plans/DevicePanel_TempState_Unification-prd-plan.md) | 2026-06-21 |
| [Controller_422_Fix-report.md](reports/Controller_422_Fix-report.md) | [PRD](prds/Controller_422_Fix-prd.md) → [Plan](plans/Controller_422_Fix-prd-plan.md) | 2026-06-20 |
| [DataGridPanel_CRUD_Phase2_3-report.md](reports/DataGridPanel_CRUD_Phase2_3-report.md) | [PRD](prds/DataGridPanel_CRUD_Phase2_3-prd.md) → [Plan](plans/DataGridPanel_CRUD_Phase2_3-prd-plan.md) | 2026-06-20 |
| [DataGridPanel_CRUD_Phase1-report.md](reports/DataGridPanel_CRUD_Phase1-report.md) | [PRD](prds/DataGridPanel_CRUD_Phase1-prd.md) → [Plan](plans/DataGridPanel_CRUD_Phase1-prd-plan.md) | 2026-06-19 |
| [Client_API_v46_Conformance_Batch2-report.md](reports/Client_API_v46_Conformance_Batch2-report.md) | [PRD](prds/Client_API_v46_Conformance_Batch2-prd.md) → [Plan](plans/Client_API_v46_Conformance_Batch2-prd-plan.md) | 2026-06-19 |
| [Client_API_v46_Conformance_Phase0-report.md](reports/Client_API_v46_Conformance_Phase0-report.md) | [PRD](prds/Client_API_v46_Conformance_Phase0-prd.md) → [Plan](plans/Client_API_v46_Conformance_Phase0-prd-plan.md) | 2026-06-19 |
| [DigitalZoom_RenderTransform-report.md](reports/DigitalZoom_RenderTransform-report.md) | [PRD](prds/DigitalZoom_RenderTransform-prd.md) → [Plan](plans/DigitalZoom_RenderTransform-prd-plan.md) | 2026-06-15 |
| [WebServer_Enable_Feature-report.md](reports/WebServer_Enable_Feature-report.md) | [PRD](prds/WebServer_Enable_Feature-prd.md) → [Plan](plans/WebServer_Enable_Feature-prd-plan.md) | 2026-06-05 |
| [SymbolUpdate_DispatcherFreeze_Fix-report.md](reports/SymbolUpdate_DispatcherFreeze_Fix-report.md) | [PRD](prds/SymbolUpdate_DispatcherFreeze_Fix-prd.md) → [Plan](plans/SymbolUpdate_DispatcherFreeze_Fix-prd-plan.md) | 2026-06-04 |
| [OverlayMap_MBTiles_Provider-report.md](reports/OverlayMap_MBTiles_Provider-report.md) | [PRD](prds/OverlayMap_MBTiles_Provider-prd.md) → [Plan](plans/OverlayMap_MBTiles_Provider-prd-plan.md) | 2026-06-02 |
| [Device_CompositeState_SSOT_And_FaultAutoRecovery-report.md](reports/Device_CompositeState_SSOT_And_FaultAutoRecovery-report.md) | [PRD](prds/Device_CompositeState_SSOT_And_FaultAutoRecovery-prd.md) → [Plan](plans/Device_CompositeState_SSOT_And_FaultAutoRecovery-prd-plan.md) | 2026-05-20 |
| [Skillset_Issues_And_Improvements_2026-05-19.md](reports/Skillset_Issues_And_Improvements_2026-05-19.md) | [PRD](prds/Skillset_Issues_And_Improvements_2026-05-19.md-prd.md) → [Plan](plans/Skillset_Issues_And_Improvements_2026-05-19.md-prd-plan.md) | 2026-05-19 |
| [Detection_Sound_And_DualPath_Fix-report.md](reports/Detection_Sound_And_DualPath_Fix-report.md) | [PRD](prds/Detection_Sound_And_DualPath_Fix-prd.md) → [Plan](plans/Detection_Sound_And_DualPath_Fix-prd-plan.md) | 2026-05-18 |
| [REPORT_GMapCustomControl_ImageDrag_BugFix.md](reports/REPORT_GMapCustomControl_ImageDrag_BugFix.md) | [PRD](prds/REPORT_GMapCustomControl_ImageDrag_BugFix.md-prd.md) → [Plan](plans/REPORT_GMapCustomControl_ImageDrag_BugFix.md-prd-plan.md) | 2026-05-15 |
| [REPORT_ImageOverlay_FileCopy_On_Register.md](reports/REPORT_ImageOverlay_FileCopy_On_Register.md) | [PRD](prds/REPORT_ImageOverlay_FileCopy_On_Register.md-prd.md) → [Plan](plans/REPORT_ImageOverlay_FileCopy_On_Register.md-prd-plan.md) | 2026-05-13 |
| [REPORT_DetectionEvent_Symbol_Visual_Restore.md](reports/REPORT_DetectionEvent_Symbol_Visual_Restore.md) | [PRD](prds/REPORT_DetectionEvent_Symbol_Visual_Restore.md-prd.md) → [Plan](plans/REPORT_DetectionEvent_Symbol_Visual_Restore.md-prd-plan.md) | 2026-05-12 |
| [REPORT_Broadcast_Panel_Embedded.md](reports/REPORT_Broadcast_Panel_Embedded.md) | [PRD](prds/REPORT_Broadcast_Panel_Embedded.md-prd.md) → [Plan](plans/REPORT_Broadcast_Panel_Embedded.md-prd-plan.md) | 2026-05-12 |
| [REPORT_Pids_Symbol_Background.md](reports/REPORT_Pids_Symbol_Background.md) | [PRD](prds/REPORT_Pids_Symbol_Background.md-prd.md) → [Plan](plans/REPORT_Pids_Symbol_Background.md-prd-plan.md) | 2026-05-12 |
| [REPORT_OverlayMap_Visibility_Activate.md](reports/REPORT_OverlayMap_Visibility_Activate.md) | [PRD](prds/REPORT_OverlayMap_Visibility_Activate.md-prd.md) → [Plan](plans/REPORT_OverlayMap_Visibility_Activate.md-prd-plan.md) | 2026-05-12 |
| [REPORT_EventCard_EntryId_Connection.md](reports/REPORT_EventCard_EntryId_Connection.md) | [PRD](prds/REPORT_EventCard_EntryId_Connection.md-prd.md) → [Plan](plans/REPORT_EventCard_EntryId_Connection.md-prd-plan.md) | 2026-05-12 |
| [REPORT_MBTiles_DefinedMap_Integration.md](reports/REPORT_MBTiles_DefinedMap_Integration.md) | [PRD](prds/REPORT_MBTiles_DefinedMap_Integration.md-prd.md) → [Plan](plans/REPORT_MBTiles_DefinedMap_Integration.md-prd-plan.md) | 2026-05-12 |
| [REPORT_DeviceGroupSelection_ProgressCircle.md](reports/REPORT_DeviceGroupSelection_ProgressCircle.md) | [PRD](prds/REPORT_DeviceGroupSelection_ProgressCircle.md-prd.md) → [Plan](plans/REPORT_DeviceGroupSelection_ProgressCircle.md-prd-plan.md) | 2026-05-12 |
| [REPORT_Gateway_DeviceGroup_Migration.md](reports/REPORT_Gateway_DeviceGroup_Migration.md) | [PRD](prds/REPORT_Gateway_DeviceGroup_Migration.md-prd.md) → [Plan](plans/REPORT_Gateway_DeviceGroup_Migration.md-prd-plan.md) | 2026-05-12 |
| [REPORT_Layer_Panel_Tree_Redesign.md](reports/REPORT_Layer_Panel_Tree_Redesign.md) | [PRD](prds/REPORT_Layer_Panel_Tree_Redesign.md-prd.md) → [Plan](plans/REPORT_Layer_Panel_Tree_Redesign.md-prd-plan.md) | 2026-05-12 |
| [REPORT_DeviceView_ViewModel_Alignment.md](reports/REPORT_DeviceView_ViewModel_Alignment.md) | [PRD](prds/REPORT_DeviceView_ViewModel_Alignment.md-prd.md) → [Plan](plans/REPORT_DeviceView_ViewModel_Alignment.md-prd-plan.md) | 2026-05-06 |
| [REPORT_Geolocation_AllDevices.md](reports/REPORT_Geolocation_AllDevices.md) | [PRD](prds/REPORT_Geolocation_AllDevices.md-prd.md) → [Plan](plans/REPORT_Geolocation_AllDevices.md-prd-plan.md) | 2026-05-06 |
| [REPORT_DeviceGroup_Assignment_Ui.md](reports/REPORT_DeviceGroup_Assignment_Ui.md) | [PRD](prds/REPORT_DeviceGroup_Assignment_Ui.md-prd.md) → [Plan](plans/REPORT_DeviceGroup_Assignment_Ui.md-prd-plan.md) | 2026-05-06 |
| [REPORT_SelectionView_Layout_Compact.md](reports/REPORT_SelectionView_Layout_Compact.md) | [PRD](prds/REPORT_SelectionView_Layout_Compact.md-prd.md) → [Plan](plans/REPORT_SelectionView_Layout_Compact.md-prd-plan.md) | 2026-05-06 |
| [REPORT_EventPanel_Cache_Reuse.md](reports/REPORT_EventPanel_Cache_Reuse.md) | [PRD](prds/REPORT_EventPanel_Cache_Reuse.md-prd.md) → [Plan](plans/REPORT_EventPanel_Cache_Reuse.md-prd-plan.md) | 2026-05-06 |
| [REPORT_PanelView_Missing_Columns.md](reports/REPORT_PanelView_Missing_Columns.md) | [PRD](prds/REPORT_PanelView_Missing_Columns.md-prd.md) → [Plan](plans/REPORT_PanelView_Missing_Columns.md-prd-plan.md) | 2026-05-06 |
| [REPORT_EventPanel_Cancel_Token.md](reports/REPORT_EventPanel_Cancel_Token.md) | [PRD](prds/REPORT_EventPanel_Cancel_Token.md-prd.md) → [Plan](plans/REPORT_EventPanel_Cancel_Token.md-prd-plan.md) | 2026-05-06 |
| [REPORT_DeviceTab_Header_Truncation.md](reports/REPORT_DeviceTab_Header_Truncation.md) | [PRD](prds/REPORT_DeviceTab_Header_Truncation.md-prd.md) → [Plan](plans/REPORT_DeviceTab_Header_Truncation.md-prd-plan.md) | 2026-05-06 |
| [REPORT_SelectionView_CheckBox_To_ComboBox.md](reports/REPORT_SelectionView_CheckBox_To_ComboBox.md) | [PRD](prds/REPORT_SelectionView_CheckBox_To_ComboBox.md-prd.md) → [Plan](plans/REPORT_SelectionView_CheckBox_To_ComboBox.md-prd-plan.md) | 2026-05-06 |
| [REPORT_Nats_Detection_Routing_Fix.md](reports/REPORT_Nats_Detection_Routing_Fix.md) | [PRD](prds/REPORT_Nats_Detection_Routing_Fix.md-prd.md) → [Plan](plans/REPORT_Nats_Detection_Routing_Fix.md-prd-plan.md) | 2026-05-06 |
| [REPORT_GetMarkerAtScreen_Priority_Fix.md](reports/REPORT_GetMarkerAtScreen_Priority_Fix.md) | [PRD](prds/REPORT_GetMarkerAtScreen_Priority_Fix.md-prd.md) → [Plan](plans/REPORT_GetMarkerAtScreen_Priority_Fix.md-prd-plan.md) | 2026-03-30 |
| [REPORT_Symbol_ZOrder_HitTest_Bug.md](reports/REPORT_Symbol_ZOrder_HitTest_Bug.md) | [PRD](prds/REPORT_Symbol_ZOrder_HitTest_Bug.md-prd.md) → [Plan](plans/REPORT_Symbol_ZOrder_HitTest_Bug.md-prd-plan.md) | 2026-03-30 |
| [REPORT_Symbol_ZOrder_Control.md](reports/REPORT_Symbol_ZOrder_Control.md) | [PRD](prds/REPORT_Symbol_ZOrder_Control.md-prd.md) → [Plan](plans/REPORT_Symbol_ZOrder_Control.md-prd-plan.md) | 2026-03-30 |
| [REPORT_Map_DragButton_LeftMouse.md](reports/REPORT_Map_DragButton_LeftMouse.md) | [PRD](prds/REPORT_Map_DragButton_LeftMouse.md-prd.md) → [Plan](plans/REPORT_Map_DragButton_LeftMouse.md-prd-plan.md) | 2026-03-30 |
| [REPORT_EditMode_HitTest_Passthrough.md](reports/REPORT_EditMode_HitTest_Passthrough.md) | [PRD](prds/REPORT_EditMode_HitTest_Passthrough.md-prd.md) → [Plan](plans/REPORT_EditMode_HitTest_Passthrough.md-prd-plan.md) | 2026-03-30 |
| [REPORT_OverlayImage_ZOrder_EditMode.md](reports/REPORT_OverlayImage_ZOrder_EditMode.md) | [PRD](prds/REPORT_OverlayImage_ZOrder_EditMode.md-prd.md) → [Plan](plans/REPORT_OverlayImage_ZOrder_EditMode.md-prd-plan.md) | 2026-03-27 |
| [REPORT_OverlayMap_ZOrder_Rendering.md](reports/REPORT_OverlayMap_ZOrder_Rendering.md) | [PRD](prds/REPORT_OverlayMap_ZOrder_Rendering.md-prd.md) → [Plan](plans/REPORT_OverlayMap_ZOrder_Rendering.md-prd-plan.md) | 2026-03-26 |
| [REPORT_Layer_Ordering_Investigation.md](reports/REPORT_Layer_Ordering_Investigation.md) | [PRD](prds/REPORT_Layer_Ordering_Investigation.md-prd.md) → [Plan](plans/REPORT_Layer_Ordering_Investigation.md-prd-plan.md) | 2026-03-26 |
| [REPORT_OverlayImage_Status_Analysis.md](reports/REPORT_OverlayImage_Status_Analysis.md) | [PRD](prds/REPORT_OverlayImage_Status_Analysis.md-prd.md) → [Plan](plans/REPORT_OverlayImage_Status_Analysis.md-prd-plan.md) | 2026-03-25 |
| [REPORT_MBTiles_ZoomLevel_Shadowing_Fix.md](reports/REPORT_MBTiles_ZoomLevel_Shadowing_Fix.md) | [PRD](prds/REPORT_MBTiles_ZoomLevel_Shadowing_Fix.md-prd.md) → [Plan](plans/REPORT_MBTiles_ZoomLevel_Shadowing_Fix.md-prd-plan.md) | 2026-03-24 |
| [REPORT_MapViewModel_Provider_Cleanup.md](reports/REPORT_MapViewModel_Provider_Cleanup.md) | [PRD](prds/REPORT_MapViewModel_Provider_Cleanup.md-prd.md) → [Plan](plans/REPORT_MapViewModel_Provider_Cleanup.md-prd-plan.md) | 2026-03-24 |
| [REPORT_EntryId_Nats_Uuid_DirectMatch.md](reports/REPORT_EntryId_Nats_Uuid_DirectMatch.md) | [PRD](prds/REPORT_EntryId_Nats_Uuid_DirectMatch.md-prd.md) → [Plan](plans/REPORT_EntryId_Nats_Uuid_DirectMatch.md-prd-plan.md) | 2026-03-13 |
| [REPORT_CollectionChanged_BatchReset.md](reports/REPORT_CollectionChanged_BatchReset.md) | [PRD](prds/REPORT_CollectionChanged_BatchReset.md-prd.md) → [Plan](plans/REPORT_CollectionChanged_BatchReset.md-prd-plan.md) | 2026-03-13 |
| [REPORT_SharedTimer_Chunk_Dequeue.md](reports/REPORT_SharedTimer_Chunk_Dequeue.md) | [PRD](prds/REPORT_SharedTimer_Chunk_Dequeue.md-prd.md) → [Plan](plans/REPORT_SharedTimer_Chunk_Dequeue.md-prd-plan.md) | 2026-03-13 |
| [REPORT_EventQueue_Logic_Analysis.md](reports/REPORT_EventQueue_Logic_Analysis.md) | [PRD](prds/REPORT_EventQueue_Logic_Analysis.md-prd.md) → [Plan](plans/REPORT_EventQueue_Logic_Analysis.md-prd-plan.md) | 2026-03-13 |
| [REPORT_EventQueue_Symbol_Unification.md](reports/REPORT_EventQueue_Symbol_Unification.md) | [PRD](prds/REPORT_EventQueue_Symbol_Unification.md-prd.md) → [Plan](plans/REPORT_EventQueue_Symbol_Unification.md-prd-plan.md) | 2026-03-13 |
| [REPORT_Batch_Action_Report.md](reports/REPORT_Batch_Action_Report.md) | [PRD](prds/REPORT_Batch_Action_Report.md-prd.md) → [Plan](plans/REPORT_Batch_Action_Report.md-prd-plan.md) | 2026-03-13 |
| [REPORT_DevicePanel_ProgressCircle_Fix.md](reports/REPORT_DevicePanel_ProgressCircle_Fix.md) | [PRD](prds/REPORT_DevicePanel_ProgressCircle_Fix.md-prd.md) → [Plan](plans/REPORT_DevicePanel_ProgressCircle_Fix.md-prd-plan.md) | 2026-03-12 |
| [ANALYSIS_DevicePanel_ProgressCircle_Visibility.md](reports/ANALYSIS_DevicePanel_ProgressCircle_Visibility.md) | [PRD](prds/ANALYSIS_DevicePanel_ProgressCircle_Visibility.md-prd.md) → [Plan](plans/ANALYSIS_DevicePanel_ProgressCircle_Visibility.md-prd-plan.md) | 2026-03-12 |
| [ANALYSIS_Redis_RTSP_Popup_Communication.md](reports/ANALYSIS_Redis_RTSP_Popup_Communication.md) | [PRD](prds/ANALYSIS_Redis_RTSP_Popup_Communication.md-prd.md) → [Plan](plans/ANALYSIS_Redis_RTSP_Popup_Communication.md-prd-plan.md) | 2026-03-12 |
| [REPORT_EventPanel_CentralizedDatePicker.md](reports/REPORT_EventPanel_CentralizedDatePicker.md) | [PRD](prds/REPORT_EventPanel_CentralizedDatePicker.md-prd.md) → [Plan](plans/REPORT_EventPanel_CentralizedDatePicker.md-prd-plan.md) | 2026-03-12 |
| [ANALYSIS_EventPanel_Chart_DataGrid_Mismatch.md](reports/ANALYSIS_EventPanel_Chart_DataGrid_Mismatch.md) | [PRD](prds/ANALYSIS_EventPanel_Chart_DataGrid_Mismatch.md-prd.md) → [Plan](plans/ANALYSIS_EventPanel_Chart_DataGrid_Mismatch.md-prd-plan.md) | 2026-03-12 |
| [REPORT_Event_BatchUI_Performance.md](reports/REPORT_Event_BatchUI_Performance.md) | [PRD](prds/REPORT_Event_BatchUI_Performance.md-prd.md) → [Plan](plans/REPORT_Event_BatchUI_Performance.md-prd-plan.md) | 2026-03-08 |
| [REPORT_Event_Pipeline_Redesign.md](reports/REPORT_Event_Pipeline_Redesign.md) | [PRD](prds/REPORT_Event_Pipeline_Redesign.md-prd.md) → [Plan](plans/REPORT_Event_Pipeline_Redesign.md-prd-plan.md) | 2026-03-08 |
| [ANALYSIS_Performance_Event_Processing_v2.md](reports/ANALYSIS_Performance_Event_Processing_v2.md) | [PRD](prds/ANALYSIS_Performance_Event_Processing_v2.md-prd.md) → [Plan](plans/ANALYSIS_Performance_Event_Processing_v2.md-prd-plan.md) | 2026-03-07 |
| [REPORT_DeviceGroup_AssignDialog_Wrapper.md](reports/REPORT_DeviceGroup_AssignDialog_Wrapper.md) | [PRD](prds/REPORT_DeviceGroup_AssignDialog_Wrapper.md-prd.md) → [Plan](plans/REPORT_DeviceGroup_AssignDialog_Wrapper.md-prd-plan.md) | 2026-03-07 |
| [REPORT_WindyMode_Nats_Integration.md](reports/REPORT_WindyMode_Nats_Integration.md) | [PRD](prds/REPORT_WindyMode_Nats_Integration.md-prd.md) → [Plan](plans/REPORT_WindyMode_Nats_Integration.md-prd-plan.md) | 2026-03-06 |
| [ANALYSIS_Performance_Event_Processing.md](reports/ANALYSIS_Performance_Event_Processing.md) | [PRD](prds/ANALYSIS_Performance_Event_Processing.md-prd.md) → [Plan](plans/ANALYSIS_Performance_Event_Processing.md-prd-plan.md) | 2026-03-06 |
| [REPORT_SymbolVisual_SyncDevice_Unification.md](reports/REPORT_SymbolVisual_SyncDevice_Unification.md) | [PRD](prds/REPORT_SymbolVisual_SyncDevice_Unification.md-prd.md) → [Plan](plans/REPORT_SymbolVisual_SyncDevice_Unification.md-prd-plan.md) | 2026-03-06 |
| [REPORT_PidsSymbol_Status_Visual_Fix.md](reports/REPORT_PidsSymbol_Status_Visual_Fix.md) | [PRD](prds/REPORT_PidsSymbol_Status_Visual_Fix.md-prd.md) → [Plan](plans/REPORT_PidsSymbol_Status_Visual_Fix.md-prd-plan.md) | 2026-03-06 |
| [REPORT_SyncDevice_SensorAllTypes_And_DeviceGroup.md](reports/REPORT_SyncDevice_SensorAllTypes_And_DeviceGroup.md) | [PRD](prds/REPORT_SyncDevice_SensorAllTypes_And_DeviceGroup.md-prd.md) → [Plan](plans/REPORT_SyncDevice_SensorAllTypes_And_DeviceGroup.md-prd-plan.md) | 2026-03-06 |
| [REPORT_NatsSync_PidsIndicator_Realtime_Fix.md](reports/REPORT_NatsSync_PidsIndicator_Realtime_Fix.md) | [PRD](prds/REPORT_NatsSync_PidsIndicator_Realtime_Fix.md-prd.md) → [Plan](plans/REPORT_NatsSync_PidsIndicator_Realtime_Fix.md-prd-plan.md) | 2026-03-06 |
| [REPORT_DeviceDetailUrl_SswSvms_Format.md](reports/REPORT_DeviceDetailUrl_SswSvms_Format.md) | [PRD](prds/REPORT_DeviceDetailUrl_SswSvms_Format.md-prd.md) → [Plan](plans/REPORT_DeviceDetailUrl_SswSvms_Format.md-prd-plan.md) | 2026-03-05 |
| [REPORT_Nats_SyncDevice_Handling.md](reports/REPORT_Nats_SyncDevice_Handling.md) | [PRD](prds/REPORT_Nats_SyncDevice_Handling.md-prd.md) → [Plan](plans/REPORT_Nats_SyncDevice_Handling.md-prd-plan.md) | 2026-03-05 |
| [REPORT_Speaker_Broadcast_ContextMenu.md](reports/REPORT_Speaker_Broadcast_ContextMenu.md) | [PRD](prds/REPORT_Speaker_Broadcast_ContextMenu.md-prd.md) → [Plan](plans/REPORT_Speaker_Broadcast_ContextMenu.md-prd-plan.md) | 2026-03-05 |
| [REPORT_PidsMarker_ContextMenu_DeviceDetail.md](reports/REPORT_PidsMarker_ContextMenu_DeviceDetail.md) | [PRD](prds/REPORT_PidsMarker_ContextMenu_DeviceDetail.md-prd.md) → [Plan](plans/REPORT_PidsMarker_ContextMenu_DeviceDetail.md-prd-plan.md) | 2026-03-05 |
| [REPORT_PidsMarker_FaultBlink_Animation.md](reports/REPORT_PidsMarker_FaultBlink_Animation.md) | [PRD](prds/REPORT_PidsMarker_FaultBlink_Animation.md-prd.md) → [Plan](plans/REPORT_PidsMarker_FaultBlink_Animation.md-prd-plan.md) | 2026-03-05 |
| [REPORT_Camera_PtzStatus_Nats_Service.md](reports/REPORT_Camera_PtzStatus_Nats_Service.md) | [PRD](prds/REPORT_Camera_PtzStatus_Nats_Service.md-prd.md) → [Plan](plans/REPORT_Camera_PtzStatus_Nats_Service.md-prd-plan.md) | 2026-03-05 |
| [REPORT_GMaps_Pids_SmartSensor_Symbol.md](reports/REPORT_GMaps_Pids_SmartSensor_Symbol.md) | [PRD](prds/REPORT_GMaps_Pids_SmartSensor_Symbol.md-prd.md) → [Plan](plans/REPORT_GMaps_Pids_SmartSensor_Symbol.md-prd-plan.md) | 2026-03-05 |
| [REPORT_FaultFence_GroupSymbol_Color_Fix.md](reports/REPORT_FaultFence_GroupSymbol_Color_Fix.md) | [PRD](prds/REPORT_FaultFence_GroupSymbol_Color_Fix.md-prd.md) → [Plan](plans/REPORT_FaultFence_GroupSymbol_Color_Fix.md-prd-plan.md) | 2026-03-05 |
| [REPORT_ActionReport_Flow_Analysis.md](reports/REPORT_ActionReport_Flow_Analysis.md) | [PRD](prds/REPORT_ActionReport_Flow_Analysis.md-prd.md) → [Plan](plans/REPORT_ActionReport_Flow_Analysis.md-prd-plan.md) | 2026-03-05 |
| [REPORT_GMaps_Pids_Speaker_Symbol.md](reports/REPORT_GMaps_Pids_Speaker_Symbol.md) | [PRD](prds/REPORT_GMaps_Pids_Speaker_Symbol.md-prd.md) → [Plan](plans/REPORT_GMaps_Pids_Speaker_Symbol.md-prd-plan.md) | 2026-03-05 |
| [REPORT_Nats_MessageService_Wiring_Fix.md](reports/REPORT_Nats_MessageService_Wiring_Fix.md) | [PRD](prds/REPORT_Nats_MessageService_Wiring_Fix.md-prd.md) → [Plan](plans/REPORT_Nats_MessageService_Wiring_Fix.md-prd-plan.md) | 2026-03-05 |
| [REPORT_Pids_NatsSync_OperationState_Realtime.md](reports/REPORT_Pids_NatsSync_OperationState_Realtime.md) | [PRD](prds/REPORT_Pids_NatsSync_OperationState_Realtime.md-prd.md) → [Plan](plans/REPORT_Pids_NatsSync_OperationState_Realtime.md-prd-plan.md) | 2026-03-05 |
| [REPORT_NATS_Event_Integration.md](reports/REPORT_NATS_Event_Integration.md) | [PRD](prds/REPORT_NATS_Event_Integration.md-prd.md) → [Plan](plans/REPORT_NATS_Event_Integration.md-prd-plan.md) | 2026-03-04 |
| [REPORT_Nats_Setup_Refactoring.md](reports/REPORT_Nats_Setup_Refactoring.md) | [PRD](prds/REPORT_Nats_Setup_Refactoring.md-prd.md) → [Plan](plans/REPORT_Nats_Setup_Refactoring.md-prd-plan.md) | 2026-03-04 |
| [REPORT_GMap_Pids_Indicator_OperationState_Policy.md](reports/REPORT_GMap_Pids_Indicator_OperationState_Policy.md) | [PRD](prds/REPORT_GMap_Pids_Indicator_OperationState_Policy.md-prd.md) → [Plan](plans/REPORT_GMap_Pids_Indicator_OperationState_Policy.md-prd-plan.md) | 2026-03-04 |
| [REPORT_EventPanel_Remove_Zone_Add_ActionReported.md](reports/REPORT_EventPanel_Remove_Zone_Add_ActionReported.md) | [PRD](prds/REPORT_EventPanel_Remove_Zone_Add_ActionReported.md-prd.md) → [Plan](plans/REPORT_EventPanel_Remove_Zone_Add_ActionReported.md-prd-plan.md) | 2026-03-04 |
| [REPORT_GMap_PidsGroup_DeviceGroup_Integration.md](reports/REPORT_GMap_PidsGroup_DeviceGroup_Integration.md) | [PRD](prds/REPORT_GMap_PidsGroup_DeviceGroup_Integration.md-prd.md) → [Plan](plans/REPORT_GMap_PidsGroup_DeviceGroup_Integration.md-prd-plan.md) | 2026-03-04 |
| [REPORT_DeviceAssignDialog_MultiSelect_And_RemoveConfirm.md](reports/REPORT_DeviceAssignDialog_MultiSelect_And_RemoveConfirm.md) | [PRD](prds/REPORT_DeviceAssignDialog_MultiSelect_And_RemoveConfirm.md-prd.md) → [Plan](plans/REPORT_DeviceAssignDialog_MultiSelect_And_RemoveConfirm.md-prd-plan.md) | 2026-03-04 |
| [REPORT_DevicePanel_CRUD_And_GroupAssignment.md](reports/REPORT_DevicePanel_CRUD_And_GroupAssignment.md) | [PRD](prds/REPORT_DevicePanel_CRUD_And_GroupAssignment.md-prd.md) → [Plan](plans/REPORT_DevicePanel_CRUD_And_GroupAssignment.md-prd-plan.md) | 2026-03-04 |
| [REPORT_GMaps_Db_Safe_Enum_Parse.md](reports/REPORT_GMaps_Db_Safe_Enum_Parse.md) | [PRD](prds/REPORT_GMaps_Db_Safe_Enum_Parse.md-prd.md) → [Plan](plans/REPORT_GMaps_Db_Safe_Enum_Parse.md-prd-plan.md) | 2026-03-04 |
| [REPORT_Dashboard_Loading_Progress.md](reports/REPORT_Dashboard_Loading_Progress.md) | [PRD](prds/REPORT_Dashboard_Loading_Progress.md-prd.md) → [Plan](plans/REPORT_Dashboard_Loading_Progress.md-prd-plan.md) | 2026-03-03 |
| [REPORT_EventDashboard_Statistics_Integration.md](reports/REPORT_EventDashboard_Statistics_Integration.md) | [PRD](prds/REPORT_EventDashboard_Statistics_Integration.md-prd.md) → [Plan](plans/REPORT_EventDashboard_Statistics_Integration.md-prd-plan.md) | 2026-03-03 |
| [REPORT_DevicePanel_InfiniteScroll_Pagination.md](reports/REPORT_DevicePanel_InfiniteScroll_Pagination.md) | [PRD](prds/REPORT_DevicePanel_InfiniteScroll_Pagination.md-prd.md) → [Plan](plans/REPORT_DevicePanel_InfiniteScroll_Pagination.md-prd-plan.md) | 2026-03-03 |
| [REPORT_Tab_Switch_Info_Refresh.md](reports/REPORT_Tab_Switch_Info_Refresh.md) | [PRD](prds/REPORT_Tab_Switch_Info_Refresh.md-prd.md) → [Plan](plans/REPORT_Tab_Switch_Info_Refresh.md-prd-plan.md) | 2026-03-03 |
| [REPORT_Chart_Empty_Data_Display.md](reports/REPORT_Chart_Empty_Data_Display.md) | [PRD](prds/REPORT_Chart_Empty_Data_Display.md-prd.md) → [Plan](plans/REPORT_Chart_Empty_Data_Display.md-prd-plan.md) | 2026-03-03 |
| [REPORT_ActionEvent_Loading_Fix.md](reports/REPORT_ActionEvent_Loading_Fix.md) | [PRD](prds/REPORT_ActionEvent_Loading_Fix.md-prd.md) → [Plan](plans/REPORT_ActionEvent_Loading_Fix.md-prd-plan.md) | 2026-02-27 |
| [REPORT_Event_UnitTest_Coverage.md](reports/REPORT_Event_UnitTest_Coverage.md) | [PRD](prds/REPORT_Event_UnitTest_Coverage.md-prd.md) → [Plan](plans/REPORT_Event_UnitTest_Coverage.md-prd-plan.md) | 2026-02-27 |
| [REPORT_EventPanel_Loading_State_Fix.md](reports/REPORT_EventPanel_Loading_State_Fix.md) | [PRD](prds/REPORT_EventPanel_Loading_State_Fix.md-prd.md) → [Plan](plans/REPORT_EventPanel_Loading_State_Fix.md-prd-plan.md) | 2026-02-27 |
| [REPORT_Event_InfiniteScroll_Pagination.md](reports/REPORT_Event_InfiniteScroll_Pagination.md) | [PRD](prds/REPORT_Event_InfiniteScroll_Pagination.md-prd.md) → [Plan](plans/REPORT_Event_InfiniteScroll_Pagination.md-prd-plan.md) | 2026-02-27 |
| [REPORT_Event_DtoModel_Matching.md](reports/REPORT_Event_DtoModel_Matching.md) | [PRD](prds/REPORT_Event_DtoModel_Matching.md-prd.md) → [Plan](plans/REPORT_Event_DtoModel_Matching.md-prd-plan.md) | 2026-02-26 |
| [REPORT_Dashboard_NewDevice_Fetch_Fix.md](reports/REPORT_Dashboard_NewDevice_Fetch_Fix.md) | [PRD](prds/REPORT_Dashboard_NewDevice_Fetch_Fix.md-prd.md) → [Plan](plans/REPORT_Dashboard_NewDevice_Fetch_Fix.md-prd-plan.md) | 2026-02-26 |
| [REPORT_AllDevices_Geo_Sync_Fix.md](reports/REPORT_AllDevices_Geo_Sync_Fix.md) | [PRD](prds/REPORT_AllDevices_Geo_Sync_Fix.md-prd.md) → [Plan](plans/REPORT_AllDevices_Geo_Sync_Fix.md-prd-plan.md) | 2026-02-26 |
| [REPORT_Sensor_UpdateProperties_Geo_Fix.md](reports/REPORT_Sensor_UpdateProperties_Geo_Fix.md) | [PRD](prds/REPORT_Sensor_UpdateProperties_Geo_Fix.md-prd.md) → [Plan](plans/REPORT_Sensor_UpdateProperties_Geo_Fix.md-prd-plan.md) | 2026-02-26 |
| [REPORT_Sensor_DeviceEquals_Fix.md](reports/REPORT_Sensor_DeviceEquals_Fix.md) | [PRD](prds/REPORT_Sensor_DeviceEquals_Fix.md-prd.md) → [Plan](plans/REPORT_Sensor_DeviceEquals_Fix.md-prd-plan.md) | 2026-02-26 |
| [REPORT_Camera_Dto_Mapping_Fix.md](reports/REPORT_Camera_Dto_Mapping_Fix.md) | [PRD](prds/REPORT_Camera_Dto_Mapping_Fix.md-prd.md) → [Plan](plans/REPORT_Camera_Dto_Mapping_Fix.md-prd-plan.md) | 2026-02-26 |
| [REPORT_Camera_IsRecord_Editable.md](reports/REPORT_Camera_IsRecord_Editable.md) | [PRD](prds/REPORT_Camera_IsRecord_Editable.md-prd.md) → [Plan](plans/REPORT_Camera_IsRecord_Editable.md-prd-plan.md) | 2026-02-26 |
| [REPORT_Camera_Model_Cleanup_And_DetailView.md](reports/REPORT_Camera_Model_Cleanup_And_DetailView.md) | [PRD](prds/REPORT_Camera_Model_Cleanup_And_DetailView.md-prd.md) → [Plan](plans/REPORT_Camera_Model_Cleanup_And_DetailView.md-prd-plan.md) | 2026-02-25 |
| [REPORT_DevicePanel_CRUD_Completion.md](reports/REPORT_DevicePanel_CRUD_Completion.md) | [PRD](prds/REPORT_DevicePanel_CRUD_Completion.md-prd.md) → [Plan](plans/REPORT_DevicePanel_CRUD_Completion.md-prd-plan.md) | 2026-02-25 |
| [REPORT_DeviceGroup_Ui.md](reports/REPORT_DeviceGroup_Ui.md) | [PRD](prds/REPORT_DeviceGroup_Ui.md-prd.md) → [Plan](plans/REPORT_DeviceGroup_Ui.md-prd-plan.md) | 2026-02-25 |
| [REPORT_EventUi_DeviceProperty_Binding.md](reports/REPORT_EventUi_DeviceProperty_Binding.md) | [PRD](prds/REPORT_EventUi_DeviceProperty_Binding.md-prd.md) → [Plan](plans/REPORT_EventUi_DeviceProperty_Binding.md-prd-plan.md) | 2026-02-25 |
| [REPORT_EventApi_IntegrationTest.md](reports/REPORT_EventApi_IntegrationTest.md) | [PRD](prds/REPORT_EventApi_IntegrationTest.md-prd.md) → [Plan](plans/REPORT_EventApi_IntegrationTest.md-prd-plan.md) | 2026-02-25 |
| [REPORT_CameraPreset_ROI_Point_Api.md](reports/REPORT_CameraPreset_ROI_Point_Api.md) | [PRD](prds/REPORT_CameraPreset_ROI_Point_Api.md-prd.md) → [Plan](plans/REPORT_CameraPreset_ROI_Point_Api.md-prd-plan.md) | 2026-02-24 |
| [REPORT_ServerApi_IntegrationTest.md](reports/REPORT_ServerApi_IntegrationTest.md) | [PRD](prds/REPORT_ServerApi_IntegrationTest.md-prd.md) → [Plan](plans/REPORT_ServerApi_IntegrationTest.md-prd-plan.md) | 2026-02-24 |
| [REPORT_DeviceApi_IntegrationTest.md](reports/REPORT_DeviceApi_IntegrationTest.md) | [PRD](prds/REPORT_DeviceApi_IntegrationTest.md-prd.md) → [Plan](plans/REPORT_DeviceApi_IntegrationTest.md-prd-plan.md) | 2026-02-24 |

<!-- auto-section-end -->

## 議곗쑉/?뺤씤?붿껌 (docs/coordination/)

| ?뚯씪 | ???| ?댁슜 | ?좎쭨 |
|------|------|------|------|
| [REQ_Proxy_WINDY_RSP_ReplyTo.md](coordination/REQ_Proxy_WINDY_RSP_ReplyTo.md) | PidsProxy ?대떦 | WINDY(?띾웾紐⑤뱶) RSP 誘몄닔????reply-to ?뚯떊 寃쎈줈 ?먭? ?붿껌 | 2026-07-23 |
| [gop-rfp-checklist-gis-scope.md](coordination/gop-rfp-checklist-gis-scope.md) | API서버 세션 대상 | GOP RFP 점검표 GIS 클라이언트 관점 판정 18건 — 통합상황도 3.3.4.12.x 9행(점검표 반영) + 인접 9행(API서버 판정과 계층 구분) · 신규 결함 5종(MGRS 라벨 데드코드 · PTZ 사문 서비스 · PTZ_STATUS 실무 무수신 · GeoTIFF CRS 재투영 부재 · 오버레이 무음실패) · **last-write-wins 저장 사고/복구 기록 + 동시편집 금지 권고** | 2026-08-07 |
| [symbol-3d-housing-handoff.md](coordination/symbol-3d-housing-handoff.md) | PRD 검토 세션 대상 | 지도 심볼 **3D 하우징 PRD 검토 인계** — 사용자 요구 10건 이력 · 파일 지도(정본 5종·캡처 6종) · 조사한 코드 17지점(file:line) · **선행 문서 오류 4건(E-1~E-4)** · 결정 15건 요지(뒤집힌 4건 G-6/8/14/15) · 스코프 원칙(base 확장이 가산적인 이유) · 시뮬 626 ISSUE 는 대부분 대조군 · 미검증 V-01~15 + 성능 근거 0 · 검토 요청 5항 · 금지 5항(게이트 force·승인 대행·운영 DB 테스트) | 2026-09-06 |
| [GOP_Server_API_suppression_recurrence_GIS_NOTIFY.md](coordination/GOP_Server_API_suppression_recurrence_GIS_NOTIFY.md) | 서버팀 → GIS | 억제 스케줄 **주간 반복 + 기간 무제한** 배포 안내 (API **6.3.3**) — `recurrence_type`·`days_of_week`(월=0 비트마스크)·`daily_*`(offset 금지)·`window_end: null` 명시 전송 | 2026-09-07 |
| [GOP_Server_API_suppression_input_guards_GIS_NOTIFY.md](coordination/GOP_Server_API_suppression_input_guards_GIS_NOTIFY.md) | 서버팀 → GIS | 억제 스케줄 **입력 검증 강화** (API **6.3.4**) — **422 5종 신설**(도달 불가 창 · 비자정 동일 시각 · 연도 범위 · 단발 366일 · `all`+대상배열) + PATCH 동일 검증 + `next_occurrence_start` 정확도 수정. **필수 대응은 센티널 `9999-12-31` 제거 하나**(GIS 는 해당 0건). ⇒ 대응 완료 커밋 `248b160e` · PRD v1.1 FR-19 | 2026-09-08 |

## ?ㅺ퀎 (docs/design/)

| ?뚯씪 | ?댁슜 | ?좎쭨 |
|------|------|------|
| [window-surface-kernel-storyboard.html](design/window-surface-kernel-storyboard.html) | **창 아키텍처 재설계 스토리보드 / 와이어프레임 v1.0** — PRD [window-architecture-prd](prds/window-architecture-prd.md)와 동반 산출. 실제 토큰(`Tokens.{Light,Dark}.xaml`)으로 렌더하고 WPF `#AARRGGBB`→CSS `rgba()` 변환 명시(현행 `#88000000`=.53 · Light `#8C0E161F`=.55 · Dark `#B8040810`=**.72**). 구성 10절: ①현행 시각트리(모달 3층이 `LeftMenuSectionView:271` 자식) ②목표 Surface Kernel ③**조작 가능한 와이어프레임**(Float 드래그·리사이즈·최대화·창목록·클램프 금지구역) ④표면 5종 배치표 ⑤Before/After 조치보고 ⑥**GAP-2 라이트 다크카드 대비 1.05:1 재현 vs 제안** ⑦**GAP-9 스크림 4조합**(주간/야간 × 라이트/다크) ⑧**권한 UX — 역할 전환 데모(ADMIN/OPERATOR/VIEWER) + 무음 7곳 → Toast + 누락 위험 7지점** ⑨다이얼로그 API Before/After ⑩확인 요청 6건. 브라우저 실측 검증(Chromium) — 스크립트 오류 0, 검증 중 클래스 충돌 결함 1건 발견·수정(`.alarm` ↔ `.sym.alarm`), 수정 후 재실행은 브라우저 점유로 **미실행**(정적 검사만). 캡처 `docs/assets/window-surface-{wireframe-dark,gap2-light,perm-light}.png` | 2026-09-06 |
 [action-report-custom-template-storyboard.html](design/action-report-custom-template-storyboard.html) | 조치보고 커스텀 문구 스토리보드 v1.0 — 흐름 S1~S13(정형선택·보강입력·직접입력·검증실패·로드실패·추가/중복·수정/삭제·순서변경·동기화·권한·기본값복구), 각 프레임에 SIM ID | 2026-08-07 |
 [action-report-custom-template-wireframe.html](design/action-report-custom-template-wireframe.html) | 조치보고 커스텀 문구 와이어프레임 v1.0 — **치수(다이얼로그 900×660 · 관리패널 840×640)** · 컨트롤트리/바인딩 · 상태기계 · API매핑(문서와 다른 계약 3건) · 검증규칙 · 토큰 · 권한 · AutomationId · 확정결함 34건 · 미결 G1~G8 | 2026-08-07 |
| [event-mapping-workbench-storyboard.html](design/event-mapping-workbench-storyboard.html) | 이벤트 매핑 워크벤치 스토리보드 v1.0 — 3-Pane 워크벤치(목록/액션보드/장비팔레트) · DnD 3종(멀티셀렉션 등록 · 슬롯드롭 프리셋/음원/색상 · 드래그백 해제) · 부분실패·고아·권한 · 흐름 S1~S8. Dark/Light 토큰 실측 | 2026-08-07 |
| [event-mapping-workbench-wireframe.html](design/event-mapping-workbench-wireframe.html) | 이벤트 매핑 워크벤치 와이어프레임 v1.0 — **치수(1120×680, 카드 4규격 고정) · 컨트롤 트리/바인딩 · DnD 상태기계+드롭 유효성 매트릭스 · Draft 커밋 3단계 · 토큰 매핑 · 권한 · AutomationId · 결함 D1~D10 정본 · 미결 Q1~Q10** | 2026-08-07 |
| [detection-fault-trafficlight-wireframe.html](design/detection-fault-trafficlight-wireframe.html) | 탐지·장애 신호등 와이어프레임 v1.2 — **★확정: 신호등=이벤트 카드 창 헤더(B′)+T1 · 시스템=지도 상단바 R1 도넛(분리) · 기존 pill 계기 제거(D2)+보기>탐지·장애(Ctrl+Shift+F) 토글을 신호등 표시로 재연결**. 대안 기록·상태 매트릭스·클릭=같은 창 필터·Dark/Light | 2026-08-06 |
| [map-topbar-simplification-wireframe.html](design/map-topbar-simplification-wireframe.html) | 지도 상단 메뉴·툴바 간소화 v1.0 — **운영/편집 이원화**: 운영 기본=콤보1+아이콘7+편집스위치(현행 20컨트롤→9, -55%), 편집 ON 시 2번째 줄 편집 스트립(등록: 맵/이미지/커스텀맵 · 심볼 · 선택 · 기준) 확장. 전 항목 이동 매핑표(누락 0)·중복 2건 삭제(툴바 십자선, 파일>타일폴더)·메뉴 파일 폐지→지도/보기 2종·편집 스위치 권한자만 렌더·Q1~Q4 결정 대기 | 2026-08-06 |
| [detection-fault-trafficlight-storyboard.html](design/detection-fault-trafficlight-storyboard.html) | 탐지·장애 신호등 스토리보드 v1.1(확정 B′ 기준) — 9장면(정상→탐지 펄스→장애 상시펄스→동시 점등→클릭=같은 창 필터·포커스→툴팁/우클릭 설정→분리 배치 구도(상단바 도넛+헤더 신호등)→미초기화 '—' 게이트→테마·3중 부호화) EQM/NATS 소스·불변식 표기 | 2026-08-06 |
| [event-suppression-recurrence-wireframe.html](design/event-suppression-recurrence-wireframe.html) | **억제 주간반복 와이어프레임 v1.0** — 치수·바인딩·직렬화 계약. Row3 격리(단발 세로증가 **0**) · 요일 스트립 ToggleButton×7 드래그 범위선택 · 검증 매트릭스(**서버가 안 막는 2건**) · 요일비트 원점변환(월0 vs 일0) · **적대검증 fatal 10 반영**(TimePicker `SelectedTime` **부재** · `DateTimeZoneHandling.Local` · 무제한 게이트 불일치 · 배너 **로컬값>Trigger** · 제스처바 1.00:1 · TimePickerBase **UIA 미노출**) | 2026-09-07 |
| [event-suppression-recurrence-storyboard.html](design/event-suppression-recurrence-storyboard.html) | **억제 주간반복 스토리보드 v1.0** — 흐름·문구 S1~S13. S1=단발 무변화 회귀 · S5=서버가 안 막는 입력(요일0개→**유령 창**, start==end→**24h 종일**) · S6=반복→단발 되돌리기(적대검증 실결함) · S7=`status`≠억제중(**62.2%**) · S8=배너 stale 병기 · S9=키보드 폴백(**UIA에 드래그 단언 불가**) · 안내문구 17종 · 미결 6 | 2026-09-07 |
| [pidsgroup-rightclick-storyboard.html](design/pidsgroup-rightclick-storyboard.html) | PidsGroup ?고겢由???댁뼱?꾨젅???ㅽ넗由щ낫??v1.0 ??吏꾩엯??3怨?留?援ъ뿭 ?щ낵 ?고겢由?硫붾돱쨌?ㅻ쾭?덉씠 ???고겢由?룹옣鍮꾩젙蹂?洹몃９ ?????고겢由?쨌?깅줉 ?쇱꽌 ?뺣낫 ?ㅻ쾭?덉씠(LayerPanel ?쒖? ?щ＼, DeviceProvider ??븘???쒕쾭蹂寃?0)쨌洹몃９ ?먯? ?대젰 ?뺤옣???쇱꽌 ?ъ븘??硫???쒕━利?李⑦듃, ?붾젅??CVD 寃利?쨌S1~S6 ?꾨쫫?ㅽ듃由승룸━?ㅽ겕/?곗씠??怨꾩빟/Phase 1쨌2 遺꾪븷쨌?ㅽ넗??Dark/Light ?좉? | 2026-08-06 |
| [installer-wizard-plan.html](design/installer-wizard-plan.html) | IRONWALL 愿??SW Inno Setup ?몄뒪?⑤윭 湲고쉷??v1.0 ??16?붾㈃ ?꾩?????댁뼱?꾨젅???ㅽ넗由щ낫???좉퇋/?낃렇?덉씠??遺꾧린쨌?덉쇅 3醫?쨌appsettings.json ?꾩옣?ㅼ젙 ?④퀎蹂??낅젰(?꾧끝 紐낆묶: ?듯빀/?쒕뱶?뚰떚 釉뚮줈而??쒕쾭쨌?듯빀 愿???쒕쾭쨌?곗씠????μ냼)쨌?뱀쭠 蹂대뱶 2??諛곕꼫 ?쒖븞(踰꾩쟾 ?쒓린 湲덉?)쨌?ㅼ젙 湲곕줉(?뚮젅?댁뒪????쒗뵆由?/蹂댁〈 ?뺤콉쨌湲곗닠 ?ㅺ퀎 ?명듃쨌誘멸껐 Q5 | 2026-08-04 |
| [event-suppression-schedule-wireframe.html](design/event-suppression-schedule-wireframe.html) | ?대깽???듭젣(?뺣퉬 李? ?ㅼ?以?愿由???댁뼱?꾨젅??v1.0 ??G-1 CRUD ?⑤꼸(?꾧뎄紐⑥쓬+?몃씪?명뤌 DateTimePicker+DataGrid ?곹깭諛곗? pill+臾댄븳?ㅽ겕濡?쨌G-2 ?쒖꽦 諛곕꼫(SurfaceTranslucent+StatusWarning)쨌G-3 ?ㅒ룰텒??disable ?쒕?쨌Conductor PanelShell쨌Tactical Dark/Light ?좉? | 2026-07-31 |
| [event-suppression-schedule-storyboard.html](design/event-suppression-schedule-storyboard.html) | ?대깽???듭젣 ?ㅼ?以??ㅽ넗由щ낫??v1.0 ??9?λ㈃ ?ъ슜???먮쫫(吏꾩엯?믪깮?기넂寃利앪넂紐⑸줉/?꾪꽣?믪닔?뺚넂痍⑥냼 Confirm?믫솢?깅같?댿넂?쇱씠釉뚮뵥(P2)?믨텒?? 媛??λ㈃ REST/NATS/UI 硫붿떆吏 ?쒓린쨌Dark/Light | 2026-07-31 |
| [action-report-dialogs-wireframe.html](design/action-report-dialogs-wireframe.html) | ?먯?쨌?μ븷 議곗튂蹂닿퀬 ?ㅼ씠?쇰줈洹???댁뼱?꾨젅??????李쎌쓣 Tactical Command ?ㅽ넗??Dark/Light 誘몃윭+?좉?)?쇰줈 ?뚮뜑쨌?먯?=醫??띿꽦 ?ㅽ겕濡?MaxHeight 156)+???몃꽕???깃났 ?대?吏/?ㅽ뙣쨌遺??湲곕낯?붾㈃) **?곸슜??*쨌?μ븷=?숈씪 ?먯튃 ?뺣━ ?쒖븞(醫??띿꽦+??FirstStart/End쨌SecondStart/End 援ш컙媛?移대뱶)쨌怨듭쑀 議곗튂蹂닿퀬 ??ぉ(?쇰뵒??5+湲고? 硫붾え)쨌?뺤씤/痍⑥냼쨌援ъ“ 二쇱꽍 5횞2 | 2026-07-31 |
| [GMap_Windy_Indicator-wireframe.html](design/GMap_Windy_Indicator-wireframe.html) | 媛뺥뭾紐⑤뱶(WINDY) ?몃뵒耳?댄꽣 CustomControl ??댁뼱?꾨젅??v1.0 ??4紐⑤뱶(wind0~3) ?꾩씠肄????꾪솚쨌?꾩씠肄??쇰꺼/?꾩씠肄섎쭔쨌?됱긽?쒖닲源쨌?쒕옒洹??곸냽쨌z6 怨꾧린痢돠룸낫湲?View)硫붾돱 ?좉?쨌?ㅽ넗??Dark/Light | 2026-07-31 |
| [GMap_Detection_Fault_Indicator-wireframe.html](design/GMap_Detection_Fault_Indicator-wireframe.html) | ?먯?쨌?μ븷 ?곹깭 ?몃뵒耳?댄꽣 CustomControl ??댁뼱?꾨젅??v1.0 ???먯?(EnumDetectionType)/?μ븷(EnumFaultType) 吏묎퀎 pill+??낆묩쨌?쒖꽦 媛뺤“쨌0嫄댁닲源쨌?몃줈/媛濡쑣룸뱶?섍렇 ?곸냽쨌z6쨌蹂닿린硫붾돱쨌?ㅽ넗??Dark/Light | 2026-07-31 |
| [GMap_Compass_Control-wireframe.html](design/GMap_Compass_Control-wireframe.html) | 諛⑹쐞媛??щ낵(?섏묠諛? CustomControl ??댁뼱?꾨젅??**v2.2** ???덉씠?대쭅 ?뺤젙??PropertyPanelCanvas ?먯떇 z6=怨꾧린痢? OverlayWindow/?앹뾽/?덈툕 ?꾨옒, 理쒖긽??鍮꾧텒??洹쇨굅 3)쨌 Theme ?ㅽ넗??Dark/Light 誘몃윭)+?뙔/? ?좉?쨌?좏겙 留ㅽ븨?쑣룸씪?대툕 ?곕え(罐쨌?쒕옒洹??대룞쨌留??뚯쟾쨌?붾툝?대┃ ?뺣턿쨌**?고겢由??ㅼ젙 硫붾돱**=?ъ슜???뺤젙: ?ш린 S/M/L쨌?ㅽ??셋룰컖?꾪몴?쑣룹젙遺? AppSettings.MapCompass ?곸냽 ?쒖븞)쨌蹂??A濡쒖쫰/B留겶룻빐遺?꽷룹꽕怨꽷룸?寃곗쭏臾?5 | 2026-07-30 |
| [line-drawing-hud-redesign-preview.html](design/line-drawing-hud-redesign-preview.html) | ?쇱씤쨌援ъ뿭쨌PIDS ?쒕줈??HUD 由щ뵒?먯씤 ?꾨━酉????쒖? ?⑤꼸 ?⑦꽩(?쒖븞 ?ㅻ뜑+?곗긽??X ?リ린)쨌?ㅽ겕/?쇱씠??Tactical ?좏겙쨌Before/After쨌?꾩튂 ?좎? 踰꾧렇 before/after쨌?좏겙 留ㅽ븨 | 2026-07-28 |
| [Measure_Tools_Storyboard.html](design/Measure_Tools_Storyboard.html) | 痢≪젙 ??湲몄씠쨌?볦씠) ?ㅽ넗由щ낫?쑣룹??댁뼱?꾨젅??v0.1 ???묐찓???뚯륫?뺛?洹몃９(Ruler/VectorPolygon)쨌?대┃?믪떎?쒓컙 由щ뱶?꾩썐?믪셿猷??뚮줈?걔룹??ㅻ뜲??嫄곕━/硫댁쟻쨌?ㅽ겕/?쇱씠???좏겙쨌?ъ궗?⑸㏊(LineDrawingAdorner ?ы겕)쨌誘멸껐吏덈Ц 7 | 2026-07-24 |
| [Detection_Signal_History_Storyboard.html](design/Detection_Signal_History_Storyboard.html) | ?먯? ?좏샇(detail.signal) ?쒕㈃????댁뼱?꾨젅??& ?ㅽ넗由щ낫??v2 ???대젰洹몃━???좏샇而щ읆(+MessageType ?쒓굅)쨌移대뱶 蹂묎린쨌?고겢由?吏꾩엯 2醫??щ낵/DevicePanel)쨌?앹뾽 ?ㅼ씠?쇰줈洹?李⑦듃+洹몃━??쨌?ㅽ겕/?쇱씠???좉? | 2026-07-23 |

## assets/

| 파일 | 설명 |
|---|---|
| [symbol-3d/housing-native-gallery-20260907.png](assets/symbol-3d/housing-native-gallery-20260907.png) | 3D 하우징 갤러리 PNG(2026-09-07, 32종 + 통문 fencegate·함체 도어 관절 후 재생성, R-06 닫힌 크기 확인) |
| [symbol-3d/housing-palette-light-20260907.png](assets/symbol-3d/housing-palette-light-20260907.png) | 팔레트 라이트 렌더(20 PIDS 셀, 통문 포함) |
| [symbol-3d/housing-palette-dark-20260907.png](assets/symbol-3d/housing-palette-dark-20260907.png) | 팔레트 다크 렌더 |
