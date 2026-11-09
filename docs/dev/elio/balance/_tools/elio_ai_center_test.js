// Тактические сценарии ИИ v1.4: гонка за «Ц», срыв удержания и защита накопленных тиков.
const assert = require('assert');
const path = require('path');
global.window = global;
require(path.join(__dirname, '..', '..', 'elio_engine.js'));
require(path.join(__dirname, '..', '..', 'elio_ai.js'));
const E = window.ElioEngine;
const AI = window.ElioAI;
Math.random = () => 0.5;

function scenario(specs, opts = {}) {
  const s = E.init({ turn: 'cycle', death: opts.death || 'long' });
  for (const c of s.cells.values()) c.piece = null;
  s.pieces = specs.map((sp, i) => ({
    id: sp.id || 'T-' + i,
    faction: sp.faction,
    type: sp.type,
    q: sp.q,
    r: sp.r,
    alive: true,
    inert: false,
    supportTarget: sp.supportTarget || null,
  }));
  for (const p of s.pieces) s.cells.get(p.q + ',' + p.r).piece = p.id;
  s.turnFaction = opts.turn || 'plus';
  s.hold = { plus: 0, minus: 0, zero: 0, ...(opts.hold || {}) };
  s.lastHolder = opts.lastHolder || null;
  return s;
}

function chosen(s, faction = 'plus') {
  return faction === 'zero' ? AI.chooseMove(s, faction) : AI.chooseMoveAggressive(s, faction);
}

const kings = [
  { faction: 'plus', type: 'line', q: 0, r: -5 },
  { faction: 'minus', type: 'line', q: -5, r: 5 },
];

{
  const s = scenario([...kings,
    { id: 'P-D', faction: 'plus', type: 'destroyer', q: 0, r: -1 },
    { id: 'M-D', faction: 'minus', type: 'destroyer', q: 0, r: 0 },
  ], { hold: { minus: 2 }, lastHolder: 'minus' });
  const mv = chosen(s);
  assert.equal(mv.act.q, 0);
  assert.equal(mv.act.r, 0);
  assert.equal(mv.act.capture, true, 'ИИ обязан снять держателя при hold=2');
}

{
  const s = scenario([...kings,
    { id: 'P-D', faction: 'plus', type: 'destroyer', q: 1, r: -1 },
    { id: 'M-D', faction: 'minus', type: 'destroyer', q: 0, r: 0 },
    { id: 'Z-P', faction: 'zero', type: 'parliament', q: 1, r: 0, supportTarget: 'M-D' },
  ], { hold: { minus: 2 }, lastHolder: 'minus' });
  const mv = chosen(s);
  const target = E.pieceAt(s, mv.act.q, mv.act.r);
  assert.equal(target && target.id, 'Z-P', 'Сначала снимается щит именно держателя «Ц»');
}

{
  const s = scenario([...kings,
    { id: 'P-D', faction: 'plus', type: 'destroyer', q: 1, r: -1 },
    { id: 'M-D', faction: 'minus', type: 'destroyer', q: 1, r: 0 },
  ]);
  const mv = chosen(s);
  assert.equal(mv.act.q, 1);
  assert.equal(mv.act.r, 0, 'ИИ не входит на «Ц» под немедленное взятие, а снимает соперника из ворот');
}

{
  const s = scenario([...kings,
    { id: 'P-D', faction: 'plus', type: 'destroyer', q: 0, r: -2 },
    { id: 'M-D', faction: 'minus', type: 'destroyer', q: 0, r: 0 },
    { id: 'M-X', faction: 'minus', type: 'destroyer', q: 2, r: -2 },
  ], { hold: { minus: 2 }, lastHolder: 'minus' });
  const mv = chosen(s);
  assert.equal(mv.act.q, 0);
  assert.equal(mv.act.r, -1, 'При невозможности снять держателя ИИ выдвигается в ворота');
}

{
  const s = scenario([
    { id: 'Z-P', faction: 'zero', type: 'parliament', q: 0, r: 0 },
    { id: 'Z-E', faction: 'zero', type: 'engineer', q: 0, r: -1 },
    ...kings,
  ], { turn: 'zero', hold: { zero: 2 }, lastHolder: 'zero' });
  const mv = chosen(s, 'zero');
  assert.equal(mv.type, 'support');
  assert.equal(mv.act.targetId, 'Z-P', 'Нули защищают держателя при hold=2');
}

{
  const s = scenario([
    { id: 'P-D', faction: 'plus', type: 'destroyer', q: 0, r: 0 },
    { id: 'P-L', faction: 'plus', type: 'line', q: 0, r: -5 },
    { id: 'M-L', faction: 'minus', type: 'line', q: -1, r: 1 },
  ], { hold: { plus: 1 }, lastHolder: 'plus' });
  const mv = chosen(s);
  assert.notEqual(mv.act.q, -1, 'В Длинном режиме накопленный «Ц» не бросается ради выбывания короля');
}

console.log('AI center tactics: 6/6 scenarios passed');
