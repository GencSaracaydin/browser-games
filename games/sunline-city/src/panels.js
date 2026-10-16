import {WEAPONS} from './combat.js';
import {ACTIVITY_CATALOG} from './activities.js';
import {PAINTS} from './county.js';
import {escapeXML as esc, ACTIVITY_COLORS, ACTIVITY_SYMBOLS} from './maps.js';

const money = value => '$' + Math.round(value || 0).toLocaleString();
const DESCRIPTIONS = {
  'race-coast':'Old coast, fast corners. Chase a medal and beat your personal best.',
  'race-county':'A long-distance sprint through the new southern county.',
  'race-airport':'Take a fast car through the airport circuit. Every second counts.',
  taxi:'Pick up three passengers, keep the ride smooth, and bank the tips.',
  export:'Find the requested vehicle, lose the heat, and deliver it in one piece.',
  survival:'Three waves in the freight yard. Keep moving, find cover, and reload.',
  heist:'A getaway car, a guarded case, and a clean escape. Make every stage count.',
  bounty:'Track the marked fugitive, defeat the guards, and collect your reward.',
};

export function activitiesHTML(sim) {
  const active = sim.activities.active;
  const list=sim.activities.list();
  return `<div class="eyebrow">SUNLINE COUNTY / THINGS TO GET INTO</div><h2 id="modal-title">Make a day of it.</h2>
  <p>Choose an adventure to mark its starting point. Drive there and press <kbd>E</kbd>. Cash buys weapons, ammunition, and a better ride.</p>
  <div class="guide-summary"><span>${sim.completed}/5 story jobs</span><span>${sim.county.jumps.size}/${sim.world.stuntRamps?.length||0} stunt jumps</span><span>${sim.collected.size}/12 mixtapes</span><strong>${money(sim.cash)}</strong></div>
  ${active?`<div class="active-job"><div><small>IN PROGRESS</small><strong>${esc(sim.activities.objective()?.title || active.name || active.id)}</strong></div><button class="secondary danger" id="cancel-activity">Abandon activity</button></div>`:''}
  <div class="activity-grid">${ACTIVITY_CATALOG.map((a,i)=>{
    const status=list.find(x=>x.id===a.id)||{},record=sim.activities.records?.[a.id],type=a.type||a.kind||a.id.split('-')[0];
    const best=record?.bestTime??record?.best;
    return `<article class="activity-card" style="--activity:${ACTIVITY_COLORS[type]||'#5b877a'}"><div class="card-top"><span class="activity-icon">${ACTIVITY_SYMBOLS[type]||'•'}</span><small>${esc(type.toUpperCase())}</small><span>${String(i+1).padStart(2,'0')}</span></div><h3>${esc(a.name||a.title)}</h3><p>${esc(a.description||DESCRIPTIONS[a.id]||'Find your next adventure in Sunline County.')}</p><div class="card-meta"><span>${money(a.reward||status.reward)} reward</span><span>${record?.medal&&record.medal!=='none'?esc(record.medal).toUpperCase():record?.completions?'COMPLETED':status.distance?`${Math.round(status.distance/12)} m away`:'REPLAYABLE'}</span></div>${typeof best==='number'?`<div class="personal-best">Best ${best.toFixed(1)}s</div>`:''}<button class="secondary" data-guide="${esc(a.id)}">Mark starting point <span>↗</span></button></article>`;
  }).join('')}</div><p class="guide-foot">Gold diamonds follow the original five-job story. County adventures have lettered markers. Stunt ramps pay $300 on the first clean landing. Active activities restart when you leave the game; rewards and records are saved.</p>`;
}

