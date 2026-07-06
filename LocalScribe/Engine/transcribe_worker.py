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
import threading
import time
import traceback
from pathlib import Path

# Windows defaults to cp1252; force UTF-8 so CJK transcript text can be emitted.
if hasattr(sys.stdout, "buffer"):
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", line_buffering=True)
if hasattr(sys.stderr, "buffer"):
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", line_buffering=True)

_model_instance = None
_current_model_size: str | None = None
_current_device: str | None = None
_current_compute_type: str | None = None

MODEL_DOWNLOAD_HINTS = {
    "large-v3": "~3 GB",
    "medium": "~1.5 GB",
    "small": "~500 MB",
    "base": "~150 MB",
}

MODEL_EXPECTED_BYTES = {
    "large-v3": 3_000_000_000,
    "large-v2": 3_000_000_000,
    "large": 3_000_000_000,
    "medium": 1_500_000_000,
    "small": 500_000_000,
    "base": 150_000_000,
}


def hf_cache_root() -> Path:
    return Path.home() / ".cache" / "huggingface" / "hub"


def hf_repo_name(model_size: str) -> str:
    return f"models--Systran--faster-whisper-{model_size}"


def directory_size(path: Path) -> int:
    if not path.exists():
        return 0

    total = 0
    for child in path.rglob("*"):
        if child.is_file():
            try:
                total += child.stat().st_size
            except OSError:
                pass
    return total


def model_download_state(model_size: str) -> tuple[int, int, int]:
    expected = MODEL_EXPECTED_BYTES.get(model_size, 1_000_000_000)
    repo_path = hf_cache_root() / hf_repo_name(model_size)
    downloaded = directory_size(repo_path)
    percent = int(min(99, round((downloaded / expected) * 100))) if expected > 0 else 0
    return downloaded, expected, percent


def fmt_bytes(num_bytes: int) -> str:
    if num_bytes >= 1_000_000_000:
        return f"{num_bytes / 1_000_000_000:.1f} GB"
    if num_bytes >= 1_000_000:
        return f"{num_bytes / 1_000_000:.0f} MB"
    return f"{num_bytes / 1_000:.0f} KB"


def emit(payload: dict) -> None:
    print(json.dumps(payload, ensure_ascii=False), flush=True)


