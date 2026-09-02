# TCP Server with Authentication

Crestron SIMPL+ / Simpl# TCP **listener**. Defaults match the built-in SIMPL Windows **TCP/IP Server** symbol (plain TCP, no login). Optional TLS and optional username/password are extras.

| File | Role |
|------|------|
| `TCP Server with Authentication.usp` | SIMPL+ symbol |
| `TCP_Server_Auth.clz` | Compiled Simpl# library (required next to the `.usp`) |
| `TCP Server with Authentication/` | Simpl# source (Visual Studio 2008, Windows CE / .NET CF 3.5) |
| `test-client/` | Pointer to the canonical client in [Testing-Tools](https://github.com/Prophet6/Testing-Tools) |

3-Series and 4-Series. Firmware **1.500.0005+** if you turn Security (TLS) on.

The committed `TCP_Server_Auth.clz` is produced from a .NET Compact Framework 3.5 build and compiles with SPlusCC for **4-Series**. For a **3-Series** image, open `TCP Server with Authentication.sln` in Visual Studio **2008** with the Crestron Simpl# plugin and rebuild so the plugin packages the CLZ.

## Load

1. Keep `TCP_Server_Auth.clz` in this folder.
2. Compile SIMPL+ for both series:

   ```powershell
   & "C:\Program Files (x86)\Crestron\Simpl\SPlusCC.exe" `
     \rebuild "TCP Server with Authentication.usp" `
     \target series3 series4
   ```

3. `.usp` files must be **CRLF**. LF-only is Error 1700.

Rebuild Simpl# in VS2008 after editing `TcpAuthServer.cs`, then copy `TCP_Server_Auth.clz` here.

## Defaults (stock TCP server)

Leave **Security** and **Authentication** off. Hold **Enable**. `TX$` / `RX$` pass bytes. Port default **50001**.

## TLS (Security)

Hold **Enable_Security** or set the Security parameter to On. Processor:

```text
ssl self
```

Clients must skip certificate verification. If listen fails, `Error$` tells you to enable SSL.

## AUTH (Authentication)

Hold **Enable_Authentication** or set Authentication to On. After connect:

```text
220 TCP Server with Authentication ready
AUTH user pass
230 Authenticated
```

Failure (then drop): `535 Authentication failed` or `535 Authentication timeout`.

Parameters default to username `user` and password `pass`. Serial overrides replace them when non-empty.

Authentication **without** TLS sends the password in the clear; `Error$` warns.

## Test client

Canonical copy: [Testing-Tools](https://github.com/Prophet6/Testing-Tools) (`start-tcp-auth-client.bat`, or `tcp-auth-client/tcp_auth_client.py`). A local copy remains in `test-client/` for convenience.

```powershell
python tcp_auth_client.py --host <processor-ip> --port 50001
python tcp_auth_client.py --host <processor-ip> --port 50001 --tls --user user --password pass
```

`--tls` uses an unverified TLS context (self-signed lab certs).
