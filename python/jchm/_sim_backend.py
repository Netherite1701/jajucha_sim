"""
Simulator backend that communicates with the Unity bridge over TCP.

This module handles:
- Persistent TCP connection to 127.0.0.1:8765
- Automatic connection on first command
- Reconnection on broken connection
- Request/response matching via IDs
- Timeout enforcement
- Clean disconnection

Students should NOT import this directly. Use jchm.control instead.
"""

import json
import logging
import socket
import threading
import time
from typing import Any, Dict, Optional, Tuple

import cv2
import numpy as np

from ._protocol import SimulatorProtocol
from .errors import JchmConnectionError, JchmSimulatorTimeout, JchmProtocolError

logger = logging.getLogger("jchm-sim")

# Debug logging enabled via environment variable
_DEBUG = False
import os
if os.environ.get("JCHM_SIM_DEBUG", "0").lower() in ("1", "true", "yes"):
    _DEBUG = True


def _log(msg: str):
    if _DEBUG:
        print(f"[jchm-sim] {msg}")


class SimulatorBackend:
    """
    Simulator backend using a persistent TCP connection to the Unity bridge.
    """

    DEFAULT_HOST = "127.0.0.1"
    DEFAULT_PORT = 8765
    DEFAULT_TIMEOUT = 1.0  # seconds per request
    RECONNECT_ATTEMPTS = 1  # one reconnect attempt per call

    def __init__(self, host: str = None, port: int = None):
        self._host = host or self.DEFAULT_HOST
        self._port = port or self.DEFAULT_PORT
        self._sock: Optional[socket.socket] = None
        self._lock = threading.Lock()
        self._next_id = 1
        self._connected = False
        self._handshake_done = False

    # --- Public API called by jchm.control ---

    def set_motor(self, left: int, right: int, speed: int):
        """Send a set_motor command to the simulator."""
        self._ensure_connected()
        response = self._send_command("set_motor", {
            "left": left,
            "right": right,
            "speed": speed,
        })
        if not response.get("ok", False):
            error = response.get("error", {})
            code = error.get("code", "UNKNOWN")
            msg = error.get("message", "No details")
            raise RuntimeError(f"set_motor failed: [{code}] {msg}")

    def ping(self) -> Dict[str, Any]:
        """Send a ping command to check connectivity."""
        self._ensure_connected()
        return self._send_command("ping", {})

    def get_status(self) -> Dict[str, Any]:
        """Request simulator status."""
        self._ensure_connected()
        return self._send_command("get_status", {})

    def sim_start(self):
        """Start the simulation."""
        self._ensure_connected()
        self._send_command("sim_start", {})

    def sim_pause(self):
        """Pause the simulation."""
        self._ensure_connected()
        self._send_command("sim_pause", {})

    def sim_step(self):
        """Advance exactly one simulation tick when paused."""
        self._ensure_connected()
        self._send_command("sim_step", {})

    def sim_reset(self):
        """Reset the simulation to initial state."""
        self._ensure_connected()
        self._send_command("sim_reset", {})

    # --- Scenario automation API (Step 8.36) ---

    def sim_start_run(self):
        """Start the scenario run (begin start signal sequence)."""
        self._ensure_connected()
        self._send_command("start_run", {})

    def sim_abort_run(self):
        """Abort the active scenario run."""
        self._ensure_connected()
        self._send_command("abort_run", {})

    def sim_get_run_status(self) -> Dict[str, Any]:
        """Get the current scenario run status."""
        self._ensure_connected()
        return self._send_command("get_run_status", {})

    def sim_get_result(self) -> Dict[str, Any]:
        """
        Get the final run result.

        Returns:
            A dict containing the serialized result JSON under the "result"
            key (already parsed). Raises RuntimeError if the run has not
            finished yet.
        """
        self._ensure_connected()
        response = self._send_command("get_result", {})
        if not response.get("ok", False):
            error = response.get("error", {})
            code = error.get("code", "UNKNOWN")
            msg = error.get("message", "No details")
            raise RuntimeError(f"get_result failed: [{code}] {msg}")
        result_json = response.get("payload", {}).get("result", "{}")
        return json.loads(result_json)

    # --- Public API called by jchm.camera ---

    def get_image(self, location: str) -> np.ndarray:
        """
        Get the latest camera frame as a NumPy array.

        Returns:
            np.ndarray of shape (height, width, 3), dtype=uint8, BGR order.
        """
        self._ensure_connected()
        header, binary = self._send_command_binary("get_image", {"location": location})

        if not header.get("ok", False):
            error = header.get("error", {})
            code = error.get("code", "UNKNOWN")
            msg = error.get("message", "No details")
            raise RuntimeError(f"get_image failed: [{code}] {msg}")

        width = header.get("width", 0)
        height = header.get("height", 0)
        fmt = header.get("format", "rgb24")

        if width <= 0 or height <= 0 or len(binary) == 0:
            raise RuntimeError(
                f"get_image received invalid frame: {width}x{height}, {len(binary)} bytes"
            )

        # Parse binary data into a NumPy array
        frame = np.frombuffer(binary, dtype=np.uint8)

        if fmt == "rgb24":
            frame = frame.reshape(height, width, 3)
            # Convert RGB -> BGR for OpenCV compatibility
            frame = cv2.cvtColor(frame, cv2.COLOR_RGB2BGR)
        else:
            frame = frame.reshape(height, width)

        _log(f"get_image({location}) -> {width}x{height} {fmt}")
        return frame

    def get_depth(self) -> np.ndarray:
        """
        Get the latest depth image as a NumPy array.

        Returns:
            np.ndarray of shape (height, width), dtype=uint8.
            Brighter = nearer, darker = farther.
        """
        self._ensure_connected()
        header, binary = self._send_command_binary("get_depth", {})

        if not header.get("ok", False):
            error = header.get("error", {})
            code = error.get("code", "UNKNOWN")
            msg = error.get("message", "No details")
            raise RuntimeError(f"get_depth failed: [{code}] {msg}")

        width = header.get("width", 0)
        height = header.get("height", 0)

        if width <= 0 or height <= 0 or len(binary) == 0:
            raise RuntimeError(
                f"get_depth received invalid frame: {width}x{height}, {len(binary)} bytes"
            )

        depth = np.frombuffer(binary, dtype=np.uint8)
        depth = depth.reshape(height, width)

        _log(f"get_depth() -> {width}x{height}")
        return depth

    def disconnect(self):
        """Close the connection to the simulator."""
        with self._lock:
            self._close_socket()
            self._connected = False
            self._handshake_done = False
            _log("disconnected")

    # --- Internal ---

    def _ensure_connected(self):
        """Connect and handshake if not already connected."""
        if self._connected and self._handshake_done:
            return

        with self._lock:
            if self._connected and self._handshake_done:
                return

            # Try to connect
            self._connect()
            self._handshake()

    def _connect(self):
        """Open TCP connection."""
        if self._sock is not None:
            try:
                self._sock.close()
            except Exception:
                pass
            self._sock = None

        try:
            self._sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self._sock.settimeout(self.DEFAULT_TIMEOUT)
            self._sock.connect((self._host, self._port))
            self._sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            self._connected = True
            _log(f"connected {self._host}:{self._port}")
        except (socket.timeout, ConnectionRefusedError) as e:
            self._connected = False
            raise JchmConnectionError(
                f"JCHM Simulator connection failed.\n"
                f"Expected simulator at: {self._host}:{self._port}\n"
                f"Start Jajucha Simulator and try again.\n"
                f"Underlying error: {e}"
            ) from e
        except OSError as e:
            self._connected = False
            raise JchmConnectionError(
                f"JCHM Simulator connection failed: {e}\n"
                f"Expected simulator at: {self._host}:{self._port}"
            ) from e

    def _handshake(self):
        """Perform protocol version handshake."""
        hello = SimulatorProtocol.create_hello()
        self._send_raw(hello)

        response = self._recv_line()
        if response is None:
            self._close_socket()
            self._connected = False
            raise JchmConnectionError("No response during handshake")

        try:
            msg = json.loads(response)
        except json.JSONDecodeError:
            self._close_socket()
            self._connected = False
            raise JchmProtocolError(f"Invalid handshake response: {response}")

        if msg.get("type") == "error":
            code = msg.get("code", "UNKNOWN")
            self._close_socket()
            self._connected = False
            raise JchmProtocolError(
                f"Handshake rejected: [{code}] {msg.get('message', '')}"
            )

        if msg.get("type") != "hello_ack":
            self._close_socket()
            self._connected = False
            raise JchmProtocolError(
                f"Expected hello_ack, got: {msg.get('type')}"
            )

        self._handshake_done = True
        _log("handshake complete")

    def _send_command(self, name: str, payload: Dict) -> Dict:
        """Send a command and return the parsed response."""
        with self._lock:
            if not self._connected or self._sock is None:
                # Try reconnecting once
                self._connected = False
                self._handshake_done = False
                try:
                    self._connect()
                    self._handshake()
                except JchmConnectionError as e:
                    raise e

            cmd_id = self._next_id
            self._next_id += 1

            command = SimulatorProtocol.create_command(cmd_id, name, payload)
            self._send_raw(command)
            _log(f"{name}({json.dumps(payload)})")

            response = self._recv_line()
            if response is None:
                # Connection lost, try reconnecting once
                self._connected = False
                self._handshake_done = False
                try:
                    self._connect()
                    self._handshake()
                    # Resend command
                    command = SimulatorProtocol.create_command(cmd_id, name, payload)
                    self._send_raw(command)
                    response = self._recv_line()
                    if response is None:
                        raise JchmSimulatorTimeout(
                            f"Command '{name}' timed out (no response)"
                        )
                except (JchmConnectionError, OSError) as e:
                    raise JchmConnectionError(
                        f"Connection lost and reconnection failed: {e}"
                    ) from e

            try:
                result = json.loads(response)
            except json.JSONDecodeError as e:
                raise JchmProtocolError(f"Invalid response JSON: {e}") from e

            _log(f"ack id={cmd_id}")
            return result

    def _send_raw(self, message: Dict):
        """Send a JSON message with newline terminator."""
        if self._sock is None:
            raise JchmConnectionError("Not connected")
        data = (json.dumps(message) + "\n").encode("utf-8")
        try:
            self._sock.sendall(data)
        except OSError as e:
            self._connected = False
            raise JchmConnectionError(f"Send failed: {e}") from e

    def _recv_line(self) -> Optional[str]:
        """
        Read one newline-delimited line from the socket.
        Returns None on timeout or connection close.
        """
        if self._sock is None:
            return None

        buf = []
        try:
            while True:
                byte = self._sock.recv(1)
                if not byte:
                    # Connection closed
                    return None
                if byte == b"\n":
                    break
                if byte != b"\r":
                    buf.append(byte.decode("utf-8"))
        except socket.timeout:
            return None
        except OSError:
            return None

        return "".join(buf)

    def _send_command_binary(self, name: str, payload: Dict) -> Tuple[Dict, bytes]:
        """
        Send a command and read a binary response.
        The response consists of a JSON header line followed by N raw bytes.

        Returns:
            Tuple of (header_dict, binary_data).
        """
        with self._lock:
            if not self._connected or self._sock is None:
                self._connected = False
                self._handshake_done = False
                try:
                    self._connect()
                    self._handshake()
                except JchmConnectionError as e:
                    raise e

            cmd_id = self._next_id
            self._next_id += 1

            command = SimulatorProtocol.create_command(cmd_id, name, payload)
            self._send_raw(command)
            _log(f"{name}({json.dumps(payload)})")

            # Read response header (JSON line)
            response = self._recv_line()
            if response is None:
                self._connected = False
                self._handshake_done = False
                try:
                    self._connect()
                    self._handshake()
                    command = SimulatorProtocol.create_command(cmd_id, name, payload)
                    self._send_raw(command)
                    response = self._recv_line()
                    if response is None:
                        raise JchmSimulatorTimeout(
                            f"Command '{name}' timed out (no response)"
                        )
                except (JchmConnectionError, OSError) as e:
                    raise JchmConnectionError(
                        f"Connection lost and reconnection failed: {e}"
                    ) from e

            try:
                header = json.loads(response)
            except json.JSONDecodeError as e:
                raise JchmProtocolError(f"Invalid response JSON: {e}") from e

            _log(f"header id={cmd_id} type={header.get('payload_type')} length={header.get('length')}")

            # Read binary payload if present
            binary_length = header.get("length", 0)
            binary_data = b""
            if binary_length > 0:
                binary_data = self._recv_bytes(binary_length)
                if binary_data is None or len(binary_data) != binary_length:
                    raise JchmConnectionError(
                        f"Expected {binary_length} binary bytes, got {len(binary_data) if binary_data else 0}"
                    )

            return header, binary_data

    def _recv_bytes(self, n: int) -> Optional[bytes]:
        """
        Read exactly n bytes from the socket.
        Returns None on timeout or connection close.
        """
        if self._sock is None or n <= 0:
            return None

        chunks = []
        remaining = n
        try:
            while remaining > 0:
                chunk = self._sock.recv(remaining)
                if not chunk:
                    return None
                chunks.append(chunk)
                remaining -= len(chunk)
        except socket.timeout:
            return None
        except OSError:
            return None

        return b"".join(chunks)

    def _close_socket(self):
        """Safely close the socket."""
        if self._sock is not None:
            try:
                self._sock.close()
            except Exception:
                pass
            self._sock = None
