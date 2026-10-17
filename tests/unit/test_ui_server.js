'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),os=require('node:os'),http=require('node:http');
const {execFileSync}=require('node:child_process');
const {startServer,identity}=require('../../.claude/hooks/_ui-server'),{defaults}=require('../../.claude/hooks/_model-config');
const dir=fs.mkdtempSync(path.join(os.tmpdir(),'harness-v14-ui-'));let count=0;const servers=[];
function git(cwd,args){return execFileSync('git',args,{cwd,encoding:'utf8',windowsHide:true,stdio:['ignore','pipe','pipe']}).trim();}
async function check(name,fn){await fn();count++;console.log('PASS '+name);}
(async()=>{
 const a=path.join(dir,'one'),b=path.join(dir,'two'),wt=path.join(dir,'worktree');fs.mkdirSync(a);fs.mkdirSync(b);
 for(const root of [a,b]){git(root,['init']);git(root,['-c','user.name=Harness Test','-c','user.email=harness@example.invalid','commit','--allow-empty','-m','fixture']);}
 git(a,['worktree','add','-b','second',wt]);
 const first=await startServer(a),second=await startServer(b);servers.push(first,second);
 await check('separate repositories choose separate live ports',async()=>{assert.notEqual(first.port,second.port);assert.notEqual((await identity(first.url)).repo_id,(await identity(second.url)).repo_id);});
 await check('worktrees share the same verified dashboard',async()=>{const reused=await startServer(wt);assert.equal(reused.reused,true);assert.equal(reused.instance_id,first.instance_id);assert.equal(reused.url,first.url);});
 await check('fixed port does not silently move to a different port',async()=>{await assert.rejects(()=>startServer(a,{port:second.port}),/PROJECT_ALREADY_SERVED/);});
 const boot=await (await fetch(first.url+'/api/bootstrap')).json();
 const post=(route,value,csrf=boot.csrf)=>fetch(first.url+route,{method:'POST',headers:{Origin:first.url,'X-Harness-CSRF':csrf,'Content-Type':'application/json'},body:JSON.stringify(value)});
 await check('foreign origin and missing CSRF cannot edit configuration',async()=>{const r=await fetch(first.url+'/api/models',{method:'POST',body:'{}'});assert.equal(r.status,403);assert.equal((await post('/api/models',{},'wrong')).status,403);});
 await check('DNS rebinding host is rejected',async()=>{const status=await new Promise((resolve,reject)=>{const r=http.get(first.url+'/api/bootstrap',{headers:{host:'attacker.invalid'}},r=>{r.resume();resolve(r.statusCode);});r.on('error',reject);});assert.equal(status,403);});
 await check('settings save and stale browser conflicts return distinct results',async()=>{assert.equal((await post('/api/models',{value:defaults('lean'),expectedVersion:0,operation_id:'one'})).status,200);assert.equal((await post('/api/models',{value:defaults(),expectedVersion:0,operation_id:'two'})).status,409);});
 await check('SSE replays committed events after the snapshot cursor',async()=>{
  const snap=await (await fetch(first.url+'/api/snapshot')).json(),controller=new AbortController(),timeout=setTimeout(()=>controller.abort(),8000);
  try{const stream=await fetch(first.url+'/api/events?after='+snap.cursor,{signal:controller.signal});
  await post('/api/models',{value:defaults(),expectedVersion:1,operation_id:'three'});
  const reader=stream.body.getReader();let text='',deadline=Date.now()+5000;
  while(!text.includes('models.changed')&&Date.now()<deadline){const r=await reader.read();if(r.done)break;text+=Buffer.from(r.value).toString();}
  assert.match(text,/models.changed/);const id=Number(text.match(/id: (\d+)/)[1]);assert.ok(id>snap.cursor);
  }finally{clearTimeout(timeout);controller.abort();}
 });
 await check('HTML and script ship without external network assets',async()=>{const page=await (await fetch(first.url+'/models')).text();assert.match(page,/app.js/);const script=await (await fetch(first.url+'/app.js')).text();new Function(script);assert.ok(!/https:\/\/cdn/.test(page));});
 await check('snapshot never exposes session secrets',async()=>{const s=first.coordinator.join({runtime:'claude',worktree:a});const raw=await (await fetch(first.url+'/api/snapshot')).text();assert.ok(!raw.includes(s.token));assert.ok(!raw.includes('token_hash'));first.coordinator.leave(s);});
})().then(async()=>{for(const s of servers)await s.close();if(path.resolve(dir).startsWith(path.join(os.tmpdir(),'harness-v14-ui-')))fs.rmSync(dir,{recursive:true,force:true});console.log('결과: '+count+' PASS / 0 FAIL');}).catch(async e=>{console.error(e);for(const s of servers)await s.close();process.exitCode=1;});
