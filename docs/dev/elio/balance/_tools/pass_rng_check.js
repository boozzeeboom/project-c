// Диагностика pass-режима: ИИ оценивает ход с одним случайным passTo,
// а фактический applyMove выбирает другой. Проверяем, совпадают ли цели.
const path = require('path');
global.window = global;
require(path.join(__dirname, '..', '..', 'elio_engine.js'));
require(path.join(__dirname, '..', '..', 'elio_ai.js'));
const E = window.ElioEngine;
const AI = window.ElioAI;

function mulberry32(a) {
  return function () {
    a |= 0; a = (a + 0x6D2B79F5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

let checked = 0, same = 0, diff = 0;
const examples = [];
for (let seed = 1; seed <= 40; seed++) {
  Math.random = mulberry32(seed * 7919);
  const s = E.init({ turn: 'pass', death: 'main', pawns: 1, passRule: 'random' });
  const f = s.turnFaction;
  const mv = AI.chooseMove(s, f);
  if (!mv || !mv.act) continue;
  const act = mv.act;
  let r;
  if (mv.type === 'support') r = E.applySupport(s, act.zeroId, act.targetId, mv.passTo);
  else r = E.applyMove(s, act.pieceId, act.q, act.r, mv.passTo);
  if (r.error) continue;
  checked++;
  const simTarget = mv.state && mv.state.turnFaction;
  const actualTarget = r.state.turnFaction;
  if (simTarget === actualTarget) same++;
  else {
    diff++;
    if (examples.length < 5) examples.push({ seed, faction: f, simTarget, actualTarget });
  }
}
console.log(JSON.stringify({ checked, same, diff, examples }, null, 2));
