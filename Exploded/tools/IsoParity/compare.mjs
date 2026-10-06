import { readFileSync } from 'node:fs';
const a = JSON.parse(readFileSync('cs.json', 'utf8')), b = JSON.parse(readFileSync('js.json', 'utf8'));
let worst = 0, count = 0, fails = [];
function walk(x, y, path) {
  if (Array.isArray(x)) { if (!Array.isArray(y) || x.length !== y.length) { fails.push(`${path}: length ${x.length} vs ${y?.length}`); return; } x.forEach((v, i) => walk(v, y[i], `${path}[${i}]`)); }
  else if (typeof x === 'object') { for (const k of Object.keys(x)) walk(x[k], y[k], `${path}.${k}`); }
  else { count++; const d = Math.abs(x - y); worst = Math.max(worst, d); if (d > 1e-9) fails.push(`${path}: ${x} vs ${y}`); }
}
walk(a, b, '');
console.log(`${count} numbers compared, worst difference ${worst.toExponential(2)}, ${fails.length} beyond 1e-9`);
for (const f of fails.slice(0, 10)) console.log('  ' + f);
process.exit(fails.length ? 1 : 0);
