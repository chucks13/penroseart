const canvas = document.getElementById('waveCanvas');
const ctx = canvas.getContext('2d');
const inputSequence = document.getElementById('waveSequence');
const inputAmplitude = document.getElementById('amplitudeSequence');

function draw() {
    const w = canvas.width;
    const h = canvas.height;
    const segments = 64;
    
    // Parse inputs
    const freqStr = inputSequence.value.replace(/[^1248]/g, '') || "1";
    const ampStr = inputAmplitude.value.replace(/[^0-8]/g, '') || "8";

    ctx.clearRect(0, 0, w, h);

    const stepX = w / segments;

    // Draw vertical segment lines
    for (let i = 1; i < segments; i++) {
        if (i % 16 === 0) ctx.strokeStyle = 'red';          // Major Beats (1, 2, 3, 4)
        else if (i % 8 === 0) ctx.strokeStyle = 'yellow';   // 8th notes (1/2 cycle of 1Hz)
        else if (i % 4 === 0) ctx.strokeStyle = 'blue';     // 16th notes
        else if (i % 2 === 0) ctx.strokeStyle = 'magenta';  // 32nd notes (New Harmonic)
        else ctx.strokeStyle = '#004400';                   // 64th notes (Individual segments)

        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(i * stepX, 0);
        ctx.lineTo(i * stepX, h);
        ctx.stroke();
    }

    // Layout Constants
    const graphH = h * 0.8;
    const baseY = h * 0.9; // Bottom of the single graph area

    // Draw horizontal helper lines for both graphs
    ctx.strokeStyle = '#333';
    ctx.setLineDash([5, 5]);
    ctx.beginPath();
    ctx.moveTo(0, baseY); ctx.lineTo(w, baseY);
    ctx.moveTo(0, baseY - graphH); ctx.lineTo(w, baseY - graphH);
    ctx.stroke();
    ctx.setLineDash([]);

    // 1. Pre-calculate fixed pixel boundaries for each splice
    // Base rule: 1Hz half-cycle = 8 segments (1/8th of canvas)
    const freqs = freqStr.split('').map(Number);
    const amps = ampStr.split('').map(v => parseInt(v) / 8);
    const splices = [];
    let currentX = 0;
    for (let f of freqs) {
        const pxWidth = (w / 8) / f;
        splices.push({ start: currentX, end: currentX + pxWidth });
        currentX += pxWidth;
    }

    // 2. Prepare paths
    const wavePath = new Path2D();
    let pathStarted = false;

    // 3. Draw both lines using fixed timing (truncates at canvas edge)
    let spliceIdx = 0;
    for (let i = 0; i <= w; i++) {
        // Advance to the correct splice for this pixel
        while (spliceIdx < splices.length && i > splices[spliceIdx].end) {
            spliceIdx++;
        }

        // Truncate if we run out of sequence before the end of the canvas
        if (spliceIdx >= splices.length) break;

        // Amplitude Mapping: 
        // First value (0) applies to first splice. 
        // Subsequent values (1, 2, ...) apply to pairs (1-2, 3-4, ...).
        const ampIdx = (spliceIdx === 0) ? 0 : Math.floor((spliceIdx - 1) / 2) + 1;
        const currentAmp = amps[Math.min(ampIdx, amps.length - 1)];

        const s = splices[spliceIdx];
        const localProgress = (i - s.start) / (s.end - s.start);
        
        // Each splice represents exactly PI radians (half cycle)
        const angle = (spliceIdx * Math.PI) + (localProgress * Math.PI);
        
        // cos(0) starts at 1.0 as requested
        const cosVal = Math.cos(angle);
        
        // 4. Calculate Waveform
        // (cosVal + 1) / 2 maps cos from [1, -1] to [1, 0]
        // Multiplying by currentAmp keeps the bottom at 0 and lowers the top
        const normalizedY = ((cosVal + 1) / 2) * currentAmp;
        const waveY = baseY - (normalizedY * graphH);

        // Map to canvas coordinates
        if (!pathStarted) {
            wavePath.moveTo(i, waveY);
            pathStarted = true;
        } else {
            wavePath.lineTo(i, waveY);
        }
    }

    ctx.lineWidth = 2;
    ctx.lineJoin = 'round';
    ctx.strokeStyle = '#00ffcc'; // Waveform color
    ctx.stroke(wavePath);

    requestAnimationFrame(draw);
}

draw();