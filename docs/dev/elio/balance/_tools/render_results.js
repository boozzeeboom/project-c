// Рендер summary из results.json в markdown-таблицу.
const fs = require('fs');
const p = process.argv[2];
if (!p) { console.error('usage: node render_results.js path/to/results.json'); process.exit(1); }
const r = JSON.parse(fs.readFileSync(p, 'utf8'));
const rows = r.summary.map(s => {
  const a = s.aggregate;
  const pct = n => (100 * n / a.games).toFixed(1);
  return '| ' + [s.turn + '+' + s.death + '+p' + s.pawns, a.wins.plus, a.wins.minus, a.wins.zero, a.wins.draw, pct(a.wins.plus), pct(a.wins.minus), pct(a.wins.zero), a.avgMoves, a.lineDeaths, pct(a.centerEntered), a.contaminated, a.stuck].join(' | ') + ' |';
});
console.log('| Вариант | P | M | Z | D | P% | M% | Z% | Avg moves | Line deaths | Center entered % | Cont | Stuck |');
console.log('|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|');
console.log(rows.join('\n'));
console.log('\nGames:', r.games, 'Elapsed:', r.elapsedSec, 's');
