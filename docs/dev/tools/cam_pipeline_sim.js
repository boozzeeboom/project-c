// Численная проверка конвейера SpringArmCamera (T-CAM22/23/24, T-PAROM-25/26).
// Мирная симуляция тех же формул, что в C#: lag(exp) -> desired -> SmoothPosition(exp 0.04)
// -> LookAt. Метрики: (1) разброс дистанции камера↔персонаж — это "камера не успевает";
// (2) разброс шага камеры за кадр — рваность; (3) разброс вертикального смещения.
//
// Запуск: node docs/dev/tools/cam_pipeline_sim.js

const DT = 1 / 60;                 // рендер 60 fps
const FIXED = 1 / 50;              // физика 50 Гц (beat 10 Гц)
const SPEED = 9.0;                 // м/с, паром
const LAG_XZ = 0.15, LAG_Y = 0.05, SMOOTH = 0.04, LOOK = 0.03;
const DIST = 5, HEIGHT = 2, LOOK_H = 1.5;

const v3 = (x, y, z) => ({ x, y, z });
const add = (a, b) => v3(a.x + b.x, a.y + b.y, a.z + b.z);
const sub = (a, b) => v3(a.x - b.x, a.y - b.y, a.z - b.z);
const len = (a) => Math.hypot(a.x, a.y, a.z);
const lerp = (a, b, t) => v3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
const ease = (tau) => 1 - Math.exp(-DT / Math.max(tau, 0.001));

// Провис троса: кабинка идёт по сагу, вертикаль до ~0.6 м/с
const deckY = (t) => 0.6 * Math.sin(t * 0.7) - 0.6 * Math.sin(0.35);
const platformPos = (t, s) => v3(s, deckY(t), 0);

// Локальная микродрожь персонажа по палубе (прижим CC): 5 мм 37 Гц + 3 мм пила.
function microJitter(t, frame) {
  const y = 0.005 * Math.sin(2 * Math.PI * 37 * t) + 0.0015 * ((frame % 3) - 1);
  const xz = 0.004 * Math.sin(2 * Math.PI * 23 * t);
  return v3(xz, y, xz * 0.5);
}

function run(label, { follow, stepped, interpDelay, physicsLag }) {
  let cam = v3(0, 0, 0), lag = v3(0, 0, 0);
  let lastPlatform = null, lastLocal = v3(0, 0, 0);
  let samples = [], netS = 0;
  let offMin = Infinity, offMax = -Infinity, dyMin = Infinity, dyMax = -Infinity;
  let stepMin = Infinity, stepMax = -Infinity, prevCam = null;

  const frames = Math.round(12 / DT);
  for (let f = 0; f < frames; f++) {
    const t = f * DT;

    let s;
    if (!stepped) {
      s = SPEED * t;                                   // хост/оффлайн: per-frame
    } else {
      if (f % 2 === 0) netS = SPEED * t;               // клиент: тики 30 Гц
      if (interpDelay > 0) {
        if (samples.length === 0 || samples[samples.length - 1].s !== netS) samples.push({ t, s: netS });
        while (samples.length > 2 && samples[0].t < t - Math.max(interpDelay * 2, 0.2)) samples.shift();
        const target = t - interpDelay;
        s = samples[samples.length - 1].s;
        for (let i = samples.length - 1; i >= 0; i--) {
          if (samples[i].t <= target) {
            if (i === samples.length - 1) { s = samples[i].s; break; }
            const a = samples[i], b = samples[i + 1];
            const u = (target - a.t) / Math.max(b.t - a.t, 1e-4);
            s = a.s + (b.s - a.s) * u;
            break;
          }
        }
      } else s = netS;
    }

    const platform = platformPos(t, s);
    const jitter = microJitter(t, f);

    // T-PAROM-26: коллайдер палубы у кинематики отстаёт на шаг физики (autoSyncTransforms off).
    // CC прижимает персонажа к НИЗУ последнего шага физики -> локальное вертикальное смещение.
    let deckLocalY = 0;
    if (physicsLag) deckLocalY = deckY(Math.floor(t / FIXED) * FIXED) - deckY(t);

    const target = v3(platform.x + jitter.x, platform.y + deckLocalY + jitter.y, platform.z + jitter.z);

    // T-CAM23: платформа двигает риг 1:1
    if (lastPlatform) {
      const d = sub(platform, lastPlatform);
      if (follow) { cam = add(cam, d); lag = add(lag, d); }
    }
    lastPlatform = platform;
    lastLocal = sub(target, platform);

    // lag (dynamicLag в префабе выключен -> фиксированные времена)
    const d = sub(target, lag);
    lag = v3(lag.x + d.x * ease(LAG_XZ), lag.y + d.y * ease(LAG_Y), lag.z + d.z * ease(LAG_XZ));

    const pitch = 15 * Math.PI / 180;
    const dir = v3(0, Math.sin(pitch), -Math.cos(pitch));
    const desired = add(add(lag, v3(0, dir.y * DIST, dir.z * DIST)), v3(0, HEIGHT, 0));
    if (len(sub(cam, desired)) >= 0.003) cam = lerp(cam, desired, ease(SMOOTH));

    if (t > 4 && prevCam) {
      const off = len(sub(cam, target));
      const dy = cam.y - target.y;
      if (off < offMin) offMin = off; if (off > offMax) offMax = off;
      if (dy < dyMin) dyMin = dy; if (dy > dyMax) dyMax = dy;
      const step = len(sub(cam, prevCam));
      if (step < stepMin) stepMin = step; if (step > stepMax) stepMax = step;
    }
    prevCam = cam;
  }

  const pc = (a, b) => ((b - a) * 1000).toFixed(1);
  console.log(
    `${label.padEnd(48)} dist=${(offMin * 100).toFixed(1)}..${(offMax * 100).toFixed(1)}см ` +
    `(разброс ${pc(offMin, offMax)} мм) | dY разброс ${pc(dyMin, dyMax)} мм | шаг ${pc(stepMin, stepMax)} мм`);
}

console.log('=== ХОСТ: платформа гладкая, палуба-физика СИНХРОНИЗИРОВАНА (T-PAROM-26 вкл) ===');
run('было: без follow', { follow: false, stepped: false, interpDelay: 0, physicsLag: false });
run('T-CAM23: follow', { follow: true, stepped: false, interpDelay: 0, physicsLag: false });

console.log('\n=== ХОСТ: палуба-физика отстаёт на шаг 20 мс (T-PAROM-26 выкл) ===');
run('без follow', { follow: false, stepped: false, interpDelay: 0, physicsLag: true });
run('T-CAM23: follow', { follow: true, stepped: false, interpDelay: 0, physicsLag: true });

console.log('\n=== КЛИЕНТ: платформа ступеньками 30 Гц, палуба-физика синхронизирована ===');
run('было: без follow, без интерполяции s', { follow: false, stepped: true, interpDelay: 0, physicsLag: false });
run('T-CAM23 follow, без интерполяции s', { follow: true, stepped: true, interpDelay: 0, physicsLag: false });
run('T-PAROM-25 + T-CAM23 (как в HEAD)', { follow: true, stepped: true, interpDelay: 0.07, physicsLag: false });
