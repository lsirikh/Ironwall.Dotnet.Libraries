'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),os=require('node:os');
const {Store}=require('../../.claude/hooks/_harness-store');
const {Coordinator}=require('../../.claude/hooks/_coordinator');
const dir=fs.mkdtempSync(path.join(os.tmpdir(),'harness-v14-coordinator-'));
const store=new Store(path.join(dir,'state.sqlite')),c=new Coordinator(store,{repo_id:'repo-test',clock:()=>time});
let time=1000,passed=0;
function check(name,fn){fn();passed++;console.log('PASS '+name);}
const credential=c.join({runtime:'claude',pid:process.pid,worktree:dir});
const other=c.join({runtime:'codex',pid:process.pid,worktree:dir});
const auth={source:'trusted-launcher',reference:'explicit test user request'};
function work(title,nodes){const w=c.createWork({title,track:'C',nodes});c.authorize(w.work_id,auth,w.graph_hash);return w;}
const first=work('first',[{id:'A',files:['src/auth.js'],executor:'command'},{id:'B',deps:['A'],files:['assets/ui.js'],ui:true,executor:'command'}]);
check('graph must be approved before execution',()=>{
 const w=c.createWork({title:'draft',track:'B',nodes:[{id:'X',files:['src/x.js']}]});
 assert.throws(()=>c.claim(credential,w.work_id,'X'),/WORK_NOT_AUTHORIZED/);
});
check('cyclic and unknown dependencies are rejected',()=>{
 assert.throws(()=>c.createWork({title:'cycle',track:'C',nodes:[{id:'A',deps:['B']},{id:'B',deps:['A']}]}),/GRAPH_CYCLE/);
 assert.throws(()=>c.createWork({title:'missing',track:'C',nodes:[{id:'A',deps:['missing']}]}),/UNKNOWN_DEPENDENCY/);
});
check('unmet dependency waits without claiming resources',()=>assert.equal(c.claim(credential,first.work_id,'B').status,'waiting_dependency'));
const active=c.claim(credential,first.work_id,'A');
check('claimed node is not consumed a second time',()=>assert.throws(()=>c.claim(other,first.work_id,'A'),/NODE_OWNED/));
const second=work('second',[{id:'X',files:['src/**'],executor:'command'}]);
check('overlapping directory scope waits on the owner',()=>{
 const result=c.claim(other,second.work_id,'X');assert.equal(result.status,'waiting_resource');assert.equal(result.blockers[0].session_id,credential.session_id);
});
check('forged session secret cannot change state',()=>assert.throws(()=>c.heartbeat({...credential,token:'wrong'}),/SESSION_AUTH/));
c.start(credential,first.work_id,'A',active.run_id,active.epoch);
check('finish without verification is refused',()=>assert.throws(()=>c.finish(credential,first.work_id,'A',active.run_id,active.epoch,'hash-1'),/MISSING_EVIDENCE/));
c.evidence(credential,first.work_id,'A',active.run_id,active.epoch,{mode:'automated',status:'passed',checks:1,content_hash:'hash-1',command:['node','test.js']});
check('stale evidence cannot finish changed content',()=>assert.throws(()=>c.finish(credential,first.work_id,'A',active.run_id,active.epoch,'hash-2'),/STALE_EVIDENCE/));
check('verified node releases resources',()=>{
 assert.equal(c.finish(credential,first.work_id,'A',active.run_id,active.epoch,'hash-1').status,'verified');
 assert.equal(c.claim(other,second.work_id,'X').status,'claimed');
});
const gui=c.claim(credential,first.work_id,'B');c.start(credential,first.work_id,'B',gui.run_id,gui.epoch);
c.evidence(credential,first.work_id,'B',gui.run_id,gui.epoch,{mode:'headless',status:'passed',checks:8,content_hash:'gui-hash',command:['browser','headless']});
check('UI needs both headless and headed evidence',()=>assert.throws(()=>c.finish(credential,first.work_id,'B',gui.run_id,gui.epoch,'gui-hash'),/MISSING_EVIDENCE.*headed/));
c.evidence(credential,first.work_id,'B',gui.run_id,gui.epoch,{mode:'headed',status:'passed',checks:8,content_hash:'gui-hash',command:['browser','headed']});
check('two-mode UI verification completes',()=>assert.equal(c.finish(credential,first.work_id,'B',gui.run_id,gui.epoch,'gui-hash').status,'verified'));
check('zero checks cannot become verification success',()=>assert.throws(()=>c.evidence(other,second.work_id,'X',c.node(second.work_id,'X').run_id,c.node(second.work_id,'X').epoch,{mode:'automated',status:'passed',checks:0,content_hash:'x'}),/EMPTY_VERIFICATION/));
time+=100000;
check('stale heartbeat is an observation, not automatic lock release',()=>{
 const snap=c.snapshot();assert.ok(snap.sessions.find(s=>s.session_id===other.session_id).stale);
 assert.equal(c.node(second.work_id,'X').status,'claimed');
});
check('live owner cannot be reclaimed on TTL alone',()=>assert.throws(()=>c.recover(other.session_id),/OWNER_STILL_ALIVE/));
check('only ten active sessions are admitted',()=>{
 for(let i=2;i<10;i++)c.join({runtime:'codex',pid:process.pid,worktree:dir});
 assert.throws(()=>c.join({runtime:'claude',pid:process.pid,worktree:dir}),/SESSION_LIMIT/);
});
check('protected and escaping paths are rejected',()=>{
 for(const file of ['../escape','.git/config','.claude/.branch-x/pipeline-state.json','docs/memory/decisions.jsonl']){
  assert.throws(()=>work('bad',[{id:'A',files:[file]}]),/PROTECTED_RESOURCE|INVALID_RESOURCE/);
 }
});

