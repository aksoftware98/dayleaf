// ═══════════════════════════════════════════════════════════════════════════
// Dayleaf canvas engine. Every pointer interaction happens here; .NET is
// called once on pointerup with the final transform (plan §7).
// Coordinates are A4 CSS px (794 × 1123), independent of zoom.
// ═══════════════════════════════════════════════════════════════════════════

const SHEET_W = 794;
const SHEET_H = 1123;
const KEEP = 24;

let ed = null;        // editor state
let listKeys = null;  // timeline keyboard handler

// ── helpers ────────────────────────────────────────────────────────────────
const num = (v, d = 0) => { const n = parseFloat(v); return Number.isFinite(n) ? n : d; };
const round1 = v => Math.round(v * 10) / 10;
const isRtl = () => document.documentElement.dir === 'rtl';
const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));

function isTyping(target) {
  const t = target || document.activeElement;
  if (!t) return false;
  const tag = t.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || t.isContentEditable;
}

function readBox(el) {
  const m = /rotate\((-?[\d.]+)deg\)/.exec(el.style.transform || '');
  return {
    x: num(el.style.left), y: num(el.style.top),
    w: num(el.style.width), h: num(el.style.height),
    rot: m ? num(m[1]) : 0,
  };
}

function writeBox(el, b) {
  el.style.left = `${round1(b.x)}px`;
  el.style.top = `${round1(b.y)}px`;
  el.style.width = `${round1(b.w)}px`;
  el.style.height = `${round1(b.h)}px`;
  el.style.transform = `rotate(${round1(b.rot)}deg)`;
  const badge = el.querySelector(':scope > .hdl-badge');
  if (badge) badge.textContent = `${Math.round(b.w)} × ${Math.round(b.h)} · ${Math.round(b.rot)}°`;
  if (el.dataset.kind === 'emoji') {
    const e = el.querySelector('.emoji-glyph');
    if (e) e.style.fontSize = `${Math.min(b.w, b.h) * 0.84}px`;
  }
}

function sheetScale(sheet) {
  const r = sheet.getBoundingClientRect();
  return r.width / SHEET_W || 1;
}

/** Which sheet is under a client point, and where on it (A4 px). */
function hitSheet(root, clientX, clientY) {
  for (const s of root.querySelectorAll('.sheet')) {
    const r = s.getBoundingClientRect();
    if (clientX >= r.left && clientX <= r.right && clientY >= r.top && clientY <= r.bottom) {
      const sc = r.width / SHEET_W;
      return { sheet: s, index: num(s.dataset.sheet), x: (clientX - r.left) / sc, y: (clientY - r.top) / sc };
    }
  }
  return null;
}

function clampBox(b) {
  b.w = clamp(b.w, 12, SHEET_W * 1.5);
  b.h = clamp(b.h, 8, SHEET_H * 1.5);
  b.x = clamp(b.x, KEEP - b.w, SHEET_W - KEEP);
  b.y = clamp(b.y, KEEP - b.h, SHEET_H - KEEP);
  return b;
}

const call = (method, ...args) => {
  if (!ed) return Promise.resolve();
  return ed.ref.invokeMethodAsync(method, ...args).catch(err => console.warn('[dayleaf]', method, err));
};

// ── editor ─────────────────────────────────────────────────────────────────
export function initEditor(root, dotNetRef, canvasCol, fileInput) {
  disposeEditor();
  ed = { root, ref: dotNetRef, canvasCol, fileInput, drag: null, editing: null, swallowClick: false, zoom: 1, manualZoom: false, ro: null };

  root.addEventListener('pointerdown', onPointerDown);
  root.addEventListener('pointermove', onPointerMove);
  root.addEventListener('pointerup', onPointerUp);
  root.addEventListener('pointercancel', onPointerUp);
  root.addEventListener('dblclick', onDblClick);
  root.addEventListener('click', onClickCapture, true);
  document.addEventListener('keydown', onKeyDown);
  document.addEventListener('paste', onPaste);
  document.addEventListener('dragstart', onStickerDragStart);
  canvasCol.addEventListener('dragover', onDragOver);
  canvasCol.addEventListener('dragleave', onDragLeave);
  canvasCol.addEventListener('drop', onDrop);

  ed.ro = new ResizeObserver(() => { if (!ed.manualZoom) fit(); });
  ed.ro.observe(canvasCol);
  fit();
  return Math.round(ed.zoom * 100);
}

