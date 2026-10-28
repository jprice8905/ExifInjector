# ExifInjector

A WinUI 3 desktop app (net10.0, Windows 10/11 only) that writes photo metadata into JPEG files
using [ExifTool](https://exiftool.org/).

It can inject:

| Field | Tags written |
| --- | --- |
| Date taken | `EXIF:DateTimeOriginal`, `EXIF:CreateDate`, `EXIF:ModifyDate` |
| Caption | `IPTC:Caption-Abstract` |
| Keywords | `IPTC:Keywords` (the list is replaced, not appended, so re-runs do not duplicate) |
| GPS location | `EXIF:GPSLatitude`, `EXIF:GPSLatitudeRef`, `EXIF:GPSLongitude`, `EXIF:GPSLongitudeRef` |
| Comment | `EXIF:UserComment` |

Every field is optional — only the fields you fill in are written.

---

## How it is put together

The two projects form a single solution, `ExifInjector.slnx`.

| Project | Contents |
| --- | --- |
| `ExifInjector.Core` | All non-UI logic: the ExifTool wrapper, the injection service, models and the Exif-version compatibility rules. No WinUI references. |
| `ExifInjector.App` | WinUI 3 shell: `MainWindow`, `MainViewModel`, the file picker service and the composition root (`App.xaml.cs`). |

Key types:

- `ExifToolWrapper` (`IExifTool`) — the only code that starts `exiftool.exe`, builds its arguments
  and parses its JSON output.
- `MetadataInjectionService` (`IMetadataInjectionService`) — decides what to write per file, applies
  the skip rules and the date sequence, and reports a `FileInjectionResult` per file.
- `ExifCompatibility` — maps a file's `ExifVersion` to the tags it can safely receive.
- `JpegFileScanner` — folder/selection enumeration, sorted by name so the date sequence is predictable.
- `FilePickerService` (`IFilePickerService`) — the only code that touches the WinRT pickers.

## Usage

1. Pick a date and time (clear **Set date taken** if you only want to write the other fields).
2. Fill in any of caption, keywords, user comment, latitude/longitude (signed decimal degrees).
3. Choose the options you want (see below).
4. Click **Select files…** for individual JPEGs, or **Select folder…** for a whole folder.

Processing starts as soon as the dialog is confirmed. **Cancel** stops the run between files;
files already written stay written.

### Date sequencing

The first file receives the chosen date/time. Each subsequent file that actually gets a date
receives a date **two minutes later** than the previous one, so no two photos share a timestamp.
Skipped files do not consume a slot. The interval is configurable in the UI and defaults to
`Injection:IncrementPerFile` in `appsettings.json`.

### Options

| Option | Effect |
| --- | --- |
| **Skip files that already have a valid Exif date taken** | Files with a parsable `DateTimeOriginal`/`CreateDate` are left completely untouched and logged at INFO. |
| **Older files: write only the date when the other tags are not supported** | For each file, the declared `ExifVersion` is checked. `UserComment` needs Exif ≥ 2.1 and GPS needs Exif ≥ 2.2; unsupported tags are skipped (and logged at INFO) instead of written, so only the date tags change. IPTC is a separate standard and is always written. With the box unchecked, everything is written regardless of Exif version. |
| **Include subfolders** | Applies to **Select folder…** only. |

## Configuration — `appsettings.json`

```json
{
  "ExifTool": {
    "ExecutablePath": "c:\\Tools\\ExifTool.exe",
    "Timeout": "00:02:00",
    "OverwriteOriginal": true
  },
  "Injection": {
    "IncrementPerFile": "00:02:00",
    "IncludeSubfolders": false
  }
}
```

- `ExifTool:ExecutablePath` — full path to `exiftool.exe`. A bare `exiftool.exe` also works when the
  tool is on `PATH`.
- `OverwriteOriginal` — when `true`, ExifTool rewrites files in place instead of leaving `*_original`
  backups. Set it to `false` if you want automatic backups.

### Logging

Serilog is configured entirely from the `Serilog` section of `appsettings.json` and writes
compact JSON (one object per line) to `logs/exif-injector-<date>.jsonl` next to the executable:

```json
{"@t":"2026-10-27T22:21:53.8Z","@mt":"Skipped file: it already has a valid date. File={File} ExistingDate={ExistingDate}","File":"C:\\photos\\a.jpg","ExistingDate":"2012:05:06 07:08:09","Application":"ExifInjector"}
```

Errors (ExifTool failures, unreadable files) are logged at `Error`; skipped files and skipped
unsupported tags are logged at `Information`. The same events are mirrored in the activity list in
the window.

## Build and run

From the repository root:

```powershell
dotnet build ExifInjector.slnx
dotnet run --project ExifInjector.App\ExifInjector.App.csproj
```

The app is unpackaged (`WindowsPackageType=None`) and carries the Windows App SDK with it
(`WindowsAppSDKSelfContained=true`), so no runtime install is needed. It defaults to `win-x64`;
build with `-r win-arm64` on ARM devices.

To publish a self-contained folder you can copy anywhere:

```powershell
dotnet publish ExifInjector.App\ExifInjector.App.csproj -c Release -r win-x64
```
