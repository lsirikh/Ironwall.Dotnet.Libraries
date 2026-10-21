# DECISIONS — t-20261021-101626-2fe7 (3D 환경: 해 · 날씨 · 차 · 사람, GIS 쪽)

## 1. 설정 화면 묶음 · 3D 빠른 판의 저장 방식 — 즉시 자동 저장 vs 저장 막대(초안 · [저장])
- options: (a) 바꾸면 바로 3D 반영 + 0.8초 뒤 자동 저장, 설정 창 저장 막대와는 잇지 않음(초안 0) / (b) 설정 창은 초안 · [저장] · [되돌리기], 빠른 판만 자동 저장
- chosen: (a)
- why: 계약이 "빠른 판에서 바꾼 값도 설정에 저장(같은 원본)"이라 한 VM · 한 모델을 두 화면이 같이 쓰는 것이 가장 단순하고, 두 저장 규칙을 섞으면 같은 값이 화면마다 다르게 저장된다. 앱에 이미 「초안 없는 블록」 선례(SensorModelsDraftBlock)가 있다.
- effect: 설정 창에서 바꿔도 [저장] 없이 저장된다(묶음 오른쪽 아래 「저장됨」). 저장 막대에 넣고 싶으면 앱 쪽 DraftBlock 이 Map3DEnvironmentViewModel 의 값을 저장본과 비교하고 VM 의 SaveDelay 를 크게(또는 save 대리자를 바꿔) 두면 된다.

## 2. 수동 날씨 · 자동 실패 시 보내는 바람 · 기온 값
- options: (a) 바람 0 m/s · 0°, 기온 null(모름) / (b) 바람 · 기온 모두 null / (c) 수동 칸에 바람 · 기온 입력란 추가
- chosen: (a)
- why: 계약 수동 칸에 바람 · 기온이 없다(요청 밖 칸 추가 금지). 비 · 눈 기울기는 숫자가 있어야 엔진이 바로 쓸 수 있어 "바람 없음"을 0 으로, 기온은 그림에 쓰이지 않으니 모름(null).
- effect: env.set weather.wind_mps=0, wind_from_deg=0, temp_c=null, source="manual". Map3DEnvironment.Manual 한 곳만 바꾸면 된다.

## 3. 빨리 감기(cycle)의 시작 시각
- options: (a) 오늘의 「고른 시각」(ManualTime)에서 시작 / (b) 지금 시각에서 시작
- chosen: (a)
- why: 해 뜨는 · 지는 모습을 보려고 빨리 감기를 쓰므로 시작점을 고를 수 있어야 한다. 계약 iso 칸에 한 시각만 있어 manual 과 같은 규칙을 쓴다.
- effect: cycle 이면 iso = 오늘 날짜 + ManualTime. Map3DEnvironment.TimeOf 한 줄.

## 4. 자동 날씨를 처음 받는 때
- options: (a) 3D 가 장면을 처음 그린(rendered) 뒤부터 / (b) 자동 모드로 바꾸는 즉시(3D 를 안 열어도)
- chosen: (a) — 단 [연결 시험] 은 언제든 한 번 부른다
- why: 3D 를 쓰지 않는 동안 바깥 호출을 하지 않는다(폐쇄망 · 키 사용량 보호). 계약도 "3D 를 열 때 한 번 · 새 날씨를 받을 때마다 보낸다".
- effect: 3D 를 안 열었으면 상태 글 "기상청 날씨는 3D 를 열면 받습니다". Map3DEnvironmentViewModel.RestartWeather 의 _sceneSeen 조건.

## 5. 기상청 강수량(RN1) → 세기 값
- options: 구간 값(약 0.3 · 보통 0.6 · 강 0.9, 약한 강수형태 5·6·7 은 0.2) / 연속 함수
- chosen: 구간 값
- why: 계약이 "0.1~1 약 · 1~5 보통 · 5~ 강" 구간으로 적었다. 연속값은 근거가 없다.
- effect: KmaWeatherProvider.IntensityOf · ToSnapshot. 숫자만 바꾸면 된다.

## 6. 설정 화면이 앱(Dotnet.Monitoring.Solution)에 있음 — 라이브러리 범위
- options: (a) 라이브러리에 모델 · VM · 설정 묶음 뷰 · 빠른 판까지, 앱 배선은 사용자가 / (b) 앱도 고침
- chosen: (a) (사용자 범위 안내 그대로)
- why: 이 작업은 GIS 라이브러리만 맡는다. 붙일 자리는 최종 보고에 파일 · 줄로 적었다.
- effect: 앱에 붙이기 전에는 설정 창에 「3D 환경」 묶음이 안 보인다. 3D 위 「환경」 판은 라이브러리(MapView.xaml)에 있어 DI 등록(GMapUiModule)만으로 바로 보인다.

## 7. 인증키 입력 형식(포털의 인코딩 키 · 디코딩 키)
- options: (a) % 가 들었으면 그대로, 아니면 인코딩 / (b) 하나만 받는다고 안내
- chosen: (a)
- why: 공공데이터포털은 두 형식을 함께 주고, 사용자가 어느 쪽을 붙여 넣을지 모른다 — 두 번 인코딩하면 인증 실패.
- effect: KmaWeatherProvider.GetItemsAsync 한 줄.

## 8. `&sky=1` 을 붙이는 조건
- options: (a) 3D 환경 VM 이 있으면(= env.set 을 보내면) 늘 / (b) 날씨가 구름 · 비일 때만 / (c) 설정 칸으로 켜고 끄기
- chosen: (a)
- why: 지시가 "날씨 모드가 무엇이든 env 를 보내는 한". 맑음이어도 하늘색 · 해 뜸/짐 빛이 보여야 한다. 칸 추가는 요청 밖.
- effect: Map3DMath.ViewUrl(sky: true) — 주소 끝에 `&sky=1`(기존 키 그대로). MapViewModel.Map3D 의 LoadMap3DScene `sky: Map3DEnvironment != null` 한 곳.

## 9. env.state 의 장면 시각(time) 표시
- options: (a) 판의 해 줄 앞에 "3D 시각 14:30" 으로 / (b) 받기만 하고 안 보임
- chosen: (a)
- why: 빨리 감기 중 지금 몇 시인지 판에서 알 수 있어야 한다. 엔진이 새로 보내는 칸이라 버리면 쓸 데가 없다.
- effect: Map3DEnvState.Time · SunText. 극지방 sunrise/sunset null 은 그 쪽만 빠진다.
