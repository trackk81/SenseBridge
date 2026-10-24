"""Measured boost-loop start/stop pair, bank target 316695819.

3320838179 Play and 346378969 Stop were observed during ascent.
This is world-effect routing, not a claim of player ownership for every object.
"""
class BoostHaptics:
    # Measured ground loop lasted 4.219 seconds. A short two-second guard
    # truncated normal thrust. Retain a finite failsafe for lost Stop events.
    MAX_LOOP_MS=8000
    # cs_main.bnk Play/Stop actions have matching target IDs. Keep loops
    # independent: stopping ground boost must not cancel ascent on that object.
    STARTS={3320838179:316695819,3320838178:600420960}
    STOPS={346378969:316695819,346378968:600420960}
    # Repeated offline captures: quick-boost ignition and assault ignition.
    # These are bounded onset effects, not inferred continuous thrust.
    IMPULSES={3320838177,3304060489}
    def __init__(self):self.active={};self.last=-1000;self.impulse=False
    def record(self,row,now):
        if len(row)!=7 or row[1]!='1':return
        try:tick,owner,event,result=map(int,(row[0],row[2],row[3],row[5]))
        except ValueError:return
        if not 0<=now-tick<=150 or result<=0:return
        if event in self.STARTS:
            key=(owner,self.STARTS[event])
            if key in self.active or len(self.active)<8:self.active[key]=now+self.MAX_LOOP_MS
        if event in self.STOPS:self.active.pop((owner,self.STOPS[event]),None)
        if event in self.IMPULSES:self.impulse=True
    def poll(self,now,safe):
        if not safe:self.active.clear();self.impulse=False;return False
        self.active={o:t for o,t in self.active.items() if t>now}
        if self.impulse:
            self.impulse=False
            if now-self.last>=120:self.last=now;return True
        if self.active and now-self.last>=120:self.last=now;return True
        return False

def test():
    g=BoostHaptics()
    def row(t,o,e,r=1):return list(map(str,(t,1,o,e,0,r)))+['']
    g.record(row(100,7,3320838179),100);assert g.poll(100,True)
    assert not g.poll(110,True)
    g.record(row(120,8,346378969),120);assert g.poll(230,True)
    g.record(row(240,7,346378969),240);assert not g.poll(400,True)
    g.record(row(500,7,3320838179),500);assert not g.poll(501,False)
    assert not g.poll(700,True)
    g.record(row(800,7,3320838179),800);assert g.poll(2801,True)
    assert not g.poll(8801,True)
    g.record(row(3000,7,3320838179,0),3000);assert not g.poll(3000,True)
    g.record(row(3000,7,3320838179),3300);assert not g.poll(3300,True)
    g.record(row(3400,7,3320838178),3400);assert g.poll(3400,True)
    g.record(row(3500,7,346378969),3500);assert g.poll(3530,True)
    g.record(row(3600,7,346378968),3600);assert not g.poll(3700,True)
    for event in g.IMPULSES:
        g.record(row(4000,7,event),4000);assert g.poll(4000,True)
        assert not g.poll(4200,True)
        g=BoostHaptics()
    print('PASS boost start/owner stop, focus clear, duration cap, stale and failed events')
if __name__=='__main__':test()
