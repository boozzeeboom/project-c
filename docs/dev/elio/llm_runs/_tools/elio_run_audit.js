// Аудит сырых логов арены: легальность, replay, целостность состояния,
// залипшие щиты, осцилляции, пропущенные защиты Линейного, возможности «Ц».
const path = require('path');
global.window = global;
require(path.join(__dirname, '..', '..', 'elio_engine.js'));
const E = window.ElioEngine;
const OUT = 'C:/Users/leon7/AppData/Local/Temp/opencode/';

function loadMatches(paths) {
  const out = [];
  for (const p of paths) {
    const j = JSON.parse(require('fs').readFileSync(p, 'utf8'));
    if (Array.isArray(j)) out.push(...j); else out.push(j);
  }
  return out;
}
function normAct(entry) {
  const a = entry.act;
  if (!a) return null;
  if (a.kind === 'skip' || a.type === 'skip') return { kind: 'skip' };
  if (a.kind === 'support' || a.type === 'support') {
    return { kind: 'support', zeroId: a.zeroId, targetId: a.targetId === undefined ? null : a.targetId, explicitTarget: Object.prototype.hasOwnProperty.call(a, 'targetId') };
  }
  if (a.kind === 'move') {
    const to = a.to || a;
    return { kind: 'move', pieceId: a.pieceId, q: to.q, r: to.r, displace: !!a.displace };
  }
  if (a.type === 'move') return { kind: 'move', pieceId: a.pieceId, q: a.q, r: a.r, displace: !!a.displace };
  return null;
}
function keyOf(a) {
  if (!a) return 'null';
  if (a.kind === 'skip') return 'skip';
  if (a.kind === 'support') return 'support|' + a.zeroId + '|' + (a.targetId || '');
  return 'move|' + a.pieceId + '|' + a.q + ',' + a.r;
}
function legalActions(state, faction) {
  const acts = [];
  for (const p of state.pieces.filter(x => x.alive && !x.inert && x.faction === faction)) {
    if (E.isNullType(p.type)) {
      for (const t of E.legalSupportTargets(state, p.id)) acts.push({ kind: 'support', zeroId: p.id, targetId: t });
      if (p.supportTarget) acts.push({ kind: 'support', zeroId: p.id, targetId: null });
    }
    for (const m of E.legalMoves(state, p.id)) {
      acts.push({ kind: 'move', pieceId: p.id, q: m.q, r: m.r, displace: !!m.displace });
    }
  }
  if (!acts.length) acts.push({ kind: 'skip' });
  return acts;
}
function selectActions(acts, faction) {
  if (faction !== 'zero') return acts.slice(0, 12);
  const moves = acts.filter(a => a.kind === 'move');
  const supports = acts.filter(a => a.kind === 'support');
  const gateMoves = moves.filter(a => (a.q === 0 && a.r === 0) || E.GATE_SET.has(a.q + ',' + a.r));
  const otherMoves = moves.filter(a => gateMoves.indexOf(a) === -1);
  const picked = [];
  picked.push(...gateMoves.slice(0, 6));
  picked.push(...otherMoves.slice(0, 4));
  picked.push(...supports.slice(0, 6));
  for (const a of [...moves, ...supports, { kind: 'skip' }]) {
    if (picked.length >= 16) break;
    if (picked.indexOf(a) === -1) picked.push(a);
  }
  return picked.slice(0, 16);
}
function apply(s, a) {
  if (a.kind === 'skip') return E.skipTurn(s);
  if (a.kind === 'support') return E.applySupport(s, a.zeroId, a.targetId);
  return E.applyMove(s, a.pieceId, a.q, a.r);
}
function integrity(s) {
  const errs = [];
  const seen = new Set();
  for (const [k, c] of s.cells) {
    if (!c.piece) continue;
    const p = s.pieces.find(x => x.id === c.piece);
    if (!p) { errs.push('cell ' + k + ' -> missing piece ' + c.piece); continue; }
    if (!p.alive) errs.push('cell ' + k + ' -> dead piece ' + p.id);
    if (p.q !== c.q || p.r !== c.r) errs.push('cell ' + k + ' -> piece ' + p.id + ' at ' + p.q + ',' + p.r);
    if (seen.has(p.id)) errs.push('piece on multiple cells ' + p.id);
    seen.add(p.id);
  }
  const alive = s.pieces.filter(p => p.alive);
  for (const p of alive) {
    if (!E.isValidCell(s, p.q, p.r)) errs.push('alive piece off board ' + p.id + ' ' + p.q + ',' + p.r);
    const c = s.cells.get(p.q + ',' + p.r);
    if (!c || c.piece !== p.id) errs.push('alive piece not linked to cell ' + p.id + ' ' + p.q + ',' + p.r);
  }
  const pos = new Set();
  for (const p of alive) {
    const k = p.q + ',' + p.r;
    if (pos.has(k)) errs.push('two alive pieces on ' + k);
    pos.add(k);
  }
  return errs;
}
function staleSupports(s) {
  const out = [];
  for (const p of s.pieces) {
    if (!p.supportTarget) continue;
    const t = s.pieces.find(x => x.id === p.supportTarget);
    if (!t || !t.alive) { out.push({ supporter: p.id, target: p.supportTarget, reason: 'target dead' }); continue; }
    const d = E.dist([p.q, p.r], [t.q, t.r]);
    if (d !== 1) out.push({ supporter: p.id, target: t.id, dist: d });
  }
  return out;
}
function threatsToLines(s) {
  const out = [];
  for (const f of ['plus', 'minus', 'zero']) {
    const line = s.pieces.find(p => p.alive && !p.inert && p.faction === f && p.type === 'line');
    if (!line) continue;
    const attackers = [];
    for (const p of s.pieces) {
      if (!p.alive || p.inert || p.faction === f || E.isNullType(p.type)) continue;
      const c = E.clone(s);
      c.turnFaction = p.faction;
      const ms = E.legalMoves(c, p.id);
      if (ms.some(m => m.q === line.q && m.r === line.r)) attackers.push(p.id);
    }
    if (attackers.length) out.push({ line: line.id, faction: f, attackers });
  }
  return out;
}

