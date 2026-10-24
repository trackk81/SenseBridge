"""Pure control policy. Runtime adapters must independently verify their signals.

AI audio labels and button holds alone are never charge/sustain evidence.
No process access or device writes occur here.
"""
import math


class WeaponActionControl:
    LEASE_MS = 100
    MAX_RUN_MS = 8000

    def __init__(self):
        self.active = {}
        self.sequence = {}
        self.cooldown = set()

    def reset(self):
        self.active.clear()
        self.sequence.clear()
        self.cooldown.clear()

    def observe(self, mask, identity, sequence, source_tick, now, kind,
                progress=None, verified=False, held=False, focused=False):
        if mask not in (1, 2):
            return False
        if not held or not focused:
            self.active.pop(mask, None)
            self.cooldown.discard(mask)
            return False
        if not verified or not 0 <= now-source_tick <= self.LEASE_MS:
            self.active.pop(mask, None)
            return False
        if sequence <= self.sequence.get(mask, -1):
            return False  # Duplicate observations cannot renew a lease.
        self.sequence[mask] = sequence
        if kind in ('stop', 'cancel', 'discharge', 'idle'):
            self.active.pop(mask, None)
            self.cooldown.discard(mask)
            return True
        if kind not in ('charge', 'sustain') or not identity:
            self.active.pop(mask, None)
            return False
        if kind == 'charge' and (not isinstance(progress, (int, float)) or
                                  not math.isfinite(progress) or not 0 <= progress <= 1):
            self.active.pop(mask, None)
            return False
        if mask in self.cooldown:
            return False
        previous = self.active.get(mask)
        if previous and previous['identity'] != identity:
            self.active.pop(mask, None)
            self.cooldown.add(mask)  # Require release/idle after equipment replacement.
            return False
        start = previous['start'] if previous else now
        if now-start >= self.MAX_RUN_MS:
            self.active.pop(mask, None)
            self.cooldown.add(mask)
            return False
        self.active[mask] = dict(identity=identity, start=start,
                                expires=source_tick+self.LEASE_MS,
                                force=1+int(progress*3) if kind == 'charge' else 2)
        return True

    def forces(self, now, held_mask, focused=True):
        result = []
        for mask in (1, 2):
            state = self.active.get(mask)
            if not focused or not held_mask & mask:
                self.active.pop(mask, None)
                self.cooldown.discard(mask)
                state = None
            if state and (now >= state['expires'] or now-state['start'] >= self.MAX_RUN_MS):
                self.active.pop(mask, None)
                if now-state['start'] >= self.MAX_RUN_MS:
                    self.cooldown.add(mask)
                state = None
            result.append(state['force'] if state else 0)
        return tuple(result)


def test():
    c = WeaponActionControl()
    def feed(mask=1, seq=1, now=0, kind='charge', progress=.5, **kwargs):
        params = dict(identity=(123, 456), sequence=seq, source_tick=now,
                      verified=True, held=True, focused=True)
        params.update(kwargs)
        return c.observe(mask, now=now, kind=kind, progress=progress, **params)
    assert not feed(verified=False)
    assert c.forces(0, 3) == (0, 0)
    assert feed(progress=0)
    assert feed(2, kind='sustain')
    assert c.forces(0, 3) == (1, 2)
    assert feed(seq=2, now=25, progress=1)
    assert c.forces(25, 3) == (4, 2)
    assert not feed(seq=2, now=90)  # duplicate cannot extend 125ms expiry
    assert c.forces(125, 3) == (0, 0)
    assert feed(seq=3, now=130)
    assert feed(seq=4, now=140, kind='discharge')
    assert c.forces(140, 3) == (0, 0)
    for invalid in (float('nan'), float('inf'), -1, 1.01, None):
        c.reset(); assert not feed(progress=invalid)
    c.reset(); assert feed()
    assert not feed(seq=2, now=1, identity=(123, 999))
    assert not feed(seq=3, now=2)
    c.forces(3, 0); assert feed(seq=4, now=4)
    assert c.forces(5, 3, False) == (0, 0)
    c.reset()
    for t in range(0, 8000, 50):
        assert feed(seq=t+1, now=t, kind='sustain')
    assert not feed(seq=8001, now=8000, kind='sustain')
    assert c.forces(8000, 3) == (0, 0)
    assert not feed(seq=8002, now=8001, kind='sustain')
    c.forces(8002, 0); assert feed(seq=8003, now=8003, kind='sustain')
    c.reset(); assert not feed(source_tick=1)
    assert not feed(source_tick=-101)
    print('PASS charge ramp, both arms, discharge, stale/duplicate, invalid progress, '
          'equipment replacement, focus/release, sustain cap and rearm')


if __name__ == '__main__':
    test()
