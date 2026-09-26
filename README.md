# RFTool — project dasar dari Cheat Table

Project WinForms .NET 8 untuk memulai migrasi struktur `.CT` menjadi aplikasi `.EXE`.

## Isi yang sudah disiapkan
- Project Visual Studio / `dotnet` untuk Windows.
- UI sederhana dengan process attach dan daftar fitur.
- Katalog fitur statis yang diekstrak dari CT.
- Lapisan `ProcessMemory` dan `FeatureService` terpisah.
- Struktur siap dikembangkan menjadi tab Player/NPC/Stage 2/Combine.

## Catatan penting
- `Auto NPC`, `Stage 2`, dan `Combine Jarak Jauh` belum diaktifkan sebagai fitur otomatis; ketiganya memerlukan implementasi pattern scan, validasi signature, penyimpanan nilai asli, dan pemulihan yang aman.
- Offset `RF_Online.bin+C28B40` diperlakukan sebagai module base + offset. Verifikasi kembali terhadap versi target sebelum dipakai.
- Gunakan hanya pada software/game yang Anda miliki atau memiliki izin untuk diuji.

## Build di Windows
```powershell
dotnet restore .\RFTool\RFTool.csproj
dotnet build .\RFTool\RFTool.csproj -c Release

dotnet publish .\RFTool\RFTool.csproj -c Release -r win-x86 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

Output berada di `RFTool/bin/Release/net8.0-windows/win-x86/publish/`.

## GitHub Actions
Tambahkan workflow Windows runner yang menjalankan `dotnet publish` di atas lalu mengunggah folder `publish` sebagai artifact.
