// The view: three.js draws boxes where the C# simulation says they are. No game logic lives here.
// Per frame: read keys → DotNet Tick(dt, keys) → update meshes → render. One interop call a frame.

import * as THREE from './vendor/three-r170.module.js';

const ASSEMBLY = 'TwentyTons.Sandbox';
const KEY = { up: 1, down: 2, left: 4, right: 8, horn: 16, autopilot: 32, door: 64, pay: 128, refuse: 256 };

// Colours per vehicle class, in the order of the C# VehicleClass enum.
// Pedestrian, Rickshaw, Cng, Car, Truck, Bus. Flat, no textures: this is a grey box.
const CLASS_COLOURS = [0xe6cfa7, 0x4f6fa8, 0x3f8f4f, 0xc9c9c9, 0x7d6f5c, 0x6c8aa0];
const PLAYER_COLOUR = 0xd9a441;
const OWN_COMPANY_COLOUR = 0xb8862e;
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
  sceneRoot.add(stripMesh(L, R, 0x4a4a4c));
  // The oncoming carriageway and the painted median between them.
  sceneRoot.add(stripMesh(sceneDto.oncomingLeftEdge, sceneDto.oncomingRightEdge, 0x454547));
  const medPts = [];
  for (let i = 0; i < R.length; i += 2) medPts.push(new THREE.Vector3(R[i], 0.03, R[i + 1]));
  sceneRoot.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(medPts), new THREE.LineBasicMaterial({ color: 0xd8d2b4 })));

  // Kerb lines help read the road edge from the chase camera.
  for (const edge of [L, R]) {
    const pts = [];
    for (let i = 0; i < edge.length; i += 2) pts.push(new THREE.Vector3(edge[i], 0.02, edge[i + 1]));
    sceneRoot.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts),
      new THREE.LineBasicMaterial({ color: 0xa8a8a0 })));
  }

  // Cross streets: same strip construction, slightly different shade so junctions read.
  for (let c = 0; c < sceneDto.crossEdges.length; c += 2) {
    const CL = sceneDto.crossEdges[c], CR = sceneDto.crossEdges[c + 1];
    sceneRoot.add(stripMesh(CL, CR, 0x47474a));
  }

  // Zones: a pale slab on the kerb where people wait; crowds are drawn per frame.
  zoneMarkers.length = 0;
  for (let i = 0; i < sceneDto.zones.length; i += 4) {
    const [x, z, yaw, side] = sceneDto.zones.slice(i, i + 4);
    const slab = new THREE.Mesh(geometryBox, new THREE.MeshLambertMaterial({ color: 0x8c8878 }));
    slab.scale.set(2.5, 0.12, 30);
    slab.position.set(x, 0.06, z);
    slab.rotation.y = yaw;
    sceneRoot.add(slab);
    zoneMarkers.push({ x, z, yaw, side, name: sceneDto.zoneNames[i / 4], boxes: [] });
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

// A road surface between two edge polylines (x,z pairs), as a triangle strip.
function stripMesh(L, R, colour) {
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
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(verts, 3));
  geo.setIndex(idx);
  geo.computeVertexNormals();
  return new THREE.Mesh(geo, new THREE.MeshLambertMaterial({ color: colour }));
}

// Waiting crowds: one small box per person, in a loose cluster behind the kerb slab.
const zoneMarkers = [];
const crowdMaterial = new THREE.MeshLambertMaterial({ color: 0xd9c4a0 });
function updateCrowds(counts) {
  zoneMarkers.forEach((zm, i) => {
    const want = Math.min(counts[i] || 0, 30);
    while (zm.boxes.length < want) {
      const k = zm.boxes.length;
      const m = new THREE.Mesh(geometryBox, crowdMaterial);
      m.scale.set(0.45, 1.7, 0.45);
      // Deterministic scatter: along the slab and a little away from the road.
      const along = ((k * 7) % 26) - 13 + ((k * 3) % 5) * 0.3;
      const away = 0.6 + ((k * 5) % 4) * 0.5;
      const fx = Math.sin(zm.yaw), fz = Math.cos(zm.yaw);
      const rx = Math.cos(zm.yaw), rz = -Math.sin(zm.yaw);     // right of travel
      m.position.set(zm.x + fx * along + rx * away * zm.side, 0.85, zm.z + fz * along + rz * away * zm.side);
      m.rotation.y = zm.yaw;
      scene.add(m);
      zm.boxes.push(m);
    }
    while (zm.boxes.length > want) scene.remove(zm.boxes.pop());
  });
}

