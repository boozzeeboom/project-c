// Точечные проверки правил/движка Эльо. Не в git.
const path = require('path');
global.window = global;
require(path.join(__dirname, '..', '..', 'elio_engine.js'));
const E = window.ElioEngine;
const out = [];
function rec(name, expected, actual, ok, detail) {
  out.push({ name, expected, actual, ok: !!ok, detail: detail || '' });
}
function clearBoard(s) {
  for (const p of s.pieces) {
    p.alive = false;
    p.inert = false;
    p.supportTarget = null;
    const c = s.cells.get(p.q + ',' + p.r);
    if (c) c.piece = null;
  }
}
function place(s, p, q, r) {
  const old = s.cells.get(p.q + ',' + p.r);
  if (old && old.piece === p.id) old.piece = null;
  p.q = q; p.r = r; p.alive = true; p.inert = false; p.supportTarget = null;
  const c = s.cells.get(q + ',' + r);
  if (!c) throw new Error('bad cell ' + q + ',' + r);
  c.piece = p.id;
}
function find(s, type, faction) {
  return s.pieces.find(p => p.type === type && p.faction === faction);
}
function dist(a, b) { return E.dist([a.q, a.r], [b.q, b.r]); }

// 1. Нуль может оттеснить нейтрального нуля (правила §7: только военная фигура).
(function () {
  const s = E.init({ turn: 'plus', death: 'main', pawns: 0 });
  clearBoard(s);
  const z1 = find(s, 'free', 'zero');
  const z2 = find(s, 'courier', 'zero');
  place(s, z1, 1, 1);   // нейтральный нуль (поле Плюсов A12)
  place(s, z2, 0, 1);   // нейтральный нуль (спорный MZ-1)
  s.turnFaction = 'zero';
  const moves = E.legalMoves(s, z2.id);
  const displace = moves.find(m => m.displace && m.q === z1.q && m.r === z1.r);
  rec('zero_displace_generated',
    'нуль НЕ должен иметь displace-ход',
    displace ? 'displace есть: ' + JSON.stringify(displace) : 'displace нет',
    !displace);
  if (displace) {
    const r = E.applyMove(s, z2.id, z1.q, z1.r);
    rec('zero_displace_applied',
      'ошибка (нули не оттесняют)',
      r && r.error ? r.error : 'успешно, нуль сдвинут',
      !!(r && r.error));
  }
})();

// 2. Оттеснение: push на клетку с q=0 или r=0 должен быть разрешён (§7: только не «Ц»/ворота/спорный).
(function () {
  const s = E.init({ turn: 'plus', death: 'main', pawns: 0 });
  clearBoard(s);
  const bb = find(s, 'battleship', 'plus');
  const z = find(s, 'free', 'zero');
  place(s, bb, 0, -1); // жёлтые ворота Плюсов
  place(s, z, 0, -2);  // нейтральный нуль; push должен быть на (0,-3) — q=0
  s.turnFaction = 'plus';
  const moves = E.legalMoves(s, bb.id);
  const displace = moves.find(m => m.displace && m.q === 0 && m.r === -2);
  rec('displace_axis_generated',
    'push на (0,-3) разрешён (это поле, не «Ц»/ворота/спорный)',
    displace ? 'displace есть' : 'displace отсутствует (q=0 отфильтрован)',
    !!displace);
  const r = E.applyDisplace(s, bb.id, 0, -2);
  rec('displace_axis_apply',
    'applyDisplace разрешает push на (0,-3)',
    r && !r.error ? 'успешно, pushTo=' + JSON.stringify(r.record.pushTo) : 'ошибка: ' + (r && r.error),
    !!(r && !r.error));
})();

// 3. applyDisplace без соседства — не должен срабатывать.
(function () {
  const s = E.init({ turn: 'plus', death: 'main', pawns: 0 });
  clearBoard(s);
  const bb = find(s, 'battleship', 'plus');
  const z = find(s, 'free', 'zero');
  place(s, bb, 5, -1);
  place(s, z, 3, 0);
  s.turnFaction = 'plus';
  const d = dist(bb, z);
  const r = E.applyDisplace(s, bb.id, z.q, z.r);
  rec('displace_nonadjacent',
    'ошибка: фигура не соседняя с нулём (dist=' + d + ')',
    r && r.error ? 'ошибка: ' + r.error : 'успешно, нуль перемещён в ' + z.q + ',' + z.r,
    !!(r && r.error));
})();

