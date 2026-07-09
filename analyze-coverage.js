const fs = require('fs');
const file = 'DataverseLabelTranslator.Test/TestResults/coverage.cobertura.xml';
if (!fs.existsSync(file)) {
    console.log('NOT FOUND');
    process.exit();
}
const x = fs.readFileSync(file, 'utf8');
const pat = /<class name="([^"]+)" filename="([^"]+)"[^>]*line-rate="([0-9.]+)"[^>]*branch-rate="([0-9.]+)"/g;
let m;
const classes = [];
while ((m = pat.exec(x)) !== null) {
    classes.push({
        n: m[1].split('.').pop(),
        f: m[2].split(/[\\/]/).pop(),
        l: Math.round(parseFloat(m[3]) * 100),
        b: Math.round(parseFloat(m[4]) * 100)
    });
}

// Group by source file and show files with low line coverage
const byfile = {};
classes.forEach(c => {
    if (!byfile[c.f]) byfile[c.f] = { sul: [], sub: [] };
    byfile[c.f].sul.push(c.l);
    byfile[c.f].sub.push(c.b);
});
const fileSummary = Object.keys(byfile).map(f => {
    const arrL = byfile[f].sul;
    const arrB = byfile[f].sub;
    const avgL = Math.round(arrL.reduce((s, v) => s + v, 0) / arrL.length);
    const avgB = Math.round(arrB.reduce((s, v) => s + v, 0) / arrB.length);
    return { f, L: avgL, B: avgB, count: arrL.length };
});
fileSummary.sort((a, b) => (a.L + a.B) - (b.L + b.B));
console.log('===> File-level coverage (ascending):');
fileSummary.forEach(c => console.log(c.f.padEnd(55), 'L:' + String(c.L).padStart(3, ' ') + '% B:' + String(c.B).padStart(3, ' ') + '%', '(' + c.count + ' classes)'));
console.log('---TOTAL classes:', classes.length);
