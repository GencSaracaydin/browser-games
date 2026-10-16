/* Small, carefully layered Canvas sprites. All sizes are in world pixels. */
const INK = '#173b59';
const CREAM = '#fff1d5';

function shape(ctx, points, fill) {
  ctx.beginPath();
  ctx.moveTo(points[0][0], points[0][1]);
  for (let i = 1; i < points.length; i++) ctx.lineTo(points[i][0], points[i][1]);
  ctx.closePath();
  ctx.fillStyle = fill;
  ctx.fill();
}

function rounded(ctx, x, y, w, h, r, fill) {
  ctx.beginPath();
  ctx.roundRect(x, y, w, h, r);
  ctx.fillStyle = fill;
  ctx.fill();
}

function oval(ctx, x, y, rx, ry, fill) {
  ctx.beginPath();
  ctx.ellipse(x, y, rx, ry, 0, 0, Math.PI * 2);
  ctx.fillStyle = fill;
  ctx.fill();
}

/** A vehicle points east at angle zero. */
export function drawCar(ctx, car, time = 0) {
  const angle = car.angle || 0;
  const van = car.type === 'van';
  const police = car.type === 'police';
  const sport = car.type === 'sport' || !car.type;
  const color = police ? '#f9eed7' : car.color || '#f17c82';
  const halfLength = van ? 26 : 23;
  const halfWidth = van ? 12 : 10.5;
  ctx.save();
  ctx.translate(car.x, car.y);

  // The sun always casts toward the bottom right, independent of heading.
  ctx.save();
  ctx.translate(4.5, 5.5);
  ctx.rotate(angle);
  rounded(ctx, -halfLength - 1, -halfWidth - 1, halfLength * 2 + 2, halfWidth * 2 + 2, 5, 'rgba(18,46,78,.25)');
  ctx.restore();
  ctx.rotate(angle);

  // Wheels and the thin dark lower sill give a tiny sprite some weight.
  for (const x of [-14, 14]) {
    rounded(ctx, x - 4, -halfWidth - 2.5, 8, 4.5, 1.2, '#17384c');
    rounded(ctx, x - 4, halfWidth - 2, 8, 4.5, 1.2, '#17384c');
    ctx.fillStyle = '#9fb7b1';
    ctx.fillRect(x - 2, -halfWidth - 2.6, 4, .8);
    ctx.fillRect(x - 2, halfWidth + 1.7, 4, .8);
  }
  rounded(ctx, -halfLength, -halfWidth, halfLength * 2, halfWidth * 2 + 1.5, van ? 3 : 5, INK);
  rounded(ctx, -halfLength, -halfWidth - 1, halfLength * 2, halfWidth * 2, van ? 3 : 5, color);

  // Cream edge catches and the broad shaded near-side panel.
  ctx.save();
  ctx.globalAlpha = .22;
  rounded(ctx, -halfLength + 3, -halfWidth, halfLength * 2 - 6, 2, 1, '#fff9dc');
  ctx.restore();
  rounded(ctx, -halfLength + 2, halfWidth - 3, halfLength * 2 - 4, 3.4, 1.2, 'rgba(17,50,71,.22)');
  ctx.fillStyle = '#e6ebcf';
  ctx.fillRect(halfLength - 1, -halfWidth + 3, 1.4, halfWidth * 2 - 6);
  ctx.fillRect(-halfLength - .4, -halfWidth + 3, 1.5, halfWidth * 2 - 6);

  if (van) {
    rounded(ctx, -20, -8.8, 28, 16, 2, 'rgba(255,250,224,.18)');
    ctx.strokeStyle = 'rgba(35,68,81,.18)';
    ctx.lineWidth = .7;
    ctx.strokeRect(-18, -7, 24, 12.8);
    shape(ctx, [[10,-8],[16,-6.7],[16,6],[10,7.3]], '#244e69');
    shape(ctx, [[10,-8],[12,-7.7],[12,6.8],[10,7.3]], '#8eced4');
    ctx.fillStyle = CREAM;
    ctx.fillRect(8.4, -10, 5, 1.1);
    ctx.fillRect(8.4, 8.6, 5, 1.1);
    ctx.fillStyle = 'rgba(20,52,70,.3)';
    ctx.fillRect(-6, -9.9, 4, .8);
    ctx.fillRect(-6, 8.7, 4, .8);
  } else if (sport) {
    // Open cabin: leather seats, console, driver, chrome windscreen frame.
    rounded(ctx, -12.5, -7.5, 19, 14, 2.4, '#234959');
    rounded(ctx, -11, -6.2, 10, 5, 1.5, '#d39273');
    rounded(ctx, -11, .4, 10, 5, 1.5, '#d39273');
    rounded(ctx, -10.5, -6.2, 2.8, 5, 1, '#f3c19b');
    rounded(ctx, -10.5, .4, 2.8, 5, 1, '#f3c19b');
    ctx.fillStyle = '#bbd1be';
    ctx.fillRect(-9, -.8, 12, 1.1);
    oval(ctx, 1.8, -3.4, 2.1, 2, '#173844');
    oval(ctx, 1.8, -3.4, 1.15, 1.1, '#a6b9ac');
    if (car.occupied || car.driver || car.isPlayer) {
      oval(ctx, -3.2, -3.8, 2.3, 2.4, '#ffedcf');
      oval(ctx, -3.7, -4.1, 1.45, 1.5, '#493d42');
    }
    shape(ctx, [[6.5,-8],[10,-7],[10,6.2],[6.5,7]], '#c5eddf');
    shape(ctx, [[7.3,-6.7],[9.5,-6.3],[9.5,5.7],[7.3,6]], '#6eafb7');
    ctx.fillStyle = '#fff7dc';
    ctx.fillRect(6.2, -8, .8, 15);
    ctx.fillRect(-14, -7.4, 1, 14);
    // Low louvres on the long rear deck.
    ctx.fillStyle = 'rgba(33,56,64,.32)';
    for (let i = 0; i < 3; i++) ctx.fillRect(-19 + i * 1.8, -5.3, .65, 10);
    ctx.fillStyle = 'rgba(255,250,225,.28)';
    ctx.fillRect(12, -7, 6, .8);
  } else {
    // A conventional glazed roof, kept separate from the convertible silhouette.
    shape(ctx, [[-13,-7.7],[-8,-8.5],[8,-7],[11,-5.8],[11,5.8],[8,7],[-8,8],[-13,6.8]], '#28566b');
    rounded(ctx, -7, -6.8, 12, 12.7, 1.7, police ? '#204459' : color);
    shape(ctx, [[5.8,-6.7],[9,-5.5],[9,5],[5.8,6]], '#9cd1cf');
    shape(ctx, [[-11.5,-6.6],[-8,-7],[-8,6.4],[-11.5,5.5]], '#68a6b6');
    ctx.fillStyle = 'rgba(255,255,224,.35)';
    ctx.fillRect(-5, -6, 8, .8);
    ctx.fillStyle = CREAM;
    ctx.fillRect(-2, -9, 4, .7);
    ctx.fillRect(-2, 8.3, 4, .7);
  }

  // Mirrors, inset headlights, tail lamps and a miniature rear number plate.
  rounded(ctx, 5, -halfWidth - 2, 3.5, 2.2, .6, color);
  rounded(ctx, 5, halfWidth -.2, 3.5, 2.2, .6, color);
  ctx.fillStyle = '#fff5b8';
  ctx.fillRect(halfLength - 2.5, -halfWidth + 2, 2, 4);
  ctx.fillRect(halfLength - 2.5, halfWidth - 6, 2, 4);
  ctx.fillStyle = car.braking ? '#ff4b46' : '#d94f5d';
  ctx.fillRect(-halfLength + .3, -halfWidth + 2, 2, 3.5);
  ctx.fillRect(-halfLength + .3, halfWidth - 5.5, 2, 3.5);
  ctx.fillStyle = '#f7e5c2';
  ctx.fillRect(-halfLength -.5, -2.1, 1, 4);
  ctx.fillStyle = '#4f6470';
  ctx.fillRect(halfLength - .7, -2.6, .8, 5);

  if (police) {
    ctx.fillStyle = '#24455b';
    ctx.fillRect(-18, -8, 4, 15);
    rounded(ctx, -3, -7.3, 3.5, 13.2, .8, '#d4dad0');
    const flash = Math.sin(time * 12) > 0;
    rounded(ctx, -2.8, -7.1, 3, 5.7, .7, flash ? '#ff5865' : '#a83d58');
    rounded(ctx, -2.8, .1, 3, 5.7, .7, flash ? '#2c769e' : '#65c8ff');
    if (car.siren || car.pursuing) {
      ctx.globalAlpha = .16;
      oval(ctx, -1.1, flash ? -5 : 4, 9, 9, flash ? '#ff647c' : '#6de1ff');
      ctx.globalAlpha = 1;
    }
    // Door badge, a five-sided gleam at sprite scale.
    shape(ctx, [[0,7.3],[1.3,8.3],[.8,9.8],[-.8,9.8],[-1.3,8.3]], '#e9cb8a');
  }

  if (car.health !== undefined && car.health < 45) {
    const intensity = (45 - Math.max(0, car.health)) / 45;
    for (let i = 0; i < 3; i++) {
      const p = ((time * .55 + i / 3) % 1 + 1) % 1;
      oval(ctx, 13 - p * 10, -p * 15, 2 + p * 5, 2 + p * 4, `rgba(50,63,76,${(1-p)*intensity*.42})`);
    }
  }
  ctx.restore();
}