// 4. Щит должен сниматься, когда цель ушла дальше 1 клетки (§8).
(function () {
  const s = E.init({ turn: 'arrow', death: 'main', pawns: 0 });
  s.turnFaction = 'zero';
  const zSup = find(s, 'courier', 'zero');   // (3,2)
  const zTgt = find(s, 'free', 'zero');      // (2,2) — рядом
  const sup = E.applySupport(s, zSup.id, zTgt.id);
  if (sup.error) { rec('support_stale_move', 'setup', 'support error ' + sup.error, false); return; }
  let st = sup.state;
  st.turnFaction = 'zero';
  const moves = E.legalMoves(st, zTgt.id).filter(m => {
    const dq = m.q - zSup.q, dr = m.r - zSup.r;
    return (Math.abs(dq) + Math.abs(dr) + Math.abs(dq + dr)) / 2 > 1;
  });
  if (!moves.length) { rec('support_stale_move', 'setup', 'нет хода дальше 1', false); return; }
  const mv = moves[0];
  const r = E.applyMove(st, zTgt.id, mv.q, mv.r);
  const supPiece = r.state.pieces.find(p => p.id === zSup.id);
  const tgtPiece = r.state.pieces.find(p => p.id === zTgt.id);
  const d = E.dist([supPiece.q, supPiece.r], [tgtPiece.q, tgtPiece.r]);
  rec('support_stale_move',
    'supportTarget=null, т.к. дистанция ' + d + ' > 1',
    supPiece.supportTarget === null ? 'null' : 'остался ' + supPiece.supportTarget,
    supPiece.supportTarget === null,
    'дистанция после хода: ' + d);
})();

// 5. Щит должен сниматься при оттеснении поддерживаемого нуля (если бы оттеснение было легально).
(function () {
  const s = E.init({ turn: 'arrow', death: 'main', pawns: 0 });
  s.turnFaction = 'zero';
  clearBoard(s);
  const bb = find(s, 'battleship', 'plus');
  const zSup = find(s, 'courier', 'zero');
  const zTgt = find(s, 'free', 'zero');
  place(s, bb, 0, -1);
  place(s, zTgt, 0, -2);
  place(s, zSup, 1, -2);
  s.turnFaction = 'zero';
  const sup = E.applySupport(s, zSup.id, zTgt.id);
  if (sup.error) { rec('support_stale_displace', 'setup', 'support error ' + sup.error, false); return; }
  let st = sup.state;
  st.turnFaction = 'plus';
  const r = E.applyDisplace(st, bb.id, zTgt.q, zTgt.r);
  if (r.error) { rec('support_stale_displace', 'setup', 'displace error ' + r.error, false); return; }
  const supPiece = r.state.pieces.find(p => p.id === zSup.id);
  const tgtPiece = r.state.pieces.find(p => p.id === zTgt.id);
  const d = E.dist([supPiece.q, supPiece.r], [tgtPiece.q, tgtPiece.r]);
  rec('support_stale_displace',
    'supportTarget=null, т.к. дистанция ' + d + ' > 1',
    supPiece.supportTarget === null ? 'null' : 'остался ' + supPiece.supportTarget,
    supPiece.supportTarget === null,
    'дистанция после оттеснения: ' + d);
})();

// 6. Приоритет побед: гибель Линейного (Основной) vs 3-й тик удержания в том же круге.
(function () {
  const s = E.init({ turn: 'minus', death: 'main', pawns: 0 });
  clearBoard(s);
  const plusLine = find(s, 'line', 'plus');
  const minusLine = find(s, 'line', 'minus');
  const plusCenter = find(s, 'destroyer', 'plus');
  place(s, plusLine, 0, 1);
  place(s, minusLine, 0, 2);
  place(s, plusCenter, 0, 0);
  s.turnFaction = 'minus';
  s.actedBy = { plus: true, minus: false, zero: true };
  s.moveNumber = 10;
  s.limit = 90;
  s.hold = { plus: 2, minus: 0, zero: 0 };
  s.lastHolder = 'plus';
  s.gameOver = false; s.winner = null; s.winReason = null;
  s.eliminated = { plus: false, minus: false };
  const moves = E.legalMoves(s, minusLine.id);
  const kill = moves.find(m => m.q === plusLine.q && m.r === plusLine.r);
  rec('king_vs_hold_legal', 'ход на Линейного легален', kill ? 'легален' : 'нелегален', !!kill);
  if (!kill) return;
  const r = E.applyMove(s, minusLine.id, plusLine.q, plusLine.r);
  rec('king_vs_hold_winner',
    'Минусы / king (немедленная победа убийцы)',
    (r.state.winner || 'null') + ' / ' + (r.state.winReason || 'null'),
    r.state.winner === 'minus' && r.state.winReason === 'king',
    'hold после тика: ' + JSON.stringify(r.state.hold));
})();

