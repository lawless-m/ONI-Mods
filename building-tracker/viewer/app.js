// -- Element color map (ONI-like colors) --
const ELEMENT_COLORS = {
  // Gases
  Oxygen:        [0x6c, 0xb4, 0xff],
  Hydrogen:      [0x2c, 0x3c, 0x8c],
  CarbonDioxide: [0x5a, 0x5a, 0x5a],
  ChlorineGas:   [0x8c, 0xcc, 0x28],
  ContaminatedOxygen: [0x9c, 0x8c, 0x24],
  NaturalGas:    [0xc0, 0x8c, 0x40],
  Steam:         [0xcc, 0xdc, 0xec],
  Methane:       [0x80, 0x50, 0x70],
  SourGas:       [0xc0, 0xb0, 0x20],
  SuperCoolant:  [0x60, 0xe0, 0xe0],

  // Liquids
  Water:         [0x28, 0x58, 0xc8],
  DirtyWater:    [0x68, 0x58, 0x28],
  Brine:         [0x50, 0x70, 0x80],
  SaltWater:     [0x40, 0x68, 0x90],
  Magma:         [0xff, 0x44, 0x00],
  CrudeOil:      [0x20, 0x20, 0x20],
  Petroleum:     [0x40, 0x38, 0x20],
  Naphtha:       [0xb8, 0x60, 0xc8],
  MoltenIron:    [0xd0, 0x50, 0x20],
  MoltenGold:    [0xff, 0xd7, 0x00],
  MoltenCopper:  [0xd0, 0x70, 0x20],
  LiquidPhosphorus: [0xcc, 0xcc, 0x00],
  ViscoGel:      [0x60, 0xc0, 0x60],
  SuperCoolantLiquid: [0x40, 0xc0, 0xc0],
  Resin:         [0xd4, 0x9a, 0x40],
  Ethanol:       [0xb0, 0xc0, 0xd0],

  // Solids — natural
  SandStone:     [0xc8, 0xa8, 0x68],
  Granite:       [0x88, 0x78, 0x78],
  IgneousRock:   [0x58, 0x58, 0x58],
  SedimentaryRock:[0x90, 0x80, 0x60],
  MaficRock:     [0x48, 0x48, 0x40],
  Obsidian:      [0x30, 0x28, 0x38],
  Dirt:          [0x80, 0x60, 0x30],
  Sand:          [0xd8, 0xc8, 0x80],
  Clay:          [0xa0, 0x60, 0x40],
  Algae:         [0x40, 0x90, 0x28],
  SlimeMold:     [0x60, 0x80, 0x30],
  Oxylite:       [0xa0, 0xc0, 0xe0],
  Fertilizer:    [0x70, 0x50, 0x20],
  Snow:          [0xe8, 0xf0, 0xf8],
  Ice:           [0x90, 0xd0, 0xf0],
  DirtyIce:      [0x80, 0x90, 0x80],
  BrineIce:      [0x70, 0x88, 0x90],
  PollutedIce:   [0x80, 0x90, 0x80],
  Carbon:        [0x2c, 0x2c, 0x2c],
  Diamond:       [0xc0, 0xe8, 0xf8],
  Coal:          [0x30, 0x30, 0x30],
  Phosphorite:   [0xb0, 0xb0, 0x30],
  Sulfur:        [0xd0, 0xd0, 0x20],
  BleachStone:   [0xd0, 0xe0, 0x50],
  Rust:          [0xa0, 0x40, 0x20],
  Salt:          [0xe0, 0xe0, 0xe0],
  Regolith:      [0x98, 0x88, 0x70],

  // Solids — metals / refined
  Iron:          [0x80, 0x80, 0x88],
  IronOre:       [0x88, 0x50, 0x30],
  Copper:        [0xc0, 0x70, 0x30],
  CopperOre:     [0x90, 0x60, 0x40],
  Gold:          [0xff, 0xd7, 0x00],
  GoldAmalgam:   [0xc0, 0xa0, 0x20],
  Wolframite:    [0x50, 0x48, 0x40],
  Tungsten:      [0x70, 0x70, 0x78],
  Steel:         [0x70, 0x78, 0x80],
  Lead:          [0x50, 0x50, 0x60],
  Aluminum:      [0xa0, 0xa8, 0xb0],
  AluminumOre:   [0x88, 0x88, 0x90],
  Niobium:       [0x90, 0x80, 0xa0],
  Cobalt:        [0x40, 0x50, 0xa0],
  CobaltOre:     [0x40, 0x50, 0x80],

  // Tiles / special
  Unobtanium:    [0xff, 0x00, 0xff],
  Neutronium:    [0x44, 0x44, 0x44],
  Katairite:     [0xc0, 0x60, 0x80],
  Abyssalite:    [0x50, 0x40, 0x48],
  TempShift:     [0x60, 0xa0, 0xb0],

  // Vacuum / void
  Vacuum:        [0x2a, 0x2a, 0x30],
  Void:          [0x2a, 0x2a, 0x30],
};

