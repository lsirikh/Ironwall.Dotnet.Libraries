'use strict';
const fs=require('fs'),path=require('path'),os=require('os'),assert=require('assert/strict'),{spawn}=require('child_process');
const {repository,git}=require('../../.claude/hooks/_harness-store'),{Coordinator}=require('../../.claude/hooks/_coordinator'),managed=require('../../.claude/hooks/_managed-work');
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
async function until(fn,timeout=90000){const start=Date.now();while(!fn()){if(Date.now()-start>timeout)throw Error('CONDITION_TIMEOUT');await sleep(100);}}
async function main(){
 const mode=process.argv[2];
 if(mode==='worker'){
  const repo=repository(process.argv[3]);try{const r=await managed.run(repo,process.argv[4],{runtime:process.argv[5],session_name:process.argv[6],jobs:1,max_wait_ms:3600000,onNotice:()=>{}});assert.equal(r.status,'verified');console.log(JSON.stringify(r));}finally{repo.store.close();}return;
 }
 if(mode==='task'){
  const control=process.argv[3],slot=process.argv[4],phase=Number(process.argv[5]),cfg=JSON.parse(fs.readFileSync(path.join(control,'config.json'))),file=(phase===3?'shared':'slot-'+slot)+'.txt';
  fs.writeFileSync(path.join(control,'ready-'+slot+'-'+phase),'ready');fs.writeFileSync(file,'in progress');
  if(phase===3)await sleep(250);
  else{await until(()=>fs.existsSync(path.join(control,'release')),90000);const end=cfg.soak?cfg.started_at+cfg.duration_ms*(phase+1)/3:Date.now()+200;while(Date.now()<end){fs.writeFileSync(path.join(control,'ping-'+slot),String(Date.now()));await sleep(500);}}
  fs.writeFileSync(file,'complete-'+slot+'-'+phase);return;
 }
 const soak=process.argv.includes('--soak'),duration_ms=Number(process.env.HARNESS_SOAK_MS||1800000),sizes=soak?[10]:[2,4,8,10],results=[];
 if(!Number.isSafeInteger(duration_ms)||duration_ms<15000||duration_ms>2400000)throw Error('INVALID_SOAK_DURATION');
 for(const size of sizes){
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'harness-multisession-process-')),root=path.join(dir,'project'),control=path.join(dir,'control');fs.mkdirSync(root);fs.mkdirSync(control);git(root,['init','-q']);for(const f of ['prd.md','plan.md'])fs.writeFileSync(path.join(root,f),'# Process verification');git(root,['add','.']);git(root,['-c','user.name=Harness','-c','user.email=harness@test','commit','-qm','fixture']);
  const repo=repository(root),c=new Coordinator(repo.store,{repo_id:repo.repo_id}),started=Date.now(),cfg={soak,duration_ms,started_at:started};fs.writeFileSync(path.join(control,'config.json'),JSON.stringify(cfg));
  const works=Array.from({length:size},(_,i)=>{
   const phases=soak?[0,1,2,3]:[0,3];return managed.create(repo,{title:'Session '+i,track:'C',max_parallel:1,prd:'prd.md',plan:'plan.md',nodes:phases.map((phase,index)=>({id:'phase-'+phase,deps:index?['phase-'+phases[index-1]]:[],files:[(phase===3?'shared':'slot-'+i)+'.txt'],executor:'command',timeout_ms:Math.min(3600000,duration_ms+120000),command:[process.execPath,__filename,'task',control,String(i),String(phase)],tests:[{mode:'automated',format:'json',command:[process.execPath,'-e',"require('assert').equal(require('fs').readFileSync('"+(phase===3?'shared':'slot-'+i)+".txt','utf8'),'complete-"+i+'-'+phase+"');console.log(JSON.stringify({status:'passed',checks:1}))"]}]}))},'authorized multisession integration fixture');
  });
  const children=[];function worker(i){const child=spawn(process.execPath,[__filename,'worker',root,works[i].work_id,i%2?'codex':'claude','Window '+i],{windowsHide:true,stdio:['ignore','pipe','pipe']});const state={child,i,code:null,closed:false,out:'',err:''};child.stdout.on('data',b=>state.out+=b);child.stderr.on('data',b=>state.err+=b);child.on('close',code=>{state.code=code;state.closed=true;});children.push(state);return state;}
  let peak=0,waitSeen=false,samples=0,recovered=false,timer;
  try{
   for(let i=0;i<size;i++)worker(i);
   await until(()=>Array.from({length:size},(_,i)=>fs.existsSync(path.join(control,'ready-'+i+'-0'))).every(Boolean));
   const snap=c.snapshot();assert.equal(snap.sessions.filter(s=>s.status==='active').length,size);assert.equal(snap.nodes.filter(n=>n.status==='running').length,size);
   if(size===10)assert.throws(()=>c.join({runtime:'claude',worktree:root}),/SESSION_LIMIT/);
   fs.writeFileSync(path.join(control,'release'),'go');
   timer=setInterval(()=>{const snapshot=c.snapshot(),count=snapshot.nodes.filter(n=>['claimed','running','verifying'].includes(n.status)).length;peak=Math.max(peak,count);waitSeen ||=snapshot.nodes.some(n=>n.status==='waiting_resource');samples++;assert.ok(count<=10);const report={size,soak,elapsed_ms:Date.now()-started,active:count,peak,samples,recovered,waitSeen};if(process.env.HARNESS_PROCESS_EVIDENCE)fs.writeFileSync(process.env.HARNESS_PROCESS_EVIDENCE+'.progress.json',JSON.stringify(report));},250);
   if(soak){
    await sleep(Math.min(300000,duration_ms/5));const target=children[2],owner=c.snapshot().sessions.find(s=>s.pid===target.child.pid);assert.ok(owner);target.expected_kill=true;target.child.kill();await until(()=>target.closed);await until(()=>{try{c.recover(owner.session_id);return true;}catch(e){if(e.code!=='OWNER_STILL_ALIVE')throw e;return false;}});
    const quarantined=c.snapshot().nodes.filter(n=>n.session_id===owner.session_id&&n.status==='quarantined');assert.ok(quarantined.length);for(const n of quarantined)c.releaseQuarantine(n.work_id,n.node_id,'Verified dead controller and supervised workers; bounded fixture writes are idempotent',n.epoch);worker(2);recovered=true;
   }
   await until(()=>children.every(s=>s.closed),soak?duration_ms+180000:180000);clearInterval(timer);timer=null;
   for(const child of children)if(!child.expected_kill)assert.equal(child.code,0,child.err+'\n'+child.out);
   const final=c.snapshot();assert.ok(final.nodes.every(n=>n.status==='verified'));assert.equal(final.sessions.filter(s=>s.status==='active').length,0);assert.ok(final.nodes.every(n=>n.attempts===1||(soak&&n.work_id===works[2].work_id&&n.attempts===2)));assert.equal(new Set(final.nodes.map(n=>n.candidate.workspace)).size,final.nodes.length);assert.ok(waitSeen,'resource contention must have been observed');
   const elapsed_ms=Date.now()-started;if(soak){assert.ok(elapsed_ms>=duration_ms);assert.ok(recovered);}
   results.push({size,root,soak,elapsed_ms,peak,samples,recovered,waitSeen,sessions:final.sessions.length,nodes:final.nodes.length,status:'passed',checks:soak?12:10});console.log('PASS '+size+' real managed processes'+(soak?' soak':''));
  }finally{if(timer)clearInterval(timer);for(const child of children)if(!child.closed)child.child.kill();repo.store.close();}
 }
 const evidence={status:'passed',checks:results.reduce((n,r)=>n+r.checks,0),results,at:new Date().toISOString()};if(process.env.HARNESS_PROCESS_EVIDENCE)fs.writeFileSync(process.env.HARNESS_PROCESS_EVIDENCE,JSON.stringify(evidence,null,2));console.log('결과: '+evidence.checks+' PASS / 0 FAIL');
}
main().catch(e=>{console.error(e);process.exitCode=1;});
