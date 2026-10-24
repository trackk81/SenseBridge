"""Independent, bounded left/right effects using the existing USB report format."""
import ctypes as C

class TriggerOutput:
    def __init__(self,physical=None,sender=None):
        self.end=[0,0];self.force=[0,0];self.last=[-1000,-1000];self.sent=(0,0)
        self.sender=sender
        self.starts=(20,20);self.sent_starts=self.starts
        self.positions=(0,0);self.sent_modes=(0,0)
        # Actual shot/emission only; physical pull depth never suppresses recoil.
        self.automatic_recoil=True
        if physical:
            fn=physical.sdl.SDL_SendGamepadEffect
            fn.argtypes=[C.c_void_p,C.c_void_p,C.c_int];fn.restype=C.c_bool
            def send(data):
                b=C.create_string_buffer(data)
                return bool(fn(physical.pad,b,len(data)))
            self.sender=send
    def pulse(self,mask,ms,power,now):
        if mask not in (1,2,3) or not 30<=ms<=350 or not 1<=power<=100:return False
        accepted=False
        for i,base in enumerate((4,4)):
            if mask&(1<<i) and now-self.last[i]>=20:
                self.last[i]=now;self.end[i]=now+ms;self.force[i]=max(1,base*power//100);accepted=True
        return accepted
    def pump(self,now,enabled=True,continuous=(0,0),emission=(0,0)):
        if len(emission)!=2 or any(type(v)!=int or not 0<=v<=4 for v in emission):
            raise ValueError('Invalid emission force')
        if len(self.positions)!=2 or any(type(v)!=int or not 0<=v<=255 for v in self.positions):
            raise ValueError('Invalid trigger position')
        if len(continuous)!=2 or any(type(v)!=int or not 0<=v<=4 for v in continuous):
            raise ValueError('Invalid bounded continuous force')
        desired=tuple(max(self.force[i] if now<self.end[i] else 0,continuous[i]) if enabled else 0 for i in range(2))
        # Use cycling recoil throughout travel for confirmed shots/emission;
        # never switch recoil off merely because the trigger is fully pulled.
        # ds.daidr.me Automatic Trigger: mode 6, frequency/force/start parameters.
        modes=tuple(6 if self.automatic_recoil and desired[i] and (now<self.end[i] or emission[i]) else 1 if desired[i] else 5 for i in range(2))
        if not enabled:self.end=[0,0];self.force=[0,0]
        if desired==self.sent and modes==self.sent_modes and (not any(desired) or self.starts==self.sent_starts):return None
        packet=bytearray(47);packet[0]=12;packet[10]=packet[21]=5
        for i,o in enumerate((21,10)):
            if desired[i]:packet[o]=1;packet[o+1]=self.starts[i];packet[o+2]=desired[i]*25
            if modes[i]==6:
                # Match tester defaults at maximum configured force; no pull-depth gate.
                packet[o:o+11]=bytes([6,10,round(desired[i]*255/4),self.starts[i]])+bytes(7)
        ok=self.sender(bytes(packet))
        if not ok:raise RuntimeError('DualSense trigger output report rejected')
        self.sent=desired
        self.sent_starts=self.starts
        self.sent_modes=modes
        return {'forces':desired,'start_positions':self.starts,'modes':modes,'driver_accepted':True}
    def close(self):self.pump(0,False)

def test():
    packets=[];g=TriggerOutput(sender=lambda b:packets.append(b) or True);g.automatic_recoil=True
    assert g.pulse(1,200,100,100);g.pump(100)
    assert g.pulse(2,40,70,101);g.pump(101);assert g.sent==(4,2)
    g.pump(142);assert g.sent==(4,0)
    g.pump(300);assert g.sent==(0,0)
    assert not g.pulse(3,900,100,400)
    g.pulse(3,100,100,500);g.pump(500);g.pump(501,False);assert g.sent==(0,0)
    assert all(len(b)==47 for b in packets)
    assert packets[1][10]==packets[1][21]==6
    assert packets[-1][10]==packets[-1][21]==5
    g.pulse(3,110,100,700);g.pump(700)
    assert g.sent==(4,4) and packets[-1][12]==packets[-1][23]==255
    assert packets[-1][13]==packets[-1][24]==20
    g.starts=(50,30);g.pump(701)
    assert packets[-1][24]==50 and packets[-1][13]==30
    g.pump(810);assert g.sent==(0,0)
    bad=TriggerOutput(sender=lambda b:False);bad.pulse(2,40,70,0)
    try:bad.pump(0);raise AssertionError('driver error ignored')
    except RuntimeError:pass
    print('PASS simultaneous sides, independent expiry, limits, focus release, rejected driver report')

if __name__=='__main__':test()
