import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {canConfirm,needsPolling,previewCells,templatePresentation,coverageLabel,policyLabel} from '../../src/FinanceLogistics.Api/wwwroot/js/trade-ui-rules.mjs';
const ready={status:'READY',jobStatus:'SUCCEEDED',periodStatus:'OPEN',errorCount:0,warningCount:1,totalRows:2};
for(const [name,values,ack,stale,expected]of [['warning not accepted',{},false,false,false],['ready reviewed',{},true,false,true],['stale',{},true,true,false],['invalid',{status:'INVALID',errorCount:1},true,false,false],['closed',{periodStatus:'CLOSED'},true,false,false],['failed worker',{jobStatus:'FAILED'},true,false,false],['empty',{totalRows:0},true,false,false],['validating',{status:'VALIDATING'},true,false,false]])test(name,()=>assert.equal(canConfirm({...ready,...values},ack,stale),expected));
test('values preserved',()=>assert.deepEqual(previewCells({normalizedCells:[null,'0','  000123  ']},[]),[null,'0','  000123  ']));
test('control not a transaction',()=>assert.deepEqual(previewCells({rowKind:'PLACEHOLDER',normalizedCells:null},['A']),[null]));
test('invalid visibly unnormalized',()=>assert.deepEqual(previewCells({rowKind:'DATA',normalizedCells:null},['A']),['Chưa chuẩn hóa']));
test('Pin cannot deduplicate',()=>assert.match(templatePresentation({profile:'PIN_TEST_1000_WORKBOOK_V1'}).hint,/không khử trùng/));
test('Pin no date filter',()=>assert.match(coverageLabel({periodMode:'AS_OF_SNAPSHOT'}),/không lọc/));
test('coverage not business signoff',()=>assert.match(coverageLabel({coverageStart:'2026-01-01',coverageEnd:'2026-12-31'}),/chưa xác nhận/));
test('stop failed polling',()=>assert.equal(needsPolling({status:'VALIDATING',jobStatus:'FAILED'}),false));
test('XLSX only',()=>assert.equal(templatePresentation({profile:'XK_6000_WORKBOOK_V1'}).accept,'.xlsx'));
test('static DOM targets exist',()=>{const js=readFileSync(new URL('../../src/FinanceLogistics.Api/wwwroot/js/trade-upload.mjs',import.meta.url),'utf8');const html=readFileSync(new URL('../../src/FinanceLogistics.Api/wwwroot/trade-upload.html',import.meta.url),'utf8');for(const[,id]of js.matchAll(/\$\('([^']+)'\)/g))assert.ok(html.includes(`id="${id}"`),id);});

test('Pin policy never suggests filtering by date',()=>assert.match(policyLabel({periodMode:'AS_OF_SNAPSHOT'}),/không lọc dòng/));
test('export policy displays its actual date field',()=>assert.match(policyLabel({periodMode:'EXPLICIT_RANGE',coverageProperty:'IssuedDate',coverageStart:'2026-01-01',coverageEndExclusive:'2027-01-01'}),/Ngày phát hành/));
