#!/usr/bin/env python3
"""
SonicScribe faster-whisper worker.

Reads one JSON command per stdin line and emits one JSON event per stdout line.
The worker process stays alive across jobs so the Whisper model can be reused.
"""

from __future__ import annotations

import io
import json
import sys
import time

# Windows defaults to cp1252; force UTF-8 so CJK transcript text can be emitted.
if hasattr(sys.stdout, "buffer"):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", line_buffering=True)
if hasattr(sys.stderr, "buffer"):
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", line_buffering=True)
import traceback
from pathlib import Path

_model_instance = None
_current_model_size: str | None = None


def emit(payload: dict) -> None:
    print(json.dumps(payload, ensure_ascii=False), flush=True)


def fmt_dur(seconds: float) -> str:
    minutes = int(seconds // 60)
    secs = int(seconds % 60)
    return f"{minutes}:{secs:02d}"


def detect_device() -> tuple[str, str, str]:
    try:
        import torch

        if torch.cuda.is_available():
            return "cuda", "float16", torch.cuda.get_device_name(0)
    except ImportError:
        pass
    return "cpu", "int8", "CPU"


def get_model(model_size: str) -> tuple[object, str, str, str]:
    global _model_instance, _current_model_size

    from faster_whisper import WhisperModel

    device, compute_type, gpu_name = detect_device()

    if _model_instance is None or _current_model_size != model_size:
        emit(
            {
                "type": "status",
                "status": "loading_model",
                "progress": 5,
                "log": f"Loading {model_size} on {gpu_name}...",
                "device": device.upper(),
                "gpu_name": gpu_name,
            }
        )
        _model_instance = WhisperModel(model_size, device=device, compute_type=compute_type)
        _current_model_size = model_size

    return _model_instance, device, compute_type, gpu_name


def transcribe(command: dict) -> None:
    file_path = command.get("file")
    model_size = command.get("model", "large-v3")
    language = command.get("language") or "auto"
    job_id = command.get("job_id", "")

    if not file_path or not Path(file_path).exists():
        raise FileNotFoundError(f"Audio file not found: {file_path}")

    emit({"type": "status", "status": "loading_model", "progress": 3, "log": "Loading model..."})

    model, device, _, gpu_name = get_model(model_size)

    emit(
        {
            "type": "status",
            "status": "transcribing",
            "progress": 10,
            "log": f"Transcribing on {device.upper()}...",
            "device": device.upper(),
            "gpu_name": gpu_name,
            "job_id": job_id,
        }
    )

    lang_param = None if str(language).lower() == "auto" else language
    segments_list: list[dict] = []
    start_time = time.time()

    segments, info = model.transcribe(
        str(file_path),
        language=lang_param,
        beam_size=1,
        vad_filter=True,
        vad_parameters={"min_silence_duration_ms": 500},
    )

    detected_lang = info.language
    duration = info.duration or 0.0

    for segment in segments:
        segment_data = {
            "start": round(segment.start, 2),
            "end": round(segment.end, 2),
            "text": segment.text.strip(),
        }
        segments_list.append(segment_data)

        if duration > 0:
            progress = int(10 + (segment.end / duration) * 87)
            elapsed = time.time() - start_time
            speed = segment.end / elapsed if elapsed > 0 else 0.0
            remaining = (duration - segment.end) / speed if speed > 0 else 0.0
            emit(
                {
                    "type": "progress",
                    "status": "transcribing",
                    "progress": min(progress, 96),
                    "log": (
                        f"Transcribing {fmt_dur(segment.end)} / {fmt_dur(duration)}"
                        f"  ·  {speed:.1f}x  ·  ~{fmt_dur(remaining)} left"
                    ),
                    "detected_language": detected_lang,
                    "duration": round(duration, 1),
                    "job_id": job_id,
                }
            )

    elapsed_total = time.time() - start_time
    realtime = duration / elapsed_total if elapsed_total > 0 else 0.0

    emit(
        {
            "type": "status",
            "status": "transcribing",
            "progress": 97,
            "log": f"Finalizing {len(segments_list)} segments...",
            "detected_language": detected_lang,
            "duration": round(duration, 1),
            "job_id": job_id,
        }
    )

    emit(
        {
            "type": "done",
            "status": "done",
            "progress": 100,
            "log": f"Done in {elapsed_total / 60:.1f} min  ·  {realtime:.1f}x realtime",
            "detected_language": detected_lang,
            "duration": round(duration, 1),
            "segment_count": len(segments_list),
            "segments": segments_list,
            "elapsed": round(elapsed_total, 1),
            "elapsed_min": round(elapsed_total / 60, 1),
            "device": device.upper(),
            "gpu_name": gpu_name,
            "job_id": job_id,
        }
    )


def handle_command(command: dict) -> bool:
    cmd = command.get("cmd")

    if cmd == "shutdown":
        emit({"type": "status", "status": "shutdown", "log": "Worker shutting down."})
        return False

    if cmd == "ping":
        emit({"type": "pong", "status": "ready", "log": "Worker ready."})
        return True

    if cmd == "transcribe":
        transcribe(command)
        return True

    emit({"type": "error", "status": "error", "error": f"Unknown command: {cmd}", "log": f"Unknown command: {cmd}"})
    return True


def main() -> int:
    emit({"type": "status", "status": "ready", "log": "Worker started."})

    for raw_line in sys.stdin:
        line = raw_line.strip()
        if not line:
            continue

        try:
            command = json.loads(line)
        except json.JSONDecodeError as exc:
            emit({"type": "error", "status": "error", "error": str(exc), "log": f"Invalid JSON command: {exc}"})
            continue

        try:
            if not handle_command(command):
                break
        except Exception as exc:  # noqa: BLE001
            traceback.print_exc()
            err = str(exc)
            hint = " — Reinstall CUDA PyTorch" if any(token in err.lower() for token in ("cublas", "cudnn", "cuda")) else ""
            emit(
                {
                    "type": "error",
                    "status": "error",
                    "error": err + hint,
                    "log": f"Error: {err}",
                    "job_id": command.get("job_id"),
                }
            )

    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        emit({"type": "error", "status": "cancelled", "error": "Cancelled", "log": "Cancelled"})
        raise SystemExit(130) from None