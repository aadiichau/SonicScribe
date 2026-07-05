# SonicScribe

Local speech-to-text for Windows, powered by [faster-whisper](https://github.com/SYSTRAN/faster-whisper). Transcribe audio and video on your PC — no cloud, no usage limits.

![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-blue)
![.NET](https://img.shields.io/badge/.NET-8-purple)

## Features

- Unlimited local transcription (MP3, MP4, WAV, M4A, FLAC, MKV, WEBM, and more)
- GPU acceleration with NVIDIA CUDA, CPU fallback
- 99+ languages with auto-detect
- Queue multiple files, review transcripts in-app
- Export TXT, SRT, VTT, JSON
- Searchable history

## Quick start (download release)

1. Download the latest **`SonicScribe-win-x64.zip`** from [Releases](https://github.com/YOUR_USERNAME/SonicScribe/releases).
2. Unzip anywhere (e.g. `Desktop\SonicScribe`).
3. Install prerequisites (see below).
4. Run **`SonicScribe.exe`**.

> Keep all files in the folder together. Do not copy only the `.exe`.

## Prerequisites

| Requirement | Notes |
|---|---|
| Windows 10/11 (64-bit) | Required |
| Python 3.11 or 3.12 | [python.org](https://www.python.org/downloads/) |
| faster-whisper + PyTorch | See install commands below |
| FFmpeg | Recommended for video (`winget install Gyan.FFmpeg`) |

### Python packages

**NVIDIA GPU (CUDA):**

```powershell
pip install faster-whisper torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu124
```

**CPU only:**

```powershell
pip install faster-whisper torch torchvision torchaudio
```

In SonicScribe: **Settings → Auto-detect Python → Re-detect GPU**.

## Build from source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download) and Windows 10/11.

```powershell
git clone https://github.com/YOUR_USERNAME/SonicScribe.git
cd SonicScribe
.\publish.ps1
```

Output: `dist\SonicScribe\SonicScribe.exe`

## Project layout

| Path | Purpose |
|---|---|
| `LocalScribe/` | WinUI 3 app (C#) |
| `LocalScribe/Engine/transcribe_worker.py` | Whisper worker process |
| `publish.ps1` | Build self-contained release |
| `scripts/generate_icons.py` | Regenerate app icons from PNG |

## Data locations

| Data | Path |
|---|---|
| Transcripts | `Documents\SonicScribe\Outputs\` |
| Settings & history | `%LocalAppData%\SonicScribe\` |

## Publishing a GitHub release (for maintainers)

```powershell
.\publish.ps1
Compress-Archive -Path dist\SonicScribe\* -DestinationPath SonicScribe-win-x64.zip -Force
```

Upload `SonicScribe-win-x64.zip` to a new GitHub Release (tag e.g. `v1.0.0`).

## License

MIT — see [LICENSE](LICENSE).