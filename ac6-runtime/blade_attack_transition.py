"""BU-TT/A attack onset observed in short and held-attack captures.

The rise in weapon heat marks execution, NOT charge progress. No effect is
generated just because an input is held or a preparatory animation is active.
"""
import math,struct
from weapon_slots import decode

class BladeAttackTransitions:
    def __init__(self):self.previous={}
    def sample(self,tick,blocks):
        current={};events=[]
        for address,s,raw in blocks:
            if len(raw)<0x2dc or s['weapon_id']!=120100000:continue
            if decode(address,raw[:0x40],s['owner'])!=s:continue
            heat=struct.unpack_from('<f',raw,0x110)[0]
            if not math.isfinite(heat) or not 0<=heat<=1001:continue
            key=(address,s['owner'],s['manager'],s['weapon_id'])
            current[key]=(tick,heat)
            old=self.previous.get(key)
            if old and 0<=tick-old[0]<=100 and old[1]<1 and heat>=999:
                events.append(dict(mask=s['mask'],weapon_id=s['weapon_id'],source_tick=tick,
                                   source='validated-blade-execution-heat-rise'))
        self.previous=current
        return events
