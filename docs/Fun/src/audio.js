export class Soundscape {
  constructor() {
    this.ctx = null;
    this.master = null;
    this.muted = localStorage.getItem('cloudtrader_muted') === '1';
  }

  start() {
    if (this.ctx) { this.ctx.resume(); return; }
    const AudioContext = window.AudioContext || window.webkitAudioContext;
    if (!AudioContext) return;
    this.ctx = new AudioContext();
    this.master = this.ctx.createGain();
    this.master.gain.value = this.muted ? 0 : 0.16;
    this.master.connect(this.ctx.destination);

    const length = this.ctx.sampleRate * 3;
    const buffer = this.ctx.createBuffer(1, length, this.ctx.sampleRate);
    const data = buffer.getChannelData(0);
    let last = 0;
    for (let i = 0; i < length; i++) {
      last = last * 0.985 + (Math.random() * 2 - 1) * 0.015;
      data[i] = last;
    }
    const wind = this.ctx.createBufferSource();
    wind.buffer = buffer;
    wind.loop = true;
    const filter = this.ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.value = 620;
    const gain = this.ctx.createGain();
    gain.gain.value = 0.45;
    wind.connect(filter).connect(gain).connect(this.master);
    wind.start();

    [55, 82.41, 110].forEach((frequency, index) => {
      const oscillator = this.ctx.createOscillator();
      const level = this.ctx.createGain();
      oscillator.type = index === 0 ? 'sine' : 'triangle';
      oscillator.frequency.value = frequency;
      level.gain.value = index === 0 ? 0.055 : 0.018;
      oscillator.connect(level).connect(this.master);
      oscillator.start();
    });
  }

  toggle() {
    this.muted = !this.muted;
    localStorage.setItem('cloudtrader_muted', this.muted ? '1' : '0');
    if (this.master && this.ctx) this.master.gain.setTargetAtTime(this.muted ? 0 : 0.16, this.ctx.currentTime, 0.05);
    return this.muted;
  }

  cue(kind = 'ui') {
    if (!this.ctx || this.muted) return;
    const now = this.ctx.currentTime;
    const oscillator = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    oscillator.type = kind === 'error' ? 'sawtooth' : 'sine';
    const base = kind === 'success' ? 520 : kind === 'launch' ? 110 : kind === 'error' ? 150 : 360;
    oscillator.frequency.setValueAtTime(base, now);
    oscillator.frequency.exponentialRampToValueAtTime(kind === 'launch' ? 260 : base * 1.35, now + 0.18);
    gain.gain.setValueAtTime(0.001, now);
    gain.gain.exponentialRampToValueAtTime(0.18, now + 0.015);
    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.28);
    oscillator.connect(gain).connect(this.master);
    oscillator.start(now);
    oscillator.stop(now + 0.3);
  }
}
