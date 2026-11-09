// Headless-матрица Эльо v1.3d: 100 партий на каждую вариацию, без LLM.
// 3 порядка хода × 2 типа игры × 3 варианта пешек = 18 вариаций.
// ИИ играет все три фракции; seeded RNG; integrity-guard.
const fs = require('fs');
const path = require('path');
const E = (() => { global.window = global; require(path.join(__dirname, '..', '..', 'elio_engine.js')); return window.ElioEngine; })();
const AI = (() => { require(path.join(__dirname, '..', '..', 'elio_ai.js')); return window.ElioAI; })();

const N = parseInt(process.env.N || '100', 10);
const BASE_SEED = parseInt(process.env.SEED || '20261108', 10);
const OUT_DIR = process.env.OUT_DIR || path.join(__dirname, '..', '2026-11-08_headless100_v13d');

const VARIANTS = [];
for (const turn of ['cycle', 'arrow', 'pass']) {
  for (const death of ['main', 'long']) {
    for (const pawns of [0, 1, 5]) {
      VARIANTS.push({ turn, death, pawns, passRule: 'random', cycleDir: 'forward' });
    }
  }
}
const ALL_VARIANTS = VARIANTS.slice();
const ONLY = (process.env.ONLY || '').split(',').map(s => s.trim()).filter(Boolean);
const RUN_VARIANTS = ONLY.length
  ? ALL_VARIANTS.filter(v => ONLY.includes(v.turn + '+' + v.death + '+p' + v.pawns))
  : ALL_VARIANTS;