const DEFAULT_COLOR = [0x80, 0x40, 0x80]; // purple for unknown
const FOG_COLOR = [0x1a, 0x10, 0x28];
const BUILDING_OUTLINE = '#ffcc00';
const BUILDING_FILL = 'rgba(255,204,0,0.25)';
const SELECTED_FILL = 'rgba(255,100,100,0.45)';

// -- State --
let db = null;
let timestamps = [];       // sorted sim_time values
let timeIndex = 0;
let gridW = 0, gridH = 0;  // world dimensions in cells
let minX = 0, minY = 0;    // world origin offset

// Terrain cache
let terrainGrid = null;     // Float64Array-backed grid [element_id, mass, temp, revealed] per cell
let terrainElements = [];   // string element names indexed by element_id
let terrainElementMap = {}; // element name -> element_id
let cachedSimTime = -1;     // sim_time of current terrainGrid
let fullSnapshots = [];     // sorted sim_times that have is_full=1

// Current frame data
let buildings = [];
let flows = [];

// Detected worlds (asteroid clusters)
let worlds = []; // [{id, x0, y0, x1, y1, label}] in grid coords

// View
let scale = 4;
let panX = 0, panY = 0;
let selectedBuilding = null;

// DOM
const canvas = document.getElementById('map-canvas');
const ctx = canvas.getContext('2d');
const dropOverlay = document.getElementById('drop-overlay');
const fileInput = document.getElementById('file-input');
const controls = document.getElementById('controls');
const timeSlider = document.getElementById('time-slider');
const timeLabel = document.getElementById('time-label');
const layerTerrain = document.getElementById('layer-terrain');
const layerBuildings = document.getElementById('layer-buildings');
const layerFog = document.getElementById('layer-fog');
const colorMode = document.getElementById('color-mode');
const infoPanel = document.getElementById('info-panel');
const worldSelect = document.getElementById('world-select');
const infoTitle = document.getElementById('info-title');
const infoBody = document.getElementById('info-body');

// Offscreen canvas for terrain (1 cell = 1 pixel)
let offscreen = null;
let offCtx = null;

// -- File loading --
dropOverlay.addEventListener('click', () => fileInput.click());
dropOverlay.addEventListener('dragover', e => { e.preventDefault(); dropOverlay.classList.add('drag-over'); });
dropOverlay.addEventListener('dragleave', () => dropOverlay.classList.remove('drag-over'));
dropOverlay.addEventListener('drop', e => {
  e.preventDefault();
  dropOverlay.classList.remove('drag-over');
  if (e.dataTransfer.files.length) loadFile(e.dataTransfer.files[0]);
});
fileInput.addEventListener('change', () => { if (fileInput.files.length) loadFile(fileInput.files[0]); });

async function loadFile(file) {
  const buf = await file.arrayBuffer();
  const SQL = await initSqlJs({ locateFile: f => `https://cdnjs.cloudflare.com/ajax/libs/sql.js/1.10.3/${f}` });
  db = new SQL.Database(new Uint8Array(buf));

  loadTimestamps();
  loadWorldBounds();
  initOffscreen();

  dropOverlay.hidden = true;
  controls.hidden = false;

  timeSlider.max = timestamps.length - 1;
  timeSlider.value = 0;
  timeIndex = 0;

  loadFullSnapshotIndex();
  detectWorlds();
  goToTime(0);
  zoomToWorld('all');
}

