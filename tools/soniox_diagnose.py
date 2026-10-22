"""Soniox server diagnostic: standard library only; sends one second of synthetic silence.
Usage: python3 tools/soniox_diagnose.py /path/to/appsettings.json
Run with the API service's environment so AI__... overrides match. Never paste keys.
"""
import argparse
import base64
import hashlib
import io
import json
import os
from pathlib import Path
import re
import socket
import ssl
import struct
from urllib.parse import urlsplit
import wave


def receive_exact(connection, length):
    result = bytearray()
    while len(result) < length:
        chunk = connection.recv(length - len(result))
        if not chunk:
            raise EOFError("connection_closed")
        result.extend(chunk)
    return bytes(result)


def send_frame(connection, opcode, data):
    mask = os.urandom(4)
    size = len(data)
    header = bytes([0x80 | opcode])
    header += bytes([0x80 | size]) if size < 126 else bytes([0x80 | 126]) + struct.pack('!H', size)
    connection.sendall(header + mask + bytes(value ^ mask[index % 4] for index, value in enumerate(data)))


def receive_message(connection):
    message = bytearray()
    while True:
        first, second = receive_exact(connection, 2)
        size = second & 127
        if size == 126:
            size = struct.unpack('!H', receive_exact(connection, 2))[0]
        elif size == 127:
            size = struct.unpack('!Q', receive_exact(connection, 8))[0]
        if size > 128000 or len(message) + size > 128000:
            raise ValueError("response_too_large")
        mask = receive_exact(connection, 4) if second & 128 else None
        payload = receive_exact(connection, size)
        if mask:
            payload = bytes(value ^ mask[index % 4] for index, value in enumerate(payload))
        opcode = first & 15
        if opcode == 8:
            raise EOFError("websocket_closed")
        if opcode == 9:
            send_frame(connection, 10, payload)
            continue
        if opcode == 10:
            continue
        if opcode not in (0, 1):
            raise ValueError("unexpected_websocket_frame")
        message.extend(payload)
        if first & 128:
            return json.loads(message)


def sanitize(value, secrets):
    text = str(value)
    for secret in sorted((item for item in secrets if item), key=len, reverse=True):
        text = text.replace(secret, '[REDACTED]')
    text = re.sub(r'(?i)Bearer\s+\S+', 'Bearer [REDACTED]', text)
    return ''.join(char if char.isprintable() else ' ' for char in text)[:1200]


def lookup(settings, path, default=None):
    for part in path.split(':'):
        if not isinstance(settings, dict):
            return default
        settings = next((value for key, value in settings.items() if key.lower() == part.lower()), default)
    return settings


def setting(settings, path, default=None):
    env_key = path.replace(':', '__').lower()
    return next((value for key, value in os.environ.items() if key.lower() == env_key), lookup(settings, path, default))


def probe(settings, auth_mode):
    key = setting(settings, 'AI:Providers:Soniox:ApiKey', '')
    model = setting(settings, 'AI:SpeechToText:Model', 'stt-rt-v5')
    endpoint = setting(settings, 'AI:Providers:Soniox:WebSocketUrl', 'wss://stt-rt.soniox.com/transcribe-websocket')
    hints = setting(settings, 'AI:SpeechToText:Options:LanguageHints', ['fa'])
    if not isinstance(hints, list):
        raise ValueError('LanguageHints must be an array')
    hints = [setting(settings, 'AI:SpeechToText:Options:LanguageHints:' + str(index), value) for index, value in enumerate(hints)]
    if not isinstance(key, str) or not key.strip() or any(char in key for char in '\r\n'):
        raise ValueError('Soniox API key is missing or malformed')
    uri = urlsplit(endpoint)
    if uri.scheme != 'wss' or not uri.hostname or uri.username or uri.password:
        raise ValueError('Soniox endpoint must be wss without URL credentials')
    nonce = base64.b64encode(os.urandom(16)).decode()
    with socket.create_connection((uri.hostname, uri.port or 443), timeout=25) as raw:
        with ssl.create_default_context().wrap_socket(raw, server_hostname=uri.hostname) as connection:
            path = uri.path or '/'
            if uri.query:
                path += '?' + uri.query
            headers = [f'GET {path} HTTP/1.1', f'Host: {uri.hostname}:{uri.port or 443}',
                       'Upgrade: websocket', 'Connection: Upgrade', f'Sec-WebSocket-Key: {nonce}', 'Sec-WebSocket-Version: 13']
            if auth_mode in ('header', 'both'):
                headers.append('Authorization: Bearer ' + key)
            connection.sendall(('\r\n'.join(headers) + '\r\n\r\n').encode())
            response = bytearray()
            while not response.endswith(b'\r\n\r\n'):
                if len(response) >= 16384:
                    raise ValueError('handshake_too_large')
                response.extend(receive_exact(connection, 1))
            lines = response.decode('latin1').split('\r\n')
            status = lines[0].split(' ')[1]
            if status != '101':
                return {'handshakeStatus': status}
            expected = base64.b64encode(hashlib.sha1((nonce + '258EAFA5-E914-47DA-95CA-C5AB0DC85B11').encode()).digest()).decode()
            upgrade_headers = {name.lower(): value.strip() for line in lines[1:] if ':' in line for name, value in [line.split(':', 1)]}
            if upgrade_headers.get('sec-websocket-accept') != expected:
                raise ValueError('invalid_websocket_handshake')
            config = {'model': model, 'audio_format': 'auto', 'language_hints': hints}
            if auth_mode in ('config', 'both'):
                config['api_key'] = key
            send_frame(connection, 1, json.dumps(config).encode())
            data = io.BytesIO()
            with wave.open(data, 'wb') as audio:
                audio.setnchannels(1)
                audio.setsampwidth(2)
                audio.setframerate(16000)
                audio.writeframes(bytes(32000))
            payload = data.getvalue()
            for start in range(0, len(payload), 16384):
                send_frame(connection, 2, payload[start:start + 16384])
            send_frame(connection, 1, b'')
            for _ in range(128):
                result = receive_message(connection)
                if result.get('error_code'):
                    return {field: sanitize(result.get(field, ''), [key]) for field in ('error_code', 'error_type', 'error_message', 'request_id')}
                if result.get('finished'):
                    return {'finished': True, 'syntheticAudio': 'one_second_silence', 'authMode': auth_mode}
            raise ValueError('too_many_provider_frames')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('settings', type=Path)
    parser.add_argument('--auth-mode', choices=['header', 'config', 'both'], default='header', help='default matches the application; alternate modes only diagnose legacy authentication')
    args = parser.parse_args()
    try:
        settings = json.loads(args.settings.read_text(encoding='utf-8-sig'))
        # Match ASP.NET environment-specific settings when a sibling file exists.
        environment = os.environ.get('DOTNET_ENVIRONMENT') or os.environ.get('ASPNETCORE_ENVIRONMENT')
        def merge(target, source):
            for key, value in source.items():
                existing = next((name for name in target if name.lower() == key.lower()), key)
                if isinstance(value, dict) and isinstance(target.get(existing), dict):
                    merge(target[existing], value)
                else:
                    target[existing] = value
        if environment:
            overlay = args.settings.with_name('appsettings.' + environment + '.json')
            if overlay.is_file():
                merge(settings, json.loads(overlay.read_text(encoding='utf-8-sig')))
        print(json.dumps(probe(settings, args.auth_mode), ensure_ascii=False))
    except Exception as error:
        # Do not print exception text: socket/JSON errors may contain endpoints or input.
        print(json.dumps({'diagnosticErrorType': type(error).__name__}))
        return 1
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
