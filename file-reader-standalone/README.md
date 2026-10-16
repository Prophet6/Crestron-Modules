# File Reader standalone

A browser editor for a File Reader JSON file. It does not use SIMPL, a contract, or a touchpanel symbol.

The page edits the same `digitals`, `analogs`, and `serials` arrays File Reader v3.3.0 writes. Check, Read, and Write move that one file with `http`, `https`, or `ftp`, the same three schemes as Remote File Sync.

<img src="Screenshot%201.png" alt="File Reader standalone" width="400">

## Run

```bash
cd file-reader-standalone
npm start
```

Open http://localhost:5180.

## Putting the file in `\user`

Remote File Sync writes `\user` because it runs on the processor. Its `Local_Path` is a processor path such as `/user/config.json`.

This program runs on the technician PC, so it cannot open `\user` itself. **Publish for \user** serves the current JSON at `http://<this-pc>:5180/export.json`. On the processor, set Remote File Sync `Remote_URL` to that address and `Local_Path` to `/user/config.json`, then Retrieve. That is the same write Remote File Sync already does.

Direct Write still sends the JSON to an `http`, `https`, or `ftp` URL. Use `ftp://<processor>/user/config.json` when the processor FTP server is enabled. Allow insecure TLS only for a self-signed HTTPS certificate.

Check does not change the file. Read loads it into the editor. A missing file on Read leaves the editor as it is.

## One program for technicians

`npm run build` writes `dist/File Reader.exe`. That file is not committed. Double-click it to open the editor. The PC firewall has to allow the processor to reach port 5180 when Remote File Sync retrieves the published file.

This tool rewrites JSON only. It does not encode CSV, XML, INI, binary, or AES. File Reader still owns those formats.
