// Campo tático animado (formação 4-3-3) usado na tela de login.
// Registra o custom element <field-canvas>.
class FieldCanvas extends HTMLElement {
  connectedCallback() {
    this.style.display = 'block';
    this.style.position = 'absolute';
    this.style.inset = '0';
    const canvas = document.createElement('canvas');
    canvas.style.width = '100%';
    canvas.style.height = '100%';
    this.appendChild(canvas);
    this._canvas = canvas;
    this._resize();
    this._initPlayers();
    this._animate();
    this._ro = new ResizeObserver(() => this._resize());
    this._ro.observe(this);
  }

  disconnectedCallback() {
    cancelAnimationFrame(this._raf);
    this._ro && this._ro.disconnect();
  }

  _resize() {
    const c = this._canvas;
    const r = this.getBoundingClientRect();
    c.width = r.width * devicePixelRatio;
    c.height = r.height * devicePixelRatio;
    this._w = r.width;
    this._h = r.height;
  }

  _initPlayers() {
    // Field center and size (scaled relative to panel)
    // 4-3-3 home team + 4-3-3 away team
    this._t = 0;

    // Formation 4-3-3 positions (x: 0–1, y: 0–1 of field rect)
    const home = [
      // GK
      { x: 0.08, y: 0.5 },
      // Defenders
      { x: 0.22, y: 0.18 }, { x: 0.22, y: 0.38 }, { x: 0.22, y: 0.62 }, { x: 0.22, y: 0.82 },
      // Midfielders
      { x: 0.42, y: 0.25 }, { x: 0.42, y: 0.5 }, { x: 0.42, y: 0.75 },
      // Forwards
      { x: 0.62, y: 0.15 }, { x: 0.62, y: 0.5 }, { x: 0.62, y: 0.85 },
    ];
    const away = [
      { x: 0.92, y: 0.5 },
      { x: 0.78, y: 0.18 }, { x: 0.78, y: 0.38 }, { x: 0.78, y: 0.62 }, { x: 0.78, y: 0.82 },
      { x: 0.58, y: 0.25 }, { x: 0.58, y: 0.5 }, { x: 0.58, y: 0.75 },
      { x: 0.38, y: 0.15 }, { x: 0.38, y: 0.5 }, { x: 0.38, y: 0.85 },
    ];

    const makePlayer = (p, team) => ({
      bx: p.x, by: p.y,
      ox: (Math.random() - 0.5) * 0.04,
      oy: (Math.random() - 0.5) * 0.04,
      phase: Math.random() * Math.PI * 2,
      speed: 0.3 + Math.random() * 0.4,
      team,
    });

    this._home = home.map(p => makePlayer(p, 'home'));
    this._away = away.map(p => makePlayer(p, 'away'));

    // Arrow paths for tactical movements
    this._arrows = [
      { from: home[9], to: home[8], color: 'rgba(234,179,8,0.5)' },
      { from: home[6], to: home[9], color: 'rgba(234,179,8,0.4)' },
      { from: home[7], to: home[10], color: 'rgba(234,179,8,0.35)' },
      { from: home[3], to: home[6], color: 'rgba(234,179,8,0.3)' },
    ];
  }

  _fieldRect() {
    const w = this._w, h = this._h;
    const margin = Math.min(w, h) * 0.08;
    return {
      x: margin,
      y: margin + h * 0.05,
      w: w - margin * 2,
      h: h - margin * 2 - h * 0.05,
    };
  }

  _playerPos(player, fr) {
    const t = this._t * 0.0008 * player.speed;
    const mx = Math.sin(t + player.phase) * player.ox * fr.w;
    const my = Math.cos(t * 0.7 + player.phase) * player.oy * fr.h;
    return {
      x: fr.x + player.bx * fr.w + mx,
      y: fr.y + player.by * fr.h + my,
    };
  }

