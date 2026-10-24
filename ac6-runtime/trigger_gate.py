"""Read-only XInput trigger observation; never injects or remaps game input.

Experimental temporal correlation, not proof of sound ownership. Only one
connected XInput slot is accepted, and both-trigger presses are ambiguous.
"""
import ctypes as C
import os

class Pad(C.Structure):
    _fields_=[('buttons',C.c_ushort),('lt',C.c_ubyte),('rt',C.c_ubyte),
              ('lx',C.c_short),('ly',C.c_short),('rx',C.c_short),('ry',C.c_short)]
class State(C.Structure):
    _fields_=[('packet',C.c_uint),('pad',Pad)]

class DualSenseInput:
    """SDL input only. No output reports, virtual pad, input injection or remap."""
    def __init__(self,folder):
        self.directory=os.add_dll_directory(str(folder));self.sdl=C.CDLL(str(folder/'SDL3.dll'));self.pad=None
        signatures={'SDL_InitSubSystem':([C.c_uint],C.c_bool),
            'SDL_SetHint':([C.c_char_p,C.c_char_p],C.c_bool),
            'SDL_GetGamepads':([C.POINTER(C.c_int)],C.POINTER(C.c_uint)),
            'SDL_OpenGamepad':([C.c_uint],C.c_void_p),
            'SDL_GetGamepadVendor':([C.c_void_p],C.c_ushort),
            'SDL_GetGamepadProduct':([C.c_void_p],C.c_ushort),
            'SDL_GamepadConnected':([C.c_void_p],C.c_bool),
            'SDL_GetGamepadAxis':([C.c_void_p,C.c_int],C.c_short),
            'SDL_GetGamepadButton':([C.c_void_p,C.c_int],C.c_bool),
            'SDL_CloseGamepad':([C.c_void_p],None),'SDL_PumpEvents':([],None),
            'SDL_free':([C.c_void_p],None),'SDL_QuitSubSystem':([C.c_uint],None)}
        for name,(args,result) in signatures.items():
            fn=getattr(self.sdl,name);fn.argtypes=args;fn.restype=result
        self.sdl.SDL_SetHint(b'SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS',b'1')
        try:
            if not self.sdl.SDL_InitSubSystem(0x2000):raise RuntimeError('DualSense input initialization failed')
            count=C.c_int();ids=self.sdl.SDL_GetGamepads(C.byref(count));matches=[]
            try:
                for i in range(count.value):
                    pad=self.sdl.SDL_OpenGamepad(ids[i])
                    if not pad:continue
                    if self.sdl.SDL_GetGamepadVendor(pad)==0x054c and self.sdl.SDL_GetGamepadProduct(pad) in (0x0ce6,0x0df2):matches.append(pad)
                    else:self.sdl.SDL_CloseGamepad(pad)
            finally:self.sdl.SDL_free(ids)
            if len(matches)!=1:
                for pad in matches:self.sdl.SDL_CloseGamepad(pad)
                raise RuntimeError('Exactly one physical DualSense input is required')
            self.pad=matches[0]
        except:
            self.close();raise
    def read(self):
        self.sdl.SDL_PumpEvents()
        if not self.sdl.SDL_GamepadConnected(self.pad):return None
        return tuple(max(0,min(255,round(self.sdl.SDL_GetGamepadAxis(self.pad,axis)*255/32767))) for axis in (4,5))
    def close(self):
        if self.pad:self.sdl.SDL_CloseGamepad(self.pad);self.pad=None
        self.sdl.SDL_QuitSubSystem(0x2000);self.directory.close()
    def snapshot(self):
        axes=self.read()
        if axes is None:return None
        # Record all SDL button indices; do not assume the user's game mapping.
        return {'lt':axes[0],'rt':axes[1],
                'buttons':[i for i in range(26) if self.sdl.SDL_GetGamepadButton(self.pad,i)]}

class TriggerGate:
    def __init__(self):
        self.api=C.WinDLL('xinput1_4');self.api.XInputGetState.argtypes=[C.c_uint,C.POINTER(State)]
        self.api.XInputGetState.restype=C.c_uint
        self.slot=None;self.previous=None;self.pressed=[False,False];self.edges=[None,None];self.pending=0
    def reset(self):
        self.slot=None;self.previous=None;self.pressed=[False,False];self.edges=[None,None];self.pending=0
    def update(self,values,slot,now,focused):
        if not focused or values is None:self.reset();return
        pressed=[v>=30 if not self.pressed[i] else v>=15 for i,v in enumerate(values)]
        if self.slot!=slot or self.previous is None:
            self.slot=slot;self.previous=pressed;self.pressed=pressed;self.edges=[None,None];self.pending=0;return
        for i in range(2):
            if pressed[i] and not self.previous[i]:self.edges[i]=now;self.pending|=1<<i
            if not pressed[i]:self.edges[i]=None
        self.previous=pressed;self.pressed=pressed
    def take_press(self):
        mask=self.pending;self.pending=0
        return mask
    def poll(self,now,focused):
        found=[]
        for slot in range(4):
            state=State()
            if self.api.XInputGetState(slot,C.byref(state))==0:found.append((slot,(state.pad.lt,state.pad.rt)))
        if len(found)!=1:self.reset();return
        self.update(found[0][1],found[0][0],now,focused)
    def event(self,label,now):
        if label not in ('ballistic_fire','missile_launch','energy_discharge'):return 0
        if sum(self.pressed)!=1:return 0
        i=self.pressed.index(True);edge=self.edges[i]
        if edge is None or not 0<=now-edge<=.16:return 0
        self.edges[i]=None
        return 1<<i

def test():
    g=TriggerGate();g.update((0,0),0,0,True);g.update((100,0),0,.1,True)
    assert g.event('mechanical_step',.11)==0
    assert g.event('missile_launch',.12)==1
    assert g.event('missile_launch',.13)==0
    g.update((0,0),0,.2,True);g.update((0,100),0,.3,True)
    assert g.event('energy_discharge',.32)==2
    g.update((0,0),0,.4,True);g.update((100,100),0,.5,True)
    assert g.event('ballistic_fire',.51)==0
    g.update((0,0),0,.6,True);g.update((100,0),0,.7,True)
    assert g.event('ballistic_fire',.9)==0
    g.update((0,0),0,1,False);g.update((100,0),0,1.1,True)
    assert g.event('ballistic_fire',1.12)==0
    g.reset();assert g.event('ballistic_fire',1.2)==0
    g.update((0,0),0,2,True);g.update((40,0),0,2.1,True)
    assert g.take_press()==1 and g.take_press()==0
    g.update((25,0),0,2.2,True);assert g.take_press()==0
    g.update((0,0),0,2.3,True);g.update((50,50),0,2.4,True)
    assert g.take_press()==3
    g.update((0,0),0,2.5,False);assert g.take_press()==0
    print('PASS trigger gate: left/right, non-weapon, single-shot, simultaneous, stale, focus, disconnect')
if __name__=='__main__':test()
