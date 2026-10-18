# SmartNotch for Windows 🍎✨

> **SmartNotch** adalah aplikasi utilitas desktop Windows yang membawa pengalaman interaktif **MacBook Notch & Dynamic Island** (1:1 terinspirasi dari konsep **Nusa** oleh `@citoxstd`).

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-blue?logo=windows)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/.NET-10.0%20WPF-purple?logo=dotnet)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)

---

## 📖 Dokumentasi Lengkap
Dokumentasi teknis menyeluruh (arsitektur, alur pembuatan dari awal sampai akhir, Win32 interop, state machine, dan penjelasan teknologi) tersedia di:  
👉 **[DOKUMENTASI_SMARTNOTCH.md](DOKUMENTASI_SMARTNOTCH.md)**

---

## ⚡ Teknologi yang Digunakan & Alasan Pemilihannya

| Teknologi | Peran | Alasan Pemilihan Utama |
|---|---|---|
| **C# 13 & .NET 10** | Core Runtime | Akses native ganda ke Win32 API & WinRT tanpa jembatan (*bridge*). Konsumsi RAM super hemat (~35 MB saat idle) dengan CPU 0.0%. |
| **WPF (DirectComposition)** | UI Framework | Dipilih dibandingkan Electron (terlalu boros RAM 300MB+) dan WinUI 3 (bug jendela transparan). WPF mendukung akselerasi GPU murni dan transparansi per-pixel sempurna (`WS_EX_TRANSPARENT`). |
| **WinRT SMTC** | Now Playing Media | Membaca lagu aktif dari **SEMUA** pemutar (Spotify, Apple Music, YouTube di browser, VLC) tanpa perlu API Key / OAuth developer. |
| **LRCLIB API** | Lirik Lagu Sinkron | Basis data lirik terbuka, gratis, dan menyediakan lirik sinkron bertanda waktu (*timestamped LRC*) tanpa biaya. |
| **WinRT Bluetooth & Audio Endpoint** | Deteksi Headphone/TWS | Memantau koneksi perangkat nirkabel seketika saat AirPods / TWS terhubung ke laptop via `DeviceWatcher`. |
| **Win32 `WM_DEVICECHANGE`** | Deteksi USB & Type-C | Event kernel level rendah untuk mendeteksi tancap/cabut Flashdisk (membaca huruf drive) dan HP dalam hitungan milidetik. |
| **Inno Setup 6** | Compiler Installer | Bebas dari batasan sandbox MSIX yang ketat. Mengompresi seluruh runtime mandiri menjadi installer ringan (~45 MB). |

---

## ✨ Fitur Unggulan

- **Idle Notch Minimalis (1:1 Nusa)**:
  - 🟢 **Circular Battery Ring**: Cincin indikator baterai melingkar di kiri jam dengan ikon petir `⚡` saat charging.
  - 🕒 **Jam Digital Bersih**: Waktu minimalis di tengah.
  - ıll **Harmonic Equalizer Waveform**: 3-batang gelombang audio bersinkronisasi halus (*SineEase* 60 FPS) di kanan saat musik berputar.
- **5 Widget Interaktif (Expanded Panel)**:
  - 🎵 **Musik & Lirik Sinkron**: Artwork album 56px, ambient glow warna dinamis, slider knob, kontrol playback, dan lirik sinkron 3-baris vertikal via LRCLIB.
  - 📁 **File Shelf**: Kotak drop berkas putus-putus berbasis *drag & drop*.
  - ⏱ **Timer Fokus (Pomodoro)**: Pengatur waktu produktivitas 25 menit.
  - 💧 **Pengingat Minum**: Timer hidrasi periodik.
  - 📊 **Pemantau Sistem**: Grafik real-time beban CPU, RAM, dan internet speed.
- **Deteksi Perangkat Keras Otomatis (Peek Banner)**:
  - 🎧 **Bluetooth**: Notifikasi otomatis saat TWS, AirPods, headphone, speaker, atau gamepad terhubung/terputus.
  - 💾 **USB & Type-C**: Deteksi instan saat Flashdisk, smartphone HP, atau periferal dicolokkan/dicabut.
  - ⚡ **Pengisi Daya**: Notifikasi real-time saat charger dicolok atau dicabut.
- **Transisi Geser Halus (*Slide & Fade*)**:
  - Perpindahan antar halaman menggunakan animasi horizontal *Cubic Ease-Out* yang sangat lembut via tombol `‹` / `›`, scroll roda mouse, atau titik paginasi.

---

## 🚀 Unduh & Pasang

- **Installer Setup**: [SmartNotch-Setup-1.0.0.exe](dist/SmartNotch-Setup-1.0.0.exe)
- **Portable Langsung Jalan**: [SmartNotch.exe](dist/publish/SmartNotch.exe)

---

## 🛠️ Build dari Kode Sumber

Pastikan Anda telah memasang **.NET 10 SDK**. Jalankan skrip PowerShell berikut:

```powershell
# Build dan compile installer setup otomatis
.\build-installer.ps1
```