function loadTimestamps() {
  const res = db.exec("SELECT DISTINCT sim_time FROM buildings UNION SELECT DISTINCT sim_time FROM terrain ORDER BY sim_time");
  timestamps = res.length ? res[0].values.map(r => r[0]) : [];
}

function loadWorldBounds() {
  // Determine grid bounds from terrain data
  const res = db.exec("SELECT MIN(cell_x), MAX(cell_x), MIN(cell_y), MAX(cell_y) FROM terrain");
  if (!res.length || res[0].values[0][0] === null) {
    // fallback: try buildings
    const res2 = db.exec("SELECT MIN(cell_x), MAX(cell_x), MIN(cell_y), MAX(cell_y) FROM buildings");
    if (res2.length && res2[0].values[0][0] !== null) {
      const [x0, x1, y0, y1] = res2[0].values[0];
      minX = x0; minY = y0;
      gridW = x1 - x0 + 10;
      gridH = y1 - y0 + 10;
    } else {
      gridW = 256; gridH = 256; minX = 0; minY = 0;
    }
    return;
  }
  const [x0, x1, y0, y1] = res[0].values[0];
  minX = x0; minY = y0;
  gridW = x1 - x0 + 1;
  gridH = y1 - y0 + 1;
}

function initOffscreen() {
  offscreen = document.createElement('canvas');
  offscreen.width = gridW;
  offscreen.height = gridH;
  offCtx = offscreen.getContext('2d');
}

function loadFullSnapshotIndex() {
  const res = db.exec("SELECT DISTINCT sim_time FROM terrain WHERE is_full = 1 ORDER BY sim_time");
  fullSnapshots = res.length ? res[0].values.map(r => r[0]) : [];
}

// -- World detection --
// Detect asteroid clusters by scanning terrain columns for non-vacuum strips.
// ONI worlds are rectangular and separated by large vacuum gaps, so we find
// bounding boxes of contiguous non-vacuum regions.
function detectWorlds() {
  worlds = [];
  // Query bounding boxes: group terrain cells into rectangular clusters
  // Use the first full snapshot to detect worlds (most complete data)
  const snapTime = fullSnapshots.length ? fullSnapshots[0] : (timestamps.length ? timestamps[0] : null);
  if (snapTime === null) return;

  // Get all occupied cells at this timestamp
  const stmt = db.prepare(
    "SELECT DISTINCT cell_x, cell_y FROM terrain WHERE sim_time = ? AND element != 'Vacuum'"
  );
  stmt.bind([snapTime]);
  const cells = new Set();
  while (stmt.step()) {
    const [cx, cy] = stmt.get();
    const gx = cx - minX;
    const gy = cy - minY;
    cells.add(gy * gridW + gx);
  }
  stmt.free();

  if (cells.size === 0) return;

  // Flood-fill to find connected clusters (4-connected, with gap tolerance
  // of 2 cells to bridge small vacuum pockets inside asteroids)
  const visited = new Set();
  const GAP = 3; // bridge small gaps within an asteroid

  for (const startIdx of cells) {
    if (visited.has(startIdx)) continue;

    let bx0 = startIdx % gridW, by0 = Math.floor(startIdx / gridW);
    let bx1 = bx0, by1 = by0;
    const queue = [startIdx];
    visited.add(startIdx);

    while (queue.length) {
      const idx = queue.pop();
      const x = idx % gridW;
      const y = Math.floor(idx / gridW);
      bx0 = Math.min(bx0, x); by0 = Math.min(by0, y);
      bx1 = Math.max(bx1, x); by1 = Math.max(by1, y);

      // Check neighbors within GAP radius
      for (let dy = -GAP; dy <= GAP; dy++) {
        for (let dx = -GAP; dx <= GAP; dx++) {
          if (dx === 0 && dy === 0) continue;
          const nx = x + dx, ny = y + dy;
          if (nx < 0 || ny < 0 || nx >= gridW || ny >= gridH) continue;
          const ni = ny * gridW + nx;
          if (visited.has(ni) || !cells.has(ni)) continue;
          visited.add(ni);
          queue.push(ni);
        }
      }
    }

    worlds.push({ x0: bx0, y0: by0, x1: bx1, y1: by1 });
  }

  // Sort by size descending (main asteroid first)
  worlds.sort((a, b) => ((b.x1 - b.x0) * (b.y1 - b.y0)) - ((a.x1 - a.x0) * (a.y1 - a.y0)));

  // Label them
  worlds.forEach((w, i) => {
    w.id = i;
    const width = w.x1 - w.x0 + 1;
    const height = w.y1 - w.y0 + 1;
    w.label = i === 0 ? `Main Asteroid (${width}x${height})` : `Asteroid ${i + 1} (${width}x${height})`;
  });

  // Populate dropdown
  worldSelect.innerHTML = '<option value="all">All (System View)</option>';
  for (const w of worlds) {
    const opt = document.createElement('option');
    opt.value = w.id;
    opt.textContent = w.label;
    worldSelect.appendChild(opt);
  }
}

