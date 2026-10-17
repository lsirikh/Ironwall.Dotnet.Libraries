'use strict';
const fs=require('node:fs'),path=require('node:path'),os=require('node:os'),assert=require('node:assert/strict'),{spawn,execFileSync}=require('node:child_process');
const {startServer}=require('../../.claude/hooks/_ui-server');
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
class CDP{
 constructor(ws){this.ws=ws;this.next=1;this.pending=new Map();this.errors=[];ws.addEventListener('message',event=>{const m=JSON.parse(event.data);if(m.id){const p=this.pending.get(m.id);if(p){this.pending.delete(m.id);clearTimeout(p.timer);m.error?p.reject(Error(m.error.message)):p.resolve(m.result);}}if(m.method==='Runtime.exceptionThrown')this.errors.push(m.params.exceptionDetails.text);});}
 static async connect(url){const ws=new WebSocket(url);await new Promise((r,j)=>{ws.addEventListener('open',r,{once:true});ws.addEventListener('error',j,{once:true});});const c=new CDP(ws);await c.call('Runtime.enable');await c.call('Page.enable');return c;}
 call(method,params={}){const id=this.next++;return new Promise((resolve,reject)=>{const timer=setTimeout(()=>{this.pending.delete(id);reject(Error('CDP_TIMEOUT '+method));},12000);this.pending.set(id,{resolve,reject,timer});this.ws.send(JSON.stringify({id,method,params}));});}
 async eval(expression){const r=await this.call('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error(r.exceptionDetails.exception?.description||r.exceptionDetails.text);return r.result.value;}
 async wait(expression){const deadline=Date.now()+15000;while(Date.now()<deadline){if(await this.eval(expression))return;await sleep(100);}throw Error('UI_WAIT_TIMEOUT '+expression);}
 close(){this.ws.close();}
}
async function main(){
 const root=fs.mkdtempSync(path.join(os.tmpdir(),'harness-v14-browser-')),repo=path.join(root,'repo'),out=process.env.HARNESS_BROWSER_EVIDENCE||path.join(root,'evidence');fs.mkdirSync(repo);fs.mkdirSync(out,{recursive:true});
 const git=args=>execFileSync('git',args,{cwd:repo,windowsHide:true,stdio:'pipe'});git(['init']);git(['-c','user.name=Harness Test','-c','user.email=harness@example.invalid','commit','--allow-empty','-m','fixture']);
 const server=await startServer(repo),c=server.coordinator;
 const session=c.join({runtime:'claude',worktree:repo}),other=c.join({runtime:'codex',worktree:repo});
 const w=c.createWork({title:'브라우저 검증용 워크플로우',track:'C',nodes:[{id:'N01',title:'분석',files:['docs/analysis.md']},{id:'N02',title:'서버 구현',deps:['N01'],files:['src/api.js']},{id:'N03',title:'화면 구현',deps:['N01'],files:['src/ui.js'],ui:true},{id:'N04',title:'통합 검증',deps:['N02','N03'],files:['tests/core.js']}]});
 c.authorize(w.work_id,{source:'trusted-launcher',reference:'automated browser test fixture'},w.graph_hash);
 const n=c.claim(session,w.work_id,'N01');c.evidence(session,w.work_id,'N01',n.run_id,n.epoch,{mode:'automated',checks:1,status:'passed',content_hash:'fixture',command:['fixture']});c.finish(session,w.work_id,'N01',n.run_id,n.epoch,'fixture');
 const running=c.claim(session,w.work_id,'N02');c.start(session,w.work_id,'N02',running.run_id,running.epoch);
 const browser=process.env.HARNESS_BROWSER||['C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe','C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe'].find(fs.existsSync);if(!browser)throw Error('UNVERIFIED: browser unavailable; headed and headless are required');
 const results=[];
 try{
  for(const mode of ['headless','headed']){
   const profile=path.join(root,mode);fs.mkdirSync(profile);
   const args=['--remote-debugging-port=0','--user-data-dir='+profile,'--no-first-run','--no-default-browser-check','--disable-background-networking','--window-size=1440,1000',...(mode==='headless'?['--headless=new']:[]),'about:blank'];
   const child=spawn(browser,args,{windowsHide:mode==='headless',stdio:'ignore'});let client;
   try{
    let portFile=path.join(profile,'DevToolsActivePort'),deadline=Date.now()+12000;
    while(!fs.existsSync(portFile)&&Date.now()<deadline)await sleep(100);
    if(!fs.existsSync(portFile))throw Error('UNVERIFIED: '+mode+' browser unavailable');
    const port=Number(fs.readFileSync(portFile,'utf8').split('\n')[0]);
    const targets=await (await fetch('http://127.0.0.1:'+port+'/json')).json();client=await CDP.connect(targets.find(t=>t.type==='page').webSocketDebuggerUrl);
    let checks=0;
    await client.call('Page.navigate',{url:server.url+'/models'});await client.wait("document.querySelectorAll('#role-rows tr').length===8");checks++;
    await client.eval("document.getElementById('model-preset').value='lean';document.getElementById('model-preset').dispatchEvent(new Event('change'));document.querySelector('#model-form button[type=submit]').click()");
    await client.wait("document.getElementById('alert').textContent.includes('설정을 저장했습니다')");checks++;
    await client.eval("document.getElementById('model-preset').value='balanced';document.getElementById('model-preset').dispatchEvent(new Event('change'));document.querySelector('#model-form button[type=submit]').click()");
    await client.wait("document.querySelectorAll('#history .history-row').length>=2");checks++;
    await client.eval("document.querySelector('#history .history-row:last-child button').click()");
    await client.wait("document.getElementById('alert').textContent.includes('복원했습니다')");checks++;
    const before=server.models.read();server.models.save(before.value,before.version);
    await client.eval("document.querySelector('#model-form button[type=submit]').click()");
    await client.wait("document.getElementById('alert').classList.contains('error') && document.getElementById('alert').textContent.includes('다른 창')");checks++;
    await client.eval("document.getElementById('reload-config').click()");await client.wait("document.getElementById('alert').textContent.includes('불러왔습니다')");checks++;
    let shot=await client.call('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(out,mode+'-models.png'),Buffer.from(shot.data,'base64'));
    await client.call('Page.navigate',{url:server.url+'/workflows?work='+w.work_id});await client.wait("document.querySelectorAll('.node').length===4");checks++;
    await client.eval("document.querySelectorAll('.node')[2].click()");await client.wait("document.getElementById('node-detail').textContent.includes('headless') && document.getElementById('node-detail').textContent.includes('headed')");checks++;
    if(mode==='headless'){const gui=c.claim(other,w.work_id,'N03');c.start(other,w.work_id,'N03',gui.run_id,gui.epoch);}
    await client.wait("document.getElementById('metric-running').textContent==='2'");checks++;
    await client.wait("document.getElementById('connection').textContent.includes('실시간')");checks++;
    assert.equal(client.errors.length,0,'browser JS errors');checks++;
    shot=await client.call('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(out,mode+'-workflow.png'),Buffer.from(shot.data,'base64'));
    results.push({mode,status:'passed',checks,browser:path.basename(browser),at:new Date().toISOString()});console.log('PASS '+mode+' '+checks+' checks');
   }finally{if(client){await client.call('Browser.close').catch(()=>{});client.close();}if(child.exitCode===null)child.kill();}
  }
  fs.writeFileSync(path.join(out,'browser-evidence.json'),JSON.stringify({schema_version:1,results},null,2));console.log('Evidence: '+out);console.log('결과: '+results.reduce((n,r)=>n+r.checks,0)+' PASS / 0 FAIL');
 }finally{await server.close();}
}
module.exports={CDP};
if(require.main===module)main().catch(e=>{console.error(e);process.exitCode=1;});
