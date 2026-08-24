#!/usr/bin/env python3
"""Bench client for TCP Server with Authentication (plain or self-signed TLS)."""

from __future__ import print_function

import argparse
import socket
import ssl
import sys


def connect(host, port, use_tls):
    raw = socket.create_connection((host, port), timeout=10)
    if not use_tls:
        return raw
    ctx = ssl._create_unverified_context()
    return ctx.wrap_socket(raw, server_hostname=host)


def recv_line(sock, timeout=10.0):
    sock.settimeout(timeout)
    buf = b""
    while b"\n" not in buf:
        chunk = sock.recv(1024)
        if not chunk:
            break
        buf += chunk
    return buf.decode("ascii", "replace")


def main():
    p = argparse.ArgumentParser(description="TCP Server with Authentication test client")
    p.add_argument("--host", required=True, help="Processor IP")
    p.add_argument("--port", type=int, default=50001)
    p.add_argument("--tls", action="store_true", help="TLS (skip cert verify; ssl self)")
    p.add_argument("--user", default="", help="AUTH username (omit for no AUTH)")
    p.add_argument("--password", default="", help="AUTH password")
    p.add_argument("--send", default="", help="Optional payload after AUTH / connect")
    args = p.parse_args()

    sock = connect(args.host, args.port, args.tls)
    try:
        if args.user:
            banner = recv_line(sock)
            sys.stdout.write(banner)
            auth = "AUTH %s %s\r\n" % (args.user, args.password)
            sock.sendall(auth.encode("ascii"))
            reply = recv_line(sock)
            sys.stdout.write(reply)
            if not reply.startswith("230"):
                return 2
        if args.send:
            payload = args.send
            if not payload.endswith("\n"):
                payload += "\r\n"
            sock.sendall(payload.encode("ascii"))
        sock.settimeout(2.0)
        try:
            data = sock.recv(4096)
            if data:
                sys.stdout.write(data.decode("ascii", "replace"))
        except socket.timeout:
            pass
    finally:
        sock.close()
    return 0


if __name__ == "__main__":
    sys.exit(main() or 0)
