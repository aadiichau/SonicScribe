# SonicScribe

Local speech-to-text for Windows. Drop an audio or video file, get a transcript. Runs on your machine with [faster-whisper](https://github.com/SYSTRAN/faster-whisper) — nothing gets uploaded anywhere.

![SonicScribe screenshot](docs/screenshot.png)

## Download

**[Get the latest release](https://github.com/aadiichau/SonicScribe/releases/latest)**

| File | What it is |
|------|------------|
| `SonicScribe-Setup-v1.0.0.exe` | Normal Windows installer (~70 MB). Start Menu shortcut, uninstaller, the works. |
| `SonicScribe-v1.0.0-Portable-win-x64.zip` | Portable build. Unzip and run `SonicScribe.exe`. Keep the whole folder together. |

Windows 10 or 11, 64-bit.

## First time setup

SonicScribe needs Python and a few packages before it can transcribe. Easiest path:

1. Install and open SonicScribe
2. Accept **Install everything** on first launch (or go to **Settings → Install everything**)
3. Wait — Python, PyTorch, and faster-whisper can take 10–30 minutes on a slow connection
4. Drop a file on the Transcribe page and hit **Start**

The first transcription also downloads the Whisper model (up to ~3 GB for large-v3). There's a progress bar. If it sits at the same percent for a while, that's normal — big downloads come in chunks.

### Manual install (if you prefer)

Python 3.11 or 3.12 from [python.org](https://www.python.org/downloads/) — tick "Add to PATH".

**With an NVIDIA GPU:**
```powershell
pip install faster-whisper torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu124
```

**CPU only:**
```powershell
pip install faster-whisper torch torchvision torchaudio
```

**Video files (optional):**
```powershell
winget install Gyan.FFmpeg
```

Then in SonicScribe: **Settings → Auto-detect Python → Re-detect GPU → Save**.

## What you get

- Transcribe MP3, MP4, WAV, M4A, FLAC, MKV, WEBM, and more
- GPU acceleration when CUDA is available
- Queue multiple files
- Pick the model on the Transcribe page (large-v3, medium, small, base)
- Export TXT, SRT, VTT, JSON
- History of past jobs

Transcripts land in `Documents\SonicScribe\Outputs`. Settings and history are in `%LocalAppData%\SonicScribe`.

## Build from source

Needs Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/aadiichau/SonicScribe.git
cd SonicScribe
.\publish.ps1
```

Output is in `dist\SonicScribe\`.

To cut a release zip + installer:
```powershell
.\release.ps1 -Version v1.0.0
.\publish-release.ps1 -Version v1.0.0
```

## Problems?

**App won't start** — run the installer or keep the portable folder intact (don't move just the `.exe`). Check `%LocalAppData%\SonicScribe\logs\crash.log` if it dies on launch.

**Won't transcribe** — Python or faster-whisper probably isn't set up. Use **Settings → Install everything** or install the packages manually.

**Transcription error mentioning model.bin** — corrupted model cache. Hit **Repair model & retry** on the error screen, or delete the folder under `%USERPROFILE%\.cache\huggingface\hub\` that mentions `faster-whisper` and try again.

## License

MIT — see [LICENSE](LICENSE).

Made by [@aadiichau](https://github.com/aadiichau).