import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch
from urllib.error import HTTPError, URLError


def module(name):
    spec = importlib.util.spec_from_file_location(name.replace("-", "_"), Path(__file__).with_name(name + ".py"))
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


readonly = module("read-only-deployment")
SHA = "a" * 40
HOST = "https://niobiumstore-func-test.synthetic.azurewebsites.net"


def response(payload, url=HOST + "/health", status=200):
    result = Mock()
    result.status = status
    result.geturl.return_value = url
    result.read.return_value = json.dumps(payload).encode()
    result.__enter__ = Mock(return_value=result)
    result.__exit__ = Mock(return_value=False)
    return result


class ReadOnlyTests(unittest.TestCase):
    def test_only_get_health_reaches_network(self):
        opener = Mock()
        client = readonly.ReadOnlyClient(HOST, opener)
        for method, path in [("POST", "/health"), ("POST", "/order"), ("GET", "/track"),
                             ("GET", "/health?token=secret"), ("GET", "//elsewhere/health")]:
            with self.subTest(method=method, path=path), self.assertRaises(readonly.VerificationFailure):
                client.request(method, path)
        opener.open.assert_not_called()
        opener.open.return_value = response({"status": "ready", "commit": SHA})
        self.assertEqual(SHA, client.request()["commit"])
        request = opener.open.call_args.args[0]
        self.assertEqual("GET", request.method)
        self.assertEqual(HOST + "/health", request.full_url)
        self.assertIsNone(request.data)

    def test_invalid_origins_rejected(self):
        for value in ["http://host", "https://u:p@host", "https://host/path", "https://host?token=x",
                      "https://host#fragment", "https://host:123", "https://host:bad", "", "https://[broken", "https://host\n", "https://ho st"]:
            with self.subTest(value=value), self.assertRaises(readonly.VerificationFailure):
                readonly.ReadOnlyClient(value)

    def test_redirect_and_large_or_invalid_response_rejected(self):
        opener = Mock()
        for received in [response({}, url="https://elsewhere/health"), response({}, status=302), response({"data": "x" * 4097})]:
            opener.open.return_value = received
            with self.assertRaises(readonly.VerificationFailure):
                readonly.ReadOnlyClient(HOST, opener).request()
        self.assertIsNone(readonly.NoRedirect().redirect_request(None, None, 302, "", {}, "https://elsewhere"))
        opener.open.return_value = response({})
        opener.open.return_value.read.return_value = b"not JSON with sensitive body"
        with self.assertRaisesRegex(readonly.VerificationFailure, "^health_request_failed$"):
            readonly.ReadOnlyClient(HOST, opener).request()

    def test_transport_errors_are_sanitized(self):
        for error in [URLError("sensitive"), TimeoutError("sensitive"), HTTPError(HOST, 500, "sensitive", {}, None)]:
            with self.subTest(error=type(error)), self.assertRaisesRegex(readonly.VerificationFailure, "^health_request_failed$"):
                readonly.ReadOnlyClient(HOST, Mock(open=Mock(side_effect=error))).request()

    def test_commit_readiness_retries_then_verifies_exact_sha(self):
        client = Mock()
        client.request.side_effect = [readonly.VerificationFailure("transient"), {"status": "ready", "commit": "b" * 40},
                                      {"status": "ready", "commit": SHA}]
        sleeper = Mock()
        report = readonly.verify(HOST, SHA, attempts=3, client=client, sleeper=sleeper)
        self.assertTrue(report["success"])
        self.assertEqual(SHA, report["commit"])
        self.assertEqual(2, sleeper.call_count)
        self.assertEqual(3, client.request.call_count)

    def test_commit_mismatch_exhausts_bounded_read_only_retries(self):
        client = Mock(request=Mock(return_value={"status": "ready", "commit": "b" * 40}))
        sleeper = Mock()
        with self.assertRaisesRegex(readonly.VerificationFailure, "deployed_commit_not_ready"):
            readonly.verify(HOST, SHA, attempts=2, client=client, sleeper=sleeper)
        self.assertEqual(2, client.request.call_count)
        sleeper.assert_called_once_with(5)
        with self.assertRaisesRegex(readonly.VerificationFailure, "invalid_expected_commit"):
            readonly.verify(HOST, "main", client=client)
        self.assertEqual(2, client.request.call_count)