/** Tiny upright figures read clearly even while the world scrolls and rotates. */
export function drawPerson(ctx, person, time = 0, isPlayer = false) {
  const angle = person.angle || 0;
  const moving = Boolean(person.moving);
  const phase = person.phase || 0;
  const stride = moving ? Math.sin(time * 13 + phase) * 2.2 : 0;
  const skin = person.skin || '#dba780';
  const shirt = isPlayer ? '#fff0d8' : person.shirt || '#f28f8b';
  ctx.save();
  ctx.translate(person.x, person.y);
  oval(ctx, 3, 3, 5.6, 3.2, 'rgba(21,49,78,.26)');
  if (isPlayer && person.highlight) {
    ctx.strokeStyle = 'rgba(255,244,208,.72)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.ellipse(0, 2, 8, 4, 0, 0, Math.PI * 2);
    ctx.stroke();
  }
  // Shoes and navy tapered trousers.
  rounded(ctx, -3.4, 1 + stride, 2.8, 5, .8, '#304a64');
  rounded(ctx, .7, 1 - stride, 2.8, 5, .8, '#304a64');
  rounded(ctx, -3.6, 4.6 + stride, 3, 1.8, .7, isPlayer ? '#fff4d8' : '#244259');
  rounded(ctx, .7, 4.6 - stride, 3, 1.8, .7, isPlayer ? '#fff4d8' : '#244259');
  // Far arm precedes the body; foreground arm follows it.
  rounded(ctx, -5.1, -4 - stride * .55, 2.4, 6, 1, shirt);
  oval(ctx, -3.9, 1.2 - stride * .55, 1.15, 1.2, skin);
  rounded(ctx, -3.6, -5.7, 7.4, 8.4, 1.4, shirt);
  if (isPlayer) {
    shape(ctx, [[-.8,-5.4],[1,-5.4],[1.7,2.2],[-1.2,2.2]], '#ec9191');
    ctx.fillStyle = '#fff7e3';
    ctx.fillRect(-3, -4.8, 1.6, 2.2);
    ctx.fillRect(1.6, -4.8, 1.6, 2.2);
  } else {
    ctx.fillStyle = 'rgba(29,61,78,.15)';
    ctx.fillRect(-3.5, .8, 7, 1.3);
  }
  rounded(ctx, 2.9, -4 + stride * .55, 2.4, 6, 1, shirt);
  oval(ctx, 4.1, 1.2 + stride * .55, 1.15, 1.2, skin);
  // Neck, head, asymmetric hair, and a small nose indicate facing direction.
  ctx.fillStyle = skin;
  ctx.fillRect(-1.3, -7.3, 2.6, 2.7);
  oval(ctx, 0, -8.2, 3.1, 3.45, skin);
  const facingX = Math.cos(angle);
  const facingY = Math.sin(angle);
  oval(ctx, facingX * 2.6, -8.2 + facingY * 1.7, 1.05, 1.15, skin);
  oval(ctx, -facingX * .65, -9.3 - facingY * .4, 2.9, 2.4, person.hair || (isPlayer ? '#433d49' : '#3c4550'));
  if (isPlayer) {
    ctx.strokeStyle = '#243d53';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(facingX * 1.1 - 1.3, -7.6 + facingY * .5);
    ctx.lineTo(facingX * 1.1 + 1.3, -7.6 + facingY * .5);
    ctx.stroke();
  }
  if (person.armed || person.aiming) {
    ctx.save();
    ctx.translate(2, -2);
    ctx.rotate(angle);
    rounded(ctx, 1, -1.2, 6.5, 2, .5, skin);
    rounded(ctx, 5.2, -1.5, 5.5, 1.8, .3, '#233e51');
    ctx.restore();
  }
  ctx.restore();
}