// 7. Инженер Нулей даёт соседнему нулю защиту 1? (правила §9 говорят «своя защита = 0, если не щитит другой нуль»)
(function () {
  const s = E.init({ turn: 'zero', death: 'main', pawns: 0 });
  clearBoard(s);
  const eng = find(s, 'engineer', 'zero');
  const z = find(s, 'saboteur', 'zero');
  place(s, eng, 4, 1);
  place(s, z, 3, 1);
  const lv = E.protLevel(s, z);
  rec('zero_engineer_aura', '1 (принято: аура Инженера действует и на Нулей; §9 обновлён)',
    String(lv), lv === 1, 'щит 1 от своего Инженера; щит 2 — только поддержкой другого нуля');
})();

// 8. Лимит 90 против 120.
(function () {
  const a = E.init({ pawns: 0 }).limit;
  const b = E.init({ pawns: 1 }).limit;
  const c = E.init({ pawns: 5 }).limit;
  rec('limit_10',
    '10 фигур: 90',
    String(a), a === 90);
  rec('limit_10plus1',
    '10+1: 90 (в rules §12.3; в тесте арены 60)',
    String(b), b === 90);
  rec('limit_15',
    '15 фигур: 120 (тест; §12.3 исправлен)',
    String(c), c === 120, '10/10+1 — 90, 15 — 120');
})();

// 9. Скиталец не поддерживает.
(function () {
  const s = E.init({ turn: 'arrow', death: 'main', pawns: 1 });
  s.turnFaction = 'zero';
  const sk = s.pieces.find(p => p.type === 'skiff');
  const targets = E.legalSupportTargets(s, sk.id);
  const r = E.applySupport(s, sk.id, null);
  rec('skiff_no_support', '0 целей и ошибка', targets.length + ' целей / ' + (r.error || 'без ошибки'),
    targets.length === 0 && !!r.error);
})();

// 10. Вход на «Ц» только с ворот.
(function () {
  const s = E.init({ turn: 'plus', death: 'main', pawns: 0 });
  clearBoard(s);
  const line = find(s, 'line', 'plus');
  place(s, line, 0, -1); // ворота
  let moves = E.legalMoves(s, line.id);
  const fromGate = moves.some(m => m.q === 0 && m.r === 0);
  place(s, line, 1, -1); // спорный PZ-1, соседний с «Ц», не ворота
  moves = E.legalMoves(s, line.id);
  const fromDisputed = moves.some(m => m.q === 0 && m.r === 0);
  rec('center_from_gate', 'вход с ворот разрешён', fromGate ? 'разрешён' : 'запрещён', fromGate);
  rec('center_from_disputed', 'вход со спорного запрещён', fromDisputed ? 'разрешён' : 'запрещён', !fromDisputed);
})();

// 11. Нуль на «Ц» уязвим для обеих военных.
(function () {
  const s = E.init({ turn: 'plus', death: 'main', pawns: 0 });
  clearBoard(s);
  const z = find(s, 'free', 'zero');
  const plusLine = find(s, 'line', 'plus');
  const minusLine = find(s, 'line', 'minus');
  place(s, z, 0, 0);
  place(s, plusLine, 0, -1);
  place(s, minusLine, 0, 1);
  const v = E.zeroVulnerability(s, z);
  rec('center_zero_vulnerable', "'both'", String(v), v === 'both');
})();