function zoomToWorld(id) {
  let x0, y0, x1, y1;
  if (id === 'all') {
    x0 = 0; y0 = 0; x1 = gridW - 1; y1 = gridH - 1;
  } else {
    const w = worlds[id];
    if (!w) return;
    // Add a small margin
    const margin = 5;
    x0 = Math.max(0, w.x0 - margin);
    y0 = Math.max(0, w.y0 - margin);
    x1 = Math.min(gridW - 1, w.x1 + margin);
    y1 = Math.min(gridH - 1, w.y1 + margin);
  }

  const regionW = x1 - x0 + 1;
  const regionH = y1 - y0 + 1;

  // Fit to viewport with some padding
  const pad = 40;
  const scaleX = (canvas.width - pad * 2) / regionW;
  const scaleY = (canvas.height - pad * 2) / regionH;
  scale = Math.min(scaleX, scaleY);
  scale = Math.max(0.5, Math.min(64, scale));

  // Center the region (flip Y: grid y0 = bottom → screen top)
  const screenX0 = (canvas.width - regionW * scale) / 2;
  const screenY0 = (canvas.height - regionH * scale) / 2;
  panX = screenX0 - x0 * scale;
  // y0 in grid = bottom of region, which maps to top of flipped screen
  panY = screenY0 - (gridH - 1 - y1) * scale;

  render();
}

worldSelect.addEventListener('change', () => {
  const v = worldSelect.value;
  zoomToWorld(v === 'all' ? 'all' : parseInt(v));
});

// -- Terrain reconstruction --
// Grid stores per cell: elementId (int), mass, temperature, revealed
// Packed as 4 values per cell in a flat Float64Array
function allocGrid() {
  terrainGrid = new Float64Array(gridW * gridH * 4);
  terrainElements = ['Vacuum'];
  terrainElementMap = { Vacuum: 0 };
}

function elementId(name) {
  if (name in terrainElementMap) return terrainElementMap[name];
  const id = terrainElements.length;
  terrainElements.push(name);
  terrainElementMap[name] = id;
  return id;
}

function setCell(cx, cy, elemName, mass, temp, revealed) {
  const gx = cx - minX;
  const gy = cy - minY;
  if (gx < 0 || gy < 0 || gx >= gridW || gy >= gridH) return;
  const i = (gy * gridW + gx) * 4;
  terrainGrid[i]     = elementId(elemName);
  terrainGrid[i + 1] = mass;
  terrainGrid[i + 2] = temp;
  terrainGrid[i + 3] = revealed;
}

function getCell(gx, gy) {
  if (gx < 0 || gy < 0 || gx >= gridW || gy >= gridH) return null;
  const i = (gy * gridW + gx) * 4;
  return {
    element: terrainElements[terrainGrid[i]],
    mass: terrainGrid[i + 1],
    temperature: terrainGrid[i + 2],
    revealed: terrainGrid[i + 3],
  };
}

