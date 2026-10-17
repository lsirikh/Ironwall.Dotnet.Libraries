'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),os=require('node:os');
const {Store}=require('../../.claude/hooks/_harness-store'),{ModelConfig,defaults}=require('../../.claude/hooks/_model-config');
const dir=fs.mkdtempSync(path.join(os.tmpdir(),'harness-v14-models-')),s=new Store(path.join(dir,'db.sqlite')),m=new ModelConfig(s);let count=0;
function check(name,fn){fn();console.log('PASS '+name);count++;}
check('B uses current model with no extra heavy role calls',()=>assert.equal(m.resolve({role:'review',track:'B',currentRuntime:'codex'}).model,'current'));
check('fresh projects receive runtime defaults without storing a migration',()=>{
 const fresh=m.read();assert.equal(fresh.version,0);assert.equal(fresh.value.launchers.codex.roles.implementation.runtime,'codex');assert.equal(s.get('models/config').value,null);
});
check('legacy global settings migrate without losing models skills or verification policy',()=>{
 const legacy=defaults();legacy.roles.review={runtime:'claude',model:'opus-custom',effort:'xhigh',require_model:true};legacy.roles.implementation={runtime:'codex',model:'codex-custom',effort:'high',require_model:false};legacy.skills.special={runtime:'claude',model:'sonnet-custom',effort:'medium',require_model:true};
 const fixture=new Store(path.join(dir,'migration.sqlite'));try{
  fixture.transaction(tx=>tx.put('models/config',legacy));const config=new ModelConfig(fixture),read=config.read();
  assert.equal(read.version,1);assert.equal(fixture.get('models/config').value.schema_version,1);
  for(const runtime of ['claude','codex']){assert.deepEqual(read.value.launchers[runtime].roles,legacy.roles);assert.deepEqual({...read.value.launchers[runtime].skills},legacy.skills);const resolved=config.resolve({role:'review',currentRuntime:runtime});assert.equal(resolved.model,'opus-custom');assert.equal(resolved.require_model,true);}
  read.value.launchers.codex.roles.review.model='codex-side-custom';config.save(read.value,1);assert.equal(config.read().value.launchers.claude.roles.review.model,'opus-custom');assert.equal(config.read().value.launchers.codex.roles.review.model,'codex-side-custom');
 }finally{fixture.close();}
});
const one=m.save(defaults(),0,'one');
check('concurrent stale settings do not overwrite',()=>assert.throws(()=>m.save(defaults('lean'),0),/VERSION_CONFLICT/));
check('retried save is idempotent',()=>assert.equal(m.save(defaults(),0,'one').version,one.version));
check('reused request ID cannot smuggle a different change',()=>assert.throws(()=>m.save(defaults('lean'),0,'one'),/OPERATION_CONFLICT/));
const frozen=m.resolve({role:'documentation',currentRuntime:'codex'});m.save(defaults('lean'),1);
check('running node profile stays frozen while next node changes',()=>{assert.equal(frozen.model,'sonnet');assert.equal(m.resolve({role:'documentation',currentRuntime:'codex'}).model,'current');});
check('rollback restores a historical config as a new revision',()=>{assert.equal(m.rollback(1,2).version,3);assert.equal(m.read().value.roles.documentation.model,'sonnet');});
check('unknown Fable identifier is user configured, not substituted',()=>{const v=m.read().value;v.skills.custom={runtime:'claude',model:'fable-provider/model-v1',effort:'low',require_model:true};m.save(v,3);assert.equal(m.resolve({skill:'custom'}).model,'fable-provider/model-v1');});
check('secrets are never accepted by connection schema',()=>{const v=defaults();v.connections.claude.api_key='not-a-real-key';assert.throws(()=>m.save(v,4),/CONNECTION_SECRETS_FORBIDDEN/);});
check('malformed model and notification settings rejected',()=>{for(const bad of ['--model other','bad\nvalue']){const v=defaults();v.roles.analysis.model=bad;assert.throws(()=>m.save(v,4),/INVALID_MODEL/);}const v=defaults();v.notice_minutes=-1;assert.throws(()=>m.save(v,4),/INVALID_NOTICE_INTERVAL/);});
check('each runtime retains model effort and provenance policy after save and reopen',()=>{
 const value=m.read().value,b={runtime:'claude',model:'opus',effort:'xhigh',require_model:true,runtime_settings:{claude:{model:'opus',effort:'xhigh',require_model:true},codex:{model:'codex-fixture',effort:'high',require_model:false}}};
 value.roles.analysis=b;value.launchers.claude.roles.analysis=b;m.save(value,m.read().version);
 const other=new Store(path.join(dir,'db.sqlite'));try{const saved=new ModelConfig(other).read().value.launchers.claude.roles.analysis;assert.equal(saved.model,'opus');assert.equal(saved.runtime_settings.codex.model,'codex-fixture');assert.equal(saved.runtime_settings.claude.effort,'xhigh');assert.equal(saved.runtime_settings.codex.require_model,false);}finally{other.close();}
});
check('selected runtime is executed while the other runtime settings remain saved',()=>{
 const value=m.read().value,old=value.launchers.claude.roles.analysis;
 value.launchers.claude.roles.analysis={...old,runtime:'codex',...old.runtime_settings.codex};m.save(value,m.read().version);
 const profile=m.resolve({role:'analysis',currentRuntime:'claude'});assert.equal(profile.runtime,'codex');assert.equal(profile.model,'codex-fixture');assert.equal(profile.runtime_settings.claude.model,'opus');
});
check('invalid stored runtime choices are rejected even when not active',()=>{
 for(const settings of [{other:{model:'x',effort:'high',require_model:false}},{codex:{model:'--bad',effort:'high',require_model:false}},{codex:{model:'x',effort:'high',require_model:false,runtime_settings:{}}}]){
  const value=m.read().value;value.launchers.claude.roles.analysis.runtime_settings=settings;assert.throws(()=>m.save(value,m.read().version),/INVALID_RUNTIME_SETTINGS|INVALID_MODEL/);
 }
});
// [v16/N-01] 공식 스펙과의 정합은 다짐이 아니라 테스트가 지킨다. 우리는 Claude CLI 에 직접 결합돼
//   있고, 플래그·별칭·effort 중 하나만 바뀌어도 조용히 깨진다. 실제로 Codex 쪽에서는 그 사이 훅
//   체계가 새로 생겨 봉투 하나의 전제가 통째로 무너졌다. 사람이 조사해야만 아는 구조를 없앤다.
const {spawnSync}=require('node:child_process');
const {executableCandidates}=require('../../.claude/hooks/_ui-server');
const CLAUDE_FLAGS=['-p','--safe-mode','--strict-mcp-config','--tools','--permission-mode','--output-format','--verbose','--no-session-persistence','--model','--effort'];
const CLAUDE_EFFORTS=['low','medium','high','xhigh','max'];
const claudeHelp=(()=>{
 const exe=executableCandidates('claude')[0];if(!exe)return null;
 const r=spawnSync(exe,['--help'],{encoding:'utf8',windowsHide:true,timeout:60000});
 const text=(r.stdout||'')+(r.stderr||'');return text.length>500?text:null;
})();
if(!claudeHelp){
 // 조용히 통과시키지 않는다 — 건너뛴 사실이 보고에 남아야 한다.
 console.log('SKIP 공식 스펙 정합 — claude 설치본을 찾지 못했습니다 (플래그·별칭·effort 미검증)');
}else{
 check('every flag we pass to Claude exists in the installed CLI',()=>{
  const missing=CLAUDE_FLAGS.filter(f=>!claudeHelp.includes(f));
  assert.deepEqual(missing,[],'설치본 --help 에 없는 플래그: '+missing.join(' '));
 });
 check('the permission mode we pass is one the installed CLI accepts',()=>assert.match(claudeHelp,/dontAsk/));
 check('every effort we allow for Claude exists in the installed CLI',()=>{
  const line=(claudeHelp.match(/--effort[\s\S]{0,200}/)||[''])[0];
  const missing=CLAUDE_EFFORTS.filter(e=>!line.includes(e));
  assert.deepEqual(missing,[],'설치본이 받지 않는 effort: '+missing.join(' '));
 });
 check('the model aliases we offer are the ones the installed CLI names',()=>{
  const line=(claudeHelp.match(/--model[\s\S]{0,300}/)||[''])[0];
  const named=[...line.matchAll(/'([^']+)'/g)].map(x=>x[1]).filter(a=>!a.includes('-'));
  const {claudeAliases}=require('../../.claude/hooks/_model-adapter');
  assert.equal(typeof claudeAliases,'function','설치본에서 별칭을 유도하는 경로가 있어야 한다');
  const offered=claudeAliases(claudeHelp);
  assert.deepEqual(offered.slice().sort(),named.slice().sort(),'노출 별칭은 설치본이 이름 붙인 것과 같아야 한다');
  assert.ok(offered.includes('fable'),'공식 별칭 fable 이 노출되어야 한다');
 });
}
check('Claude rejects an effort it cannot run, at save time',()=>{
 const value=m.read().value;value.launchers.claude.roles.analysis={runtime:'claude',model:'opus',effort:'ultra',require_model:true};
 assert.throws(()=>m.save(value,m.read().version),/INVALID_EFFORT/);
});
check('Codex keeps the efforts its runtime reports',()=>{
 const value=m.read().value;value.launchers.codex.roles.analysis={runtime:'codex',model:'gpt-6-astra',effort:'ultra',require_model:false};
 const saved=m.save(value,m.read().version);
 assert.equal(saved.value.launchers.codex.roles.analysis.effort,'ultra');
});
s.close();if(path.resolve(dir).startsWith(path.join(os.tmpdir(),'harness-v14-models-')))fs.rmSync(dir,{recursive:true,force:true});console.log('결과: '+count+' PASS / 0 FAIL');
