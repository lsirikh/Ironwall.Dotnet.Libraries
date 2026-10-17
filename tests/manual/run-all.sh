#!/usr/bin/env bash
# tests/manual/run-all.sh — 모든 시나리오 일괄 실행
# IMPL-10 (FR-10)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

TOTAL_PASS=0
TOTAL_FAIL=0
SCRIPT_COUNT=0
FAILED_SCRIPTS=()

echo ""
echo "════════════════════════════════════════════════════════"
echo -e "  ${BLUE}시나리오 회귀 테스트 러너${NC}"
echo "════════════════════════════════════════════════════════"
echo ""

for script in "$SCRIPT_DIR"/*.sh; do
  name=$(basename "$script" .sh)
  # 헬퍼/러너/self-test 제외
  case "$name" in
    _helpers|run-all|_self-test) continue ;;
  esac

  SCRIPT_COUNT=$((SCRIPT_COUNT + 1))
  echo -e "${BLUE}▶${NC} $name"

  output=$(bash "$script" 2>&1)
  rc=$?

  # PASS/FAIL 카운트 (assert 함수 출력 기반)
  pass=$(echo "$output" | grep -c "PASS")
  fail=$(echo "$output" | grep -c "FAIL")

  TOTAL_PASS=$((TOTAL_PASS + pass))
  TOTAL_FAIL=$((TOTAL_FAIL + fail))

  if [ $fail -gt 0 ]; then
    FAILED_SCRIPTS+=("$name")
    echo -e "  ${RED}✗${NC} $pass pass, $fail fail"
    echo "$output" | grep "FAIL" | head -3 | sed 's/^/    /'
  else
    echo -e "  ${GREEN}✓${NC} $pass pass"
  fi
done

echo ""
echo "════════════════════════════════════════════════════════"
echo -e "  ${BLUE}TOTAL${NC}: scripts=$SCRIPT_COUNT  ${GREEN}PASS=$TOTAL_PASS${NC}  ${RED}FAIL=$TOTAL_FAIL${NC}"
if [ ${#FAILED_SCRIPTS[@]} -gt 0 ]; then
  echo -e "  ${RED}실패 스크립트${NC}: ${FAILED_SCRIPTS[*]}"
fi
echo "════════════════════════════════════════════════════════"
echo ""

exit $TOTAL_FAIL