function reconstructTerrain(simTime) {
  // Find the most recent full snapshot at or before simTime
  let fullTime = null;
  for (let i = fullSnapshots.length - 1; i >= 0; i--) {
    if (fullSnapshots[i] <= simTime) { fullTime = fullSnapshots[i]; break; }
  }

  if (fullTime === null) {
    // No full snapshot yet — try loading whatever terrain is available
    allocGrid();
    const stmt = db.prepare("SELECT cell_x, cell_y, element, mass, temperature, revealed FROM terrain WHERE sim_time <= ? ORDER BY sim_time");
    stmt.bind([simTime]);
    while (stmt.step()) {
      const [cx, cy, elem, mass, temp, rev] = stmt.get();
      setCell(cx, cy, elem, mass, temp, rev);
    }
    stmt.free();
    cachedSimTime = simTime;
    return;
  }

  // Check if we can incrementally update from cachedSimTime
  if (terrainGrid && cachedSimTime >= fullTime && cachedSimTime < simTime) {
    // Apply only new deltas from cachedSimTime to simTime
    const stmt = db.prepare("SELECT cell_x, cell_y, element, mass, temperature, revealed FROM terrain WHERE sim_time > ? AND sim_time <= ?");
    stmt.bind([cachedSimTime, simTime]);
    while (stmt.step()) {
      const [cx, cy, elem, mass, temp, rev] = stmt.get();
      setCell(cx, cy, elem, mass, temp, rev);
    }
    stmt.free();
    cachedSimTime = simTime;
    return;
  }

  // Full rebuild: load snapshot then apply deltas
  allocGrid();
  const stmt = db.prepare("SELECT cell_x, cell_y, element, mass, temperature, revealed FROM terrain WHERE sim_time = ? AND is_full = 1");
  stmt.bind([fullTime]);
  while (stmt.step()) {
    const [cx, cy, elem, mass, temp, rev] = stmt.get();
    setCell(cx, cy, elem, mass, temp, rev);
  }
  stmt.free();

  // Apply deltas between fullTime and simTime
  if (simTime > fullTime) {
    const stmt2 = db.prepare("SELECT cell_x, cell_y, element, mass, temperature, revealed FROM terrain WHERE sim_time > ? AND sim_time <= ?");
    stmt2.bind([fullTime, simTime]);
    while (stmt2.step()) {
      const [cx, cy, elem, mass, temp, rev] = stmt2.get();
      setCell(cx, cy, elem, mass, temp, rev);
    }
    stmt2.free();
  }
  cachedSimTime = simTime;
}

// -- Data queries --
function loadBuildings(simTime) {
  const stmt = db.prepare("SELECT prefab_id, name, cell_x, cell_y, width, height, revealed FROM buildings WHERE sim_time = ?");
  stmt.bind([simTime]);
  buildings = [];
  while (stmt.step()) {
    const [prefab, name, cx, cy, w, h, rev] = stmt.get();
    buildings.push({ prefab, name, cx, cy, w, h, revealed: rev });
  }
  stmt.free();
}

function loadFlows(simTime) {
  const stmt = db.prepare("SELECT prefab_id, cell_x, cell_y, resource, amount, direction, category FROM resource_flows WHERE sim_time = ?");
  stmt.bind([simTime]);
  flows = [];
  while (stmt.step()) {
    const [prefab, cx, cy, resource, amount, dir, cat] = stmt.get();
    flows.push({ prefab, cx, cy, resource, amount, direction: dir, category: cat });
  }
  stmt.free();
}

// -- Rendering --
function resizeCanvas() {
  canvas.width = window.innerWidth;
  canvas.height = window.innerHeight;
}
window.addEventListener('resize', () => { resizeCanvas(); render(); });
resizeCanvas();

