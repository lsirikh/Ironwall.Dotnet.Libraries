# -*- coding: utf-8 -*-
"""
시나리오 검증 상태표 — 시험 실행 기록(TRX)만으로 만든다(사람 · 에이전트가 손으로 '확인' 표시를 하지 않는다).

연결 고리
  1. 시나리오 목록(카탈로그 md)의 id(MAP-052 …)
  2. 시험 코드의 표지 [Trait("Scenario", "MAP-052")] · [TestProperty("Scenario", "MAP-052")]
     (쉼표로 여러 개). 표지가 없던 옛 시험은 카탈로그 마지막 칸의 Class::method 참조로 잇는다.
  3. 증거 폴더의 실행 기록: <evidence>/<run>/meta.json(실행 시각 · 두 저장소 커밋 · 명령) + *.trx(+ SHA-256)
     헤디드 시험은 표준 출력에 'EVIDENCE: <그림 경로>' 를 남기면 그 그림과 해시를 증거에 붙인다.

상태(시나리오마다, 이어진 시험들의 '가장 최근' 결과로 정한다)
  검증됨      — 이어진 시험이 모두 최근 실행에서 통과, 그 실행이 지금 커밋에서 돌았다
  검증됨(옛)  — 모두 통과했지만 그 뒤 코드가 바뀌었다(다시 돌려야 '검증됨')
  실패        — 이어진 시험 중 하나라도 최근 실행에서 실패
  미실행      — 시험은 있는데 증거 폴더에 실행 기록이 없다
  시험 없음   — 이어진 시험이 없다

사용: python collect.py --catalog <md> --repo <lib> --repo <host> --evidence <dir> --out <dir>
"""
import argparse, glob, hashlib, html, json, os, re, subprocess, sys
import xml.etree.ElementTree as ET
from datetime import datetime

ID_RE = re.compile(r"\b([A-Z]{2,4}-\d{3}[A-Z]?)\b")
ROW_RE = re.compile(r"^\|\s*([A-Z]{2,4}-\d{3}[A-Z]?)\s*\|")
SCEN_ATTR_RE = re.compile(r'\[(?:[\w.]*\.)?(?:Trait|TestProperty)\(\s*"Scenario"\s*,\s*"([^"]+)"\s*\)\s*\]')
METHOD_RE = re.compile(r"\b(?:public|internal|private|protected)\s+(?:static\s+)?(?:async\s+)?[\w<>\[\],.? ]+?\s+(\w+)\s*\(")
CLASS_RE = re.compile(r"\b(?:class|record)\s+(\w+)")
NS_RE = re.compile(r"^\s*namespace\s+([\w.]+)")
SKIP_DIRS = {"bin", "obj", ".harness-worktrees", "node_modules", ".git", "worktrees"}
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def git_head(repo):
    try:
        return subprocess.check_output(["git", "-C", repo, "rev-parse", "HEAD"], text=True).strip()
    except Exception:
        return ""


def git_changed_since(repo, sha):
    """sha 이후 바뀐 파일이 있는가(커밋 기준)."""
    if not sha:
        return True
    try:
        out = subprocess.check_output(["git", "-C", repo, "rev-list", "--count", f"{sha}..HEAD"], text=True).strip()
        return out != "0"
    except Exception:
        return True


def parse_catalog(path):
    rows, section = [], ""
    with open(path, encoding="utf-8-sig") as f:
        for line in f:
            if line.startswith("## ") or line.startswith("### "):
                section = line.strip("# \n")
            m = ROW_RE.match(line)
            if not m:
                continue
            cells = [c.strip() for c in line.strip().strip("|").split(" | ")]
            rows.append({
                "id": m.group(1),
                "section": section,
                "area": cells[1] if len(cells) > 1 else "",
                "action": cells[2] if len(cells) > 2 else "",
                "expect": cells[3] if len(cells) > 3 else "",
                "refs": cells[-1] if len(cells) > 4 else "",
            })
    return rows


