import { readFileSync } from 'node:fs';
const src = readFileSync(process.argv[2] ?? (process.env.HOME + '/.claude/skills/hairline-create/kernel.js'), 'utf8');
const HL = new Function(src + '\n;return HL;')();
const { Cam, fit, proj, unproj, rings, rrect, prism, facing, hull, ringAt, run, extremes, fillet, circ } = HL;
const cams = [];
for (const [az, k, S] of [[45, 0.5, 1.62], [0, 0.34, 2], [123, 0.64, 1.3], [-80, 0.5, 1.85]]) {
  const C = Cam(az, k, S); fit(C, [[0, 0, 0], [100, 80, 0], [0, 0, 40]], 200, 160); const P = proj(C);
  const pts = [[0, 0, 0], [12.5, -40, 0], [80, 33, 17], [-20, 60, -9]];
  cams.push({ cam: [C.ox, C.oy], proj: pts.map((q) => P(...q)), unproj: pts.map((q) => { const s = P(...q); return unproj(C, s[0], s[1], q[2]); }) });
}
const C = Cam(45, 0.5, 1.42);
fit(C, [[0, 0, 0], [132, 96, 0], [132, 0, 0], [0, 96, 0], [0, 0, 104.4], [132, 0, 104.4]], 180, 166);
const P = proj(C), front = facing(C);
const [ring, inner] = rings(0, 0, 132, 96, 7, 1.3);
// prism() returns path strings; recompute its points the way it does
const sil = hull(ringAt(P, ring, 2.4).concat(ringAt(P, ring, 0)));
const crease = ringAt(P, run(inner, front), 2.4);
const foot = rrect(0.8, 0.8, 13.2, 13.2, 2.6), top = rrect(2.6, 2.6, 11.4, 11.4, 2.0), tin = rrect(3.5, 3.5, 10.5, 10.5, 1.2);
const tsil = hull(ringAt(P, foot, 1).concat(ringAt(P, top, 11.3)));
const tcrease = ringAt(P, run(tin, front), 11.3);
const ext = extremes(P, ring);
const card = fillet([[0, 0], [84, 0], [84, 54], [28, 54], [28, 61], [6, 61], [6, 54], [0, 54]], [1, 1, 3.2, 1.8, 2.4, 2.4, 1.8, 3.2]);
const S = (s) => s.map((q) => [q.u, q.v, q.nu, q.nv]);
const out = { cams, ring: S(ring), inner: S(inner), sil, crease, tsil, tcrease, ext: S(ext), fillet: card, circ: S(circ(5, 24)), hull: hull([[0, 0], [10, 0], [10, 10], [0, 10], [5, 5], [5, 12], [-1, 5]]) };
console.log(JSON.stringify(out));
