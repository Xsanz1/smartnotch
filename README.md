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
| **SQLite (`Microsoft.Data.Sqlite`)** | Pembaca Notifikasi Windows | Membaca database notifikasi lokal (`wpndatabase.db`) secara non-intrusif sehingga pesan WhatsApp & app lain terbaca tanpa batasan paket UWP/MSIX. |
| **WASAPI & Core Audio API** | Audio & Deteksi Panggilan | Mengontrol volume hardware master dan mendeteksi sesi mikrofon aktif saat telepon berlangsung. |
| **WinRT Bluetooth & Audio Endpoint** | Deteksi Headphone/TWS | Memantau koneksi perangkat nirkabel seketika saat AirPods / TWS terhubung ke laptop via `DeviceWatcher`. |
| **Win32 `WM_DEVICECHANGE`** | Deteksi USB & Type-C | Event kernel level rendah untuk mendeteksi tancap/cabut Flashdisk (membaca huruf drive) dan HP dalam hitungan milidetik. |
| **Inno Setup 6** | Compiler Installer | Bebas dari batasan sandbox MSIX yang ketat. Mengompresi seluruh runtime mandiri menjadi installer ringan (~45 MB). |

---

## ✨ Fitur Unggulan

- **Idle Notch Minimalis (1:1 Nusa)**:
  - 🟢 **Circular Battery Ring**: Cincin indikator baterai melingkar di kiri jam dengan ikon petir `⚡` saat charging.
  - 🕒 **Jam Digital Bersih**: Waktu minimalis di tengah (otomatis beralih ke hitungan mundur saat timer fokus aktif).
  - ıll **Harmonic Equalizer Waveform**: 3-batang gelombang audio bersinkronisasi halus (*SineEase* 60 FPS) di kanan saat musik berputar.
  - 📞 **Dynamic Island Call Pill**: Saat panggilan aktif, notch memanjang menjadi kapsul hijau elegan dengan avatar kontak, nama penelepon, timer durasi, dan gelombang suara hijau.
- **7 Widget Interaktif (Expanded Panel)**:
  - 🎵 **Musik & Lirik Sinkron**: Artwork album 56px, ambient glow warna dinamis, slider volume hardware interaktif, slider progress, kontrol playback, dan lirik sinkron 3-baris vertikal via LRCLIB.
  - 📞 **Kartu Panggilan (Call Card)**: Tampilan panggilan suara/video interaktif dengan tombol "Buka Aplikasi", tombol Mute/Unmute mic, gelombang suara, dan timer berjalan.
  - 💬 **Pesan & Notifikasi**: Menampilkan daftar pesan WhatsApp dan notifikasi masuk dengan badge kontak, waktu, teks pesan lengkap, tombol bersihkan, serta **dapat diklik langsung (`↗`) untuk membuka aplikasi pengirim**.
  - 📁 **File Shelf**: Kotak drop berkas putus-putus berbasis *drag & drop*.
  - ⏱ **Timer Fokus (Pomodoro)**: Pengatur waktu produktivitas dengan preset (5, 15, 25, 50 mnt), durasi kustom, dan notifikasi audio saat selesai.
  - 💧 **Pengingat Minum**: Timer hidrasi periodik yang menyimpan asupan harian otomatis di-reset saat ganti hari.
  - 📊 **Pemantau Sistem**: Grafik real-time beban CPU, RAM, dan internet download/upload speed.
