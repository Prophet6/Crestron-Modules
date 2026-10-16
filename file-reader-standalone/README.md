# File Reader standalone

A browser editor for a File Reader JSON file. It does not use SIMPL, a contract, or a touchpanel symbol.

The page edits the same `digitals`, `analogs`, and `serials` arrays File Reader v3.3.0 writes. Check, Read, and Write move that one file with `http`, `https`, or `ftp`, the same three schemes as Remote File Sync.

## Run

```bash
cd file-reader-standalone
npm start
```

Open http://localhost:5180.

## Processor files

A 4-Series processor does not publish `\nvram` or `\user` over its web server. HTTP and HTTPS can read a file that is already under the web folder, such as `https://<processor>/html/config.json`.

To read or replace a file in `\user` or `\nvram` without a SIMPL program, turn on the processor FTP server and use an `ftp://` URL. Example: `ftp://192.168.86.200/user/config.json` with the processor's FTP user and password. Allow insecure TLS only for a self-signed HTTPS certificate.

Check does not change the file. Read loads it into the editor. Write replaces the remote file with the editor's JSON. A missing file on Read leaves the editor as it is.

This tool rewrites JSON only. It does not encode CSV, XML, INI, binary, or AES. File Reader still owns those formats.
