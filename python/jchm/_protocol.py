"""
Protocol message construction for the Jajucha Simulator bridge protocol v1.

Each message is a JSON object with a type field and command-specific fields.
Messages are newline-delimited over TCP.
"""

from typing import Any, Dict


class SimulatorProtocol:
    """Helper to construct protocol messages."""

    PROTOCOL_VERSION = 1
    CLIENT_NAME = "jchm-sim"

    @staticmethod
    def create_hello() -> Dict[str, Any]:
        """Create a handshake hello message."""
        return {
            "type": "hello",
            "id": 0,
            "protocol": SimulatorProtocol.PROTOCOL_VERSION,
            "client": SimulatorProtocol.CLIENT_NAME,
        }

    @staticmethod
    def create_command(cmd_id: int, name: str, payload: Dict[str, Any]) -> Dict[str, Any]:
        """Create a command message."""
        return {
            "type": "command",
            "id": cmd_id,
            "name": name,
            "payload": payload,
        }
