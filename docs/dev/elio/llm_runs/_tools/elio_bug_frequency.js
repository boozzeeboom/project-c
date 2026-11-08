// Насколько часто AI/движок уходят за доску и используют оттеснение.
const path = require('path');
global.window = global;
require(path.join(__dirname, '..', '..', 'elio_engine.js'));
require(path.join(__dirname, '..', '..', 'elio_ai.js'));
const E = window.ElioEngine;
const AI = window.ElioAI;

function integrity(s) {
  const errs = [];
  const seen = new Set();
  for (const [k, c] of s.cells) {
    if (!c.piece) continue;
    const p = s.pieces.find(x => x.id === c.piece);
    if (!p) { errs.push('missing piece ' + c.piece); continue; }
    if (!p.alive) errs.push('dead on cell ' + p.id);
    if (p.q !== c.q || p.r !== c.r) errs.push('cell mismatch ' + p.id);
    if (seen.has(p.id)) errs.push('dup cell ' + p.id);
    seen.add(p.id);
  }
  for (const p of s.pieces.filter(x => x.alive)) {
    if (!E.isValidCell(s, p.q, p.r)) errs.push('offboard ' + p.id + ' ' + p.q + ',' + p.r);
    const c = s.cells.get(p.q + ',' + p.r);
    if (!c || c.piece !== p.id) errs.push('unlinked ' + p.id);
  }
  return errs;
}

function run(mode, death, n) {
  const st = { mode, death, games: n, moves: 0, displace: 0, gamesWithDisplace: 0, offboardMoves: 0, gamesWithOffboard: 0, integrityGames: 0, reasons: {} };
  for (let i = 0; i < n; i++) {
    let s = E.init({ turn: mode, death, pawns: 0 });
    let safety = 0;
    let hadDisp = false, hadOff = false, hadInt = false;
    while (!s.gameOver && safety < 160) {
      safety++;
      const f = s.turnFaction;
      let mv = f === 'zero' ? AI.chooseMove(s, f) : AI.chooseMoveAggressive(s, f);
      let r;
      const act = mv && (mv.act || {});
      if (!mv || mv.type === 'skip' || !act.type && !act.kind) r = E.skipTurn(s);
      else if (mv.type === 'support') r = E.applySupport(s, act.zeroId, act.targetId);
      else r = E.applyMove(s, act.pieceId, act.q, act.r);
      if (r.error) { st.reasons['error:' + r.error] = (st.reasons['error:' + r.error] || 0) + 1; break; }
      if (act.displace || (r.record && r.record.kind === 'displace')) { st.displace++; hadDisp = true; }
      const p = act.type === 'move' ? r.state.pieces.find(x => x.id === act.pieceId) : null;
      if (p && !E.isValidCell(r.state, p.q, p.r)) { st.offboardMoves++; hadOff = true; }
      const ie = integrity(r.state);
      if (ie.length) hadInt = true;
      s = r.state;
      st.moves++;
    }
    st.gamesWithDisplace += hadDisp ? 1 : 0;
    st.gamesWithOffboard += hadOff ? 1 : 0;
    st.integrityGames += hadInt ? 1 : 0;
    const reason = s.winReason || 'none';
    st.reasons[reason] = (st.reasons[reason] || 0) + 1;
  }
  return st;
}
console.log(JSON.stringify({ main: run('arrow', 'main', 40), long: run('arrow', 'long', 40) }, null, 2));
