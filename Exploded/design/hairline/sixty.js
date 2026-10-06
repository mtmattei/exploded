/**
 * Sixty: the app's 60% build taken apart along its build axis, in the five
 * layers it is sold as. A tray case, a PCB with a hot-swap socket under every
 * key and a USB-C port at the back, a plate with its switch cutouts, the
 * switches, and the sculpted keycaps. The pointer's x opens the stack through
 * a spring; its y picks a layer, which takes the bright edge, and its callout
 * number and name go to the read-out. At rest the stack is barely open and the
 * plate is lit, the layer the drawing is named for. The slider is the gap.
 *
 * The pattern: scrub and pick, as the package's Exploded. Picking is tested
 * against the target gap, so a layer moving out from under the pointer cannot
 * flip the choice; drops are painted before the layer they lead to.
 */
const { Cam, circ, clamp, extremes, facing, fit, hull, open, poly, proj, prism, ringAt, rings, rrect, run, seg, spring, stepS, disposer, mk, pointer, put, register, solid } = HL;

const U = 14, W = 15 * U, D = 5 * U, BZ = 5, CASE = 7, REST = 0.45, GMAX = 30;
/** Key widths per row, back to front: 61 keys, ANSI, every row 15 units. */
const ROWS = [
  [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2],
  [1.5, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1.5],
  [1.75, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.25],
  [2.25, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.75],
  [1.25, 1.25, 1.25, 6.25, 1.25, 1.25, 1.25, 1.25],
];
/** Each row's cap height, back to front: the sculpted profile, lowest on the home row. */
const CAP_H = [10, 9, 8.2, 8.7, 9.4];
/** Bottom to top, as the stack is built; the callout number counts down from the top. */
const NAMES = ["case", "pcb", "plate", "switches", "keycaps"];
/** Each layer's footprint and thickness; caps and switches have no slab of their own. */
const SLAB = [[-BZ, -BZ, W + BZ, D + BZ, 7, CASE], [-2, -2, W + 2, D + 2, 3, 1.6], [-1, -1, W + 1, D + 1, 3, 1.5], null, null];

function inside(pt, pg) {
  let c = false;
  for (let i = 0, j = pg.length - 1; i < pg.length; j = i++) {
    const [xi, yi] = pg[i], [xj, yj] = pg[j];
    if ((yi > pt[1]) !== (yj > pt[1]) && pt[0] < ((xj - xi) * (pt[1] - yi)) / (yj - yi) + xi) c = !c;
  }
  return c;
}

