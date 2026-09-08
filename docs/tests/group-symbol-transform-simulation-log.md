# 그룹 심볼 변환(회전/확대축소) — 시뮬레이션 전량 로그 v6

- 생성: `gtsim` v6(스크래치패드, 제품 코드 무접촉, 결정론) · 카탈로그: [group-symbol-transform-scenarios.md](group-symbol-transform-scenarios.md)
- **v6 = 3D/틸트 도입 재검토판**(2026-09-08): SIM-TL(틸트 왜곡)·SIM-VF(뷰 프레임 변화) 계열 추가.
  적대 검증 반박으로 v5 의 ISSUE-42("제스처는 1/cos 역보정 필수") 를 **폐기**하고 ISSUE-44(역보정 추가 = 이중보정 버그)로 정정.
- 총 5,255 시뮬 · PASS 3,069 · ISSUE 2,166 · GAP 20

```
시뮬 SIM-R0001 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 3.748m (50.21px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0002 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 3.748m (50.21px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0003 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0004 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0005 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0006 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0007 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.005m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R0008 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.005m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R0009 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0010 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0011 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 2.263m (30.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0012 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 2.263m (30.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0013 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0014 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0015 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0016 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0017 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.005m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R0018 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.005m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R0019 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0020 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0021 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 3.451m (46.23px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0022 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 3.451m (46.23px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0023 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0024 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0025 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0026 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0027 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.027m (0.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R0028 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.027m (0.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R0029 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0030 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0031 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 3.192m (42.76px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0032 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 3.192m (42.76px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0033 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0034 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0035 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0036 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0037 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.080m (1.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R0038 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.080m (1.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R0039 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0040 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0041 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 4.884m (65.43px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0042 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 4.884m (65.43px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0043 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0044 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0045 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0046 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0047 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.221m (2.96px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0048 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.221m (2.96px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0049 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0050 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0051 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 3.515m (47.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0052 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 3.515m (47.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0053 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0054 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0055 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0056 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0057 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.312m (4.18px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0058 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.312m (4.18px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0059 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0060 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0061 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 4.022m (53.88px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0062 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 4.022m (53.88px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0063 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0064 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0065 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0066 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0067 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0068 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0069 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0070 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0071 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 4.022m (53.88px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0072 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 4.022m (53.88px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0073 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0074 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0075 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0076 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0077 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0078 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0079 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0080 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0081 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 4.106m (55.00px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0082 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 4.106m (55.00px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0083 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0084 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0085 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0086 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0087 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.312m (4.18px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0088 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.312m (4.18px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0089 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0090 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0091 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 2.263m (30.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0092 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 2.263m (30.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0093 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0094 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0095 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0096 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0097 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.005m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R0098 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.005m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R0099 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0100 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0101 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 3.005m (40.25px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0102 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 3.005m (40.25px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0103 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0104 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0105 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0106 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0107 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0108 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0109 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0110 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0111 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 3.005m (40.25px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0112 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 3.005m (40.25px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0113 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0114 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0115 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0116 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0117 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0118 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0119 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0120 | 회전 | init=적도(0N) 범위50m z15 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0121 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 1.130m (15.14px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0122 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 1.130m (15.14px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0123 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0124 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0125 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0126 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0127 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.005m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R0128 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.005m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R0129 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0130 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0131 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 1.127m (15.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0132 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 1.127m (15.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0133 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0134 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0135 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0136 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0137 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.005m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R0138 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.005m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R0139 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0140 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0141 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 1.117m (14.97px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0142 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 1.117m (14.97px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0143 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0144 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0145 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0146 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0147 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.027m (0.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R0148 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.027m (0.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R0149 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0150 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0151 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 0.889m (11.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0152 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.889m (11.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0153 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0154 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0155 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0156 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0157 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.080m (1.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R0158 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.080m (1.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R0159 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0160 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0161 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 1.014m (13.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0162 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 1.014m (13.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0163 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0164 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0165 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0166 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0167 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.221m (2.96px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0168 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.221m (2.96px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0169 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0170 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0171 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 1.181m (15.83px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0172 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 1.181m (15.83px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0173 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0174 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0175 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0176 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0177 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.312m (4.18px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0178 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.312m (4.18px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0179 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0180 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0181 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.858m (11.49px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0182 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.858m (11.49px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0183 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0184 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0185 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0186 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0187 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0188 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0189 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0190 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0191 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 0.857m (11.49px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0192 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.857m (11.49px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0193 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0194 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0195 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0196 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0197 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0198 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0199 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0200 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0201 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 1.186m (15.89px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0202 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 1.186m (15.89px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0203 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0204 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0205 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0206 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0207 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.312m (4.18px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0208 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.312m (4.18px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0209 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0210 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0211 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 1.127m (15.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0212 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 1.127m (15.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0213 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0214 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0215 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0216 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0217 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.005m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R0218 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.005m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R0219 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0220 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0221 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 0.674m (9.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0222 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.674m (9.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0223 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0224 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0225 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0226 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0227 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0228 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0229 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0230 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0231 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 0.674m (9.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0232 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.674m (9.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0233 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0234 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0235 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0236 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0237 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0238 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0239 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0240 | 회전 | init=적도(0N) 범위50m z17 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0241 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 0.232m (3.11px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0242 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.232m (3.11px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0243 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0244 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0245 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0246 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0247 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.005m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R0248 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.005m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R0249 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0250 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0251 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.204m (2.73px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0252 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.204m (2.73px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0253 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0254 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0255 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0256 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0257 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.005m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R0258 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.005m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R0259 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0260 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0261 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 0.201m (2.69px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0262 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.201m (2.69px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0263 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0264 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0265 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0266 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0267 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.027m (0.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R0268 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.027m (0.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R0269 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0270 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0271 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 0.185m (2.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0272 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.185m (2.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0273 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0274 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0275 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0276 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0277 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.080m (1.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R0278 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.080m (1.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R0279 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0280 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0281 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 0.199m (2.66px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0282 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.199m (2.66px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0283 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0284 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0285 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0286 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0287 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.221m (2.96px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0288 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.221m (2.96px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0289 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0290 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0291 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 0.237m (3.17px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0292 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.237m (3.17px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0293 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0294 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0295 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0296 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0297 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.312m (4.18px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0298 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.312m (4.18px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0299 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0300 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0301 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.193m (2.58px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0302 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.193m (2.58px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0303 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0304 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0305 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0306 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0307 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0308 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0309 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0310 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0311 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 0.193m (2.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0312 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.193m (2.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0313 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0314 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0315 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0316 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0317 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0318 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0319 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0320 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0321 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 0.237m (3.18px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0322 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.237m (3.18px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0323 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0324 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0325 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0326 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0327 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.312m (4.18px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0328 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.312m (4.18px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0329 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0330 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0331 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 0.204m (2.73px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0332 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.204m (2.73px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0333 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0334 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0335 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0336 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0337 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.005m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R0338 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.005m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R0339 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0340 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0341 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 0.134m (1.79px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0342 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.134m (1.79px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0343 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0344 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0345 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0346 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0347 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0348 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0349 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0350 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0351 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 0.134m (1.79px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0352 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.134m (1.79px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0353 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0354 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0355 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0356 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0357 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0358 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0359 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0360 | 회전 | init=적도(0N) 범위50m z19 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0361 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 3.109m (41.65px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0362 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 3.109m (41.65px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0363 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0364 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0365 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0366 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0367 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0368 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0369 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0370 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0371 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 3.744m (50.16px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0372 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 3.744m (50.16px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0373 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0374 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0375 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0376 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0377 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0378 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0379 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0380 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0381 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 4.826m (64.65px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0382 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 4.826m (64.65px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0383 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0384 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0385 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0386 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0387 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.271m (3.63px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0388 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.271m (3.63px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0389 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0390 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0391 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 4.138m (55.43px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0392 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 4.138m (55.43px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0393 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0394 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0395 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0396 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0397 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.807m (10.81px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0398 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.807m (10.81px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0399 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0400 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0401 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 4.197m (56.22px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0402 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 4.197m (56.22px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0403 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0404 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0405 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0406 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0407 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 2.205m (29.54px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0408 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 2.205m (29.54px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0409 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0410 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0411 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 5.550m (74.35px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0412 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 5.550m (74.35px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0413 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0414 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0415 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0416 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0417 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 3.118m (41.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0418 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 3.118m (41.77px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0419 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0420 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0421 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 2.270m (30.41px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0422 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 2.270m (30.41px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0423 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0424 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0425 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0426 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0427 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0428 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0429 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0430 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0431 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 2.269m (30.39px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0432 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 2.269m (30.39px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0433 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0434 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0435 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0436 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0437 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0438 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0439 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0440 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0441 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 5.652m (75.72px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0442 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 5.652m (75.72px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0443 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0444 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0445 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0446 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0447 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 3.118m (41.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0448 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 3.118m (41.77px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0449 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0450 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0451 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 3.744m (50.16px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0452 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 3.744m (50.16px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0453 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0454 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0455 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0456 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0457 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0458 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0459 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0460 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0461 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 2.425m (32.48px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0462 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 2.425m (32.48px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0463 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0464 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0465 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0466 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0467 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0468 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0469 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0470 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0471 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 2.425m (32.48px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0472 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 2.425m (32.48px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0473 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0474 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0475 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0476 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0477 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0478 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0479 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0480 | 회전 | init=적도(0N) 범위500m z15 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0481 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 1.192m (15.97px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0482 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 1.192m (15.97px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0483 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0484 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0485 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0486 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0487 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0488 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0489 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0490 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0491 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 1.136m (15.22px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0492 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 1.136m (15.22px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0493 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0494 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0495 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0496 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0497 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0498 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0499 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0500 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0501 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 1.316m (17.63px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0502 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 1.316m (17.63px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0503 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0504 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0505 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0506 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0507 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.271m (3.63px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0508 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.271m (3.63px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0509 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0510 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0511 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 0.992m (13.28px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0512 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.992m (13.28px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0513 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0514 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0515 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0516 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0517 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.807m (10.81px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0518 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.807m (10.81px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0519 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0520 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0521 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 1.046m (14.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0522 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 1.046m (14.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0523 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0524 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0525 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0526 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0527 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 2.205m (29.54px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0528 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 2.205m (29.54px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0529 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0530 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0531 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 0.786m (10.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0532 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.786m (10.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0533 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0534 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0535 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0536 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0537 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 3.118m (41.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0538 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 3.118m (41.77px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0539 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0540 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0541 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.727m (9.73px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0542 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.727m (9.73px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0543 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0544 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0545 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0546 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0547 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0548 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0549 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0550 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0551 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 0.727m (9.74px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0552 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.727m (9.74px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0553 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0554 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0555 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0556 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0557 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0558 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0559 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0560 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0561 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 0.647m (8.67px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0562 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.647m (8.67px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0563 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0564 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0565 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0566 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0567 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 3.118m (41.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0568 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 3.118m (41.77px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0569 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0570 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0571 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 1.136m (15.22px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0572 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 1.136m (15.22px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0573 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0574 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0575 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0576 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0577 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0578 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0579 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0580 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0581 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 0.776m (10.40px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0582 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.776m (10.40px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0583 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0584 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0585 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0586 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0587 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0588 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0589 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0590 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0591 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 0.776m (10.40px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0592 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.776m (10.40px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0593 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0594 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0595 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0596 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0597 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0598 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0599 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0600 | 회전 | init=적도(0N) 범위500m z17 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0601 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.287m (3.85px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0602 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.287m (3.85px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0603 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0604 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0605 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0606 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0607 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0608 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0609 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0610 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0611 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.272m (3.64px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0612 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.272m (3.64px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0613 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0614 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0615 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0616 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0617 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0618 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0619 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0620 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0621 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.246m (3.30px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0622 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.246m (3.30px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0623 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0624 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0625 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0626 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0627 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.271m (3.63px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0628 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.271m (3.63px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0629 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0630 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0631 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 0.271m (3.63px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0632 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.271m (3.63px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0633 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0634 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0635 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0636 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0637 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 0.807m (10.81px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0638 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.807m (10.81px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0639 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0640 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0641 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 0.305m (4.08px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0642 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.305m (4.08px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0643 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0644 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0645 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0646 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0647 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 2.205m (29.54px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0648 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 2.205m (29.54px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0649 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0650 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0651 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 0.232m (3.10px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0652 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.232m (3.10px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0653 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0654 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0655 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0656 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0657 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 3.118m (41.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0658 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 3.118m (41.77px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0659 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0660 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0661 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.256m (3.43px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0662 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.256m (3.43px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0663 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0664 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0665 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0666 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0667 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0668 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0669 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0670 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0671 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 0.263m (3.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0672 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.263m (3.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0673 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0674 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0675 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0676 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0677 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0678 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0679 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0680 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0681 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 0.276m (3.70px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0682 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.276m (3.70px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0683 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0684 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0685 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0686 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0687 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 3.118m (41.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0688 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 3.118m (41.77px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0689 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0690 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0691 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.272m (3.64px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0692 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.272m (3.64px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0693 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0694 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0695 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0696 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0697 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0698 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.055m (0.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R0699 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0700 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0701 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.167m (2.23px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0702 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.167m (2.23px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0703 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0704 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0705 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0706 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0707 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0708 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0709 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0710 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0711 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.167m (2.23px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0712 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.167m (2.23px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0713 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0714 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0715 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0716 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0717 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0718 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0719 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0720 | 회전 | init=적도(0N) 범위500m z19 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0721 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 2.562m (34.32px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0722 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 2.562m (34.32px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0723 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0724 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0725 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0726 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0727 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0728 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0729 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0730 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0731 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 4.294m (57.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0732 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 4.294m (57.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0733 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0734 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0735 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0736 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0737 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0738 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0739 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0740 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0741 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 4.046m (54.21px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0742 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 4.046m (54.21px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0743 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0744 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0745 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0746 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0747 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 2.718m (36.41px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0748 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 2.718m (36.41px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0749 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0750 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0751 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 2.900m (38.86px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0752 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 2.900m (38.86px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0753 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0754 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0755 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0756 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0757 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 8.070m (108.11px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0758 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 8.070m (108.11px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0759 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0760 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0761 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 3.120m (41.80px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0762 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 3.120m (41.80px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0763 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0764 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0765 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0766 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0767 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 22.049m (295.38px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0768 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 22.049m (295.38px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0769 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0770 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0771 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 2.076m (27.81px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0772 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 2.076m (27.82px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0773 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0774 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0775 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0776 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0777 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 31.183m (417.74px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0778 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 31.182m (417.74px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0779 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0780 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0781 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 2.209m (29.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0782 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 2.209m (29.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0783 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0784 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0785 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0786 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0787 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0788 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0789 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0790 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0791 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 2.240m (30.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0792 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 2.241m (30.02px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0793 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0794 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0795 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0796 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0797 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0798 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0799 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0800 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0801 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 2.525m (33.82px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0802 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 2.525m (33.82px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0803 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0804 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0805 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0806 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0807 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 31.183m (417.74px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0808 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 31.182m (417.74px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0809 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0810 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0811 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 4.294m (57.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0812 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 4.294m (57.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0813 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0814 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0815 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0816 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0817 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0818 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0819 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0820 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0821 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 1.794m (24.04px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0822 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 1.794m (24.04px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0823 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0824 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0825 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0826 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0827 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0828 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0829 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0830 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0831 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 1.794m (24.04px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0832 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 1.794m (24.04px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0833 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0834 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0835 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0836 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0837 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0838 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0839 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0840 | 회전 | init=적도(0N) 범위5000m z15 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0841 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 1.098m (14.71px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0842 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 1.098m (14.71px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0843 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0844 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0845 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0846 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0847 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0848 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0849 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0850 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0851 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 1.047m (14.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0852 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 1.047m (14.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0853 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0854 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0855 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0856 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0857 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0858 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0859 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0860 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0861 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 1.147m (15.37px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0862 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 1.147m (15.37px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0863 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0864 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0865 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0866 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0867 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 2.718m (36.41px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0868 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 2.718m (36.41px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0869 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0870 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0871 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 1.178m (15.78px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0872 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 1.178m (15.78px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0873 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0874 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0875 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0876 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0877 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 8.070m (108.11px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0878 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 8.070m (108.11px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0879 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0880 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0881 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 1.028m (13.77px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0882 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 1.028m (13.77px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0883 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0884 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0885 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0886 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0887 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 22.049m (295.38px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0888 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 22.049m (295.38px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0889 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0890 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0891 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 0.945m (12.66px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0892 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.945m (12.66px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0893 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0894 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0895 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0896 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0897 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 31.183m (417.74px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0898 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 31.182m (417.74px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0899 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0900 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0901 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 1.127m (15.10px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0902 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 1.127m (15.10px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0903 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0904 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0905 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0906 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0907 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0908 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0909 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0910 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0911 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 1.106m (14.82px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0912 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 1.106m (14.82px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0913 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0914 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0915 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0916 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0917 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0918 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0919 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0920 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0921 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 1.089m (14.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0922 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 1.089m (14.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0923 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0924 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0925 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0926 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0927 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 31.183m (417.74px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0928 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 31.182m (417.74px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0929 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0930 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0931 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 1.047m (14.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0932 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 1.047m (14.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0933 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0934 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0935 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0936 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0937 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0938 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0939 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0940 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0941 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.740m (9.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0942 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.740m (9.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0943 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0944 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0945 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0946 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0947 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0948 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0949 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0950 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0951 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.740m (9.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0952 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.740m (9.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0953 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0954 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0955 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0956 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0957 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0958 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0959 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0960 | 회전 | init=적도(0N) 범위5000m z17 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0961 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.214m (2.86px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0962 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.214m (2.86px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0963 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0964 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0965 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0966 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0967 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0968 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0969 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0970 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0971 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.301m (4.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0972 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.301m (4.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0973 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0974 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0975 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0976 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0977 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0978 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0979 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0980 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0981 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.217m (2.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0982 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.217m (2.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0983 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0984 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0985 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0986 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0987 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 2.718m (36.41px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0988 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 2.718m (36.41px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0989 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0990 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0991 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 0.198m (2.65px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0992 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.198m (2.65px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R0993 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0994 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R0995 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0996 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R0997 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 8.070m (108.11px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R0998 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 8.070m (108.11px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R0999 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1000 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1001 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 0.139m (1.86px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1002 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.139m (1.86px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1003 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1004 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1005 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1006 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1007 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 22.049m (295.38px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1008 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 22.049m (295.38px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1009 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1010 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1011 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 0.214m (2.87px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1012 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.214m (2.87px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1013 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1014 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1015 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1016 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1017 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 31.183m (417.74px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1018 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 31.182m (417.74px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1019 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1020 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1021 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.290m (3.88px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1022 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.290m (3.88px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1023 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1024 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1025 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1026 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1027 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1028 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1029 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1030 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1031 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 0.258m (3.46px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1032 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.258m (3.46px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1033 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1034 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1035 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1036 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1037 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1038 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1039 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1040 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1041 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 0.245m (3.28px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1042 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.245m (3.29px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1043 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1044 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1045 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1046 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1047 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 31.183m (417.74px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1048 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 31.182m (417.74px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1049 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1050 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1051 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.301m (4.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1052 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.301m (4.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1053 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1054 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1055 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1056 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1057 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1058 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.544m (7.29px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1059 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1060 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1061 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.165m (2.21px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1062 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.165m (2.21px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1063 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1064 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1065 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1066 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1067 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1068 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1069 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1070 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1071 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.165m (2.21px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1072 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.165m (2.21px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1073 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1074 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1075 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1076 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1077 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1078 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1079 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1080 | 회전 | init=적도(0N) 범위5000m z19 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1081 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 2.701m (45.54px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1082 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 2.701m (45.54px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1083 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1084 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1085 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1086 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1087 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.003m (0.05px@z21) | want=<0.1m | PASS
시뮬 SIM-R1088 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.003m (0.05px@z21) | want=<0.1m | PASS
시뮬 SIM-R1089 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1090 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1091 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 2.704m (45.60px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1092 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 2.704m (45.60px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1093 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1094 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1095 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1096 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1097 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.004m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1098 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.004m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1099 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1100 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1101 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 4.028m (67.92px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1102 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 4.028m (67.92px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1103 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1104 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1105 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1106 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1107 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.017m (0.29px@z21) | want=<0.1m | PASS
시뮬 SIM-R1108 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.017m (0.29px@z21) | want=<0.1m | PASS
시뮬 SIM-R1109 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 0.971m (16.37px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1110 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 0.971m (16.37px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1111 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 4.109m (69.28px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1112 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 4.109m (69.28px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1113 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1114 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1115 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1116 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1117 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.051m (0.86px@z21) | want=<0.1m | PASS
시뮬 SIM-R1118 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.051m (0.86px@z21) | want=<0.1m | PASS
시뮬 SIM-R1119 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 2.883m (48.62px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1120 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 2.883m (48.62px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1121 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 2.830m (47.72px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1122 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 2.830m (47.72px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1123 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1124 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1125 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1126 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1127 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.140m (2.35px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1128 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.139m (2.35px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1129 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 7.877m (132.82px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1130 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 7.877m (132.82px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1131 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 3.186m (53.72px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1132 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 3.186m (53.72px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1133 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1134 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1135 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1136 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1137 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.197m (3.32px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1138 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.197m (3.33px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1139 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 11.140m (187.84px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1140 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 11.140m (187.84px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1141 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 3.006m (50.68px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1142 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 3.006m (50.69px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1143 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1144 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1145 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1146 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1147 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1148 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1149 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1150 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1151 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 3.005m (50.67px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1152 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 3.005m (50.68px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1153 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1154 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1155 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1156 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1157 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1158 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1159 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1160 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1161 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 4.172m (70.35px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1162 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 4.172m (70.35px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1163 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1164 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1165 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1166 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1167 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.197m (3.32px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1168 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.197m (3.33px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1169 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 11.140m (187.83px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1170 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 11.140m (187.84px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1171 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 2.704m (45.60px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1172 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 2.704m (45.60px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1173 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1174 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1175 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1176 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1177 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.004m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1178 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.004m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1179 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1180 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1181 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 2.392m (40.33px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1182 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 2.392m (40.33px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1183 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1184 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1185 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1186 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1187 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1188 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1189 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1190 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1191 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 2.392m (40.33px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1192 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 2.392m (40.33px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1193 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1194 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1195 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1196 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1197 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1198 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1199 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1200 | 회전 | init=안양(37.4N) 범위50m z15 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1201 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 0.890m (15.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1202 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.890m (15.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1203 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1204 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1205 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1206 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1207 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.003m (0.05px@z21) | want=<0.1m | PASS
시뮬 SIM-R1208 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.003m (0.05px@z21) | want=<0.1m | PASS
시뮬 SIM-R1209 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1210 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1211 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 1.086m (18.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1212 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 1.086m (18.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1213 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1214 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1215 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1216 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1217 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.004m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1218 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.004m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1219 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1220 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1221 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 0.893m (15.05px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1222 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.893m (15.05px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1223 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1224 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1225 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1226 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1227 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.017m (0.29px@z21) | want=<0.1m | PASS
시뮬 SIM-R1228 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.017m (0.29px@z21) | want=<0.1m | PASS
시뮬 SIM-R1229 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 0.971m (16.37px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1230 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 0.971m (16.37px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1231 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 0.638m (10.75px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1232 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.638m (10.75px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1233 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1234 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1235 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1236 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1237 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.051m (0.86px@z21) | want=<0.1m | PASS
시뮬 SIM-R1238 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.051m (0.86px@z21) | want=<0.1m | PASS
시뮬 SIM-R1239 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 2.883m (48.62px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1240 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 2.883m (48.62px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1241 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 0.933m (15.74px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1242 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.934m (15.74px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1243 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1244 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1245 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1246 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1247 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.140m (2.35px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1248 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.139m (2.35px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1249 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 7.877m (132.82px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1250 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 7.877m (132.82px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1251 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 0.879m (14.81px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1252 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.879m (14.81px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1253 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1254 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1255 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1256 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1257 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.197m (3.32px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1258 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.197m (3.33px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1259 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 11.140m (187.84px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1260 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 11.140m (187.84px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1261 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 1.073m (18.10px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1262 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 1.073m (18.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1263 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1264 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1265 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1266 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1267 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1268 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1269 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1270 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1271 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 1.073m (18.10px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1272 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 1.073m (18.10px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1273 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1274 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1275 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1276 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1277 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1278 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1279 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1280 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1281 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 0.838m (14.13px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1282 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.838m (14.13px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1283 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1284 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1285 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1286 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1287 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.197m (3.32px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1288 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.197m (3.33px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1289 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 11.140m (187.83px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1290 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 11.140m (187.84px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1291 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 1.086m (18.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1292 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 1.086m (18.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1293 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1294 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1295 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1296 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1297 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.004m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1298 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.004m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1299 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1300 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1301 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 0.604m (10.18px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1302 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.604m (10.18px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1303 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1304 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1305 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1306 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1307 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1308 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1309 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1310 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1311 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 0.604m (10.18px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1312 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.604m (10.18px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1313 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1314 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1315 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1316 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1317 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1318 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1319 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1320 | 회전 | init=안양(37.4N) 범위50m z17 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1321 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 0.206m (3.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1322 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.206m (3.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1323 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1324 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1325 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1326 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1327 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.003m (0.05px@z21) | want=<0.1m | PASS
시뮬 SIM-R1328 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.003m (0.05px@z21) | want=<0.1m | PASS
시뮬 SIM-R1329 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1330 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1331 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.266m (4.48px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1332 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.266m (4.48px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1333 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1334 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1335 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1336 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1337 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.004m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1338 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.004m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1339 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1340 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1341 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 0.233m (3.92px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1342 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.233m (3.92px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1343 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1344 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1345 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1346 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1347 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.017m (0.29px@z21) | want=<0.1m | PASS
시뮬 SIM-R1348 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.017m (0.29px@z21) | want=<0.1m | PASS
시뮬 SIM-R1349 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 0.971m (16.37px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1350 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 0.971m (16.37px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1351 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 0.228m (3.85px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1352 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.228m (3.85px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1353 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1354 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1355 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1356 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1357 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.051m (0.86px@z21) | want=<0.1m | PASS
시뮬 SIM-R1358 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.051m (0.86px@z21) | want=<0.1m | PASS
시뮬 SIM-R1359 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 2.883m (48.62px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1360 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 2.883m (48.62px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1361 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 0.229m (3.86px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1362 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.229m (3.86px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1363 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1364 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1365 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1366 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1367 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.140m (2.35px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1368 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.139m (2.35px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1369 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 7.877m (132.82px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1370 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 7.877m (132.82px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1371 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 0.213m (3.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1372 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.213m (3.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1373 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1374 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1375 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1376 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1377 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.197m (3.32px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1378 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.197m (3.33px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1379 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 11.140m (187.84px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1380 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 11.140m (187.84px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1381 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.271m (4.56px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1382 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.271m (4.57px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1383 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1384 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1385 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1386 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1387 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1388 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1389 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1390 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1391 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 0.270m (4.56px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1392 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.270m (4.56px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1393 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1394 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1395 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1396 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1397 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1398 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1399 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1400 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1401 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 0.226m (3.81px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1402 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.226m (3.81px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1403 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1404 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1405 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1406 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1407 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.197m (3.32px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1408 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.197m (3.33px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1409 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 11.140m (187.83px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1410 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 11.140m (187.84px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1411 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 0.266m (4.48px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1412 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.266m (4.48px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1413 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1414 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1415 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1416 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1417 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.004m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1418 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.004m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1419 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1420 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.194m (3.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1421 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 0.153m (2.57px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1422 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.153m (2.57px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1423 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1424 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1425 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1426 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1427 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1428 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1429 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1430 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1431 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 0.153m (2.57px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1432 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.153m (2.57px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1433 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1434 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1435 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1436 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1437 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1438 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1439 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1440 | 회전 | init=안양(37.4N) 범위50m z19 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1441 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 3.260m (54.97px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1442 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 3.260m (54.97px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1443 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1444 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1445 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1446 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1447 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.034m (0.58px@z21) | want=<0.1m | PASS
시뮬 SIM-R1448 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.034m (0.58px@z21) | want=<0.1m | PASS
시뮬 SIM-R1449 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1450 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1451 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 3.549m (59.85px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1452 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 3.549m (59.85px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1453 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1454 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1455 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1456 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1457 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.035m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R1458 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.035m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R1459 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1460 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1461 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 3.288m (55.44px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1462 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 3.288m (55.44px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1463 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R1464 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1465 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.002m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R1466 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1467 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.172m (2.90px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1468 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.171m (2.88px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1469 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 9.709m (163.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1470 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 9.708m (163.69px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1471 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 3.084m (51.99px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1472 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 3.082m (51.97px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1473 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.003m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1474 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1475 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.004m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R1476 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1477 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.510m (8.60px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1478 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.506m (8.54px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1479 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 28.833m (486.16px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1480 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 28.829m (486.10px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1481 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 2.250m (37.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1482 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 2.253m (37.99px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1483 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.010m (0.17px@z21) | want=<0.1m | PASS
시뮬 SIM-R1484 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1485 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.010m (0.16px@z21) | want=<0.1m | PASS
시뮬 SIM-R1486 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1487 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 1.396m (23.53px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1488 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 1.389m (23.42px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1489 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 78.773m (1328.21px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1490 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 78.767m (1328.11px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1491 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 4.125m (69.56px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1492 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 4.118m (69.44px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1493 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.018m (0.31px@z21) | want=<0.1m | PASS
시뮬 SIM-R1494 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1495 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.019m (0.31px@z21) | want=<0.1m | PASS
시뮬 SIM-R1496 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1497 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 1.967m (33.16px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1498 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 1.974m (33.28px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1499 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 111.394m (1878.24px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1500 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 111.402m (1878.39px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1501 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 2.292m (38.65px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1502 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 2.295m (38.69px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1503 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.026m (0.43px@z21) | want=<0.1m | PASS
시뮬 SIM-R1504 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1505 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.026m (0.43px@z21) | want=<0.1m | PASS
시뮬 SIM-R1506 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1507 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.029m (0.49px@z21) | want=<0.1m | PASS
시뮬 SIM-R1508 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.011m (0.18px@z21) | want=<0.1m | PASS
시뮬 SIM-R1509 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.030m (0.50px@z21) | want=<0.1m | PASS
시뮬 SIM-R1510 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.012m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R1511 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 2.295m (38.69px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1512 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 2.297m (38.73px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1513 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.026m (0.43px@z21) | want=<0.1m | PASS
시뮬 SIM-R1514 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1515 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.026m (0.44px@z21) | want=<0.1m | PASS
시뮬 SIM-R1516 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1517 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.030m (0.50px@z21) | want=<0.1m | PASS
시뮬 SIM-R1518 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.010m (0.18px@z21) | want=<0.1m | PASS
시뮬 SIM-R1519 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.030m (0.50px@z21) | want=<0.1m | PASS
시뮬 SIM-R1520 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.010m (0.18px@z21) | want=<0.1m | PASS
시뮬 SIM-R1521 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 3.253m (54.84px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1522 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 3.245m (54.72px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1523 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.018m (0.31px@z21) | want=<0.1m | PASS
시뮬 SIM-R1524 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1525 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.018m (0.30px@z21) | want=<0.1m | PASS
시뮬 SIM-R1526 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1527 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 1.944m (32.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1528 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 1.961m (33.06px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1529 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 111.376m (1877.94px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1530 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 111.392m (1878.21px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1531 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 3.549m (59.85px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1532 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 3.549m (59.85px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1533 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1534 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1535 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1536 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1537 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.035m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R1538 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.035m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R1539 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1540 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1541 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 1.981m (33.41px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1542 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 1.981m (33.41px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1543 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1544 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1545 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1546 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1547 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1548 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1549 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1550 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1551 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 1.981m (33.41px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1552 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 1.981m (33.41px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1553 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1554 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1555 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1556 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1557 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1558 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1559 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1560 | 회전 | init=안양(37.4N) 범위500m z15 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1561 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 0.955m (16.10px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1562 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.955m (16.10px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1563 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1564 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1565 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1566 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1567 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.034m (0.58px@z21) | want=<0.1m | PASS
시뮬 SIM-R1568 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.034m (0.58px@z21) | want=<0.1m | PASS
시뮬 SIM-R1569 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1570 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1571 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.785m (13.24px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1572 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.785m (13.23px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1573 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1574 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1575 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1576 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1577 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.035m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R1578 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.035m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R1579 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1580 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1581 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 0.916m (15.45px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1582 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.917m (15.45px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1583 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R1584 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1585 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.002m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R1586 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1587 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.172m (2.90px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1588 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.171m (2.88px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1589 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 9.709m (163.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1590 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 9.708m (163.69px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1591 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 1.010m (17.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1592 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 1.012m (17.06px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1593 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.003m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1594 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1595 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.004m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R1596 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1597 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.510m (8.60px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1598 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.506m (8.54px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1599 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 28.833m (486.16px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1600 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 28.829m (486.10px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1601 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 0.805m (13.57px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1602 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.810m (13.66px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1603 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.010m (0.17px@z21) | want=<0.1m | PASS
시뮬 SIM-R1604 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1605 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.010m (0.16px@z21) | want=<0.1m | PASS
시뮬 SIM-R1606 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1607 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 1.396m (23.53px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1608 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 1.389m (23.42px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1609 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 78.773m (1328.21px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1610 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 78.767m (1328.11px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1611 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 0.915m (15.43px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1612 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.908m (15.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1613 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.018m (0.31px@z21) | want=<0.1m | PASS
시뮬 SIM-R1614 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1615 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.019m (0.31px@z21) | want=<0.1m | PASS
시뮬 SIM-R1616 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1617 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 1.967m (33.16px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1618 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 1.974m (33.28px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1619 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 111.394m (1878.24px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1620 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 111.402m (1878.39px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1621 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.939m (15.84px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1622 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.933m (15.73px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1623 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.026m (0.43px@z21) | want=<0.1m | PASS
시뮬 SIM-R1624 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1625 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.026m (0.43px@z21) | want=<0.1m | PASS
시뮬 SIM-R1626 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1627 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.029m (0.49px@z21) | want=<0.1m | PASS
시뮬 SIM-R1628 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.011m (0.18px@z21) | want=<0.1m | PASS
시뮬 SIM-R1629 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.030m (0.50px@z21) | want=<0.1m | PASS
시뮬 SIM-R1630 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.012m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R1631 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 0.941m (15.87px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1632 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.935m (15.76px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1633 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.026m (0.43px@z21) | want=<0.1m | PASS
시뮬 SIM-R1634 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1635 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.026m (0.44px@z21) | want=<0.1m | PASS
시뮬 SIM-R1636 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1637 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.030m (0.50px@z21) | want=<0.1m | PASS
시뮬 SIM-R1638 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.010m (0.18px@z21) | want=<0.1m | PASS
시뮬 SIM-R1639 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.030m (0.50px@z21) | want=<0.1m | PASS
시뮬 SIM-R1640 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.010m (0.18px@z21) | want=<0.1m | PASS
시뮬 SIM-R1641 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 0.817m (13.77px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1642 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.807m (13.61px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1643 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.018m (0.31px@z21) | want=<0.1m | PASS
시뮬 SIM-R1644 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1645 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.018m (0.30px@z21) | want=<0.1m | PASS
시뮬 SIM-R1646 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1647 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 1.944m (32.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1648 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 1.961m (33.06px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1649 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 111.376m (1877.94px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1650 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 111.392m (1878.21px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1651 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 0.785m (13.24px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1652 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.785m (13.23px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1653 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1654 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1655 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1656 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1657 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.035m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R1658 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.035m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R1659 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1660 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1661 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 0.647m (10.92px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1662 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.647m (10.92px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1663 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1664 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1665 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1666 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1667 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1668 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1669 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1670 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1671 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 0.647m (10.92px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1672 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.647m (10.92px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1673 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1674 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1675 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1676 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1677 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1678 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1679 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1680 | 회전 | init=안양(37.4N) 범위500m z17 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1681 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.227m (3.83px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1682 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.227m (3.83px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1683 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1684 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1685 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1686 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1687 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.034m (0.58px@z21) | want=<0.1m | PASS
시뮬 SIM-R1688 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.034m (0.58px@z21) | want=<0.1m | PASS
시뮬 SIM-R1689 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1690 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1691 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.194m (3.27px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1692 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.194m (3.27px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1693 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1694 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1695 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1696 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1697 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.035m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R1698 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.035m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R1699 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1700 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1701 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.195m (3.30px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1702 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.195m (3.29px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1703 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R1704 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1705 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.002m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R1706 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1707 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.172m (2.90px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1708 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.171m (2.88px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1709 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 9.709m (163.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1710 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 9.708m (163.69px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1711 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 0.208m (3.51px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1712 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.209m (3.52px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1713 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.003m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R1714 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1715 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.004m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R1716 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1717 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 0.510m (8.60px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1718 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.506m (8.54px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1719 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 28.833m (486.16px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1720 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 28.829m (486.10px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1721 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 0.245m (4.14px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1722 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.243m (4.11px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1723 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.010m (0.17px@z21) | want=<0.1m | PASS
시뮬 SIM-R1724 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1725 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.010m (0.16px@z21) | want=<0.1m | PASS
시뮬 SIM-R1726 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1727 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 1.396m (23.53px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1728 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 1.389m (23.42px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1729 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 78.773m (1328.21px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1730 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 78.767m (1328.11px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1731 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 0.241m (4.07px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1732 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.238m (4.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1733 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.018m (0.31px@z21) | want=<0.1m | PASS
시뮬 SIM-R1734 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1735 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 0.019m (0.31px@z21) | want=<0.1m | PASS
시뮬 SIM-R1736 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1737 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 1.967m (33.16px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1738 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 1.974m (33.28px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1739 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 111.394m (1878.24px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1740 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 111.402m (1878.39px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1741 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.227m (3.83px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1742 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.219m (3.68px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1743 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.026m (0.43px@z21) | want=<0.1m | PASS
시뮬 SIM-R1744 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1745 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.026m (0.43px@z21) | want=<0.1m | PASS
시뮬 SIM-R1746 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1747 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.029m (0.49px@z21) | want=<0.1m | PASS
시뮬 SIM-R1748 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.011m (0.18px@z21) | want=<0.1m | PASS
시뮬 SIM-R1749 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.030m (0.50px@z21) | want=<0.1m | PASS
시뮬 SIM-R1750 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.012m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R1751 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 0.222m (3.75px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1752 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.214m (3.61px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1753 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.026m (0.43px@z21) | want=<0.1m | PASS
시뮬 SIM-R1754 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1755 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 0.026m (0.44px@z21) | want=<0.1m | PASS
시뮬 SIM-R1756 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1757 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 0.030m (0.50px@z21) | want=<0.1m | PASS
시뮬 SIM-R1758 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.010m (0.18px@z21) | want=<0.1m | PASS
시뮬 SIM-R1759 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 0.030m (0.50px@z21) | want=<0.1m | PASS
시뮬 SIM-R1760 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.010m (0.18px@z21) | want=<0.1m | PASS
시뮬 SIM-R1761 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 0.179m (3.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1762 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.180m (3.04px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1763 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.018m (0.31px@z21) | want=<0.1m | PASS
시뮬 SIM-R1764 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1765 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 0.018m (0.30px@z21) | want=<0.1m | PASS
시뮬 SIM-R1766 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1767 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 1.944m (32.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1768 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 1.961m (33.06px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1769 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 111.376m (1877.94px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1770 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 111.392m (1878.21px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1771 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.194m (3.27px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1772 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.194m (3.27px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1773 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1774 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1775 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1776 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1777 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.035m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R1778 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.035m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R1779 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1780 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 1.944m (32.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1781 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.139m (2.34px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1782 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.139m (2.34px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1783 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1784 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1785 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1786 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1787 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1788 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1789 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1790 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1791 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.139m (2.34px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1792 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.139m (2.34px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1793 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1794 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1795 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1796 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1797 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1798 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1799 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1800 | 회전 | init=안양(37.4N) 범위500m z19 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1801 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 3.494m (58.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1802 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 3.487m (58.79px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1803 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.022m (0.38px@z21) | want=<0.1m | PASS
시뮬 SIM-R1804 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1805 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.023m (0.39px@z21) | want=<0.1m | PASS
시뮬 SIM-R1806 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1807 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.343m (5.78px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1808 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.336m (5.67px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1809 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 19.439m (327.77px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1810 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 19.417m (327.40px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1811 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 3.041m (51.28px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1812 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 3.044m (51.33px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1813 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.022m (0.38px@z21) | want=<0.1m | PASS
시뮬 SIM-R1814 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1815 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.022m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R1816 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1817 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.342m (5.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1818 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.336m (5.66px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1819 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 19.439m (327.76px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1820 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 19.417m (327.40px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1821 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 3.699m (62.37px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1822 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 3.691m (62.24px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1823 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.112m (1.90px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1824 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1825 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.112m (1.89px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1826 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1827 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 1.718m (28.96px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1828 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 1.679m (28.31px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1829 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 97.082m (1636.93px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1830 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 96.971m (1635.06px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1831 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 3.060m (51.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1832 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 3.036m (51.20px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1833 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.336m (5.67px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1834 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1835 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.336m (5.67px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1836 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1837 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 5.145m (86.75px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1838 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 4.991m (84.15px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1839 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 288.324m (4861.52px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1840 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 287.990m (4855.88px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1841 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 3.528m (59.49px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1842 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 3.328m (56.11px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1843 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.986m (16.63px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1844 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1845 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.986m (16.63px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1846 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1847 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 14.188m (239.22px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1848 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 13.574m (228.88px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1849 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 787.726m (13282.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1850 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 787.100m (13271.53px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1851 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 2.558m (43.13px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1852 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 2.125m (35.83px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1853 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 1.823m (30.73px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1854 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1855 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 1.823m (30.74px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1856 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1857 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 20.406m (344.08px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1858 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 20.068m (338.38px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1859 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 1113.223m (18770.39px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1860 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 1114.084m (18784.91px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1861 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 3.381m (57.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1862 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 2.359m (39.77px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1863 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 2.577m (43.46px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1864 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1865 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 2.578m (43.46px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1866 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1867 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 2.972m (50.10px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1868 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 1.044m (17.60px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1869 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 2.972m (50.11px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1870 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 1.058m (17.83px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1871 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 3.351m (56.50px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1872 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 2.327m (39.24px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1873 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 2.577m (43.46px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1874 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1875 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 2.577m (43.46px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1876 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1877 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 2.971m (50.10px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1878 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 1.044m (17.60px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1879 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 2.971m (50.10px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1880 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 1.044m (17.60px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1881 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 2.464m (41.55px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1882 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 2.004m (33.79px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1883 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 1.823m (30.73px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1884 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1885 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 1.823m (30.74px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1886 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1887 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 18.172m (306.41px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1888 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 18.774m (316.55px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1889 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 1111.402m (18739.69px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1890 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 1113.008m (18766.76px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1891 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 3.041m (51.28px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1892 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 3.044m (51.33px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1893 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.022m (0.38px@z21) | want=<0.1m | PASS
시뮬 SIM-R1894 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1895 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.022m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R1896 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1897 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.342m (5.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1898 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.336m (5.66px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1899 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 19.439m (327.76px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1900 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 19.417m (327.40px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1901 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 1.668m (28.12px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1902 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 1.668m (28.12px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1903 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1904 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1905 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1906 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1907 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1908 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1909 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1910 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1911 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 1.668m (28.12px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1912 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 1.668m (28.12px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1913 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1914 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1915 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1916 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1917 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1918 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1919 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1920 | 회전 | init=안양(37.4N) 범위5000m z15 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1921 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.795m (13.41px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1922 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.802m (13.52px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1923 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.022m (0.38px@z21) | want=<0.1m | PASS
시뮬 SIM-R1924 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1925 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.023m (0.39px@z21) | want=<0.1m | PASS
시뮬 SIM-R1926 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1927 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.343m (5.78px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1928 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.336m (5.67px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1929 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 19.439m (327.77px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1930 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 19.417m (327.40px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1931 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.854m (14.39px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1932 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.848m (14.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1933 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.022m (0.38px@z21) | want=<0.1m | PASS
시뮬 SIM-R1934 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1935 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.022m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R1936 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1937 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.342m (5.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1938 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.336m (5.66px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1939 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 19.439m (327.76px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1940 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 19.417m (327.40px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1941 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.822m (13.86px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1942 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.802m (13.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1943 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.112m (1.90px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1944 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1945 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.112m (1.89px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1946 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1947 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 1.718m (28.96px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1948 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 1.679m (28.31px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1949 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 97.082m (1636.93px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1950 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 96.971m (1635.06px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1951 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 0.957m (16.14px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1952 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.747m (12.60px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1953 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.336m (5.67px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1954 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1955 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.336m (5.67px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1956 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1957 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 5.145m (86.75px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1958 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 4.991m (84.15px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1959 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 288.324m (4861.52px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1960 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 287.990m (4855.88px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1961 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 1.140m (19.23px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1962 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.866m (14.60px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1963 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.986m (16.63px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1964 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1965 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.986m (16.63px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1966 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1967 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 14.188m (239.22px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1968 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 13.574m (228.88px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1969 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 787.726m (13282.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1970 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 787.100m (13271.53px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1971 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 2.117m (35.69px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1972 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.879m (14.82px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1973 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 1.823m (30.73px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1974 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1975 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 1.823m (30.74px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1976 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1977 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 20.406m (344.08px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1978 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 20.068m (338.38px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1979 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 1113.223m (18770.39px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1980 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 1114.084m (18784.91px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1981 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 2.362m (39.82px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1982 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.948m (15.99px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1983 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 2.577m (43.46px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1984 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1985 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 2.578m (43.46px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1986 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1987 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 2.972m (50.10px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1988 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 1.044m (17.60px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1989 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 2.972m (50.11px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1990 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 1.058m (17.83px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R1991 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 2.414m (40.71px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1992 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.997m (16.82px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R1993 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 2.577m (43.46px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R1994 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R1995 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 2.577m (43.46px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1996 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R1997 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 2.971m (50.10px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R1998 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 1.044m (17.60px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R1999 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 2.971m (50.10px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2000 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 1.044m (17.60px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2001 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 2.080m (35.07px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2002 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.886m (14.93px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2003 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 1.823m (30.73px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2004 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2005 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 1.823m (30.74px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2006 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2007 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 18.172m (306.41px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2008 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 18.774m (316.55px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2009 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 1111.402m (18739.69px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2010 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 1113.008m (18766.76px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2011 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.854m (14.39px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2012 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.848m (14.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2013 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.022m (0.38px@z21) | want=<0.1m | PASS
시뮬 SIM-R2014 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2015 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.022m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2016 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2017 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.342m (5.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2018 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.336m (5.66px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2019 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 19.439m (327.76px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2020 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 19.417m (327.40px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2021 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.467m (7.88px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2022 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.467m (7.88px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2023 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2024 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2025 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2026 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2027 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2028 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2029 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2030 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2031 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.467m (7.88px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2032 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.467m (7.88px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2033 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2034 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2035 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2036 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2037 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2038 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2039 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2040 | 회전 | init=안양(37.4N) 범위5000m z17 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2041 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.222m (3.75px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2042 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.219m (3.69px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2043 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.022m (0.38px@z21) | want=<0.1m | PASS
시뮬 SIM-R2044 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2045 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.023m (0.39px@z21) | want=<0.1m | PASS
시뮬 SIM-R2046 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2047 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.343m (5.78px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2048 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.336m (5.67px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2049 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 19.439m (327.77px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2050 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 19.417m (327.40px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2051 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.226m (3.82px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2052 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.234m (3.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2053 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.022m (0.38px@z21) | want=<0.1m | PASS
시뮬 SIM-R2054 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2055 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.022m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2056 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2057 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.342m (5.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2058 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.336m (5.66px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2059 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 19.439m (327.76px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2060 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 19.417m (327.40px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2061 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.293m (4.93px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2062 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.281m (4.74px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2063 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.112m (1.90px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2064 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2065 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.112m (1.89px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2066 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2067 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 1.718m (28.96px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2068 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 1.679m (28.31px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2069 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 97.082m (1636.93px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2070 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 96.971m (1635.06px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2071 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 0.373m (6.29px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2072 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.236m (3.98px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2073 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.336m (5.67px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2074 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2075 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.336m (5.67px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2076 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2077 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 5.145m (86.75px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2078 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 4.991m (84.15px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2079 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 288.324m (4861.52px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2080 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 287.990m (4855.88px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2081 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 1.140m (19.23px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2082 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.242m (4.08px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2083 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.986m (16.63px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2084 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2085 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.986m (16.63px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2086 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2087 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 14.188m (239.22px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2088 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 13.574m (228.88px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2089 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 787.726m (13282.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2090 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 787.100m (13271.53px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2091 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 1.769m (29.83px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2092 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.210m (3.54px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2093 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 1.823m (30.73px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2094 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2095 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 1.823m (30.74px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2096 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2097 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 20.406m (344.08px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2098 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 20.068m (338.38px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2099 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 1113.223m (18770.39px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2100 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 1114.084m (18784.91px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2101 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 2.547m (42.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2102 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.255m (4.29px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2103 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 2.577m (43.46px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2104 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2105 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 2.578m (43.46px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2106 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2107 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 2.972m (50.10px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2108 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 1.044m (17.60px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2109 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 2.972m (50.11px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2110 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 1.058m (17.83px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2111 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 2.610m (44.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2112 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.255m (4.30px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2113 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 2.577m (43.46px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2114 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2115 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 2.577m (43.46px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2116 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2117 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 2.971m (50.10px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2118 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 1.044m (17.60px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2119 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 2.971m (50.10px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2120 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 1.044m (17.60px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2121 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 1.757m (29.63px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2122 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.191m (3.22px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2123 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 1.823m (30.73px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2124 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2125 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 1.823m (30.74px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2126 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2127 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 18.172m (306.41px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2128 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 18.774m (316.55px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2129 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 1111.402m (18739.69px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2130 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 1113.008m (18766.76px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2131 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.226m (3.82px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2132 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.234m (3.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2133 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.022m (0.38px@z21) | want=<0.1m | PASS
시뮬 SIM-R2134 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2135 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.022m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2136 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2137 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.342m (5.77px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2138 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.336m (5.66px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2139 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 19.439m (327.76px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2140 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 19.417m (327.40px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2141 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.153m (2.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2142 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.153m (2.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2143 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2144 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2145 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2146 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2147 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2148 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2149 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2150 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2151 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.153m (2.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2152 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.153m (2.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2153 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2154 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2155 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2156 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2157 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2158 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2159 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2160 | 회전 | init=안양(37.4N) 범위5000m z19 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2161 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 1.599m (42.84px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2162 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 1.599m (42.84px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2163 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2164 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2165 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2166 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2167 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2168 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2169 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2170 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2171 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 1.579m (42.32px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2172 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 1.579m (42.32px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2173 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2174 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2175 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2176 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2177 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2178 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2179 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2180 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2181 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 1.514m (40.58px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2182 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 1.514m (40.58px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2183 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2184 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2185 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2186 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2187 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.007m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R2188 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.007m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R2189 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 3.431m (91.92px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2190 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 3.431m (91.92px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2191 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 1.457m (39.04px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2192 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 1.457m (39.04px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2193 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2194 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2195 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2196 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2197 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.020m (0.54px@z21) | want=<0.1m | PASS
시뮬 SIM-R2198 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.020m (0.54px@z21) | want=<0.1m | PASS
시뮬 SIM-R2199 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 10.188m (272.98px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2200 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 10.188m (272.98px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2201 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 2.277m (61.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2202 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 2.277m (61.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2203 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2204 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2205 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2206 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2207 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.056m (1.49px@z21) | want=<0.1m | PASS
시뮬 SIM-R2208 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.055m (1.49px@z21) | want=<0.1m | PASS
시뮬 SIM-R2209 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 27.835m (745.79px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2210 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 27.835m (745.79px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2211 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 2.182m (58.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2212 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 2.182m (58.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2213 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2214 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2215 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2216 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2217 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2218 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2219 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 39.365m (1054.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2220 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 39.365m (1054.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2221 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 2.290m (61.35px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2222 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 2.290m (61.35px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2223 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2224 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2225 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2226 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2227 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2228 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2229 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.001m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R2230 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2231 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 2.290m (61.35px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2232 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 2.289m (61.34px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2233 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2234 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2235 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2236 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2237 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2238 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2239 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2240 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2241 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 2.178m (58.36px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2242 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 2.178m (58.37px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2243 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2244 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2245 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2246 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2247 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.078m (2.09px@z21) | want=<0.1m | PASS
시뮬 SIM-R2248 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2249 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 39.364m (1054.70px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2250 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 39.365m (1054.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2251 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 1.579m (42.32px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2252 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 1.579m (42.32px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2253 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2254 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2255 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2256 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2257 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2258 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2259 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2260 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2261 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 1.144m (30.65px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2262 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 1.144m (30.65px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2263 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2264 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2265 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2266 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2267 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2268 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2269 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2270 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2271 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 1.144m (30.65px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2272 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 1.144m (30.65px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2273 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2274 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2275 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2276 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2277 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2278 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2279 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2280 | 회전 | init=고위도(60N) 범위50m z15 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2281 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 0.436m (11.68px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2282 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.436m (11.68px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2283 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2284 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2285 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2286 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2287 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2288 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2289 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2290 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2291 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.477m (12.77px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2292 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.477m (12.77px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2293 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2294 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2295 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2296 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2297 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2298 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2299 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2300 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2301 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 0.721m (19.32px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2302 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.721m (19.32px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2303 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2304 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2305 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2306 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2307 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.007m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R2308 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.007m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R2309 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 3.431m (91.92px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2310 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 3.431m (91.92px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2311 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 0.604m (16.19px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2312 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.604m (16.18px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2313 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2314 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2315 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2316 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2317 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.020m (0.54px@z21) | want=<0.1m | PASS
시뮬 SIM-R2318 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.020m (0.54px@z21) | want=<0.1m | PASS
시뮬 SIM-R2319 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 10.188m (272.98px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2320 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 10.188m (272.98px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2321 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 0.629m (16.86px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2322 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.629m (16.86px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2323 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2324 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2325 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2326 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2327 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.056m (1.49px@z21) | want=<0.1m | PASS
시뮬 SIM-R2328 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.055m (1.49px@z21) | want=<0.1m | PASS
시뮬 SIM-R2329 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 27.835m (745.79px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2330 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 27.835m (745.79px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2331 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 0.408m (10.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2332 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.409m (10.95px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2333 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2334 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2335 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2336 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2337 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2338 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2339 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 39.365m (1054.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2340 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 39.365m (1054.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2341 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.411m (11.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2342 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.410m (10.99px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2343 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2344 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2345 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2346 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2347 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2348 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2349 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.001m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R2350 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2351 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 0.411m (11.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2352 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.411m (11.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2353 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2354 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2355 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2356 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2357 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2358 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2359 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2360 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2361 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 0.407m (10.89px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2362 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.407m (10.89px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2363 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2364 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2365 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2366 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2367 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.078m (2.09px@z21) | want=<0.1m | PASS
시뮬 SIM-R2368 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2369 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 39.364m (1054.70px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2370 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 39.365m (1054.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2371 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 0.477m (12.77px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2372 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.477m (12.77px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2373 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2374 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2375 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2376 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2377 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2378 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2379 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2380 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2381 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 0.377m (10.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2382 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.377m (10.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2383 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2384 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2385 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2386 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2387 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2388 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2389 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2390 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2391 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 0.377m (10.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2392 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.377m (10.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2393 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2394 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2395 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2396 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2397 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2398 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2399 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2400 | 회전 | init=고위도(60N) 범위50m z17 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2401 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 0.148m (3.96px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2402 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.148m (3.96px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2403 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2404 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2405 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2406 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2407 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2408 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2409 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2410 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2411 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.164m (4.40px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2412 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.164m (4.40px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2413 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2414 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2415 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2416 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2417 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2418 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2419 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2420 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2421 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 0.115m (3.07px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2422 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.115m (3.07px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2423 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2424 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2425 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2426 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2427 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.007m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R2428 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.007m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R2429 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 3.431m (91.92px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2430 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 3.431m (91.92px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2431 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 0.164m (4.39px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2432 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.164m (4.39px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2433 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2434 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2435 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2436 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2437 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.020m (0.54px@z21) | want=<0.1m | PASS
시뮬 SIM-R2438 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.020m (0.54px@z21) | want=<0.1m | PASS
시뮬 SIM-R2439 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 10.188m (272.98px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2440 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 10.188m (272.98px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2441 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 0.126m (3.36px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2442 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.126m (3.36px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2443 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2444 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2445 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2446 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2447 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.056m (1.49px@z21) | want=<0.1m | PASS
시뮬 SIM-R2448 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.055m (1.49px@z21) | want=<0.1m | PASS
시뮬 SIM-R2449 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 27.835m (745.79px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2450 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 27.835m (745.79px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2451 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 0.096m (2.56px@z21) | want=<0.1m | PASS
시뮬 SIM-R2452 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.096m (2.56px@z21) | want=<0.1m | PASS
시뮬 SIM-R2453 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2454 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2455 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2456 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2457 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2458 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2459 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 39.365m (1054.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2460 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 39.365m (1054.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2461 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.111m (2.98px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2462 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.111m (2.98px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2463 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2464 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2465 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2466 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2467 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2468 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2469 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.001m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R2470 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2471 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 0.111m (2.98px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2472 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.111m (2.98px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2473 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2474 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2475 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2476 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2477 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2478 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2479 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2480 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2481 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 0.092m (2.46px@z21) | want=<0.1m | PASS
시뮬 SIM-R2482 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.092m (2.46px@z21) | want=<0.1m | PASS
시뮬 SIM-R2483 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2484 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2485 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2486 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2487 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.078m (2.09px@z21) | want=<0.1m | PASS
시뮬 SIM-R2488 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2489 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 39.364m (1054.70px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2490 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 39.365m (1054.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2491 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 0.164m (4.40px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2492 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.164m (4.40px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2493 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2494 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2495 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2496 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2497 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2498 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.002m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R2499 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2500 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 0.687m (18.41px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2501 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2502 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2503 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2504 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2505 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2506 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2507 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2508 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2509 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2510 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2511 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2512 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.078m (2.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R2513 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2514 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2515 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2516 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2517 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2518 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2519 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2520 | 회전 | init=고위도(60N) 범위50m z19 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2521 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 1.913m (51.26px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2522 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 1.913m (51.26px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2523 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2524 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2525 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R2526 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2527 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.014m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2528 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.013m (0.35px@z21) | want=<0.1m | PASS
시뮬 SIM-R2529 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 6.870m (184.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2530 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 6.869m (184.06px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2531 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 2.610m (69.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2532 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 2.610m (69.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2533 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2534 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2535 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2536 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2537 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.014m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2538 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.013m (0.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R2539 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 6.870m (184.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2540 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 6.869m (184.06px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2541 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 2.201m (58.97px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2542 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 2.200m (58.96px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2543 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.003m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R2544 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2545 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.003m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R2546 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2547 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.068m (1.83px@z21) | want=<0.1m | PASS
시뮬 SIM-R2548 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.066m (1.76px@z21) | want=<0.1m | PASS
시뮬 SIM-R2549 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 34.308m (919.22px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2550 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 34.305m (919.16px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2551 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 2.258m (60.49px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2552 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 2.251m (60.30px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2553 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.008m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R2554 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2555 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.008m (0.21px@z21) | want=<0.1m | PASS
시뮬 SIM-R2556 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2557 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.203m (5.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2558 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.195m (5.24px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2559 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 101.882m (2729.75px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2560 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 101.874m (2729.55px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2561 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 2.116m (56.68px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2562 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 2.107m (56.45px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2563 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.022m (0.60px@z21) | want=<0.1m | PASS
시뮬 SIM-R2564 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2565 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.022m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R2566 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2567 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.558m (14.95px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2568 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.542m (14.52px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2569 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 278.343m (7457.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2570 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 278.332m (7457.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2571 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 1.559m (41.77px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2572 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 1.550m (41.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2573 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.041m (1.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R2574 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2575 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.042m (1.12px@z21) | want=<0.1m | PASS
시뮬 SIM-R2576 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2577 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.774m (20.73px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2578 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.789m (21.14px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2579 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 393.616m (10546.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2580 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 393.642m (10546.97px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2581 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 1.864m (49.93px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2582 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 1.840m (49.29px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2583 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.058m (1.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R2584 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2585 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.059m (1.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R2586 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2587 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.067m (1.80px@z21) | want=<0.1m | PASS
시뮬 SIM-R2588 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.024m (0.64px@z21) | want=<0.1m | PASS
시뮬 SIM-R2589 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.069m (1.85px@z21) | want=<0.1m | PASS
시뮬 SIM-R2590 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.029m (0.78px@z21) | want=<0.1m | PASS
시뮬 SIM-R2591 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 1.980m (53.04px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2592 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 1.950m (52.24px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2593 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.058m (1.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R2594 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2595 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.058m (1.56px@z21) | want=<0.1m | PASS
시뮬 SIM-R2596 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2597 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.068m (1.81px@z21) | want=<0.1m | PASS
시뮬 SIM-R2598 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.024m (0.64px@z21) | want=<0.1m | PASS
시뮬 SIM-R2599 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.067m (1.80px@z21) | want=<0.1m | PASS
시뮬 SIM-R2600 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.024m (0.63px@z21) | want=<0.1m | PASS
시뮬 SIM-R2601 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 1.438m (38.54px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2602 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 1.433m (38.39px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2603 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.041m (1.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R2604 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2605 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.041m (1.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R2606 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2607 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.722m (19.34px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2608 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.760m (20.36px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2609 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 393.591m (10545.60px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2610 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 393.623m (10546.46px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2611 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 2.610m (69.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2612 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 2.610m (69.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2613 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2614 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2615 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2616 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2617 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.014m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2618 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.013m (0.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R2619 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 6.870m (184.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2620 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 6.869m (184.06px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2621 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 1.276m (34.19px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2622 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 1.276m (34.19px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2623 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2624 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2625 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2626 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2627 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2628 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2629 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2630 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2631 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 1.276m (34.19px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2632 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 1.276m (34.19px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2633 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2634 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2635 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2636 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2637 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2638 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2639 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2640 | 회전 | init=고위도(60N) 범위500m z15 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2641 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.467m (12.50px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2642 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.466m (12.49px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2643 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2644 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2645 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R2646 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2647 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.014m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2648 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.013m (0.35px@z21) | want=<0.1m | PASS
시뮬 SIM-R2649 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 6.870m (184.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2650 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 6.869m (184.06px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2651 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.608m (16.29px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2652 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.608m (16.29px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2653 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2654 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2655 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2656 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2657 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.014m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2658 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.013m (0.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R2659 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 6.870m (184.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2660 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 6.869m (184.06px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2661 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.535m (14.33px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2662 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.535m (14.32px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2663 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.003m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R2664 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2665 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.003m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R2666 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2667 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.068m (1.83px@z21) | want=<0.1m | PASS
시뮬 SIM-R2668 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.066m (1.76px@z21) | want=<0.1m | PASS
시뮬 SIM-R2669 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 34.308m (919.22px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2670 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 34.305m (919.16px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2671 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 0.486m (13.03px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2672 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.482m (12.92px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2673 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.008m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R2674 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2675 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.008m (0.21px@z21) | want=<0.1m | PASS
시뮬 SIM-R2676 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2677 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 0.203m (5.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2678 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.195m (5.24px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2679 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 101.882m (2729.75px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2680 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 101.874m (2729.55px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2681 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 0.617m (16.54px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2682 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.616m (16.49px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2683 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.022m (0.60px@z21) | want=<0.1m | PASS
시뮬 SIM-R2684 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2685 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.022m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R2686 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2687 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 0.558m (14.95px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2688 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.542m (14.52px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2689 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 278.343m (7457.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2690 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 278.332m (7457.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2691 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 0.475m (12.73px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2692 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.471m (12.63px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2693 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.041m (1.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R2694 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2695 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 0.042m (1.12px@z21) | want=<0.1m | PASS
시뮬 SIM-R2696 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2697 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 0.774m (20.73px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2698 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.789m (21.14px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2699 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 393.616m (10546.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2700 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 393.642m (10546.97px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2701 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.543m (14.54px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2702 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.519m (13.92px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2703 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.058m (1.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R2704 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2705 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.059m (1.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R2706 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2707 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.067m (1.80px@z21) | want=<0.1m | PASS
시뮬 SIM-R2708 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.024m (0.64px@z21) | want=<0.1m | PASS
시뮬 SIM-R2709 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.069m (1.85px@z21) | want=<0.1m | PASS
시뮬 SIM-R2710 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.029m (0.78px@z21) | want=<0.1m | PASS
시뮬 SIM-R2711 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 0.540m (14.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2712 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.517m (13.85px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2713 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.058m (1.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R2714 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2715 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 0.058m (1.56px@z21) | want=<0.1m | PASS
시뮬 SIM-R2716 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2717 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 0.068m (1.81px@z21) | want=<0.1m | PASS
시뮬 SIM-R2718 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.024m (0.64px@z21) | want=<0.1m | PASS
시뮬 SIM-R2719 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 0.067m (1.80px@z21) | want=<0.1m | PASS
시뮬 SIM-R2720 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.024m (0.63px@z21) | want=<0.1m | PASS
시뮬 SIM-R2721 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 0.672m (18.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2722 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.673m (18.02px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2723 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.041m (1.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R2724 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2725 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 0.041m (1.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R2726 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2727 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 0.722m (19.34px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2728 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.760m (20.36px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2729 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 393.591m (10545.60px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2730 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 393.623m (10546.46px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2731 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.608m (16.29px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2732 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.608m (16.29px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2733 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2734 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2735 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2736 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2737 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.014m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2738 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.013m (0.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R2739 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 6.870m (184.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2740 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 6.869m (184.06px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2741 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.373m (9.98px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2742 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.373m (9.98px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2743 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2744 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2745 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2746 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2747 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2748 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2749 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2750 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2751 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.373m (9.98px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2752 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.373m (9.98px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2753 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2754 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2755 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2756 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2757 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2758 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2759 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2760 | 회전 | init=고위도(60N) 범위500m z17 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2761 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.148m (3.96px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2762 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.148m (3.96px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2763 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2764 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2765 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R2766 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2767 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.014m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2768 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.013m (0.35px@z21) | want=<0.1m | PASS
시뮬 SIM-R2769 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 6.870m (184.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2770 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 6.869m (184.06px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2771 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.145m (3.89px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2772 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.145m (3.89px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2773 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2774 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2775 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2776 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2777 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.014m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2778 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.013m (0.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R2779 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 6.870m (184.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2780 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 6.869m (184.06px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2781 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.135m (3.63px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2782 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.134m (3.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2783 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.003m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R2784 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2785 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.003m (0.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R2786 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2787 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.068m (1.83px@z21) | want=<0.1m | PASS
시뮬 SIM-R2788 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.066m (1.76px@z21) | want=<0.1m | PASS
시뮬 SIM-R2789 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 34.308m (919.22px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2790 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 34.305m (919.16px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2791 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 0.122m (3.26px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2792 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.125m (3.34px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2793 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.008m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R2794 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2795 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.008m (0.21px@z21) | want=<0.1m | PASS
시뮬 SIM-R2796 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2797 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 0.203m (5.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2798 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.195m (5.24px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2799 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 101.882m (2729.75px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2800 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 101.874m (2729.55px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2801 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 0.159m (4.27px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2802 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.162m (4.35px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2803 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.022m (0.60px@z21) | want=<0.1m | PASS
시뮬 SIM-R2804 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2805 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.022m (0.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R2806 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2807 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 0.558m (14.95px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2808 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.542m (14.52px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2809 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 278.343m (7457.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2810 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 278.332m (7457.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2811 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 0.163m (4.36px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2812 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.171m (4.57px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2813 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.041m (1.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R2814 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2815 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 0.042m (1.12px@z21) | want=<0.1m | PASS
시뮬 SIM-R2816 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2817 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 0.774m (20.73px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2818 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.789m (21.14px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2819 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 393.616m (10546.28px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2820 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 393.642m (10546.97px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2821 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.096m (2.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R2822 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.095m (2.54px@z21) | want=<0.1m | PASS
시뮬 SIM-R2823 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.058m (1.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R2824 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2825 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.059m (1.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R2826 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2827 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.067m (1.80px@z21) | want=<0.1m | PASS
시뮬 SIM-R2828 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.024m (0.64px@z21) | want=<0.1m | PASS
시뮬 SIM-R2829 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.069m (1.85px@z21) | want=<0.1m | PASS
시뮬 SIM-R2830 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.029m (0.78px@z21) | want=<0.1m | PASS
시뮬 SIM-R2831 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 0.100m (2.69px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2832 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.092m (2.46px@z21) | want=<0.1m | PASS
시뮬 SIM-R2833 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.058m (1.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R2834 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2835 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 0.058m (1.56px@z21) | want=<0.1m | PASS
시뮬 SIM-R2836 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2837 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 0.068m (1.81px@z21) | want=<0.1m | PASS
시뮬 SIM-R2838 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.024m (0.64px@z21) | want=<0.1m | PASS
시뮬 SIM-R2839 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 0.067m (1.80px@z21) | want=<0.1m | PASS
시뮬 SIM-R2840 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.024m (0.63px@z21) | want=<0.1m | PASS
시뮬 SIM-R2841 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 0.156m (4.18px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2842 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.147m (3.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2843 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.041m (1.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R2844 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2845 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 0.041m (1.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R2846 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2847 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 0.722m (19.34px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2848 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.760m (20.36px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2849 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 393.591m (10545.60px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2850 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 393.623m (10546.46px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2851 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.145m (3.89px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2852 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.145m (3.89px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2853 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2854 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2855 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2856 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2857 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.014m (0.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2858 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.013m (0.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R2859 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 6.870m (184.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2860 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 6.869m (184.06px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2861 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.080m (2.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R2862 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.080m (2.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R2863 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2864 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2865 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2866 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2867 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2868 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2869 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2870 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2871 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.080m (2.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R2872 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.080m (2.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R2873 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2874 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2875 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2876 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2877 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2878 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2879 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2880 | 회전 | init=고위도(60N) 범위500m z19 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2881 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 2.401m (64.33px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2882 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 2.409m (64.54px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2883 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.051m (1.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2884 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2885 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.051m (1.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R2886 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2887 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.140m (3.75px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2888 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.171m (4.59px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2889 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 68.684m (1840.26px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2890 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 68.633m (1838.90px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2891 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 2.269m (60.78px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2892 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 2.233m (59.83px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2893 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.051m (1.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2894 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2895 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.051m (1.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R2896 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2897 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.138m (3.70px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2898 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.171m (4.59px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2899 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 68.683m (1840.25px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2900 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 68.633m (1838.89px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2901 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 1.931m (51.74px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2902 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 2.168m (58.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2903 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.255m (6.83px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2904 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2905 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.255m (6.84px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2906 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2907 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.718m (19.24px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2908 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.859m (23.01px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2909 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 343.005m (9190.23px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2910 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 342.750m (9183.40px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2911 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 1.944m (52.09px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2912 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 1.665m (44.61px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2913 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.763m (20.43px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2914 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2915 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.763m (20.43px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2916 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2917 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 2.289m (61.33px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2918 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 2.559m (68.58px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2919 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 1018.606m (27291.80px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2920 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 1017.873m (27272.17px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2921 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 3.554m (95.23px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2922 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 2.157m (57.79px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2923 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 2.236m (59.90px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2924 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2925 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 2.236m (59.90px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2926 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2927 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 7.546m (202.17px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2928 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 6.851m (183.55px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2929 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 2782.553m (74553.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2930 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 2781.471m (74524.79px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2931 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 5.093m (136.45px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2932 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 1.899m (50.89px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2933 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 4.130m (110.65px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2934 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2935 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 4.130m (110.66px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2936 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2937 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 12.409m (332.47px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2938 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 8.924m (239.10px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2939 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 3933.179m (105382.85px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2940 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 3935.756m (105451.89px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2941 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 7.592m (203.42px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2942 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 1.774m (47.54px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2943 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 5.841m (156.50px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2944 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2945 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 5.841m (156.50px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2946 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2947 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 6.734m (180.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2948 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 2.365m (63.37px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2949 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 6.749m (180.82px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2950 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 2.419m (64.80px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2951 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 7.658m (205.19px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2952 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 1.847m (49.48px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2953 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 5.841m (156.50px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2954 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2955 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 5.841m (156.50px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2956 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2957 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 6.734m (180.43px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2958 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 2.365m (63.37px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2959 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 6.734m (180.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2960 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 2.365m (63.36px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2961 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 5.362m (143.66px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2962 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 1.404m (37.62px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2963 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 4.132m (110.71px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R2964 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2965 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 4.132m (110.72px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2966 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2967 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 7.501m (200.97px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2968 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 8.042m (215.46px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2969 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 3930.636m (105314.70px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2970 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 3933.865m (105401.21px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2971 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 2.269m (60.78px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2972 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 2.233m (59.83px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2973 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.051m (1.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R2974 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2975 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.051m (1.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R2976 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R2977 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.138m (3.70px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R2978 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.171m (4.59px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R2979 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 68.683m (1840.25px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2980 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 68.633m (1838.89px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R2981 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 1.473m (39.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2982 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 1.473m (39.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2983 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2984 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2985 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2986 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2987 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2988 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2989 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2990 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2991 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 1.473m (39.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2992 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 1.473m (39.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R2993 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2994 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R2995 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2996 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2997 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2998 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R2999 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3000 | 회전 | init=고위도(60N) 범위5000m z15 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3001 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.324m (8.69px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3002 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.325m (8.72px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3003 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.051m (1.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R3004 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3005 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.051m (1.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R3006 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3007 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.140m (3.75px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3008 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.171m (4.59px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3009 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 68.684m (1840.26px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3010 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 68.633m (1838.90px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3011 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.485m (13.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3012 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.481m (12.90px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3013 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.051m (1.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R3014 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3015 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.051m (1.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R3016 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3017 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.138m (3.70px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3018 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.171m (4.59px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3019 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 68.683m (1840.25px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3020 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 68.633m (1838.89px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3021 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.676m (18.12px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3022 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.651m (17.45px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3023 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.255m (6.83px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3024 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3025 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.255m (6.84px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3026 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3027 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.718m (19.24px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3028 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.859m (23.01px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3029 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 343.005m (9190.23px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3030 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 342.750m (9183.40px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3031 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 1.003m (26.88px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3032 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.501m (13.41px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3033 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.763m (20.43px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3034 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3035 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.763m (20.43px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3036 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3037 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 2.289m (61.33px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3038 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 2.559m (68.58px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3039 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 1018.606m (27291.80px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3040 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 1017.873m (27272.17px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3041 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 2.146m (57.51px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3042 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.498m (13.34px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3043 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 2.236m (59.90px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3044 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3045 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 2.236m (59.90px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3046 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3047 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 7.546m (202.17px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3048 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 6.851m (183.55px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3049 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 2782.553m (74553.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3050 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 2781.471m (74524.79px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3051 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 3.829m (102.58px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3052 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.653m (17.50px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3053 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 4.130m (110.65px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3054 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3055 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 4.130m (110.66px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3056 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3057 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 12.409m (332.47px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3058 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 8.924m (239.10px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3059 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 3933.179m (105382.85px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3060 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 3935.756m (105451.89px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3061 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 5.563m (149.04px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3062 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.481m (12.90px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3063 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 5.841m (156.50px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3064 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3065 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 5.841m (156.50px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3066 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3067 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 6.734m (180.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3068 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 2.365m (63.37px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3069 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 6.749m (180.82px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3070 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 2.419m (64.80px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3071 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 5.623m (150.67px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3072 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.430m (11.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3073 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 5.841m (156.50px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3074 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3075 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 5.841m (156.50px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3076 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3077 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 6.734m (180.43px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3078 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 2.365m (63.37px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3079 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 6.734m (180.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3080 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 2.365m (63.36px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3081 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 4.188m (112.20px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3082 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.493m (13.20px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3083 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 4.132m (110.71px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3084 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3085 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 4.132m (110.72px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3086 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3087 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 7.501m (200.97px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3088 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 8.042m (215.46px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3089 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 3930.636m (105314.70px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3090 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 3933.865m (105401.21px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3091 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.485m (13.01px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3092 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.481m (12.90px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3093 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.051m (1.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R3094 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3095 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.051m (1.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R3096 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3097 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.138m (3.70px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3098 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.171m (4.59px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3099 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 68.683m (1840.25px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3100 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 68.633m (1838.89px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3101 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.373m (10.00px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3102 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.373m (10.00px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3103 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3104 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3105 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3106 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3107 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3108 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3109 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3110 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3111 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.373m (10.00px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3112 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.373m (10.00px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3113 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3114 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3115 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3116 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3117 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3118 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3119 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3120 | 회전 | init=고위도(60N) 범위5000m z17 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3121 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.149m (3.99px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3122 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.152m (4.08px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3123 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.051m (1.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R3124 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3125 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.051m (1.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R3126 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3127 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.140m (3.75px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3128 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.171m (4.59px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3129 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 68.684m (1840.26px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3130 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 68.633m (1838.90px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3131 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.155m (4.15px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3132 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.148m (3.96px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3133 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.051m (1.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R3134 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3135 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.051m (1.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R3136 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3137 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.138m (3.70px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3138 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.171m (4.59px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3139 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 68.683m (1840.25px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3140 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 68.633m (1838.89px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3141 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.356m (9.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3142 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.192m (5.16px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3143 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.255m (6.83px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3144 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3145 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.255m (6.84px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3146 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3147 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.718m (19.24px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3148 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.859m (23.01px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3149 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 343.005m (9190.23px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3150 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 342.750m (9183.40px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3151 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 0.795m (21.29px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3152 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.141m (3.77px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3153 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.763m (20.43px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3154 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3155 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.763m (20.43px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3156 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3157 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 2.289m (61.33px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3158 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 2.559m (68.58px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3159 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 1018.606m (27291.80px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3160 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 1017.873m (27272.17px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3161 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 2.271m (60.84px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3162 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.136m (3.66px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3163 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 2.236m (59.90px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3164 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3165 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 2.236m (59.90px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3166 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3167 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 7.546m (202.17px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3168 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 6.851m (183.55px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3169 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 2782.553m (74553.78px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3170 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 2781.471m (74524.79px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3171 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 4.083m (109.41px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3172 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.142m (3.79px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3173 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 4.130m (110.65px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3174 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3175 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 4.130m (110.66px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3176 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3177 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 12.409m (332.47px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3178 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 8.924m (239.10px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3179 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 3933.179m (105382.85px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3180 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 3935.756m (105451.89px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3181 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 5.808m (155.62px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3182 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.132m (3.54px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3183 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 5.841m (156.50px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3184 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3185 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 5.841m (156.50px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3186 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3187 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 6.734m (180.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3188 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 2.365m (63.37px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3189 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 6.749m (180.82px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3190 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 2.419m (64.80px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3191 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 5.835m (156.33px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3192 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.101m (2.70px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3193 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 5.841m (156.50px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3194 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3195 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 5.841m (156.50px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3196 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3197 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 6.734m (180.43px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3198 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 2.365m (63.37px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3199 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 6.734m (180.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3200 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 2.365m (63.36px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3201 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 4.188m (112.20px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3202 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.169m (4.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3203 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 4.132m (110.71px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3204 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3205 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 4.132m (110.72px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3206 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3207 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 7.501m (200.97px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3208 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 8.042m (215.46px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3209 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 3930.636m (105314.70px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3210 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 3933.865m (105401.21px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3211 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.155m (4.15px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3212 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.148m (3.96px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3213 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.051m (1.37px@z21) | want=<0.1m | PASS
시뮬 SIM-R3214 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3215 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.051m (1.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R3216 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3217 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.138m (3.70px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3218 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.171m (4.59px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3219 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 68.683m (1840.25px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3220 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 68.633m (1838.89px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3221 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.100m (2.67px@z21) | want=<0.1m | PASS
시뮬 SIM-R3222 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.100m (2.67px@z21) | want=<0.1m | PASS
시뮬 SIM-R3223 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3224 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3225 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3226 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3227 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3228 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3229 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3230 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3231 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.100m (2.67px@z21) | want=<0.1m | PASS
시뮬 SIM-R3232 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.100m (2.67px@z21) | want=<0.1m | PASS
시뮬 SIM-R3233 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3234 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3235 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3236 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3237 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3238 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3239 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3240 | 회전 | init=고위도(60N) 범위5000m z19 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3241 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 0.727m (56.07px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3242 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.727m (56.07px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3243 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3244 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3245 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3246 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3247 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3248 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3249 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3250 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3251 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.573m (44.17px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3252 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.573m (44.17px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3253 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3254 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3255 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3256 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3257 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3258 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3259 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3260 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3261 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 0.558m (43.07px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3262 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.558m (43.07px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3263 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3264 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3265 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3266 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3267 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3268 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3269 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 15.369m (1185.72px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3270 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 15.369m (1185.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3271 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 0.507m (39.12px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3272 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.507m (39.11px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3273 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3274 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3275 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.05px@z21) | want=<0.1m | PASS
시뮬 SIM-R3276 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3277 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.002m (0.19px@z21) | want=<0.1m | PASS
시뮬 SIM-R3278 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.002m (0.19px@z21) | want=<0.1m | PASS
시뮬 SIM-R3279 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 45.641m (3521.12px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3280 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 45.641m (3521.11px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3281 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 0.528m (40.75px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3282 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.528m (40.76px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3283 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3284 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3285 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3286 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3287 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.007m (0.55px@z21) | want=<0.1m | PASS
시뮬 SIM-R3288 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.007m (0.54px@z21) | want=<0.1m | PASS
시뮬 SIM-R3289 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 124.693m (9619.88px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3290 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 124.693m (9619.86px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3291 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 0.573m (44.21px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3292 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.574m (44.25px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3293 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.001m (0.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R3294 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3295 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R3296 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3297 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.011m (0.83px@z21) | want=<0.1m | PASS
시뮬 SIM-R3298 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.010m (0.76px@z21) | want=<0.1m | PASS
시뮬 SIM-R3299 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 176.343m (13604.51px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3300 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 176.344m (13604.60px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3301 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.549m (42.37px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3302 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.551m (42.49px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3303 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.002m (0.15px@z21) | want=<0.1m | PASS
시뮬 SIM-R3304 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3305 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.002m (0.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R3306 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3307 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.002m (0.17px@z21) | want=<0.1m | PASS
시뮬 SIM-R3308 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.09px@z21) | want=<0.1m | PASS
시뮬 SIM-R3309 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.004m (0.35px@z21) | want=<0.1m | PASS
시뮬 SIM-R3310 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.004m (0.29px@z21) | want=<0.1m | PASS
시뮬 SIM-R3311 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 0.549m (42.33px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3312 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.550m (42.45px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3313 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.002m (0.15px@z21) | want=<0.1m | PASS
시뮬 SIM-R3314 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3315 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.002m (0.16px@z21) | want=<0.1m | PASS
시뮬 SIM-R3316 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3317 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.002m (0.16px@z21) | want=<0.1m | PASS
시뮬 SIM-R3318 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R3319 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.002m (0.17px@z21) | want=<0.1m | PASS
시뮬 SIM-R3320 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3321 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 0.395m (30.45px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3322 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.395m (30.51px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3323 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.001m (0.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R3324 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3325 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.002m (0.12px@z21) | want=<0.1m | PASS
시뮬 SIM-R3326 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.000m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R3327 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.008m (0.65px@z21) | want=<0.1m | PASS
시뮬 SIM-R3328 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.009m (0.70px@z21) | want=<0.1m | PASS
시뮬 SIM-R3329 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 176.342m (13604.49px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3330 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 176.343m (13604.56px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3331 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 0.573m (44.17px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3332 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.573m (44.17px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3333 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3334 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3335 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3336 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3337 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3338 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3339 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3340 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3341 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 0.458m (35.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3342 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.458m (35.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3343 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3344 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3345 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3346 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3347 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3348 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3349 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3350 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3351 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 0.458m (35.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3352 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.458m (35.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3353 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3354 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3355 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3356 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3357 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3358 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3359 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3360 | 회전 | init=극근접(80N) 범위50m z15 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3361 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 0.176m (13.57px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3362 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.176m (13.57px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3363 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3364 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3365 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3366 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3367 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3368 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3369 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3370 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3371 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.170m (13.12px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3372 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.170m (13.12px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3373 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3374 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3375 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3376 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3377 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3378 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3379 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3380 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3381 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 0.184m (14.20px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3382 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.184m (14.20px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3383 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3384 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3385 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3386 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3387 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3388 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3389 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 15.369m (1185.72px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3390 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 15.369m (1185.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3391 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 0.160m (12.34px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3392 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.160m (12.34px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3393 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3394 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3395 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.05px@z21) | want=<0.1m | PASS
시뮬 SIM-R3396 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3397 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.002m (0.19px@z21) | want=<0.1m | PASS
시뮬 SIM-R3398 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.002m (0.19px@z21) | want=<0.1m | PASS
시뮬 SIM-R3399 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 45.641m (3521.12px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3400 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 45.641m (3521.11px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3401 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 0.190m (14.67px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3402 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.191m (14.70px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3403 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3404 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3405 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3406 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3407 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.007m (0.55px@z21) | want=<0.1m | PASS
시뮬 SIM-R3408 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.007m (0.54px@z21) | want=<0.1m | PASS
시뮬 SIM-R3409 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 124.693m (9619.88px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3410 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 124.693m (9619.86px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3411 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 0.165m (12.72px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3412 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.166m (12.79px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3413 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.001m (0.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R3414 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3415 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R3416 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3417 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.011m (0.83px@z21) | want=<0.1m | PASS
시뮬 SIM-R3418 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.010m (0.76px@z21) | want=<0.1m | PASS
시뮬 SIM-R3419 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 176.343m (13604.51px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3420 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 176.344m (13604.60px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3421 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.154m (11.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3422 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.155m (11.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3423 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.002m (0.15px@z21) | want=<0.1m | PASS
시뮬 SIM-R3424 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3425 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.002m (0.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R3426 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3427 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.002m (0.17px@z21) | want=<0.1m | PASS
시뮬 SIM-R3428 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.09px@z21) | want=<0.1m | PASS
시뮬 SIM-R3429 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.004m (0.35px@z21) | want=<0.1m | PASS
시뮬 SIM-R3430 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.004m (0.29px@z21) | want=<0.1m | PASS
시뮬 SIM-R3431 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 0.155m (11.92px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3432 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.155m (11.95px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3433 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.002m (0.15px@z21) | want=<0.1m | PASS
시뮬 SIM-R3434 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3435 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.002m (0.16px@z21) | want=<0.1m | PASS
시뮬 SIM-R3436 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3437 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.002m (0.16px@z21) | want=<0.1m | PASS
시뮬 SIM-R3438 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R3439 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.002m (0.17px@z21) | want=<0.1m | PASS
시뮬 SIM-R3440 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3441 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 0.128m (9.87px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3442 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.128m (9.84px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3443 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.001m (0.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R3444 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3445 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.002m (0.12px@z21) | want=<0.1m | PASS
시뮬 SIM-R3446 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.000m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R3447 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.008m (0.65px@z21) | want=<0.1m | PASS
시뮬 SIM-R3448 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.009m (0.70px@z21) | want=<0.1m | PASS
시뮬 SIM-R3449 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 176.342m (13604.49px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3450 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 176.343m (13604.56px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3451 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 0.170m (13.12px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3452 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.170m (13.12px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3453 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3454 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3455 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3456 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3457 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3458 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3459 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3460 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3461 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 0.118m (9.08px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3462 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.118m (9.08px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3463 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3464 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3465 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3466 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3467 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3468 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3469 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3470 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3471 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 0.118m (9.08px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3472 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.118m (9.08px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3473 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3474 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3475 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3476 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3477 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3478 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3479 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3480 | 회전 | init=극근접(80N) 범위50m z17 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3481 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.041m (3.15px@z21) | want=<0.1m | PASS
시뮬 SIM-R3482 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.041m (3.15px@z21) | want=<0.1m | PASS
시뮬 SIM-R3483 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3484 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3485 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3486 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3487 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3488 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3489 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3490 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3491 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.043m (3.32px@z21) | want=<0.1m | PASS
시뮬 SIM-R3492 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.043m (3.32px@z21) | want=<0.1m | PASS
시뮬 SIM-R3493 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3494 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3495 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3496 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3497 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3498 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3499 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3500 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3501 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.034m (2.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R3502 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.034m (2.59px@z21) | want=<0.1m | PASS
시뮬 SIM-R3503 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.000m (0.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3504 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3505 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3506 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3507 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3508 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3509 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 15.369m (1185.72px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3510 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 15.369m (1185.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3511 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 0.049m (3.78px@z21) | want=<0.1m | PASS
시뮬 SIM-R3512 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.049m (3.78px@z21) | want=<0.1m | PASS
시뮬 SIM-R3513 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.000m (0.02px@z21) | want=<0.1m | PASS
시뮬 SIM-R3514 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3515 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.001m (0.05px@z21) | want=<0.1m | PASS
시뮬 SIM-R3516 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3517 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 0.002m (0.19px@z21) | want=<0.1m | PASS
시뮬 SIM-R3518 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.002m (0.19px@z21) | want=<0.1m | PASS
시뮬 SIM-R3519 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 45.641m (3521.12px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3520 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 45.641m (3521.11px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3521 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 0.049m (3.78px@z21) | want=<0.1m | PASS
시뮬 SIM-R3522 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.049m (3.78px@z21) | want=<0.1m | PASS
시뮬 SIM-R3523 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3524 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3525 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3526 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3527 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 0.007m (0.55px@z21) | want=<0.1m | PASS
시뮬 SIM-R3528 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.007m (0.54px@z21) | want=<0.1m | PASS
시뮬 SIM-R3529 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 124.693m (9619.88px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3530 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 124.693m (9619.86px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3531 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 0.026m (1.98px@z21) | want=<0.1m | PASS
시뮬 SIM-R3532 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.026m (1.99px@z21) | want=<0.1m | PASS
시뮬 SIM-R3533 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.001m (0.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R3534 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3535 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 0.001m (0.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R3536 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3537 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 0.011m (0.83px@z21) | want=<0.1m | PASS
시뮬 SIM-R3538 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.010m (0.76px@z21) | want=<0.1m | PASS
시뮬 SIM-R3539 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 176.343m (13604.51px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3540 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 176.344m (13604.60px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3541 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.031m (2.40px@z21) | want=<0.1m | PASS
시뮬 SIM-R3542 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.032m (2.44px@z21) | want=<0.1m | PASS
시뮬 SIM-R3543 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.002m (0.15px@z21) | want=<0.1m | PASS
시뮬 SIM-R3544 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3545 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.002m (0.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R3546 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3547 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.002m (0.17px@z21) | want=<0.1m | PASS
시뮬 SIM-R3548 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.001m (0.09px@z21) | want=<0.1m | PASS
시뮬 SIM-R3549 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.004m (0.35px@z21) | want=<0.1m | PASS
시뮬 SIM-R3550 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.004m (0.29px@z21) | want=<0.1m | PASS
시뮬 SIM-R3551 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 0.031m (2.36px@z21) | want=<0.1m | PASS
시뮬 SIM-R3552 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.031m (2.40px@z21) | want=<0.1m | PASS
시뮬 SIM-R3553 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.002m (0.15px@z21) | want=<0.1m | PASS
시뮬 SIM-R3554 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3555 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 0.002m (0.16px@z21) | want=<0.1m | PASS
시뮬 SIM-R3556 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3557 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 0.002m (0.16px@z21) | want=<0.1m | PASS
시뮬 SIM-R3558 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.001m (0.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R3559 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 0.002m (0.17px@z21) | want=<0.1m | PASS
시뮬 SIM-R3560 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.001m (0.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R3561 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 0.032m (2.47px@z21) | want=<0.1m | PASS
시뮬 SIM-R3562 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.033m (2.51px@z21) | want=<0.1m | PASS
시뮬 SIM-R3563 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.001m (0.10px@z21) | want=<0.1m | PASS
시뮬 SIM-R3564 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3565 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 0.002m (0.12px@z21) | want=<0.1m | PASS
시뮬 SIM-R3566 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.000m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R3567 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 0.008m (0.65px@z21) | want=<0.1m | PASS
시뮬 SIM-R3568 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.009m (0.70px@z21) | want=<0.1m | PASS
시뮬 SIM-R3569 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 176.342m (13604.49px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3570 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 176.343m (13604.56px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3571 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.043m (3.32px@z21) | want=<0.1m | PASS
시뮬 SIM-R3572 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.043m (3.32px@z21) | want=<0.1m | PASS
시뮬 SIM-R3573 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3574 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3575 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3576 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3577 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3578 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3579 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3580 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 3.078m (237.43px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3581 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.027m (2.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R3582 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.027m (2.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R3583 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3584 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3585 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3586 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3587 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3588 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3589 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3590 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3591 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.027m (2.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R3592 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.027m (2.07px@z21) | want=<0.1m | PASS
시뮬 SIM-R3593 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3594 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3595 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3596 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3597 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3598 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3599 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3600 | 회전 | init=극근접(80N) 범위50m z19 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3601 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 1° [M1 vs GT-GEO] → got=오차 0.861m (66.41px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3602 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.860m (66.36px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3603 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.002m (0.13px@z21) | want=<0.1m | PASS
시뮬 SIM-R3604 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3605 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 1° [M2 vs GT-GEO] → got=오차 0.002m (0.13px@z21) | want=<0.1m | PASS
시뮬 SIM-R3606 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3607 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 1° [M2E vs GT-GEO] → got=오차 0.002m (0.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R3608 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.003m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R3609 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 1° [M3 vs GT-GEO] → got=오차 30.773m (2374.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3610 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 1° [M3 vs GT-SCREEN] → got=오차 30.771m (2373.94px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3611 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.716m (55.26px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3612 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.716m (55.27px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3613 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.002m (0.13px@z21) | want=<0.1m | PASS
시뮬 SIM-R3614 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3615 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R3616 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.000m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R3617 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.002m (0.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R3618 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.003m (0.22px@z21) | want=<0.1m | PASS
시뮬 SIM-R3619 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 -1° [M3 vs GT-GEO] → got=오차 30.773m (2374.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3620 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 30.771m (2373.94px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3621 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 5° [M1 vs GT-GEO] → got=오차 0.596m (45.96px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3622 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.600m (46.30px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3623 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.008m (0.64px@z21) | want=<0.1m | PASS
시뮬 SIM-R3624 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3625 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 5° [M2 vs GT-GEO] → got=오차 0.008m (0.61px@z21) | want=<0.1m | PASS
시뮬 SIM-R3626 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3627 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 5° [M2E vs GT-GEO] → got=오차 0.011m (0.83px@z21) | want=<0.1m | PASS
시뮬 SIM-R3628 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.015m (1.18px@z21) | want=<0.1m | PASS
시뮬 SIM-R3629 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 5° [M3 vs GT-GEO] → got=오차 153.677m (11855.89px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3630 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 5° [M3 vs GT-SCREEN] → got=오차 153.669m (11855.27px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3631 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 15° [M1 vs GT-GEO] → got=오차 0.878m (67.77px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3632 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.876m (67.59px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3633 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.025m (1.93px@z21) | want=<0.1m | PASS
시뮬 SIM-R3634 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3635 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 15° [M2 vs GT-GEO] → got=오차 0.025m (1.91px@z21) | want=<0.1m | PASS
시뮬 SIM-R3636 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3637 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 15° [M2E vs GT-GEO] → got=오차 0.037m (2.88px@z21) | want=<0.1m | PASS
시뮬 SIM-R3638 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.046m (3.52px@z21) | want=<0.1m | PASS
시뮬 SIM-R3639 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 15° [M3 vs GT-GEO] → got=오차 456.360m (35207.34px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3640 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 15° [M3 vs GT-SCREEN] → got=오차 456.338m (35205.66px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3641 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 45° [M1 vs GT-GEO] → got=오차 0.795m (61.34px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3642 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.775m (59.82px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3643 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.073m (5.65px@z21) | want=<0.1m | PASS
시뮬 SIM-R3644 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3645 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 45° [M2 vs GT-GEO] → got=오차 0.073m (5.66px@z21) | want=<0.1m | PASS
시뮬 SIM-R3646 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3647 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 45° [M2E vs GT-GEO] → got=오차 0.145m (11.16px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3648 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.120m (9.24px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3649 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 45° [M3 vs GT-GEO] → got=오차 1246.777m (96186.64px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3650 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 45° [M3 vs GT-SCREEN] → got=오차 1246.758m (96185.14px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3651 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 90° [M1 vs GT-GEO] → got=오차 0.714m (55.08px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3652 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.700m (53.97px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3653 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.135m (10.43px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3654 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3655 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 90° [M2 vs GT-GEO] → got=오차 0.135m (10.43px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3656 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3657 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 90° [M2E vs GT-GEO] → got=오차 0.257m (19.86px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3658 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.143m (11.06px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3659 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 90° [M3 vs GT-GEO] → got=오차 1763.152m (136024.03px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3660 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 90° [M3 vs GT-SCREEN] → got=오차 1763.259m (136032.27px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3661 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.895m (69.07px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3662 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.905m (69.80px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3663 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.191m (14.76px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3664 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3665 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.191m (14.76px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3666 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3667 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.220m (17.00px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3668 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.078m (5.99px@z21) | want=<0.1m | PASS
시뮬 SIM-R3669 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.235m (18.15px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3670 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.103m (7.95px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3671 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 180° [M1 vs GT-GEO] → got=오차 0.900m (69.42px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3672 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.909m (70.16px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3673 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.191m (14.76px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3674 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3675 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 180° [M2 vs GT-GEO] → got=오차 0.191m (14.75px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3676 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3677 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 180° [M2E vs GT-GEO] → got=오차 0.221m (17.03px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3678 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.077m (5.97px@z21) | want=<0.1m | PASS
시뮬 SIM-R3679 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 180° [M3 vs GT-GEO] → got=오차 0.220m (17.01px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3680 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.077m (5.98px@z21) | want=<0.1m | PASS
시뮬 SIM-R3681 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 270° [M1 vs GT-GEO] → got=오차 0.604m (46.62px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3682 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.510m (39.37px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3683 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.135m (10.44px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3684 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3685 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 270° [M2 vs GT-GEO] → got=오차 0.135m (10.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3686 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3687 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 270° [M2E vs GT-GEO] → got=오차 0.134m (10.36px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3688 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.135m (10.41px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3689 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 270° [M3 vs GT-GEO] → got=오차 1763.125m (136021.97px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3690 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 270° [M3 vs GT-SCREEN] → got=오차 1763.208m (136028.39px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3691 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 359° [M1 vs GT-GEO] → got=오차 0.716m (55.26px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3692 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.716m (55.27px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3693 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.002m (0.13px@z21) | want=<0.1m | PASS
시뮬 SIM-R3694 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3695 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R3696 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.000m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R3697 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 359° [M2E vs GT-GEO] → got=오차 0.002m (0.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R3698 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.003m (0.22px@z21) | want=<0.1m | PASS
시뮬 SIM-R3699 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 359° [M3 vs GT-GEO] → got=오차 30.773m (2374.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3700 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 359° [M3 vs GT-SCREEN] → got=오차 30.771m (2373.94px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3701 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 360° [M1 vs GT-GEO] → got=오차 0.534m (41.22px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3702 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.534m (41.22px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3703 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3704 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3705 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3706 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3707 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3708 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3709 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3710 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3711 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 720° [M1 vs GT-GEO] → got=오차 0.534m (41.22px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3712 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.534m (41.22px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3713 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3714 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3715 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3716 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3717 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3718 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3719 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3720 | 회전 | init=극근접(80N) 범위500m z15 8점 화면내 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3721 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.166m (12.81px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3722 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.165m (12.75px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3723 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.002m (0.13px@z21) | want=<0.1m | PASS
시뮬 SIM-R3724 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3725 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.002m (0.13px@z21) | want=<0.1m | PASS
시뮬 SIM-R3726 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3727 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.002m (0.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R3728 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.003m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R3729 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 30.773m (2374.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3730 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 30.771m (2373.94px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3731 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.164m (12.61px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3732 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.164m (12.62px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3733 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.002m (0.13px@z21) | want=<0.1m | PASS
시뮬 SIM-R3734 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3735 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R3736 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.000m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R3737 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.002m (0.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R3738 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.003m (0.22px@z21) | want=<0.1m | PASS
시뮬 SIM-R3739 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 30.773m (2374.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3740 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 30.771m (2373.94px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3741 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.180m (13.90px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3742 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.180m (13.92px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3743 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.008m (0.64px@z21) | want=<0.1m | PASS
시뮬 SIM-R3744 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3745 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.008m (0.61px@z21) | want=<0.1m | PASS
시뮬 SIM-R3746 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3747 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.011m (0.83px@z21) | want=<0.1m | PASS
시뮬 SIM-R3748 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.015m (1.18px@z21) | want=<0.1m | PASS
시뮬 SIM-R3749 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 153.677m (11855.89px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3750 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 153.669m (11855.27px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3751 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 0.185m (14.29px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3752 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.185m (14.24px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3753 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.025m (1.93px@z21) | want=<0.1m | PASS
시뮬 SIM-R3754 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3755 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.025m (1.91px@z21) | want=<0.1m | PASS
시뮬 SIM-R3756 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3757 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 0.037m (2.88px@z21) | want=<0.1m | PASS
시뮬 SIM-R3758 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.046m (3.52px@z21) | want=<0.1m | PASS
시뮬 SIM-R3759 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 456.360m (35207.34px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3760 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 456.338m (35205.66px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3761 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 0.201m (15.53px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3762 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.180m (13.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3763 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.073m (5.65px@z21) | want=<0.1m | PASS
시뮬 SIM-R3764 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3765 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.073m (5.66px@z21) | want=<0.1m | PASS
시뮬 SIM-R3766 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3767 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 0.145m (11.16px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3768 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.120m (9.24px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3769 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 1246.777m (96186.64px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3770 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 1246.758m (96185.14px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3771 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 0.212m (16.37px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3772 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.156m (12.06px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3773 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.135m (10.43px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3774 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3775 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 0.135m (10.43px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3776 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3777 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 0.257m (19.86px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3778 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.143m (11.06px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3779 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 1763.152m (136024.03px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3780 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 1763.259m (136032.27px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3781 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.328m (25.31px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3782 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.167m (12.85px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3783 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.191m (14.76px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3784 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3785 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.191m (14.76px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3786 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3787 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.220m (17.00px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3788 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.078m (5.99px@z21) | want=<0.1m | PASS
시뮬 SIM-R3789 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.235m (18.15px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3790 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.103m (7.95px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3791 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 0.334m (25.74px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3792 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.172m (13.30px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3793 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.191m (14.76px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3794 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3795 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 0.191m (14.75px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3796 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3797 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 0.221m (17.03px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3798 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.077m (5.97px@z21) | want=<0.1m | PASS
시뮬 SIM-R3799 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 0.220m (17.01px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3800 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.077m (5.98px@z21) | want=<0.1m | PASS
시뮬 SIM-R3801 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 0.210m (16.17px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3802 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.142m (10.93px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3803 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.135m (10.44px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3804 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3805 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 0.135m (10.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3806 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3807 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 0.134m (10.36px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3808 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.135m (10.41px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3809 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 1763.125m (136021.97px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3810 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 1763.208m (136028.39px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3811 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.164m (12.61px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3812 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.164m (12.62px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3813 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.002m (0.13px@z21) | want=<0.1m | PASS
시뮬 SIM-R3814 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3815 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R3816 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.000m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R3817 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.002m (0.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R3818 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.003m (0.22px@z21) | want=<0.1m | PASS
시뮬 SIM-R3819 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 30.773m (2374.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3820 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 30.771m (2373.94px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3821 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.135m (10.40px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3822 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.135m (10.40px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3823 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3824 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3825 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3826 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3827 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3828 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3829 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3830 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3831 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.135m (10.40px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3832 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.135m (10.40px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3833 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3834 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3835 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3836 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3837 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3838 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3839 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3840 | 회전 | init=극근접(80N) 범위500m z17 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3841 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.046m (3.53px@z21) | want=<0.1m | PASS
시뮬 SIM-R3842 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.046m (3.51px@z21) | want=<0.1m | PASS
시뮬 SIM-R3843 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.002m (0.13px@z21) | want=<0.1m | PASS
시뮬 SIM-R3844 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3845 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.002m (0.13px@z21) | want=<0.1m | PASS
시뮬 SIM-R3846 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3847 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.002m (0.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R3848 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.003m (0.20px@z21) | want=<0.1m | PASS
시뮬 SIM-R3849 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 30.773m (2374.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3850 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 30.771m (2373.94px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3851 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.063m (4.85px@z21) | want=<0.1m | PASS
시뮬 SIM-R3852 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.063m (4.90px@z21) | want=<0.1m | PASS
시뮬 SIM-R3853 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.002m (0.13px@z21) | want=<0.1m | PASS
시뮬 SIM-R3854 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3855 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.001m (0.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R3856 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.000m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R3857 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.002m (0.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R3858 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.003m (0.22px@z21) | want=<0.1m | PASS
시뮬 SIM-R3859 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 30.773m (2374.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3860 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 30.771m (2373.94px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3861 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.065m (5.01px@z21) | want=<0.1m | PASS
시뮬 SIM-R3862 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.060m (4.61px@z21) | want=<0.1m | PASS
시뮬 SIM-R3863 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.008m (0.64px@z21) | want=<0.1m | PASS
시뮬 SIM-R3864 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3865 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.008m (0.61px@z21) | want=<0.1m | PASS
시뮬 SIM-R3866 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3867 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.011m (0.83px@z21) | want=<0.1m | PASS
시뮬 SIM-R3868 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.015m (1.18px@z21) | want=<0.1m | PASS
시뮬 SIM-R3869 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 153.677m (11855.89px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3870 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 153.669m (11855.27px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3871 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 0.043m (3.29px@z21) | want=<0.1m | PASS
시뮬 SIM-R3872 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.038m (2.95px@z21) | want=<0.1m | PASS
시뮬 SIM-R3873 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 0.025m (1.93px@z21) | want=<0.1m | PASS
시뮬 SIM-R3874 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3875 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 0.025m (1.91px@z21) | want=<0.1m | PASS
시뮬 SIM-R3876 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3877 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 0.037m (2.88px@z21) | want=<0.1m | PASS
시뮬 SIM-R3878 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 0.046m (3.52px@z21) | want=<0.1m | PASS
시뮬 SIM-R3879 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 456.360m (35207.34px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3880 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 456.338m (35205.66px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3881 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 0.117m (9.02px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3882 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.055m (4.25px@z21) | want=<0.1m | PASS
시뮬 SIM-R3883 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 0.073m (5.65px@z21) | want=<0.1m | PASS
시뮬 SIM-R3884 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3885 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 0.073m (5.66px@z21) | want=<0.1m | PASS
시뮬 SIM-R3886 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3887 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 0.145m (11.16px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3888 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 0.120m (9.24px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3889 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 1246.777m (96186.64px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3890 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 1246.758m (96185.14px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3891 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 0.149m (11.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3892 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.040m (3.12px@z21) | want=<0.1m | PASS
시뮬 SIM-R3893 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 0.135m (10.43px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3894 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3895 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 0.135m (10.43px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3896 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3897 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 0.257m (19.86px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3898 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 0.143m (11.06px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3899 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 1763.152m (136024.03px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3900 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 1763.259m (136032.27px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3901 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 0.192m (14.82px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3902 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.045m (3.45px@z21) | want=<0.1m | PASS
시뮬 SIM-R3903 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 0.191m (14.76px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3904 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3905 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 0.191m (14.76px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3906 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3907 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 0.220m (17.00px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3908 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 0.078m (5.99px@z21) | want=<0.1m | PASS
시뮬 SIM-R3909 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 0.235m (18.15px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3910 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 0.103m (7.95px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3911 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 0.198m (15.25px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3912 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.042m (3.27px@z21) | want=<0.1m | PASS
시뮬 SIM-R3913 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 0.191m (14.76px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3914 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3915 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 0.191m (14.75px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3916 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3917 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 0.221m (17.03px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3918 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 0.077m (5.97px@z21) | want=<0.1m | PASS
시뮬 SIM-R3919 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 0.220m (17.01px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3920 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 0.077m (5.98px@z21) | want=<0.1m | PASS
시뮬 SIM-R3921 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 0.112m (8.67px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3922 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.034m (2.60px@z21) | want=<0.1m | PASS
시뮬 SIM-R3923 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 0.135m (10.44px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3924 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3925 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 0.135m (10.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3926 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3927 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 0.134m (10.36px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3928 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 0.135m (10.41px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3929 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 1763.125m (136021.97px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3930 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 1763.208m (136028.39px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3931 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.063m (4.85px@z21) | want=<0.1m | PASS
시뮬 SIM-R3932 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.063m (4.90px@z21) | want=<0.1m | PASS
시뮬 SIM-R3933 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.002m (0.13px@z21) | want=<0.1m | PASS
시뮬 SIM-R3934 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3935 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.001m (0.11px@z21) | want=<0.1m | PASS
시뮬 SIM-R3936 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.000m (0.03px@z21) | want=<0.1m | PASS
시뮬 SIM-R3937 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.002m (0.14px@z21) | want=<0.1m | PASS
시뮬 SIM-R3938 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.003m (0.22px@z21) | want=<0.1m | PASS
시뮬 SIM-R3939 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 30.773m (2374.07px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3940 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 30.771m (2373.94px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3941 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.032m (2.44px@z21) | want=<0.1m | PASS
시뮬 SIM-R3942 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.032m (2.44px@z21) | want=<0.1m | PASS
시뮬 SIM-R3943 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3944 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3945 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3946 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3947 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3948 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3949 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3950 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3951 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.032m (2.44px@z21) | want=<0.1m | PASS
시뮬 SIM-R3952 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.032m (2.44px@z21) | want=<0.1m | PASS
시뮬 SIM-R3953 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3954 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3955 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3956 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3957 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3958 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3959 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3960 | 회전 | init=극근접(80N) 범위500m z19 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3961 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.794m (61.28px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3962 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.664m (51.19px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3963 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.167m (12.89px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3964 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3965 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.167m (12.86px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3966 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3967 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.122m (9.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3968 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.172m (13.25px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3969 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 307.396m (23715.09px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3970 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 307.231m (23702.33px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3971 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.634m (48.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3972 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.576m (44.44px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3973 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.167m (12.89px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3974 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3975 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.167m (12.85px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3976 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3977 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.124m (9.53px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3978 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.170m (13.14px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3979 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 307.397m (23715.13px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3980 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 307.231m (23702.30px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3981 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.778m (60.06px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3982 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.716m (55.21px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3983 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.835m (64.43px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3984 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3985 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.835m (64.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3986 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3987 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.599m (46.18px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3988 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.866m (66.78px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3989 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 1535.100m (118430.27px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3990 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 1534.291m (118367.85px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R3991 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 2.603m (200.83px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3992 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.596m (45.97px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R3993 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 2.499m (192.77px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R3994 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R3995 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 2.499m (192.76px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3996 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R3997 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 1.903m (146.80px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R3998 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 2.607m (201.16px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R3999 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 4558.533m (351682.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4000 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 4556.356m (351514.74px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4001 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 7.323m (564.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4002 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.634m (48.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4003 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 7.321m (564.78px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4004 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4005 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 7.321m (564.78px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4006 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4007 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 9.311m (718.35px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4008 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 6.598m (509.01px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4009 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 12452.074m (960655.38px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4010 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 12450.110m (960503.85px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4011 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 13.418m (1035.19px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4012 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.584m (45.06px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4013 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 13.515m (1042.64px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4014 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4015 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 13.515m (1042.67px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4016 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4017 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 18.014m (1389.73px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4018 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 7.359m (567.74px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4019 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 17604.158m (1358129.55px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4020 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 17614.817m (1358951.83px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4021 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 18.508m (1427.86px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4022 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.616m (47.55px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4023 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 19.117m (1474.88px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4024 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4025 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 19.117m (1474.88px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4026 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4027 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 22.054m (1701.41px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4028 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 7.737m (596.86px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4029 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 22.187m (1711.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4030 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 7.993m (616.61px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4031 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 18.572m (1432.79px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4032 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.680m (52.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4033 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 19.117m (1474.88px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4034 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4035 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 19.118m (1474.88px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4036 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4037 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 22.054m (1701.40px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4038 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 7.736m (596.84px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4039 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 22.054m (1701.39px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4040 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 7.736m (596.85px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4041 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 13.153m (1014.76px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4042 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.803m (61.95px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4043 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 13.544m (1044.88px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4044 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4045 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 13.544m (1044.87px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4046 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4047 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 19.025m (1467.75px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4048 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 6.726m (518.86px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4049 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 17601.482m (1357923.04px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4050 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 17609.821m (1358566.42px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4051 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.634m (48.91px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4052 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.576m (44.44px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4053 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.167m (12.89px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4054 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4055 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.167m (12.85px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4056 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4057 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.124m (9.53px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4058 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.170m (13.14px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4059 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 307.397m (23715.13px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4060 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 307.231m (23702.30px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4061 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.434m (33.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4062 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.434m (33.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4063 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4064 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4065 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4066 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4067 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4068 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4069 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4070 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4071 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.434m (33.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4072 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.434m (33.47px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4073 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4074 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4075 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4076 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4077 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4078 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4079 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4080 | 회전 | init=극근접(80N) 범위5000m z15 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4081 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.197m (15.24px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4082 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.186m (14.37px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4083 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.167m (12.89px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4084 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4085 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.167m (12.86px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4086 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4087 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.122m (9.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4088 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.172m (13.25px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4089 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 307.396m (23715.09px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4090 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 307.231m (23702.33px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4091 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.354m (27.30px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4092 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.194m (14.98px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4093 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.167m (12.89px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4094 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4095 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.167m (12.85px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4096 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4097 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.124m (9.53px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4098 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.170m (13.14px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4099 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 307.397m (23715.13px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4100 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 307.231m (23702.30px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4101 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.816m (62.94px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4102 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.179m (13.84px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4103 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.835m (64.43px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4104 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4105 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.835m (64.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4106 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4107 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.599m (46.18px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4108 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.866m (66.78px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4109 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 1535.100m (118430.27px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4110 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 1534.291m (118367.85px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4111 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 2.453m (189.27px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4112 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.194m (14.95px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4113 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 2.499m (192.77px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4114 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4115 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 2.499m (192.76px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4116 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4117 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 1.903m (146.80px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4118 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 2.607m (201.16px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4119 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 4558.533m (351682.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4120 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 4556.356m (351514.74px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4121 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 7.215m (556.62px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4122 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.156m (12.00px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4123 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 7.321m (564.78px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4124 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4125 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 7.321m (564.78px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4126 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4127 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 9.311m (718.35px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4128 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 6.598m (509.01px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4129 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 12452.074m (960655.38px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4130 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 12450.110m (960503.85px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4131 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 13.418m (1035.19px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4132 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.160m (12.37px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4133 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 13.515m (1042.64px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4134 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4135 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 13.515m (1042.67px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4136 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4137 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 18.014m (1389.73px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4138 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 7.359m (567.74px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4139 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 17604.158m (1358129.55px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4140 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 17614.817m (1358951.83px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4141 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 18.961m (1462.80px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4142 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.198m (15.25px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4143 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 19.117m (1474.88px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4144 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4145 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 19.117m (1474.88px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4146 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4147 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 22.054m (1701.41px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4148 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 7.737m (596.86px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4149 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 22.187m (1711.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4150 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 7.993m (616.61px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4151 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 19.025m (1467.76px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4152 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.173m (13.32px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4153 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 19.117m (1474.88px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4154 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4155 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 19.118m (1474.88px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4156 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4157 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 22.054m (1701.40px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4158 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 7.736m (596.84px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4159 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 22.054m (1701.39px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4160 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 7.736m (596.85px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4161 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 13.584m (1047.95px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4162 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.140m (10.81px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4163 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 13.544m (1044.88px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4164 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4165 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 13.544m (1044.87px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4166 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4167 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 19.025m (1467.75px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4168 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 6.726m (518.86px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4169 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 17601.482m (1357923.04px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4170 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 17609.821m (1358566.42px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4171 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.354m (27.30px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4172 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.194m (14.98px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4173 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.167m (12.89px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4174 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4175 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.167m (12.85px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4176 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4177 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.124m (9.53px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4178 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.170m (13.14px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4179 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 307.397m (23715.13px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4180 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 307.231m (23702.30px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4181 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.125m (9.63px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4182 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.125m (9.63px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4183 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4184 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4185 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4186 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4187 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4188 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4189 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4190 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4191 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.125m (9.63px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4192 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.125m (9.63px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4193 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4194 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4195 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4196 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4197 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4198 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4199 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4200 | 회전 | init=극근접(80N) 범위5000m z17 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4201 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 1° [M1 vs GT-GEO] → got=오차 0.143m (11.06px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4202 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 1° [M1 vs GT-SCREEN] → got=오차 0.048m (3.71px@z21) | want=<0.1m | PASS
시뮬 SIM-R4203 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 1° [M1sub vs GT-GEO] → got=오차 0.167m (12.89px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4204 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4205 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 1° [M2 vs GT-GEO] → got=오차 0.167m (12.86px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4206 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 1° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4207 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 1° [M2E vs GT-GEO] → got=오차 0.122m (9.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4208 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 1° [M2E vs GT-SCREEN] → got=오차 0.172m (13.25px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4209 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 1° [M3 vs GT-GEO] → got=오차 307.396m (23715.09px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4210 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 1° [M3 vs GT-SCREEN] → got=오차 307.231m (23702.33px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4211 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1 vs GT-GEO] → got=오차 0.170m (13.14px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4212 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1 vs GT-SCREEN] → got=오차 0.040m (3.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R4213 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-GEO] → got=오차 0.167m (12.89px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4214 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 -1° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4215 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2 vs GT-GEO] → got=오차 0.167m (12.85px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4216 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4217 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2E vs GT-GEO] → got=오차 0.124m (9.53px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4218 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 -1° [M2E vs GT-SCREEN] → got=오차 0.170m (13.14px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4219 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 -1° [M3 vs GT-GEO] → got=오차 307.397m (23715.13px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4220 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 -1° [M3 vs GT-SCREEN] → got=오차 307.231m (23702.30px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4221 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 5° [M1 vs GT-GEO] → got=오차 0.834m (64.32px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4222 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 5° [M1 vs GT-SCREEN] → got=오차 0.049m (3.80px@z21) | want=<0.1m | PASS
시뮬 SIM-R4223 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 5° [M1sub vs GT-GEO] → got=오차 0.835m (64.43px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4224 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 5° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4225 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 5° [M2 vs GT-GEO] → got=오차 0.835m (64.44px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4226 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 5° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4227 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 5° [M2E vs GT-GEO] → got=오차 0.599m (46.18px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4228 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 5° [M2E vs GT-SCREEN] → got=오차 0.866m (66.78px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4229 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 5° [M3 vs GT-GEO] → got=오차 1535.100m (118430.27px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4230 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 5° [M3 vs GT-SCREEN] → got=오차 1534.291m (118367.85px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4231 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 15° [M1 vs GT-GEO] → got=오차 2.453m (189.27px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4232 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 15° [M1 vs GT-SCREEN] → got=오차 0.051m (3.92px@z21) | want=<0.1m | PASS
시뮬 SIM-R4233 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 15° [M1sub vs GT-GEO] → got=오차 2.499m (192.77px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4234 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 15° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4235 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 15° [M2 vs GT-GEO] → got=오차 2.499m (192.76px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4236 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 15° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4237 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 15° [M2E vs GT-GEO] → got=오차 1.903m (146.80px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4238 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 15° [M2E vs GT-SCREEN] → got=오차 2.607m (201.16px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4239 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 15° [M3 vs GT-GEO] → got=오차 4558.533m (351682.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4240 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 15° [M3 vs GT-SCREEN] → got=오차 4556.356m (351514.74px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4241 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 45° [M1 vs GT-GEO] → got=오차 7.273m (561.12px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4242 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 45° [M1 vs GT-SCREEN] → got=오차 0.048m (3.73px@z21) | want=<0.1m | PASS
시뮬 SIM-R4243 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 45° [M1sub vs GT-GEO] → got=오차 7.321m (564.78px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4244 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 45° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4245 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 45° [M2 vs GT-GEO] → got=오차 7.321m (564.78px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4246 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 45° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4247 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 45° [M2E vs GT-GEO] → got=오차 9.311m (718.35px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4248 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 45° [M2E vs GT-SCREEN] → got=오차 6.598m (509.01px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4249 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 45° [M3 vs GT-GEO] → got=오차 12452.074m (960655.38px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4250 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 45° [M3 vs GT-SCREEN] → got=오차 12450.110m (960503.85px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4251 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 90° [M1 vs GT-GEO] → got=오차 13.463m (1038.63px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4252 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 90° [M1 vs GT-SCREEN] → got=오차 0.059m (4.55px@z21) | want=<0.1m | PASS
시뮬 SIM-R4253 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 90° [M1sub vs GT-GEO] → got=오차 13.515m (1042.64px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4254 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 90° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4255 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 90° [M2 vs GT-GEO] → got=오차 13.515m (1042.67px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4256 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 90° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4257 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 90° [M2E vs GT-GEO] → got=오차 18.014m (1389.73px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4258 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 90° [M2E vs GT-SCREEN] → got=오차 7.359m (567.74px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4259 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 90° [M3 vs GT-GEO] → got=오차 17604.158m (1358129.55px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4260 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 90° [M3 vs GT-SCREEN] → got=오차 17614.817m (1358951.83px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4261 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-GEO] → got=오차 19.111m (1474.38px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4262 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1 vs GT-SCREEN] → got=오차 0.047m (3.60px@z21) | want=<0.1m | PASS
시뮬 SIM-R4263 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-GEO] → got=오차 19.117m (1474.88px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4264 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4265 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-GEO] → got=오차 19.117m (1474.88px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4266 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2 vs GT-SCREEN] → got=오차 0.000m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4267 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-GEO] → got=오차 22.054m (1701.41px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4268 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M2E vs GT-SCREEN] → got=오차 7.737m (596.86px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4269 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-GEO] → got=오차 22.187m (1711.71px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4270 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 179.999° [M3 vs GT-SCREEN] → got=오차 7.993m (616.61px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4271 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 180° [M1 vs GT-GEO] → got=오차 19.112m (1474.44px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4272 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 180° [M1 vs GT-SCREEN] → got=오차 0.038m (2.95px@z21) | want=<0.1m | PASS
시뮬 SIM-R4273 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 180° [M1sub vs GT-GEO] → got=오차 19.117m (1474.88px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4274 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 180° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4275 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 180° [M2 vs GT-GEO] → got=오차 19.118m (1474.88px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4276 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 180° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4277 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 180° [M2E vs GT-GEO] → got=오차 22.054m (1701.40px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4278 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 180° [M2E vs GT-SCREEN] → got=오차 7.736m (596.84px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4279 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 180° [M3 vs GT-GEO] → got=오차 22.054m (1701.39px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4280 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 180° [M3 vs GT-SCREEN] → got=오차 7.736m (596.85px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4281 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 270° [M1 vs GT-GEO] → got=오차 13.590m (1048.48px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4282 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 270° [M1 vs GT-SCREEN] → got=오차 0.049m (3.80px@z21) | want=<0.1m | PASS
시뮬 SIM-R4283 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 270° [M1sub vs GT-GEO] → got=오차 13.544m (1044.88px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4284 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 270° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4285 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 270° [M2 vs GT-GEO] → got=오차 13.544m (1044.87px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4286 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 270° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4287 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 270° [M2E vs GT-GEO] → got=오차 19.025m (1467.75px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4288 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 270° [M2E vs GT-SCREEN] → got=오차 6.726m (518.86px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4289 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 270° [M3 vs GT-GEO] → got=오차 17601.482m (1357923.04px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4290 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 270° [M3 vs GT-SCREEN] → got=오차 17609.821m (1358566.42px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4291 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 359° [M1 vs GT-GEO] → got=오차 0.170m (13.14px@z21) | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-R4292 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 359° [M1 vs GT-SCREEN] → got=오차 0.040m (3.06px@z21) | want=<0.1m | PASS
시뮬 SIM-R4293 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 359° [M1sub vs GT-GEO] → got=오차 0.167m (12.89px@z21) | want=<0.1m | ISSUE-26: 서브픽셀 증분도 진리값과 이탈
시뮬 SIM-R4294 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 359° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4295 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 359° [M2 vs GT-GEO] → got=오차 0.167m (12.85px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4296 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 359° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4297 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 359° [M2E vs GT-GEO] → got=오차 0.124m (9.53px@z21) | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-R4298 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 359° [M2E vs GT-SCREEN] → got=오차 0.170m (13.14px@z21) | want=<0.1m | ISSUE-28: 지면공간 합성은 화면 강체 아님
시뮬 SIM-R4299 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 359° [M3 vs GT-GEO] → got=오차 307.397m (23715.13px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4300 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 359° [M3 vs GT-SCREEN] → got=오차 307.231m (23702.30px@z21) | want=<0.1m | ISSUE-2: 위경도 평면 회전 왜곡
시뮬 SIM-R4301 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 360° [M1 vs GT-GEO] → got=오차 0.033m (2.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R4302 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 360° [M1 vs GT-SCREEN] → got=오차 0.033m (2.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R4303 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 360° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4304 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 360° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4305 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 360° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4306 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 360° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4307 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 360° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4308 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 360° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4309 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 360° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4310 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 360° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4311 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 720° [M1 vs GT-GEO] → got=오차 0.033m (2.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R4312 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 720° [M1 vs GT-SCREEN] → got=오차 0.033m (2.57px@z21) | want=<0.1m | PASS
시뮬 SIM-R4313 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 720° [M1sub vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4314 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 720° [M1sub vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4315 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 720° [M2 vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4316 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 720° [M2 vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4317 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 720° [M2E vs GT-GEO] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4318 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 720° [M2E vs GT-SCREEN] → got=오차 0.001m (0.04px@z21) | want=<0.1m | PASS
시뮬 SIM-R4319 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 720° [M3 vs GT-GEO] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-R4320 | 회전 | init=극근접(80N) 범위5000m z19 8점 화면밖 op=회전 720° [M3 vs GT-SCREEN] → got=오차 0.000m (0.00px@z21) | want=<0.1m | PASS
시뮬 SIM-A4321 | 누적 | init=적도(0N) z15 op=1°×10스텝 [M1] → got=위치오차 20.662m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4322 | 누적 | init=적도(0N) z15 op=1°×10스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4323 | 누적 | init=적도(0N) z15 op=1°×10스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4324 | 누적 | init=적도(0N) z15 op=1°×10스텝 [M2E] → got=위치오차 0.508m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4325 | 누적 | init=적도(0N) z15 op=1°×90스텝 [M1] → got=위치오차 125.770m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4326 | 누적 | init=적도(0N) z15 op=1°×90스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4327 | 누적 | init=적도(0N) z15 op=1°×90스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4328 | 누적 | init=적도(0N) z15 op=1°×90스텝 [M2E] → got=위치오차 2.921m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4329 | 누적 | init=적도(0N) z15 op=1°×360스텝 [M1] → got=위치오차 347.189m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4330 | 누적 | init=적도(0N) z15 op=1°×360스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4331 | 누적 | init=적도(0N) z15 op=1°×360스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4332 | 누적 | init=적도(0N) z15 op=1°×360스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4333 | 누적 | init=적도(0N) z15 op=1°×720스텝 [M1] → got=위치오차 453.843m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4334 | 누적 | init=적도(0N) z15 op=1°×720스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4335 | 누적 | init=적도(0N) z15 op=1°×720스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4336 | 누적 | init=적도(0N) z15 op=1°×720스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4337 | 누적 | init=적도(0N) z17 op=1°×10스텝 [M1] → got=위치오차 5.603m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4338 | 누적 | init=적도(0N) z17 op=1°×10스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4339 | 누적 | init=적도(0N) z17 op=1°×10스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4340 | 누적 | init=적도(0N) z17 op=1°×10스텝 [M2E] → got=위치오차 0.508m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4341 | 누적 | init=적도(0N) z17 op=1°×90스텝 [M1] → got=위치오차 21.181m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4342 | 누적 | init=적도(0N) z17 op=1°×90스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4343 | 누적 | init=적도(0N) z17 op=1°×90스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4344 | 누적 | init=적도(0N) z17 op=1°×90스텝 [M2E] → got=위치오차 2.921m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4345 | 누적 | init=적도(0N) z17 op=1°×360스텝 [M1] → got=위치오차 84.864m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4346 | 누적 | init=적도(0N) z17 op=1°×360스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4347 | 누적 | init=적도(0N) z17 op=1°×360스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4348 | 누적 | init=적도(0N) z17 op=1°×360스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4349 | 누적 | init=적도(0N) z17 op=1°×720스텝 [M1] → got=위치오차 172.878m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4350 | 누적 | init=적도(0N) z17 op=1°×720스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4351 | 누적 | init=적도(0N) z17 op=1°×720스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4352 | 누적 | init=적도(0N) z17 op=1°×720스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4353 | 누적 | init=적도(0N) z19 op=1°×10스텝 [M1] → got=위치오차 0.946m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4354 | 누적 | init=적도(0N) z19 op=1°×10스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4355 | 누적 | init=적도(0N) z19 op=1°×10스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4356 | 누적 | init=적도(0N) z19 op=1°×10스텝 [M2E] → got=위치오차 0.508m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4357 | 누적 | init=적도(0N) z19 op=1°×90스텝 [M1] → got=위치오차 1.583m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4358 | 누적 | init=적도(0N) z19 op=1°×90스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4359 | 누적 | init=적도(0N) z19 op=1°×90스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4360 | 누적 | init=적도(0N) z19 op=1°×90스텝 [M2E] → got=위치오차 2.921m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4361 | 누적 | init=적도(0N) z19 op=1°×360스텝 [M1] → got=위치오차 6.381m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4362 | 누적 | init=적도(0N) z19 op=1°×360스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4363 | 누적 | init=적도(0N) z19 op=1°×360스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4364 | 누적 | init=적도(0N) z19 op=1°×360스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4365 | 누적 | init=적도(0N) z19 op=1°×720스텝 [M1] → got=위치오차 12.653m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4366 | 누적 | init=적도(0N) z19 op=1°×720스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4367 | 누적 | init=적도(0N) z19 op=1°×720스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4368 | 누적 | init=적도(0N) z19 op=1°×720스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4369 | 누적 | init=안양(37.4N) z15 op=1°×10스텝 [M1] → got=위치오차 17.265m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4370 | 누적 | init=안양(37.4N) z15 op=1°×10스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4371 | 누적 | init=안양(37.4N) z15 op=1°×10스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4372 | 누적 | init=안양(37.4N) z15 op=1°×10스텝 [M2E] → got=위치오차 0.320m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4373 | 누적 | init=안양(37.4N) z15 op=1°×90스텝 [M1] → got=위치오차 100.271m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4374 | 누적 | init=안양(37.4N) z15 op=1°×90스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4375 | 누적 | init=안양(37.4N) z15 op=1°×90스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4376 | 누적 | init=안양(37.4N) z15 op=1°×90스텝 [M2E] → got=위치오차 1.866m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4377 | 누적 | init=안양(37.4N) z15 op=1°×360스텝 [M1] → got=위치오차 317.466m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4378 | 누적 | init=안양(37.4N) z15 op=1°×360스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4379 | 누적 | init=안양(37.4N) z15 op=1°×360스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4380 | 누적 | init=안양(37.4N) z15 op=1°×360스텝 [M2E] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4381 | 누적 | init=안양(37.4N) z15 op=1°×720스텝 [M1] → got=위치오차 522.990m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4382 | 누적 | init=안양(37.4N) z15 op=1°×720스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4383 | 누적 | init=안양(37.4N) z15 op=1°×720스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4384 | 누적 | init=안양(37.4N) z15 op=1°×720스텝 [M2E] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4385 | 누적 | init=안양(37.4N) z17 op=1°×10스텝 [M1] → got=위치오차 2.353m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4386 | 누적 | init=안양(37.4N) z17 op=1°×10스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4387 | 누적 | init=안양(37.4N) z17 op=1°×10스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4388 | 누적 | init=안양(37.4N) z17 op=1°×10스텝 [M2E] → got=위치오차 0.320m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4389 | 누적 | init=안양(37.4N) z17 op=1°×90스텝 [M1] → got=위치오차 5.041m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4390 | 누적 | init=안양(37.4N) z17 op=1°×90스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4391 | 누적 | init=안양(37.4N) z17 op=1°×90스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4392 | 누적 | init=안양(37.4N) z17 op=1°×90스텝 [M2E] → got=위치오차 1.866m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4393 | 누적 | init=안양(37.4N) z17 op=1°×360스텝 [M1] → got=위치오차 19.265m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4394 | 누적 | init=안양(37.4N) z17 op=1°×360스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4395 | 누적 | init=안양(37.4N) z17 op=1°×360스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4396 | 누적 | init=안양(37.4N) z17 op=1°×360스텝 [M2E] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4397 | 누적 | init=안양(37.4N) z17 op=1°×720스텝 [M1] → got=위치오차 37.630m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4398 | 누적 | init=안양(37.4N) z17 op=1°×720스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4399 | 누적 | init=안양(37.4N) z17 op=1°×720스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4400 | 누적 | init=안양(37.4N) z17 op=1°×720스텝 [M2E] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4401 | 누적 | init=안양(37.4N) z19 op=1°×10스텝 [M1] → got=위치오차 0.608m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4402 | 누적 | init=안양(37.4N) z19 op=1°×10스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4403 | 누적 | init=안양(37.4N) z19 op=1°×10스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4404 | 누적 | init=안양(37.4N) z19 op=1°×10스텝 [M2E] → got=위치오차 0.320m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4405 | 누적 | init=안양(37.4N) z19 op=1°×90스텝 [M1] → got=위치오차 1.668m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4406 | 누적 | init=안양(37.4N) z19 op=1°×90스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4407 | 누적 | init=안양(37.4N) z19 op=1°×90스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4408 | 누적 | init=안양(37.4N) z19 op=1°×90스텝 [M2E] → got=위치오차 1.866m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4409 | 누적 | init=안양(37.4N) z19 op=1°×360스텝 [M1] → got=위치오차 6.541m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4410 | 누적 | init=안양(37.4N) z19 op=1°×360스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4411 | 누적 | init=안양(37.4N) z19 op=1°×360스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4412 | 누적 | init=안양(37.4N) z19 op=1°×360스텝 [M2E] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4413 | 누적 | init=안양(37.4N) z19 op=1°×720스텝 [M1] → got=위치오차 11.999m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4414 | 누적 | init=안양(37.4N) z19 op=1°×720스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4415 | 누적 | init=안양(37.4N) z19 op=1°×720스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4416 | 누적 | init=안양(37.4N) z19 op=1°×720스텝 [M2E] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4417 | 누적 | init=고위도(60N) z15 op=1°×10스텝 [M1] → got=위치오차 10.205m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4418 | 누적 | init=고위도(60N) z15 op=1°×10스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4419 | 누적 | init=고위도(60N) z15 op=1°×10스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4420 | 누적 | init=고위도(60N) z15 op=1°×10스텝 [M2E] → got=위치오차 0.127m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4421 | 누적 | init=고위도(60N) z15 op=1°×90스텝 [M1] → got=위치오차 33.719m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4422 | 누적 | init=고위도(60N) z15 op=1°×90스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4423 | 누적 | init=고위도(60N) z15 op=1°×90스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4424 | 누적 | init=고위도(60N) z15 op=1°×90스텝 [M2E] → got=위치오차 0.777m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4425 | 누적 | init=고위도(60N) z15 op=1°×360스텝 [M1] → got=위치오차 138.103m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4426 | 누적 | init=고위도(60N) z15 op=1°×360스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4427 | 누적 | init=고위도(60N) z15 op=1°×360스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4428 | 누적 | init=고위도(60N) z15 op=1°×360스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4429 | 누적 | init=고위도(60N) z15 op=1°×720스텝 [M1] → got=위치오차 251.384m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4430 | 누적 | init=고위도(60N) z15 op=1°×720스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4431 | 누적 | init=고위도(60N) z15 op=1°×720스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4432 | 누적 | init=고위도(60N) z15 op=1°×720스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4433 | 누적 | init=고위도(60N) z17 op=1°×10스텝 [M1] → got=위치오차 2.360m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4434 | 누적 | init=고위도(60N) z17 op=1°×10스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4435 | 누적 | init=고위도(60N) z17 op=1°×10스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4436 | 누적 | init=고위도(60N) z17 op=1°×10스텝 [M2E] → got=위치오차 0.127m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4437 | 누적 | init=고위도(60N) z17 op=1°×90스텝 [M1] → got=위치오차 5.139m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4438 | 누적 | init=고위도(60N) z17 op=1°×90스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4439 | 누적 | init=고위도(60N) z17 op=1°×90스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4440 | 누적 | init=고위도(60N) z17 op=1°×90스텝 [M2E] → got=위치오차 0.777m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4441 | 누적 | init=고위도(60N) z17 op=1°×360스텝 [M1] → got=위치오차 18.912m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4442 | 누적 | init=고위도(60N) z17 op=1°×360스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4443 | 누적 | init=고위도(60N) z17 op=1°×360스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4444 | 누적 | init=고위도(60N) z17 op=1°×360스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4445 | 누적 | init=고위도(60N) z17 op=1°×720스텝 [M1] → got=위치오차 38.065m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4446 | 누적 | init=고위도(60N) z17 op=1°×720스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4447 | 누적 | init=고위도(60N) z17 op=1°×720스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4448 | 누적 | init=고위도(60N) z17 op=1°×720스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4449 | 누적 | init=고위도(60N) z19 op=1°×10스텝 [M1] → got=위치오차 0.339m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4450 | 누적 | init=고위도(60N) z19 op=1°×10스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4451 | 누적 | init=고위도(60N) z19 op=1°×10스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4452 | 누적 | init=고위도(60N) z19 op=1°×10스텝 [M2E] → got=위치오차 0.127m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4453 | 누적 | init=고위도(60N) z19 op=1°×90스텝 [M1] → got=위치오차 0.706m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4454 | 누적 | init=고위도(60N) z19 op=1°×90스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4455 | 누적 | init=고위도(60N) z19 op=1°×90스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4456 | 누적 | init=고위도(60N) z19 op=1°×90스텝 [M2E] → got=위치오차 0.777m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4457 | 누적 | init=고위도(60N) z19 op=1°×360스텝 [M1] → got=위치오차 1.983m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4458 | 누적 | init=고위도(60N) z19 op=1°×360스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4459 | 누적 | init=고위도(60N) z19 op=1°×360스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4460 | 누적 | init=고위도(60N) z19 op=1°×360스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4461 | 누적 | init=고위도(60N) z19 op=1°×720스텝 [M1] → got=위치오차 3.429m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4462 | 누적 | init=고위도(60N) z19 op=1°×720스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4463 | 누적 | init=고위도(60N) z19 op=1°×720스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4464 | 누적 | init=고위도(60N) z19 op=1°×720스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4465 | 누적 | init=극근접(80N) z15 op=1°×10스텝 [M1] → got=위치오차 3.219m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4466 | 누적 | init=극근접(80N) z15 op=1°×10스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4467 | 누적 | init=극근접(80N) z15 op=1°×10스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4468 | 누적 | init=극근접(80N) z15 op=1°×10스텝 [M2E] → got=위치오차 0.015m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4469 | 누적 | init=극근접(80N) z15 op=1°×90스텝 [M1] → got=위치오차 9.036m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4470 | 누적 | init=극근접(80N) z15 op=1°×90스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4471 | 누적 | init=극근접(80N) z15 op=1°×90스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4472 | 누적 | init=극근접(80N) z15 op=1°×90스텝 [M2E] → got=위치오차 0.247m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4473 | 누적 | init=극근접(80N) z15 op=1°×360스텝 [M1] → got=위치오차 30.286m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4474 | 누적 | init=극근접(80N) z15 op=1°×360스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4475 | 누적 | init=극근접(80N) z15 op=1°×360스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4476 | 누적 | init=극근접(80N) z15 op=1°×360스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4477 | 누적 | init=극근접(80N) z15 op=1°×720스텝 [M1] → got=위치오차 57.206m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4478 | 누적 | init=극근접(80N) z15 op=1°×720스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4479 | 누적 | init=극근접(80N) z15 op=1°×720스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4480 | 누적 | init=극근접(80N) z15 op=1°×720스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4481 | 누적 | init=극근접(80N) z17 op=1°×10스텝 [M1] → got=위치오차 0.773m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4482 | 누적 | init=극근접(80N) z17 op=1°×10스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4483 | 누적 | init=극근접(80N) z17 op=1°×10스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4484 | 누적 | init=극근접(80N) z17 op=1°×10스텝 [M2E] → got=위치오차 0.015m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4485 | 누적 | init=극근접(80N) z17 op=1°×90스텝 [M1] → got=위치오차 0.998m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4486 | 누적 | init=극근접(80N) z17 op=1°×90스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4487 | 누적 | init=극근접(80N) z17 op=1°×90스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4488 | 누적 | init=극근접(80N) z17 op=1°×90스텝 [M2E] → got=위치오차 0.247m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4489 | 누적 | init=극근접(80N) z17 op=1°×360스텝 [M1] → got=위치오차 3.778m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4490 | 누적 | init=극근접(80N) z17 op=1°×360스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4491 | 누적 | init=극근접(80N) z17 op=1°×360스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4492 | 누적 | init=극근접(80N) z17 op=1°×360스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4493 | 누적 | init=극근접(80N) z17 op=1°×720스텝 [M1] → got=위치오차 7.119m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4494 | 누적 | init=극근접(80N) z17 op=1°×720스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4495 | 누적 | init=극근접(80N) z17 op=1°×720스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4496 | 누적 | init=극근접(80N) z17 op=1°×720스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4497 | 누적 | init=극근접(80N) z19 op=1°×10스텝 [M1] → got=위치오차 0.118m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4498 | 누적 | init=극근접(80N) z19 op=1°×10스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4499 | 누적 | init=극근접(80N) z19 op=1°×10스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4500 | 누적 | init=극근접(80N) z19 op=1°×10스텝 [M2E] → got=위치오차 0.015m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4501 | 누적 | init=극근접(80N) z19 op=1°×90스텝 [M1] → got=위치오차 0.168m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4502 | 누적 | init=극근접(80N) z19 op=1°×90스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4503 | 누적 | init=극근접(80N) z19 op=1°×90스텝 [M2] → got=위치오차 0.001m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4504 | 누적 | init=극근접(80N) z19 op=1°×90스텝 [M2E] → got=위치오차 0.247m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4505 | 누적 | init=극근접(80N) z19 op=1°×360스텝 [M1] → got=위치오차 0.971m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4506 | 누적 | init=극근접(80N) z19 op=1°×360스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4507 | 누적 | init=극근접(80N) z19 op=1°×360스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4508 | 누적 | init=극근접(80N) z19 op=1°×360스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4509 | 누적 | init=극근접(80N) z19 op=1°×720스텝 [M1] → got=위치오차 1.831m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | ISSUE-3: 증분 누적 드리프트(절단/양자화)
시뮬 SIM-A4510 | 누적 | init=극근접(80N) z19 op=1°×720스텝 [M1sub] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4511 | 누적 | init=극근접(80N) z19 op=1°×720스텝 [M2] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-A4512 | 누적 | init=극근접(80N) z19 op=1°×720스텝 [M2E] → got=위치오차 0.000m 방위오차 0.0000° | want=위치<0.1m 방위<0.005° | PASS
시뮬 SIM-S4513 | 스케일 | init=적도(0N) z15 op=×0.1 [M1 vs GT-GEO] → got=오차 2.883m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4514 | 스케일 | init=적도(0N) z15 op=×0.1 [M1 vs GT-SCREEN] → got=오차 2.883m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4515 | 스케일 | init=적도(0N) z15 op=×0.1 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4516 | 스케일 | init=적도(0N) z15 op=×0.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4517 | 스케일 | init=적도(0N) z15 op=×0.1 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4518 | 스케일 | init=적도(0N) z15 op=×0.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4519 | 스케일 | init=적도(0N) z15 op=×0.5 [M1 vs GT-GEO] → got=오차 2.570m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4520 | 스케일 | init=적도(0N) z15 op=×0.5 [M1 vs GT-SCREEN] → got=오차 2.570m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4521 | 스케일 | init=적도(0N) z15 op=×0.5 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4522 | 스케일 | init=적도(0N) z15 op=×0.5 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4523 | 스케일 | init=적도(0N) z15 op=×0.5 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4524 | 스케일 | init=적도(0N) z15 op=×0.5 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4525 | 스케일 | init=적도(0N) z15 op=×0.9 [M1 vs GT-GEO] → got=오차 3.516m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4526 | 스케일 | init=적도(0N) z15 op=×0.9 [M1 vs GT-SCREEN] → got=오차 3.516m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4527 | 스케일 | init=적도(0N) z15 op=×0.9 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4528 | 스케일 | init=적도(0N) z15 op=×0.9 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4529 | 스케일 | init=적도(0N) z15 op=×0.9 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4530 | 스케일 | init=적도(0N) z15 op=×0.9 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4531 | 스케일 | init=적도(0N) z15 op=×1.1 [M1 vs GT-GEO] → got=오차 4.746m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4532 | 스케일 | init=적도(0N) z15 op=×1.1 [M1 vs GT-SCREEN] → got=오차 4.746m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4533 | 스케일 | init=적도(0N) z15 op=×1.1 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4534 | 스케일 | init=적도(0N) z15 op=×1.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4535 | 스케일 | init=적도(0N) z15 op=×1.1 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4536 | 스케일 | init=적도(0N) z15 op=×1.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4537 | 스케일 | init=적도(0N) z15 op=×1.5 [M1 vs GT-GEO] → got=오차 5.359m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4538 | 스케일 | init=적도(0N) z15 op=×1.5 [M1 vs GT-SCREEN] → got=오차 5.359m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4539 | 스케일 | init=적도(0N) z15 op=×1.5 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4540 | 스케일 | init=적도(0N) z15 op=×1.5 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4541 | 스케일 | init=적도(0N) z15 op=×1.5 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4542 | 스케일 | init=적도(0N) z15 op=×1.5 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4543 | 스케일 | init=적도(0N) z15 op=×2 [M1 vs GT-GEO] → got=오차 5.732m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4544 | 스케일 | init=적도(0N) z15 op=×2 [M1 vs GT-SCREEN] → got=오차 5.732m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4545 | 스케일 | init=적도(0N) z15 op=×2 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4546 | 스케일 | init=적도(0N) z15 op=×2 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4547 | 스케일 | init=적도(0N) z15 op=×2 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4548 | 스케일 | init=적도(0N) z15 op=×2 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4549 | 스케일 | init=적도(0N) z15 op=×4 [M1 vs GT-GEO] → got=오차 8.832m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4550 | 스케일 | init=적도(0N) z15 op=×4 [M1 vs GT-SCREEN] → got=오차 8.832m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4551 | 스케일 | init=적도(0N) z15 op=×4 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4552 | 스케일 | init=적도(0N) z15 op=×4 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4553 | 스케일 | init=적도(0N) z15 op=×4 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4554 | 스케일 | init=적도(0N) z15 op=×4 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4555 | 스케일 | init=적도(0N) z17 op=×0.1 [M1 vs GT-GEO] → got=오차 0.635m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4556 | 스케일 | init=적도(0N) z17 op=×0.1 [M1 vs GT-SCREEN] → got=오차 0.635m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4557 | 스케일 | init=적도(0N) z17 op=×0.1 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4558 | 스케일 | init=적도(0N) z17 op=×0.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4559 | 스케일 | init=적도(0N) z17 op=×0.1 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4560 | 스케일 | init=적도(0N) z17 op=×0.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4561 | 스케일 | init=적도(0N) z17 op=×0.5 [M1 vs GT-GEO] → got=오차 0.761m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4562 | 스케일 | init=적도(0N) z17 op=×0.5 [M1 vs GT-SCREEN] → got=오차 0.761m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4563 | 스케일 | init=적도(0N) z17 op=×0.5 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4564 | 스케일 | init=적도(0N) z17 op=×0.5 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4565 | 스케일 | init=적도(0N) z17 op=×0.5 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4566 | 스케일 | init=적도(0N) z17 op=×0.5 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4567 | 스케일 | init=적도(0N) z17 op=×0.9 [M1 vs GT-GEO] → got=오차 1.070m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4568 | 스케일 | init=적도(0N) z17 op=×0.9 [M1 vs GT-SCREEN] → got=오차 1.070m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4569 | 스케일 | init=적도(0N) z17 op=×0.9 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4570 | 스케일 | init=적도(0N) z17 op=×0.9 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4571 | 스케일 | init=적도(0N) z17 op=×0.9 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4572 | 스케일 | init=적도(0N) z17 op=×0.9 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4573 | 스케일 | init=적도(0N) z17 op=×1.1 [M1 vs GT-GEO] → got=오차 1.149m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4574 | 스케일 | init=적도(0N) z17 op=×1.1 [M1 vs GT-SCREEN] → got=오차 1.149m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4575 | 스케일 | init=적도(0N) z17 op=×1.1 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4576 | 스케일 | init=적도(0N) z17 op=×1.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4577 | 스케일 | init=적도(0N) z17 op=×1.1 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4578 | 스케일 | init=적도(0N) z17 op=×1.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4579 | 스케일 | init=적도(0N) z17 op=×1.5 [M1 vs GT-GEO] → got=오차 1.700m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4580 | 스케일 | init=적도(0N) z17 op=×1.5 [M1 vs GT-SCREEN] → got=오차 1.700m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4581 | 스케일 | init=적도(0N) z17 op=×1.5 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4582 | 스케일 | init=적도(0N) z17 op=×1.5 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4583 | 스케일 | init=적도(0N) z17 op=×1.5 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4584 | 스케일 | init=적도(0N) z17 op=×1.5 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4585 | 스케일 | init=적도(0N) z17 op=×2 [M1 vs GT-GEO] → got=오차 1.518m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4586 | 스케일 | init=적도(0N) z17 op=×2 [M1 vs GT-SCREEN] → got=오차 1.518m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4587 | 스케일 | init=적도(0N) z17 op=×2 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4588 | 스케일 | init=적도(0N) z17 op=×2 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4589 | 스케일 | init=적도(0N) z17 op=×2 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4590 | 스케일 | init=적도(0N) z17 op=×2 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4591 | 스케일 | init=적도(0N) z17 op=×4 [M1 vs GT-GEO] → got=오차 3.098m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4592 | 스케일 | init=적도(0N) z17 op=×4 [M1 vs GT-SCREEN] → got=오차 3.098m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4593 | 스케일 | init=적도(0N) z17 op=×4 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4594 | 스케일 | init=적도(0N) z17 op=×4 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4595 | 스케일 | init=적도(0N) z17 op=×4 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4596 | 스케일 | init=적도(0N) z17 op=×4 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4597 | 스케일 | init=적도(0N) z19 op=×0.1 [M1 vs GT-GEO] → got=오차 0.140m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4598 | 스케일 | init=적도(0N) z19 op=×0.1 [M1 vs GT-SCREEN] → got=오차 0.140m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4599 | 스케일 | init=적도(0N) z19 op=×0.1 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4600 | 스케일 | init=적도(0N) z19 op=×0.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4601 | 스케일 | init=적도(0N) z19 op=×0.1 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4602 | 스케일 | init=적도(0N) z19 op=×0.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4603 | 스케일 | init=적도(0N) z19 op=×0.5 [M1 vs GT-GEO] → got=오차 0.252m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4604 | 스케일 | init=적도(0N) z19 op=×0.5 [M1 vs GT-SCREEN] → got=오차 0.252m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4605 | 스케일 | init=적도(0N) z19 op=×0.5 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4606 | 스케일 | init=적도(0N) z19 op=×0.5 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4607 | 스케일 | init=적도(0N) z19 op=×0.5 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4608 | 스케일 | init=적도(0N) z19 op=×0.5 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4609 | 스케일 | init=적도(0N) z19 op=×0.9 [M1 vs GT-GEO] → got=오차 0.215m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4610 | 스케일 | init=적도(0N) z19 op=×0.9 [M1 vs GT-SCREEN] → got=오차 0.215m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4611 | 스케일 | init=적도(0N) z19 op=×0.9 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4612 | 스케일 | init=적도(0N) z19 op=×0.9 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4613 | 스케일 | init=적도(0N) z19 op=×0.9 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4614 | 스케일 | init=적도(0N) z19 op=×0.9 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4615 | 스케일 | init=적도(0N) z19 op=×1.1 [M1 vs GT-GEO] → got=오차 0.222m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4616 | 스케일 | init=적도(0N) z19 op=×1.1 [M1 vs GT-SCREEN] → got=오차 0.222m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4617 | 스케일 | init=적도(0N) z19 op=×1.1 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4618 | 스케일 | init=적도(0N) z19 op=×1.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4619 | 스케일 | init=적도(0N) z19 op=×1.1 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4620 | 스케일 | init=적도(0N) z19 op=×1.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4621 | 스케일 | init=적도(0N) z19 op=×1.5 [M1 vs GT-GEO] → got=오차 0.395m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4622 | 스케일 | init=적도(0N) z19 op=×1.5 [M1 vs GT-SCREEN] → got=오차 0.395m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4623 | 스케일 | init=적도(0N) z19 op=×1.5 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4624 | 스케일 | init=적도(0N) z19 op=×1.5 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4625 | 스케일 | init=적도(0N) z19 op=×1.5 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4626 | 스케일 | init=적도(0N) z19 op=×1.5 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4627 | 스케일 | init=적도(0N) z19 op=×2 [M1 vs GT-GEO] → got=오차 0.456m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4628 | 스케일 | init=적도(0N) z19 op=×2 [M1 vs GT-SCREEN] → got=오차 0.456m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4629 | 스케일 | init=적도(0N) z19 op=×2 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4630 | 스케일 | init=적도(0N) z19 op=×2 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4631 | 스케일 | init=적도(0N) z19 op=×2 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4632 | 스케일 | init=적도(0N) z19 op=×2 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4633 | 스케일 | init=적도(0N) z19 op=×4 [M1 vs GT-GEO] → got=오차 0.792m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4634 | 스케일 | init=적도(0N) z19 op=×4 [M1 vs GT-SCREEN] → got=오차 0.792m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4635 | 스케일 | init=적도(0N) z19 op=×4 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4636 | 스케일 | init=적도(0N) z19 op=×4 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4637 | 스케일 | init=적도(0N) z19 op=×4 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4638 | 스케일 | init=적도(0N) z19 op=×4 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4639 | 스케일 | init=안양(37.4N) z15 op=×0.1 [M1 vs GT-GEO] → got=오차 2.531m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4640 | 스케일 | init=안양(37.4N) z15 op=×0.1 [M1 vs GT-SCREEN] → got=오차 2.531m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4641 | 스케일 | init=안양(37.4N) z15 op=×0.1 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4642 | 스케일 | init=안양(37.4N) z15 op=×0.1 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4643 | 스케일 | init=안양(37.4N) z15 op=×0.1 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4644 | 스케일 | init=안양(37.4N) z15 op=×0.1 [M2E vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4645 | 스케일 | init=안양(37.4N) z15 op=×0.5 [M1 vs GT-GEO] → got=오차 2.220m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4646 | 스케일 | init=안양(37.4N) z15 op=×0.5 [M1 vs GT-SCREEN] → got=오차 2.219m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4647 | 스케일 | init=안양(37.4N) z15 op=×0.5 [M2 vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4648 | 스케일 | init=안양(37.4N) z15 op=×0.5 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4649 | 스케일 | init=안양(37.4N) z15 op=×0.5 [M2E vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4650 | 스케일 | init=안양(37.4N) z15 op=×0.5 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4651 | 스케일 | init=안양(37.4N) z15 op=×0.9 [M1 vs GT-GEO] → got=오차 2.490m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4652 | 스케일 | init=안양(37.4N) z15 op=×0.9 [M1 vs GT-SCREEN] → got=오차 2.489m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4653 | 스케일 | init=안양(37.4N) z15 op=×0.9 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4654 | 스케일 | init=안양(37.4N) z15 op=×0.9 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4655 | 스케일 | init=안양(37.4N) z15 op=×0.9 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4656 | 스케일 | init=안양(37.4N) z15 op=×0.9 [M2E vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4657 | 스케일 | init=안양(37.4N) z15 op=×1.1 [M1 vs GT-GEO] → got=오차 3.838m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4658 | 스케일 | init=안양(37.4N) z15 op=×1.1 [M1 vs GT-SCREEN] → got=오차 3.838m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4659 | 스케일 | init=안양(37.4N) z15 op=×1.1 [M2 vs GT-GEO] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4660 | 스케일 | init=안양(37.4N) z15 op=×1.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4661 | 스케일 | init=안양(37.4N) z15 op=×1.1 [M2E vs GT-GEO] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4662 | 스케일 | init=안양(37.4N) z15 op=×1.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4663 | 스케일 | init=안양(37.4N) z15 op=×1.5 [M1 vs GT-GEO] → got=오차 3.810m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4664 | 스케일 | init=안양(37.4N) z15 op=×1.5 [M1 vs GT-SCREEN] → got=오차 3.817m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4665 | 스케일 | init=안양(37.4N) z15 op=×1.5 [M2 vs GT-GEO] → got=오차 0.008m | want=<0.1m | PASS
시뮬 SIM-S4666 | 스케일 | init=안양(37.4N) z15 op=×1.5 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4667 | 스케일 | init=안양(37.4N) z15 op=×1.5 [M2E vs GT-GEO] → got=오차 0.010m | want=<0.1m | PASS
시뮬 SIM-S4668 | 스케일 | init=안양(37.4N) z15 op=×1.5 [M2E vs GT-SCREEN] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4669 | 스케일 | init=안양(37.4N) z15 op=×2 [M1 vs GT-GEO] → got=오차 3.779m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4670 | 스케일 | init=안양(37.4N) z15 op=×2 [M1 vs GT-SCREEN] → got=오차 3.774m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4671 | 스케일 | init=안양(37.4N) z15 op=×2 [M2 vs GT-GEO] → got=오차 0.023m | want=<0.1m | PASS
시뮬 SIM-S4672 | 스케일 | init=안양(37.4N) z15 op=×2 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4673 | 스케일 | init=안양(37.4N) z15 op=×2 [M2E vs GT-GEO] → got=오차 0.026m | want=<0.1m | PASS
시뮬 SIM-S4674 | 스케일 | init=안양(37.4N) z15 op=×2 [M2E vs GT-SCREEN] → got=오차 0.006m | want=<0.1m | PASS
시뮬 SIM-S4675 | 스케일 | init=안양(37.4N) z15 op=×4 [M1 vs GT-GEO] → got=오차 7.300m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4676 | 스케일 | init=안양(37.4N) z15 op=×4 [M1 vs GT-SCREEN] → got=오차 7.434m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4677 | 스케일 | init=안양(37.4N) z15 op=×4 [M2 vs GT-GEO] → got=오차 0.136m | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-S4678 | 스케일 | init=안양(37.4N) z15 op=×4 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4679 | 스케일 | init=안양(37.4N) z15 op=×4 [M2E vs GT-GEO] → got=오차 0.156m | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-S4680 | 스케일 | init=안양(37.4N) z15 op=×4 [M2E vs GT-SCREEN] → got=오차 0.035m | want=<0.1m | PASS
시뮬 SIM-S4681 | 스케일 | init=안양(37.4N) z17 op=×0.1 [M1 vs GT-GEO] → got=오차 0.390m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4682 | 스케일 | init=안양(37.4N) z17 op=×0.1 [M1 vs GT-SCREEN] → got=오차 0.390m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4683 | 스케일 | init=안양(37.4N) z17 op=×0.1 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4684 | 스케일 | init=안양(37.4N) z17 op=×0.1 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4685 | 스케일 | init=안양(37.4N) z17 op=×0.1 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4686 | 스케일 | init=안양(37.4N) z17 op=×0.1 [M2E vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4687 | 스케일 | init=안양(37.4N) z17 op=×0.5 [M1 vs GT-GEO] → got=오차 0.630m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4688 | 스케일 | init=안양(37.4N) z17 op=×0.5 [M1 vs GT-SCREEN] → got=오차 0.630m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4689 | 스케일 | init=안양(37.4N) z17 op=×0.5 [M2 vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4690 | 스케일 | init=안양(37.4N) z17 op=×0.5 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4691 | 스케일 | init=안양(37.4N) z17 op=×0.5 [M2E vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4692 | 스케일 | init=안양(37.4N) z17 op=×0.5 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4693 | 스케일 | init=안양(37.4N) z17 op=×0.9 [M1 vs GT-GEO] → got=오차 0.889m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4694 | 스케일 | init=안양(37.4N) z17 op=×0.9 [M1 vs GT-SCREEN] → got=오차 0.889m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4695 | 스케일 | init=안양(37.4N) z17 op=×0.9 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4696 | 스케일 | init=안양(37.4N) z17 op=×0.9 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4697 | 스케일 | init=안양(37.4N) z17 op=×0.9 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4698 | 스케일 | init=안양(37.4N) z17 op=×0.9 [M2E vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4699 | 스케일 | init=안양(37.4N) z17 op=×1.1 [M1 vs GT-GEO] → got=오차 0.819m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4700 | 스케일 | init=안양(37.4N) z17 op=×1.1 [M1 vs GT-SCREEN] → got=오차 0.819m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4701 | 스케일 | init=안양(37.4N) z17 op=×1.1 [M2 vs GT-GEO] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4702 | 스케일 | init=안양(37.4N) z17 op=×1.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4703 | 스케일 | init=안양(37.4N) z17 op=×1.1 [M2E vs GT-GEO] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4704 | 스케일 | init=안양(37.4N) z17 op=×1.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4705 | 스케일 | init=안양(37.4N) z17 op=×1.5 [M1 vs GT-GEO] → got=오차 0.898m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4706 | 스케일 | init=안양(37.4N) z17 op=×1.5 [M1 vs GT-SCREEN] → got=오차 0.891m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4707 | 스케일 | init=안양(37.4N) z17 op=×1.5 [M2 vs GT-GEO] → got=오차 0.008m | want=<0.1m | PASS
시뮬 SIM-S4708 | 스케일 | init=안양(37.4N) z17 op=×1.5 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4709 | 스케일 | init=안양(37.4N) z17 op=×1.5 [M2E vs GT-GEO] → got=오차 0.010m | want=<0.1m | PASS
시뮬 SIM-S4710 | 스케일 | init=안양(37.4N) z17 op=×1.5 [M2E vs GT-SCREEN] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4711 | 스케일 | init=안양(37.4N) z17 op=×2 [M1 vs GT-GEO] → got=오차 1.342m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4712 | 스케일 | init=안양(37.4N) z17 op=×2 [M1 vs GT-SCREEN] → got=오차 1.323m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4713 | 스케일 | init=안양(37.4N) z17 op=×2 [M2 vs GT-GEO] → got=오차 0.023m | want=<0.1m | PASS
시뮬 SIM-S4714 | 스케일 | init=안양(37.4N) z17 op=×2 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4715 | 스케일 | init=안양(37.4N) z17 op=×2 [M2E vs GT-GEO] → got=오차 0.026m | want=<0.1m | PASS
시뮬 SIM-S4716 | 스케일 | init=안양(37.4N) z17 op=×2 [M2E vs GT-SCREEN] → got=오차 0.006m | want=<0.1m | PASS
시뮬 SIM-S4717 | 스케일 | init=안양(37.4N) z17 op=×4 [M1 vs GT-GEO] → got=오차 2.064m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4718 | 스케일 | init=안양(37.4N) z17 op=×4 [M1 vs GT-SCREEN] → got=오차 2.035m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4719 | 스케일 | init=안양(37.4N) z17 op=×4 [M2 vs GT-GEO] → got=오차 0.136m | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-S4720 | 스케일 | init=안양(37.4N) z17 op=×4 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4721 | 스케일 | init=안양(37.4N) z17 op=×4 [M2E vs GT-GEO] → got=오차 0.156m | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-S4722 | 스케일 | init=안양(37.4N) z17 op=×4 [M2E vs GT-SCREEN] → got=오차 0.035m | want=<0.1m | PASS
시뮬 SIM-S4723 | 스케일 | init=안양(37.4N) z19 op=×0.1 [M1 vs GT-GEO] → got=오차 0.150m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4724 | 스케일 | init=안양(37.4N) z19 op=×0.1 [M1 vs GT-SCREEN] → got=오차 0.151m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4725 | 스케일 | init=안양(37.4N) z19 op=×0.1 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4726 | 스케일 | init=안양(37.4N) z19 op=×0.1 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4727 | 스케일 | init=안양(37.4N) z19 op=×0.1 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4728 | 스케일 | init=안양(37.4N) z19 op=×0.1 [M2E vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4729 | 스케일 | init=안양(37.4N) z19 op=×0.5 [M1 vs GT-GEO] → got=오차 0.137m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4730 | 스케일 | init=안양(37.4N) z19 op=×0.5 [M1 vs GT-SCREEN] → got=오차 0.136m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4731 | 스케일 | init=안양(37.4N) z19 op=×0.5 [M2 vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4732 | 스케일 | init=안양(37.4N) z19 op=×0.5 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4733 | 스케일 | init=안양(37.4N) z19 op=×0.5 [M2E vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4734 | 스케일 | init=안양(37.4N) z19 op=×0.5 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4735 | 스케일 | init=안양(37.4N) z19 op=×0.9 [M1 vs GT-GEO] → got=오차 0.119m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4736 | 스케일 | init=안양(37.4N) z19 op=×0.9 [M1 vs GT-SCREEN] → got=오차 0.120m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4737 | 스케일 | init=안양(37.4N) z19 op=×0.9 [M2 vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4738 | 스케일 | init=안양(37.4N) z19 op=×0.9 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4739 | 스케일 | init=안양(37.4N) z19 op=×0.9 [M2E vs GT-GEO] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4740 | 스케일 | init=안양(37.4N) z19 op=×0.9 [M2E vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4741 | 스케일 | init=안양(37.4N) z19 op=×1.1 [M1 vs GT-GEO] → got=오차 0.291m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4742 | 스케일 | init=안양(37.4N) z19 op=×1.1 [M1 vs GT-SCREEN] → got=오차 0.291m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4743 | 스케일 | init=안양(37.4N) z19 op=×1.1 [M2 vs GT-GEO] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4744 | 스케일 | init=안양(37.4N) z19 op=×1.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4745 | 스케일 | init=안양(37.4N) z19 op=×1.1 [M2E vs GT-GEO] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4746 | 스케일 | init=안양(37.4N) z19 op=×1.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4747 | 스케일 | init=안양(37.4N) z19 op=×1.5 [M1 vs GT-GEO] → got=오차 0.272m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4748 | 스케일 | init=안양(37.4N) z19 op=×1.5 [M1 vs GT-SCREEN] → got=오차 0.272m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4749 | 스케일 | init=안양(37.4N) z19 op=×1.5 [M2 vs GT-GEO] → got=오차 0.008m | want=<0.1m | PASS
시뮬 SIM-S4750 | 스케일 | init=안양(37.4N) z19 op=×1.5 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4751 | 스케일 | init=안양(37.4N) z19 op=×1.5 [M2E vs GT-GEO] → got=오차 0.010m | want=<0.1m | PASS
시뮬 SIM-S4752 | 스케일 | init=안양(37.4N) z19 op=×1.5 [M2E vs GT-SCREEN] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4753 | 스케일 | init=안양(37.4N) z19 op=×2 [M1 vs GT-GEO] → got=오차 0.316m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4754 | 스케일 | init=안양(37.4N) z19 op=×2 [M1 vs GT-SCREEN] → got=오차 0.321m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4755 | 스케일 | init=안양(37.4N) z19 op=×2 [M2 vs GT-GEO] → got=오차 0.023m | want=<0.1m | PASS
시뮬 SIM-S4756 | 스케일 | init=안양(37.4N) z19 op=×2 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4757 | 스케일 | init=안양(37.4N) z19 op=×2 [M2E vs GT-GEO] → got=오차 0.026m | want=<0.1m | PASS
시뮬 SIM-S4758 | 스케일 | init=안양(37.4N) z19 op=×2 [M2E vs GT-SCREEN] → got=오차 0.006m | want=<0.1m | PASS
시뮬 SIM-S4759 | 스케일 | init=안양(37.4N) z19 op=×4 [M1 vs GT-GEO] → got=오차 0.646m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4760 | 스케일 | init=안양(37.4N) z19 op=×4 [M1 vs GT-SCREEN] → got=오차 0.668m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4761 | 스케일 | init=안양(37.4N) z19 op=×4 [M2 vs GT-GEO] → got=오차 0.136m | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-S4762 | 스케일 | init=안양(37.4N) z19 op=×4 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4763 | 스케일 | init=안양(37.4N) z19 op=×4 [M2E vs GT-GEO] → got=오차 0.156m | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-S4764 | 스케일 | init=안양(37.4N) z19 op=×4 [M2E vs GT-SCREEN] → got=오차 0.035m | want=<0.1m | PASS
시뮬 SIM-S4765 | 스케일 | init=고위도(60N) z15 op=×0.1 [M1 vs GT-GEO] → got=오차 1.358m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4766 | 스케일 | init=고위도(60N) z15 op=×0.1 [M1 vs GT-SCREEN] → got=오차 1.355m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4767 | 스케일 | init=고위도(60N) z15 op=×0.1 [M2 vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4768 | 스케일 | init=고위도(60N) z15 op=×0.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4769 | 스케일 | init=고위도(60N) z15 op=×0.1 [M2E vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4770 | 스케일 | init=고위도(60N) z15 op=×0.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4771 | 스케일 | init=고위도(60N) z15 op=×0.5 [M1 vs GT-GEO] → got=오차 1.387m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4772 | 스케일 | init=고위도(60N) z15 op=×0.5 [M1 vs GT-SCREEN] → got=오차 1.390m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4773 | 스케일 | init=고위도(60N) z15 op=×0.5 [M2 vs GT-GEO] → got=오차 0.007m | want=<0.1m | PASS
시뮬 SIM-S4774 | 스케일 | init=고위도(60N) z15 op=×0.5 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4775 | 스케일 | init=고위도(60N) z15 op=×0.5 [M2E vs GT-GEO] → got=오차 0.008m | want=<0.1m | PASS
시뮬 SIM-S4776 | 스케일 | init=고위도(60N) z15 op=×0.5 [M2E vs GT-SCREEN] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4777 | 스케일 | init=고위도(60N) z15 op=×0.9 [M1 vs GT-GEO] → got=오차 1.923m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4778 | 스케일 | init=고위도(60N) z15 op=×0.9 [M1 vs GT-SCREEN] → got=오차 1.925m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4779 | 스케일 | init=고위도(60N) z15 op=×0.9 [M2 vs GT-GEO] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4780 | 스케일 | init=고위도(60N) z15 op=×0.9 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4781 | 스케일 | init=고위도(60N) z15 op=×0.9 [M2E vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4782 | 스케일 | init=고위도(60N) z15 op=×0.9 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4783 | 스케일 | init=고위도(60N) z15 op=×1.1 [M1 vs GT-GEO] → got=오차 1.508m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4784 | 스케일 | init=고위도(60N) z15 op=×1.1 [M1 vs GT-SCREEN] → got=오차 1.507m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4785 | 스케일 | init=고위도(60N) z15 op=×1.1 [M2 vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4786 | 스케일 | init=고위도(60N) z15 op=×1.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4787 | 스케일 | init=고위도(60N) z15 op=×1.1 [M2E vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4788 | 스케일 | init=고위도(60N) z15 op=×1.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4789 | 스케일 | init=고위도(60N) z15 op=×1.5 [M1 vs GT-GEO] → got=오차 2.749m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4790 | 스케일 | init=고위도(60N) z15 op=×1.5 [M1 vs GT-SCREEN] → got=오차 2.730m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4791 | 스케일 | init=고위도(60N) z15 op=×1.5 [M2 vs GT-GEO] → got=오차 0.019m | want=<0.1m | PASS
시뮬 SIM-S4792 | 스케일 | init=고위도(60N) z15 op=×1.5 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4793 | 스케일 | init=고위도(60N) z15 op=×1.5 [M2E vs GT-GEO] → got=오차 0.022m | want=<0.1m | PASS
시뮬 SIM-S4794 | 스케일 | init=고위도(60N) z15 op=×1.5 [M2E vs GT-SCREEN] → got=오차 0.005m | want=<0.1m | PASS
시뮬 SIM-S4795 | 스케일 | init=고위도(60N) z15 op=×2 [M1 vs GT-GEO] → got=오차 3.604m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4796 | 스케일 | init=고위도(60N) z15 op=×2 [M1 vs GT-SCREEN] → got=오차 3.610m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4797 | 스케일 | init=고위도(60N) z15 op=×2 [M2 vs GT-GEO] → got=오차 0.051m | want=<0.1m | PASS
시뮬 SIM-S4798 | 스케일 | init=고위도(60N) z15 op=×2 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4799 | 스케일 | init=고위도(60N) z15 op=×2 [M2E vs GT-GEO] → got=오차 0.059m | want=<0.1m | PASS
시뮬 SIM-S4800 | 스케일 | init=고위도(60N) z15 op=×2 [M2E vs GT-SCREEN] → got=오차 0.013m | want=<0.1m | PASS
시뮬 SIM-S4801 | 스케일 | init=고위도(60N) z15 op=×4 [M1 vs GT-GEO] → got=오차 5.514m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4802 | 스케일 | init=고위도(60N) z15 op=×4 [M1 vs GT-SCREEN] → got=오차 5.208m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4803 | 스케일 | init=고위도(60N) z15 op=×4 [M2 vs GT-GEO] → got=오차 0.307m | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-S4804 | 스케일 | init=고위도(60N) z15 op=×4 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4805 | 스케일 | init=고위도(60N) z15 op=×4 [M2E vs GT-GEO] → got=오차 0.352m | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-S4806 | 스케일 | init=고위도(60N) z15 op=×4 [M2E vs GT-SCREEN] → got=오차 0.078m | want=<0.1m | PASS
시뮬 SIM-S4807 | 스케일 | init=고위도(60N) z17 op=×0.1 [M1 vs GT-GEO] → got=오차 0.419m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4808 | 스케일 | init=고위도(60N) z17 op=×0.1 [M1 vs GT-SCREEN] → got=오차 0.418m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4809 | 스케일 | init=고위도(60N) z17 op=×0.1 [M2 vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4810 | 스케일 | init=고위도(60N) z17 op=×0.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4811 | 스케일 | init=고위도(60N) z17 op=×0.1 [M2E vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4812 | 스케일 | init=고위도(60N) z17 op=×0.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4813 | 스케일 | init=고위도(60N) z17 op=×0.5 [M1 vs GT-GEO] → got=오차 0.419m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4814 | 스케일 | init=고위도(60N) z17 op=×0.5 [M1 vs GT-SCREEN] → got=오차 0.413m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4815 | 스케일 | init=고위도(60N) z17 op=×0.5 [M2 vs GT-GEO] → got=오차 0.007m | want=<0.1m | PASS
시뮬 SIM-S4816 | 스케일 | init=고위도(60N) z17 op=×0.5 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4817 | 스케일 | init=고위도(60N) z17 op=×0.5 [M2E vs GT-GEO] → got=오차 0.008m | want=<0.1m | PASS
시뮬 SIM-S4818 | 스케일 | init=고위도(60N) z17 op=×0.5 [M2E vs GT-SCREEN] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4819 | 스케일 | init=고위도(60N) z17 op=×0.9 [M1 vs GT-GEO] → got=오차 0.353m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4820 | 스케일 | init=고위도(60N) z17 op=×0.9 [M1 vs GT-SCREEN] → got=오차 0.351m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4821 | 스케일 | init=고위도(60N) z17 op=×0.9 [M2 vs GT-GEO] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4822 | 스케일 | init=고위도(60N) z17 op=×0.9 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4823 | 스케일 | init=고위도(60N) z17 op=×0.9 [M2E vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4824 | 스케일 | init=고위도(60N) z17 op=×0.9 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4825 | 스케일 | init=고위도(60N) z17 op=×1.1 [M1 vs GT-GEO] → got=오차 0.526m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4826 | 스케일 | init=고위도(60N) z17 op=×1.1 [M1 vs GT-SCREEN] → got=오차 0.527m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4827 | 스케일 | init=고위도(60N) z17 op=×1.1 [M2 vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4828 | 스케일 | init=고위도(60N) z17 op=×1.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4829 | 스케일 | init=고위도(60N) z17 op=×1.1 [M2E vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4830 | 스케일 | init=고위도(60N) z17 op=×1.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4831 | 스케일 | init=고위도(60N) z17 op=×1.5 [M1 vs GT-GEO] → got=오차 0.392m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4832 | 스케일 | init=고위도(60N) z17 op=×1.5 [M1 vs GT-SCREEN] → got=오차 0.406m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4833 | 스케일 | init=고위도(60N) z17 op=×1.5 [M2 vs GT-GEO] → got=오차 0.019m | want=<0.1m | PASS
시뮬 SIM-S4834 | 스케일 | init=고위도(60N) z17 op=×1.5 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4835 | 스케일 | init=고위도(60N) z17 op=×1.5 [M2E vs GT-GEO] → got=오차 0.022m | want=<0.1m | PASS
시뮬 SIM-S4836 | 스케일 | init=고위도(60N) z17 op=×1.5 [M2E vs GT-SCREEN] → got=오차 0.005m | want=<0.1m | PASS
시뮬 SIM-S4837 | 스케일 | init=고위도(60N) z17 op=×2 [M1 vs GT-GEO] → got=오차 0.737m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4838 | 스케일 | init=고위도(60N) z17 op=×2 [M1 vs GT-SCREEN] → got=오차 0.752m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4839 | 스케일 | init=고위도(60N) z17 op=×2 [M2 vs GT-GEO] → got=오차 0.051m | want=<0.1m | PASS
시뮬 SIM-S4840 | 스케일 | init=고위도(60N) z17 op=×2 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4841 | 스케일 | init=고위도(60N) z17 op=×2 [M2E vs GT-GEO] → got=오차 0.059m | want=<0.1m | PASS
시뮬 SIM-S4842 | 스케일 | init=고위도(60N) z17 op=×2 [M2E vs GT-SCREEN] → got=오차 0.013m | want=<0.1m | PASS
시뮬 SIM-S4843 | 스케일 | init=고위도(60N) z17 op=×4 [M1 vs GT-GEO] → got=오차 1.217m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4844 | 스케일 | init=고위도(60N) z17 op=×4 [M1 vs GT-SCREEN] → got=오차 1.342m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4845 | 스케일 | init=고위도(60N) z17 op=×4 [M2 vs GT-GEO] → got=오차 0.307m | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-S4846 | 스케일 | init=고위도(60N) z17 op=×4 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4847 | 스케일 | init=고위도(60N) z17 op=×4 [M2E vs GT-GEO] → got=오차 0.352m | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-S4848 | 스케일 | init=고위도(60N) z17 op=×4 [M2E vs GT-SCREEN] → got=오차 0.078m | want=<0.1m | PASS
시뮬 SIM-S4849 | 스케일 | init=고위도(60N) z19 op=×0.1 [M1 vs GT-GEO] → got=오차 0.072m | want=<0.1m | PASS
시뮬 SIM-S4850 | 스케일 | init=고위도(60N) z19 op=×0.1 [M1 vs GT-SCREEN] → got=오차 0.074m | want=<0.1m | PASS
시뮬 SIM-S4851 | 스케일 | init=고위도(60N) z19 op=×0.1 [M2 vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4852 | 스케일 | init=고위도(60N) z19 op=×0.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4853 | 스케일 | init=고위도(60N) z19 op=×0.1 [M2E vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4854 | 스케일 | init=고위도(60N) z19 op=×0.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4855 | 스케일 | init=고위도(60N) z19 op=×0.5 [M1 vs GT-GEO] → got=오차 0.106m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4856 | 스케일 | init=고위도(60N) z19 op=×0.5 [M1 vs GT-SCREEN] → got=오차 0.103m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4857 | 스케일 | init=고위도(60N) z19 op=×0.5 [M2 vs GT-GEO] → got=오차 0.007m | want=<0.1m | PASS
시뮬 SIM-S4858 | 스케일 | init=고위도(60N) z19 op=×0.5 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4859 | 스케일 | init=고위도(60N) z19 op=×0.5 [M2E vs GT-GEO] → got=오차 0.008m | want=<0.1m | PASS
시뮬 SIM-S4860 | 스케일 | init=고위도(60N) z19 op=×0.5 [M2E vs GT-SCREEN] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4861 | 스케일 | init=고위도(60N) z19 op=×0.9 [M1 vs GT-GEO] → got=오차 0.144m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4862 | 스케일 | init=고위도(60N) z19 op=×0.9 [M1 vs GT-SCREEN] → got=오차 0.145m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4863 | 스케일 | init=고위도(60N) z19 op=×0.9 [M2 vs GT-GEO] → got=오차 0.002m | want=<0.1m | PASS
시뮬 SIM-S4864 | 스케일 | init=고위도(60N) z19 op=×0.9 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4865 | 스케일 | init=고위도(60N) z19 op=×0.9 [M2E vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4866 | 스케일 | init=고위도(60N) z19 op=×0.9 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4867 | 스케일 | init=고위도(60N) z19 op=×1.1 [M1 vs GT-GEO] → got=오차 0.114m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4868 | 스케일 | init=고위도(60N) z19 op=×1.1 [M1 vs GT-SCREEN] → got=오차 0.113m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4869 | 스케일 | init=고위도(60N) z19 op=×1.1 [M2 vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4870 | 스케일 | init=고위도(60N) z19 op=×1.1 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4871 | 스케일 | init=고위도(60N) z19 op=×1.1 [M2E vs GT-GEO] → got=오차 0.003m | want=<0.1m | PASS
시뮬 SIM-S4872 | 스케일 | init=고위도(60N) z19 op=×1.1 [M2E vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4873 | 스케일 | init=고위도(60N) z19 op=×1.5 [M1 vs GT-GEO] → got=오차 0.153m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4874 | 스케일 | init=고위도(60N) z19 op=×1.5 [M1 vs GT-SCREEN] → got=오차 0.144m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4875 | 스케일 | init=고위도(60N) z19 op=×1.5 [M2 vs GT-GEO] → got=오차 0.019m | want=<0.1m | PASS
시뮬 SIM-S4876 | 스케일 | init=고위도(60N) z19 op=×1.5 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4877 | 스케일 | init=고위도(60N) z19 op=×1.5 [M2E vs GT-GEO] → got=오차 0.022m | want=<0.1m | PASS
시뮬 SIM-S4878 | 스케일 | init=고위도(60N) z19 op=×1.5 [M2E vs GT-SCREEN] → got=오차 0.005m | want=<0.1m | PASS
시뮬 SIM-S4879 | 스케일 | init=고위도(60N) z19 op=×2 [M1 vs GT-GEO] → got=오차 0.178m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4880 | 스케일 | init=고위도(60N) z19 op=×2 [M1 vs GT-SCREEN] → got=오차 0.172m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4881 | 스케일 | init=고위도(60N) z19 op=×2 [M2 vs GT-GEO] → got=오차 0.051m | want=<0.1m | PASS
시뮬 SIM-S4882 | 스케일 | init=고위도(60N) z19 op=×2 [M2 vs GT-SCREEN] → got=오차 0.000m | want=<0.1m | PASS
시뮬 SIM-S4883 | 스케일 | init=고위도(60N) z19 op=×2 [M2E vs GT-GEO] → got=오차 0.059m | want=<0.1m | PASS
시뮬 SIM-S4884 | 스케일 | init=고위도(60N) z19 op=×2 [M2E vs GT-SCREEN] → got=오차 0.013m | want=<0.1m | PASS
시뮬 SIM-S4885 | 스케일 | init=고위도(60N) z19 op=×4 [M1 vs GT-GEO] → got=오차 0.464m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4886 | 스케일 | init=고위도(60N) z19 op=×4 [M1 vs GT-SCREEN] → got=오차 0.342m | want=<0.1m | ISSUE-1: 정수 픽셀 왕복 절단
시뮬 SIM-S4887 | 스케일 | init=고위도(60N) z19 op=×4 [M2 vs GT-GEO] → got=오차 0.307m | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-S4888 | 스케일 | init=고위도(60N) z19 op=×4 [M2 vs GT-SCREEN] → got=오차 0.001m | want=<0.1m | PASS
시뮬 SIM-S4889 | 스케일 | init=고위도(60N) z19 op=×4 [M2E vs GT-GEO] → got=오차 0.352m | want=<0.1m | ISSUE-27: 화면공간 합성은 지면 강체 아님
시뮬 SIM-S4890 | 스케일 | init=고위도(60N) z19 op=×4 [M2E vs GT-SCREEN] → got=오차 0.078m | want=<0.1m | PASS
시뮬 SIM-S4891 | 크기 | init=W=12px op=×0.1 5회 [증분(읽기→곱→쓰기)] → got=10.000px 역복원=1000000.000px | want=원복 12px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4892 | 크기 | init=W=12px op=×0.1 5회 [base합성] → got=10.000px 역복원=1000000.000px | want=원복 12px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4893 | 크기 | init=W=12px op=×0.5 5회 [증분(읽기→곱→쓰기)] → got=10.000px 역복원=320.000px | want=원복 12px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4894 | 크기 | init=W=12px op=×0.5 5회 [base합성] → got=10.000px 역복원=320.000px | want=원복 12px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4895 | 크기 | init=W=12px op=×0.9 5회 [증분(읽기→곱→쓰기)] → got=10.000px 역복원=16.935px | want=원복 12px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4896 | 크기 | init=W=12px op=×0.9 5회 [base합성] → got=10.000px 역복원=16.935px | want=원복 12px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4897 | 크기 | init=W=12px op=×1.1 5회 [증분(읽기→곱→쓰기)] → got=19.326px 역복원=12.000px | want=원복 12px | PASS
시뮬 SIM-S4898 | 크기 | init=W=12px op=×1.1 5회 [base합성] → got=19.326px 역복원=12.000px | want=원복 12px | PASS
시뮬 SIM-S4899 | 크기 | init=W=12px op=×1.5 5회 [증분(읽기→곱→쓰기)] → got=91.125px 역복원=12.000px | want=원복 12px | PASS
시뮬 SIM-S4900 | 크기 | init=W=12px op=×1.5 5회 [base합성] → got=91.125px 역복원=12.000px | want=원복 12px | PASS
시뮬 SIM-S4901 | 크기 | init=W=12px op=×2 5회 [증분(읽기→곱→쓰기)] → got=384.000px 역복원=12.000px | want=원복 12px | PASS
시뮬 SIM-S4902 | 크기 | init=W=12px op=×2 5회 [base합성] → got=384.000px 역복원=12.000px | want=원복 12px | PASS
시뮬 SIM-S4903 | 크기 | init=W=12px op=×4 5회 [증분(읽기→곱→쓰기)] → got=12288.000px 역복원=12.000px | want=원복 12px | PASS
시뮬 SIM-S4904 | 크기 | init=W=12px op=×4 5회 [base합성] → got=12288.000px 역복원=12.000px | want=원복 12px | PASS
시뮬 SIM-S4905 | 크기 | init=W=30px op=×0.1 5회 [증분(읽기→곱→쓰기)] → got=10.000px 역복원=1000000.000px | want=원복 30px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4906 | 크기 | init=W=30px op=×0.1 5회 [base합성] → got=10.000px 역복원=1000000.000px | want=원복 30px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4907 | 크기 | init=W=30px op=×0.5 5회 [증분(읽기→곱→쓰기)] → got=10.000px 역복원=320.000px | want=원복 30px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4908 | 크기 | init=W=30px op=×0.5 5회 [base합성] → got=10.000px 역복원=320.000px | want=원복 30px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4909 | 크기 | init=W=30px op=×0.9 5회 [증분(읽기→곱→쓰기)] → got=17.715px 역복원=30.001px | want=원복 30px | PASS
시뮬 SIM-S4910 | 크기 | init=W=30px op=×0.9 5회 [base합성] → got=17.715px 역복원=30.001px | want=원복 30px | PASS
시뮬 SIM-S4911 | 크기 | init=W=30px op=×1.1 5회 [증분(읽기→곱→쓰기)] → got=48.315px 역복원=30.000px | want=원복 30px | PASS
시뮬 SIM-S4912 | 크기 | init=W=30px op=×1.1 5회 [base합성] → got=48.315px 역복원=30.000px | want=원복 30px | PASS
시뮬 SIM-S4913 | 크기 | init=W=30px op=×1.5 5회 [증분(읽기→곱→쓰기)] → got=227.813px 역복원=30.000px | want=원복 30px | PASS
시뮬 SIM-S4914 | 크기 | init=W=30px op=×1.5 5회 [base합성] → got=227.813px 역복원=30.000px | want=원복 30px | PASS
시뮬 SIM-S4915 | 크기 | init=W=30px op=×2 5회 [증분(읽기→곱→쓰기)] → got=960.000px 역복원=30.000px | want=원복 30px | PASS
시뮬 SIM-S4916 | 크기 | init=W=30px op=×2 5회 [base합성] → got=960.000px 역복원=30.000px | want=원복 30px | PASS
시뮬 SIM-S4917 | 크기 | init=W=30px op=×4 5회 [증분(읽기→곱→쓰기)] → got=30720.000px 역복원=30.000px | want=원복 30px | PASS
시뮬 SIM-S4918 | 크기 | init=W=30px op=×4 5회 [base합성] → got=30720.000px 역복원=30.000px | want=원복 30px | PASS
시뮬 SIM-S4919 | 크기 | init=W=200px op=×0.1 5회 [증분(읽기→곱→쓰기)] → got=10.000px 역복원=1000000.000px | want=원복 200px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4920 | 크기 | init=W=200px op=×0.1 5회 [base합성] → got=10.000px 역복원=1000000.000px | want=원복 200px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4921 | 크기 | init=W=200px op=×0.5 5회 [증분(읽기→곱→쓰기)] → got=10.000px 역복원=320.000px | want=원복 200px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4922 | 크기 | init=W=200px op=×0.5 5회 [base합성] → got=10.000px 역복원=320.000px | want=원복 200px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4923 | 크기 | init=W=200px op=×0.9 5회 [증분(읽기→곱→쓰기)] → got=118.098px 역복원=200.000px | want=원복 200px | PASS
시뮬 SIM-S4924 | 크기 | init=W=200px op=×0.9 5회 [base합성] → got=118.098px 역복원=200.000px | want=원복 200px | PASS
시뮬 SIM-S4925 | 크기 | init=W=200px op=×1.1 5회 [증분(읽기→곱→쓰기)] → got=322.102px 역복원=200.000px | want=원복 200px | PASS
시뮬 SIM-S4926 | 크기 | init=W=200px op=×1.1 5회 [base합성] → got=322.102px 역복원=200.000px | want=원복 200px | PASS
시뮬 SIM-S4927 | 크기 | init=W=200px op=×1.5 5회 [증분(읽기→곱→쓰기)] → got=1518.750px 역복원=200.000px | want=원복 200px | PASS
시뮬 SIM-S4928 | 크기 | init=W=200px op=×1.5 5회 [base합성] → got=1518.750px 역복원=200.000px | want=원복 200px | PASS
시뮬 SIM-S4929 | 크기 | init=W=200px op=×2 5회 [증분(읽기→곱→쓰기)] → got=6400.000px 역복원=200.000px | want=원복 200px | PASS
시뮬 SIM-S4930 | 크기 | init=W=200px op=×2 5회 [base합성] → got=6400.000px 역복원=200.000px | want=원복 200px | PASS
시뮬 SIM-S4931 | 크기 | init=W=200px op=×4 5회 [증분(읽기→곱→쓰기)] → got=204800.000px 역복원=200.000px | want=원복 200px | PASS
시뮬 SIM-S4932 | 크기 | init=W=200px op=×4 5회 [base합성] → got=204800.000px 역복원=200.000px | want=원복 200px | PASS
시뮬 SIM-S4933 | 크기 | init=W=2000px op=×0.1 5회 [증분(읽기→곱→쓰기)] → got=10.000px 역복원=1000000.000px | want=원복 2000px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4934 | 크기 | init=W=2000px op=×0.1 5회 [base합성] → got=10.000px 역복원=1000000.000px | want=원복 2000px | ISSUE-5: 하한 10px 클램프 → 역배율 비가역
시뮬 SIM-S4935 | 크기 | init=W=2000px op=×0.5 5회 [증분(읽기→곱→쓰기)] → got=62.500px 역복원=2000.000px | want=원복 2000px | PASS
시뮬 SIM-S4936 | 크기 | init=W=2000px op=×0.5 5회 [base합성] → got=62.500px 역복원=2000.000px | want=원복 2000px | PASS
시뮬 SIM-S4937 | 크기 | init=W=2000px op=×0.9 5회 [증분(읽기→곱→쓰기)] → got=1180.980px 역복원=2000.000px | want=원복 2000px | PASS
시뮬 SIM-S4938 | 크기 | init=W=2000px op=×0.9 5회 [base합성] → got=1180.980px 역복원=2000.000px | want=원복 2000px | PASS
시뮬 SIM-S4939 | 크기 | init=W=2000px op=×1.1 5회 [증분(읽기→곱→쓰기)] → got=3221.020px 역복원=2000.000px | want=원복 2000px | PASS
시뮬 SIM-S4940 | 크기 | init=W=2000px op=×1.1 5회 [base합성] → got=3221.020px 역복원=2000.000px | want=원복 2000px | PASS
시뮬 SIM-S4941 | 크기 | init=W=2000px op=×1.5 5회 [증분(읽기→곱→쓰기)] → got=15187.500px 역복원=2000.000px | want=원복 2000px | PASS
시뮬 SIM-S4942 | 크기 | init=W=2000px op=×1.5 5회 [base합성] → got=15187.500px 역복원=2000.000px | want=원복 2000px | PASS
시뮬 SIM-S4943 | 크기 | init=W=2000px op=×2 5회 [증분(읽기→곱→쓰기)] → got=64000.000px 역복원=2000.000px | want=원복 2000px | PASS
시뮬 SIM-S4944 | 크기 | init=W=2000px op=×2 5회 [base합성] → got=64000.000px 역복원=2000.000px | want=원복 2000px | PASS
시뮬 SIM-S4945 | 크기 | init=W=2000px op=×4 5회 [증분(읽기→곱→쓰기)] → got=2048000.000px 역복원=2000.000px | want=원복 2000px | PASS
시뮬 SIM-S4946 | 크기 | init=W=2000px op=×4 5회 [base합성] → got=2048000.000px 역복원=2000.000px | want=원복 2000px | PASS
시뮬 SIM-S4947 | 이미지단위 | init=W=12px op=×0.1 [무대응] → got=W'=1.200 → '도'로 해석되어 폭 134km | want=px 유지 | ISSUE-29: 이미지 UpdateSize px/도 자동판별(임계 10) — 축소 시 대륙 크기로 폭발
시뮬 SIM-S4948 | 이미지단위 | init=W=12px op=×0.1 [제안(단위 명시 API)] → got=W'=1.200 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4949 | 이미지단위 | init=W=12px op=×0.5 [무대응] → got=W'=6.000 → '도'로 해석되어 폭 668km | want=px 유지 | ISSUE-29: 이미지 UpdateSize px/도 자동판별(임계 10) — 축소 시 대륙 크기로 폭발
시뮬 SIM-S4950 | 이미지단위 | init=W=12px op=×0.5 [제안(단위 명시 API)] → got=W'=6.000 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4951 | 이미지단위 | init=W=12px op=×0.9 [무대응] → got=W'=10.800 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4952 | 이미지단위 | init=W=12px op=×0.9 [제안(단위 명시 API)] → got=W'=10.800 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4953 | 이미지단위 | init=W=30px op=×0.1 [무대응] → got=W'=3.000 → '도'로 해석되어 폭 334km | want=px 유지 | ISSUE-29: 이미지 UpdateSize px/도 자동판별(임계 10) — 축소 시 대륙 크기로 폭발
시뮬 SIM-S4954 | 이미지단위 | init=W=30px op=×0.1 [제안(단위 명시 API)] → got=W'=3.000 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4955 | 이미지단위 | init=W=30px op=×0.5 [무대응] → got=W'=15.000 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4956 | 이미지단위 | init=W=30px op=×0.5 [제안(단위 명시 API)] → got=W'=15.000 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4957 | 이미지단위 | init=W=30px op=×0.9 [무대응] → got=W'=27.000 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4958 | 이미지단위 | init=W=30px op=×0.9 [제안(단위 명시 API)] → got=W'=27.000 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4959 | 이미지단위 | init=W=100px op=×0.1 [무대응] → got=W'=10.000 → '도'로 해석되어 폭 1113km | want=px 유지 | ISSUE-29: 이미지 UpdateSize px/도 자동판별(임계 10) — 축소 시 대륙 크기로 폭발
시뮬 SIM-S4960 | 이미지단위 | init=W=100px op=×0.1 [제안(단위 명시 API)] → got=W'=10.000 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4961 | 이미지단위 | init=W=100px op=×0.5 [무대응] → got=W'=50.000 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4962 | 이미지단위 | init=W=100px op=×0.5 [제안(단위 명시 API)] → got=W'=50.000 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4963 | 이미지단위 | init=W=100px op=×0.9 [무대응] → got=W'=90.000 → px 유지 | want=px 유지 | PASS
시뮬 SIM-S4964 | 이미지단위 | init=W=100px op=×0.9 [제안(단위 명시 API)] → got=W'=90.000 → px 유지 | want=px 유지 | PASS
시뮬 SIM-Q4965 | 래스터 | init=원본 512px 실폭 300m z17 op=×1 → got=화면/원본=0.62 | want=≤1.0 | PASS
시뮬 SIM-Q4966 | 래스터 | init=원본 512px 실폭 300m z17 op=×1.5 → got=화면/원본=0.93 | want=≤1.0 | PASS
시뮬 SIM-Q4967 | 래스터 | init=원본 512px 실폭 300m z17 op=×2 → got=화면/원본=1.23 | want=≤1.0 | ISSUE-9: 경미 업스케일(1~2배) — 허용 정책 필요
시뮬 SIM-Q4968 | 래스터 | init=원본 512px 실폭 300m z17 op=×4 → got=화면/원본=2.47 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4969 | 래스터 | init=원본 512px 실폭 300m z19 op=×1 → got=화면/원본=2.47 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4970 | 래스터 | init=원본 512px 실폭 300m z19 op=×1.5 → got=화면/원본=3.70 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4971 | 래스터 | init=원본 512px 실폭 300m z19 op=×2 → got=화면/원본=4.94 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4972 | 래스터 | init=원본 512px 실폭 300m z19 op=×4 → got=화면/원본=9.88 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4973 | 래스터 | init=원본 512px 실폭 300m z20 op=×1 → got=화면/원본=4.94 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4974 | 래스터 | init=원본 512px 실폭 300m z20 op=×1.5 → got=화면/원본=7.41 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4975 | 래스터 | init=원본 512px 실폭 300m z20 op=×2 → got=화면/원본=9.88 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4976 | 래스터 | init=원본 512px 실폭 300m z20 op=×4 → got=화면/원본=19.76 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4977 | 래스터 | init=원본 1024px 실폭 200m z17 op=×1 → got=화면/원본=0.21 | want=≤1.0 | PASS
시뮬 SIM-Q4978 | 래스터 | init=원본 1024px 실폭 200m z17 op=×1.5 → got=화면/원본=0.31 | want=≤1.0 | PASS
시뮬 SIM-Q4979 | 래스터 | init=원본 1024px 실폭 200m z17 op=×2 → got=화면/원본=0.41 | want=≤1.0 | PASS
시뮬 SIM-Q4980 | 래스터 | init=원본 1024px 실폭 200m z17 op=×4 → got=화면/원본=0.82 | want=≤1.0 | PASS
시뮬 SIM-Q4981 | 래스터 | init=원본 1024px 실폭 200m z19 op=×1 → got=화면/원본=0.82 | want=≤1.0 | PASS
시뮬 SIM-Q4982 | 래스터 | init=원본 1024px 실폭 200m z19 op=×1.5 → got=화면/원본=1.23 | want=≤1.0 | ISSUE-9: 경미 업스케일(1~2배) — 허용 정책 필요
시뮬 SIM-Q4983 | 래스터 | init=원본 1024px 실폭 200m z19 op=×2 → got=화면/원본=1.65 | want=≤1.0 | ISSUE-9: 경미 업스케일(1~2배) — 허용 정책 필요
시뮬 SIM-Q4984 | 래스터 | init=원본 1024px 실폭 200m z19 op=×4 → got=화면/원본=3.29 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4985 | 래스터 | init=원본 1024px 실폭 200m z20 op=×1 → got=화면/원본=1.65 | want=≤1.0 | ISSUE-9: 경미 업스케일(1~2배) — 허용 정책 필요
시뮬 SIM-Q4986 | 래스터 | init=원본 1024px 실폭 200m z20 op=×1.5 → got=화면/원본=2.47 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4987 | 래스터 | init=원본 1024px 실폭 200m z20 op=×2 → got=화면/원본=3.29 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4988 | 래스터 | init=원본 1024px 실폭 200m z20 op=×4 → got=화면/원본=6.59 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4989 | 래스터 | init=원본 2000px 실폭 500m z17 op=×1 → got=화면/원본=0.26 | want=≤1.0 | PASS
시뮬 SIM-Q4990 | 래스터 | init=원본 2000px 실폭 500m z17 op=×1.5 → got=화면/원본=0.40 | want=≤1.0 | PASS
시뮬 SIM-Q4991 | 래스터 | init=원본 2000px 실폭 500m z17 op=×2 → got=화면/원본=0.53 | want=≤1.0 | PASS
시뮬 SIM-Q4992 | 래스터 | init=원본 2000px 실폭 500m z17 op=×4 → got=화면/원본=1.05 | want=≤1.0 | ISSUE-9: 경미 업스케일(1~2배) — 허용 정책 필요
시뮬 SIM-Q4993 | 래스터 | init=원본 2000px 실폭 500m z19 op=×1 → got=화면/원본=1.05 | want=≤1.0 | ISSUE-9: 경미 업스케일(1~2배) — 허용 정책 필요
시뮬 SIM-Q4994 | 래스터 | init=원본 2000px 실폭 500m z19 op=×1.5 → got=화면/원본=1.58 | want=≤1.0 | ISSUE-9: 경미 업스케일(1~2배) — 허용 정책 필요
시뮬 SIM-Q4995 | 래스터 | init=원본 2000px 실폭 500m z19 op=×2 → got=화면/원본=2.11 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4996 | 래스터 | init=원본 2000px 실폭 500m z19 op=×4 → got=화면/원본=4.22 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4997 | 래스터 | init=원본 2000px 실폭 500m z20 op=×1 → got=화면/원본=2.11 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4998 | 래스터 | init=원본 2000px 실폭 500m z20 op=×1.5 → got=화면/원본=3.16 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q4999 | 래스터 | init=원본 2000px 실폭 500m z20 op=×2 → got=화면/원본=4.22 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q5000 | 래스터 | init=원본 2000px 실폭 500m z20 op=×4 → got=화면/원본=8.43 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q5001 | 래스터 | init=원본 4096px 실폭 1000m z17 op=×1 → got=화면/원본=0.26 | want=≤1.0 | PASS
시뮬 SIM-Q5002 | 래스터 | init=원본 4096px 실폭 1000m z17 op=×1.5 → got=화면/원본=0.39 | want=≤1.0 | PASS
시뮬 SIM-Q5003 | 래스터 | init=원본 4096px 실폭 1000m z17 op=×2 → got=화면/원본=0.51 | want=≤1.0 | PASS
시뮬 SIM-Q5004 | 래스터 | init=원본 4096px 실폭 1000m z17 op=×4 → got=화면/원본=1.03 | want=≤1.0 | ISSUE-9: 경미 업스케일(1~2배) — 허용 정책 필요
시뮬 SIM-Q5005 | 래스터 | init=원본 4096px 실폭 1000m z19 op=×1 → got=화면/원본=1.03 | want=≤1.0 | ISSUE-9: 경미 업스케일(1~2배) — 허용 정책 필요
시뮬 SIM-Q5006 | 래스터 | init=원본 4096px 실폭 1000m z19 op=×1.5 → got=화면/원본=1.54 | want=≤1.0 | ISSUE-9: 경미 업스케일(1~2배) — 허용 정책 필요
시뮬 SIM-Q5007 | 래스터 | init=원본 4096px 실폭 1000m z19 op=×2 → got=화면/원본=2.06 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q5008 | 래스터 | init=원본 4096px 실폭 1000m z19 op=×4 → got=화면/원본=4.12 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q5009 | 래스터 | init=원본 4096px 실폭 1000m z20 op=×1 → got=화면/원본=2.06 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q5010 | 래스터 | init=원본 4096px 실폭 1000m z20 op=×1.5 → got=화면/원본=3.09 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q5011 | 래스터 | init=원본 4096px 실폭 1000m z20 op=×2 → got=화면/원본=4.12 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-Q5012 | 래스터 | init=원본 4096px 실폭 1000m z20 op=×4 → got=화면/원본=8.23 | want=≤1.0 | ISSUE-8: 2배 초과 업스케일(가시 블러) — 경고/캡 필요
시뮬 SIM-C5013 | 계열분기 | init=point op=회전 15° [T-RT] → got=점심볼만 회전·모델 미반영(재부팅 소실) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5014 | 계열분기 | init=point op=회전 15° [T-MODEL] → got=점심볼 Position 공전 OK, Bearing write-back 규약 충돌 | want=전 계열 시각·모델 정합 | ISSUE-12: 점심볼 Bearing write-back 규약(R-35) 충돌
시뮬 SIM-C5015 | 계열분기 | init=point op=회전 15° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5016 | 계열분기 | init=point op=회전 45° [T-RT] → got=점심볼만 회전·모델 미반영(재부팅 소실) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5017 | 계열분기 | init=point op=회전 45° [T-MODEL] → got=점심볼 Position 공전 OK, Bearing write-back 규약 충돌 | want=전 계열 시각·모델 정합 | ISSUE-12: 점심볼 Bearing write-back 규약(R-35) 충돌
시뮬 SIM-C5018 | 계열분기 | init=point op=회전 45° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5019 | 계열분기 | init=point op=회전 90° [T-RT] → got=점심볼만 회전·모델 미반영(재부팅 소실) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5020 | 계열분기 | init=point op=회전 90° [T-MODEL] → got=점심볼 Position 공전 OK, Bearing write-back 규약 충돌 | want=전 계열 시각·모델 정합 | ISSUE-12: 점심볼 Bearing write-back 규약(R-35) 충돌
시뮬 SIM-C5021 | 계열분기 | init=point op=회전 90° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5022 | 계열분기 | init=line op=회전 15° [T-RT] → got=라인/구역 미회전(정점 재투영이 원좌표 유지) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5023 | 계열분기 | init=line op=회전 15° [T-MODEL] → got=라인 정점 회전 OK | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5024 | 계열분기 | init=line op=회전 15° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5025 | 계열분기 | init=line op=회전 45° [T-RT] → got=라인/구역 미회전(정점 재투영이 원좌표 유지) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5026 | 계열분기 | init=line op=회전 45° [T-MODEL] → got=라인 정점 회전 OK | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5027 | 계열분기 | init=line op=회전 45° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5028 | 계열분기 | init=line op=회전 90° [T-RT] → got=라인/구역 미회전(정점 재투영이 원좌표 유지) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5029 | 계열분기 | init=line op=회전 90° [T-MODEL] → got=라인 정점 회전 OK | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5030 | 계열분기 | init=line op=회전 90° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5031 | 계열분기 | init=image op=회전 15° [T-RT] → got=점심볼만 회전·모델 미반영(재부팅 소실) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5032 | 계열분기 | init=image op=회전 15° [T-MODEL] → got=라인 정점 회전 OK | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5033 | 계열분기 | init=image op=회전 15° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5034 | 계열분기 | init=image op=회전 45° [T-RT] → got=점심볼만 회전·모델 미반영(재부팅 소실) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5035 | 계열분기 | init=image op=회전 45° [T-MODEL] → got=라인 정점 회전 OK | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5036 | 계열분기 | init=image op=회전 45° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5037 | 계열분기 | init=image op=회전 90° [T-RT] → got=점심볼만 회전·모델 미반영(재부팅 소실) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5038 | 계열분기 | init=image op=회전 90° [T-MODEL] → got=라인 정점 회전 OK | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5039 | 계열분기 | init=image op=회전 90° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5040 | 계열분기 | init=pidsgroup op=회전 15° [T-RT] → got=라인/구역 미회전(정점 재투영이 원좌표 유지) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5041 | 계열분기 | init=pidsgroup op=회전 15° [T-MODEL] → got=라인 정점 회전 OK | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5042 | 계열분기 | init=pidsgroup op=회전 15° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5043 | 계열분기 | init=pidsgroup op=회전 45° [T-RT] → got=라인/구역 미회전(정점 재투영이 원좌표 유지) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5044 | 계열분기 | init=pidsgroup op=회전 45° [T-MODEL] → got=라인 정점 회전 OK | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5045 | 계열분기 | init=pidsgroup op=회전 45° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5046 | 계열분기 | init=pidsgroup op=회전 90° [T-RT] → got=라인/구역 미회전(정점 재투영이 원좌표 유지) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5047 | 계열분기 | init=pidsgroup op=회전 90° [T-MODEL] → got=라인 정점 회전 OK | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5048 | 계열분기 | init=pidsgroup op=회전 90° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5049 | 계열분기 | init=mixed op=회전 15° [T-RT] → got=라인/구역 미회전(정점 재투영이 원좌표 유지) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5050 | 계열분기 | init=mixed op=회전 15° [T-MODEL] → got=점심볼 Position 공전 OK, Bearing write-back 규약 충돌 | want=전 계열 시각·모델 정합 | ISSUE-12: 점심볼 Bearing write-back 규약(R-35) 충돌
시뮬 SIM-C5051 | 계열분기 | init=mixed op=회전 15° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5052 | 계열분기 | init=mixed op=회전 45° [T-RT] → got=라인/구역 미회전(정점 재투영이 원좌표 유지) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5053 | 계열분기 | init=mixed op=회전 45° [T-MODEL] → got=점심볼 Position 공전 OK, Bearing write-back 규약 충돌 | want=전 계열 시각·모델 정합 | ISSUE-12: 점심볼 Bearing write-back 규약(R-35) 충돌
시뮬 SIM-C5054 | 계열분기 | init=mixed op=회전 45° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-C5055 | 계열분기 | init=mixed op=회전 90° [T-RT] → got=라인/구역 미회전(정점 재투영이 원좌표 유지) | want=전 계열 시각·모델 정합 | ISSUE-11: RenderTransform 단독=라인 미회전·비영속
시뮬 SIM-C5056 | 계열분기 | init=mixed op=회전 90° [T-MODEL] → got=점심볼 Position 공전 OK, Bearing write-back 규약 충돌 | want=전 계열 시각·모델 정합 | ISSUE-12: 점심볼 Bearing write-back 규약(R-35) 충돌
시뮬 SIM-C5057 | 계열분기 | init=mixed op=회전 90° [T-HYBRID] → got=계열별 분기(점=Position공전+Bearing가산 / 라인=LinePoints / 구역=PidsGroupPoints / 이미지=AABB+Rotation) | want=전 계열 시각·모델 정합 | PASS
시뮬 SIM-F5058 | 부가채널 | init=PIDS FOV(DetectionBearing) (PTZ 런타임 덮어쓰기 + 비영속(재부팅 시 BaseBearing 복원)) op=그룹 변환 [무대응] → got=미처리 | want=정합 | ISSUE-13: 회전 후 FOV 월드각 소실
시뮬 SIM-F5059 | 부가채널 | init=PIDS FOV(DetectionBearing) (PTZ 런타임 덮어쓰기 + 비영속(재부팅 시 BaseBearing 복원)) op=그룹 변환 [제안(명시 처리)] → got=명시 정책+동반 갱신 | want=정합 | PASS
시뮬 SIM-F5060 | 부가채널 | init=라벨 오프셋(점=px) (화면 고정 px — 회전/스케일 미보정) op=그룹 변환 [무대응] → got=미처리 | want=정합 | ISSUE-14: 라벨이 심볼과 함께 안 돎
시뮬 SIM-F5061 | 부가채널 | init=라벨 오프셋(점=px) (화면 고정 px — 회전/스케일 미보정) op=그룹 변환 [제안(명시 처리)] → got=명시 정책+동반 갱신 | want=정합 | PASS
시뮬 SIM-F5062 | 부가채널 | init=라벨 오프셋(라인=비율) (재렌더가 Position/Bearing/W/H PropertyChanged 필터 의존) op=그룹 변환 [무대응] → got=미처리 | want=정합 | ISSUE-15: ApplyGeometry 경로 알림 미발화 시 라벨 stale
시뮬 SIM-F5063 | 부가채널 | init=라벨 오프셋(라인=비율) (재렌더가 Position/Bearing/W/H PropertyChanged 필터 의존) op=그룹 변환 [제안(명시 처리)] → got=명시 정책+동반 갱신 | want=정합 | PASS
시뮬 SIM-F5064 | 부가채널 | init=카메라 팝업 지오앵커 (심볼과 별개 테이블 영속(CameraPopupPositionStore)) op=그룹 변환 [무대응] → got=미처리 | want=정합 | ISSUE-16: 심볼 이동 후 팝업 잔류
시뮬 SIM-F5065 | 부가채널 | init=카메라 팝업 지오앵커 (심볼과 별개 테이블 영속(CameraPopupPositionStore)) op=그룹 변환 [제안(명시 처리)] → got=명시 정책+동반 갱신 | want=정합 | PASS
시뮬 SIM-F5066 | 부가채널 | init=격자 스냅 (그룹 경로 스냅 참조 0건(단일/방향키만)) op=그룹 변환 [무대응] → got=미처리 | want=정합 | ISSUE-17: 그룹 변환 스냅 정책 부재
시뮬 SIM-F5067 | 부가채널 | init=격자 스냅 (그룹 경로 스냅 참조 0건(단일/방향키만)) op=그룹 변환 [제안(명시 처리)] → got=명시 정책+동반 갱신 | want=정합 | PASS
시뮬 SIM-F5068 | 부가채널 | init=Symbols.Zoom(표시 최소줌) (스케일해도 불변) op=그룹 변환 [무대응] → got=미처리 | want=정합 | ISSUE-18: 키운 심볼이 여전히 줌 게이트로 비가시
시뮬 SIM-F5069 | 부가채널 | init=Symbols.Zoom(표시 최소줌) (스케일해도 불변) op=그룹 변환 [제안(명시 처리)] → got=명시 정책+동반 갱신 | want=정합 | PASS
시뮬 SIM-F5070 | 부가채널 | init=펄스 애니메이션(_pulseScale) (이벤트 중 BeginAnimation이 Scale 점유) op=그룹 변환 [무대응] → got=미처리 | want=정합 | ISSUE-19: 탐지중 심볼 스케일이 애니메이션에 덮임
시뮬 SIM-F5071 | 부가채널 | init=펄스 애니메이션(_pulseScale) (이벤트 중 BeginAnimation이 Scale 점유) op=그룹 변환 [제안(명시 처리)] → got=명시 정책+동반 갱신 | want=정합 | PASS
시뮬 SIM-F5072 | 부가채널 | init=카메라 조준 원점(AimLocation) (심볼 좌표=PTZ 조준 원점(CameraAimRequestBuilder)) op=그룹 변환 [무대응] → got=미처리 | want=정합 | ISSUE-30: 카메라 심볼 이동 시 실제 조준 어긋남(발행 명령은 Undo 불가)
시뮬 SIM-F5073 | 부가채널 | init=카메라 조준 원점(AimLocation) (심볼 좌표=PTZ 조준 원점(CameraAimRequestBuilder)) op=그룹 변환 [제안(명시 처리)] → got=명시 정책+동반 갱신 | want=정합 | PASS
시뮬 SIM-F5074 | 부가채널 | init=그룹 공통 속성창(절대 균일) (Bearing/W/H 전원 동일값 일괄 쓰기 경로 기존재) op=그룹 변환 [무대응] → got=미처리 | want=정합 | ISSUE-31: 상대 변환과 의미 충돌(속성창이 회전 결과 평탄화)
시뮬 SIM-F5075 | 부가채널 | init=그룹 공통 속성창(절대 균일) (Bearing/W/H 전원 동일값 일괄 쓰기 경로 기존재) op=그룹 변환 [제안(명시 처리)] → got=명시 정책+동반 갱신 | want=정합 | PASS
시뮬 SIM-F5076 | 부가채널 | init=맵 회전 θ(MapRotation) (점 심볼만 DisplayAngle에서 −θ 보정, 라인 계열 미보정) op=그룹 변환 [무대응] → got=미처리 | want=정합 | ISSUE-32: θ≠0에서 화면 제스처각→Bearing 이중 보정
시뮬 SIM-F5077 | 부가채널 | init=맵 회전 θ(MapRotation) (점 심볼만 DisplayAngle에서 −θ 보정, 라인 계열 미보정) op=그룹 변환 [제안(명시 처리)] → got=명시 정책+동반 갱신 | want=정합 | PASS
시뮬 SIM-F5078 | 부가채널 | init=레이어 가시성 (선택 후 레이어 숨겨도 그룹 선택 유지) op=그룹 변환 [무대응] → got=미처리 | want=정합 | ISSUE-33: 보이지 않는 멤버가 조용히 변환됨
시뮬 SIM-F5079 | 부가채널 | init=레이어 가시성 (선택 후 레이어 숨겨도 그룹 선택 유지) op=그룹 변환 [제안(명시 처리)] → got=명시 정책+동반 갱신 | want=정합 | PASS
시뮬 SIM-F5080 | 부가채널 | init=ZOrder (그룹 ZOrder는 밴드 평탄화·이미지 미영속) op=그룹 변환 [무대응] → got=미처리 | want=정합 | ISSUE-34: 변환 후 겹침 순서/히트 불명
시뮬 SIM-F5081 | 부가채널 | init=ZOrder (그룹 ZOrder는 밴드 평탄화·이미지 미영속) op=그룹 변환 [제안(명시 처리)] → got=명시 정책+동반 갱신 | want=정합 | PASS
시뮬 SIM-T5082 | 영속 | init=5건 중 0건 실패 [현행(멤버별 개별 UPDATE·멤버간 트랜잭션 없음)] → got=전량 반영 | want=전부-또는-전무 | PASS
시뮬 SIM-T5083 | 영속 | init=5건 중 0건 실패 [제안(멤버간 트랜잭션)] → got=전량 반영 | want=전부-또는-전무 | PASS
시뮬 SIM-T5084 | 영속 | init=5건 중 1건 실패 [현행(멤버별 개별 UPDATE·멤버간 트랜잭션 없음)] → got=4건만 반영·화면/DB 영구 불일치 | want=전부-또는-전무 | ISSUE-20: 부분 커밋 → 영구 불일치
시뮬 SIM-T5085 | 영속 | init=5건 중 1건 실패 [제안(멤버간 트랜잭션)] → got=전량 롤백+통지 | want=전부-또는-전무 | PASS
시뮬 SIM-T5086 | 영속 | init=5건 중 3건 실패 [현행(멤버별 개별 UPDATE·멤버간 트랜잭션 없음)] → got=2건만 반영·화면/DB 영구 불일치 | want=전부-또는-전무 | ISSUE-20: 부분 커밋 → 영구 불일치
시뮬 SIM-T5087 | 영속 | init=5건 중 3건 실패 [제안(멤버간 트랜잭션)] → got=전량 롤백+통지 | want=전부-또는-전무 | PASS
시뮬 SIM-T5088 | 영속 | init=20건 중 0건 실패 [현행(멤버별 개별 UPDATE·멤버간 트랜잭션 없음)] → got=전량 반영 | want=전부-또는-전무 | PASS
시뮬 SIM-T5089 | 영속 | init=20건 중 0건 실패 [제안(멤버간 트랜잭션)] → got=전량 반영 | want=전부-또는-전무 | PASS
시뮬 SIM-T5090 | 영속 | init=20건 중 1건 실패 [현행(멤버별 개별 UPDATE·멤버간 트랜잭션 없음)] → got=19건만 반영·화면/DB 영구 불일치 | want=전부-또는-전무 | ISSUE-20: 부분 커밋 → 영구 불일치
시뮬 SIM-T5091 | 영속 | init=20건 중 1건 실패 [제안(멤버간 트랜잭션)] → got=전량 롤백+통지 | want=전부-또는-전무 | PASS
시뮬 SIM-T5092 | 영속 | init=20건 중 3건 실패 [현행(멤버별 개별 UPDATE·멤버간 트랜잭션 없음)] → got=17건만 반영·화면/DB 영구 불일치 | want=전부-또는-전무 | ISSUE-20: 부분 커밋 → 영구 불일치
시뮬 SIM-T5093 | 영속 | init=20건 중 3건 실패 [제안(멤버간 트랜잭션)] → got=전량 롤백+통지 | want=전부-또는-전무 | PASS
시뮬 SIM-T5094 | 영속 | init=100건 중 0건 실패 [현행(멤버별 개별 UPDATE·멤버간 트랜잭션 없음)] → got=전량 반영 | want=전부-또는-전무 | PASS
시뮬 SIM-T5095 | 영속 | init=100건 중 0건 실패 [제안(멤버간 트랜잭션)] → got=전량 반영 | want=전부-또는-전무 | PASS
시뮬 SIM-T5096 | 영속 | init=100건 중 1건 실패 [현행(멤버별 개별 UPDATE·멤버간 트랜잭션 없음)] → got=99건만 반영·화면/DB 영구 불일치 | want=전부-또는-전무 | ISSUE-20: 부분 커밋 → 영구 불일치
시뮬 SIM-T5097 | 영속 | init=100건 중 1건 실패 [제안(멤버간 트랜잭션)] → got=전량 롤백+통지 | want=전부-또는-전무 | PASS
시뮬 SIM-T5098 | 영속 | init=100건 중 3건 실패 [현행(멤버별 개별 UPDATE·멤버간 트랜잭션 없음)] → got=97건만 반영·화면/DB 영구 불일치 | want=전부-또는-전무 | ISSUE-20: 부분 커밋 → 영구 불일치
시뮬 SIM-T5099 | 영속 | init=100건 중 3건 실패 [제안(멤버간 트랜잭션)] → got=전량 롤백+통지 | want=전부-또는-전무 | PASS
시뮬 SIM-T5100 | 영속 | init=1000건 중 0건 실패 [현행(멤버별 개별 UPDATE·멤버간 트랜잭션 없음)] → got=전량 반영 | want=전부-또는-전무 | PASS
시뮬 SIM-T5101 | 영속 | init=1000건 중 0건 실패 [제안(멤버간 트랜잭션)] → got=전량 반영 | want=전부-또는-전무 | PASS
시뮬 SIM-T5102 | 영속 | init=1000건 중 1건 실패 [현행(멤버별 개별 UPDATE·멤버간 트랜잭션 없음)] → got=999건만 반영·화면/DB 영구 불일치 | want=전부-또는-전무 | ISSUE-20: 부분 커밋 → 영구 불일치
시뮬 SIM-T5103 | 영속 | init=1000건 중 1건 실패 [제안(멤버간 트랜잭션)] → got=전량 롤백+통지 | want=전부-또는-전무 | PASS
시뮬 SIM-T5104 | 영속 | init=1000건 중 3건 실패 [현행(멤버별 개별 UPDATE·멤버간 트랜잭션 없음)] → got=997건만 반영·화면/DB 영구 불일치 | want=전부-또는-전무 | ISSUE-20: 부분 커밋 → 영구 불일치
시뮬 SIM-T5105 | 영속 | init=1000건 중 3건 실패 [제안(멤버간 트랜잭션)] → got=전량 롤백+통지 | want=전부-또는-전무 | PASS
시뮬 SIM-T5106 | 성능 | init=100개 선택 [현행(멤버당 2~3 순차 왕복)] → got=DB 왕복 200회 ≈ 0.8s(4ms/왕복 가정) | want=<1s | PASS
시뮬 SIM-T5107 | 성능 | init=100개 선택 [제안(배치 1왕복)] → got=DB 왕복 1회 ≈ 0.0s(4ms/왕복 가정) | want=<1s | PASS
시뮬 SIM-T5108 | 성능 | init=500개 선택 [현행(멤버당 2~3 순차 왕복)] → got=DB 왕복 1000회 ≈ 4.0s(4ms/왕복 가정) | want=<1s | ISSUE-35: 대량 선택 시 순차 왕복으로 UI 장시간 블로킹
시뮬 SIM-T5109 | 성능 | init=500개 선택 [제안(배치 1왕복)] → got=DB 왕복 1회 ≈ 0.0s(4ms/왕복 가정) | want=<1s | PASS
시뮬 SIM-T5110 | 성능 | init=1000개 선택 [현행(멤버당 2~3 순차 왕복)] → got=DB 왕복 2000회 ≈ 8.0s(4ms/왕복 가정) | want=<1s | ISSUE-35: 대량 선택 시 순차 왕복으로 UI 장시간 블로킹
시뮬 SIM-T5111 | 성능 | init=1000개 선택 [제안(배치 1왕복)] → got=DB 왕복 1회 ≈ 0.0s(4ms/왕복 가정) | want=<1s | PASS
시뮬 SIM-X5112 | 생명주기 | init=변환 중 멤버 삭제(레이어패널) [무대응] → got=미정의 동작 | want=안전 종료 | ISSUE-21: dispose된 멤버에 변환/DB 쓰기(RemoveAndRefresh 호출부 0건)
시뮬 SIM-X5113 | 생명주기 | init=변환 중 멤버 삭제(레이어패널) [제안(세션 가드)] → got=세션 무효화+안전 종료 | want=안전 종료 | PASS
시뮬 SIM-X5114 | 생명주기 | init=변환 중 맵 전환 [무대응] → got=미정의 동작 | want=안전 종료 | ISSUE-22: write-behind가 사라진 마커에 도달 + Undo 스택 소거
시뮬 SIM-X5115 | 생명주기 | init=변환 중 맵 전환 [제안(세션 가드)] → got=세션 무효화+안전 종료 | want=안전 종료 | PASS
시뮬 SIM-X5116 | 생명주기 | init=변환 중 ESC/편집모드 OFF [무대응] → got=미정의 동작 | want=안전 종료 | ISSUE-23: ESC는 AdornerManager만 취소 → 그룹 상태 미정의
시뮬 SIM-X5117 | 생명주기 | init=변환 중 ESC/편집모드 OFF [제안(세션 가드)] → got=세션 무효화+안전 종료 | want=안전 종료 | PASS
시뮬 SIM-X5118 | 생명주기 | init=변환 중 권한 강등(CanEditMap=false) [무대응] → got=미정의 동작 | want=안전 종료 | ISSUE-24: 루프 내 재확인 부재 → 부분 커밋
시뮬 SIM-X5119 | 생명주기 | init=변환 중 권한 강등(CanEditMap=false) [제안(세션 가드)] → got=세션 무효화+안전 종료 | want=안전 종료 | PASS
시뮬 SIM-X5120 | 생명주기 | init=변환 중 이벤트 writer(Detecting/PTZ) [무대응] → got=미정의 동작 | want=안전 종료 | ISSUE-25: 로컬 2차 writer와 경합
시뮬 SIM-X5121 | 생명주기 | init=변환 중 이벤트 writer(Detecting/PTZ) [제안(세션 가드)] → got=세션 무효화+안전 종료 | want=안전 종료 | PASS
시뮬 SIM-X5122 | 생명주기 | init=변환 중 선택집합 변이(SetMarkers) [무대응] → got=미정의 동작 | want=안전 종료 | ISSUE-36: base 스냅샷 인덱스 페어링 붕괴 → 멤버가 남의 원점으로 순간이동
시뮬 SIM-X5123 | 생명주기 | init=변환 중 선택집합 변이(SetMarkers) [제안(세션 가드)] → got=세션 무효화+안전 종료 | want=안전 종료 | PASS
시뮬 SIM-X5124 | 생명주기 | init=타 클라이언트 동시 편집 [무대응] → got=미정의 동작 | want=안전 종료 | ISSUE-37: 낙관적 동시성·서버 푸시 부재 → 무경고 last-write-wins
시뮬 SIM-X5125 | 생명주기 | init=타 클라이언트 동시 편집 [제안(세션 가드)] → got=세션 무효화+안전 종료 | want=안전 종료 | PASS
시뮬 SIM-X5126 | 생명주기 | init=변환 결과가 앵커 뷰포트 밖 [무대응] → got=미정의 동작 | want=안전 종료 | ISSUE-38: 팬으로 도달 불가 + Undo 소거 시 영구 유실
시뮬 SIM-X5127 | 생명주기 | init=변환 결과가 앵커 뷰포트 밖 [제안(세션 가드)] → got=세션 무효화+안전 종료 | want=안전 종료 | PASS
시뮬 SIM-X5128 | 생명주기 | init=측정/드로잉 모드와 동시 [무대응] → got=미정의 동작 | want=안전 종료 | ISSUE-39: 그룹 어도너가 측정 클릭 선점(e.Handled)
시뮬 SIM-X5129 | 생명주기 | init=측정/드로잉 모드와 동시 [제안(세션 가드)] → got=세션 무효화+안전 종료 | want=안전 종료 | PASS
시뮬 SIM-TL5130 | 틸트 | init=phi=0° op=회전 5° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 5.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5131 | 틸트 | init=phi=0° op=회전 5° [TS-OUTER] → got=지면 비등방 1.0000, 화면 관측각 5.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5132 | 틸트 | init=phi=0° op=회전 15° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 15.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5133 | 틸트 | init=phi=0° op=회전 15° [TS-OUTER] → got=지면 비등방 1.0000, 화면 관측각 15.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5134 | 틸트 | init=phi=0° op=회전 45° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 45.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5135 | 틸트 | init=phi=0° op=회전 45° [TS-OUTER] → got=지면 비등방 1.0000, 화면 관측각 45.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5136 | 틸트 | init=phi=0° op=회전 90° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 90.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5137 | 틸트 | init=phi=0° op=회전 90° [TS-OUTER] → got=지면 비등방 1.0000, 화면 관측각 90.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5138 | 틸트 | init=phi=0° op=회전 135° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 135.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5139 | 틸트 | init=phi=0° op=회전 135° [TS-OUTER] → got=지면 비등방 1.0000, 화면 관측각 135.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5140 | 틸트 | init=phi=10° op=회전 5° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 4.92°(입력 대비 0.08°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5141 | 틸트 | init=phi=10° op=회전 5° [TS-OUTER] → got=지면 비등방 1.0027, 화면 관측각 5.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5142 | 틸트 | init=phi=10° op=회전 15° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 14.78°(입력 대비 0.22°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5143 | 틸트 | init=phi=10° op=회전 15° [TS-OUTER] → got=지면 비등방 1.0080, 화면 관측각 15.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5144 | 틸트 | init=phi=10° op=회전 45° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 44.56°(입력 대비 0.44°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5145 | 틸트 | init=phi=10° op=회전 45° [TS-OUTER] → got=지면 비등방 1.0219, 화면 관측각 45.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5146 | 틸트 | init=phi=10° op=회전 90° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 90.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5147 | 틸트 | init=phi=10° op=회전 90° [TS-OUTER] → got=지면 비등방 1.0311, 화면 관측각 90.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5148 | 틸트 | init=phi=10° op=회전 135° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 135.44°(입력 대비 0.44°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5149 | 틸트 | init=phi=10° op=회전 135° [TS-OUTER] → got=지면 비등방 1.0219, 화면 관측각 135.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5150 | 틸트 | init=phi=20° op=회전 5° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 4.70°(입력 대비 0.30°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5151 | 틸트 | init=phi=20° op=회전 5° [TS-OUTER] → got=지면 비등방 1.0109, 화면 관측각 5.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5152 | 틸트 | init=phi=20° op=회전 15° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 14.13°(입력 대비 0.87°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5153 | 틸트 | init=phi=20° op=회전 15° [TS-OUTER] → got=지면 비등방 1.0327, 화면 관측각 15.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5154 | 틸트 | init=phi=20° op=회전 45° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 43.22°(입력 대비 1.78°) | want=비등방 1.000(지면 강체) | ISSUE-41: 지면 강체는 유지되나 화면 관측각이 입력각과 불일치(사용자 혼동)
시뮬 SIM-TL5155 | 틸트 | init=phi=20° op=회전 45° [TS-OUTER] → got=지면 비등방 1.0920, 화면 관측각 45.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5156 | 틸트 | init=phi=20° op=회전 90° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 90.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5157 | 틸트 | init=phi=20° op=회전 90° [TS-OUTER] → got=지면 비등방 1.1325, 화면 관측각 90.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5158 | 틸트 | init=phi=20° op=회전 135° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 136.78°(입력 대비 1.78°) | want=비등방 1.000(지면 강체) | ISSUE-41: 지면 강체는 유지되나 화면 관측각이 입력각과 불일치(사용자 혼동)
시뮬 SIM-TL5159 | 틸트 | init=phi=20° op=회전 135° [TS-OUTER] → got=지면 비등방 1.0920, 화면 관측각 135.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5160 | 틸트 | init=phi=27° op=회전 5° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 4.46°(입력 대비 0.54°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5161 | 틸트 | init=phi=27° op=회전 5° [TS-OUTER] → got=지면 비등방 1.0204, 화면 관측각 5.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5162 | 틸트 | init=phi=27° op=회전 15° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 13.43°(입력 대비 1.57°) | want=비등방 1.000(지면 강체) | ISSUE-41: 지면 강체는 유지되나 화면 관측각이 입력각과 불일치(사용자 혼동)
시뮬 SIM-TL5163 | 틸트 | init=phi=27° op=회전 15° [TS-OUTER] → got=지면 비등방 1.0617, 화면 관측각 15.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5164 | 틸트 | init=phi=27° op=회전 45° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 41.70°(입력 대비 3.30°) | want=비등방 1.000(지면 강체) | ISSUE-41: 지면 강체는 유지되나 화면 관측각이 입력각과 불일치(사용자 혼동)
시뮬 SIM-TL5165 | 틸트 | init=phi=27° op=회전 45° [TS-OUTER] → got=지면 비등방 1.1775, 화면 관측각 45.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5166 | 틸트 | init=phi=27° op=회전 90° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 90.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5167 | 틸트 | init=phi=27° op=회전 90° [TS-OUTER] → got=지면 비등방 1.2596, 화면 관측각 90.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5168 | 틸트 | init=phi=27° op=회전 135° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 138.30°(입력 대비 3.30°) | want=비등방 1.000(지면 강체) | ISSUE-41: 지면 강체는 유지되나 화면 관측각이 입력각과 불일치(사용자 혼동)
시뮬 SIM-TL5169 | 틸트 | init=phi=27° op=회전 135° [TS-OUTER] → got=지면 비등방 1.1775, 화면 관측각 135.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5170 | 틸트 | init=phi=35° op=회전 5° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 4.10°(입력 대비 0.90°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5171 | 틸트 | init=phi=35° op=회전 5° [TS-OUTER] → got=지면 비등방 1.0356, 화면 관측각 5.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5172 | 틸트 | init=phi=35° op=회전 15° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 12.38°(입력 대비 2.62°) | want=비등방 1.000(지면 강체) | ISSUE-41: 지면 강체는 유지되나 화면 관측각이 입력각과 불일치(사용자 혼동)
시뮬 SIM-TL5173 | 틸트 | init=phi=35° op=회전 15° [TS-OUTER] → got=지면 비등방 1.1095, 화면 관측각 15.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5174 | 틸트 | init=phi=35° op=회전 45° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 39.32°(입력 대비 5.68°) | want=비등방 1.000(지면 강체) | ISSUE-41: 지면 강체는 유지되나 화면 관측각이 입력각과 불일치(사용자 혼동)
시뮬 SIM-TL5175 | 틸트 | init=phi=35° op=회전 45° [TS-OUTER] → got=지면 비등방 1.3272, 화면 관측각 45.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5176 | 틸트 | init=phi=35° op=회전 90° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 90.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | PASS
시뮬 SIM-TL5177 | 틸트 | init=phi=35° op=회전 90° [TS-OUTER] → got=지면 비등방 1.4903, 화면 관측각 90.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5178 | 틸트 | init=phi=35° op=회전 135° [TS-INNER] → got=지면 비등방 1.0000, 화면 관측각 140.68°(입력 대비 5.68°) | want=비등방 1.000(지면 강체) | ISSUE-41: 지면 강체는 유지되나 화면 관측각이 입력각과 불일치(사용자 혼동)
시뮬 SIM-TL5179 | 틸트 | init=phi=35° op=회전 135° [TS-OUTER] → got=지면 비등방 1.3272, 화면 관측각 135.00°(입력 대비 0.00°) | want=비등방 1.000(지면 강체) | ISSUE-40: 화면공간 회전이 틸트에서 지면 형상 파괴(비강체)
시뮬 SIM-TL5180 | 틸트입력 | init=phi=20° [제스처(핸들 드래그) · 역보정 없음(정답)] → got=WPF 자동 역변환으로 inner 좌표 정합 | want=inner 좌표 정합 | PASS
시뮬 SIM-TL5181 | 틸트입력 | init=phi=20° [제스처(핸들 드래그) · 1/cos 역보정 추가(오답)] → got=이중보정 — 좌표가 1/cos 만큼 어긋남 | want=inner 좌표 정합 | ISSUE-44: 틸트 좌표에 수동 1/cos 역보정 추가 = 이중보정 버그(코드 명시 금지)
시뮬 SIM-TL5182 | 틸트입력 | init=phi=20° [버튼 HUD(각도 입력) · 역보정 없음(정답)] → got=WPF 자동 역변환으로 inner 좌표 정합 | want=inner 좌표 정합 | PASS
시뮬 SIM-TL5183 | 틸트입력 | init=phi=20° [버튼 HUD(각도 입력) · 1/cos 역보정 추가(오답)] → got=이중보정 — 좌표가 1/cos 만큼 어긋남 | want=inner 좌표 정합 | ISSUE-44: 틸트 좌표에 수동 1/cos 역보정 추가 = 이중보정 버그(코드 명시 금지)
시뮬 SIM-TL5184 | 틸트입력 | init=phi=35° [제스처(핸들 드래그) · 역보정 없음(정답)] → got=WPF 자동 역변환으로 inner 좌표 정합 | want=inner 좌표 정합 | PASS
시뮬 SIM-TL5185 | 틸트입력 | init=phi=35° [제스처(핸들 드래그) · 1/cos 역보정 추가(오답)] → got=이중보정 — 좌표가 1/cos 만큼 어긋남 | want=inner 좌표 정합 | ISSUE-44: 틸트 좌표에 수동 1/cos 역보정 추가 = 이중보정 버그(코드 명시 금지)
시뮬 SIM-TL5186 | 틸트입력 | init=phi=35° [버튼 HUD(각도 입력) · 역보정 없음(정답)] → got=WPF 자동 역변환으로 inner 좌표 정합 | want=inner 좌표 정합 | PASS
시뮬 SIM-TL5187 | 틸트입력 | init=phi=35° [버튼 HUD(각도 입력) · 1/cos 역보정 추가(오답)] → got=이중보정 — 좌표가 1/cos 만큼 어긋남 | want=inner 좌표 정합 | ISSUE-44: 틸트 좌표에 수동 1/cos 역보정 추가 = 이중보정 버그(코드 명시 금지)
시뮬 SIM-TL5188 | 게이트 | init=실효줌 15 [틸트 OFF(기본)] → got=틸트 비활성(phi=0) | want=그룹 변환 정책 명시 | PASS
시뮬 SIM-TL5189 | 게이트 | init=실효줌 15 [틸트 ON] → got=틸트 비활성(phi=0) | want=그룹 변환 정책 명시 | PASS
시뮬 SIM-TL5190 | 게이트 | init=실효줌 17 [틸트 OFF(기본)] → got=틸트 비활성(phi=0) | want=그룹 변환 정책 명시 | PASS
시뮬 SIM-TL5191 | 게이트 | init=실효줌 17 [틸트 ON] → got=틸트 비활성(phi=0) | want=그룹 변환 정책 명시 | PASS
시뮬 SIM-TL5192 | 게이트 | init=실효줌 17.5 [틸트 OFF(기본)] → got=틸트 비활성(phi=0) | want=그룹 변환 정책 명시 | PASS
시뮬 SIM-TL5193 | 게이트 | init=실효줌 17.5 [틸트 ON] → got=틸트 비활성(phi=0) | want=그룹 변환 정책 명시 | PASS
시뮬 SIM-TL5194 | 게이트 | init=실효줌 18 [틸트 OFF(기본)] → got=틸트 비활성(phi=0) | want=그룹 변환 정책 명시 | PASS
시뮬 SIM-TL5195 | 게이트 | init=실효줌 18 [틸트 ON] → got=틸트 활성(phi>0) | want=그룹 변환 정책 명시 | ISSUE-43: 그룹 변환과 틸트 활성 대역 교집합 — 차단/역보정/경고 정책 필요
시뮬 SIM-TL5196 | 게이트 | init=실효줌 18.5 [틸트 OFF(기본)] → got=틸트 비활성(phi=0) | want=그룹 변환 정책 명시 | PASS
시뮬 SIM-TL5197 | 게이트 | init=실효줌 18.5 [틸트 ON] → got=틸트 활성(phi>0) | want=그룹 변환 정책 명시 | ISSUE-43: 그룹 변환과 틸트 활성 대역 교집합 — 차단/역보정/경고 정책 필요
시뮬 SIM-TL5198 | 게이트 | init=실효줌 19 [틸트 OFF(기본)] → got=틸트 비활성(phi=0) | want=그룹 변환 정책 명시 | PASS
시뮬 SIM-TL5199 | 게이트 | init=실효줌 19 [틸트 ON] → got=틸트 활성(phi>0) | want=그룹 변환 정책 명시 | ISSUE-43: 그룹 변환과 틸트 활성 대역 교집합 — 차단/역보정/경고 정책 필요
시뮬 SIM-TL5200 | 게이트 | init=실효줌 20 [틸트 OFF(기본)] → got=틸트 비활성(phi=0) | want=그룹 변환 정책 명시 | PASS
시뮬 SIM-TL5201 | 게이트 | init=실효줌 20 [틸트 ON] → got=틸트 활성(phi>0) | want=그룹 변환 정책 명시 | ISSUE-43: 그룹 변환과 틸트 활성 대역 교집합 — 차단/역보정/경고 정책 필요
시뮬 SIM-VF5202 | 뷰프레임 | init=H_view=600 phi=10° [로컬 px 스냅샷] op=세션 중 틸트 전이 → got=전 멤버 4.6px(2.2m @z18) 평행 오차 | want=스냅샷 유효 | ISSUE-45: 로컬 px base 스냅샷이 뷰 프레임 변화(틸트 오버스캔)로 stale
시뮬 SIM-VF5203 | 뷰프레임 | init=H_view=600 phi=10° [투영픽셀/지오 스냅샷] op=세션 중 틸트 전이 → got=프레임 변화에 불변 | want=스냅샷 유효 | PASS
시뮬 SIM-VF5204 | 뷰프레임 | init=H_view=600 phi=20° [로컬 px 스냅샷] op=세션 중 틸트 전이 → got=전 멤버 19.3px(9.1m @z18) 평행 오차 | want=스냅샷 유효 | ISSUE-45: 로컬 px base 스냅샷이 뷰 프레임 변화(틸트 오버스캔)로 stale
시뮬 SIM-VF5205 | 뷰프레임 | init=H_view=600 phi=20° [투영픽셀/지오 스냅샷] op=세션 중 틸트 전이 → got=프레임 변화에 불변 | want=스냅샷 유효 | PASS
시뮬 SIM-VF5206 | 뷰프레임 | init=H_view=600 phi=35° [로컬 px 스냅샷] op=세션 중 틸트 전이 → got=전 멤버 66.2px(31.4m @z18) 평행 오차 | want=스냅샷 유효 | ISSUE-45: 로컬 px base 스냅샷이 뷰 프레임 변화(틸트 오버스캔)로 stale
시뮬 SIM-VF5207 | 뷰프레임 | init=H_view=600 phi=35° [투영픽셀/지오 스냅샷] op=세션 중 틸트 전이 → got=프레임 변화에 불변 | want=스냅샷 유효 | PASS
시뮬 SIM-VF5208 | 뷰프레임 | init=H_view=800 phi=10° [로컬 px 스냅샷] op=세션 중 틸트 전이 → got=전 멤버 6.2px(2.9m @z18) 평행 오차 | want=스냅샷 유효 | ISSUE-45: 로컬 px base 스냅샷이 뷰 프레임 변화(틸트 오버스캔)로 stale
시뮬 SIM-VF5209 | 뷰프레임 | init=H_view=800 phi=10° [투영픽셀/지오 스냅샷] op=세션 중 틸트 전이 → got=프레임 변화에 불변 | want=스냅샷 유효 | PASS
시뮬 SIM-VF5210 | 뷰프레임 | init=H_view=800 phi=20° [로컬 px 스냅샷] op=세션 중 틸트 전이 → got=전 멤버 25.7px(12.2m @z18) 평행 오차 | want=스냅샷 유효 | ISSUE-45: 로컬 px base 스냅샷이 뷰 프레임 변화(틸트 오버스캔)로 stale
시뮬 SIM-VF5211 | 뷰프레임 | init=H_view=800 phi=20° [투영픽셀/지오 스냅샷] op=세션 중 틸트 전이 → got=프레임 변화에 불변 | want=스냅샷 유효 | PASS
시뮬 SIM-VF5212 | 뷰프레임 | init=H_view=800 phi=35° [로컬 px 스냅샷] op=세션 중 틸트 전이 → got=전 멤버 88.3px(41.9m @z18) 평행 오차 | want=스냅샷 유효 | ISSUE-45: 로컬 px base 스냅샷이 뷰 프레임 변화(틸트 오버스캔)로 stale
시뮬 SIM-VF5213 | 뷰프레임 | init=H_view=800 phi=35° [투영픽셀/지오 스냅샷] op=세션 중 틸트 전이 → got=프레임 변화에 불변 | want=스냅샷 유효 | PASS
시뮬 SIM-VF5214 | 뷰프레임 | init=H_view=1080 phi=10° [로컬 px 스냅샷] op=세션 중 틸트 전이 → got=전 멤버 8.3px(4.0m @z18) 평행 오차 | want=스냅샷 유효 | ISSUE-45: 로컬 px base 스냅샷이 뷰 프레임 변화(틸트 오버스캔)로 stale
시뮬 SIM-VF5215 | 뷰프레임 | init=H_view=1080 phi=10° [투영픽셀/지오 스냅샷] op=세션 중 틸트 전이 → got=프레임 변화에 불변 | want=스냅샷 유효 | PASS
시뮬 SIM-VF5216 | 뷰프레임 | init=H_view=1080 phi=20° [로컬 px 스냅샷] op=세션 중 틸트 전이 → got=전 멤버 34.7px(16.4m @z18) 평행 오차 | want=스냅샷 유효 | ISSUE-45: 로컬 px base 스냅샷이 뷰 프레임 변화(틸트 오버스캔)로 stale
시뮬 SIM-VF5217 | 뷰프레임 | init=H_view=1080 phi=20° [투영픽셀/지오 스냅샷] op=세션 중 틸트 전이 → got=프레임 변화에 불변 | want=스냅샷 유효 | PASS
시뮬 SIM-VF5218 | 뷰프레임 | init=H_view=1080 phi=35° [로컬 px 스냅샷] op=세션 중 틸트 전이 → got=전 멤버 119.2px(56.6m @z18) 평행 오차 | want=스냅샷 유효 | ISSUE-45: 로컬 px base 스냅샷이 뷰 프레임 변화(틸트 오버스캔)로 stale
시뮬 SIM-VF5219 | 뷰프레임 | init=H_view=1080 phi=35° [투영픽셀/지오 스냅샷] op=세션 중 틸트 전이 → got=프레임 변화에 불변 | want=스냅샷 유효 | PASS
시뮬 SIM-VF5220 | 뷰프레임 | init=휠 줌 [로컬 px 스냅샷 · 가드 없음] → got=base 좌표 기준 이동 → 그룹 전체 평행 오차 커밋 | want=안전 | ISSUE-46: 뷰 프레임 변화 무가드 — 줌/팬/리사이즈/틸트 공통
시뮬 SIM-VF5221 | 뷰프레임 | init=휠 줌 [로컬 px 스냅샷 · 세션 무효화 가드] → got=안전 | want=안전 | PASS
시뮬 SIM-VF5222 | 뷰프레임 | init=휠 줌 [투영픽셀/지오 스냅샷 · 가드 없음] → got=안전 | want=안전 | PASS
시뮬 SIM-VF5223 | 뷰프레임 | init=휠 줌 [투영픽셀/지오 스냅샷 · 세션 무효화 가드] → got=안전 | want=안전 | PASS
시뮬 SIM-VF5224 | 뷰프레임 | init=팬 드래그 [로컬 px 스냅샷 · 가드 없음] → got=base 좌표 기준 이동 → 그룹 전체 평행 오차 커밋 | want=안전 | ISSUE-46: 뷰 프레임 변화 무가드 — 줌/팬/리사이즈/틸트 공통
시뮬 SIM-VF5225 | 뷰프레임 | init=팬 드래그 [로컬 px 스냅샷 · 세션 무효화 가드] → got=안전 | want=안전 | PASS
시뮬 SIM-VF5226 | 뷰프레임 | init=팬 드래그 [투영픽셀/지오 스냅샷 · 가드 없음] → got=안전 | want=안전 | PASS
시뮬 SIM-VF5227 | 뷰프레임 | init=팬 드래그 [투영픽셀/지오 스냅샷 · 세션 무효화 가드] → got=안전 | want=안전 | PASS
시뮬 SIM-VF5228 | 뷰프레임 | init=창 리사이즈 [로컬 px 스냅샷 · 가드 없음] → got=base 좌표 기준 이동 → 그룹 전체 평행 오차 커밋 | want=안전 | ISSUE-46: 뷰 프레임 변화 무가드 — 줌/팬/리사이즈/틸트 공통
시뮬 SIM-VF5229 | 뷰프레임 | init=창 리사이즈 [로컬 px 스냅샷 · 세션 무효화 가드] → got=안전 | want=안전 | PASS
시뮬 SIM-VF5230 | 뷰프레임 | init=창 리사이즈 [투영픽셀/지오 스냅샷 · 가드 없음] → got=안전 | want=안전 | PASS
시뮬 SIM-VF5231 | 뷰프레임 | init=창 리사이즈 [투영픽셀/지오 스냅샷 · 세션 무효화 가드] → got=안전 | want=안전 | PASS
시뮬 SIM-VF5232 | 뷰프레임 | init=틸트 히스테리시스 자동 전이 [로컬 px 스냅샷 · 가드 없음] → got=base 좌표 기준 이동 → 그룹 전체 평행 오차 커밋 | want=안전 | ISSUE-46: 뷰 프레임 변화 무가드 — 줌/팬/리사이즈/틸트 공통
시뮬 SIM-VF5233 | 뷰프레임 | init=틸트 히스테리시스 자동 전이 [로컬 px 스냅샷 · 세션 무효화 가드] → got=안전 | want=안전 | PASS
시뮬 SIM-VF5234 | 뷰프레임 | init=틸트 히스테리시스 자동 전이 [투영픽셀/지오 스냅샷 · 가드 없음] → got=안전 | want=안전 | PASS
시뮬 SIM-VF5235 | 뷰프레임 | init=틸트 히스테리시스 자동 전이 [투영픽셀/지오 스냅샷 · 세션 무효화 가드] → got=안전 | want=안전 | PASS
시뮬 SIM-G5236 | 정책공백 | GAP-1 | 점 심볼 '확대/축소' 의미: 위치 산개(지리) / 아이콘 px 크기 / 둘 다 | GAP → 사용자 결정
시뮬 SIM-G5237 | 정책공백 | GAP-2 | 회전 시 점 심볼 Bearing 동반 회전(강체) 여부 — 카메라 FOV·PIDS 방위 포함 | GAP → 사용자 결정
시뮬 SIM-G5238 | 정책공백 | GAP-3 | 피벗: 화면 bbox 중심(어도너 박스 일치) vs 지리 bbox 중심 vs 도심 | GAP → 사용자 결정
시뮬 SIM-G5239 | 정책공백 | GAP-4 | 잠금(IsLocked) 멤버 제외 확정 + 중심 계산 포함 여부 (코드/주석 모순 GMapCustomControl.cs:984 vs 1000) | GAP → 사용자 결정
시뮬 SIM-G5240 | 정책공백 | GAP-5 | 회전 UI: 15° 스텝 + 자유 입력, 단일 회전의 '역행 금지' 규약 승계 여부 | GAP → 사용자 결정
시뮬 SIM-G5241 | 정책공백 | GAP-6 | 라벨 오프셋 동반 처리(점=px 화면고정 / 라인=비율 익스텐트 추종) | GAP → 사용자 결정
시뮬 SIM-G5242 | 정책공백 | GAP-7 | 래스터 업스케일 정책: 무제한 / 경고(>1x) / 캡(>2x 금지) | GAP → 사용자 결정
시뮬 SIM-G5243 | 정책공백 | GAP-8 | 부분 저장 실패: 롤백 / 재시도 / 불일치 허용 | GAP → 사용자 결정
시뮬 SIM-G5244 | 정책공백 | GAP-9 | '전체 선택' 입구: 러버밴드 단일 / Ctrl+A를 그룹 퍼널로 재배선(현재 죽은 코드) | GAP → 사용자 결정
시뮬 SIM-G5245 | 정책공백 | GAP-10 | 이미지 오버레이 마커 포함 여부 — 포함 시 AABB+Rotation 별도 경로 + px/도 판별 함정 | GAP → 사용자 결정
시뮬 SIM-G5246 | 정책공백 | GAP-11 | 맵 회전 θ·앵커 회전잠금과 그룹 회전의 축 관계 | GAP → 사용자 결정
시뮬 SIM-G5247 | 정책공백 | GAP-12 | 라인/구역 정점 전량 재기록(LinePoints/PidsGroupPoints DELETE+INSERT) 비용 감당 범위 | GAP → 사용자 결정
시뮬 SIM-G5248 | 정책공백 | GAP-13 | ★진리값 정본: 지면 강체(측지) vs 화면 강체(메르카토르) — 두 관점이 상충(적대검증 L2) | GAP → 사용자 결정
시뮬 SIM-G5249 | 정책공백 | GAP-14 | 기존 그룹 속성창(절대·균일 일괄)과 신규 상대 변환의 우선순위·비활성화 규약 | GAP → 사용자 결정
시뮬 SIM-G5250 | 정책공백 | GAP-15 | Symbols에 MapId 부재(전 맵 공유) — 전역 변환 허용 vs MapId 컬럼 신설 | GAP → 사용자 결정
시뮬 SIM-G5251 | 정책공백 | GAP-16 | 틸트 ON 시 그룹 변환: 차단 vs 허용+배지 — (c)outer 기준은 코드가 금지(이중보정)라 선택지 아님. 틸트 대역[18.0~20.5]이 편집 대역과 거의 전부 겹쳐 차단 시 기능 상시 잠금 | GAP → 사용자 결정
시뮬 SIM-G5252 | 정책공백 | GAP-21 | ★빌보드 예외의 분기 키: 렌더 플래그(IsBillboard) vs 데이터(장비/모델) 기준 — Symbol3DFeature 가 설치별 플래그(개발 ON/배포 OFF)라 렌더 기준이면 같은 장비가 설치본마다 다른 DB 를 쓴다 | GAP → 사용자 결정
시뮬 SIM-G5253 | 정책공백 | GAP-22 | ★base 스냅샷 좌표계: 컨트롤 로컬 px vs 투영픽셀/지오 — 로컬 px 은 뷰 프레임 변화(틸트 오버스캔·줌·팬·리사이즈)에 stale | GAP → 사용자 결정
시뮬 SIM-G5254 | 정책공백 | GAP-17 | 3D 하우징 심볼이 그룹 스케일 대상인가 — 2D W/H 스케일이 3D 모델에 주는 영향(별도 정찰 B2 결과 반영) | GAP → 사용자 결정
시뮬 SIM-G5255 | 정책공백 | GAP-18 | PidsGroup 정점 회전 시 3D 철망/통문/함체 재생성 트리거 — 자동 추종 vs 명시 재생성 호출 | GAP → 사용자 결정

총 5255 시뮬 | PASS 3069 | ISSUE 2166 | GAP 20
  ISSUE-1: 912건
  ISSUE-11: 15건
  ISSUE-12: 6건
  ISSUE-13: 1건
  ISSUE-14: 1건
  ISSUE-15: 1건
  ISSUE-16: 1건
  ISSUE-17: 1건
  ISSUE-18: 1건
  ISSUE-19: 1건
  ISSUE-2: 477건
  ISSUE-20: 8건
  ISSUE-21: 1건
  ISSUE-22: 1건
  ISSUE-23: 1건
  ISSUE-24: 1건
  ISSUE-25: 1건
  ISSUE-26: 84건
  ISSUE-27: 285건
  ISSUE-28: 183건
  ISSUE-29: 4건
  ISSUE-3: 69건
  ISSUE-30: 1건
  ISSUE-31: 1건
  ISSUE-32: 1건
  ISSUE-33: 1건
  ISSUE-34: 1건
  ISSUE-35: 2건
  ISSUE-36: 1건
  ISSUE-37: 1건
  ISSUE-38: 1건
  ISSUE-39: 1건
  ISSUE-40: 20건
  ISSUE-41: 8건
  ISSUE-43: 4건
  ISSUE-44: 4건
  ISSUE-45: 9건
  ISSUE-46: 4건
  ISSUE-5: 16건
  ISSUE-8: 25건
  ISSUE-9: 10건
```
