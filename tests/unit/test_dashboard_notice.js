'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),os=require('node:os'),{execFile}=require('node:child_process'),{promisify}=require('node:util');
const exec=promisify(execFile),{git,repository}=require('../../.claude/hooks/_harness-store'),{startServer}=require('../../.claude/hooks/_ui-server');
const {createReminder,readRegistration,dashboardStatus,sessionNotice}=require('../../.claude/hooks/_dashboard-notice');
const dir=fs.mkdtempSync(path.join(os.tmpdir(),'harness-notice-')),cli=path.resolve(__dirname,'../../.claude/hooks/advance-phase.js');let checks=0,server,repo;
async function check(name,fn){await fn();checks++;console.log('PASS '+name);}
async function command(file,args,cwd,input){
 const child=exec(process.execPath,[file,...args],{cwd,encoding:'utf8',windowsHide:true,timeout:15000,maxBuffer:2*1024*1024});
 child.child.stdin.end(input||'');return child;
}
(async()=>{
 let time=0,minutes=5;const lines=[],remind=createReminder({url:'http://127.0.0.1:1234',work_id:'work/1',minutes:()=>minutes,now:()=>time,write:line=>lines.push(line)});
 await check('first notice and exact five-minute reminder are emitted without polling spam',async()=>{
  assert.equal(remind(),true);time=299999;assert.equal(remind(),false);time=300000;assert.equal(remind(),true);assert.equal(lines.length,2);assert.match(lines[0],/work=work%2F1/);
 });
 await check('changed interval is used and zero disables only periodic reminders',async()=>{
  minutes=1;time+=60000;assert.equal(remind(),true);minutes=0;time+=3600000;assert.equal(remind(),false);assert.equal(remind('검증 완료',true),true);assert.match(lines.at(-1),/검증 완료/);
 });
 git(dir,['init']);git(dir,['-c','user.name=Test','-c','user.email=test@example.invalid','commit','--allow-empty','-m','fixture']);
 await check('prompt notice does not initialize a coordinator or emit anything in a new repository',async()=>{
  const output=[];assert.equal(readRegistration(dir),null);assert.equal(await sessionNotice(dir,line=>output.push(line)),null);assert.deepEqual(output,[]);assert.equal(fs.existsSync(path.join(dir,'.git','harness-v14')),false);
 });
 server=await startServer(dir);repo=repository(dir);
 await check('live identity yields a clickable work link without changing coordination records',async()=>{
  const before=JSON.stringify(repo.store.snapshot()),status=await dashboardStatus(readRegistration(dir),'work/1');
  assert.equal(status.url,server.url+'/workflows?work=work%2F1');assert.equal(status.status,'online');assert.equal(JSON.stringify(repo.store.snapshot()),before);
 });
 await check('another worktree sees the same live project dashboard',async()=>{
  const sibling=path.join(dir,'sibling');git(dir,['worktree','add','-b','sibling',sibling]);
  assert.equal((await dashboardStatus(readRegistration(sibling))).url,server.url+'/workflows');
 });
 await check('stale instance, foreign project and non-local records never yield a live link',async()=>{
  const existing=readRegistration(dir);
  for(const value of [{...existing,repo_id:'foreign'},{...existing,record:{...existing.record,instance_id:'stale'}},{...existing,record:{...existing.record,url:'https://example.invalid'}}]){
   const status=await dashboardStatus(value);assert.equal(status.status,'offline');assert.equal(status.url,null);assert.match(status.start_command,/advance-phase.js ui$/);
  }
 });
 let work;
 await check('CLI create, status and context remain JSON and expose the verified dashboard',async()=>{
  const file=path.join(dir,'graph.json');fs.writeFileSync(file,JSON.stringify({track:'B',title:'notice contract',nodes:[{id:'one',files:['one.txt'],executor:'command',command:[process.execPath,'-e',"require('fs').writeFileSync('one.txt','ready')"],tests:[{mode:'automated',format:'json',command:[process.execPath,'-e',"require('assert').equal(require('fs').readFileSync('one.txt','utf8'),'ready');console.log(JSON.stringify({status:'passed',checks:1}))"]}]}]}));
  work=JSON.parse((await command(cli,['work','create','--file',file,'--request-ref','automated dashboard notice fixture'],dir)).stdout);
  assert.equal(work.dashboard.url,server.url+'/workflows?work='+work.work_id);
  for(const action of ['status','context']){
   const result=JSON.parse((await command(cli,['work',action,work.work_id],dir)).stdout);assert.equal(result.dashboard.url,work.dashboard.url);
  }
 });
 await check('CLI run keeps reminders on stderr and the final result on JSON stdout',async()=>{
  const result=await command(cli,['work','run',work.work_id,'--runtime','codex'],dir),value=JSON.parse(result.stdout);
  assert.equal(value.status,'verified');assert.equal(value.dashboard.url,work.dashboard.url);
  for(const detail of ['작업 시작·재개','one 시작','one 테스트·검증','one 검증 완료','이번 세션 종료'])assert.ok(result.stderr.includes(detail),result.stderr);
 });
 await check('each Claude prompt gets an untruncated page notice through the real hook',async()=>{
  fs.cpSync(path.resolve(__dirname,'../../.claude/hooks'),path.join(dir,'.claude','hooks'),{recursive:true});
  fs.writeFileSync(path.join(dir,'CLAUDE.md'),'project_name: "Notice test"\nversion: "1.0.0"\n');
  const hook=path.join(dir,'.claude','hooks','session-gate.js');
  for(let i=0;i<2;i++){
   const result=await command(hook,[],dir,JSON.stringify({hook_event_name:'UserPromptSubmit',session_id:'notice-test',prompt:'현재 작업 상태를 알려줘'}));
   const line=result.stdout.split('\n').find(line=>line.startsWith('[Harness] '));
   assert.ok(line,result.stdout.slice(-1000));assert.ok(line.includes(server.url+'/workflows'));assert.ok(line.includes('응답에 이 링크'));
  }
 });
 await server.close();server=null;
 await check('after shutdown CLI returns a reopen command instead of a stale URL',async()=>{
  const result=JSON.parse((await command(cli,['work','context',work.work_id],dir)).stdout);
  assert.equal(result.dashboard.status,'offline');assert.equal(result.dashboard.url,null);assert.match(result.dashboard.start_command,/advance-phase.js ui$/);
 });
})().then(()=>{repo?.store.close();console.log('결과: '+checks+' PASS / 0 FAIL');}).catch(async e=>{console.error(e);if(server)await server.close();repo?.store.close();process.exitCode=1;});
