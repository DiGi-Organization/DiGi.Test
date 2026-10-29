namespace DiGi.PRG.Geometry.xUnit
{
    public partial class Facts
    {
        /// <summary>
        /// The review page written by <see cref="EntranceFacadeControlSample_Draw"/>; <c>__SAMPLE__</c> is replaced by the sample JSON.
        /// <para>A self-contained local page: each building is drawn north up with numbered edges (edge <c>i</c> from ring point <c>i</c> to <c>i + 1</c>, the numbering of the sample); clicking an edge marks it as the main entrance, <c>Unknown</c> marks a building whose entrance cannot be seen. Labels persist in the browser's local storage and are exported as <c>EntranceFacadeControlSample_Labels.json</c> (<c>{ Index: edge }</c>, -1 for unknown), which is merged into the sample before it is committed to <c>DiGi.Test/files/</c>. Roads and rule results are deliberately not shown.</para>
        /// </summary>
        private const string entranceFacadeControlSample_ReviewPage = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Entrance Facade Labelling</title>
<style>
  :root { --bg: #f6f6f4; --card: #ffffff; --ink: #1d1d1b; --muted: #6b6b66; --line: #d9d9d4; --edge: #3a5a8c; --pick: #c0392b; --fill: #e8edf5; }
  * { box-sizing: border-box; }
  body { margin: 0; background: var(--bg); color: var(--ink); font: 14px/1.45 system-ui, sans-serif; }
  header { position: sticky; top: 0; z-index: 2; background: var(--card); border-bottom: 1px solid var(--line); padding: 10px 16px; display: flex; flex-wrap: wrap; gap: 10px 16px; align-items: center; }
  header h1 { font-size: 16px; margin: 0; }
  header .progress { font-variant-numeric: tabular-nums; color: var(--muted); }
  main { padding: 16px; display: grid; grid-template-columns: repeat(auto-fill, minmax(340px, 1fr)); gap: 16px; }
  .intro { grid-column: 1 / -1; color: var(--muted); margin: 0; }
  .card { background: var(--card); border: 1px solid var(--line); border-radius: 8px; padding: 12px; }
  .card.done { border-color: #7aa874; }
  .card.unknown { border-color: #c9a94a; }
  .card h2 { font-size: 14px; margin: 0 0 4px; }
  .meta, .addr { color: var(--muted); font-size: 12px; margin: 0 0 6px; }
  svg { width: 100%; height: 300px; background: #fbfbfa; border: 1px solid var(--line); border-radius: 6px; }
  .edge { stroke: var(--edge); stroke-width: 2; }
  .hit { stroke: transparent; stroke-width: 14; cursor: pointer; }
  .hit:hover + .edge { stroke-width: 4; }
  .edge.picked { stroke: var(--pick); stroke-width: 5; }
  .num { font-size: 11px; fill: var(--edge); text-anchor: middle; dominant-baseline: middle; pointer-events: none; }
  .num.picked { fill: var(--pick); font-weight: 700; }
  .row { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-top: 8px; }
  .status { font-weight: 600; }
  button, input[type=text] { font: inherit; padding: 4px 10px; border: 1px solid var(--line); border-radius: 6px; background: var(--card); color: var(--ink); }
  button { cursor: pointer; }
  a { color: var(--edge); }
  .hidden { display: none; }
</style>
</head>
<body>
<header>
  <h1>Entrance facade labelling</h1>
  <span class="progress" id="progress"></span>
  <label>Labeller <input type="text" id="labeller" size="12"></label>
  <label><input type="checkbox" id="onlyOpen"> Only unlabelled</label>
  <button id="export">Export labels</button>
  <label><button id="importButton" type="button">Import labels</button><input type="file" id="import" accept=".json" class="hidden"></label>
</header>
<main id="main">
  <p class="intro">For each building, click the edge of the footprint that holds the <b>main entrance</b>, using the satellite and Street View links. Use <b>Unknown</b> when the entrance cannot be seen. North is up; red dots are the PRG address points. Labels are kept in this browser; export them when done.</p>
</main>
<script>
const sample = __SAMPLE__;
const key = "EntranceFacadeControlSample_" + sample.Seed;
let state = { labels: {}, labeller: "" };
try { state = Object.assign(state, JSON.parse(localStorage.getItem(key) || "{}")); } catch (e) { }
const save = () => { try { localStorage.setItem(key, JSON.stringify(state)); } catch (e) { } };
const ns = "http://www.w3.org/2000/svg";
const main = document.getElementById("main");
const labeller = document.getElementById("labeller");
labeller.value = state.labeller || "";
labeller.addEventListener("input", () => { state.labeller = labeller.value; save(); });

function el(tag, attributes, parent) {
  const node = document.createElementNS(ns, tag);
  for (const name in attributes) node.setAttribute(name, attributes[name]);
  if (parent) parent.appendChild(node);
  return node;
}

function draw(building, svg) {
  svg.innerHTML = "";
  const ring = building.Ring;
  const points = ring.concat(building.AddressPoints.map(p => [p.X, p.Y]));
  const xs = points.map(p => p[0]), ys = points.map(p => p[1]);
  const minX = Math.min(...xs), maxX = Math.max(...xs), minY = Math.min(...ys), maxY = Math.max(...ys);
  const size = Math.max(maxX - minX, maxY - minY, 10) * 1.3;
  const cx = (minX + maxX) / 2, cy = (minY + maxY) / 2;
  const scale = 280 / size;
  const sx = x => 150 + (x - cx) * scale;
  const sy = y => 150 - (y - cy) * scale;
  el("polygon", { points: ring.map(p => sx(p[0]) + "," + sy(p[1])).join(" "), fill: "var(--fill)" }, svg);
  const picked = state.labels[building.Index];
  ring.forEach((p, i) => {
    const q = ring[(i + 1) % ring.length];
    const hit = el("line", { x1: sx(p[0]), y1: sy(p[1]), x2: sx(q[0]), y2: sy(q[1]), class: "hit" }, svg);
    el("line", { x1: sx(p[0]), y1: sy(p[1]), x2: sx(q[0]), y2: sy(q[1]), class: "edge" + (picked === i ? " picked" : "") }, svg);
    hit.addEventListener("click", () => setLabel(building.Index, i));
    const mx = (p[0] + q[0]) / 2, my = (p[1] + q[1]) / 2;
    const dx = q[0] - p[0], dy = q[1] - p[1], length = Math.hypot(dx, dy) || 1;
    // Counter-clockwise ring: the outward normal is (dy, -dx)
    const ox = dy / length, oy = -dx / length;
    const text = el("text", { x: sx(mx) + ox * 12, y: sy(my) - oy * 12, class: "num" + (picked === i ? " picked" : "") }, svg);
    text.textContent = i;
  });
  building.AddressPoints.forEach(p => el("circle", { cx: sx(p.X), cy: sy(p.Y), r: 4, fill: "#c0392b" }, svg));
  const north = el("text", { x: 285, y: 18, class: "num" }, svg);
  north.textContent = "N";
  const bar = 10 * scale;
  el("line", { x1: 10, y1: 290, x2: 10 + bar, y2: 290, stroke: "#6b6b66", "stroke-width": 2 }, svg);
  const barText = el("text", { x: 10 + bar / 2, y: 280, class: "num" }, svg);
  barText.textContent = "10 m";
}

function setLabel(index, edge) {
  if (edge === null) delete state.labels[index]; else state.labels[index] = edge;
  save();
  render();
}

const cards = sample.Buildings.map(building => {
  const card = document.createElement("section");
  card.className = "card";
  const first = building.AddressPoints[0];
  const address = building.AddressPoints.map(p => (p.Street || p.Locality) + " " + p.HouseNumber + (p.Street ? ", " + p.Locality : "")).join("; ");
  card.innerHTML = "<h2>#" + building.Index + " · " + building.Stratum + "</h2>" +
    "<p class='meta'>County " + building.CountyCode + " · " + building.Function + (building.Storeys ? " · " + building.Storeys + " storeys" : "") + "</p>" +
    "<p class='addr'></p>";
  card.querySelector(".addr").textContent = address;
  const svg = el("svg", { viewBox: "0 0 300 300", role: "img", "aria-label": "Footprint of building " + building.Index }, card);
  const row = document.createElement("div");
  row.className = "row";
  row.innerHTML = "<a target='_blank' rel='noopener' href='https://www.google.com/maps/@" + building.Latitude + "," + building.Longitude + ",45m/data=!3m1!1e3'>Satellite</a>" +
    "<a target='_blank' rel='noopener' href='https://www.google.com/maps/@?api=1&map_action=pano&viewpoint=" + first.Latitude + "," + first.Longitude + "'>Street View</a>" +
    "<button type='button' data-unknown>Unknown</button><button type='button' data-clear>Clear</button><span class='status'></span>";
  row.querySelector("[data-unknown]").addEventListener("click", () => setLabel(building.Index, -1));
  row.querySelector("[data-clear]").addEventListener("click", () => setLabel(building.Index, null));
  card.appendChild(row);
  main.appendChild(card);
  return { building, card, svg, status: row.querySelector(".status") };
});

function render() {
  const onlyOpen = document.getElementById("onlyOpen").checked;
  let done = 0;
  cards.forEach(({ building, card, svg, status }) => {
    const label = state.labels[building.Index];
    const has = label !== undefined;
    if (has) done++;
    card.classList.toggle("done", has && label >= 0);
    card.classList.toggle("unknown", label === -1);
    card.classList.toggle("hidden", onlyOpen && has);
    status.textContent = !has ? "Unlabelled" : label === -1 ? "Unknown" : "Entrance: edge " + label;
    draw(building, svg);
  });
  document.getElementById("progress").textContent = done + " / " + cards.length + " labelled";
}

document.getElementById("onlyOpen").addEventListener("change", render);
document.getElementById("export").addEventListener("click", () => {
  const output = { Seed: sample.Seed, Labeller: state.labeller || null, LabelDate: new Date().toISOString().slice(0, 10), Labels: state.labels };
  const link = document.createElement("a");
  link.href = URL.createObjectURL(new Blob([JSON.stringify(output, null, 1)], { type: "application/json" }));
  link.download = "EntranceFacadeControlSample_Labels.json";
  link.click();
});
document.getElementById("importButton").addEventListener("click", () => document.getElementById("import").click());
document.getElementById("import").addEventListener("change", event => {
  const file = event.target.files[0];
  if (!file) return;
  file.text().then(text => {
    const input = JSON.parse(text);
    if (input.Seed !== sample.Seed) { alert("These labels belong to another sample (seed " + input.Seed + ")."); return; }
    state.labels = input.Labels || {};
    if (input.Labeller) { state.labeller = input.Labeller; labeller.value = input.Labeller; }
    save();
    render();
  });
});
render();
</script>
</body>
</html>
""";
    }
}
