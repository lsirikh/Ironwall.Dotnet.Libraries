// 수정 전/후 증명용 — node truth_eval.mjs <map-3d 폴더> <truth.json…>
//   그 폴더의 뷰어 enuOf · sceneYaw · makeHeightField 로 진실값(test_rotated_truth.py 가 만든 것)을 잰다: 오버레이 자리 vs GLB 지형/건물(±0.5 m).
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";

const [repo, ...files] = process.argv.slice(2);
const cs = await import(pathToFileURL(`${repo}/map3d/static/camsync.js`).href);
const og = await import(pathToFileURL(`${repo}/map3d/static/overlay-geom.js`).href);
let bad = 0;
for (const f of files) {
  const t = JSON.parse(readFileSync(f, "utf-8"));
  const yaw = cs.sceneYaw(t.scene), c = Math.cos(yaw), s = Math.sin(yaw);
  const world = ([x, y, z]) => ({ x: x * c + z * s, y, z: -x * s + z * c });
  let worst = 0, what = "";
  for (const p of t.samples) {
    const w = world(p.local), e = cs.enuOf(t.scene.origin, p.lon, p.lat), d = Math.hypot(w.x - e.x, w.z - e.z);
    if (d > worst) { worst = d; what = p.what; }
  }
  const pos = new Float32Array(t.terrain.length);
  for (let i = 0; i < t.terrain.length; i += 3) { const w = world([t.terrain[i], t.terrain[i + 1], t.terrain[i + 2]]); pos[i] = w.x; pos[i + 1] = w.y; pos[i + 2] = w.z; }
  const hf = og.makeHeightField(pos, t.grid[0], t.grid[1], yaw);
  let worstH = 0;
  for (const h of t.heights) { const e = cs.enuOf(t.scene.origin, h.lon, h.lat); worstH = Math.max(worstH, Math.abs(hf.sample(e.x, e.z) - h.y)); }
  const pass = worst <= 0.5 && worstH <= 0.1;
  if (!pass) bad++;
  console.log(`${t.name.padEnd(8)} 오버레이↔지형 최대 ${worst.toFixed(3)} m (${what}) · 높이표 최대 ${worstH.toFixed(3)} m → ${pass ? "통과" : "실패"}`);
}
process.exit(bad ? 1 : 0);
