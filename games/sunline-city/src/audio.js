/* An original, softly mixed procedural radio. No downloads and no autoplay. */
const midi = note => 440 * 2 ** ((note - 69) / 12);
const clamp = (n, lo, hi) => Math.max(lo, Math.min(hi, n));

export class AudioSystem {
  constructor() {
    this.enabled = false;
    this.paused = false;
    this.ctx = null;
    this.destroyed = false;
    this._voices = new Set();
    this._lastSfx = new Map();
    this._step = 0;
    this._nextStep = 0;
    this._sixteenth = 60 / 88 / 4;
    this._lastState = { speed: 0, inCar: false, wanted: 0 };
    this._onVisibility = () => this._syncPlayback();
    if (typeof document !== 'undefined') document.addEventListener('visibilitychange', this._onVisibility);
  }

  async start() {
    if (this.destroyed) return false;
    const AudioContext = globalThis.AudioContext || globalThis.webkitAudioContext;
    if (!AudioContext) return false;
    try {
      if (!this.ctx) this._build(new AudioContext());
      if (this.enabled && !this.paused && !this._hidden()) {
        await this.ctx.resume();
        this.master.gain.setTargetAtTime(.5, this.ctx.currentTime, .08);
        this._nextStep = Math.max(this._nextStep, this.ctx.currentTime + .05);
      } else {
        this.master.gain.value = 0;
        if (this.ctx.state === 'running') await this.ctx.suspend();
      }
      return true;
    } catch {
      // Browsers may still require a new user gesture before resuming audio.
      return false;
    }
  }

  setEnabled(enabled) {
    this.enabled = Boolean(enabled);
    if (this.enabled) void this.start();
    else this._syncPlayback();
  }

  setPaused(paused) {
    this.paused = Boolean(paused);
    this._syncPlayback();
  }

  _hidden() {
    return typeof document !== 'undefined' && document.hidden;
  }

  _syncPlayback() {
    if (!this.ctx || this.destroyed) return;
    const running = this.enabled && !this.paused && !this._hidden();
    if (running) {
      void this.ctx.resume().then(() => {
        if (this.destroyed || !this.enabled || this.paused || this._hidden()) return;
        this.master.gain.setTargetAtTime(.5, this.ctx.currentTime, .08);
        this._nextStep = Math.max(this._nextStep, this.ctx.currentTime + .04);
      }).catch(() => {});
    } else {
      this.master.gain.cancelScheduledValues(this.ctx.currentTime);
      this.master.gain.setValueAtTime(0, this.ctx.currentTime);
      void this.ctx.suspend().catch(() => {});
    }
  }

  _build(ctx) {
    this.ctx = ctx;
    this.master = ctx.createGain();
    this.master.gain.value = 0;
    const limiter = ctx.createDynamicsCompressor();
    limiter.threshold.value = -17;
    limiter.knee.value = 16;
    limiter.ratio.value = 3;
    limiter.attack.value = .006;
    limiter.release.value = .2;
    this.master.connect(limiter);
    limiter.connect(ctx.destination);

    this.radio = ctx.createGain();
    this.radio.gain.value = .6;
    this.radio.connect(this.master);
    this.effects = ctx.createGain();
    this.effects.gain.value = .65;
    this.effects.connect(this.master);

    // A low-volume, filtered echo is reserved for the electric-piano melody.
    this.echo = ctx.createDelay(1);
    this.echo.delayTime.value = this._sixteenth * 3;
    const echoFilter = ctx.createBiquadFilter();
    echoFilter.type = 'lowpass';
    echoFilter.frequency.value = 1800;
    const feedback = ctx.createGain();
    feedback.gain.value = .23;
    const wet = ctx.createGain();
    wet.gain.value = .15;
    this.echo.connect(echoFilter);
    echoFilter.connect(feedback);
    feedback.connect(this.echo);
    echoFilter.connect(wet);
    wet.connect(this.radio);

    this.engineFilter = ctx.createBiquadFilter();
    this.engineFilter.type = 'lowpass';
    this.engineFilter.frequency.value = 185;
    this.engineGain = ctx.createGain();
    this.engineGain.gain.value = 0;
    this.engine = ctx.createOscillator();
    this.engine.type = 'triangle';
    this.engine.frequency.value = 36;
    this.engine.connect(this.engineFilter);
    this.engineFilter.connect(this.engineGain);
    this.engineGain.connect(this.master);
    this.engine.start();

    this.sirenGain = ctx.createGain();
    this.sirenGain.gain.value = 0;
    this.siren = ctx.createOscillator();
    this.siren.type = 'sine';
    this.siren.frequency.value = 570;
    this.siren.connect(this.sirenGain);
    this.sirenGain.connect(this.master);
    this.siren.start();

    this.noiseBuffer = ctx.createBuffer(1, ctx.sampleRate, ctx.sampleRate);
    const samples = this.noiseBuffer.getChannelData(0);
    for (let i = 0; i < samples.length; i++) samples[i] = Math.random() * 2 - 1;
    this._nextStep = ctx.currentTime + .06;
  }

