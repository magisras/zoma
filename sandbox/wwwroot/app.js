// The view: three.js draws boxes where the C# simulation says they are. No game logic lives here.
// Per frame: read keys → DotNet Tick(dt, keys) → update meshes → render. One interop call a frame.

import * as THREE from './vendor/three-r170.module.js';

const ASSEMBLY = 'TwentyTons.Sandbox';
const KEY = { up: 1, down: 2, left: 4, right: 8, horn: 16 };

// Colours per vehicle class, in the order of the C# VehicleClass enum.
// Pedestrian, Rickshaw, Cng, Car, Truck, Bus. Flat, no textures: this is a grey box.
const CLASS_COLOURS = [0xe6cfa7, 0x4f6fa8, 0x3f8f4f, 0xc9c9c9, 0x7d6f5c, 0x6c8aa0];
const PLAYER_COLOUR = 0xd9a441;
const YIELD_TINT = 0x9ad0ff;
const BLUFF_TINT = 0xff8a7a;

let renderer, scene, camera, playerLight;
let meshes = new Map();          // agent id → mesh
let keys = 0;
let topDown = false;
let seed = 1;
let lastTime = performance.now();
let frames = 0, fpsTime = 0;
let sceneRoot;                   // road + buildings, rebuilt on reset

export function main() {
  setupRenderer();
  buildWorld(DotNet.invokeMethod(ASSEMBLY, 'Init', seed));
  bindInput();
  bindTuning();
  document.getElementById('loading').classList.add('hidden');
  requestAnimationFrame(loop);
}

// ---------------------------------------------------------------- setup

function setupRenderer() {
  const canvas = document.getElementById('view');
  renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));   // laptops first
  renderer.setSize(window.innerWidth, window.innerHeight);
  renderer.shadowMap.enabled = false;

  scene = new THREE.Scene();
  scene.background = new THREE.Color(0xb9c3cc);                       // Dhaka haze
  scene.fog = new THREE.Fog(0xb9c3cc, 150, 600);

  camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.5, 1500);

  scene.add(new THREE.HemisphereLight(0xdde6ee, 0x55504a, 0.9));
  const sun = new THREE.DirectionalLight(0xfff1dc, 1.1);
  sun.position.set(80, 120, -60);
  scene.add(sun);

  const ground = new THREE.Mesh(
    new THREE.PlaneGeometry(4000, 4000),
    new THREE.MeshLambertMaterial({ color: 0x5e6258 }));
  ground.rotation.x = -Math.PI / 2;
  ground.position.y = -0.05;
  scene.add(ground);

  window.addEventListener('resize', () => {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
  });
}

// Road surface from the two edge polylines, buildings as boxes.
function buildWorld(sceneDto) {
  if (sceneRoot) scene.remove(sceneRoot);
  for (const m of meshes.values()) scene.remove(m);
  meshes.clear();
  sceneRoot = new THREE.Group();

  const L = sceneDto.leftEdge, R = sceneDto.rightEdge;
  const n = L.length / 2;
  const verts = new Float32Array(n * 2 * 3);
  const idx = [];
  for (let i = 0; i < n; i++) {
    verts.set([L[2 * i], 0, L[2 * i + 1]], i * 6);          // left vertex
    verts.set([R[2 * i], 0, R[2 * i + 1]], i * 6 + 3);      // right vertex
    if (i < n - 1) {
      const a = i * 2, b = a + 1, c = a + 2, d = a + 3;
      idx.push(a, c, b, b, c, d);
    }
  }
  const road = new THREE.BufferGeometry();
  road.setAttribute('position', new THREE.BufferAttribute(verts, 3));
  road.setIndex(idx);
  road.computeVertexNormals();
  sceneRoot.add(new THREE.Mesh(road, new THREE.MeshLambertMaterial({ color: 0x4a4a4c })));

  // Kerb lines help read the road edge from the chase camera.
  for (const edge of [L, R]) {
    const pts = [];
    for (let i = 0; i < edge.length; i += 2) pts.push(new THREE.Vector3(edge[i], 0.02, edge[i + 1]));
    sceneRoot.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts),
      new THREE.LineBasicMaterial({ color: 0xa8a8a0 })));
  }

  const B = sceneDto.buildings;
  const box = new THREE.BoxGeometry(1, 1, 1);
  for (let i = 0; i < B.length; i += 6) {
    const [x, z, yaw, len, wid, hgt] = B.slice(i, i + 6);
    const shade = 0x7a + Math.floor(Math.random() * 0x20);
    const m = new THREE.Mesh(box, new THREE.MeshLambertMaterial({ color: (shade << 16) | (shade << 8) | (shade - 4) }));
    m.scale.set(wid, hgt, len);
    m.position.set(x, hgt / 2, z);
    m.rotation.y = yaw;
    sceneRoot.add(m);
  }
  scene.add(sceneRoot);
}

