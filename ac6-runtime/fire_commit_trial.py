"""Version-specific accepted-shot candidate; requires native kind15 records.

The +0x20 field was 110300000 in all 279 observed HU-BEN accepted shots.
This is a session-observed discriminator, not a verified universal weapon ID.
Player ownership and projectile presentation timing remain unverified.
No source-audio or timer fallback is permitted.
"""
from attack_event_trial import AttackEventTrial

class FireCommitTrial(AttackEventTrial):
    def __init__(self,pid):
        super().__init__(pid);self.allowed=pid>0;self.last_shot=None;self.right_slot=None;self.slot_resolver=None
    def poll(self,now):return None
    def record(self,row,now):
        if len(row)!=7:return None
        if row[1]=='15':
            tick,weapon,count,weapon_id=map(int,(row[0],row[2],row[3],row[5]))
            if not self.allowed or self.previous is None or not 0<=now-tick<=100 or not 1<=count<=16:return None
            mask=2
            if self.slot_resolver:
                slot=self.slot_resolver(weapon)
                if not slot or slot['weapon_id']!=weapon_id or slot['mask']==0:return None
                mask=slot['mask']
                if not (self.previous if mask==1 else self.right_previous):return None
            else:
                # A weapon ID cannot establish player ownership or hand. In
                # particular HU-BEN may be on the left, so never assume R2.
                return None
            key=(tick,weapon)
            if key==self.last_shot:return None
            self.last_shot=key
            return dict(state=1,mask=mask,source_tick=tick,weapon_id=weapon_id,
                        weapon_object=weapon,accepted_shots=count,
                        scope='successful-fire-return; validated current owner, manager and arm slot')
        if row[1] in ('12','13') or (row[1]=='1' and row[3] in ('818944284','1315642802')):return None
        return super().record(row,now)

def test():
    g=FireCommitTrial(123)
    equipment={1234:dict(weapon_id=110300000,mask=2)}
    g.slot_resolver=equipment.get
    g.input(dict(lt=0,rt=0),0,True);g.input(dict(lt=0,rt=100),10,True)
    r=['200','15','1234','1','0','110300000','fire-commit-candidate']
    assert g.poll(100) is None
    assert g.record(['20','1','7','818944284','0','42',''],20) is None
    assert g.record(r,200)['accepted_shots']==1
    assert g.record(r,201) is None
    for index,value in [(0,'-1000'),(3,'0'),(3,'100'),(5,'0')]:
        bad=r.copy();bad[index]=value;assert g.record(bad,220) is None
    swapped=r.copy();swapped[0]='240';swapped[5]='130210000'
    assert g.record(swapped,240) is None
    other=swapped.copy();other[0]='260';other[2]='9999'
    assert g.record(other,260) is None
    fresh=FireCommitTrial(124);fresh.input(dict(lt=0,rt=0),0,True);fresh.input(dict(lt=0,rt=100),10,True)
    assert fresh.record(swapped,240) is None
    for tick,wid in [(270,110110000),(280,111300000)]:
        equipment[1234]['weapon_id']=wid
        new=r.copy();new[0]=str(tick);new[5]=str(wid)
        assert g.record(new,tick)['weapon_id']==wid
        new[0]=str(tick+1);new[2]='9999'
        assert g.record(new,tick+1) is None
    equipment.clear()
    assert g.record(new,281) is None  # old wrapper was destroyed
    equipment[5678]=dict(weapon_id=110300000,mask=1)
    g.input(dict(lt=100,rt=0),285,True)
    left=['290','15','5678','1','0','110300000','']
    assert g.record(left,290)['mask']==1
    g.allowed=False;left[0]='291';assert g.record(left,291) is None
    g.allowed=True
    g.input(dict(lt=0,rt=0),300,True)
    r[0]='310';assert g.record(r,310) is None
    g.input(dict(lt=0,rt=100),320,False)
    r[0]='330';assert g.record(r,330) is None
    print('PASS: shot-only, no audio/timer fallback, ID filtering, failed/duplicate/stale record, release/focus')
if __name__=='__main__':test()