export function disposeEditor() {
  if (!ed) return;
  const { root, canvasCol } = ed;
  root.removeEventListener('pointerdown', onPointerDown);
  root.removeEventListener('pointermove', onPointerMove);
  root.removeEventListener('pointerup', onPointerUp);
  root.removeEventListener('pointercancel', onPointerUp);
  root.removeEventListener('dblclick', onDblClick);
  root.removeEventListener('click', onClickCapture, true);
  document.removeEventListener('keydown', onKeyDown);
  document.removeEventListener('paste', onPaste);
  document.removeEventListener('dragstart', onStickerDragStart);
  canvasCol.removeEventListener('dragover', onDragOver);
  canvasCol.removeEventListener('dragleave', onDragLeave);
  canvasCol.removeEventListener('drop', onDrop);
  ed.ro?.disconnect();
  ed = null;
}

// ── zoom ───────────────────────────────────────────────────────────────────
function applyZoom(z) {
  if (!ed) return 100;
  ed.zoom = clamp(z, 0.3, 2);
  ed.root.style.setProperty('--zoom', ed.zoom.toFixed(3));
  return Math.round(ed.zoom * 100);
}

function fit() {
  if (!ed) return 100;
  const avail = ed.canvasCol.clientWidth - 48;
  return applyZoom(clamp(avail / SHEET_W, 0.3, 1));
}

export function setZoom(mode) {
  if (!ed) return 100;
  if (mode === 'fit') { ed.manualZoom = false; return fit(); }
  ed.manualZoom = true;
  const steps = [0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1, 1.15, 1.3, 1.5, 1.75, 2];
  const cur = ed.zoom;
  const next = mode === 'in' ? steps.find(s => s > cur + 0.001) ?? 2 : [...steps].reverse().find(s => s < cur - 0.001) ?? 0.3;
  return applyZoom(next);
}

// ── pointer: select, move, resize, rotate ──────────────────────────────────
function onPointerDown(e) {
  if (e.button !== 0) return;
  const root = ed.root;

  if (ed.editing && ed.editing.field.contains(e.target)) return; // caret placement while typing

  // Page title (meta) editing with a single click.
  const pageField = e.target.closest('[data-page-field]');
  if (pageField && root.querySelector('.sheets[data-meta-editable]')) {
    finishEditing();
    beginEdit(pageField, null, 'title', true);
    e.preventDefault();
    placeCaretAt(pageField, e.clientX, e.clientY);
    return;
  }

  const node = e.target.closest('.el');
  if (!node || !root.contains(node)) {
    finishEditing();
    if (e.target.closest('.sheet')) call('Select', null);
    return;
  }
  const id = node.dataset.elId;

  if (!node.dataset.editable) {
    finishEditing();
    call('Select', id);
    return;
  }

  const check = e.target.closest('[data-check-index]');
  if (check && !ed.editing) {
    e.preventDefault();
    call('ToggleCheck', id, num(check.dataset.checkIndex));
    return;
  }

  if (ed.editing && ed.editing.node !== node) finishEditing();
  if (ed.editing) return;

  if (!node.classList.contains('is-selected')) call('Select', id);

  const handle = e.target.closest('[data-handle]');
  const sheet = node.closest('.sheet');
  const box = readBox(node);
  ed.drag = {
    node, id, sheet, sheetIndex: num(sheet.dataset.sheet),
    mode: handle ? handle.dataset.handle : 'move',
    start: box, box: { ...box },
    sx: e.clientX, sy: e.clientY,
    scale: sheetScale(sheet),
    moved: false,
    aspect: node.dataset.aspect === 'true',
    target: null,
  };
  node.setPointerCapture(e.pointerId);
  e.preventDefault();
}

const HANDLE_SIGNS = { nw: [-1, -1], ne: [1, -1], sw: [-1, 1], se: [1, 1], e: [1, 0], s: [0, 1] };

