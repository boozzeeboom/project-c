// Численная проверка конвейера SpringArmCamera (T-CAM22/T-CAM23/T-PAROM-25).
// Мирная симуляция тех же формул, что в C#: lag(exp) -> desired -> (нет коллизий) ->
// SmoothPosition(exp 0.04) -> LookAt. Метрика: насколько "плывёт" камера ОТНОСИТЕЛЬНО
// персонажа (это и есть "камера не успевает") и насколько рваный у неё шаг.

const DT = 1 / 60;
const SPEED = 9.0;              // м/с, паром
const LAG_XZ = 0.15, LAG_Y = 0.05, SMOOTH = 0.04, LOOK = 0.03;
const DIST = 5, HEIGHT = 2, LOOK_H = 1.5;

const v3 = (x, y, z) => ({ x, y, z });
const add = (a, b) => v3(a.x + b.x, a.y + b.y, a.z + b.z);
const sub = (a, b) => v3(a.x - b.x, a.y - b.y, a.z - b.z);
const len = (a) => Math.hypot(a.x, a.y, a.z);
const lerp = (a, b, t) => v3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
const ease = (tau) => 1 - Math.exp(-DT / Math.max(tau, 0.001));

// Позиция платформы: 9 м/с по X + провис по Y (0.6 м/с), как у кабинки на тросе.
function platformPos(t) {
  return v3(SPEED * t, 0.6 * Math.sin(t * 0.7) - 0.6 * Math.sin(0.35), 0);
}
// Локальная микродрожь персонажа относительно палубы (прижим CC, carry, физика):
// 5 мм синус 37 Гц + пила 1.5 мм/кадр (квантование физики)
function localOffset(t, frame) {
  const y = 0.005 * Math.sin(2 * Math.PI * 37 * t) + 0.0015 * ((frame % 3) - 1);
  const xz = 0.004 * Math.sin(2 * Math.PI * 23 * t);
  return v3(xz, y, xz * 0.5);
}

function run(label, { follow, stepped, interpDelay }) {
  // состояние рига
  let cam = v3(0, 0, 0), lag = v3(0, 0, 0), smoothLook = v3(0, 0, 0);
  let lastPlatform = null, lastLocal = v3(0, 0, 0);
  let samples = [];           // (time, s) для интерполяции "s" прогресса платформы (T-PAROM-25)
  let netS = 0;

  let offsetMin = Infinity, offsetMax = -Infinity;
  let stepMin = Infinity, stepMax = -Infinity;
  let prevCam = null;

  const frames = Math.round(12 / DT);
  for (let f = 0; f < frames; f++) {
    const t = f * DT;

    // --- источник: прогресс платформы ---
    let s;
    if (!stepped) {
      s = SPEED * t;                             // хост/оффлайн: интегрируется каждый кадр
    } else {
      // клиент: NetworkVariable обновляется тиками 30 Гц
      if (f % 2 === 0) netS = SPEED * t;
      if (interpDelay > 0) {
        if (samples.length === 0 || samples[samples.length - 1].s !== netS)
          samples.push({ t, s: netS });
        while (samples.length > 2 && samples[0].t < t - Math.max(interpDelay * 2, 0.2)) samples.shift();
        const target = t - interpDelay;
        // линейная интерполяция
        let val = samples[samples.length - 1].s;
        for (let i = samples.length - 1; i >= 0; i--) {
          if (samples[i].t <= target) {
            if (i === samples.length - 1) { val = samples[i].s; break; }
            const a = samples[i], b = samples[i + 1];
            const u = (target - a.t) / Math.max(b.t - a.t, 1e-4);
            val = a.s + (b.s - a.s) * u;
            break;
          }
        }
        s = val;
      } else {
        s = netS;
      }
    }

    const platform = v3(s, 0.6 * Math.sin(t * 0.7) - 0.6 * Math.sin(0.35), 0);
    const target = add(platform, localOffset(t, f));

    // --- T-CAM23: платформа двигает риг 1:1 ---
    let localVel = v3(0, 0, 0);
    if (lastPlatform) {
      const d = sub(platform, lastPlatform);
      if (follow) { cam = add(cam, d); lag = add(lag, d); smoothLook = add(smoothLook, d); }
      localVel = v3(
        (target.x - platform.x - (lastLocal.x)) / DT,
        (target.y - platform.y - (lastLocal.y)) / DT,
        (target.z - platform.z - (lastLocal.z)) / DT);
    }
    lastPlatform = platform;
    lastLocal = sub(target, platform);

    // --- вход лага ---
    const sampled = target; // follow => delay 0; non-follow => условно тоже 0 (худший случай)
    const d = sub(sampled, lag);
    lag = v3(lag.x + d.x * ease(LAG_XZ), lag.y + d.y * ease(LAG_Y), lag.z + d.z * ease(LAG_XZ));

    // --- desired / look / smooth ---
    const pitch = 15 * Math.PI / 180;
    const dir = v3(0, Math.sin(pitch), -Math.cos(pitch));
    const desired = add(add(lag, v3(dir.x * DIST, dir.y * DIST, dir.z * DIST)), v3(0, HEIGHT, 0));
    const lookTarget = add(lag, v3(0, LOOK_H, 0));
    if (len(sub(cam, desired)) >= 0.003) cam = lerp(cam, desired, ease(SMOOTH));
    smoothLook = lerp(smoothLook, lookTarget, ease(LOOK));

    // --- метрики (после прогрева 4 с) ---
    if (t > 4 && prevCam) {
      const off = len(sub(cam, target));            // "камера не успевает" = колебания этой дистанции
      if (off < offsetMin) offsetMin = off;
      if (off > offsetMax) offsetMax = off;
      const step = len(sub(cam, prevCam));          // шаг камеры за кадр (рваность)
      if (step < stepMin) stepMin = step;
      if (step > stepMax) stepMax = step;
    }
    prevCam = cam;
  }

  console.log(
    `${label.padEnd(46)} offset_to_player=${(offsetMin * 100).toFixed(1)}..${(offsetMax * 100).toFixed(1)} см ` +
    `(разброс ${((offsetMax - offsetMin) * 1000).toFixed(1)} мм) | шаг камеры ${(stepMin * 1000).toFixed(1)}..${(stepMax * 1000).toFixed(1)} мм (разброс ${((stepMax - stepMin) * 1000).toFixed(1)} мм)`);
}

console.log('--- ХОСТ: платформа гладкая (per-frame), локальная микродрожь ---');
run('было: без follow (лаг к сумме)', { follow: false, stepped: false, interpDelay: 0 });
run('T-CAM23: follow платформы 1:1', { follow: true, stepped: false, interpDelay: 0 });

console.log('\n--- КЛИЕНТ: платформа ступеньками 30 Гц, локальная микродрожь ---');
run('было: без follow и без интерполяции s', { follow: false, stepped: true, interpDelay: 0 });
run('T-CAM23: follow, без интерполяции s', { follow: true, stepped: true, interpDelay: 0 });
run('T-PAROM-25: follow + интерполяция s 0.07с', { follow: true, stepped: true, interpDelay: 0.07 });
run('T-PAROM-25 без follow: интерполяция s 0.07с', { follow: false, stepped: true, interpDelay: 0.07 });