  _drawField(ctx, fr) {
    const { x, y, w, h } = fr;
    const lineColor = 'rgba(255,255,255,0.07)';
    const accentLine = 'rgba(234,179,8,0.12)';

    ctx.save();
    // Field background subtle gradient
    const bg = ctx.createRadialGradient(x + w/2, y + h/2, 0, x + w/2, y + h/2, Math.max(w, h) * 0.7);
    bg.addColorStop(0, 'rgba(30,50,80,0.18)');
    bg.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = bg;
    ctx.fillRect(x, y, w, h);

    ctx.strokeStyle = lineColor;
    ctx.lineWidth = 1;

    // Outer boundary
    ctx.strokeStyle = 'rgba(255,255,255,0.1)';
    ctx.strokeRect(x, y, w, h);

    // Halfway line
    ctx.beginPath();
    ctx.moveTo(x + w/2, y);
    ctx.lineTo(x + w/2, y + h);
    ctx.stroke();

    // Center circle
    ctx.beginPath();
    ctx.arc(x + w/2, y + h/2, h * 0.18, 0, Math.PI * 2);
    ctx.stroke();

    // Center dot
    ctx.fillStyle = 'rgba(255,255,255,0.2)';
    ctx.beginPath();
    ctx.arc(x + w/2, y + h/2, 3, 0, Math.PI * 2);
    ctx.fill();

    // Left penalty area
    const paW = w * 0.14, paH = h * 0.42;
    ctx.strokeRect(x, y + (h - paH)/2, paW, paH);
    // Left goal area
    const gaW = w * 0.06, gaH = h * 0.22;
    ctx.strokeRect(x, y + (h - gaH)/2, gaW, gaH);
    // Left penalty spot
    ctx.fillStyle = 'rgba(255,255,255,0.2)';
    ctx.beginPath();
    ctx.arc(x + paW * 0.8, y + h/2, 3, 0, Math.PI * 2);
    ctx.fill();
    // Left penalty arc
    ctx.beginPath();
    ctx.arc(x + paW * 0.8, y + h/2, h * 0.1, -Math.PI * 0.55, Math.PI * 0.55);
    ctx.strokeStyle = lineColor;
    ctx.stroke();

    // Right penalty area
    ctx.strokeStyle = lineColor;
    ctx.strokeRect(x + w - paW, y + (h - paH)/2, paW, paH);
    ctx.strokeRect(x + w - gaW, y + (h - gaH)/2, gaW, gaH);
    ctx.fillStyle = 'rgba(255,255,255,0.2)';
    ctx.beginPath();
    ctx.arc(x + w - paW * 0.8, y + h/2, 3, 0, Math.PI * 2);
    ctx.fill();
    ctx.beginPath();
    ctx.arc(x + w - paW * 0.8, y + h/2, h * 0.1, Math.PI * 0.45, Math.PI * 1.55);
    ctx.strokeStyle = lineColor;
    ctx.stroke();

    // Corner arcs
    const cr = w * 0.02;
    [
      [x, y, 0, Math.PI/2],
      [x + w, y, Math.PI/2, Math.PI],
      [x + w, y + h, Math.PI, Math.PI*1.5],
      [x, y + h, Math.PI*1.5, Math.PI*2],
    ].forEach(([cx, cy, a1, a2]) => {
      ctx.beginPath();
      ctx.arc(cx, cy, cr, a1, a2);
      ctx.stroke();
    });

    ctx.restore();
  }

  _drawArrow(ctx, from, to, color) {
    const dx = to.x - from.x, dy = to.y - from.y;
    const len = Math.sqrt(dx*dx + dy*dy);
    const nx = dx/len, ny = dy/len;
    const sx = from.x + nx * 14, sy = from.y + ny * 14;
    const ex = to.x - nx * 14, ey = to.y - ny * 14;

    ctx.save();
    ctx.strokeStyle = color;
    ctx.lineWidth = 1.5;
    ctx.setLineDash([5, 5]);
    ctx.beginPath();
    ctx.moveTo(sx, sy);
    // Slight curve
    const mx = (sx + ex)/2 - ny * 20, my = (sy + ey)/2 + nx * 20;
    ctx.quadraticCurveTo(mx, my, ex, ey);
    ctx.stroke();
    ctx.setLineDash([]);

    // Arrowhead
    const angle = Math.atan2(ey - my, ex - mx);
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.moveTo(ex, ey);
    ctx.lineTo(ex - Math.cos(angle - 0.4)*8, ey - Math.sin(angle - 0.4)*8);
    ctx.lineTo(ex - Math.cos(angle + 0.4)*8, ey - Math.sin(angle + 0.4)*8);
    ctx.closePath();
    ctx.fill();
    ctx.restore();
  }

  _drawPlayer(ctx, pos, team, idx) {
    const r = 10;
    const isHome = team === 'home';
    ctx.save();

    // Glow
    const glow = ctx.createRadialGradient(pos.x, pos.y, 0, pos.x, pos.y, r * 2.5);
    glow.addColorStop(0, isHome ? 'rgba(234,179,8,0.25)' : 'rgba(59,130,246,0.2)');
    glow.addColorStop(1, 'rgba(0,0,0,0)');
    ctx.fillStyle = glow;
    ctx.beginPath();
    ctx.arc(pos.x, pos.y, r * 2.5, 0, Math.PI * 2);
    ctx.fill();

    // Player dot
    ctx.fillStyle = isHome ? '#EAB308' : '#3B82F6';
    ctx.beginPath();
    ctx.arc(pos.x, pos.y, r, 0, Math.PI * 2);
    ctx.fill();

    // Inner ring
    ctx.strokeStyle = isHome ? 'rgba(255,220,0,0.8)' : 'rgba(147,197,253,0.8)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.arc(pos.x, pos.y, r - 3, 0, Math.PI * 2);
    ctx.stroke();

    // Number
    ctx.fillStyle = isHome ? '#0a0a0a' : '#fff';
    ctx.font = `bold ${r}px "Barlow Condensed", sans-serif`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(idx + 1, pos.x, pos.y + 1);

    ctx.restore();
  }

  _animate() {
    const c = this._canvas;
    const ctx = c.getContext('2d');
    const dpr = devicePixelRatio;

    ctx.clearRect(0, 0, c.width, c.height);
    ctx.save();
    ctx.scale(dpr, dpr);

    const fr = this._fieldRect();

    this._drawField(ctx, fr);

    // Arrows first (behind players)
    this._arrows.forEach(a => {
      const fp = this._playerPos(a.from, fr);
      const tp = this._playerPos(a.to, fr);
      this._drawArrow(ctx, fp, tp, a.color);
    });

    // Players
    this._home.forEach((p, i) => {
      const pos = this._playerPos(p, fr);
      this._drawPlayer(ctx, pos, 'home', i);
    });
    this._away.forEach((p, i) => {
      const pos = this._playerPos(p, fr);
      this._drawPlayer(ctx, pos, 'away', i);
    });

    ctx.restore();
    this._t++;
    this._raf = requestAnimationFrame(() => this._animate());
  }
}

customElements.define('field-canvas', FieldCanvas);
window.FieldCanvas = FieldCanvas;