function onPointerMove(e) {
  const d = ed?.drag;
  if (!d) return;
  const dx = (e.clientX - d.sx) / d.scale;
  const dy = (e.clientY - d.sy) / d.scale;
  if (!d.moved && Math.hypot(dx, dy) < 3) return;
  if (!d.moved) { d.moved = true; d.node.classList.add('is-dragging'); }

  const s = d.start;
  let b = { ...s };

  if (d.mode === 'move') {
    b.x = s.x + dx;
    b.y = s.y + dy;
    clampBox(b);
    // Highlight another sheet if the pointer is over it.
    const hit = hitSheet(ed.root, e.clientX, e.clientY);
    const t = hit && hit.index !== d.sheetIndex ? hit.sheet : null;
    if (t !== d.target) {
      d.target?.classList.remove('drop-target');
      t?.classList.add('drop-target');
      d.target = t;
    }
  } else if (d.mode === 'rot') {
    const r = d.node.getBoundingClientRect();
    const cx = r.left + r.width / 2, cy = r.top + r.height / 2;
    let ang = Math.atan2(e.clientY - cy, e.clientX - cx) * 180 / Math.PI + 90;
    if (ang > 180) ang -= 360;
    if (e.shiftKey) ang = Math.round(ang / 5) * 5;
    else if (Math.abs(ang) < 2.5) ang = 0;
    b.rot = round1(ang);
  } else {
    const [sx, sy] = HANDLE_SIGNS[d.mode] || [1, 1];
    const rad = s.rot * Math.PI / 180, cos = Math.cos(rad), sin = Math.sin(rad);
    const lx = dx * cos + dy * sin;
    const ly = -dx * sin + dy * cos;
    let w = sx ? s.w + sx * lx : s.w;
    let h = sy ? s.h + sy * ly : s.h;
    if ((d.aspect || e.shiftKey) && sx && sy) {
      const k = Math.max(w / s.w, h / s.h);
      w = s.w * k; h = s.h * k;
    }
    w = Math.max(16, w); h = Math.max(12, h);
    // Keep the opposite corner fixed in world space, even when rotated.
    const ax = -sx * s.w / 2, ay = -sy * s.h / 2;
    const c0x = s.x + s.w / 2, c0y = s.y + s.h / 2;
    const awx = c0x + ax * cos - ay * sin, awy = c0y + ax * sin + ay * cos;
    const nax = -sx * w / 2, nay = -sy * h / 2;
    const ncx = awx - (nax * cos - nay * sin), ncy = awy - (nax * sin + nay * cos);
    b = { x: ncx - w / 2, y: ncy - h / 2, w, h, rot: s.rot };
  }

  d.box = b;
  writeBox(d.node, b);
}

function onPointerUp(e) {
  const d = ed?.drag;
  if (!d) return;
  ed.drag = null;
  try { d.node.releasePointerCapture(e.pointerId); } catch { /* ignore */ }
  d.node.classList.remove('is-dragging');
  d.target?.classList.remove('drop-target');
  if (!d.moved) return;
  ed.swallowClick = true;
  setTimeout(() => { if (ed) ed.swallowClick = false; }, 0);

  let sheet = d.sheetIndex;
  let b = d.box;
  if (d.mode === 'move' && e.type === 'pointerup') {
    const hit = hitSheet(ed.root, e.clientX, e.clientY);
    if (hit && hit.index !== d.sheetIndex) {
      // Re-anchor so the grab point stays under the pointer on the new sheet.
      const grabX = (d.sx - d.sheet.getBoundingClientRect().left) / d.scale - d.start.x;
      const grabY = (d.sy - d.sheet.getBoundingClientRect().top) / d.scale - d.start.y;
      b = clampBox({ ...b, x: hit.x - grabX, y: hit.y - grabY });
      sheet = hit.index;
      writeBox(d.node, b);
    }
  }
  const s = d.start;
  if (sheet === d.sheetIndex && b.x === s.x && b.y === s.y && b.w === s.w && b.h === s.h && b.rot === s.rot) return;
  call('CommitTransform', d.id, sheet, round1(b.x), round1(b.y), round1(b.w), round1(b.h), round1(b.rot));
}

function onClickCapture(e) {
  if (ed?.swallowClick) { e.stopPropagation(); e.preventDefault(); ed.swallowClick = false; return; }
  // Links inside the page don't navigate while editing (Ctrl/⌘-click still opens them).
  const a = e.target.closest('a');
  if (a && ed.root.contains(a) && !(e.ctrlKey || e.metaKey) && a.closest('.mode-edit')) e.preventDefault();
}

// ── inline text editing ────────────────────────────────────────────────────
function onDblClick(e) {
  const node = e.target.closest('.el');
  if (!node || !node.dataset.editable) return;
  let field = e.target.closest('[data-edit-field]');
  if (!field || !node.contains(field)) field = node.querySelector('[data-edit-field]');
  if (!field) {
    if (node.dataset.kind === 'photo') call('OpenPhotoOptions', node.dataset.elId);
    return;
  }
  finishEditing();
  beginEdit(field, node, field.dataset.editField, field.dataset.plain === 'true');
  placeCaretAt(field, e.clientX, e.clientY);
}

export function beginTextEdit(id) {
  if (!ed) return;
  const node = ed.root.querySelector(`.el[data-el-id="${id}"]`);
  const field = node?.querySelector('[data-edit-field]');
  if (!node || !field || !node.dataset.editable) return;
  finishEditing();
  beginEdit(field, node, field.dataset.editField, field.dataset.plain === 'true');
  const sel = window.getSelection();
  const range = document.createRange();
  range.selectNodeContents(field);
  range.collapse(false);
  sel.removeAllRanges();
  sel.addRange(range);
}

