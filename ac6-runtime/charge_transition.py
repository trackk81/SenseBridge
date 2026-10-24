"""Exact-build charge progress observed on the reverse-joint teaching AC.

Only the two measured arm weapon IDs are enabled. Phase 2 is cancellation /
release (including residual decay), never charging. Buttons cannot start this.
"""
from charge_state_probe import decode_candidate
from weapon_action_control import WeaponActionControl

VALIDATED_CHARGE = {111100000, 110010000}

class ChargeControl:
    def __init__(self):
        self.policy=WeaponActionControl()
        self.sequence=0

    def sample(self, now, blocks, held_mask, focused):
        self.sequence+=1
        present=0
        for address, identity, raw in blocks:
            mask=identity['mask']
            if identity['weapon_id'] not in VALIDATED_CHARGE: continue
            s=decode_candidate(address,raw[:0x2d0],identity['owner'],identity['manager'])
            if not s or s['weapon_id']!=identity['weapon_id'] or s['mask']!=mask: continue
            if not .01<=s['duration']<=10: continue
            if abs(s['progress']-min(1,max(0,s['elapsed']/s['duration'])))>.03: continue
            present|=mask
            charging=s['phase']==0 and s['advancing']
            self.policy.observe(mask,(address,s['owner'],s['weapon_id']),self.sequence,
                now,now,'charge' if charging else 'cancel',s['progress'],
                verified=True,held=bool(held_mask&mask),focused=focused)
        return self.policy.forces(now,held_mask&present,focused)
