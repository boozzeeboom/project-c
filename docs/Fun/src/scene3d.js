import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';

const CITY_STYLE = {
  primum: { x: 0, z: -44, height: 18, radius: 10, buildings: 24, accent: 0xe4b568 },
  secundus: { x: -57, z: -9, height: 13, radius: 8, buildings: 15, accent: 0xc77656 },
  tertius: { x: 57, z: -9, height: 20, radius: 8, buildings: 17, accent: 0x7f9e83 },
  quartus: { x: 0, z: 48, height: 25, radius: 11, buildings: 27, accent: 0x9e7ba8 },
  kibwe: { x: -39, z: 26, height: 11, radius: 7, buildings: 13, accent: 0x5d9e98 },
  elbrus: { x: 39, z: 26, height: 23, radius: 8, buildings: 18, accent: 0x7185ae }
};

function randomSource(seed) {
  return () => {
    seed |= 0; seed = seed + 0x6D2B79F5 | 0;
    let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}

function material(color, roughness = 0.78, metalness = 0.12) {
  return new THREE.MeshStandardMaterial({ color, roughness, metalness });
}

function mesh(geometry, surface, parent, position = [0, 0, 0]) {
  const object = new THREE.Mesh(geometry, surface);
  object.position.set(...position);
  object.castShadow = true;
  object.receiveShadow = true;
  parent.add(object);
  return object;
}

export class World3D {
  constructor(canvas, game) {
    this.canvas = canvas;
    this.game = game;
    this.random = randomSource(41723);
    this.clock = new THREE.Clock();
    this.cityObjects = new Map();
    this.clickTargets = [];
    this.turbines = [];
    this.traffic = [];
    this.labels = [];
    this.pointerDown = new THREE.Vector2();
    this.tempVector = new THREE.Vector3();
    this.raycaster = new THREE.Raycaster();
    this.pointer = new THREE.Vector2();
    this.isMobile = matchMedia('(max-width: 700px)').matches;

    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false, powerPreference: 'high-performance' });
    this.renderer.setPixelRatio(Math.min(devicePixelRatio, this.isMobile ? 1.35 : 1.8));
    this.renderer.shadowMap.enabled = !this.isMobile;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 0.92;
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.info.autoReset = false;

    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x67515d);
    this.scene.fog = new THREE.FogExp2(0x8d777a, 0.0038);
    this.camera = new THREE.OrthographicCamera(-75, 75, 55, -55, 0.1, 500);
    this.camera.position.set(0, 105, 104);
    this.camera.lookAt(0, 0, 4);
    this.controls = new OrbitControls(this.camera, canvas);
    this.controls.target.set(0, 0, 4);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.06;
    this.controls.minPolarAngle = 0.22;
    this.controls.maxPolarAngle = 1.12;
    this.controls.minZoom = 0.65;
    this.controls.maxZoom = 2.2;
    this.controls.enablePan = true;
    this.controls.mouseButtons = { LEFT: THREE.MOUSE.ROTATE, MIDDLE: THREE.MOUSE.DOLLY, RIGHT: THREE.MOUSE.PAN };
    this.controls.touches = { ONE: THREE.TOUCH.ROTATE, TWO: THREE.TOUCH.DOLLY_PAN };

    this.createLights();
    this.createAtmosphere();
    Object.entries(CITY_STYLE).forEach(([id, style], index) => this.createCity(id, style, index));
    this.createRoutes();
    this.ship = this.createShip();
    this.scene.add(this.ship);
    this.createTraffic();
    this.createPostprocessing();
    this.createLabels();
    this.bindPicking();
    this.resize();
    window.addEventListener('resize', () => this.resize());
  }

  createLights() {
    this.scene.add(new THREE.HemisphereLight(0xffd9ae, 0x28223e, 2.25));
    this.sun = new THREE.DirectionalLight(0xffc78d, 4.4);
    this.sun.position.set(-60, 95, -35);
    this.sun.castShadow = !this.isMobile;
    this.sun.shadow.mapSize.set(2048, 2048);
    Object.assign(this.sun.shadow.camera, { left: -100, right: 100, top: 100, bottom: -100, near: 10, far: 250 });
    this.sun.shadow.bias = -0.00025;
    this.scene.add(this.sun);
    const rim = new THREE.DirectionalLight(0x819bd4, 1.25);
    rim.position.set(70, 25, 70);
    this.scene.add(rim);
  }

  createAtmosphere() {
    const abyss = new THREE.Mesh(new THREE.PlaneGeometry(600, 600), new THREE.MeshBasicMaterial({ color: 0x302440 }));
    abyss.rotation.x = -Math.PI / 2;
    abyss.position.y = -24;
    this.scene.add(abyss);

    const cloudGeometry = new THREE.IcosahedronGeometry(1, 1);
    const cloudMaterial = new THREE.MeshStandardMaterial({ color: 0xd9c7bd, transparent: true, opacity: 0.42, roughness: 1, depthWrite: false });
    this.clouds = new THREE.InstancedMesh(cloudGeometry, cloudMaterial, this.isMobile ? 100 : 180);
    const dummy = new THREE.Object3D();
    this.cloudData = [];
    for (let i = 0; i < this.clouds.count; i++) {
      const angle = this.random() * Math.PI * 2;
      const radius = 24 + this.random() * 145;
      const data = { x: Math.cos(angle) * radius, z: Math.sin(angle) * radius, y: -14 - this.random() * 8, speed: 0.35 + this.random() * 0.5, scale: [3 + this.random() * 5, 0.8 + this.random() * 1.2, 2 + this.random() * 4] };
      this.cloudData.push(data);
      dummy.position.set(data.x, data.y, data.z);
      dummy.scale.set(...data.scale);
      dummy.rotation.y = this.random() * Math.PI;
      dummy.updateMatrix();
      this.clouds.setMatrixAt(i, dummy.matrix);
    }
    this.scene.add(this.clouds);

    const starCount = 280;
    const positions = new Float32Array(starCount * 3);
    for (let i = 0; i < starCount; i++) {
      positions[i * 3] = (this.random() - 0.5) * 400;
      positions[i * 3 + 1] = 70 + this.random() * 100;
      positions[i * 3 + 2] = (this.random() - 0.5) * 400;
    }
    const starGeometry = new THREE.BufferGeometry();
    starGeometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    this.stars = new THREE.Points(starGeometry, new THREE.PointsMaterial({ color: 0xffead5, size: 0.45, transparent: true, opacity: 0.32, depthWrite: false }));
    this.scene.add(this.stars);

    this.lightning = new THREE.PointLight(0xb878ff, 0, 130, 1.5);
    this.lightning.position.set(25, -6, 20);
    this.scene.add(this.lightning);

    const sunDisc = new THREE.Mesh(new THREE.CircleGeometry(13, 48), new THREE.MeshBasicMaterial({ color: 0xffd29c, fog: false }));
    sunDisc.position.set(-105, 45, -130);
    sunDisc.lookAt(this.camera.position);
    this.scene.add(sunDisc);
  }

  createCity(id, style, index) {
    const group = new THREE.Group();
    group.position.set(style.x, 0, style.z);
    this.scene.add(group);
    const rockMat = material(index % 2 ? 0x5b514f : 0x665751, 0.98, 0.02);
    const rock = new THREE.CylinderGeometry(style.radius * 0.82, 1.2, style.height + 18, 9, 5);
    const positions = rock.attributes.position;
    for (let i = 0; i < positions.count; i++) {
      const y = positions.getY(i);
      const rough = (this.random() - 0.5) * 1.25 * (1 - Math.abs(y) / (style.height + 24));
      positions.setX(i, positions.getX(i) + rough);
      positions.setZ(i, positions.getZ(i) + rough);
    }
    rock.computeVertexNormals();
    mesh(rock, rockMat, group, [0, -style.height / 2 - 2, 0]);
    const cap = mesh(new THREE.CylinderGeometry(style.radius, style.radius * 0.86, 1.4, 12), material(0x585b56, 0.95), group, [0, 4.3, 0]);
    cap.receiveShadow = true;

    const platform = mesh(new THREE.CylinderGeometry(style.radius * 0.78, style.radius * 0.84, 0.65, 16), material(0x6b6256, 0.65, 0.35), group, [0, 5.25, 0]);
    const trim = mesh(new THREE.TorusGeometry(style.radius * 0.8, 0.18, 6, 32), material(style.accent, 0.45, 0.65), group, [0, 5.55, 0]);
    trim.rotation.x = Math.PI / 2;

    const wallMat = material(index === 1 ? 0x8c644c : 0x776b5f, 0.9, 0.06);
    const roofMat = material(index % 2 ? 0x426b67 : 0x5c5049, 0.65, 0.42);
    const windowMat = new THREE.MeshStandardMaterial({ color: 0xffc977, emissive: 0xffa43b, emissiveIntensity: 2.2, roughness: 0.4 });
    for (let i = 0; i < style.buildings; i++) {
      const angle = i * 2.39996 + index;
      const radial = Math.sqrt((i + 1) / style.buildings) * style.radius * 0.64;
      const x = Math.cos(angle) * radial;
      const z = Math.sin(angle) * radial;
      const width = 1.1 + this.random() * 1.25;
      const depth = 1.1 + this.random() * 1.15;
      const height = 1.8 + this.random() * (id === 'primum' || id === 'quartus' ? 5.8 : 3.8);
      mesh(new THREE.BoxGeometry(width, height, depth), wallMat, group, [x, 5.6 + height / 2, z]);
      const roof = mesh(new THREE.ConeGeometry(Math.max(width, depth) * 0.78, 0.75, 4), roofMat, group, [x, 5.95 + height, z]);
      roof.rotation.y = Math.PI / 4;
      if (i % 2 === 0) mesh(new THREE.BoxGeometry(width * 0.55, 0.18, 0.06), windowMat, group, [x, 5.8 + height * 0.63, z + depth / 2 + 0.035]);
      if (i % 5 === 0) {
        mesh(new THREE.CylinderGeometry(0.15, 0.2, 2.2, 6), material(0x554c45, 0.7, 0.5), group, [x + width * 0.3, 6.2 + height, z]);
      }
    }

    const towerHeight = id === 'primum' ? 15 : 9 + this.random() * 4;
    mesh(new THREE.CylinderGeometry(1.35, 1.8, towerHeight, 8), wallMat, group, [0, 5.6 + towerHeight / 2, 0]);
    mesh(new THREE.ConeGeometry(1.75, 2.6, 8), roofMat, group, [0, 6.9 + towerHeight, 0]);
    const beaconMat = new THREE.MeshStandardMaterial({ color: style.accent, emissive: style.accent, emissiveIntensity: 4 });
    mesh(new THREE.SphereGeometry(0.32, 10, 8), beaconMat, group, [0, 8.3 + towerHeight, 0]);

    const dock = mesh(new THREE.BoxGeometry(5.4, 0.5, 3.5), material(0x595753, 0.72, 0.5), group, [style.radius * 0.78, 5.1, 0]);
    dock.rotation.y = 0.06;
    for (const side of [-1, 1]) {
      const pipe = mesh(new THREE.TorusGeometry(style.radius * 0.55, 0.16, 5, 20, Math.PI), material(0x8b6d48, 0.48, 0.72), group, [0, 2.2, side * style.radius * 0.45]);
      pipe.rotation.set(Math.PI / 2, 0, side * 0.5);
    }

    const turbine = new THREE.Group();
    turbine.position.set(-style.radius * 0.65, 9, 0);
    const mast = mesh(new THREE.CylinderGeometry(0.12, 0.22, 7, 7), material(0x62594d, 0.6, 0.55), turbine, [0, -3.5, 0]);
    mast.castShadow = false;
    const rotor = new THREE.Group();
    rotor.rotation.y = Math.PI / 2;
    for (let blade = 0; blade < 4; blade++) {
      const part = mesh(new THREE.BoxGeometry(0.28, 3.8, 0.12), material(0xbaa77d, 0.65, 0.3), rotor, [0, 1.8, 0]);
      part.rotation.z = blade * Math.PI / 2;
      part.position.set(-Math.sin(blade * Math.PI / 2) * 1.8, Math.cos(blade * Math.PI / 2) * 1.8, 0);
    }
    turbine.add(rotor);
    group.add(turbine);
    this.turbines.push(rotor);

    const ring = new THREE.Mesh(new THREE.RingGeometry(style.radius * 0.95, style.radius * 1.02, 48), new THREE.MeshBasicMaterial({ color: style.accent, transparent: true, opacity: 0.8, side: THREE.DoubleSide, depthWrite: false }));
    ring.rotation.x = -Math.PI / 2;
    ring.position.y = 6.2;
    ring.visible = false;
    group.add(ring);

    const hit = new THREE.Mesh(new THREE.CylinderGeometry(style.radius * 1.3, style.radius * 1.3, 30, 12), new THREE.MeshBasicMaterial({ visible: false }));
    hit.position.y = 7;
    hit.userData.cityId = id;
    group.add(hit);
    this.clickTargets.push(hit);
    this.cityObjects.set(id, { group, ring, style, beaconMat });
  }

  createRoutes() {
    const connections = [['primum', 'secundus'], ['primum', 'tertius'], ['primum', 'kibwe'], ['primum', 'elbrus'], ['secundus', 'kibwe'], ['tertius', 'elbrus'], ['kibwe', 'quartus'], ['elbrus', 'quartus'], ['kibwe', 'elbrus']];
    this.routeLines = [];
    for (const [from, to] of connections) {
      const a = CITY_STYLE[from], b = CITY_STYLE[to];
      const curve = new THREE.QuadraticBezierCurve3(new THREE.Vector3(a.x, 10, a.z), new THREE.Vector3((a.x + b.x) / 2, 19, (a.z + b.z) / 2), new THREE.Vector3(b.x, 10, b.z));
      const geometry = new THREE.BufferGeometry().setFromPoints(curve.getPoints(36));
      const line = new THREE.Line(geometry, new THREE.LineDashedMaterial({ color: 0xe6cfac, transparent: true, opacity: 0.18, dashSize: 1.3, gapSize: 1.8, depthWrite: false }));
      line.computeLineDistances();
      this.scene.add(line);
      this.routeLines.push(line);
    }
  }

  createShip() {
    const group = new THREE.Group();
    const brass = material(0x9b754b, 0.42, 0.72);
    const hullMat = material(0x46525a, 0.55, 0.58);
    const canvasMat = material(0xc5b597, 0.95, 0.02);
    const glowMat = new THREE.MeshStandardMaterial({ color: 0x76d9e8, emissive: 0x35bad5, emissiveIntensity: 4, roughness: 0.25 });
    const balloon = mesh(new THREE.SphereGeometry(1, 24, 14), canvasMat, group, [0, 2.1, 0]);
    balloon.scale.set(1.75, 1.25, 3.8);
    for (let band = -2; band <= 2; band++) {
      const ring = mesh(new THREE.TorusGeometry(Math.sqrt(Math.max(0.2, 1 - band * band / 13)) * 1.78, 0.045, 5, 24), brass, group, [0, 2.1, band * 1.25]);
      ring.scale.y = 0.72;
    }
    const hull = mesh(new THREE.CapsuleGeometry(0.85, 3.4, 5, 12), hullMat, group, [0, -0.2, 0]);
    hull.rotation.x = Math.PI / 2;
    mesh(new THREE.BoxGeometry(1.45, 0.8, 1.35), brass, group, [0, 0.65, 0.6]);
    mesh(new THREE.BoxGeometry(1.15, 0.38, 0.05), glowMat, group, [0, 0.75, 1.3]);
    for (const x of [-1.6, 1.6]) {
      const engine = mesh(new THREE.CylinderGeometry(0.58, 0.58, 0.3, 16), hullMat, group, [x, -0.5, -0.5]);
      const glow = mesh(new THREE.CylinderGeometry(0.45, 0.45, 0.06, 16), glowMat, group, [x, -0.68, -0.5]);
      engine.rotation.y = glow.rotation.y = 0;
      const prop = new THREE.Group();
      prop.position.set(x, 0.1, -2.35);
      for (let i = 0; i < 3; i++) {
        const blade = mesh(new THREE.BoxGeometry(0.13, 1.65, 0.08), brass, prop, [0, 0.75, 0]);
        blade.rotation.z = i * Math.PI * 2 / 3;
        blade.position.set(-Math.sin(i * Math.PI * 2 / 3) * 0.75, Math.cos(i * Math.PI * 2 / 3) * 0.75, 0);
      }
      group.add(prop);
      this.turbines.push(prop);
    }
    for (let crate = 0; crate < 3; crate++) mesh(new THREE.BoxGeometry(0.75, 0.6, 0.8), material(0x796046, 0.9), group, [-0.85 + crate * 0.85, 0.55, -1.15]);
    const light = new THREE.PointLight(0x4fc3f7, 3, 11, 2);
    light.position.set(0, -1, 0);
    group.add(light);
    group.scale.setScalar(0.78);
    return group;
  }

  createTraffic() {
    for (let i = 0; i < (this.isMobile ? 4 : 8); i++) {
      const vessel = new THREE.Group();
      const body = mesh(new THREE.SphereGeometry(1, 10, 6), material(i % 2 ? 0x716b62 : 0x59666a, 0.8, 0.25), vessel);
      body.scale.set(0.55, 0.42, 1.5);
      mesh(new THREE.BoxGeometry(0.5, 0.35, 0.8), material(0x5a4635, 0.8, 0.3), vessel, [0, -0.5, 0]);
      this.scene.add(vessel);
      this.traffic.push({ vessel, radius: 35 + this.random() * 60, speed: 0.025 + this.random() * 0.04, phase: this.random() * Math.PI * 2, height: 8 + this.random() * 18 });
    }
  }

  createPostprocessing() {
    this.composer = new EffectComposer(this.renderer);
    this.composer.addPass(new RenderPass(this.scene, this.camera));
    if (!this.isMobile) {
      const bloom = new UnrealBloomPass(new THREE.Vector2(innerWidth, innerHeight), 0.32, 0.5, 0.86);
      this.composer.addPass(bloom);
    }
    this.composer.addPass(new OutputPass());
  }

  createLabels() {
    const layer = document.getElementById('city-labels');
    layer.innerHTML = '';
    for (const [id, city] of this.cityObjects) {
      const label = document.createElement('button');
      label.className = 'city-label';
      label.dataset.city = id;
      label.innerHTML = `<span class="city-label__dot"></span><span>${this.game.constructor ? '' : ''}${this.gameCityName(id)}</span><small>УД ${city.style.height > 20 ? 5 : city.style.height > 15 ? 3 : 4}</small>`;
      label.addEventListener('click', (event) => { event.stopPropagation(); this.game.selectCity(id); });
      layer.appendChild(label);
      this.labels.push({ id, element: label });
    }
  }

  gameCityName(id) {
    return this.game?.cityNames?.[id] || ({ primum: 'Примум', secundus: 'Секунд', tertius: 'Тертиус', quartus: 'Квартус', kibwe: 'Кибо', elbrus: 'Эльбрус' })[id];
  }

  bindPicking() {
    this.canvas.addEventListener('pointerdown', (event) => this.pointerDown.set(event.clientX, event.clientY));
    this.canvas.addEventListener('pointerup', (event) => {
      if (Math.hypot(event.clientX - this.pointerDown.x, event.clientY - this.pointerDown.y) > 7) return;
      const rect = this.canvas.getBoundingClientRect();
      this.pointer.set((event.clientX - rect.left) / rect.width * 2 - 1, -(event.clientY - rect.top) / rect.height * 2 + 1);
      this.raycaster.setFromCamera(this.pointer, this.camera);
      const hit = this.raycaster.intersectObjects(this.clickTargets, false)[0];
      if (hit) this.game.selectCity(hit.object.userData.cityId);
    });
  }

  resize() {
    const width = this.canvas.parentElement.clientWidth;
    const height = this.canvas.parentElement.clientHeight;
    if (!width || !height) return;
    const aspect = width / height;
    const viewHeight = this.isMobile ? 142 : 132;
    this.camera.left = -viewHeight * aspect / 2;
    this.camera.right = viewHeight * aspect / 2;
    this.camera.top = viewHeight / 2;
    this.camera.bottom = -viewHeight / 2;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height, false);
    this.composer.setSize(width, height);
  }

  focusCity(id) {
    const city = this.cityObjects.get(id);
    if (!city) return;
    this.controls.target.set(city.style.x * 0.22, 2, city.style.z * 0.22 + 4);
  }

  render() {
    const elapsed = this.clock.getElapsedTime();
    const state = this.game.state;
    const phase = state.timePhase || 0;
    const night = phase === 3;
    const targetSky = night ? 0x17182d : phase === 1 ? 0x718894 : phase === 2 ? 0x87545a : 0x67515d;
    this.scene.background.lerp(new THREE.Color(targetSky), 0.012);
    this.scene.fog.color.copy(this.scene.background);
    this.stars.material.opacity = THREE.MathUtils.lerp(this.stars.material.opacity, night ? 0.8 : 0.12, 0.02);
    this.sun.intensity = THREE.MathUtils.lerp(this.sun.intensity, night ? 0.6 : 3.4, 0.015);
    this.lightning.intensity = Math.max(0, this.lightning.intensity * 0.86);
    if (this.random() < 0.0018) this.lightning.intensity = 40;

    this.turbines.forEach((rotor, index) => { rotor.rotation.z = elapsed * (index < 6 ? 0.7 : 5.5); });
    this.traffic.forEach((craft) => {
      const angle = elapsed * craft.speed + craft.phase;
      craft.vessel.position.set(Math.cos(angle) * craft.radius, craft.height + Math.sin(angle * 3) * 0.7, Math.sin(angle) * craft.radius);
      craft.vessel.rotation.y = -angle;
    });

    for (const [id, city] of this.cityObjects) {
      city.ring.visible = this.game.selectedCity === id || state.currentCity === id;
      city.ring.material.opacity = this.game.selectedCity === id ? 0.95 : 0.42;
      city.ring.scale.setScalar(1 + Math.sin(elapsed * 2.4) * 0.025);
      city.beaconMat.emissiveIntensity = 3.2 + Math.sin(elapsed * 2 + city.style.x) * 1.3;
    }

    let shipPosition;
    if (state.ship.traveling && state.ship.startPos && state.ship.endPos) {
      const progress = THREE.MathUtils.clamp(state.ship.travelProgress / state.ship.travelDuration, 0, 1);
      const smooth = progress * progress * (3 - 2 * progress);
      const start = this.normalizedToWorld(state.ship.startPos);
      const end = this.normalizedToWorld(state.ship.endPos);
      shipPosition = start.lerp(end, smooth);
      shipPosition.y = 12 + Math.sin(progress * Math.PI) * 12;
      this.ship.rotation.y = Math.atan2(end.x - start.x, end.z - start.z);
      this.ship.rotation.z = Math.sin(progress * Math.PI) * -0.05;
    } else {
      const city = this.cityObjects.get(state.currentCity);
      shipPosition = new THREE.Vector3(city.style.x + city.style.radius * 0.85, 9, city.style.z);
      this.ship.rotation.z *= 0.92;
    }
    shipPosition.y += Math.sin(elapsed * 1.5) * 0.25;
    this.ship.position.lerp(shipPosition, state.ship.traveling ? 0.22 : 0.08);
    const shipScale = ({ dinghy: 0.78, hauler: 0.96, freighter: 1.15, cargoliner: 1.36 })[state.shipType] || 0.78;
    this.ship.scale.lerp(new THREE.Vector3(shipScale, shipScale, shipScale), 0.04);

    this.controls.update();
    this.updateLabels();
    this.renderer.info.reset();
    this.composer.render();
    this.frames = (this.frames || 0) + 1;
    window.__worldStats = { calls: this.renderer.info.render.calls, triangles: this.renderer.info.render.triangles, geometries: this.renderer.info.memory.geometries, textures: this.renderer.info.memory.textures };
  }

  normalizedToWorld(position) {
    return new THREE.Vector3((position.x - 0.5) * 150, 10, (position.y - 0.5) * 110);
  }

  updateLabels() {
    const width = this.canvas.clientWidth, height = this.canvas.clientHeight;
    for (const label of this.labels) {
      const city = this.cityObjects.get(label.id);
      this.tempVector.set(city.style.x, 17 + (label.id === 'primum' ? 10 : 0), city.style.z).project(this.camera);
      const visible = this.tempVector.z < 1 && Math.abs(this.tempVector.x) < 1.15 && Math.abs(this.tempVector.y) < 1.15;
      label.element.style.display = visible ? '' : 'none';
      label.element.style.transform = `translate(-50%, -100%) translate(${(this.tempVector.x * 0.5 + 0.5) * width}px, ${(-this.tempVector.y * 0.5 + 0.5) * height}px)`;
      label.element.classList.toggle('is-current', this.game.state.currentCity === label.id);
      label.element.classList.toggle('is-selected', this.game.selectedCity === label.id);
    }
  }
}
