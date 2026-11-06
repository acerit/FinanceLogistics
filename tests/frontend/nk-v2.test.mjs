import test from 'node:test';
import assert from 'node:assert/strict';
import { templatePresentation, canConfirm } from '../../src/FinanceLogistics.Api/wwwroot/js/nk-ui-rules.mjs';
test('V2 explains cached values and no recalculation',()=>{
  const p=templatePresentation({profile:'NK_1000_WORKBOOK_V2',versionNo:950002});
  assert.equal(p.accept,'.xlsx');assert.equal(p.allowCsvDownload,false);
  assert.match(p.hint,/cache/);assert.match(p.hint,/không tính lại/);
});
test('V1 presentation remains available',()=>{
  assert.match(templatePresentation({profile:'NK_6000_WORKBOOK_V1',versionNo:950001}).label,/950001/);
  assert.match(templatePresentation({profile:'NK_6000_WORKBOOK_V1'}).hint,/56 cột/);
});
test('An unknown or absent template cannot advertise V2 cache support',()=>{
  assert.doesNotMatch(templatePresentation(null).hint,/Mẫu NK V2/);
});
