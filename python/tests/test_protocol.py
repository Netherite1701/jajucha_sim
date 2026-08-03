"""Unit tests for the simulator protocol message construction."""

import json
import unittest
import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from jchm._protocol import SimulatorProtocol


class TestSimulatorProtocol(unittest.TestCase):
    """Tests for SimulatorProtocol message construction."""

    def test_create_hello(self):
        """create_hello produces correctly structured message."""
        msg = SimulatorProtocol.create_hello()
        self.assertEqual(msg["type"], "hello")
        self.assertEqual(msg["protocol"], 1)
        self.assertEqual(msg["client"], "jchm-sim")

    def test_create_command(self):
        """create_command produces correctly structured message."""
        msg = SimulatorProtocol.create_command(
            42, "set_motor", {"left": -5, "right": -5, "speed": 3}
        )
        self.assertEqual(msg["type"], "command")
        self.assertEqual(msg["id"], 42)
        self.assertEqual(msg["name"], "set_motor")
        self.assertEqual(msg["payload"]["left"], -5)
        self.assertEqual(msg["payload"]["right"], -5)
        self.assertEqual(msg["payload"]["speed"], 3)

    def test_create_command_empty_payload(self):
        """create_command handles empty payload."""
        msg = SimulatorProtocol.create_command(1, "ping", {})
        self.assertEqual(msg["type"], "command")
        self.assertEqual(msg["id"], 1)
        self.assertEqual(msg["name"], "ping")
        self.assertEqual(msg["payload"], {})

    def test_messages_serialize_to_valid_json(self):
        """All protocol messages produce valid JSON when serialized."""
        msg = SimulatorProtocol.create_hello()
        json_str = json.dumps(msg)
        parsed = json.loads(json_str)
        self.assertEqual(parsed["type"], "hello")

        msg = SimulatorProtocol.create_command(1, "ping", {})
        json_str = json.dumps(msg)
        parsed = json.loads(json_str)
        self.assertEqual(parsed["type"], "command")
        self.assertEqual(parsed["name"], "ping")


if __name__ == "__main__":
    unittest.main()
