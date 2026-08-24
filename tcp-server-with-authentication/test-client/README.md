# Test client

Python 3 client for [TCP Server with Authentication](../README.md).

```powershell
python tcp_auth_client.py --host <processor-ip> --port 50001
python tcp_auth_client.py --host <processor-ip> --port 50001 --tls --user user --password pass
python tcp_auth_client.py --host <processor-ip> --port 50001 --user user --password pass --send "hello"
```

`--tls` does **not** verify the processor certificate (`ssl self` lab certs).
