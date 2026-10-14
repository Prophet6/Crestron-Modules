# File Reader v3.2.1

Part of **[Crestron Modules](https://github.com/Prophet6/Crestron-Modules)**.

A user module plus shared Simpl# library that store digital, analog, and serial signals in a file on the processor and restore them later. Reads detect the file format on their own. Writes use the format you pick. A successful write also copies the inputs to the outputs.

Press **F1** on the user module for the full guide: [`File Reader v3.2.1 Technical Manual.html`](File%20Reader%20v3.2.1%20Technical%20Manual.html). Keep it next to the `.umc`.

This project is not affiliated with Crestron Electronics.

<!-- screenshots -->
## Screenshots

![Screenshot 1](Screenshot%201.png)


## Overview

| File | Role |
|------|------|
| `File Reader v3.2.1.umc` | User module you drop in SIMPL Windows (F1 opens the manual) |
| `File Reader v3.2.1.usp` | Hidden SIMPL+ module (copy-after-write and auto-create) |
| `FileReader_v3.clz` | Simpl# library, class `FileDataHandler`, assembly 3.2.1.0. Source stays `FileReader20261013.cs`. Keep the `.clz` next to the `.usp`. |
| `File Reader/` | Simpl# source (Visual Studio) |

The file path is `File Location` + `Filename` (or `FileName_Override` when it's not empty) + the extension for the active format. Don't put the extension in the name.

## Requirements

- **4-Series** processor (current packaging)
- `FileReader_v3.clz` in the same folder as the SIMPL+ module

## Formats

| Code | Format | Extension |
|-----:|--------|-----------|
| 1 | Text | `.txt` |
| 2 | CSV | `.csv` |
| 3 | XML | `.xml` |
| 4 | JSON | `.json` (default) |
| 5 | INI | `.ini` |
| 6 | Binary | `.bin` |

`Detected_Format_FB` after a read: 1-6, `-1` undetected or error, `-2` integrity check failed (non-JSON). JSON from other tools works: the reader looks for `digitals`, `analogs`, and `serials` arrays (top level, or under `data` / `signalData`). `crc32` is optional.

## Parameters

| Parameter | Role |
|-----------|------|
| File Location | Folder, e.g. `\NVRAM\` or `\CF0\` (with trailing separator) |
| Filename | Name without extension |
| Format | Write format 1-6, default 4 (JSON). The `Format` analog overrides it when 1-6. |
| Default Wait Time | Wait after `Set_Default_Values` before the auto-create write. Default 0.25s (0.01s-10s). |

## Signals

| Input | Role |
|-------|------|
| Read / Write / Delete | Rising edge reads the file, writes the inputs, or deletes the file |
| FileName_Override | Non-empty replaces `Filename` |
| Match_Partial | High treats the name as a fragment: picks the first file (alphabetical, case-insensitive) containing it. No match uses the fragment as the exact name. |
| Encrypt / Encryption_Key | AES on write (16-character key = AES-128). Key also needed to read an encrypted file. |
| Enable_Backup / Enable_Backup_History | Keep a `.bak` on write / archive the old `.bak` as a dated `.gz` |
| Restore_Backup / Delete_Backup | Restore or delete the `.bak` |
| Auto_Create_File | High: at startup or path change, a missing file is created from defaults, an existing file is read (never overwritten) |
| Format_Text ... Format_Binary, Format | Set the write format (1-6) |
| Digital/Analog/Serial_Input_1-64 | Values written to the file (expandable to 256; serials up to 256 chars) |

| Output | Role |
|--------|------|
| Exists_FB / Not_Exist_FB | File present / missing |
| Busy_FB | High during read, write, delete, or auto-create |
| Operational_Feedback | Status or error text |
| FileName_FB / Size_FB | Selected file name with extension / size as text |
| Encrypted_FB / Backup_Exists_FB | File is encrypted / `.bak` exists |
| Set_Default_Values | Pulse when auto-create needs defaults driven on the inputs |
| Format_*_FB, Detected_Format_FB | Detected format of the last read |
| Digital/Analog/Serial_Output_1-64 | Loaded on a good read, copied from inputs on a good write, unchanged on error |

## Usage

1. Add **File Reader v3.2.1**. Set File Location and Filename (no extension). Set Format.
2. Expand the digital, analog, and serial pairs you need.
3. Drive the inputs and pulse **Write**. On success the file is saved and the inputs are copied to the outputs.
4. Pulse **Read** to load the file. On failure the outputs stay where they were.
5. For startup defaults, hold **Auto_Create_File** high and wire **Set_Default_Values** to the logic that drives the default inputs.

Example: Format 4, `\NVRAM\`, `config`, `Analog_Input_1` = volume. Write, then Read at startup to restore it as `config.json`.

## Version History

| Version | Date | Changes |
|---------|------|---------|
| v3.2.1 | 2026-10-13 | `Match_Partial` (partial file-name match), `FileName_FB`. Source `FileReader20261013.cs`. Library `FileReader_v3`, assembly 3.2.1.0. |
| v3.2.0 | 2026-07-16 | Write copies inputs to outputs. `Auto_Create_File`, `Set_Default_Values`, `Default Wait Time`. Library `FileReader20260623`. |
| v3.1.2 | 2026-07-16 | Digital outputs assert reliably after a read. JSON reads accept standard objects; `crc32` optional. |
| v3.1.1 | 2026-05-01 | Text detection uses structured markers (no false Text detects). Parameter formatting removed for user-module wrapping. |
| v3.1 | 2026-02-25 | Added `Not_Exist_FB`. |
| v3.0 | 2025-08-04 | First release, including JSON. Followed (Aug 5-6) by exists/delete/busy/size, integrity, CSV, INI, backup and restore, AES, backup history, Format analog, FileName_Override, Delete_Backup, 1-based format codes. |