export function drawBoat(ctx, boat, time = 0) {
  ctx.save();
  ctx.translate(boat.x, boat.y + Math.sin(time * 1.1 + (boat.phase || 0)) * .6);
  ctx.rotate(boat.angle || 0);
  // A little launch, drawn bow-first toward positive x.
  oval(ctx, -2, 2, 33, 13, 'rgba(9,92,124,.20)');
  shape(ctx, [[-29,-11],[17,-11],[32,0],[17,11],[-29,11]], '#254b68');
  shape(ctx, [[-28,-12],[17,-12],[32,-1],[17,10],[-28,10]], boat.color || '#fff0ce');
  shape(ctx, [[-24,-8],[14,-8],[25,-1],[14,6],[-24,6]], '#d5b48d');
  rounded(ctx, -15, -7, 23, 12, 2, '#3d7d8c');
  rounded(ctx, -13, -5.5, 9, 9, 1.2, '#f7dfb6');
  shape(ctx, [[8,-7],[13,-5],[13,3],[8,5]], '#bbdfd9');
  ctx.strokeStyle = '#fff6d8';
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(-24,-10); ctx.lineTo(15,-10); ctx.lineTo(28,-1); ctx.lineTo(15,8); ctx.lineTo(-24,8);
  ctx.stroke();
  rounded(ctx, -31, -3.5, 5, 5, 1, '#284962');
  ctx.restore();
}
