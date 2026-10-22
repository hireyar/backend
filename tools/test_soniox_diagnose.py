import importlib.util
import json
import os
from pathlib import Path
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('soniox_diagnose', Path(__file__).with_name('soniox_diagnose.py'))
diag = importlib.util.module_from_spec(spec)
spec.loader.exec_module(diag)


class SonioxDiagnosticTests(unittest.TestCase):
    def test_secret_and_bearer_are_redacted_from_synthetic_provider_error(self):
        secret = 'synthetic-key-never-print'
        value = diag.sanitize('Invalid ' + secret + ' Bearer other-secret\nerror', [secret])
        self.assertNotIn(secret, value)
        self.assertNotIn('other-secret', value)
        self.assertNotIn('\n', value)
        self.assertIn('[REDACTED]', value)

    def test_provider_text_is_bounded_and_strips_control_characters(self):
        value = diag.sanitize('\x1b\n' + 'x' * 2000, [])
        self.assertEqual(1200, len(value))
        self.assertTrue(all(char.isprintable() for char in value))

    def test_environment_overrides_are_case_insensitive(self):
        with patch.dict(os.environ, {'ai__providers__soniox__apikey': 'override'}, clear=True):
            self.assertEqual('override', diag.setting({'AI': {'Providers': {'Soniox': {'ApiKey': 'file'}}}}, 'AI:Providers:Soniox:ApiKey'))

    def test_masked_frame_roundtrips_without_exposing_unmasked_payload(self):
        class Connection:
            def sendall(self, data): self.data = data
        connection = Connection()
        payload = bytes(range(256))
        diag.send_frame(connection, 2, payload)
        self.assertEqual(bytes([0x82, 0xfe, 1, 0]), connection.data[:4])
        mask = connection.data[4:8]
        self.assertEqual(payload, bytes(value ^ mask[index % 4] for index, value in enumerate(connection.data[8:])))

    def test_continuation_frames_reassemble_one_json_message(self):
        class Connection:
            data = bytearray(b'\x01\x07{"finis' + b'\x80\x0ahed":true}')
            def recv(self, count):
                result = self.data[:count]
                del self.data[:count]
                return result
        self.assertEqual({'finished': True}, diag.receive_message(Connection()))


if __name__ == '__main__':
    unittest.main()
