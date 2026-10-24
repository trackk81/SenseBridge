"""Bounded shot feedback from observed cadence, not guessed weapon names.

Unmeasured weapons retain structural-event evidence status. This does not
infer charge, melee, continuous emission, or sound ownership.
"""
class FirePulseProfile:
    def __init__(self):
        self.last={}

    def select(self,event):
        key=(event['mask'],event['weapon_id'])
        now=event['source_tick'];old=self.last.get(key)
        self.last[key]=now
        known={110110000:40,110300000:40,110510000:110,110720000:100}
        if event['weapon_id'] in known:
            return known[event['weapon_id']],100
        # Fast actual fire uses short pulses. Slow/single fire gets a distinct
        # bounded kick, independent of how long the input button was held.
        return (40 if old is not None and 0<=now-old<=120 else 100),100

def test():
    p=FirePulseProfile()
    def e(t,mask=2,w=199999999):
        return dict(mask=mask,weapon_id=w,source_tick=t)
    assert p.select(e(0))==(100,100)
    assert p.select(e(60))==(40,100)
    assert p.select(e(61,1))==(100,100)
    assert p.select(e(500))==(100,100)
    assert p.select(e(501,w=110510000))==(110,100)
    assert p.select(e(502,w=110300000))==(40,100)
    assert FirePulseProfile().select(e(503))==(100,100)
    print('PASS per-hand cadence, single/burst intervals, equipment-reset behavior and bounded profiles')

if __name__=='__main__':test()
