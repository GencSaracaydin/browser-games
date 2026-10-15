// All sound is synthesized locally. No AudioContext is created before opt-in.
export function createAudio() {
  const NOTES = [293.6648, 329.6276, 369.9944, 440, 493.8833];
  let context = null;
  let master = null;
  let windFilter = null;
  let waterGain = null;
  let noiseBuffer = null;
  let wanted = false;
  let disposed = false;
  let suspendTimer = null;
  let footTime = 0;
  let nextBell = 7 + Math.random() * 5;
  let paused = false;
  const permanent = new Set();
  const voices = new Set();

  function track(node) {
    permanent.add(node);
    return node;
  }

  function createNoise() {
    const length = Math.ceil(context.sampleRate * 3.7);
    const buffer = context.createBuffer(2, length, context.sampleRate);
    for (let channel = 0; channel < 2; channel++) {
      const samples = buffer.getChannelData(channel);
      // Smoothed noise keeps the bed soft even on small laptop speakers.
      let previous = 0;
      for (let i = 0; i < length; i++) {
        previous = (previous + (Math.random() * 2 - 1) * 0.12) / 1.12;
        samples[i] = previous * 2;
      }
      // Fade both edges to zero so the looping buffer has no seam click.
      const edge = Math.ceil(context.sampleRate * 0.025);
      for (let i = 0; i < edge; i++) {
        const fade = i / edge;
        samples[i] *= fade;
        samples[length - 1 - i] *= fade;
      }
    }
    return buffer;
  }

  function ambientSource(frequency, gain, type, offset = 0) {
    const source = track(context.createBufferSource());
    const filter = track(context.createBiquadFilter());
    const level = track(context.createGain());
    source.buffer = noiseBuffer;
    source.loop = true;
    filter.type = type;
    filter.frequency.value = frequency;
    filter.Q.value = 0.4;
    level.gain.value = gain;
    source.connect(filter);
    filter.connect(level);
    level.connect(master);
    source.start(0, offset);
    return { source, filter, level };
  }

  function initialize() {
    const AudioContextClass = globalThis.AudioContext || globalThis.webkitAudioContext;
    if (!AudioContextClass) return false;
    context = new AudioContextClass();
    master = track(context.createGain());
    master.gain.value = 0;
    master.connect(context.destination);
    noiseBuffer = createNoise();
    const wind = ambientSource(420, 0.17, 'lowpass');
    const water = ambientSource(900, 0.025, 'bandpass', 1.45);
    windFilter = wind.filter;
    waterGain = water.level;

    const breath = track(context.createOscillator());
    const depth = track(context.createGain());
    breath.type = 'sine';
    breath.frequency.value = 0.075;
    depth.gain.value = 0.027;
    breath.connect(depth);
    depth.connect(wind.level.gain);
    breath.start();
    return true;
  }

  function releaseVoice(voice) {
    voices.delete(voice);
    for (const node of voice.nodes) {
      try { node.disconnect(); } catch { /* A disposed context is already silent. */ }
    }
  }

  function bell(frequency, delay = 0, intensity = 1) {
    if (!wanted || !context || context.state !== 'running' || voices.size >= 20) return;
    const start = context.currentTime + delay + 0.008;
    const end = start + 3.8;
    const voice = { nodes: [], sources: [] };
    voices.add(voice);
    // A faint second partial suggests a struck glass bell without a sharp attack.
    for (const [ratio, volume] of [[1, 0.065], [2.003, 0.012]]) {
      const oscillator = context.createOscillator();
      const envelope = context.createGain();
      oscillator.type = 'sine';
      oscillator.frequency.value = frequency * ratio;
      envelope.gain.setValueAtTime(0, start);
      envelope.gain.linearRampToValueAtTime(volume * intensity, start + 0.028);
      envelope.gain.exponentialRampToValueAtTime(0.0001, end);
      oscillator.connect(envelope);
      envelope.connect(master);
      voice.nodes.push(oscillator, envelope);
      voice.sources.push(oscillator);
      oscillator.start(start);
      oscillator.stop(end + 0.03);
    }
    voice.sources[0].onended = () => releaseVoice(voice);
  }

  function footstep(inWater, sprinting) {
    if (!wanted || !context || context.state !== 'running' || voices.size >= 24) return;
    const start = context.currentTime + 0.005;
    const duration = inWater ? 0.19 : 0.085;
    const source = context.createBufferSource();
    const filter = context.createBiquadFilter();
    const envelope = context.createGain();
    const voice = { nodes: [source, filter, envelope], sources: [source] };
    voices.add(voice);
    source.buffer = noiseBuffer;
    source.playbackRate.value = inWater ? 1.5 : 0.8 + Math.random() * 0.2;
    filter.type = 'bandpass';
    filter.frequency.value = inWater ? 1100 : 250;
    filter.Q.value = inWater ? 0.5 : 0.7;
    envelope.gain.setValueAtTime(0, start);
    envelope.gain.linearRampToValueAtTime((inWater ? 0.13 : 0.105) * (sprinting ? 1.12 : 1), start + 0.009);
    envelope.gain.exponentialRampToValueAtTime(0.0001, start + duration);
    source.connect(filter);
    filter.connect(envelope);
    envelope.connect(master);
    source.onended = () => releaseVoice(voice);
    source.start(start, Math.random() * 2);
    source.stop(start + duration + 0.02);
  }

  function chime(kind = 'echo') {
    if (!wanted) return;
    if (kind === 'complete' || kind === 'finish' || kind === 'ending') {
      [0, 2, 3, 4, 7].forEach((note, index) => {
        bell(NOTES[note % 5] * (note >= 5 ? 2 : 1), index * 0.29, 0.7);
      });
    } else if (kind === 'shrine' || kind === 'beacon' || kind === 'activate') {
      bell(NOTES[0], 0, 0.8);
      bell(NOTES[3], 0.22, 0.65);
      bell(NOTES[2] * 2, 0.5, 0.4);
    } else {
      bell(NOTES[2], 0, 0.65);
      bell(NOTES[4], 0.17, 0.55);
    }
  }

  async function setEnabled(value) {
    wanted = Boolean(value) && !disposed;
    if (suspendTimer !== null) {
      clearTimeout(suspendTimer);
      suspendTimer = null;
    }
    try {
      if (wanted && !context && !initialize()) {
        wanted = false;
        return false;
      }
      if (!context) return false;
      if (wanted && !paused && context.state !== 'running') await context.resume();
      if (disposed) return false;
      const now = context.currentTime;
      master.gain.cancelScheduledValues(now);
      master.gain.setTargetAtTime(wanted && !paused ? 0.34 : 0, now, wanted ? 0.32 : 0.12);
      if (!wanted) {
        suspendTimer = setTimeout(() => {
          suspendTimer = null;
          if (!wanted && context && context.state !== 'closed') context.suspend().catch(() => {});
        }, 650);
      }
      return wanted;
    } catch {
      wanted = false;
      if (master && context && context.state !== 'closed') {
        try { master.gain.setTargetAtTime(0, context.currentTime, 0.03); } catch { /* Unsupported audio stays off. */ }
      }
      return false;
    }
  }

  return {
    get enabled() { return wanted && !disposed; },
    setEnabled,
    async setPaused(value) {
      paused = Boolean(value);
      if (!context || context.state === 'closed' || disposed) return;
      try {
        if (paused) await context.suspend();
        else if (wanted) {
          await context.resume();
          master.gain.setTargetAtTime(0.34, context.currentTime, 0.25);
        }
      } catch { /* A browser may keep background contexts suspended. */ }
    },
    chime,
    update(dt, { moving = false, inWater = false, sprinting = false, complete = false } = {}) {
      if (!wanted || !context || context.state !== 'running' || disposed) return;
      const step = Math.min(Math.max(Number.isFinite(dt) ? dt : 0, 0), 0.2);
      const now = context.currentTime;
      windFilter.frequency.setTargetAtTime(sprinting && moving ? 560 : 420, now, 2.4);
      waterGain.gain.setTargetAtTime(inWater ? 0.075 : 0.025, now, 1.3);
      if (moving) {
        footTime += step;
        const interval = sprinting ? 0.29 : inWater ? 0.52 : 0.43;
        if (footTime >= interval) {
          footTime %= interval;
          footstep(inWater, sprinting);
        }
      } else {
        footTime = 0;
      }
      nextBell -= step;
      if (nextBell <= 0) {
        bell(NOTES[Math.floor(Math.random() * NOTES.length)] * (Math.random() > 0.8 ? 2 : 1), 0, 0.26);
        nextBell = 10 + Math.random() * 12;
      }

    },
    dispose() {
      disposed = true;
      wanted = false;
      if (suspendTimer !== null) clearTimeout(suspendTimer);
      suspendTimer = null;
      for (const voice of voices) {
        for (const source of voice.sources) {
          try { source.stop(); } catch { /* Already ended. */ }
        }
        releaseVoice(voice);
      }
      for (const node of permanent) {
        if (typeof node.stop === 'function') {
          try { node.stop(); } catch { /* Already stopped. */ }
        }
        try { node.disconnect(); } catch { /* Already disconnected. */ }
      }
      permanent.clear();
      if (context && context.state !== 'closed') context.close().catch(() => {});
    },
  };
}