const paths = process.argv.slice(2);
if (!paths.length) paths.push(OUT + 'elio_zero_test.json');
const matches = loadMatches(paths);
const report = [];
for (const m of matches) {
  const s0 = E.init({ turn: 'arrow', death: 'main', pawns: 1 });
  s0.limit = 60;
  let s = s0;
  const llm = { actions: 0, moves: 0, supports: 0, skips: 0, displaces: 0, captures: 0, lineKills: 0, supportNull: 0, noopSupports: 0, osc1: 0, osc2: 0, repeatPiece: 0, gateMoves: 0, centerTicks: 0, maxHold: 0, holdTicks: 0 };
  const ai = { actions: 0, moves: 0, supports: 0, skips: 0, displaces: 0, captures: 0, lineKills: 0, osc1: 0, osc2: 0, repeatPiece: 0, centerTicks: 0, maxHold: 0, holdTicks: 0 };
  const flags = { staleSupports: [], integrity: [], illegal: [], outsideDisplayed: [], missedLineDefense: [], noDefenseAvailable: [], centerOpportunities: [], supportTargets: [], lineDeaths: [], fallbacks: 0, errors: [] };
  const lastFrom = {}; const posHistory = {}; const lastMovePiece = { plus: null, minus: null, zero: null };
  let llmTurns = 0;
  let broke = false;
  for (let i = 0; i < (m.log || []).length; i++) {
    const entry = m.log[i];
    if (entry.error) { flags.errors.push({ i, move: entry.move, error: entry.error }); continue; }
    if (entry.fallback) flags.fallbacks++;
    if (s.gameOver) { flags.errors.push({ i, move: s.moveNumber, error: 'action after gameOver' }); break; }
    const a = normAct(entry);
    if (!a) { flags.errors.push({ i, error: 'unparsed act' }); break; }
    const f = entry.f;
    s.turnFaction = f;
    const acts = legalActions(s, f);
    const legalKey = keyOf(a);
    const legal = acts.some(x => keyOf(x) === legalKey);
    if (!legal) flags.illegal.push({ i, move: s.moveNumber, f, act: a, legalSamples: acts.slice(0, 8).map(keyOf) });
    const isLLM = !entry.ai && !entry.fallback;
    if (isLLM) {
      llmTurns++;
      const displayed = selectActions(acts, f).map(keyOf);
      if (displayed.indexOf(legalKey) === -1) flags.outsideDisplayed.push({ i, move: s.moveNumber, act: a });
      // возможности центра
      const centerMove = acts.find(x => x.kind === 'move' && x.q === 0 && x.r === 0);
      if (centerMove && legalKey !== keyOf(centerMove)) flags.centerOpportunities.push({ i, move: s.moveNumber, chosen: a, center: keyOf(centerMove) });
      // защита Линейного
      const th = threatsToLines(s);
      for (const t of th) {
        const line = s.pieces.find(p => p.id === t.line);
        const supports = acts.filter(x => x.kind === 'support' && x.targetId === t.line);
        if (supports.length && !supports.some(x => keyOf(x) === legalKey)) {
          flags.missedLineDefense.push({ i, move: s.moveNumber, line: t.line, attackers: t.attackers, available: supports.map(keyOf), chosen: a });
        } else if (!supports.length) {
          flags.noDefenseAvailable.push({ i, move: s.moveNumber, line: t.line, attackers: t.attackers, linePos: line.q + ',' + line.r });
        }
      }
      if (a.kind === 'support') {
        const t = s.pieces.find(p => p.id === a.targetId);
        flags.supportTargets.push({ i, move: s.moveNumber, zero: a.zeroId, target: a.targetId, targetType: t ? t.type : null, targetFaction: t ? t.faction : null, nullTarget: a.targetId === null });
        if (a.targetId === null) llm.supportNull++;
      }
    }
    const b = isLLM ? llm : ai;
    b.actions++;
    const piece = a.kind === 'move' ? s.pieces.find(p => p.id === a.pieceId) : null;
    const prevHold = { ...s.hold };
    const prevCenter = E.occupantAt(s);
    // no-op support
    if (a.kind === 'support') {
      const z = s.pieces.find(p => p.id === a.zeroId);
      if (z && (z.supportTarget || null) === (a.targetId || null)) { if (isLLM) llm.noopSupports++; }
    }
    const res = apply(s, a);
    if (res.error) { flags.errors.push({ i, move: s.moveNumber, error: res.error, act: a }); break; }
    s = res.state;
    if (entry.after !== undefined && entry.after !== s.moveNumber) flags.errors.push({ i, after: entry.after, replayAfter: s.moveNumber });
    if (entry.winner !== undefined && (entry.winner || null) !== (s.winner || null)) flags.errors.push({ i, winnerLog: entry.winner, winnerReplay: s.winner });
    if (entry.reason !== undefined && (entry.reason || null) !== (s.winReason || null)) flags.errors.push({ i, reasonLog: entry.reason, reasonReplay: s.winReason });
    const rec = res.record || {};
    if (a.kind === 'move') {
      b.moves++;
      if (rec.kind === 'displace' || a.displace) b.displaces++;
      if (rec.capture) { b.captures++; if (rec.capture.type === 'line') { b.lineKills++; flags.lineDeaths.push({ i, move: s.moveNumber, killer: f, victim: rec.capture.faction, winner: s.winner, reason: s.winReason }); } }
      // осцилляции
      if (a.displace) {
        // атакующий встал на клетку нуля; его from — rec.from
        if (rec.from) {
          const lf = lastFrom[a.pieceId];
          if (lf && lf.q === rec.from.q && lf.r === rec.from.r) b.osc1++;
          const h = posHistory[a.pieceId] || [];
          if (h.length >= 2 && h[h.length - 2].q === rec.from.q && h[h.length - 2].r === rec.from.r) b.osc2++;
          lastFrom[a.pieceId] = rec.from;
          h.push({ q: rec.from.q, r: rec.from.r }); posHistory[a.pieceId] = h;
        }
      } else if (piece) {
        const lf = lastFrom[a.pieceId];
        if (lf && lf.q === a.q && lf.r === a.r) b.osc1++;
        const h = posHistory[a.pieceId] || [];
        if (h.length >= 2 && h[h.length - 2].q === a.q && h[h.length - 2].r === a.r) b.osc2++;
        lastFrom[a.pieceId] = { q: piece.q, r: piece.r };
        h.push({ q: piece.q, r: piece.r }); posHistory[a.pieceId] = h;
      }
      if ((a.q === 0 && a.r === 0) || E.GATE_SET.has(a.q + ',' + a.r)) b.gateMoves++;
    } else if (a.kind === 'support') b.supports++;
    else b.skips++;
    // повтор одной фигуры (в рамках ходов этой стороны)
    if (a.kind === 'move') {
      if (lastMovePiece[f] === a.pieceId) b.repeatPiece++;
      lastMovePiece[f] = a.pieceId;
    }
    // центр и hold
    const cp = E.occupantAt(s);
    if (cp && cp.faction === f) b.centerTicks++;
    for (const x of ['plus', 'minus', 'zero']) {
      if (s.hold[x] > (prevHold[x] || 0)) b.holdTicks++;
      if (s.hold[x] > b.maxHold) b.maxHold = s.hold[x];
    }
    // целостность / залипшие щиты
    const ie = integrity(s);
    if (ie.length) flags.integrity.push({ i, move: s.moveNumber, errors: ie });
    const ss = staleSupports(s);
    if (ss.length) {
      for (const x of ss) {
        const dup = flags.staleSupports.some(y => y.supporter === x.supporter && Math.abs(y.move - s.moveNumber) <= 1);
        if (!dup) flags.staleSupports.push({ i, move: s.moveNumber, ...x, center: cp ? cp.id : null });
      }
    }
  }
  const replayOk = (m.winner || 'draw') === (s.winner || 'draw') && (m.reason || 'draw') === (s.winReason || 'draw') && (m.moves === s.moveNumber);
  report.push({
    matchNo: m.matchNo,
    result: { winner: m.winner || 'draw', reason: m.reason || 'draw', moves: m.moves },
    replay: { winner: s.winner || 'draw', reason: s.winReason || 'draw', moves: s.moveNumber, ok: replayOk },
    llm, ai, llmTurns,
    flags: {
      fallbacks: flags.fallbacks,
      errors: flags.errors,
      illegal: flags.illegal,
      outsideDisplayed: flags.outsideDisplayed,
      staleSupports: flags.staleSupports,
      integrity: flags.integrity,
      missedLineDefense: flags.missedLineDefense,
      noDefenseAvailable: flags.noDefenseAvailable,
      centerOpportunities: flags.centerOpportunities,
      supportTargets: flags.supportTargets,
      lineDeaths: flags.lineDeaths,
    },
  });
}
require('fs').writeFileSync(OUT + 'elio_run_audit.json', JSON.stringify(report, null, 2));
for (const r of report) {
  console.log('=== M' + r.matchNo + ' ===', r.result.winner, r.result.reason, r.result.moves, 'replayOk=' + r.replay.ok);
  console.log('LLM', JSON.stringify(r.llm));
  console.log('flags', JSON.stringify({
    errors: r.flags.errors.length,
    illegal: r.flags.illegal.length,
    outsideDisplayed: r.flags.outsideDisplayed.length,
    stale: r.flags.staleSupports.length,
    integrity: r.flags.integrity.length,
    missedLineDefense: r.flags.missedLineDefense.length,
    noDefenseAvailable: r.flags.noDefenseAvailable.length,
    centerOpportunities: r.flags.centerOpportunities.length,
    lineDeaths: r.flags.lineDeaths.length,
    supportTargets: r.flags.supportTargets.length,
  }));
}
