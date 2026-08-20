import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from jchm._sim_backend import SimulatorBackend


class _ByteAtATimeSocket:
    def __init__(self, data: bytes):
        self._data = data

    def recv(self, _size: int) -> bytes:
        if not self._data:
            return b""
        value, self._data = self._data[:1], self._data[1:]
        return value


def test_recv_line_decodes_multibyte_utf8_after_byte_reads():
    backend = SimulatorBackend()
    backend._sock = _ByteAtATimeSocket('{"practiceValueLabel":"비공식 연습값"}\n'.encode("utf-8"))

    assert backend._recv_line() == '{"practiceValueLabel":"비공식 연습값"}'