function beginEdit(field, node, name, plain) {
  ed.editing = { field, node, name, plain, before: plain ? field.innerText : field.innerHTML };
  field.setAttribute('contenteditable', plain ? 'plaintext-only' : 'true');
  if (plain && field.contentEditable !== 'plaintext-only') field.setAttribute('contenteditable', 'true');
  field.spellcheck = true;
  node?.classList.add('is-editing');
  field.addEventListener('blur', onEditBlur);
  field.addEventListener('keydown', onEditKey);
  field.addEventListener('paste', onEditPaste);
  field.focus({ preventScroll: true });
}

function placeCaretAt(field, x, y) {
  let range = null;
  if (document.caretRangeFromPoint) range = document.caretRangeFromPoint(x, y);
  else if (document.caretPositionFromPoint) {
    const p = document.caretPositionFromPoint(x, y);
    if (p) { range = document.createRange(); range.setStart(p.offsetNode, p.offset); }
  }
  if (!range || !field.contains(range.startContainer)) {
    range = document.createRange();
    range.selectNodeContents(field);
    range.collapse(false);
  }
  const sel = window.getSelection();
  sel.removeAllRanges();
  sel.addRange(range);
}

function onEditKey(e) {
  if (!ed?.editing) return;
  if (e.key === 'Escape') { e.preventDefault(); e.target.blur(); return; }
  if (e.key === 'Enter' && ed.editing.plain && !e.shiftKey) { e.preventDefault(); e.target.blur(); return; }
  e.stopPropagation(); // don't trigger editor shortcuts while typing
}

function onEditPaste(e) {
  e.preventDefault();
  const text = (e.clipboardData || window.clipboardData).getData('text/plain');
  document.execCommand('insertText', false, text);
}

function onEditBlur() { finishEditing(); }

function finishEditing() {
  const cur = ed?.editing;
  if (!cur) return;
  ed.editing = null;
  const { field, node, name, plain, before } = cur;
  field.removeEventListener('blur', onEditBlur);
  field.removeEventListener('keydown', onEditKey);
  field.removeEventListener('paste', onEditPaste);
  field.removeAttribute('contenteditable');
  node?.classList.remove('is-editing');
  const value = plain ? field.innerText.replace(/\n+$/, '') : field.innerHTML;
  if (value === before) return;
  if (!node) call('CommitTitle', value.replace(/\s+/g, ' ').trim());
  else call('CommitText', node.dataset.elId, name, value);
}

// ── keyboard shortcuts ─────────────────────────────────────────────────────
function onKeyDown(e) {
  if (!ed || isTyping(e.target)) return;
  const ctrl = e.ctrlKey || e.metaKey;
  const hasSel = !!ed.root.querySelector('.el.is-selected[data-editable]');
  const k = e.key;

  if (ctrl && (k === 'z' || k === 'Z')) { e.preventDefault(); call('OnKey', 'undo', false); return; }
  if (ctrl && (k === 'd' || k === 'D') && hasSel) { e.preventDefault(); call('OnKey', 'duplicate', false); return; }
  if (ctrl && (k === 'p' || k === 'P')) { e.preventDefault(); call('OnKey', 'print', false); return; }
  if (ctrl) return;

  if (k.startsWith('Arrow')) {
    if (hasSel) {
      e.preventDefault();
      const step = e.shiftKey ? 10 : 1;
      const dx = k === 'ArrowLeft' ? -step : k === 'ArrowRight' ? step : 0;
      const dy = k === 'ArrowUp' ? -step : k === 'ArrowDown' ? step : 0;
      const node = ed.root.querySelector('.el.is-selected');
      const b = clampBox(readBox(node));
      b.x += dx; b.y += dy;
      clampBox(b);
      writeBox(node, b);
      clearTimeout(ed.nudgeTimer);
      ed.nudgeTimer = setTimeout(() => {
        const sheet = num(node.closest('.sheet')?.dataset.sheet);
        call('CommitTransform', node.dataset.elId, sheet, round1(b.x), round1(b.y), round1(b.w), round1(b.h), round1(b.rot));
      }, 250);
    } else if (k === 'ArrowLeft' || k === 'ArrowRight') {
      e.preventDefault();
      // In right-to-left layouts the previous day is to the right.
      const back = isRtl() ? 'ArrowRight' : 'ArrowLeft';
      call('OnKey', k === back ? 'prev' : 'next', false);
    }
    return;
  }
  if ((k === 'Delete' || k === 'Backspace') && hasSel) { e.preventDefault(); call('OnKey', 'delete', false); return; }
  if (k === 'Escape') { call('Select', null); return; }
  if (k === 'Enter' && hasSel) {
    const node = ed.root.querySelector('.el.is-selected');
    if (node?.querySelector('[data-edit-field]')) { e.preventDefault(); beginTextEdit(node.dataset.elId); }
  }
}

