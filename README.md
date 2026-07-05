<p align="center">
  <img src="docs/icon.png" alt="SonicScribe" width="128" />
</p>

<h1 align="center">SonicScribe</h1>

<p align="center">
  <strong>Local speech-to-text for Windows — private, unlimited, GPU-accelerated.</strong>
</p>

<p align="center">
  <a href="https://github.com/aadiichau/SonicScribe/releases/latest">
    <img src="https://img.shields.io/github/v/release/aadiichau/SonicScribe?label=Download&style=for-the-badge" alt="Download" />
  </a>
  <img src="https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4?style=for-the-badge&logo=windows&logoColor=white" alt="Windows" />
  <img src="https://img.shields.io/badge/.NET-8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8" />
  <img src="https://img.shields.io/badge/Whisper-large--v3-10B981?style=for-the-badge" alt="Whisper" />
  <img src="https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge" alt="MIT" />
</p>

<p align="center">
  Transcribe audio and video on your own PC with <a href="https://github.com/SYSTRAN/faster-whisper">faster-whisper</a>.
  No cloud. No accounts. No usage caps.
</p>

---

<p align="center">
  <img src="docs/screenshot.png" alt="SonicScribe Transcribe screen" width="900" />
</p>

---

## Download

| Package | Description |
|---------|-------------|
| [**Portable (recommended)**](https://github.com/aadiichau/SonicScribe/releases/latest/download/SonicScribe-v1.0.0-Portable-win-x64.zip) | Unzip anywhere → run `SonicScribe.exe`. No installer. |
| [**Mirror zip**](https://github.com/aadiichau/SonicScribe/releases/latest/download/SonicScribe-win-x64.zip) | Same portable build, shorter filename. |

| | |
|---|---|
| **Size** | ~75 MB zipped (~200 MB extracted) |
| **Platform** | Windows 10/11, 64-bit |
| **Type** | Self-contained portable app (not a single-file exe) |

1. Download the **Portable** zip
2. Unzip to any folder (e.g. `Desktop\SonicScribe`)
3. Install Python prerequisites (below)
4. Run `SonicScribe.exe` or `Start SonicScribe.bat`

> **Important:** Keep every file in the folder together. The `.exe` needs the DLLs beside it.

---

## Features

| Feature | Description |
|---------|-------------|
| **Fully local** | Audio never leaves your machine |
| **GPU accelerated** | NVIDIA CUDA support with automatic CPU fallback |
| **99+ languages** | Auto-detect or pick a language manually |
| **Batch queue** | Drop multiple files and process them in order |
| **In-app review** | Read transcripts with optional timestamps |
| **Exports** | TXT, SRT, VTT, and JSON |
| **History** | Search and revisit past transcriptions |

**Supported formats:** MP3, MP4, WAV, M4A, FLAC, MKV, WEBM, and more (FFmpeg recommended for video).

---

## Prerequisites

SonicScribe is a desktop shell around Whisper. Users still need Python + ML libraries on their PC (one-time setup).

### 1. Python 3.11 or 3.12

Download from [python.org](https://www.python.org/downloads/) and check **"Add Python to PATH"** during install.

### 2. Whisper + PyTorch

**NVIDIA GPU (recommended):**

```powershell
pip install faster-whisper torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu124
```

**CPU only:**

```powershell
pip install faster-whisper torch torchvision torchaudio
```

### 3. FFmpeg (recommended for video)

```powershell
winget install Gyan.FFmpeg
```

### 4. First launch

Open **Settings** in SonicScribe → **Auto-detect Python** → **Re-detect GPU** (if you have an NVIDIA card).

---

## Quick start

```
1. Download SonicScribe-win-x64.zip from Releases
2. Unzip to Desktop\SonicScribe (or anywhere)
3. Install Python packages (see above)
4. Double-click SonicScribe.exe
5. Drop an audio/video file → Start
```

Transcripts save to `Documents\SonicScribe\Outputs\`.

---

## Build from source

For developers who want to compile locally:

**Requirements:** Windows 10/11, [.NET 8 SDK](https://dotnet.microsoft.com/download)

```powershell
git clone https://github.com/aadiichau/SonicScribe.git
cd SonicScribe
.\publish.ps1
```

Output: `dist\SonicScribe\SonicScribe.exe`

---

## Project structure

```
SonicScribe/
├── LocalScribe/              # WinUI 3 app (C#)
│   ├── Engine/               # Python Whisper worker
│   ├── Views/                # UI pages
│   └── Assets/               # App icons
├── publish.ps1               # Build standalone release
├── release.ps1               # Build + zip for GitHub Releases
└── scripts/generate_icons.py # Regenerate icons from PNG
```

---

## Data & privacy

| Data | Location |
|------|----------|
| Transcript exports | `%USERPROFILE%\Documents\SonicScribe\Outputs\` |
| Settings & history | `%LOCALAPPDATA%\SonicScribe\` |

Everything stays on your computer. No telemetry, no cloud API calls for transcription.

---

## Contributing

Issues and pull requests are welcome. For bugs, include your Windows version, GPU model, Python version, and the file type you tried to transcribe.

---

## License

[MIT](LICENSE) — free to use, modify, and share.

---

<p align="center">
  <sub>Built with WinUI 3 · faster-whisper · OpenAI Whisper</sub>
</p>