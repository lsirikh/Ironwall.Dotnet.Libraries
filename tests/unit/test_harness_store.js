'use strict';
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const { spawnSync, spawn } = require('node:child_process');
const { Store } = require('../../.claude/hooks/_harness-store');
const base = fs.mkdtempSync(path.join(os.tmpdir(), 'harness-v14-store-'));
const dbFile = path.join(base, 'coordinator.sqlite');
let passed = 0;
function check(name, fn) { fn(); passed++; console.log('PASS '+name); }
async function main() {
  const store = new Store(dbFile);
  check('transaction writes state and event together', () => {
    store.transaction(tx => { tx.put('counter', { value: 1 }, 0); tx.event('counter.changed', { value: 1 }); });
    assert.equal(store.get('counter').value.value, 1); assert.equal(store.events(0).length, 1);
  });
  check('failed transaction rolls back state and events', () => {
    assert.throws(() => store.transaction(tx => { tx.put('counter', { value: 99 }, 1); tx.event('bad', {}); throw Error('abort'); }));
    assert.equal(store.get('counter').value.value, 1); assert.equal(store.events(0).length, 1);
  });
  check('compare and swap rejects stale update', () => {
    assert.throws(() => store.transaction(tx => tx.put('counter', { value: 3 }, 0)), /VERSION_CONFLICT/);
  });
  check('idempotent operation commits once', () => {
    const apply = () => store.transaction(tx => { const r=tx.get('counter'); tx.put('counter',{value:r.value.value+1},r.version); tx.event('increment',{}); return 'once'; }, 'operation-1');
    assert.equal(apply(),'once'); assert.equal(apply(),'once'); assert.equal(store.get('counter').value.value,2);
  });
  check('snapshot cursor and state share a transaction', () => {
    const snap=store.snapshot(); assert.equal(snap.cursor,2); assert.equal(snap.records.counter.value.value,2);
  });
  check('async callbacks are refused', () => {
    assert.throws(() => store.transaction(() => Promise.resolve()), /ASYNC_TRANSACTION/);
  });
  let escaped;
  store.transaction(tx => { escaped=tx; });
  check('transaction handles expire after commit', () => assert.throws(()=>escaped.put('escaped',1),/TRANSACTION_CLOSED/));
  assert.throws(()=>store.transaction(async tx=>{await Promise.resolve();tx.put('escaped',1);tx.event('escaped',{});}),/ASYNC_TRANSACTION/);
  await new Promise(resolve=>setImmediate(resolve));
  check('rejected async callback cannot write after rollback',()=>{assert.equal(store.get('escaped').value,null);assert.equal(store.events(0).length,2);});
  check('deleting and recreating a key does not reuse its revision',()=>{
    store.transaction(tx=>tx.put('replace',{v:1},0));
    const first=store.get('replace');
    store.transaction(tx=>tx.remove('replace',first.version));
    const tombstone=store.get('replace');
    assert.equal(tombstone.value,null);assert.ok(tombstone.version>first.version);
    store.transaction(tx=>tx.put('replace',{v:2},tombstone.version));
    assert.throws(()=>store.transaction(tx=>tx.put('replace',{v:3},first.version)),/VERSION_CONFLICT/);
  });
  store.close();
  const modulePath = path.resolve(__dirname, '../../.claude/hooks/_harness-store.js');
  const worker = path.join(base,'worker.cjs');
  fs.writeFileSync(worker, "const {Store}=require("+JSON.stringify(modulePath)+");const s=new Store(process.argv[2]);for(let i=0;i<40;i++)s.transaction(t=>{const r=t.get('counter');t.put('counter',{value:r.value.value+1},r.version);t.event('increment',{});});s.close();");
  await Promise.all(Array.from({length:10}, () => new Promise((resolve,reject) => {
    const child=spawn(process.execPath,[worker,dbFile],{windowsHide:true,stdio:['ignore','pipe','pipe']});
    let stderr='';child.stderr.on('data',b=>stderr+=b);child.on('error',reject);child.on('exit',code=>code===0?resolve():reject(Error(stderr)));
  })));
  const reopened = new Store(dbFile);
  check('ten processes do not lose updates or events', () => {
    assert.equal(reopened.get('counter').value.value,402);
    assert.equal(reopened.events(0,1000).length,402);
  });
  reopened.close();
  const crash=path.join(base,'crash.cjs');
  fs.writeFileSync(crash,"const {Store}=require("+JSON.stringify(modulePath)+");const s=new Store(process.argv[2]);s.transaction(t=>{const r=t.get('counter');t.put('counter',{value:999999},r.version);process.exit(23);});");
  const killed=spawnSync(process.execPath,[crash,dbFile],{windowsHide:true});
  const recovered=new Store(dbFile);
  check('process death leaves uncommitted writes unapplied and releases lock', () => {
    assert.equal(killed.status,23);assert.equal(recovered.get('counter').value.value,402);
    recovered.transaction(t=>t.event('recovered',{}));assert.equal(recovered.events(402).length,1);
  });
  check('database and sidecar hardlinks are refused',()=>{
    const external=path.join(base,'external.sqlite');
    const original=new Store(external);original.close();
    const linked=path.join(base,'linked.sqlite');fs.linkSync(external,linked);
    assert.throws(()=>new Store(linked),/COORDINATOR_FILE_REDIRECT/);
    fs.unlinkSync(linked);
    const sidecar=path.join(base,'sidecar.sqlite-wal');fs.linkSync(external,sidecar);
    assert.throws(()=>new Store(path.join(base,'sidecar.sqlite')),/COORDINATOR_FILE_REDIRECT/);
    fs.unlinkSync(sidecar);
  });
  recovered.close();
  console.log('결과: '+passed+' PASS / 0 FAIL');
}
main().catch(e=>{console.error(e);process.exitCode=1;}).finally(()=>{
  const resolved=path.resolve(base),prefix=path.join(os.tmpdir(),'harness-v14-store-');
  if(resolved.startsWith(prefix))fs.rmSync(resolved,{recursive:true,force:true});
});