def scan_sources(repos):
    """시험 메서드 → 시나리오 id 들. 키는 'Class.method'(이름 공간 없이 — TRX 와 맞추기 쉽게)."""
    links = {}
    for repo in repos:
        for root, dirs, files in os.walk(repo):
            dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
            for fn in files:
                if not fn.endswith(".cs"):
                    continue
                p = os.path.join(root, fn)
                try:
                    text = open(p, encoding="utf-8-sig", errors="replace").read()
                except Exception:
                    continue
                if "Scenario" not in text:
                    continue
                cls, pending = "", []
                for line in text.splitlines():
                    cm = CLASS_RE.search(line)
                    if cm and "(" not in line.split(cm.group(0))[0]:
                        cls = cm.group(1)
                    for am in SCEN_ATTR_RE.finditer(line):
                        pending += [s.strip() for s in am.group(1).split(",") if s.strip()]
                    if pending:
                        mm = METHOD_RE.search(line)
                        if mm and not SCEN_ATTR_RE.search(line):
                            key = f"{cls}.{mm.group(1)}"
                            links.setdefault(key, {"ids": set(), "file": os.path.relpath(p, repo), "repo": repo})
                            links[key]["ids"].update(pending)
                            pending = []
    return links


def parse_trx(path, meta):
    out = []
    try:
        tree = ET.parse(path)
    except ET.ParseError:
        return out
    r = tree.getroot()
    defs = {}
    for ut in r.iterfind(".//t:UnitTest", NS):
        tm = ut.find("t:TestMethod", NS)
        if tm is None:
            continue
        cls = tm.get("className", "").split(",")[0].split(".")[-1]
        defs[ut.get("id")] = f"{cls}.{tm.get('name')}"
    for res in r.iterfind(".//t:UnitTestResult", NS):
        key = defs.get(res.get("testId"), "")
        stdout = res.findtext("t:Output/t:StdOut", default="", namespaces=NS) or ""
        shots = [s.strip() for s in re.findall(r"EVIDENCE:\s*(\S+\.(?:png|jpg))", stdout)]
        out.append({
            "test": key,
            "name": res.get("testName", ""),
            "outcome": res.get("outcome", ""),
            "start": res.get("startTime", ""),
            "duration": res.get("duration", ""),
            "computer": res.get("computerName", ""),
            "trx": path,
            "run": meta.get("run_id", os.path.basename(os.path.dirname(path))),
            "shas": meta.get("shas", {}),
            "shots": shots,
        })
    return out


def load_runs(evidence):
    results, runs = [], []
    for meta_path in sorted(glob.glob(os.path.join(evidence, "*", "meta.json"))):
        run_dir = os.path.dirname(meta_path)
        meta = json.load(open(meta_path, encoding="utf-8-sig"))
        meta["run_id"] = meta.get("run_id") or os.path.basename(run_dir)
        trxs = sorted(glob.glob(os.path.join(run_dir, "**", "*.trx"), recursive=True))
        meta["trx"] = [{"path": t, "sha256": sha256(t)} for t in trxs]
        runs.append(meta)
        for t in trxs:
            results += parse_trx(t, meta)
    return results, runs


