# File Reader standalone

A browser editor for a File Reader JSON file. It does not use SIMPL, a contract, or a touchpanel symbol.

The page follows the File Reader test interface: File and Signals, the same folder buttons, and an edit column beside the values last read. Read and Write move that one file with `http`, `https`, or `ftp`.

<img src="Screenshot%201.png" alt="File Reader standalone" width="400">

## Run

```bash
cd file-reader-standalone
npm start
```

Open http://localhost:5180.

## Folders

The File page uses the same locations as File Reader v3.3.0. **Parameter** uses the folder chosen under Processor. The other buttons force one folder:

| Button | Processor path |
|---|---|
| NVRAM | `/nvram/` |
| User | `/user/` |
| HTML | `/html/` |
| Removable | `/rm/` |
| Removable 2 | `/rm2/` |

The filename and the format extension are added, so User plus `config` plus JSON is `/user/config.json`.

Remote File Sync writes that path because it runs on the processor. **Publish** serves the current JSON at `http://<this-pc>:5180/export.json`. Set `Remote_URL` to that address and `Local_Path` to the path shown on the page, then Retrieve.

Direct Read and Write use the processor host and protocol on the Processor card. FTP can reach all five folders when the processor FTP server is on. HTTP and HTTPS usually serve only the HTML folder. Allow insecure TLS only for a self-signed HTTPS certificate.

Check does not change the file. Read loads it into the editor. A missing file on Read leaves the editor as it is.

## One program for technicians

`npm run build` writes `dist/File Reader.exe`. That file is not committed. Double-click it to open the editor. The PC firewall has to allow the processor to reach port 5180 when Remote File Sync retrieves the published file.

This tool rewrites JSON only. It does not encode CSV, XML, INI, binary, or AES. File Reader still owns those formats.