  update(state = {}, dt = 0) {
    this._lastState = { ...this._lastState, ...state };
    if (!this.ctx || !this.enabled || this.paused || this._hidden() || this.ctx.state !== 'running') return;
    const ctx = this.ctx;
    const now = ctx.currentTime;
    const speed = Math.abs(Number(this._lastState.speed) || 0);
    const fast = clamp(speed / 240, 0, 1);
    const driving = Boolean(this._lastState.inCar);
    const wanted = Math.max(0, Number(this._lastState.wanted) || 0);
    this.engine.frequency.setTargetAtTime(34 + fast * 78 + (state.boosting ? 26 : 0), now, .1);
    this.engineFilter.frequency.setTargetAtTime(120 + fast * 160 + (state.boosting ? 180 : 0), now, .15);
    this.engineGain.gain.setTargetAtTime(driving ? .015 + fast * .023 : 0, now, .14);
    this.siren.frequency.setTargetAtTime(580 + Math.sin(now * 3.3) * 145, now, .08);
    this.sirenGain.gain.setTargetAtTime(wanted ? .008 + Math.min(wanted, 3) * .002 : 0, now, .3);
    this.radio.gain.setTargetAtTime(wanted ? .5 : .6, now, .5);

    // The render loop is the scheduler, so a stopped/hidden game never runs a timer.
    if (this._nextStep < now - .3) this._nextStep = now + .03;
    let scheduled = 0;
    while (this._nextStep < now + .12 && scheduled < 8) {
      this._radioStep(this._step++, this._nextStep);
      this._nextStep += this._sixteenth;
      scheduled++;
    }
  }

  _radioStep(step, when) {
    const beat = step % 16;
    const bar = Math.floor(step / 16) % 4;
    const chords = [[57, 61, 64, 68], [54, 57, 61, 64], [50, 54, 57, 61], [52, 56, 59, 66]];
    const chord = chords[bar];
    if (beat === 0) {
      for (let i = 0; i < chord.length; i++) {
        this._tone(midi(chord[i]), when + i * .018, this._sixteenth * 15, .025, 'sine', this.radio, .2, false);
        this._tone(midi(chord[i] + 12), when + i * .018, this._sixteenth * 12, .006, 'triangle', this.radio, .28, false);
      }
    }
    if ([0, 6, 8, 14].includes(beat)) {
      const bass = chord[0] - 12 + (beat === 14 ? 7 : 0);
      this._tone(midi(bass), when, this._sixteenth * 2.8, .075, 'sine', this.radio, .015);
    }
    if (beat === 0 || beat === 8) {
      this._tone(95, when, .2, .07, 'sine', this.radio, .004, false, 42);
    }
    if (beat === 4 || beat === 12) {
      this._noise(when, .095, .018, 950, 'bandpass', this.radio);
      this._tone(170, when, .09, .012, 'triangle', this.radio, .002);
    }
    if (beat % 2 === 0) this._noise(when, .028, beat % 4 === 2 ? .012 : .007, 5600, 'highpass', this.radio);
    // A deliberately spare four-bar motif over warm major-seventh harmony.
    const motif = [[9, 76], [14, 73], [6, 73], [11, 71], [3, 69], [10, 73], [6, 71], [14, 68]];
    for (let i = bar * 2; i < bar * 2 + 2; i++) {
      if (motif[i][0] === beat) {
        this._tone(midi(motif[i][1]), when, .65, .037, 'sine', this.radio, .009, true);
        this._tone(midi(motif[i][1] + 12), when, .25, .005, 'sine', this.radio, .005, true);
      }
    }
  }

  _tone(frequency, when, duration, volume, type = 'sine', destination = this.effects, attack = .008, echo = false, endFrequency = null) {
    if (!this.ctx || this.destroyed) return;
    const osc = this.ctx.createOscillator();
    const envelope = this.ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(frequency, when);
    if (endFrequency) osc.frequency.exponentialRampToValueAtTime(endFrequency, when + duration);
    envelope.gain.setValueAtTime(.0001, when);
    envelope.gain.exponentialRampToValueAtTime(Math.max(.0002, volume), when + attack);
    envelope.gain.exponentialRampToValueAtTime(.0001, when + Math.max(attack + .01, duration));
    osc.connect(envelope);
    envelope.connect(destination);
    if (echo) envelope.connect(this.echo);
    this._voices.add(osc);
    osc.onended = () => {
      this._voices.delete(osc);
      osc.disconnect();
      envelope.disconnect();
    };
    osc.start(when);
    osc.stop(when + duration + .05);
  }

