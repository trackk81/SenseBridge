"""Measured GU-Q1 emission only; no generic beam/flamethrower inference."""
import struct
from ammo_transition import AmmoTransitions
from weapon_slots import decode
from weapon_action_control import WeaponActionControl

class SustainControl:
    def __init__(self):
        self.ammo=AmmoTransitions();self.policy=WeaponActionControl()
        self.last_shot={};self.seq=0;self.identity=None

    def sample(self,now,blocks,held_mask,focused):
        self.seq+=1
        identity=tuple((a,s['owner'],s['manager'],s['weapon_id']) for a,s,b in blocks)
        if identity!=self.identity or not focused:
            self.ammo=AmmoTransitions();self.policy.reset();self.last_shot.clear()
        self.identity=identity
        events=self.ammo.sample(now,blocks)
        for e in events:
            if e['weapon_id']==111300000:self.last_shot[e['mask']]=now
        present=0
        for address,s,raw in blocks:
            m=s['mask']
            if s['weapon_id']!=111300000 or len(raw)<0x2dc:continue
            d=decode(address,raw[:0x40],s['owner'])
            if not d or any(d[k]!=s[k] for k in ('manager','weapon_id','mask')):continue
            present|=m
            active=raw[0x2d0]==1 and 0<=now-self.last_shot.get(m,-1000)<=100
            self.policy.observe(m,(address,s['owner'],s['weapon_id']),self.seq,
                now,now,'sustain' if active else 'stop',verified=True,
                held=bool(held_mask&m),focused=focused)
        forces=self.policy.forces(now,held_mask&present,focused)
        if not focused or not present:self.last_shot.clear()
        # Stronger sustained gun resistance; retain the policy's stop/lease/cap.
        return tuple(4 if force else 0 for force in forces)