- **Deteksi Perangkat Keras & Pesan Otomatis (Peek Banner)**:
  - 💬 **Pesan WhatsApp Masuk**: Banner pop-up 2-baris menampilkan nama pengirim dan isi teks pesan secara instan.
  - 🎯 **Klik Notifikasi untuk Membuka Aplikasi**: Mengklik banner notifikasi Peek atau kartu di panel widget akan langsung mengarahkan dan memfokuskan jendela aplikasi pengirim (WhatsApp, Telegram, Discord, Teams, dll.) ke layar depan. Dilengkapi *smart hover* (timer auto-close jeda saat kursor di atas notif, dan otomatis membuka panel Pesan jika ditahan > 1.2 detik).
  - 📞 **Panggilan Masuk**: Notifikasi saat panggilan suara/video WhatsApp, Zoom, Teams, Discord, atau Skype berdering.
  - 🎧 **Bluetooth**: Notifikasi otomatis saat TWS, AirPods, headphone, speaker, atau gamepad terhubung/terputus.
  - 💾 **USB & Type-C**: Deteksi instan saat Flashdisk, smartphone HP, atau periferal dicolokkan/dicabut.
  - ⚡ **Pengisi Daya**: Notifikasi real-time saat charger dicolok atau dicabut.
- **Transisi Geser Halus (*Slide & Fade*)**:
  - Perpindahan antar halaman menggunakan animasi horizontal *Cubic Ease-Out* yang sangat lembut via tombol `‹` / `›`, scroll roda mouse, atau titik paginasi, dengan proteksi anti-blank pada scroll cepat.

---

## 🚀 Unduh & Pasang

- **Installer Setup**: [SmartNotch-Setup-1.1.0.exe](dist/SmartNotch-Setup-1.1.0.exe)
- **Portable Langsung Jalan**: [SmartNotch.exe](dist/publish/SmartNotch.exe)

---

## 🛠️ Build dari Kode Sumber

Pastikan Anda telah memasang **.NET 10 SDK**. Jalankan skrip PowerShell berikut:

```powershell
# Build dan compile installer setup otomatis
.\build-installer.ps1
```

### Menjalankan Unit Test

Proyek dilengkapi dengan 32 test cases otomatis (xUnit):

```powershell
dotnet test tests\SmartNotch.Tests
```

---

## ⚙️ Pengaturan, Multi-Monitor & Log (v1.1)

- **Jendela Pengaturan**: Akses via klik kanan ikon tray → *Pengaturan...*.
  - Atur widget mana saja yang ingin ditampilkan di panel.
  - Kustomisasi durasi timer fokus & bunyi alarm.
  - Atur interval pengingat minum dan target harian (ml).
  - Pilihan monitor tampilan notch (Multi-Monitor support).
  - Atur hotkey global (default: `Ctrl + Alt + N`).
  - Atur jeda otomatis menutup (*collapse delay*) saat kursor keluar.
  - Atur **Opasitas Notch (70% - 100%)** untuk efek semi-transparan elegan.
  - Toggle **Bunyi Bel Notifikasi Pesan Masuk** (audio chime lembut).
  - Toggle **Mulai Otomatis Bersama Windows (Autostart)**.
  - Daftar aplikasi panggilan yang dipantau (WhatsApp, Zoom, Teams, Discord, Skype).
  - Pengaturan tersimpan otomatis di `%LOCALAPPDATA%\SmartNotch\settings.json`.
- **Fitur Balas Cepat (*Quick Reply*)**:
  - Pada tab Pesan, klik tombol **`↩ Balas`** pada kartu pesan untuk memunculkan kolom teks balasan cepat. Tekan Enter atau klik `➤ Kirim` untuk menyalin balasan dan langsung memfokuskan WhatsApp.
- **Pengoptimal Memori Otomatis (*Memory Trimmer*)**:
  - Menggunakan API kernel `EmptyWorkingSet` pasca-startup dan setiap kali notch menutup untuk menjaga penggunaan RAM idle tetap super ringan (~35-45 MB).
- **Dukungan Multi-Monitor**: Penempatan notch berbasis piksel fisik DPI-aware, otomatis menyesuaikan saat monitor diputus/disambung.
- **Sensor Fullscreen Cerdas**: Notch otomatis sembunyi saat game atau aplikasi layar penuh dibuka hanya pada monitor yang bersangkutan.
- **Sistem Logging**: Log operasi dan error otomatis disimpan di `%LOCALAPPDATA%\SmartNotch\logs` dengan retensi otomatis 7 hari.
- **Cek Pembaruan**: Otomatis memeriksa rilis terbaru di GitHub Releases saat aplikasi dijalankan.