// ---------------------------------------------------------------- input

function bindInput() {
  const map = {
    KeyW: KEY.up, ArrowUp: KEY.up, KeyS: KEY.down, ArrowDown: KEY.down,
    KeyA: KEY.left, ArrowLeft: KEY.left, KeyD: KEY.right, ArrowRight: KEY.right,
    KeyH: KEY.horn, Space: KEY.horn,
  };
  window.addEventListener('keydown', e => {
    if (e.target.tagName === 'INPUT') return;
    if (map[e.code]) { keys |= map[e.code]; e.preventDefault(); }
    if (e.code === 'KeyC') topDown = !topDown;
    if (e.code === 'KeyT') document.getElementById('tuning').classList.toggle('hidden');
    if (e.code === 'KeyR') { seed++; buildWorld(DotNet.invokeMethod(ASSEMBLY, 'Reset', seed)); }
  });
  window.addEventListener('keyup', e => { if (map[e.code]) keys &= ~map[e.code]; });
  window.addEventListener('blur', () => { keys = 0; });
  bindTouch();
}

// On-screen buttons set the same key bits as the keyboard, so the simulation never knows the difference.
function bindTouch() {
  for (const b of document.querySelectorAll('#touch .tbtn[data-key]')) {
    const bit = parseInt(b.dataset.key, 10);
    const down = e => { e.preventDefault(); keys |= bit; b.classList.add('down'); };
    const up = e => { e.preventDefault(); keys &= ~bit; b.classList.remove('down'); };
    b.addEventListener('pointerdown', down);
    b.addEventListener('pointerup', up);
    b.addEventListener('pointercancel', up);
    b.addEventListener('pointerleave', up);
    b.addEventListener('contextmenu', e => e.preventDefault());
  }
  document.getElementById('touch-camera').addEventListener('click', () => { topDown = !topDown; });
  document.getElementById('touch-tuning').addEventListener('click', () => {
    const t = document.getElementById('tuning');
    t.classList.toggle('open');
    t.classList.toggle('hidden', !t.classList.contains('open'));
  });
  document.getElementById('touch-restart').addEventListener('click', () => {
    seed++; buildWorld(DotNet.invokeMethod(ASSEMBLY, 'Reset', seed));
  });
}

function bindTuning() {
  for (const input of document.querySelectorAll('#tuning input[type=range]')) {
    const output = input.nextElementSibling;
    input.addEventListener('input', () => {
      const v = parseFloat(input.value);
      output.value = Number.isInteger(parseFloat(input.step)) ? v.toFixed(0) : v.toFixed(2);
      DotNet.invokeMethod(ASSEMBLY, 'SetParam', input.dataset.param, v);
    });
  }
}

// ---------------------------------------------------------------- frame

function loop(now) {
  const dt = Math.min(0.1, (now - lastTime) / 1000);
  lastTime = now;

  const frame = DotNet.invokeMethod(ASSEMBLY, 'Tick', dt, keys);
  updateAgents(frame.agents);
  updateHud(frame, dt);
  renderer.render(scene, camera);
  requestAnimationFrame(loop);
}

const geometryBox = new THREE.BoxGeometry(1, 1, 1);
let playerPose = { x: 0, z: 0, yaw: 0 };