// 12. applyMove-оттеснение: атакующий остаётся на своей клетке, нуль сдвигается.
(function () {
  const s = E.init({ turn: 'plus', death: 'main', pawns: 0 });
  clearBoard(s);
  const bb = find(s, 'battleship', 'plus');
  const z = find(s, 'free', 'zero');
  place(s, bb, 1, -1);   // спорный PZ-1
  place(s, z, 1, -2);    // нейтральный нуль; push на (1,-3)
  s.turnFaction = 'plus';
  const moves = E.legalMoves(s, bb.id);
  const disp = moves.find(m => m.displace && m.q === 1 && m.r === -2);
  rec('applyMove_displace_generated', 'displace-ход есть', disp ? 'есть' : 'нет', !!disp);
  if (!disp) return;
  const r = E.applyMove(s, bb.id, 1, -2);
  if (r.error) { rec('applyMove_displace_integrity', 'успех', 'ошибка ' + r.error, false); return; }
  const st = r.state;
  const bbP = st.pieces.find(p => p.id === bb.id);
  const zP = st.pieces.find(p => p.id === z.id);
  const oldBb = st.cells.get('1,-1');
  const zeroOld = st.cells.get('1,-2');
  const zeroNew = st.cells.get('1,-3');
  const issues = [];
  if (bbP.q !== 1 || bbP.r !== -1) issues.push('атакующий должен остаться на (1,-1), а он в ' + bbP.q + ',' + bbP.r);
  if (!oldBb || oldBb.piece !== bb.id) issues.push('клетка атакующего (1,-1) не содержит его: ' + (oldBb ? oldBb.piece : 'null'));
  if (!zeroOld || zeroOld.piece !== null) issues.push('клетка нуля (1,-2) всё ещё содержит: ' + (zeroOld ? zeroOld.piece : 'null'));
  if (!zeroNew || zeroNew.piece !== zP.id) issues.push('нуль (1,-3) не привязан');
  rec('applyMove_displace_integrity', 'атакующий на месте, клетки согласованы', issues.length ? issues.join('; ') : 'ок', issues.length === 0);
})();

// 13. Одношаговые фигуры не должны уходить за доску.
(function () {
  const s = E.init({ turn: 'plus', death: 'main', pawns: 0 });
  clearBoard(s);
  const line = find(s, 'line', 'plus');
  place(s, line, -1, -4); // валидная клетка поля Плюсов (дистанция 2)
  s.turnFaction = 'plus';
  const moves = E.legalMoves(s, line.id);
  const off = moves.find(m => !E.isValidCell(s, m.q, m.r));
  rec('offboard_move_generated',
    'все ходы только на валидные клетки',
    off ? 'есть ход за доску: ' + JSON.stringify(off) : 'все валидны',
    !off);
  if (off) {
    const r = E.applyMove(s, line.id, off.q, off.r);
    const p = r.state.pieces.find(x => x.id === line.id);
    rec('offboard_move_applied',
      'ошибка/недопустимый ход',
      r.error ? 'ошибка ' + r.error : 'успешно, Линейный в ' + p.q + ',' + p.r + ' (валидна=' + E.isValidCell(r.state, p.q, p.r) + ')',
      !!(r.error || E.isValidCell(r.state, p.q, p.r)));
  }
})();

// 14. Recorder/replay: applyMove-запись displace хранит from атакующего, а applyDisplace ждёт from нуля.
(function () {
  const s = E.init({ turn: 'plus', death: 'main', pawns: 0 });
  clearBoard(s);
  const bb = find(s, 'battleship', 'plus');
  const z = find(s, 'free', 'zero');
  place(s, bb, 1, -1);
  place(s, z, 1, -2);
  s.turnFaction = 'plus';
  const r = E.applyMove(s, bb.id, 1, -2);
  const recMove = r.record;
  rec('recorder_displace_record', 'kind=displace', recMove ? recMove.kind : 'нет записи', !!(recMove && recMove.kind === 'displace'));
  if (!recMove) return;
  const replay = E.applyDisplace(E.clone(s), recMove.pieceId, recMove.from.q, recMove.from.r);
  rec('recorder_displace_replay',
    'replay должен применять оттеснение без ошибки',
    replay.error ? 'ошибка: ' + replay.error : 'успешно',
    !replay.error,
    'applyMove.from=' + JSON.stringify(recMove.from) + ', to=' + JSON.stringify(recMove.to));
})();

console.log(JSON.stringify(out, null, 2));