function render() {
  if (!db) return;

  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.fillStyle = '#111';
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  const mode = colorMode.value;

  // Draw terrain to offscreen canvas
  if (layerTerrain.checked && terrainGrid) {
    const imgData = offCtx.createImageData(gridW, gridH);
    const d = imgData.data;

    // Find temp/mass range for heatmaps
    let minTemp = Infinity, maxTemp = -Infinity;
    let minMass = Infinity, maxMass = -Infinity;
    if (mode === 'temperature' || mode === 'mass') {
      for (let i = 0; i < gridW * gridH; i++) {
        const t = terrainGrid[i * 4 + 2];
        const m = terrainGrid[i * 4 + 1];
        if (t > 0) { minTemp = Math.min(minTemp, t); maxTemp = Math.max(maxTemp, t); }
        if (m > 0) { minMass = Math.min(minMass, m); maxMass = Math.max(maxMass, m); }
      }
      if (minTemp === Infinity) { minTemp = 0; maxTemp = 1; }
      if (minMass === Infinity) { minMass = 0; maxMass = 1; }
    }

    for (let gy = 0; gy < gridH; gy++) {
      for (let gx = 0; gx < gridW; gx++) {
        const gi = (gy * gridW + gx) * 4;
        const elemIdx = terrainGrid[gi];
        const mass = terrainGrid[gi + 1];
        const temp = terrainGrid[gi + 2];
        const revealed = terrainGrid[gi + 3];

        let r, g, b;
        if (mode === 'element') {
          const name = terrainElements[elemIdx];
          const c = ELEMENT_COLORS[name] || DEFAULT_COLOR;
          r = c[0]; g = c[1]; b = c[2];
        } else if (mode === 'temperature') {
          const t = (maxTemp > minTemp) ? (temp - minTemp) / (maxTemp - minTemp) : 0;
          [r, g, b] = heatmapColor(t);
        } else { // mass
          const t = (maxMass > minMass) ? Math.log1p(mass - minMass) / Math.log1p(maxMass - minMass) : 0;
          [r, g, b] = heatmapColor(t);
        }

        // Fog dims unrevealed cells
        if (layerFog.checked && !revealed) {
          r = FOG_COLOR[0]; g = FOG_COLOR[1]; b = FOG_COLOR[2];
        }

        // Offscreen canvas has Y=0 at top, but ONI has Y=0 at bottom → flip
        const pi = ((gridH - 1 - gy) * gridW + gx) * 4;
        d[pi] = r; d[pi + 1] = g; d[pi + 2] = b; d[pi + 3] = 255;
      }
    }
    offCtx.putImageData(imgData, 0, 0);

    // Draw offscreen to main canvas scaled up with nearest-neighbor
    ctx.imageSmoothingEnabled = false;
    ctx.drawImage(offscreen, panX, panY, gridW * scale, gridH * scale);
  }

  // Draw buildings on the display canvas (so outlines stay sharp)
  if (layerBuildings.checked) {
    for (const b of buildings) {
      if (layerFog.checked && !b.revealed) continue;
      const gx = b.cx - minX;
      // flip Y: ONI y=0 bottom, canvas y=0 top
      const gy = gridH - 1 - (b.cy - minY) - (b.h - 1);
      const sx = panX + gx * scale;
      const sy = panY + gy * scale;
      const sw = b.w * scale;
      const sh = b.h * scale;

      const isSelected = selectedBuilding && selectedBuilding.cx === b.cx && selectedBuilding.cy === b.cy && selectedBuilding.prefab === b.prefab;
      ctx.fillStyle = isSelected ? SELECTED_FILL : BUILDING_FILL;
      ctx.fillRect(sx, sy, sw, sh);
      ctx.strokeStyle = BUILDING_OUTLINE;
      ctx.lineWidth = 1;
      ctx.strokeRect(sx + 0.5, sy + 0.5, sw - 1, sh - 1);

      // Label if zoomed in enough
      if (scale >= 8) {
        ctx.fillStyle = '#fff';
        ctx.font = `${Math.min(scale * 0.8, 14)}px monospace`;
        ctx.fillText(b.name, sx + 2, sy + scale - 2);
      }
    }
  }

  // Draw world outlines and labels (always, regardless of fog)
  for (const w of worlds) {
    const sx = panX + w.x0 * scale;
    const sy = panY + (gridH - 1 - w.y1) * scale;
    const sw = (w.x1 - w.x0 + 1) * scale;
    const sh = (w.y1 - w.y0 + 1) * scale;

    ctx.strokeStyle = 'rgba(100,180,255,0.4)';
    ctx.lineWidth = 1;
    ctx.setLineDash([6, 4]);
    ctx.strokeRect(sx, sy, sw, sh);
    ctx.setLineDash([]);

    ctx.fillStyle = 'rgba(100,180,255,0.7)';
    ctx.font = '12px monospace';
    ctx.fillText(w.label, sx + 4, sy - 4);
  }
}

function heatmapColor(t) {
  // Blue (cold) → Cyan → Green → Yellow → Red (hot)
  t = Math.max(0, Math.min(1, t));
  if (t < 0.25) {
    const s = t / 0.25;
    return [0, Math.round(s * 255), 255];
  } else if (t < 0.5) {
    const s = (t - 0.25) / 0.25;
    return [0, 255, Math.round((1 - s) * 255)];
  } else if (t < 0.75) {
    const s = (t - 0.5) / 0.25;
    return [Math.round(s * 255), 255, 0];
  } else {
    const s = (t - 0.75) / 0.25;
    return [255, Math.round((1 - s) * 255), 0];
  }
}