check('failed run loses its capability and rejoins queue tail',()=>{
 const db=new Store(path.join(dir,'fair.sqlite')),x=new Coordinator(db,{repo_id:'fair'});
 const a=x.join({runtime:'claude',worktree:dir}),b=x.join({runtime:'codex',worktree:dir});
 function make(title){const w=x.createWork({title,track:'B',nodes:[{id:'A',files:['src/shared.js']}]});x.authorize(w.work_id,auth,w.graph_hash);return w;}
 const wa=make('a'),wb=make('b'),ra=x.claim(a,wa.work_id,'A');x.claim(b,wb.work_id,'A');x.failRun(a,wa.work_id,'A',ra.run_id,ra.epoch,'failed');
 assert.throws(()=>x.evidence(a,wa.work_id,'A',null,ra.epoch,{mode:'automated',status:'passed',checks:1,content_hash:'h',command:['test']}),/STALE_OWNER/);
 assert.throws(()=>x.finish(a,wa.work_id,'A',null,ra.epoch,'h'),/STALE_OWNER/);
 assert.equal(x.claim(a,wa.work_id,'A').status,'waiting_resource');
 assert.equal(x.claim(b,wb.work_id,'A').status,'claimed');db.close();
});
check('leaving the wait queue never strands later sessions',()=>{
 const db=new Store(path.join(dir,'leave.sqlite')),x=new Coordinator(db,{repo_id:'leave'});
 const a=x.join({runtime:'claude',worktree:dir}),b=x.join({runtime:'codex',worktree:dir}),d=x.join({runtime:'codex',worktree:dir});
 function make(title){const w=x.createWork({title,track:'B',nodes:[{id:'A',files:['src/shared.js']}]});x.authorize(w.work_id,auth,w.graph_hash);return w;}
 const wa=make('a'),wb=make('b'),wd=make('d'),ra=x.claim(a,wa.work_id,'A');x.claim(b,wb.work_id,'A');x.leave(b);
 x.failRun(a,wa.work_id,'A',ra.run_id,ra.epoch,'released');assert.equal(x.claim(d,wd.work_id,'A').status,'claimed');db.close();
});
check('dead-owner recovery fences old credentials and preserves quarantine claims',()=>{
 const db=new Store(path.join(dir,'recover.sqlite')),x=new Coordinator(db,{repo_id:'recover',isAlive:()=>false});
 const a=x.join({runtime:'claude',worktree:dir}),b=x.join({runtime:'codex',worktree:dir});
 function make(title){const w=x.createWork({title,track:'B',nodes:[{id:'A',files:['src/shared.js']}]});x.authorize(w.work_id,auth,w.graph_hash);return w;}
 const wa=make('a'),wb=make('b'),ra=x.claim(a,wa.work_id,'A');x.recover(a.session_id);
 assert.throws(()=>x.start(a,wa.work_id,'A',ra.run_id,ra.epoch),/SESSION_AUTH/);
 assert.equal(x.claim(b,wb.work_id,'A').status,'waiting_resource');
 x.releaseQuarantine(wa.work_id,'A','reviewed isolated worktree, no live descendants');
 assert.equal(x.claim(b,wb.work_id,'A').status,'claimed');db.close();
});

