"""Read-only candidate telemetry. These fields are NOT a verified charge adapter.

Exact-build static evidence: wrapper+0x2a8 is passed to RVA 0x9082f0.
That routine integrates frame dt into +0x2b4, divides by +0x2b0,
and clamps the result into +0x2b8. +0x2ac/2ad contain phase values.
Runtime semantics, weapon coverage, and charge cancellation remain unverified.
"""
import ctypes as C
import math
import struct
from weapon_slots import decode


def decode_candidate(address, data, owner, manager):
    if len(data) != 0x2d0:
        return None
    slot = decode(address, data[:0x40], owner)
    if not slot or slot['manager'] != manager or not slot['mask']:
        return None
    selected = struct.unpack_from('<i', data, 0x264)[0]
    phase, prior_phase = data[0x2ac:0x2ae]
    duration, elapsed, progress = struct.unpack_from('<fff', data, 0x2b0)
    if selected not in (0, 1) or phase > 2 or prior_phase > 2:
        return None
    if not all(math.isfinite(v) for v in (duration, elapsed, progress)):
        return None
    return dict(**slot, wrapper=address, selected=selected, phase=phase,
                prior_phase=prior_phase, duration=duration, elapsed=elapsed,
                progress=progress, advancing=bool(data[0x2c8]), verified=False)


def snapshot(slots, address):
    data = C.create_string_buffer(0x2d0); count = C.c_size_t()
    if not slots.k.ReadProcessMemory(slots.h, address, data, len(data), C.byref(count)):
        return None
    if count.value != len(data):
        return None
    result = decode_candidate(address, data.raw, slots.owner, slots.manager)
    # Reject identity changes during the multi-field read.
    after = slots.snapshot(address)
    if not result or not after or any(result[k] != after[k] for k in
                                     ('owner', 'manager', 'weapon_id', 'slot')):
        return None
    return result


def test():
    b = bytearray(0x2d0)
    struct.pack_into('<QQ', b, 0, 100, 200)
    struct.pack_into('<I', b, 32, 110300000)
    struct.pack_into('<Q', b, 56, 300)
    struct.pack_into('<fff', b, 0x2b0, 2, 1, .5)
    x = decode_candidate(300, b, 100, 200)
    assert x['progress'] == .5 and not x['verified']
    assert decode_candidate(300, b, 999, 200) is None
    assert decode_candidate(300, b, 100, 999) is None
    b[0x28] = 2; assert decode_candidate(300, b, 100, 200) is None
    b[0x28] = 1; b[0x2ac] = 3
    assert decode_candidate(300, b, 100, 200) is None
    b[0x2ac] = 0; struct.pack_into('<f', b, 0x2b8, float('nan'))
    assert decode_candidate(300, b, 100, 200) is None
    print('PASS candidate decoder rejects changed identity, back slots, invalid phases/float')


if __name__ == '__main__':
    test()
