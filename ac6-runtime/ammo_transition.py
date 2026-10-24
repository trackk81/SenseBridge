"""Read-only accepted-consumption detector; no injected hook or input timer.

Initial scope: observed TURNER, HU-BEN and GU-Q1 layouts. Reloads, replacement,
stale snapshots and torn identities never generate a shot.
"""
import struct

VALIDATED = {110110000, 110300000, 110510000, 110720000, 111300000}
# SCUDDER: five right-arm ammunition drops observed in session54740.
# Its transient active-fire byte can be missed between polling samples.
# Keep the left arm structural until separately measured.
VALIDATED_SLOTS = {110120000: {1}}

class AmmoTransitions:
    def __init__(self, allow_structural=False, allow_back=False):
        self.previous = {}
        self.allow_structural = allow_structural
        self.allow_back = allow_back
        self.stable = {}

    def sample(self, tick, arms):
        events = []
        current = {}
        stable = {}
        for address, identity, raw in arms:
            known = (identity['weapon_id'] in VALIDATED or
                     identity['slot'] in VALIDATED_SLOTS.get(identity['weapon_id'], set()))
            if len(raw) < 0x268 or (not known and not self.allow_structural):
                continue
            back = self.allow_back and identity['slot'] in (2,3) and identity['mask']==0
            if not back and (identity['slot'] not in (0,1) or identity['mask'] != 1 << identity['slot']):
                continue
            selected = struct.unpack_from('<I', raw, 0x264)[0]
            if selected not in (0, 1):
                continue
            # Validate identity from the same block as ammunition.
            if (struct.unpack_from('<Q', raw, 0)[0] != identity['owner'] or
                struct.unpack_from('<Q', raw, 8)[0] != identity['manager'] or
                struct.unpack_from('<I', raw, 0x20)[0] != identity['weapon_id'] or
                raw[0x28] != identity['slot'] or
                struct.unpack_from('<Q', raw, 0x38)[0] != address):
                continue
            ammo = struct.unpack_from('<i', raw, 0x98 + selected*0x110)[0]
            if not 0 <= ammo <= 100000:
                continue
            key = (address, identity['owner'], identity['manager'], identity['weapon_id'], selected)
            current[key] = (tick, ammo)
            old = self.previous.get(key)
            reserve = struct.unpack_from('<i', raw, 0x9c + selected*0x110)[0]
            prior = self.stable.get(key)
            count = prior[0]+1 if prior and old and 0 <= tick-old[0] <= 100 else 1
            stable[key] = (count,reserve)
            structural = (len(raw)>=0x2dc and raw[0x2d0]==1 and count>=3 and
                          prior is not None and reserve==prior[1] and
                          -1 <= reserve <= 100000)
            if old and 0 <= tick-old[0] <= 100 and 1 <= old[1]-ammo <= 16:
                if not known and not structural:
                    continue
                events.append(dict(mask=identity['mask'], slot=identity['slot'], weapon_id=identity['weapon_id'],
                                   source_tick=tick, accepted_shots=old[1]-ammo,
                                   source='validated-current-player-ammo-decrease' if known else
                                          'structural-active-fire-ammo-decrease',
                                   weapon_specific_verified=known))
        self.previous = current
        self.stable = stable
        return events

def replay(path):
    import json
    detector=AmmoTransitions(); snapshots={}; events=[]
    for line in open(path, encoding='utf-8'):
        r=json.loads(line); s=r.get('state')
        if not s or 'raw' not in s: continue
        # Change-only capture: retain the other hand but never fake a fresh
        # timestamp for a stale sample. Replay each hand independently.
        snapshots.setdefault(s['mask'], AmmoTransitions())
        events.extend(snapshots[s['mask']].sample(r['tick'], [(s['wrapper'],s,bytes.fromhex(s['raw']))]))
    return events

if __name__ == '__main__':
    import sys,json
    events=replay(sys.argv[1]); print(json.dumps(events,indent=2))