def legacy_refs(refs_cell):
    """카탈로그 마지막 칸의 Class::method 참조."""
    return [f"{a}.{b}" for a, b in re.findall(r"(\w+)::(\w+)", refs_cell)]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--catalog", required=True)
    ap.add_argument("--repo", action="append", required=True)
    ap.add_argument("--evidence", required=True)
    ap.add_argument("--out", required=True)
    a = ap.parse_args()

    rows = parse_catalog(a.catalog)
    links = scan_sources(a.repo)
    results, runs = load_runs(a.evidence)
    heads = {os.path.basename(r.rstrip("/\\")): git_head(r) for r in a.repo}

    by_test = {}
    for res in results:
        by_test.setdefault(res["test"], []).append(res)

    def latest(test_key):
        rs = by_test.get(test_key, [])
        if not rs:
            return None
        last_run = max(rs, key=lambda y: y["start"])["run"]
        return [x for x in rs if x["run"] == last_run]   # 이론(Theory) 행이 여러 개면 그 실행의 행 전부

    scen_tests = {r["id"]: set() for r in rows}
    for key, info in links.items():
        for sid in info["ids"]:
            scen_tests.setdefault(sid, set()).add(key)
    for r in rows:
        for key in legacy_refs(r["refs"]):
            scen_tests[r["id"]].add(key)

    table = []
    for r in rows:
        tests = sorted(scen_tests.get(r["id"], []))
        evid, status = [], "시험 없음"
        if tests:
            outcomes, stale = [], False
            for t in tests:
                batch = latest(t)
                if not batch:
                    outcomes.append("NotRun")
                    continue
                ok = all(x["outcome"] == "Passed" for x in batch)
                outcomes.append("Passed" if ok else ("NotExecuted" if all(x["outcome"] == "NotExecuted" for x in batch) else "Failed"))
                x0 = batch[0]
                for repo_name, sha in x0["shas"].items():
                    repo_path = next((rp for rp in a.repo if os.path.basename(rp.rstrip("/\\")) == repo_name), None)
                    if repo_path and git_changed_since(repo_path, sha):
                        stale = True
                mode = "헤디드" if "UiTests" in (links.get(t, {}).get("file", "") + x0["trx"]) else "헤드리스"
                evid.append({"test": t, "mode": mode, "outcome": outcomes[-1], "run": x0["run"], "start": x0["start"],
                             "trx": x0["trx"], "computer": x0["computer"], "shots": sorted({s for x in batch for s in x["shots"]})})
            if "Failed" in outcomes:
                status = "실패"
            elif all(o == "Passed" for o in outcomes):
                status = "검증됨(옛)" if stale else "검증됨"
            elif all(o in ("NotRun", "NotExecuted") for o in outcomes):
                status = "미실행"
            else:
                status = "일부만 실행"
        table.append({**r, "tests": tests, "status": status, "evidence": evid})

    os.makedirs(a.out, exist_ok=True)
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    summary = {}
    for t in table:
        s = summary.setdefault(t["id"].split("-")[0], {})
        s[t["status"]] = s.get(t["status"], 0) + 1
    data = {"generated": now, "heads": heads, "runs": runs, "summary": summary, "scenarios": table}
    with open(os.path.join(a.out, "scenario-status.json"), "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1, default=list)

    order = ["검증됨", "검증됨(옛)", "실패", "일부만 실행", "미실행", "시험 없음"]
    lines = [f"# 시나리오 검증 상태표", "",
             f"- 만든 시각: {now} · 이 표는 `tools/scenario-evidence/collect.py` 가 실행 기록(TRX)만으로 만든다 — 손으로 고치지 않는다.",
             f"- 지금 커밋: " + " · ".join(f"{k} `{v[:8]}`" for k, v in heads.items()),
             f"- 실행 기록 {len(runs)}회 · TRX {sum(len(r['trx']) for r in runs)}개", "",
             "## 요약", "", "| 영역 | " + " | ".join(order) + " | 합계 |", "|---|" + "---|" * (len(order) + 1)]
    tot = {k: 0 for k in order}
    for area in sorted(summary):
        c = summary[area]
        lines.append(f"| {area} | " + " | ".join(str(c.get(k, 0)) for k in order) + f" | {sum(c.values())} |")
        for k in order:
            tot[k] += c.get(k, 0)
    lines.append("| **합계** | " + " | ".join(f"**{tot[k]}**" for k in order) + f" | **{sum(tot.values())}** |")
    lines += ["", "## 시나리오", "", "| 상태 | id | 영역 | 기대 | 시험 · 증거 |", "|---|---|---|---|---|"]
    mark = {"검증됨": "✅", "검증됨(옛)": "🟨", "실패": "❌", "일부만 실행": "🟧", "미실행": "⬜", "시험 없음": "·"}
    for t in table:
        ev = "<br>".join(
            f"{e['outcome']} [{e['mode']}] `{e['test']}` · {e['start'][:16]} · run `{e['run']}`" + (f" · 그림 {len(e['shots'])}" if e["shots"] else "")
            for e in t["evidence"]) or ("<br>".join(f"`{x}`" for x in t["tests"]) if t["tests"] else "")
        lines.append(f"| {mark[t['status']]} {t['status']} | {t['id']} | {t['area']} | {t['expect']} | {ev} |")
    with open(os.path.join(a.out, "scenario-status.md"), "w", encoding="utf-8-sig") as f:
        f.write("\n".join(lines) + "\n")
    print(json.dumps({"scenarios": len(table), "tests_linked": sum(1 for t in table if t["tests"]), "summary": tot}, ensure_ascii=False))


if __name__ == "__main__":
    main()
