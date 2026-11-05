// Проверка T-CAM25: поведение камеры на высокой скорости (админ-бег/noclip).
// Сравниваем старую логику (задержка 0.07 + лаг 0.15 + сглаживание 0.04 + снап по
// дистанции лага 10 м) и новую (потолок отставания 2.5 м, задержка гасится к 25 м/с,
// сглаживание ужимается до 25%, снап — только по кадровому шагу цели).
const DT = 1 / 60;
const DIST = 5, HEIGHT = 2, LOOK_H = 1.5;
const ease = tau => 1 - Math.exp(-DT / tau);
const V = (x, y, z) => ({ x, y, z });
const add = (a, b) => V(a.x + b.x, a.y + b.y, a.z + b.z);
const sub = (a, b) => V(a.x - b.x, a.y - b.y, a.z - b.z);
const len = a => Math.hypot(a.x, a.y, a.z);
const lerp = (a, b, t) => V(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
const clamp01 = v => Math.max(0, Math.min(1, v));

function run(speed, mode) {
  // mode: 'old' | 'new'
  let cam = V(0, 0, 0), lag = V(0, 0, 0);
  let samples = [];               // (time, pos) — интерполяция цели
  let prevRaw = null, prevCam = null;
  let speedSmoothed = 0;

  let maxGap = 0, stepMin = Infinity, stepMax = -Infinity, snaps = 0;

  const frames = Math.round(6 / DT);
  for (let f = 0; f < frames; f++) {
    const t = f * DT;
    const target = V(speed * t, 0, 0);
    const now = t;

    // --- задержка интерполяции цели ---
    let delay;
    if (mode === 'old') delay = 0.07;
    else {
      const stepSpeed = prevRaw ? len(sub(target, prevRaw)) / DT : 0;
      speedSmoothed = speedSmoothed + (Math.min(stepSpeed, 200) - speedSmoothed) * ease(0.15);
      delay = 0.07 * (1 - clamp01((speedSmoothed - 10) / (25 - 10)));
    }
    prevRaw = target;

    if (delay > 0.0001) {
      samples.push({ t: now, p: target });
      while (samples.length > 2 && samples[0].t < now - Math.max(delay * 2, 0.2)) samples.shift();
      const at = now - delay;
      let sampled = samples[samples.length - 1].p;
      for (let i = samples.length - 1; i >= 0; i--) {
        if (samples[i].t <= at) {
          if (i === samples.length - 1) { sampled = samples[i].p; break; }
          const a = samples[i], b = samples[i + 1];
          const u = clamp01((at - a.t) / Math.max(b.t - a.t, 1e-4));
          sampled = lerp(a.p, b.p, u);
          break;
        }
      }
      var sampledPos = sampled;
    } else {
      samples.length = 0;
      var sampledPos = target;
    }

    // --- лаг ---
    const d = sub(sampledPos, lag);
    if (mode === 'old') {
      // старый клэмп: >10 м — лаг-цель телепортируется на 10 м позади
      if (len(d) > 10) lag = sub(sampledPos, V(d.x / len(d) * 10, d.y / len(d) * 10, d.z / len(d) * 10));
      else lag = V(lag.x + d.x * ease(0.15), lag.y + d.y * ease(0.05), lag.z + d.z * ease(0.15));
    } else {
      lag = V(lag.x + d.x * ease(0.15), lag.y + d.y * ease(0.05), lag.z + d.z * ease(0.15));
      const gapv = sub(sampledPos, lag);
      if (len(gapv) > 2.5) lag = sub(sampledPos, V(gapv.x / len(gapv) * 2.5, gapv.y / len(gapv) * 2.5, gapv.z / len(gapv) * 2.5));
    }

    // --- снап ---
    const frameStep = Math.hypot(target.x - (mode === 'old' ? (prevRaw.x) : prevRaw.x), 0); // не нужен: считаем отдельно
    if (mode === 'old') {
      // старая проверка: |lagTarget - target| > 10
      if (len(sub(lag, target)) > 10) { lag = target; if (prevCam) snaps++; }
    } else {
      // новая: кадровый шаг цели > 10 м (телепорт)
      const step = prevRaw ? len(sub(target, prevRaw)) : 0;
      if (step > 10) { lag = target; if (prevCam) snaps++; }
    }

    // --- desired + сглаживание ---
    const pitch = 15 * Math.PI / 180;
    const desired = add(add(lag, V(0, Math.sin(pitch) * DIST, -Math.cos(pitch) * DIST)), V(0, HEIGHT, 0));
    let smoothTime = 0.04;
    if (mode === 'new') smoothTime *= 1 + (0.25 - 1) * clamp01((speedSmoothed - 10) / (30 - 10));
    cam = lerp(cam, desired, ease(smoothTime));

    // --- метрики (после 1.5 с) ---
    if (t > 1.5 && prevCam) {
      const gap = len(sub(cam, target));
      if (gap > maxGap) maxGap = gap;
      const step = len(sub(cam, prevCam));
      if (step < stepMin) stepMin = step;
      if (step > stepMax) stepMax = step;
    }
    prevCam = cam;
  }
  return { maxGap, stepMin, stepMax, snaps };
}

for (const speed of [12, 25, 48, 120]) {
  const o = run(speed, 'old');
  const n = run(speed, 'new');
  console.log(`\n=== ${speed} м/с (${(speed * 3.6).toFixed(0)} км/ч) ===`);
  console.log(`  было: отставание до ${o.maxGap.toFixed(2)} м | шаг камеры ${(o.stepMin * 1000).toFixed(0)}..${(o.stepMax * 1000).toFixed(0)} мм (разброс ${((o.stepMax - o.stepMin) * 1000).toFixed(0)} мм) | снапов ${o.snaps}`);
  console.log(`  стало: отставание до ${n.maxGap.toFixed(2)} м | шаг камеры ${(n.stepMin * 1000).toFixed(0)}..${(n.stepMax * 1000).toFixed(0)} мм (разброс ${((n.stepMax - n.stepMin) * 1000).toFixed(0)} мм) | снапов ${n.snaps}`);
}
