// Проверка results.json headless-прогона: правила, причины побед, seed-континуитет.
const fs = require('fs');
const path = require('path');

const p = process.argv[2];
if (!p) { console.error('usage: node validate_games.js path/to/results.json [path/to/100x18/results.json]'); process.exit(1); }
const r = JSON.parse(fs.readFileSync(p, 'utf8'));
const refPath = process.argv[3] || path.join(__dirname, '..', '2026-11-08_headless100_v13d', 'results.json');
const ref = fs.existsSync(refPath) ? JSON.parse(fs.readFileSync(refPath, 'utf8')) : null;

const issues = [];
const counts = { games: 0, contaminated: 0, stuck: 0, errors: 0, reasons: {}, winners: {}, lineDeaths: {}, holdMax: {} };
for (const g of r.games_detail) {
  counts.games++;
  if (g.contaminated) counts.contaminated++;
  if (g.stuck) counts.stuck++;
  if (g.errors && g.errors.length) counts.errors++;
  counts.reasons[g.reason] = (counts.reasons[g.reason] || 0) + 1;
  counts.winners[g.winner] = (counts.winners[g.winner] || 0) + 1;
  counts.lineDeaths[g.lineDeaths] = (counts.lineDeaths[g.lineDeaths] || 0) + 1;
  for (const x of ['plus', 'minus', 'zero']) counts.holdMax[g.holdMax[x]] = (counts.holdMax[g.holdMax[x]] || 0) + 1;

  const tag = g.turn + '+' + g.death + '+p' + g.pawns + ' #' + g.gameIndex;
  if (!['plus', 'minus', 'zero', 'draw'].includes(g.winner)) issues.push(tag + ': bad winner ' + g.winner);
  if (!['hold', 'king', 'limit', 'draw', 'stuck', 'zeros_last'].includes(g.reason)) issues.push(tag + ': bad reason ' + g.reason);
  if (g.moves < 0 || g.moves > g.limit) issues.push(tag + ': moves ' + g.moves + ' out of limit ' + g.limit);
  if (g.lineDeaths < 0 || g.lineDeaths > 1) issues.push(tag + ': lineDeaths ' + g.lineDeaths);
  for (const x of ['plus', 'minus', 'zero']) {
    if (g.holdMax[x] < 0 || g.holdMax[x] > 3) issues.push(tag + ': holdMax.' + x + '=' + g.holdMax[x]);
  }
  if (g.lineDeaths > 0 && !g.centerEntered && g.death === 'main') {
    // не обязательно: гибель Линейного может быть без центра, это нормально
  }
  if (g.death === 'main') {
    if (g.reason === 'king') {
      if (g.lineDeaths !== 1) issues.push(tag + ': king without exactly 1 line death');
      if (!['plus', 'minus'].includes(g.winner)) issues.push(tag + ': king winner ' + g.winner);
      if ((g.holdMax[g.winner] || 0) >= 3) issues.push(tag + ': king but winner holdMax=3');
    } else if (g.reason === 'hold') {
      if ((g.holdMax[g.winner] || 0) !== 3) issues.push(tag + ': hold winner holdMax=' + (g.holdMax[g.winner] || 0));
      if (g.lineDeaths !== 0) issues.push(tag + ': hold with lineDeaths=' + g.lineDeaths);
    } else if (g.reason === 'limit' || g.reason === 'draw') {
      if (g.moves !== g.limit) issues.push(tag + ': ' + g.reason + ' at move ' + g.moves + ' limit ' + g.limit);
      if (g.reason === 'limit' && g.winner === 'draw') issues.push(tag + ': limit draw winner');
      if (g.reason === 'draw' && g.winner !== 'draw') issues.push(tag + ': draw reason winner ' + g.winner);
      if (g.lineDeaths !== 0) issues.push(tag + ': ' + g.reason + ' with lineDeaths=' + g.lineDeaths);
    }
  } else {
    if (g.reason === 'king') issues.push(tag + ': king in long mode');
    if (g.reason === 'hold' && (g.holdMax[g.winner] || 0) !== 3) issues.push(tag + ': long hold winner holdMax=' + (g.holdMax[g.winner] || 0));
    if ((g.reason === 'limit' || g.reason === 'draw') && g.moves !== g.limit) issues.push(tag + ': long ' + g.reason + ' at move ' + g.moves + ' limit ' + g.limit);
    if (g.reason === 'draw' && g.winner !== 'draw') issues.push(tag + ': long draw reason winner ' + g.winner);
  }
  if (g.reason === 'zeros_last') issues.push(tag + ': zeros_last (unreachable)');
  if (g.eliminated) issues.push(tag + ': eliminated both military (zeros_last path)');
  if (g.centerEntered === false && Math.max(g.holdMax.plus, g.holdMax.minus, g.holdMax.zero) > 0) issues.push(tag + ': hold>0 but center never entered');
}

// Seed continuity: first 100 games of the 400 run must match the 100×18 rows.
let continuity = { checked: 0, mismatches: 0, details: [] };
if (ref) {
  const refMap = new Map();
  for (const g of ref.games_detail) {
    if (g.gameIndex < 100) refMap.set(g.variantIndex + ':' + g.gameIndex, g);
  }
  for (const g of r.games_detail) {
    if (g.gameIndex >= 100) continue;
    if (r.passToFix && g.turn === 'pass') continue; // pass-фикс меняет seed-поток; старые pass-строки не сравниваем
    const key = g.variantIndex + ':' + g.gameIndex;
    const rg = refMap.get(key);
    if (!rg) continue;
    continuity.checked++;
    const fields = ['winner', 'reason', 'moves', 'lineDeaths', 'centerEntered'];
    const diff = fields.filter(f => JSON.stringify(g[f]) !== JSON.stringify(rg[f]));
    if (diff.length) {
      continuity.mismatches++;
      if (continuity.details.length < 5) continuity.details.push({ key, diff, g: { winner: g.winner, reason: g.reason, moves: g.moves }, rg: { winner: rg.winner, reason: rg.reason, moves: rg.moves } });
    }
  }
}

const report = { file: p, ref: ref ? refPath : null, counts, continuity, issues: issues.slice(0, 100), issueCount: issues.length };
console.log(JSON.stringify(report, null, 2));
process.exit(issues.length || continuity.mismatches ? 1 : 0);
