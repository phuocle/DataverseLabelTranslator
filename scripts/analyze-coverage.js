// Analyze coverage for DataverseLabelTranslator.js
const fs = require('fs');
const path = require('path');

const covFile = path.join(__dirname, '..', 'DataverseLabelTranslator.WebResource', 'coverage', 'coverage-final.json');
const cov = JSON.parse(fs.readFileSync(covFile, 'utf8'));

const fileKey = Object.keys(cov).find(k => k.endsWith('DataverseLabelTranslator.js'));
const fileCov = cov[fileKey];

const stmtMap = fileCov.statementMap;
const s = fileCov.s;
const fnMap = fileCov.fnMap;
const f = fileCov.f;
const branchMap = fileCov.branchMap;
const b = fileCov.b;

const stmtIds = Object.keys(stmtMap).map(k => parseInt(k));
const total = stmtIds.length;

const uncovered = stmtIds.filter(id => s[id] === 0).map(id => stmtMap[id].start.line).filter((v, i, a) => a.indexOf(v) === i).sort((a, b) => a - b);

const fnIds = Object.keys(fnMap).map(k => parseInt(k));
const fnUncovered = fnIds.filter(id => f[id] === 0).map(id => fnMap[id].name + ' @ ' + fnMap[id].decl.start.line);

const branchIds = Object.keys(branchMap).map(k => parseInt(k));
const branchUncovered = branchIds.filter(id => b[id].filter(c => c > 0).length === 0).map(id => branchMap[id].loc.start.line);

console.log('File: ' + fileKey);
console.log('Total statements: ' + total);
console.log('Covered: ' + stmtIds.filter(id => s[id] > 0).length);
console.log('Uncovered statements lines: ' + JSON.stringify(uncovered));
console.log('Uncovered functions: ' + JSON.stringify(fnUncovered, null, 2));
console.log('Uncovered branches: ' + JSON.stringify(branchUncovered));
