"""R2 acoustic transient feedback driven only by live source-position samples."""
from attack_event_trial import AttackEventTrial

class SourcePositionTrial(AttackEventTrial):
    def __init__(self,pid,analysis):
        super().__init__(pid)
        self.allowed=pid>0
        self.duration=float(analysis['duration_ms'])
        self.onsets=tuple(float(x) for x in analysis['onsets_ms'])
        assert 0<self.duration<=10000
        assert self.onsets==tuple(sorted(set(self.onsets)))
        assert all(0<=x<self.duration for x in self.onsets)
        self.last_pulse=-1000
    def poll(self,now):
        # No wall-clock pulse generator and no fallback to fixed-rate feedback.
        if self.right and now-self.right['start']>=8000:self.right=None
        return None
    def record(self,row,now):
        if len(row)!=7:return None
        if row[1] not in ('12','13'):return super().record(row,now)
        r=self.right
        if not r:return None
        tick,owner,playing,pos,result=map(int,(row[0],row[2],row[3],row[4],row[5]))
        if owner!=r['owner'] or playing!=r['playing']:return None
        if row[1]=='13':self.right=None;return None
        if result!=1 or not 0<=now-tick<=100 or not self.right_previous:return None
        if now-r['start']>=8000:self.right=None;return None
        if not 0<=pos<=self.duration+1:
            # Do not guess modulo semantics if this build returns elapsed time.
            self.right=None;return None
        prior=r.get('position');prior_tick=r.get('position_tick')
        r['position']=pos;r['position_tick']=tick
        if prior is None:
            if pos>100:return None
            ranges=[(-1,pos)]
        elif tick-prior_tick>100:return None
        elif pos==prior:return None
        elif pos>prior:
            if pos-prior>150:return None
            ranges=[(prior,pos)]
        elif prior>=self.duration-150 and pos<150:
            ranges=[(prior,self.duration),(-1,pos)]
        else:return None # seek/discontinuity, not a new shot
        hits=[x for lo,hi in ranges for x in self.onsets if lo<x<=hi]
        if not hits or now-self.last_pulse<40 or now<self.left_until:return None
        self.last_pulse=now
        return dict(state=1,mask=2,event=818944284,playing_id=playing,
                    source_tick=tick,source_position_ms=pos,acoustic_onset_ms=hits[-1],
                    attack_object=owner,scope='live-source-position/acoustic-transient',
                    individual_projectile_verified=False)

def test():
    g=SourcePositionTrial(9,dict(duration_ms=1000,onsets_ms=[20,85,170,990]))
    g.input(dict(lt=0,rt=0),0,True);g.input(dict(lt=0,rt=100),100,True)
    g.record(['110','1','7','818944284','0','42',''],110)
    assert g.poll(1000) is None
    def pos(t,p,id=42):return g.record([str(t),'12','7',str(id),str(p),'1',''],t)
    assert pos(120,10) is None
    assert pos(130,25)['acoustic_onset_ms']==20
    assert pos(150,25) is None
    assert pos(190,90)['acoustic_onset_ms']==85
    assert pos(210,170,43) is None
    assert pos(270,175)['acoustic_onset_ms']==170
    assert pos(500,990) is None # missed samples must not produce catch-up bursts
    assert pos(530,25)['acoustic_onset_ms']==20
    g.record(['540','13','7','42','0','1',''],540)
    assert pos(590,90) is None
    g.input(dict(lt=0,rt=0),600,False);assert g.poll(610) is None
    g.input(dict(lt=0,rt=0),700,True);g.input(dict(lt=0,rt=100),710,True)
    g.record(['720','1','7','818944284','0','42',''],720)
    g.record(['730','4','0','1','0','0','dropped'],730)
    assert pos(750,90) is None
    g.input(dict(lt=0,rt=100),760,True)
    g.record(['770','1','7','818944284','0','43',''],770)
    assert pos(790,90,43) is None # held input cannot rearm after a gap
    print('PASS: irregular acoustic positions, no timer output, duplicate/foreign/stale samples, wrap, stop, focus')
if __name__=='__main__':test()