// Officers: a thin tall box at the junction centre, a cane (bar) pointing along the open flow.
const officers = [];
function updateJunctions(list) {
  while (officers.length < list.length) {
    const body = new THREE.Mesh(geometryBox, new THREE.MeshLambertMaterial({ color: 0x2f4f8f }));
    body.scale.set(0.6, 1.8, 0.6);
    const cane = new THREE.Mesh(geometryBox, new THREE.MeshLambertMaterial({ color: 0xf0e68c }));
    cane.scale.set(0.15, 0.15, 3.0);
    scene.add(body); scene.add(cane);
    officers.push({ body, cane });
  }
  list.forEach((j, i) => {
    const o = officers[i];
    o.body.position.set(j.x, 0.9, j.z);
    o.body.rotation.y = j.mainYaw;
    // Cane along the main road when main is open, across it when the cross street is open.
    o.cane.position.set(j.x, 1.6, j.z);
    o.cane.rotation.y = j.mainOpen ? j.mainYaw : j.mainYaw + Math.PI / 2;
    o.cane.material.color.setHex(j.mainOpen ? 0x7fb069 : 0xd9534f);
  });
}

// ---------------------------------------------------------------- input

function bindInput() {
  const map = {
    KeyW: KEY.up, ArrowUp: KEY.up, KeyS: KEY.down, ArrowDown: KEY.down,
    KeyA: KEY.left, ArrowLeft: KEY.left, KeyD: KEY.right, ArrowRight: KEY.right,
    KeyH: KEY.horn, Space: KEY.horn, Digit1: KEY.pay, Digit2: KEY.refuse,
  };
  window.addEventListener('keydown', e => {
    if (e.target.tagName === 'INPUT') return;
    if (map[e.code]) { keys |= map[e.code]; e.preventDefault(); }
    if (e.code === 'KeyC') topDown = !topDown;
    if (e.code === 'KeyP') keys ^= KEY.autopilot;              // toggle the careful autopilot
    if (e.code === 'KeyE') keys ^= KEY.door;                   // the helper opens or shuts the door
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
  document.getElementById('touch-door').addEventListener('click', () => { keys ^= KEY.door; });
  // Card buttons: a one-frame key press.
  const pulse = bit => { keys |= bit; setTimeout(() => { keys &= ~bit; }, 120); };
  document.getElementById('pay').addEventListener('click', () => pulse(KEY.pay));
  document.getElementById('refuse').addEventListener('click', () => pulse(KEY.refuse));
  document.getElementById('sleep-bus').addEventListener('click', () => DotNet.invokeMethod(ASSEMBLY, 'Sleep', false));
  document.getElementById('sleep-bed').addEventListener('click', () => DotNet.invokeMethod(ASSEMBLY, 'Sleep', true));
  document.getElementById('next-work').addEventListener('click', () => { seed++; buildWorld(DotNet.invokeMethod(ASSEMBLY, 'NextDay', true, seed)); });
  document.getElementById('next-rest').addEventListener('click', () => { seed++; buildWorld(DotNet.invokeMethod(ASSEMBLY, 'NextDay', false, seed)); });
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
  updateJunctions(frame.junctions);
  updateCrowds(frame.zoneCrowds);
  window.twentyTons = { lateral: frame.lateral, yawErrorDeg: frame.yawErrorDeg, speedKmh: frame.speedKmh };   // for scripted drivers

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
      const ownCompany = (flags & 32) !== 0;           // same livery as the player, a shade darker
      const colour = isPlayer ? PLAYER_COLOUR : ownCompany ? OWN_COMPANY_COLOUR : CLASS_COLOURS[cls];
      m = new THREE.Mesh(geometryBox, new THREE.MeshLambertMaterial({ color: colour }));
      m.scale.set(wid, hgt, len);                     // x across, y up, z along travel
      m.userData.base = m.material.color.getHex();
      meshes.set(id, m);
      scene.add(m);
    }
    m.position.set(x, y, z);
    m.rotation.y = yaw;

    // State tints: a horn flashes, a yielder goes pale blue, a bluffer goes red.
    let colour = m.userData.base;
    if (flags & 64) colour = 0xffffff;              // door open: white while loading
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
  el('caneRuns').textContent = f.caneRuns;
  const ws = el('wrongSide');
  ws.textContent = f.wrongSideSeconds.toFixed(0) + ' s';
  ws.className = f.wrongSideNow ? 'warn' : '';
  el('hornPresses').textContent = f.hornPresses;
  el('yields').textContent = f.yieldsToHorn + ' moved';
  el('passengers').textContent = f.passengers + ' / ' + f.seats + ' seats';
  el('fares').textContent = 'Tk ' + f.faresTk.toFixed(0);
  el('door').textContent = f.doorOpen ? (f.atDoor ? 'open · ' + f.atDoor : 'open') : 'shut';
  el('door').className = f.doorOpen ? 'warn' : '';
  el('zone').textContent = f.zoneName ? f.zoneName + ' · ' + f.zoneWaiting + ' waiting' : '—';
  el('missed').textContent = f.missedAlights;
  el('stopsLost').textContent = f.stopsLost;
  el('helper').textContent = f.helperGap;
  el('rivals').textContent = f.rivals.map(r => r.name + ' ' + (r.gapMetres >= 0 ? '+' : '') + r.gapMetres.toFixed(0) + ' m · ' + r.action.replace(/([A-Z])/g, ' $1').trim().toLowerCase() + ' · ' + r.aboard + ' aboard · grudge ' + r.grudge).join('\n');
  el('brakeWear').textContent = (f.brakeWear * 100).toFixed(0) + '%';
  const mins = Math.floor(f.time / 60), secs = Math.floor(f.time % 60);
  el('time').textContent = mins + ':' + String(secs).padStart(2, '0');
  el('distance').textContent = (f.distanceMetres / 1000).toFixed(2) + ' km · ' + f.agentCount + ' agents';
  el('horn-indicator').classList.toggle('on', (keys & KEY.horn) !== 0);
  el('autopilot').hidden = !f.autopilot;
  el('clock').textContent = f.clock + ' · trip ' + f.trips;
  el('paidOut').textContent = 'paid out Tk ' + f.paidOutTk.toFixed(0);

  // The sergeant's card.
  const sc = el('sergeant');
  sc.classList.toggle('on', f.sergeantActive);
  if (f.sergeantActive) {
    el('sergeant-text').textContent = f.sergeantText;
    el('sergeant-wait').textContent = f.sergeantDecided ? 'Papers: ' + Math.ceil(f.sergeantWaitLeft) + ' s' : '';
    el('sergeant-choice').hidden = f.sergeantDecided;
  }

  // The end of the day.
  const ov = el('overlay');
  ov.classList.toggle('on', f.dayOver);
  if (f.dayOver && ov.dataset.shown !== f.clock) {
    ov.dataset.shown = f.clock;
    const L = f.ledger;
    el('day-headline').textContent = L.arrested ? 'You hit a person.' : 'End of the shift, ' + f.clock + '.';
    el('day-sub').textContent = L.arrested ? 'The crowd gathers. The police take the bus and the day\'s money. A case follows.' : f.trips + ' trips. The owner gets the zoma whatever happened.';
    const rows = [['Fares', L.fares], ['Zoma (the deposit)', -L.zoma], ['Fuel', -L.fuel], ['Lineman', -L.lineman], ['Party man', -L.partyMan], ['Sergeant', -L.sergeant], ['Cases', -L.cases], ['Repairs', -L.repairs]];
    el('ledger').innerHTML = rows.filter(r => r[1] !== 0).map(r => '<div class="lrow"><span>' + r[0] + '</span><span>' + (r[1] < 0 ? '−' : '') + 'Tk ' + Math.abs(r[1]).toFixed(0) + '</span></div>').join('')
      + '<div class="lrow total"><span>What the crew eats</span><span class="' + (L.crewNet < 0 ? 'danger' : 'ok') + '">' + (L.crewNet < 0 ? '−' : '') + 'Tk ' + Math.abs(L.crewNet).toFixed(0) + '</span></div>';
    el('events').innerHTML = f.events.slice(-8).map(e => '<div>' + e + '</div>').join('');
  }
  if (f.dayOver) {
    el('sleep-choice').hidden = f.sleptChosen;
    el('day-choice').hidden = !f.sleptChosen;
    el('savings').textContent = 'Day ' + f.day + ' · savings ' + (f.savingsTk < 0 ? '−' : '') + 'Tk ' + Math.abs(f.savingsTk).toFixed(0);
  }

  // Subtitles: the crew's voices, for a few seconds each.
  const sub = el('subtitle');
  const showSub = f.subtitle && f.subtitleAge < 4.5;
  sub.classList.toggle('on', !!showSub);
  if (showSub) sub.textContent = f.subtitle;

  // The body: a narrowing view and, now and then, nothing at all.
  el('vignette').style.opacity = (f.tunnel * 0.9).toFixed(2);
  el('blackout').classList.toggle('on', f.asleep);
  el('fatigue').textContent = (f.fatigue * 100).toFixed(0) + '%' + (f.microSleeps ? ' · ' + f.microSleeps + ' micro-sleeps' : '');

  frames++; fpsTime += dt;
  if (fpsTime >= 1) { el('fps').textContent = frames + ' fps'; frames = 0; fpsTime = 0; }
}
