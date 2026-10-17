'use strict';
const fs=require('node:fs'),os=require('node:os'),path=require('node:path'),assert=require('node:assert/strict'),{spawn,execFileSync}=require('node:child_process');
const root=path.resolve(__dirname,'../..'),dir=fs.mkdtempSync(path.join(os.tmpdir(),'v14-stable-'));let checks=0;
const S=require('../../.claude/hooks/_signals'),H=require('../../.claude/hooks/advance-phase-harness'),P=require('../../.claude/hooks/_projection'),W=require('../../.claude/hooks/_worktree');
async function check(name,fn){await fn();checks++;console.log('PASS '+name);}
(async()=>{
 await check('model and agent decisions count, automatic commit records do not',()=>{assert.equal(S.countDecisionAdded([{source:'model'},{source:'agent'},{source:'commit'},{source:'charter'},{source:'user'}],0),3);assert.equal(S.countHumanAdded([{source:'model'},{source:'agent'},{source:'user'}],0),1);});
 await check('missing test configuration remains unverified instead of trapping completion',()=>{assert.deepEqual(H.completionEvidence({files:['src/a.js'],configuredTest:''}),{ok:true,status:'unverified',reason:'test-command-not-configured'});});
 const base={files:['src/a.js'],configuredTest:'node tests.js',evidence:{treeSig:'a',scope:'full',at:'2026-09-15T12:00:00Z'},nowSig:'a',cycleStartedAt:'2026-09-15T11:00:00Z'};
 await check('configured tests need fresh passing evidence',()=>{assert.equal(H.completionEvidence(base).status,'verified');for(const override of [{evidence:null},{nowSig:'b'},{lastFailure:{type:'test'}},{loopV:{state:'FAILING'}},{cycleStartedAt:'2026-09-15T13:00:00Z'}])assert.equal(H.completionEvidence({...base,...override}).ok,false);});
 await check('removing test configuration cannot hide an existing failure',()=>assert.equal(H.completionEvidence({...base,configuredTest:'',lastFailure:{type:'test'}}).ok,false));
 await check('missing configuration cannot hide stale or incomplete existing evidence',()=>{
  for(const override of [{nowSig:'b'},{cycleStartedAt:'2026-09-15T13:00:00Z'},{evidence:{}},{evidence:{...base.evidence,scope:'filter'}}])assert.equal(H.completionEvidence({...base,configuredTest:'',...override}).ok,false);
  assert.equal(H.completionEvidence({...base,configuredTest:''}).status,'unverified');
 });
 await check('narrow verification cannot close unrelated changes',()=>{assert.equal(H.completionEvidence({...base,evidence:{treeSig:'a',scope:'affected',files:['tests/a.js']}}).ok,false);assert.equal(H.completionEvidence({...base,evidence:{treeSig:'a',scope:'affected',files:['tests/a.js']},graph:{files:{'src/a.js':{}}},affectedTests:()=>['tests/a.js']}).ok,true);});
 // [v15/N-01] A verdict parser must let failure win. Reading only the first match turns a
 //   partially failing log into a green light, and the exit code is then the only guard left.
 await check('a failing result anywhere defeats a passing one, in either order',()=>{
  assert.equal(W.parseChecks('결과: 3 PASS / 2 FAIL\n결과: 10 PASS / 0 FAIL'),0);
  assert.equal(W.parseChecks('결과: 10 PASS / 0 FAIL\n결과: 3 PASS / 2 FAIL'),0);
  assert.equal(W.parseChecks('# pass 4\n# fail 0\n# pass 7\n# fail 3'),0);
  assert.equal(W.parseChecks('5 passed\n3 passed, 2 failed'),0);
 });
 await check('an explicit zero failure count is a pass, not an empty verification',()=>{
  assert.equal(W.parseChecks('===== 5 passed, 0 failed in 1.2s ====='),5);
  assert.equal(W.parseChecks('결과: 9 PASS / 0 FAIL'),9);
 });
 // [v15/N-01 · Loop A] "실패 우선"을 산문에까지 적용하면 통과한 빌드가 테스트 **이름** 때문에
 //   부결된다. 실패 신호는 요약 구조에 붙어 있을 때만 세어야 한다.
 await check('a failure word inside a test name is not a failure count',()=>{
  assert.equal(W.parseChecks('✓ locks account after 3 failed login attempts (5 ms)\nTests: 0 failed, 42 passed, 42 total'),42);
  assert.equal(W.parseChecks('✅ retries 2 failed shards\n결과: 9 PASS / 0 FAIL'),9);
  assert.equal(W.parseChecks('❌ rejects 1 failed token\n# pass 6\n# fail 0'),6);
 });
 await check('an empty leading family does not hide a later one',()=>{
  assert.equal(W.parseChecks('# pass 0\n\n42 passed in 1.1s'),42);
 });
 await check('a failure in any family defeats a pass in another',()=>{
  assert.equal(W.parseChecks('결과: 5 PASS / 0 FAIL\nTests: 1 failed, 41 passed, 42 total'),0);
 });
 await check('existing verdicts do not regress',()=>{
  assert.equal(W.parseChecks('# pass 4\n# fail 0'),4);
  assert.equal(W.parseChecks('# pass 4\n# fail 2'),0);
  assert.equal(W.parseChecks('===== 5 passed in 1.2s ====='),5);
  assert.equal(W.parseChecks('===== 3 passed, 2 failed in 1.2s ====='),0);
  assert.equal(W.parseChecks(JSON.stringify({status:'passed',checks:7}),'json'),7);
  assert.equal(W.parseChecks(JSON.stringify({status:'failed',checks:7}),'json'),0);
  assert.equal(W.parseChecks('no recognizable result'),0);
 });
 execFileSync('git',['init','-q'],{cwd:dir,windowsHide:true});
 const audit=path.join(dir,'docs/memory/audit-log.jsonl');
 const script=path.join(dir,'writer.cjs');fs.writeFileSync(script,"const P=require("+JSON.stringify(path.join(root,'.claude/hooks/_projection'))+");for(let i=0;i<20;i++)P.appendDurable(process.argv[2],{writer:process.argv[3],i});");
 await check('eight processes append 160 intact distinct records',async()=>{
  await Promise.all(Array.from({length:8},(_,i)=>new Promise((resolve,reject)=>{const c=spawn(process.execPath,[script,audit,String(i)],{windowsHide:true,stdio:['ignore','pipe','pipe']});let err='';c.stderr.on('data',b=>err+=b);c.on('error',reject);c.on('exit',code=>code===0?resolve():reject(Error(err)));})));
  const records=fs.readFileSync(audit,'utf8').trim().split('\n').map(JSON.parse);assert.equal(records.length,160);assert.equal(new Set(records.map(r=>r.writer+'/'+r.i)).size,160);
 });
 await check('audit writes use a lock transaction without synthetic SQLite records',()=>{const {Store}=require('../../.claude/hooks/_harness-store'),store=new Store(path.join(dir,'.git/harness-v14/projections.sqlite'));assert.equal(store.events().length,0);store.close();});
 await check('failed projection releases the cross-process lock',()=>{assert.throws(()=>P.withProjection(dir,'fixture',()=>{throw Error('fixture');}),/fixture/);P.appendDurable(audit,{after_failure:true});assert.equal(JSON.parse(fs.readFileSync(audit,'utf8').trim().split('\n').at(-1)).after_failure,true);});
 await check('only the SQLite experimental notice is suppressed',()=>{
  const script=path.join(dir,'warnings.cjs');fs.writeFileSync(script,"require("+JSON.stringify(path.join(root,'.claude/hooks/_sqlite'))+").database();process.emitWarning('keep this warning','CustomWarning');");
  const r=require('node:child_process').spawnSync(process.execPath,[script],{encoding:'utf8',windowsHide:true});assert.equal(r.status,0);assert.match(r.stderr,/keep this warning/);assert.doesNotMatch(r.stderr,/SQLite is an experimental/);
 });
 console.log('결과: '+checks+' PASS / 0 FAIL');
})().catch(e=>{console.error(e);process.exitCode=1;});
