# Test client

Python 3 client for [TCP Server with Authentication](../README.md).

Canonical home: [Testing-Tools](https://github.com/Prophet6/Testing-Tools) (`start-tcp-auth-client.bat`). Keep this copy next to the module for a one-folder bench.

```powershell
python tcp_auth_client.py --host <processor-ip> --port 50001
python tcp_auth_client.py --host <processor-ip> --port 50001 --tls --user user --password pass
python tcp_auth_client.py --host <processor-ip> --port 50001 --user user --password pass --send "hello"
```

`--tls` does **not** verify the processor certificate (`ssl self` lab certs).