  _noise(when, duration, volume, frequency, type = 'lowpass', destination = this.effects) {
    if (!this.ctx || this.destroyed) return;
    const source = this.ctx.createBufferSource();
    source.buffer = this.noiseBuffer;
    const filter = this.ctx.createBiquadFilter();
    filter.type = type;
    filter.frequency.value = frequency;
    filter.Q.value = .65;
    const envelope = this.ctx.createGain();
    envelope.gain.setValueAtTime(volume, when);
    envelope.gain.exponentialRampToValueAtTime(.0001, when + duration);
    source.connect(filter);
    filter.connect(envelope);
    envelope.connect(destination);
    this._voices.add(source);
    source.onended = () => {
      this._voices.delete(source);
      source.disconnect();
      filter.disconnect();
      envelope.disconnect();
    };
    source.start(when);
    source.stop(when + duration + .03);
  }

  sfx(name) {
    if (!this.ctx || !this.enabled || this.paused || this._hidden() || this.ctx.state !== 'running') return;
    const now = this.ctx.currentTime;
    const cooldown = { crash: .28, horn: .42, shoot: .1, shoot_smg: .065, enemyShoot: .14, hit: .07, coin: .12, mission: .5, complete: 1, busted: 1 }[name] || .18;
    if (now - (this._lastSfx.get(name) ?? -Infinity) < cooldown) return;
    this._lastSfx.set(name, now);
    const notes = (list, spacing = .09, volume = .075) => list.forEach((note, i) => this._tone(midi(note), now + i * spacing, .24, volume, 'sine'));
    switch (name) {
      case 'boost': this._noise(now,.45,.055,1200);this._tone(160,now,.3,.025,'triangle',this.effects,.02,false,360);break;
      case 'driftBank': notes([76,80,83],.06,.045);break;
      case 'photo':this._noise(now,.06,.035,3200);break;
      case 'enter':
        this._noise(now, .065, .035, 850);
        this._tone(130, now, .11, .07, 'sine', this.effects, .003, false, 65);
        break;
      case 'exit':
        this._noise(now, .075, .028, 1100);
        this._tone(100, now, .08, .045, 'sine', this.effects, .003, false, 150);
        break;
      case 'coin': notes([80, 87], .065, .055); break;
      case 'mission': notes([64, 68, 71], .1, .07); break;
      case 'complete': notes([64, 68, 71, 76], .12, .08); break;
      case 'crash':
        this._noise(now, .22, .1, 1450);
        this._tone(110, now, .25, .11, 'sine', this.effects, .002, false, 38);
        break;
      case 'horn':
        this._tone(349, now, .28, .06, 'triangle', this.effects, .02);
        this._tone(440, now, .28, .045, 'triangle', this.effects, .02);
        break;
      case 'shoot_smg':
        this._noise(now, .045, .06, 2700); this._tone(180, now, .06, .05, 'triangle', this.effects, .002, false, 80); break;
      case 'shoot_shotgun':
        this._noise(now, .17, .12, 1200); this._tone(90, now, .19, .12, 'triangle', this.effects, .002, false, 32); break;
      case 'shoot_carbine':
        this._noise(now, .08, .09, 2900); this._tone(210, now, .11, .07, 'triangle', this.effects, .002, false, 65); break;
      case 'enemyShoot':
        this._noise(now, .055, .045, 1700); break;
      case 'reload':
        this._noise(now, .045, .035, 1800); this._noise(now+.13, .06, .03, 1000); break;
      case 'reloadDone':
      case 'weaponSwitch':
        this._noise(now, .035, .025, 2400); this._tone(420, now, .04, .025, 'triangle', this.effects, .001); break;
      case 'hit':
        this._noise(now, .04, .028, 3200); break;
      case 'targetHit':
        this._tone(880, now, .1, .055, 'sine', this.effects, .003, false, 440); break;
      case 'enemyDown': notes([76,71], .04, .03); break;
      case 'hurt':
        this._noise(now, .09, .065, 600); break;
      case 'empty':
        this._tone(270, now, .025, .035, 'triangle', this.effects, .001); break;
      case 'dodge':
      case 'jump':
        this._noise(now, .18, .035, 550); break;
      case 'shoot':
        this._noise(now, .07, .08, 2200);
        this._tone(160, now, .09, .08, 'triangle', this.effects, .002, false, 55);
        break;
      case 'busted': notes([64, 60, 57, 52], .16, .065); break;
      default: break;
    }
  }

  destroy() {
    this.destroyed = true;
    this.enabled = false;
    if (typeof document !== 'undefined') document.removeEventListener('visibilitychange', this._onVisibility);
    for (const source of this._voices) {
      try { source.stop(); } catch { /* The voice may already have ended. */ }
    }
    this._voices.clear();
    if (this.ctx) {
      try { this.engine.stop(); this.siren.stop(); } catch { /* Already stopped. */ }
      this.master.disconnect();
      void this.ctx.close().catch(() => {});
      this.ctx = null;
    }
  }
}
