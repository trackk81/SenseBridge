"""Session-scoped L2 sound-event trial, not a general equipment mapping.

Input alone never emits feedback. A preparation PostEvent must be followed by
the observed attack Play request. This does not prove audible hardware latency.
"""
class AttackEventTrial:
    def __init__(self,pid):
        self.allowed=pid==45384
        self.previous=None;self.press=None;self.prepared=None;self.seen=set();self.prepared_owner=None
        self.right_previous=None;self.right_press=None;self.right=None;self.left_until=0
    def reset(self):
        self.previous=None;self.press=None;self.prepared=None;self.prepared_owner=None
        self.right_previous=None;self.right_press=None;self.right=None;self.left_until=0
    def input(self,sample,tick,focused):
        if not focused or sample is None:self.reset();return
        down=sample['lt'] >= (15 if self.previous else 30)
        if self.previous is not None and down and not self.previous:self.press=tick
        self.previous=down
        right=sample.get('rt',0)>=(15 if self.right_previous else 30)
        if self.right_previous is not None and right and not self.right_previous:self.right_press=tick
        if not right:self.right=None
        self.right_previous=right
    def poll(self,now):
        r=self.right
        if not r:return None
        if now-r['start']>=8000 or not self.right_previous:self.right=None;return None
        if now<r['next'] or now<self.left_until:return None
        r['next']=now+110
        return dict(state=1,mask=2,event=818944284,playing_id=r['playing'],
                    source_tick=r['start'],attack_object=r['owner'],
                    scope='R2 input plus observed loop start; bounded repeat')
    def record(self,row,now):
        if not self.allowed or len(row)!=7:return None
        if row[1]=='4':self.reset();return None
        if row[1]!='1':return None
        tick,owner,event,playing=int(row[0]),int(row[2]),int(row[3]),int(row[5])
        if playing<=0 or not 0<=now-tick<=150:return None
        if self.previous is None:return None
        if event==1315642802:
            if self.right and self.right['owner']==owner:self.right=None
            return None
        if event==818944284:
            if self.right_previous and self.right_press is not None and 0<=tick-self.right_press<=800 and not self.previous:
                self.right=dict(start=tick,next=now,owner=owner,playing=playing)
                self.right_press=None
            return None
        if event==1040550297:
            if self.press is not None and 0<=tick-self.press<=180:
                self.prepared=tick;self.prepared_owner=owner;self.press=None
            elif self.prepared is not None:
                # Multiple preparation objects make this window ambiguous.
                self.prepared=None;self.prepared_owner=None
            return None
        if event!=1074105598:return None
        if self.prepared is None or not 60<=tick-self.prepared<=1200:return None
        if playing in self.seen:return None
        self.seen.add(playing)
        if len(self.seen)>4096:self.seen={playing}
        delay=tick-self.prepared;prepared_owner=self.prepared_owner;self.prepared=None
        self.left_until=now+220
        return {'state':0,'mask':1,'event':event,'playing_id':playing,
                'source_tick':tick,'prepare_to_attack_ms':delay,
                'prepare_object':prepared_owner,'attack_object':owner,
                'ownership':'input-correlated candidate; not independently verified',
                'scope':'current-session-L2-play-request-trial'}

def test():
    def row(t,event,owner,playing=1):return [str(t),'1',str(owner),str(event),'0',str(playing),'']
    g=AttackEventTrial(45384)
    g.input({'lt':0},1000,True);g.input({'lt':100},1100,True)
    assert g.record(row(1130,1040550297,386445800),1130) is None
    assert g.record(row(1140,1074105598,3646086017),1140) is None
    assert g.record(row(1800,123,999),1800) is None
    assert g.record(row(1800,1074105598,280560134),1800)['mask']==1
    assert g.record(row(1800,1074105598,3646086017),1800) is None
    g.input({'lt':0},2000,True);g.input({'lt':100},2100,True)
    g.record(row(2130,1040550297,386445800),2130)
    g.input({'lt':100},2140,False)
    assert g.record(row(2500,1074105598,3646086017,2),2500) is None
    assert not AttackEventTrial(123).allowed
    g.input({'lt':0},3000,True);g.input({'lt':100},3100,True)
    g.record(row(3130,1040550297,111),3130)
    g.record(row(3140,1040550297,222),3140)
    assert g.record(row(3500,1074105598,333,3),3500) is None
    g.reset();g.input({'lt':0,'rt':0},4000,True);g.input({'lt':0,'rt':100},4100,True)
    assert g.poll(4100) is None
    g.record(row(4400,818944284,456,10),4400)
    assert g.poll(4400)['mask']==2 and g.poll(4450) is None
    assert g.poll(4510)['mask']==2
    g.record(row(4530,1315642802,789,11),4530)
    assert g.right is not None
    g.record(row(4540,1315642802,456,12),4540)
    assert g.poll(4620) is None
    g.input({'lt':0,'rt':0},4700,True);g.input({'lt':0,'rt':100},4800,True)
    g.record(row(4900,818944284,789,13),4900)
    assert g.poll(12900) is None
    print('PASS: no input-only output, preparation delay, changed object IDs, duplicate, focus loss, ambiguous preparations, session guard')
if __name__=='__main__':test()
