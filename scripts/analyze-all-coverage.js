const fs = require('fs');
const path = require('path');
const covFile = path.join(__dirname, '..', 'DataverseLabelTranslator.WebResource', 'coverage', 'coverage-final.json');
const cov = JSON.parse(fs.readFileSync(covFile, 'utf8'));
const files = Object.keys(cov).filter(k => k.includes('\\js\\') || k.includes('/js/'));
console.log('Found ' + files.length + ' JS files');
for (const fileKey of files) {
    const fileCov = cov[fileKey];
    const stmtMap = fileCov.statementMap;
    const s = fileCov.s;
    const total = Object.keys(stmtMap).length;
    const covered = Object.keys(stmtMap).filter(k => s[k] > 0).length;
    const fnMap = fileCov.fnMap;
    const f = fileCov.f;
    const fnTotal = Object.keys(fnMap).length;
    const fnCovered = Object.keys(fnMap).filter(k => f[k] > 0).length;
    const branchMap = fileCov.branchMap;
    const b = fileCov.b;
    const branchTotal = Object.keys(branchMap).length;
    const branchCovered = Object.keys(branchMap).filter(k => b[k].some(c => c > 0)).length;
    const fname = fileKey.split('/').pop();
    console.log(fname + ': stmts=' + covered + '/' + total + ' (' + (covered/total*100).toFixed(2) + '%), fns=' + fnCovered + '/' + fnTotal + ', branches=' + branchCovered + '/' + branchTotal);
}