def fmt_dur(seconds: float) -> str:
    minutes = int(seconds // 60)
    secs = int(seconds % 60)
    return f"{minutes}:{secs:02d}"


def detect_device() -> tuple[str, str, str, float]:
    try:
        import torch

        if torch.cuda.is_available():
            props = torch.cuda.get_device_properties(0)
            vram_gb = props.total_memory / (1024**3)
            return "cuda", props.name, "CUDA", round(vram_gb, 1)
    except ImportError:
        pass
    return "cpu", "CPU", "CPU", 0.0


def pick_compute_types(device: str, vram_gb: float) -> list[str]:
    if device != "cuda":
        return ["int8", "float32"]

    if vram_gb < 6:
        return ["int8_float16", "int8", "float16"]
    if vram_gb < 8:
        return ["int8_float16", "float16", "int8"]
    return ["float16", "int8_float16", "int8"]


def device_is_low_vram(vram_gb: float) -> bool:
    return 0 < vram_gb < 6


def get_model(model_size: str) -> tuple[object, str, str, str]:
    global _model_instance, _current_model_size, _current_device, _current_compute_type

    from faster_whisper import WhisperModel

    device, gpu_name, device_label, vram_gb = detect_device()

    if device_is_low_vram(vram_gb) and model_size in {"large-v3", "large-v2", "large"}:
        emit(
            {
                "type": "status",
                "status": "loading_model",
                "progress": 4,
                "log": (
                    f"{gpu_name} has {vram_gb:.1f} GB VRAM — loading {model_size} "
                    f"with memory-efficient settings. First run downloads "
                    f"{MODEL_DOWNLOAD_HINTS.get(model_size, 'several GB')}."
                ),
                "device": device_label,
                "gpu_name": gpu_name,
            }
        )

    if (
        _model_instance is not None
        and _current_model_size == model_size
        and _current_device == device
        and _current_compute_type is not None
    ):
        return _model_instance, device, _current_compute_type, gpu_name

    download_hint = MODEL_DOWNLOAD_HINTS.get(model_size, "several hundred MB")
    compute_types = pick_compute_types(device, vram_gb)
    last_error: Exception | None = None

    for compute_type in compute_types:
        emit(
            {
                "type": "status",
                "status": "loading_model",
                "progress": 5,
                "log": (
                    f"Loading {model_size} on {gpu_name} ({compute_type})... "
                    f"First run downloads {download_hint} — can take 5–30 min on slow internet."
                ),
                "device": device_label,
                "gpu_name": gpu_name,
            }
        )

        try:
            loaded = _load_model_with_heartbeat(model_size, device, compute_type, gpu_name, download_hint)
            _model_instance = loaded
            _current_model_size = model_size
            _current_device = device
            _current_compute_type = compute_type

            emit(
                {
                    "type": "status",
                    "status": "loading_model",
                    "progress": 9,
                    "log": f"Model ready ({model_size}, {compute_type}).",
                    "device": device_label,
                    "gpu_name": gpu_name,
                }
            )
            return _model_instance, device, compute_type, gpu_name
        except Exception as exc:  # noqa: BLE001
            last_error = exc
            emit(
                {
                    "type": "status",
                    "status": "loading_model",
                    "progress": 5,
                    "log": f"{compute_type} failed ({exc}). Trying next option...",
                    "device": device_label,
                    "gpu_name": gpu_name,
                }
            )
            _model_instance = None
            _current_model_size = None
            _current_device = None
            _current_compute_type = None

    if last_error is not None:
        raise last_error

    raise RuntimeError(f"Could not load model {model_size}")


def _load_model_with_heartbeat(
    model_size: str,
    device: str,
    compute_type: str,
    gpu_name: str,
    download_hint: str,
) -> object:
    from faster_whisper import WhisperModel

    result: dict[str, object] = {}
    error: dict[str, Exception] = {}
    done = threading.Event()

    def load() -> None:
        try:
            result["model"] = WhisperModel(model_size, device=device, compute_type=compute_type)
        except Exception as exc:  # noqa: BLE001
            error["exc"] = exc
        finally:
            done.set()

    thread = threading.Thread(target=load, daemon=True)
    thread.start()

    started = time.time()
    heartbeat = 0
    while not done.wait(timeout=5):
        heartbeat += 1
        elapsed = int(time.time() - started)
        downloaded, expected, download_percent = model_download_state(model_size)
        mapped_progress = 5 + int(download_percent * 0.85) if download_percent > 0 else min(8, 5 + heartbeat)

        if download_percent > 0 and downloaded < expected:
            log = (
                f"Downloading {model_size}: {download_percent}% "
                f"({fmt_bytes(downloaded)} / {fmt_bytes(expected)})"
            )
        else:
            log = (
                f"Still loading {model_size}... {elapsed}s elapsed. "
                f"First run downloads {download_hint} — please wait."
            )

        emit(
            {
                "type": "progress",
                "status": "loading_model",
                "progress": mapped_progress,
                "download_percent": download_percent,
                "download_bytes": downloaded,
                "download_total": expected,
                "log": log,
                "device": device.upper(),
                "gpu_name": gpu_name,
            }
        )

    thread.join(timeout=1)
    if "exc" in error:
        raise error["exc"]
    return result["model"]


def transcribe(command: dict) -> None:
    file_path = command.get("file")
    model_size = command.get("model", "large-v3")
    language = command.get("language") or "auto"
    job_id = command.get("job_id", "")

    if not file_path or not Path(file_path).exists():
        raise FileNotFoundError(f"Audio file not found: {file_path}")

    emit({"type": "status", "status": "loading_model", "progress": 3, "log": "Preparing transcription..."})

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