function mount({ stage, svg, read }, value) {
  const bag = disposer();
  let GAP = value, act = -1, lastP = null;
  const e = spring(REST, { eps: 0.002 });
  const C = Cam(45, 0.5, 1.22);
  const top = 4 * GMAX + CAP_H[0];
  fit(C, [[-BZ, -BZ, -CASE], [W + BZ, D + BZ, -CASE], [W + BZ, -BZ, -CASE], [-BZ, D + BZ, -CASE], [-BZ, -BZ, top], [W + BZ, -BZ, top]], 200, 166);
  const P = proj(C), front = facing(C);
  const g = mk("g", {}, svg);

  const keys = [];
  ROWS.forEach((row, r) => {
    let x = 0;
    for (const w of row) { keys.push({ r, x0: x * U, x1: (x + w) * U, y0: r * U, y1: (r + 1) * U }); x += w; }
  });
  const centre = (k) => [(k.x0 + k.x1) / 2, (k.y0 + k.y1) / 2];
  const sq = (k, s, rad) => { const [cx, cy] = centre(k); return rrect(cx - s, cy - s, cx + s, cy + s, rad, 3); };
  const socket = circ(1.6, 8), field = rrect(0, 0, W, D, 3, 3), fieldExt = extremes(P, field);

  // Bottom layer first, each layer's drop guide before its own drawing.
  const els = NAMES.map((name, i) => {
    const guide = i > 0 ? mk("path", { class: "nf dash" }, g) : null;
    const s = SLAB[i], o = { guide };
    if (s) {
      [o.ring, o.inner] = rings(s[0], s[1], s[2], s[3], s[4], 1.2);
      o.ext = extremes(P, o.ring);
      o.el = solid(g);
      o.marks = mk("path", { class: "nf lo" }, o.el.g);
      if (i === 1) o.port = solid(g);
    } else if (i === 3) {
      o.tiles = mk("path", { class: "lo" }, g);
      o.stems = mk("path", { class: "nf lo" }, g);
    } else {
      o.caps = keys.map((k) => {
        const t = 0.8 + 1.6;
        return {
          k, h: CAP_H[k.r], el: solid(g),
          foot: rrect(k.x0 + 0.8, k.y0 + 0.8, k.x1 - 0.8, k.y1 - 0.8, 2.4, 4),
          top: rrect(k.x0 + t, k.y0 + t, k.x1 - t, k.y1 - t, 1.8, 4),
          inner: rrect(k.x0 + t + 0.9, k.y0 + t + 0.9, k.x1 - t - 0.9, k.y1 - t - 0.9, 1.1, 4),
        };
      });
    }
    return o;
  });
  const [pr, pi] = rings(W / 2 - 7, -2, W / 2 + 7, 4, 2, 0.6);

  /** A layer's base and top at a given gap share. */
  const zb = (i, s) => i * GAP * s;
  const zt = (i, s) => zb(i, s) + (SLAB[i] ? (i === 0 ? 0 : SLAB[i][5]) : i === 3 ? 5 : 9);
  const corners = (i, z) => { const s = SLAB[i] || [0, 0, W, D]; return [P(s[0], s[1], z), P(s[2], s[1], z), P(s[2], s[3], z), P(s[0], s[3], z)]; };

  /** The topmost layer under the pointer, where the layers are going, not where they are. */
  function pick(p) {
    if (!p) return -1;
    for (let i = NAMES.length - 1; i >= 0; i--) if (inside(p, corners(i, zt(i, e.t)))) return i;
    return -1;
  }
  function light() {
    const on = act >= 0 ? act : 2;
    els.forEach((E, i) => {
      if (E.el) E.el.sil.classList.toggle("hi", i === on);
      if (E.tiles) E.tiles.setAttribute("class", i === on ? "hi" : "lo");
      if (E.caps) for (const c of E.caps) c.el.sil.classList.toggle("hi", i === on);
    });
  }

  let drawn = NaN;
  const B = register(stage, (dt) => {
    const m = stepS(e, dt), s = e.x;
    if (s !== drawn) {
      drawn = s;
      els.forEach((E, i) => {
        const z0 = zb(i, s), z1 = zt(i, s);
        if (E.guide) {
          const ext = E.ext || fieldExt, zp = zt(i - 1, s);
          E.guide.setAttribute("d", ext.map((q) => seg(P(q.u, q.v, i === 0 ? -CASE : z0), P(q.u, q.v, zp))).join(""));
        }
        if (i === 0) {
          put(E.el, prism(P, front, E.ring, E.inner, -CASE, 0));
          E.marks.setAttribute("d", poly(ringAt(P, rrect(-BZ + 3, -BZ + 3, W + BZ - 3, D + BZ - 3, 4, 3), 0)) + open(ringAt(P, run(E.inner, (q) => !front(q)), -CASE + 2)));
        } else if (i === 1) {
          put(E.el, prism(P, front, E.ring, E.inner, z0, z1));
          E.marks.setAttribute("d", keys.map((k) => { const [cx, cy] = centre(k); return poly(socket.map((q) => P(cx + q.u, cy + q.v + 3, z1))); }).join(""));
          put(E.port, prism(P, front, pr, pi, z1, z1 + 3));
        } else if (i === 2) {
          put(E.el, prism(P, front, E.ring, E.inner, z0, z1));
          E.marks.setAttribute("d", keys.map((k) => poly(ringAt(P, sq(k, 5.2, 0.8), z1))).join(""));
        } else if (i === 3) {
          E.tiles.setAttribute("d", keys.map((k) => poly(hull(ringAt(P, sq(k, 5.2, 1), z0).concat(ringAt(P, sq(k, 4, 1.2), z1))))).join(""));
          E.stems.setAttribute("d", keys.map((k) => { const [cx, cy] = centre(k); return seg(P(cx - 1.6, cy, z1), P(cx + 1.6, cy, z1)) + seg(P(cx, cy - 1.6, z1), P(cx, cy + 1.6, z1)); }).join(""));
        } else {
          for (const c of E.caps) put(c.el, { sil: poly(hull(ringAt(P, c.foot, z0).concat(ringAt(P, c.top, z0 + c.h)))), crease: open(ringAt(P, run(c.inner, front), z0 + c.h)) });
        }
      });
    }
    return m;
  });
  bag.add(B.unregister);

  function retarget() {
    const a = pick(lastP);
    if (a !== act) { act = a; light(); }
    read.textContent = act >= 0 ? `0${5 - act} · ${NAMES[act]}` : "rest";
    B.wake();
  }
  light();
  read.textContent = "rest";

  bag.add(pointer(stage, {
    move: (p) => { lastP = p; e.t = REST + (1 - REST) * clamp((p[0] - 60) / 280, 0, 1); retarget(); },
    leave: () => { lastP = null; e.t = REST; retarget(); },
  }));
  bag.add(() => svg.replaceChildren());

  return {
    set: (v) => { GAP = v; drawn = NaN; retarget(); },
    destroy: bag.dispose,
  };
}

hairline({
  name: "sixty",
  means: "A 60% keyboard in its five layers: move across to open the stack, and down to pick a layer.",
  rules: [1, 4, 5, 6, 9],
  range: [14, 22, 30],
  mount,
});
