# Compile SIMPL+ from the CLI (SPlusCC)

Use this whenever a `.usp` module was edited and you need to verify it builds for 3-Series / 4-Series processors. Prefer this over assuming the module is correct from reading source alone.

## Tool

| Item | Value |
|------|--------|
| Executable | `C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe` |
| Also valid | `C:\Program Files (x86)\Crestron\SIMPL\SPlusCC.exe` (same tool) |
| Host | Windows PowerShell (or PowerShell ISE) |

Confirm the tool exists before relying on it:

```powershell
Test-Path "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe"
```

## Command patterns

### From the module’s folder

```powershell
& "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
  \rebuild "MyModule.usp" `
  \target series3 series4
```

### From any directory (full paths)

```powershell
& "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
  \rebuild "C:\Users\proph\OneDrive\Work Files\_dev\Crestron-Modules\power-shutdown-confirmation\Power Shutdown Confirmation v1.0.usp" `
  \target series3 series4
```

### Capture full output to a log file

```powershell
& "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
  \rebuild "C:\Users\proph\Grok\Crestron\Power Shutdown Confirmation v1.0.usp" `
  \target series3 series4 `
  \out "C:\Users\proph\Grok\Crestron\compile-log.txt"
```

Always quote paths that contain spaces.

## Flags (SPlusCC)

| Flag | Purpose |
|------|---------|
| `\?` | Help |
| `\build <module [module…]>` | Compile listed module(s) |
| `\rebuild <module [module…]>` | Force recompile (preferred after edits) |
| `\target <device [device…]>` | Targets: `series2`, `series3`, `series4` |
| `\out <file>` | Write all compiler output to a file |
| `\usersplusfolder <folder>` | User SIMPL+ folder |
| `\silent` | Suppress console output |
| `\errorcodes` | List compilation error codes |

Built-in help text may only mention `series2 | series3`, but **`series4` is valid** and should be used for 4-Series work.

### Recommended targets for this project

```text
\target series3 series4
```

Include `series2` only when the module must still support 2-Series.

## Workflow for AI / future sessions

1. Edit the `.usp` (CRLF is typical for Crestron; avoid `//` comments in logic — use `/* */`).
2. Run `SPlusCC` with `\rebuild` and `\target series3 series4`.
3. If compile fails, read the console or `\out` log; fix errors; rebuild.
4. **0 SIMPL+ errors is not always the whole story** for modules that reference Simpl# (`.clz`): build the Simpl# library first, then the `.usp`. Linking / cross-compile issues can still appear under `SPlsWork\`.
5. Successful compile produces / updates companion artifacts (e.g. `.ush`, work files) used by SIMPL Windows — leave those for the user unless they ask to clean them.

## Example (this repo)

```powershell
& "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
  \rebuild "power-shutdown-confirmation\Power Shutdown Confirmation v1.0.usp" `
  \target series3 series4
```

## Full help

```powershell
& "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" \?
```

## Source of these notes

User-documented CLI usage for Crestron SIMPL+ compilation (SPlusCC). Reformatted for reuse in this workspace.