// ── drop zone, stickers, paste ─────────────────────────────────────────────
function onStickerDragStart(e) {
  const s = e.target.closest?.('[data-sticker]');
  if (!s) return;
  e.dataTransfer.setData('text/x-dayleaf-sticker', s.dataset.sticker);
  e.dataTransfer.effectAllowed = 'copy';
}

function hasPayload(e) {
  const types = Array.from(e.dataTransfer?.types || []);
  return types.includes('Files') || types.includes('text/x-dayleaf-sticker');
}

function onDragOver(e) {
  if (!hasPayload(e)) return;
  e.preventDefault();
  e.dataTransfer.dropEffect = 'copy';
  const canvas = ed.canvasCol.querySelector('.canvas');
  if (Array.from(e.dataTransfer.types).includes('Files')) canvas?.classList.add('drag-over');
}

function onDragLeave(e) {
  if (e.relatedTarget && ed.canvasCol.contains(e.relatedTarget)) return;
  ed.canvasCol.querySelector('.canvas')?.classList.remove('drag-over');
}

async function onDrop(e) {
  if (!hasPayload(e)) return;
  e.preventDefault();
  ed.canvasCol.querySelector('.canvas')?.classList.remove('drag-over');
  const hit = hitSheet(ed.root, e.clientX, e.clientY);
  const sticker = e.dataTransfer.getData('text/x-dayleaf-sticker');
  if (sticker) {
    if (hit) call('DropSticker', sticker, hit.index, round1(hit.x - 32), round1(hit.y - 32));
    else call('DropSticker', sticker, -1, 0, 0);
    return;
  }
  const files = Array.from(e.dataTransfer.files || []).filter(f => /^image\/(jpeg|png|webp|gif)$/.test(f.type));
  if (files.length === 0) { call('Toast', 'Only JPEG, PNG, WebP and GIF images can be dropped.'); return; }
  await sendFiles(files, hit);
}

function onPaste(e) {
  if (!ed || isTyping(e.target)) return;
  const files = Array.from(e.clipboardData?.files || []).filter(f => f.type.startsWith('image/'));
  if (files.length === 0) return;
  e.preventDefault();
  sendFiles(files, null);
}

async function sendFiles(files, hit) {
  const input = ed.fileInput;
  if (!input) return;
  await call('SetDropPoint', hit ? hit.index : -1, hit ? round1(hit.x) : 0, hit ? round1(hit.y) : 0);
  const dt = new DataTransfer();
  files.forEach(f => dt.items.add(f));
  input.files = dt.files;
  input.dispatchEvent(new Event('change', { bubbles: true }));
}

// ── misc ───────────────────────────────────────────────────────────────────
export function scrollToElement(id) {
  const node = ed?.root.querySelector(`.el[data-el-id="${id}"]`);
  node?.scrollIntoView({ behavior: 'smooth', block: 'center' });
}

export function openFilePicker(input) { input?.click(); }

export function scrollToId(id) { document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'center' }); }

export function printNow() { window.print(); }

export async function copyToClipboard(text) {
  try { await navigator.clipboard.writeText(text); return true; }
  catch {
    const ta = document.createElement('textarea');
    ta.value = text; document.body.appendChild(ta); ta.select();
    const ok = document.execCommand('copy'); ta.remove(); return ok;
  }
}

export function openTab(url) { window.open(url, '_blank', 'noopener'); }

// ── timeline keys ──────────────────────────────────────────────────────────
export function initListKeys(dotNetRef) {
  disposeListKeys();
  listKeys = e => {
    if (isTyping(e.target) || e.ctrlKey || e.metaKey || e.altKey) return;
    if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
      e.preventDefault();
      // .NET thinks in LTR terms (ArrowLeft = earlier); swap for right-to-left layouts.
      const key = isRtl() ? (e.key === 'ArrowLeft' ? 'ArrowRight' : 'ArrowLeft') : e.key;
      dotNetRef.invokeMethodAsync('OnListKey', key).catch(() => { });
    }
  };
  document.addEventListener('keydown', listKeys);
}

export function disposeListKeys() {
  if (listKeys) document.removeEventListener('keydown', listKeys);
  listKeys = null;
}
