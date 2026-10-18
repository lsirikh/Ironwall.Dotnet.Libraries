## 1. 빈 값 · 알 수 없는 값을 어떻게 읽을지
- options: (a) 빈 값 · 알 수 없는 값도 모두 extract (b) 옛 값 composite 와 "마지막 값 없음"(DefaultMode 의 빈 값)만 extract, 저장 값 정리(NormalizeMode · Map3DModel.Normalize)의 빈 값 · 알 수 없는 값은 지금처럼 tiles
- chosen: (b)
- why: 설정 키가 없으면 Map3DModel 기본값(extract)이 쓰이므로 목표는 이미 채워진다. NormalizeMode 는 요청 본문(BuildBody)에도 쓰여, 모르는 값을 가장 안전한 tiles(옛 서버와 맞는 bbox 만 본문)로 두는 기존 규칙과 시험(null → tiles)을 그대로 둔다.
- effect: appsettings 에 OverlayMode 가 "" · 엉뚱한 값으로 저장돼 있으면 tiles 로 읽힌다. 바꾸려면 Map3DBake.NormalizeMode · Map3DModel.Normalize 의 마지막 분기를 ModeExtract 로.

## 2. 메인 앱 시험의 가짜 창구 기본값
- options: (a) FakeMap3DPort._mode 를 "tiles" 그대로 두고 처음 선택 글만 바꾸기 (b) 라이브러리 새 기본값 extract 로 맞추기
- chosen: (b)
- why: 가짜 창구 주석이 "라이브러리 Map3DModel 기본값과 같은" 이라 기본값 변경을 따라가는 것이 뜻에 맞다. 그 결과 저장 시험은 Options[3](이제 tiles)를 골라 "tiles" 로 저장되는지 본다.
- effect: host.patch 의 Map3DSettingsSectionTests 두 곳. 되돌리려면 _mode 와 두 기대값만 고치면 된다.

## 3. 메인 앱 복사본을 둔 곳
- options: (a) 워크트리 밖 임시 폴더 (b) 워크트리 안 `.hostcopy`(추적 파일만 복사 + `Ironwall.Dotnet.Libraries` junction → 이 워크트리)
- chosen: (b), 끝나면 junction 은 `rmdir` 로 끊고 `.hostcopy` 는 지운다
- why: 워크트리 밖은 지울 수 없어 찌꺼기가 남는다. junction 으로 메인 앱이 바뀐 라이브러리(기본값 extract)로 빌드 · 시험된다.
- effect: 결과는 `.harness/t-20261018-151440-188e/host.patch`(메인 앱 v0.5 기준 `git apply` 용) 하나만 남는다.