// -- Time navigation --
function goToTime(index) {
  timeIndex = index;
  const simTime = timestamps[timeIndex];
  const cycle = Math.floor(simTime / 600);
  const pct = ((simTime % 600) / 600 * 100).toFixed(0);
  timeLabel.textContent = `Cycle ${cycle} (${pct}%)`;

  reconstructTerrain(simTime);
  loadBuildings(simTime);
  loadFlows(simTime);

  if (selectedBuilding) updateInfoPanel();
  render();
}

timeSlider.addEventListener('input', () => goToTime(parseInt(timeSlider.value)));
layerTerrain.addEventListener('change', render);
layerBuildings.addEventListener('change', render);
layerFog.addEventListener('change', render);
colorMode.addEventListener('change', render);

// -- Interaction: zoom / pan --
let dragging = false;
let dragStartX, dragStartY;

canvas.addEventListener('wheel', e => {
  e.preventDefault();
  const oldScale = scale;
  // Zoom centered on mouse position
  const rect = canvas.getBoundingClientRect();
  const mx = e.clientX - rect.left;
  const my = e.clientY - rect.top;

  if (e.deltaY < 0) scale = Math.min(scale * 1.2, 64);
  else scale = Math.max(scale / 1.2, 0.5);

  // Adjust pan so the point under the mouse stays fixed
  panX = mx - (mx - panX) * (scale / oldScale);
  panY = my - (my - panY) * (scale / oldScale);
  render();
}, { passive: false });

canvas.addEventListener('mousedown', e => {
  dragging = true;
  dragStartX = e.clientX - panX;
  dragStartY = e.clientY - panY;
});

canvas.addEventListener('mousemove', e => {
  if (!dragging) return;
  panX = e.clientX - dragStartX;
  panY = e.clientY - dragStartY;
  render();
});

canvas.addEventListener('mouseup', e => {
  if (!dragging) return;
  dragging = false;

  // If barely moved, treat as click
  const dx = e.clientX - (dragStartX + panX);
  const dy = e.clientY - (dragStartY + panY);
  if (Math.abs(dx) < 3 && Math.abs(dy) < 3) {
    handleClick(e);
  }
});

function handleClick(e) {
  const rect = canvas.getBoundingClientRect();
  const mx = e.clientX - rect.left;
  const my = e.clientY - rect.top;

  // Convert screen coords to grid coords
  const gx = (mx - panX) / scale;
  const gy = (my - panY) / scale;

  // Convert to world coords (flip Y)
  const wx = Math.floor(gx) + minX;
  const wy = (gridH - 1 - Math.floor(gy)) + minY;

  // Find a building at this position
  selectedBuilding = null;
  for (const b of buildings) {
    if (wx >= b.cx && wx < b.cx + b.w && wy >= b.cy && wy < b.cy + b.h) {
      selectedBuilding = b;
      break;
    }
  }

  if (selectedBuilding) {
    updateInfoPanel();
    infoPanel.hidden = false;
  } else {
    infoPanel.hidden = true;
  }
  render();
}

function updateInfoPanel() {
  const b = selectedBuilding;
  infoTitle.textContent = `${b.name} (${b.cx}, ${b.cy})`;

  // Find flows for this building
  const bFlows = flows.filter(f => f.cx === b.cx && f.cy === b.cy && f.prefab === b.prefab);

  if (!bFlows.length) {
    infoBody.innerHTML = '<p style="color:#888">No resource flows at this time</p>';
    return;
  }

  let html = '<table>';
  for (const f of bFlows) {
    const cls = f.direction === 'consumed' ? 'flow-consumed' : 'flow-produced';
    const sign = f.direction === 'consumed' ? '-' : '+';
    const amt = f.amount >= 1000 ? (f.amount / 1000).toFixed(2) + ' t' : f.amount.toFixed(1) + ' kg';
    html += `<tr class="${cls}"><td>${f.resource}</td><td>${sign}${amt}</td><td>${f.category}</td></tr>`;
  }
  html += '</table>';
  infoBody.innerHTML = html;
}
