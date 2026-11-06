// UI presentation only; backend authorization remains authoritative.
export const tradeKinds=Object.freeze([{code:'OTHER_IMPORT',label:'OTHER Nhập'},{code:'OTHER_EXPORT',label:'OTHER Xuất'},{code:'XK',label:'Xuất khẩu'},{code:'PIN_TEST',label:'Pin Test'}]);
const known=new Set(['OTHER_IMPORT_1000','OTHER_IMPORT_6000','OTHER_EXPORT_1000','OTHER_EXPORT_6000','XK_1000','XK_6000','PIN_TEST_1000']);
export function splitSource(code){if(!known.has(code))throw new Error('Nguồn chưa được hỗ trợ.');const i=code.lastIndexOf('_');return {kind:code.slice(0,i),site:code.slice(i+1)};}
export function sourceLabel(code){const {kind,site}=splitSource(code);return `${tradeKinds.find(x=>x.code===kind).label} · Site ${site}`;}
export function sourceChoices(allowed,kind){return [...new Set(allowed.map(x=>x.sourceCode).filter(x=>known.has(x)))].filter(x=>splitSource(x).kind===kind).map(x=>({sourceCode:x,...splitSource(x)})).sort((a,b)=>a.site.localeCompare(b.site));}
export function resolveSource(allowed,kind,site){const x=sourceChoices(allowed,kind).find(x=>x.site===site);if(!x)throw new Error('Nguồn/site chưa được cấu hình hoặc bạn chưa được cấp quyền.');return x.sourceCode;}
