# Transcribing a ride-along video's audio (research tool)

For videos whose captions drop the Bangla ("[Music]" where the driver speaks). Runs on CPU, no
cloud. About 3× real time on four cores with the large model.

1. `pip install sherpa-onnx numpy` and have `ffmpeg`.
2. Models from the sherpa-onnx GitHub release `asr-models`: `sherpa-onnx-whisper-large-v3.tar.bz2`
   (1 GB) and `silero_vad.onnx`, unpacked next to the script.
3. `ffmpeg -i video.mp3 -ac 1 -ar 16000 day.wav`
4. `python3 transcribe.py day.wav en sherpa-onnx-whisper-large-v3 large-v3`

Known limits (4 Oct 2026): the sherpa-onnx build drops Bangla and Devanagari characters, so a
forced `bn` decode comes back empty. Forcing `en` makes Whisper *translate* the Bangla spans to
English instead; that is rough machine translation under background music, usable for what a
speaker is talking about, not for quoting. Short pieces (under 12 s) hallucinate less than long
chunks; a 200–3800 Hz band-pass thins the music. Mark everything from it unconfirmed.
