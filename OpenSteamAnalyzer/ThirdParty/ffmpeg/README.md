# FFmpeg native libraries

Put the Windows x64 FFmpeg shared DLLs in this directory.

Required examples:

- `avcodec-*.dll`
- `avformat-*.dll`
- `avutil-*.dll`

Files in this folder are copied to the application output directory during build, and the app loads Flyleaf/FFmpeg from that copied `ThirdParty/ffmpeg` folder.
