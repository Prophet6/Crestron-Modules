# File Reader UI

Touchpanel and browser page for **File Reader v3.3.0**. It drives every user-module input, shows every output, and edits the 64 digital, analog, and serial values stored in the file.

The page is a CH5 project in the same shape as [Crestron-HTML5](https://github.com/Prophet6/Crestron-HTML5): vanilla TypeScript, Vite, and CrComLib. It is not a Construct `.cca`. The contract is a Construct-style GUI extender (`.chd`, `Sgntr=CHD`) plus a `.cse2j`.

| | |
|---|---|
| Layout | Fluid, aimed at 1920×1200 |
| IP-ID | **C1** for the HTML5 XPanel |
| Vite | http://localhost:5176 |
| Processor page | `https://<processor>/file-reader/index.html?ipID=C1&authToken=<token>` |
| Default processor | `192.168.86.200`, or `?host=` / `VITE_PROCESSOR_HOST` |

`?debug=1` opens Eruda.

## Pages

Bottom tabs: **File** and **Signals**.

Read and Write stay in the header. The header also shows the selected file name, size, exists, encrypted, backup, busy, and operational feedback.

- **File.** Location, name override, match partial, write format, auto-create, delete, backup, and encrypt.
- **Signals.** Digitals, analogs, and serials. The edit column is the module input. The file column is the module output. **Use file values** copies outputs onto inputs. **Clear edits** publishes zeros and empty strings and does not write the file.

File Location, Filename, Format, and Default Wait Time stay parameters on the user module. **Parameter** uses that File Location string. NVRAM, User, HTML, Removable, and Removable 2 publish `File_Location_Override` as 1 through 5 and hold it. Those folders are `\nvram\`, `\user\`, `\html\`, `\rm\`, and `\rm2\`. The extension is still appended.

A read fills outputs only. Inputs stay until **Use file values**. A successful write makes the module copy inputs onto outputs.

Edits made before the processor is online are sent when the connection comes up. Signals you did not touch are left alone.

## Contract

`npm run generate:contract` writes:

| File | Use |
|---|---|
| `contracts/file-reader.cse2j` | CH5 mapping |
| `public/config/contract.cse2j` | Path CrComLib loads |
| `contracts/file-reader.chd` | SIMPL **Manage GUI Extenders** |
| `docs/CONTRACT-MAP.md` | Row grid |

Control Join Id is **1**. Signal names match the user module. Wiring is in [docs/simpl.md](docs/simpl.md).

## Run

```bash
cd file-reader/html5
npm install
npm run generate:contract
npm run dev
```

```bash
npm run build:ch5z
npm run deploy:xpanel
PANEL_HOST=<panel-ip> npm run deploy:panel
```

`build:ch5z` writes `archive/file-reader-YYYYMMDD.ch5z` and a dated `.chd`. `node_modules/`, `dist/`, and `archive/` stay out of git.

## License

MIT