function mulberry32(a) {
  return function () {
    a |= 0; a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function integrityErrors(s) {
  const errors = [];
  const seen = new Set();
  for (const [k, c] of s.cells) {
    if (!c.piece) continue;
    const p = s.pieces.find(x => x.id === c.piece);
    if (!p) { errors.push('cell ' + k + ' -> missing ' + c.piece); continue; }
    if (!p.alive) errors.push('cell ' + k + ' -> dead ' + p.id);
    if (p.q !== c.q || p.r !== c.r) errors.push('cell ' + k + ' -> ' + p.id + ' at ' + p.q + ',' + p.r);
    if (seen.has(p.id)) errors.push('duplicate cell ' + p.id);
    seen.add(p.id);
  }
  for (const p of s.pieces.filter(x => x.alive)) {
    if (!E.isValidCell(s, p.q, p.r)) errors.push('offboard ' + p.id + ' ' + p.q + ',' + p.r);
    const c = s.cells.get(p.q + ',' + p.r);
    if (!c || c.piece !== p.id) errors.push('unlinked ' + p.id);
  }
  for (const p of s.pieces) {
    if (!p.supportTarget) continue;
    const t = s.pieces.find(x => x.id === p.supportTarget);
    if (!t || !t.alive || E.dist([p.q, p.r], [t.q, t.r]) !== 1) errors.push('stale support ' + p.id + ' -> ' + p.supportTarget);
  }
  return errors;
}

function oneGame(v, gameIndex, variantIndex) {
  const seed = (BASE_SEED + variantIndex * 100003 + gameIndex * 7919) >>> 0;
  Math.random = mulberry32(seed);
  let s = E.init({ turn: v.turn, death: v.death, pawns: v.pawns, passRule: v.passRule, cycleDir: v.cycleDir });
  const startFaction = s.turnFaction;
  const row = {
    variantIndex, gameIndex, seed, startFaction,
    turn: v.turn, death: v.death, pawns: v.pawns, limit: s.limit,
    winner: null, reason: null, moves: 0, lineDeaths: 0,
    contaminated: false, stuck: false,
    centerEntered: false, centerTicks: { plus: 0, minus: 0, zero: 0 },
    holdMax: { plus: 0, minus: 0, zero: 0 },
    eliminated: false, errors: [],
  };
  let safety = 0;
  let lastCenter = null;
  while (!s.gameOver && safety < 400) {
    safety++;
    const f = s.turnFaction;
    let mv = f === 'zero' ? AI.chooseMove(s, f) : AI.chooseMoveAggressive(s, f);
    const act = mv && (mv.act || {});
    const passTo = (s.mode.turn === 'pass' && mv && mv.passTo) ? mv.passTo : undefined;
    let r;
    if (!mv || mv.type === 'skip' || (!act.type && !act.kind)) r = E.skipTurn(s, passTo);
    else if (mv.type === 'support') r = E.applySupport(s, act.zeroId, act.targetId, passTo);
    else r = E.applyMove(s, act.pieceId, act.q, act.r, passTo);
    if (r.error) { row.errors.push('action: ' + r.error); row.contaminated = true; break; }
    if (r.record && r.record.kind === 'displace') {
      // легальное оттеснение; проверяем, что клетки согласованы ниже
    }
    if (r.record && r.record.capture && r.record.capture.type === 'line') row.lineDeaths++;
    s = r.state;
    const cp = E.occupantAt(s);
    if (cp) {
      row.centerEntered = true;
      row.centerTicks[cp.faction] = (row.centerTicks[cp.faction] || 0) + 1;
      lastCenter = cp.faction;
    }
    for (const x of ['plus', 'minus', 'zero']) {
      if (s.hold[x] > row.holdMax[x]) row.holdMax[x] = s.hold[x];
    }
    const ie = integrityErrors(s);
    if (ie.length) { row.errors.push('integrity: ' + ie.join('; ')); row.contaminated = true; break; }
  }
  row.stuck = !s.gameOver;
  row.moves = s.moveNumber;
  row.winner = s.winner || 'draw';
  row.reason = s.winReason || (row.stuck ? 'stuck' : 'draw');
  row.eliminated = !!(s.eliminated.plus && s.eliminated.minus);
  if (row.stuck && !row.contaminated) row.errors.push('safety limit without gameOver');
  return row;
}

const t0 = Date.now();
const all = [];
for (const v of RUN_VARIANTS) {
  const vi = ALL_VARIANTS.indexOf(v);
  const rows = [];
  for (let gi = 0; gi < N; gi++) rows.push(oneGame(v, gi, vi));
  all.push(...rows);
  const label = v.turn + '+' + v.death + '+p' + v.pawns;
  const wins = { plus: 0, minus: 0, zero: 0, draw: 0 };
  for (const r of rows) wins[r.winner === 'draw' ? 'draw' : r.winner]++;
  const contaminated = rows.filter(r => r.contaminated).length;
  const avgMoves = (rows.reduce((a, r) => a + r.moves, 0) / rows.length).toFixed(1);
  console.log(label + ': P' + wins.plus + ' M' + wins.minus + ' Z' + wins.zero + ' D' + wins.draw + ' avg=' + avgMoves + ' cont=' + contaminated);
  if (contaminated) {
    const ex = rows.find(r => r.contaminated);
    console.log('  !! contamination example:', ex.errors.join('; '));
  }
}

function aggregate(rows) {
  const g = { games: rows.length, wins: { plus: 0, minus: 0, zero: 0, draw: 0 }, reasons: {}, avgMoves: 0, lineDeaths: 0, centerEntered: 0, centerTicks: { plus: 0, minus: 0, zero: 0 }, holdMax: { plus: 0, minus: 0, zero: 0 }, contaminated: 0, stuck: 0, eliminated: 0 };
  let moves = 0;
  for (const r of rows) {
    g.wins[r.winner === 'draw' ? 'draw' : r.winner]++;
    g.reasons[r.reason] = (g.reasons[r.reason] || 0) + 1;
    moves += r.moves;
    g.lineDeaths += r.lineDeaths;
    if (r.centerEntered) g.centerEntered++;
    g.centerTicks.plus += r.centerTicks.plus;
    g.centerTicks.minus += r.centerTicks.minus;
    g.centerTicks.zero += r.centerTicks.zero;
    g.holdMax.plus = Math.max(g.holdMax.plus, r.holdMax.plus);
    g.holdMax.minus = Math.max(g.holdMax.minus, r.holdMax.minus);
    g.holdMax.zero = Math.max(g.holdMax.zero, r.holdMax.zero);
    if (r.contaminated) g.contaminated++;
    if (r.stuck) g.stuck++;
    if (r.eliminated) g.eliminated++;
  }
  g.avgMoves = +(moves / rows.length).toFixed(2);
  return g;
}

const summary = RUN_VARIANTS.map(v => {
  const vi = ALL_VARIANTS.indexOf(v);
  return {
    ...v,
    aggregate: aggregate(all.filter(r => r.variantIndex === vi)),
  };
});
const out = {
  engine: 'elio_engine.js v1.3d',
  ai: 'elio_ai.js v1.4',
  date: '2026-11-08',
  baseSeed: BASE_SEED,
  gamesPerVariant: N,
  variants: RUN_VARIANTS.length,
  selected: ONLY,
  passToFix: true,
  games: all.length,
  elapsedSec: +((Date.now() - t0) / 1000).toFixed(1),
  summary,
  games_detail: all,
};
fs.mkdirSync(OUT_DIR, { recursive: true });
fs.writeFileSync(path.join(OUT_DIR, 'results.json'), JSON.stringify(out, null, 1));
console.log('=== SUMMARY ===');
console.log(JSON.stringify({ engine: out.engine, games: out.games, elapsedSec: out.elapsedSec, variants: summary.map(s => ({ key: s.turn + '+' + s.death + '+p' + s.pawns, ...s.aggregate })) }, null, 2));
