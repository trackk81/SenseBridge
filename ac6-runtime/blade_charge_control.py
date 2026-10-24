"""BU-TT/A late-windup resistance from measured animation state.
Not a decoded game charge gauge. Early shared tap/hold windup is excluded.
"""
import math,struct
from weapon_slots import decode

class BladeChargeControl:
    START=.55
    END=1.02
    def __init__(self):self.previous={}
    def sample(self,now,blocks,held_mask,focused):
        result=[0,0];current={}
        if not focused:self.previous={};return tuple(result)
        for address,s,raw in blocks:
            if s['weapon_id']!=120100000 or s['slot'] not in (0,1) or s['mask']!=1<<s['slot'] or len(raw)<0x2dc:continue
            if decode(address,raw[:0x40],s['owner'])!=s:continue
            heat=struct.unpack_from('<f',raw,0x110)[0]
            elapsed=struct.unpack_from('<f',raw,0x2d8)[0]
            action=struct.unpack_from('<i',raw,0x2d4)[0]
            if not all(math.isfinite(v) for v in (heat,elapsed)):continue
            key=(address,s['owner'],s['manager'],s['weapon_id'])
            old=self.previous.get(key)
            advancing=old and 0<=now-old[0]<=100 and old[1]<=elapsed and elapsed-old[1]<=.15
            changed=now if not old or elapsed!=old[1] else old[2]
            current[key]=(now,elapsed,changed)
            if (held_mask&s['mask'] and raw[0x2d0]==1 and action==707 and 0<=heat<1
                    and self.START<=elapsed<=1.10 and advancing and now-changed<100):
                progress=min(1,max(0,(elapsed-self.START)/(self.END-self.START)))
                result[s['slot']]=1+int(progress*3)
        self.previous=current
        return tuple(result)