function weaponDrawing(w) {
  const long=w.id==='carbine'||w.id==='shotgun',smg=w.id==='smg';
  return `<svg viewBox="0 0 160 70" aria-hidden="true"><path d="${long?'M18 30h24l10-8h47v6h40v7h-40l-7 9H51l-12-5-15 8-7-9z':smg?'M24 23h71v7h25v7H83l-10 8H61l-2 17H46l-3-26H24z':'M35 20h79v15H75l-4 6H60l-5 23H39l4-30h-8z'}" fill="#26484f"/><path d="M${long?'53 27h39':'48 25h50'}" stroke="#87aaa0" stroke-width="3"/><path d="M${long?'102 31h29':'100 29h8'}" stroke="#b87862" stroke-width="3"/></svg>`;
}
export function arsenalHTML(sim, shopping = false) {
  const state=sim.combat.state();
  return `<div class="eyebrow">${shopping?'SUNLINE SPORTING ARMS / DOWNTOWN':'YOUR LOADOUT / 1–4 TO SWITCH'}</div><h2 id="modal-title">A little more firepower.</h2>
  <p>${shopping?'Ammunition included with every new weapon. Purchases and your loadout are saved.':'Four different tools for the job. Equip an owned weapon here or with 1–4. Visit Sunline Sporting Arms in Downtown to buy weapons and ammunition.'}</p>
  <div class="guide-summary"><span>Mouse to aim · hold click to fire</span><span>R reload · Q dodge</span><strong>${money(sim.cash)}</strong></div>
  <div class="weapon-grid">${WEAPONS.map((w,i)=>{const item=state.weapons.find(x=>x.id===w.id),owned=item?.owned,selected=state.selected===w.id;return `<article class="weapon-card ${selected?'selected':''}"><div class="card-top"><small>0${i+1} / ${esc(w.shortName)}</small><span>${selected?'EQUIPPED':owned?'OWNED':money(w.price)}</span></div><div class="weapon-art">${weaponDrawing(w)}</div><h3>${esc(w.name)}</h3><p>${esc(w.description)}</p><div class="weapon-stats"><span>CAPACITY <b>${w.magazine}</b></span><span>RANGE <b>${Math.round(w.range/12)}m</b></span><span>RESERVE <b>${owned?item.reserve:'—'}</b></span></div><div class="weapon-actions">${owned?`<button class="secondary" data-equip="${w.id}" ${selected?'disabled':''}>${selected?'Equipped':'Equip'}</button>${shopping?`<button class="secondary" data-ammo="${w.id}" ${sim.cash<w.refillPrice?'disabled':''}>Refill ${money(w.refillPrice)}</button>`:''}`:shopping?`<button class="secondary" data-buy="${w.id}" ${sim.cash<w.price?'disabled':''}>Buy ${money(w.price)}</button>`:'<span class="locked-note">Available at Sporting Arms</span>'}</div></article>`;}).join('')}</div>
  ${!shopping?'<div class="modal-actions"><button class="secondary" id="find-supply">Find Sporting Arms ↗</button><button class="secondary" id="find-range">Visit the shooting range ↗</button></div>':''}`;
}

export function workshopHTML(sim) {
  const c=sim.vehicle,tune=sim.county.tuning();
  return `<div class="eyebrow">SOL MOTOR WORKS / SERVICE & GOOD COMPANY</div><h2 id="modal-title">Make it your ride.</h2><p>Performance and paint are saved for this vehicle model. A quiet street deserves a good-looking car.</p><div class="guide-summary"><span>${esc(c?.type?.toUpperCase()||'NO VEHICLE')}</span><span>Condition ${Math.round(c?.health||0)}%</span><strong>${money(sim.cash)}</strong></div>
  <div class="workshop-grid"><article class="upgrade-card"><small>01 / ENGINE</small><h3>${tune.engine===2?'Fully tuned':`Stage ${tune.engine+1} tuning`}</h3><p>Stronger acceleration and a higher top speed. Two stages available.</p><button class="secondary" data-upgrade="engine" ${tune.engine>=2||sim.cash<500+tune.engine*400?'disabled':''}>${tune.engine===2?'Installed':money(500+tune.engine*400)+' · Install'}</button></article><article class="upgrade-card"><small>02 / HANDLING</small><h3>Road & track kit</h3><p>Quicker steering and more confident braking through the corners.</p><button class="secondary" data-upgrade="handling" ${tune.handling||sim.cash<450?'disabled':''}>${tune.handling?'Installed':'$450 · Install'}</button></article><article class="upgrade-card"><small>03 / RECOVERY</small><h3>Good as new.</h3><p>Restore your car’s condition and your health for the next adventure.</p><button class="secondary" data-upgrade="repair" ${sim.cash<100?'disabled':''}>$100 · Repair</button></article></div><div class="paint-row"><div><h3>A fresh coat.</h3><p>$120 · Choose a color</p></div><div>${PAINTS.map((color,i)=>`<button class="paint-swatch" style="background:${color}" data-paint="${color}" aria-label="Paint color ${i+1}" ${sim.cash<120||c?.type==='police'?'disabled':''}></button>`).join('')}</div></div>`;
}