function updateAgents(data) {
  const seen = new Set();
  for (let i = 0; i < data.length; i += 10) {
    const id = data[i], cls = data[i + 1], x = data[i + 2], y = data[i + 3], z = data[i + 4];
    const yaw = data[i + 5], len = data[i + 6], wid = data[i + 7], hgt = data[i + 8], flags = data[i + 9];
    const isPlayer = (flags & 1) !== 0;
    seen.add(id);

    let m = meshes.get(id);
    if (!m) {
      m = new THREE.Mesh(geometryBox, new THREE.MeshLambertMaterial({ color: isPlayer ? PLAYER_COLOUR : CLASS_COLOURS[cls] }));
      m.scale.set(wid, hgt, len);                     // x across, y up, z along travel
      m.userData.base = m.material.color.getHex();
      meshes.set(id, m);
      scene.add(m);
    }
    m.position.set(x, y, z);
    m.rotation.y = yaw;

    // State tints: a horn flashes, a yielder goes pale blue, a bluffer goes red.
    let colour = m.userData.base;
    if (flags & 2) colour = 0xfff2b0;
    else if (flags & 16) colour = BLUFF_TINT;
    else if (flags & 4) colour = YIELD_TINT;
    if (m.material.color.getHex() !== colour) m.material.color.setHex(colour);

    if (isPlayer) playerPose = { x, z, yaw };
  }
  for (const [id, m] of meshes) {
    if (!seen.has(id)) { scene.remove(m); m.material.dispose(); meshes.delete(id); }
  }
  placeCamera();
}

const camTarget = new THREE.Vector3(), camPos = new THREE.Vector3();
function placeCamera() {
  const { x, z, yaw } = playerPose;
  const fx = Math.sin(yaw), fz = Math.cos(yaw);
  if (topDown) {
    camPos.set(x, 90, z);
    camera.up.set(fx, 0, fz);
    camTarget.set(x, 0, z);
    camera.position.lerp(camPos, 0.2);
  } else {
    camPos.set(x - fx * 22, 8, z - fz * 22);
    camera.up.set(0, 1, 0);
    camTarget.set(x + fx * 12, 1.5, z + fz * 12);
    camera.position.lerp(camPos, 0.12);
  }
  camera.lookAt(camTarget);
}

// ---------------------------------------------------------------- HUD

const el = id => document.getElementById(id);
function updateHud(f, dt) {
  el('speed').textContent = f.speedKmh.toFixed(0);
  const touching = f.gapAheadMetres < 0;
  el('gap').textContent = f.gapAheadMetres > 190 ? 'clear' : touching ? 'contact' : f.gapAheadMetres.toFixed(1) + ' m';
  const hw = el('headway');
  hw.textContent = f.headwaySeconds > 50 ? '—' : touching ? 'contact' : f.headwaySeconds.toFixed(2) + ' s';
  hw.className = f.headwaySeconds < 0.5 ? 'danger' : f.headwaySeconds < 1.0 ? 'warn' : '';
  el('minHeadway').textContent = f.minHeadwaySeconds > 50 ? '—' : f.minHeadwaySeconds.toFixed(2) + ' s';
  el('nearMisses').textContent = f.nearMisses;
  el('nearMissRate').textContent = f.time > 10 ? f.nearMissesPerMinute.toFixed(1) + ' / min' : '';
  el('contacts').textContent = f.contacts;
  el('hornPresses').textContent = f.hornPresses;
  el('yields').textContent = f.yieldsToHorn + ' moved';
  el('passengers').textContent = f.passengers;
  el('brakeWear').textContent = (f.brakeWear * 100).toFixed(0) + '%';
  const mins = Math.floor(f.time / 60), secs = Math.floor(f.time % 60);
  el('time').textContent = mins + ':' + String(secs).padStart(2, '0');
  el('distance').textContent = (f.distanceMetres / 1000).toFixed(2) + ' km · ' + f.agentCount + ' agents';
  el('horn-indicator').classList.toggle('on', (keys & KEY.horn) !== 0);
  el('overlay').classList.toggle('on', f.personHit);

  frames++; fpsTime += dt;
  if (fpsTime >= 1) { el('fps').textContent = frames + ' fps'; frames = 0; fpsTime = 0; }
}
