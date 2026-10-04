# VAD segments merged into chunks of up to ~28 s (decoding cost is per 30 s window, not per second),
# each chunk decoded with a forced language. argv: wav, language, model_dir, prefix
import sys, wave, numpy as np, sherpa_onnx
wav, lang, mdir, pre = sys.argv[1:5]
with wave.open(wav) as f:
    x = np.frombuffer(f.readframes(f.getnframes()), dtype=np.int16).astype(np.float32) / 32768
cfg = sherpa_onnx.VadModelConfig()
cfg.silero_vad.model = "silero_vad.onnx"; cfg.silero_vad.threshold = 0.5
cfg.silero_vad.min_silence_duration = 0.4; cfg.silero_vad.min_speech_duration = 0.3
cfg.silero_vad.max_speech_duration = 28; cfg.sample_rate = 16000
vad = sherpa_onnx.VoiceActivityDetector(cfg, buffer_size_in_seconds=60)
win = cfg.silero_vad.window_size; segs = []
i = 0
while i + win <= len(x):
    vad.accept_waveform(x[i:i+win]); i += win
    while not vad.empty():
        s = vad.front; vad.pop(); segs.append((s.start, s.start + len(s.samples)))
vad.flush()
while not vad.empty():
    s = vad.front; vad.pop(); segs.append((s.start, s.start + len(s.samples)))
# merge into chunks <= 28 s of span
chunks = []
for a, b in segs:
    if chunks and (b - chunks[-1][0]) <= 28 * 16000: chunks[-1][1] = b
    else: chunks.append([a, b])
print(f"{len(segs)} segments -> {len(chunks)} chunks", file=sys.stderr, flush=True)
rec = sherpa_onnx.OfflineRecognizer.from_whisper(
    encoder=f"{mdir}/{pre}-encoder.int8.onnx", decoder=f"{mdir}/{pre}-decoder.int8.onnx",
    tokens=f"{mdir}/{pre}-tokens.txt", language=lang, task="transcribe", num_threads=4, tail_paddings=800)
for a, b in chunks:
    s = rec.create_stream(); s.accept_waveform(16000, x[a:b]); rec.decode_stream(s)
    print(f"[{a/16000:6.1f}-{b/16000:6.1f}] {s.result.text.strip()}", flush=True)
print("DONE", flush=True)