// [v16/N-01] 사람이 연 창은 노드를 소유하지 않는다. 그래도 세션이다 — 등록되지 않으면 화면에
//   뜨지 않고, 같은 저장소를 누가 쓰고 있는지 다른 세션도 알 수 없다. kind 를 execution·preparation
//   두 가지로만 두면 관측이 하네스가 띄운 창으로 한정된다.
// 앞선 검사들이 세션을 여럿 남겨 한도(10)에 닿아 있다 — 대화형 세션은 깨끗한 저장소에서 본다.
{
 const ivStore=new Store(path.join(dir,'interactive.sqlite'));
 const ic=new Coordinator(ivStore,{repo_id:'repo-interactive',clock:()=>time});
 try{
  check('a session that owns no node is still a session',()=>{
   const cred=ic.join({runtime:'claude',worktree:'C:/tmp/iv',kind:'interactive',name:'iv-window'});
   assert.ok(cred.session_id,'interactive 세션이 등록되어야 한다');
   const view=require('../../.claude/hooks/_session-view').sessionView(ic.snapshot(),{alive:()=>true});
   const mine=view.sessions.find(s=>s.session_id===cred.session_id);
   assert.ok(mine,'스냅샷에 보여야 한다');
   assert.equal(mine.kind,'interactive');
   assert.equal(mine.state,'idle','노드를 쥐지 않은 대화형 세션은 idle 이다');
   assert.deepEqual(mine.owned_nodes,[],'표시 전용 — 노드를 claim 하지 않는다');
   ic.leave(cred);
  });
  check('an unknown session kind is still rejected',()=>
   assert.throws(()=>ic.join({runtime:'claude',worktree:'C:/tmp/x',kind:'whatever'}),/INVALID_SESSION/));

  // [v16/N-01] 등록만으로는 화면이 거짓말을 한다 — 몇 시간을 일해도 'idle' 로 보인다.
  //   세션이 지금 무엇을 하는지(phase·track·노드·태스크 진행)가 서버에 실려야 모니터가 사실이 된다.
  check('a session reports what it is working on, not just that it exists',()=>{
   const cred=ic.join({runtime:'claude',worktree:'C:/tmp/iv2',kind:'interactive',name:'iv-activity'});
   const activity={phase:'dev',track:'C',charter:'harness-v16-one-graph-one-screen',node:'N-01',
    prd:'docs/prds/harness-v16-n01-claude-official-contract-prd.md',task:'IMPL-03',progress:'4/6',verified_at:'2026-09-17T08:20:00Z'};
   ic.heartbeat(cred,undefined,activity);
   const seen=ic.snapshot().sessions.find(s=>s.session_id===cred.session_id);
   assert.deepEqual(seen.activity,activity,'스냅샷이 작업 내용을 그대로 실어야 한다');
   const view=require('../../.claude/hooks/_session-view').sessionView(ic.snapshot(),{alive:()=>true});
   assert.equal(view.sessions.find(s=>s.session_id===cred.session_id).activity.node,'N-01');
   ic.leave(cred);
  });
  // [v16/N-01 · Loop A] "표시만 한다" 가 우연이면 안 된다. 한 줄의 재사용이 사람이 연 창을
  //   워커로 만들어 노드를 점유하면, 사용자는 자기 창이 무엇을 쥐었는지 모른 채 통합까지 간다.
  check('an interactive session cannot claim a node',()=>{
   const cred=ic.join({runtime:'claude',worktree:'C:/tmp/iv4',kind:'interactive',name:'iv-claim'});
   assert.throws(()=>ic.claim(cred,'w-any','N1'),/INTERACTIVE_CANNOT_CLAIM|UNKNOWN_WORK/);
   ic.leave(cred);
  });
  // [v16/N-02] 창의 pid 를 믿을 수 없는 실행 환경이 있다 — 훅이 셸을 거쳐 뜨면 ppid 는 그 셸이고
  //   셸은 곧 죽는다(원장 D-2026-09-16 의 재발방지 (a) 가 실제로 발화했다). 그러면 사람이 연 창이
  //   영원히 '연결 끊김' 으로 보이고, 복구 버튼이 살아 있는 창을 죽인다.
  //   사람이 연 창의 생존 신호는 pid 가 아니라 **최근 박동**이다. 관리형 세션은 pid 판정을 유지한다.
  check('an interactive window is alive while it keeps reporting',()=>{
   const cred=ic.join({runtime:'claude',pid:999999,worker_pids:[999999],worktree:'C:/tmp/iv5',kind:'interactive',name:'iv-heartbeat'});
   ic.heartbeat(cred,undefined,{phase:'dev'});
   const view=require('../../.claude/hooks/_session-view').sessionView(ic.snapshot(),{now:time,alive:()=>false});
   const mine=view.sessions.find(s=>s.session_id===cred.session_id);
   assert.equal(mine.recovery_needed,false,'박동이 신선하면 죽은 것으로 보지 않는다');
   assert.notEqual(mine.state,'disconnected');
   ic.leave(cred);
  });
  check('a managed session still uses pid liveness',()=>{
   const cred=ic.join({runtime:'claude',pid:999999,worker_pids:[999999],worktree:'C:/tmp/iv6',kind:'execution',name:'ex-dead'});
   const view=require('../../.claude/hooks/_session-view').sessionView(ic.snapshot(),{now:time,alive:()=>false});
   assert.equal(view.sessions.find(s=>s.session_id===cred.session_id).recovery_needed,true,'죽은 워커는 회수 대상이다');
   ic.leave(cred);
  });
  check('activity is data, not a free-for-all',()=>{
   const cred=ic.join({runtime:'claude',worktree:'C:/tmp/iv3',kind:'interactive',name:'iv-bad'});
   assert.throws(()=>ic.heartbeat(cred,undefined,'not-an-object'),/INVALID_ACTIVITY/);
   assert.throws(()=>ic.heartbeat(cred,undefined,{phase:'dev',junk:'x'.repeat(5000)}),/INVALID_ACTIVITY/);
   ic.leave(cred);
  });
 }finally{ivStore.close();}
}

store.close();
const resolved=path.resolve(dir);if(resolved.startsWith(path.join(os.tmpdir(),'harness-v14-coordinator-')))fs.rmSync(resolved,{recursive:true,force:true});
console.log('결과: '+passed+' PASS / 0 FAIL');
