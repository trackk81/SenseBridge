"""Current-player shoulder ammunition feedback. Never sends trigger effects."""
from weapon_slots import decode
from ammo_transition import AmmoTransitions

class BackWeaponHaptics:
    def __init__(self, reader):
        self.reader=reader
        self.detector=AmmoTransitions(allow_structural=True,allow_back=True)
    def sample(self, now, focused):
        arms=self.reader.arms() if focused else {}
        blocks=[]
        if arms:
            base=min(arms)
            owner=self.reader.owner;manager=self.reader.manager
            for slot in (2,3):
                address=base+slot*0x480
                raw=self.reader.read(address,0x480)
                identity=decode(address,raw[:64],owner)
                if identity and identity['slot']==slot and identity['manager']==manager:
                    blocks.append((address,identity,raw))
            if self.reader.arms()!=arms:blocks=[]
        return self.detector.sample(now,blocks)
