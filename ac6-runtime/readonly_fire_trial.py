"""Bounded AC6 trial. Uses current ammunition, never an injected firing hook.

Measured charge/emission only. Unmeasured weapons use guarded active-fire/ammunition pulses only; no inferred charge or sustain. All output is cleared on
focus loss, player unload, equipment replacement, disconnect and exit.
"""
import ctypes as C
from ctypes import wintypes as W
import json,time,sys
from pathlib import Path
from current_weapon_slots import CurrentWeaponSlots
from ammo_transition import AmmoTransitions
from blade_attack_transition import BladeAttackTransitions
from charge_transition import ChargeControl
from blade_charge_control import BladeChargeControl
from sustain_transition import SustainControl
from trigger_gate import DualSenseInput
from trigger_output import TriggerOutput
from fire_pulse_profile import FirePulseProfile
from trigger_settings import TriggerSettings
from ac6_options import Options
from runtime_paths import application_root

def run(pid,seconds,path,parent_pid=None):
    if not (1<=seconds<=300 or (seconds==0 and parent_pid)): raise ValueError('Trial duration out of range')
    reader=CurrentWeaponSlots(pid); pad=None; output=None
    u=C.WinDLL('user32');u.GetForegroundWindow.restype=W.HWND
    u.GetWindowThreadProcessId.argtypes=[W.HWND,C.POINTER(W.DWORD)]
    k=C.WinDLL('kernel32');k.GetTickCount64.restype=C.c_ulonglong
    detector=AmmoTransitions(allow_structural=True); blade=BladeAttackTransitions(); charge=ChargeControl(); blade_charge=BladeChargeControl(); sustain=SustainControl(); old_identity=None
    profiles=FirePulseProfile();settings=TriggerSettings();options=Options()
    last_input=None;last_input_tick=-1000
    stop=Path(str(path)+'.stop'); parent=None
    k.GetExitCodeProcess.argtypes=[C.c_void_p,C.POINTER(W.DWORD)]
    def running(handle):
        result=W.DWORD()
        return bool(k.GetExitCodeProcess(handle,C.byref(result))) and result.value==259
    if parent_pid:
        parent=reader.k.OpenProcess(0x1000,False,parent_pid)
        if not parent:reader.close();raise RuntimeError('SenseBridge is not running')
    try:
        pad=DualSenseInput(application_root())
        output=TriggerOutput(pad)
        with open(path,'w',buffering=1,encoding='utf-8') as log:
            end=time.monotonic()+seconds if seconds else float('inf')
            log.write(json.dumps(dict(status='ready',pid=pid,session_mode=not seconds))+'\n')
            while time.monotonic()<end and not stop.exists() and running(reader.h) and (not parent or running(parent)):
                now=k.GetTickCount64(); foreground=W.DWORD()
                u.GetWindowThreadProcessId(u.GetForegroundWindow(),C.byref(foreground))
                buttons=pad.snapshot()
                if buttons is None: raise RuntimeError('Controller disconnected')
                output.positions=(buttons['lt'],buttons['rt'])
                held=(1 if buttons['lt']>=15 or u.GetAsyncKeyState(1)&0x8000 else 0)|(2 if buttons['rt']>=15 or u.GetAsyncKeyState(2)&0x8000 else 0)
                arms=reader.arms()
                identity=tuple((a,s['owner'],s['weapon_id']) for a,s in arms.items())
                settings_now=options.read()
                safe=foreground.value==pid and bool(arms) and settings_now['enabled']==1 and settings_now['triggers']==1
                input_state=(buttons['lt'],buttons['rt'],held,safe,identity)
                if input_state!=last_input and now-last_input_tick>=100:
                    log.write(json.dumps(dict(tick=now,input=dict(lt=buttons['lt'],rt=buttons['rt'],held=held,safe=safe),weapons=[dict(slot=s['slot'],weapon_id=s['weapon_id']) for s in arms.values()]))+'\n')
                    last_input=input_state;last_input_tick=now
                if not safe or identity!=old_identity:
                    output.pump(now,False);detector=AmmoTransitions(allow_structural=True);blade=BladeAttackTransitions();charge=ChargeControl();blade_charge=BladeChargeControl();sustain=SustainControl()
                    profiles=FirePulseProfile()
                old_identity=identity
                blocks=[(a,s,reader.read(a,0x480)) for a,s in arms.items()] if safe else []
                starts=[20,20]
                for s in arms.values():
                    starts[s['slot']]=settings.get(s['weapon_id'])
                output.starts=tuple(starts)
                # Revalidate the root after the multi-field read.
                if reader.arms()!=arms:
                    output.pump(now,False);detector=AmmoTransitions(allow_structural=True);blade=BladeAttackTransitions();charge=ChargeControl();blade_charge=BladeChargeControl();sustain=SustainControl();time.sleep(.01);continue
                for event in detector.sample(now,blocks):
                    ms,power=profiles.select(event)
                    event['pulse_ms']=ms
                    event['pulse_accepted']=output.pulse(event['mask'],ms,power,now) if event['weapon_id']!=111300000 else False
                    log.write(json.dumps(event)+'\n')
                for event in blade.sample(now,blocks):
                    event['pulse_accepted']=output.pulse(event['mask'],180,100,now)
                    log.write(json.dumps(event)+'\n')
                forces=charge.sample(now,blocks,held,safe)
                blade_forces=blade_charge.sample(now,blocks,held,safe)
                forces=tuple(max(a,b) for a,b in zip(forces,blade_forces))
                emission=sustain.sample(now,blocks,held,safe)
                forces=tuple(max(a,b) for a,b in zip(forces,emission))
                result=output.pump(now,safe,forces,emission)
                if result:log.write(json.dumps(dict(tick=now,output=result))+'\n')
                time.sleep(.01)
            log.write(json.dumps(dict(status='stopping'))+'\n')
    finally:
        try:
            if output:output.close()
        finally:
            if pad:pad.close()
            if parent:reader.k.CloseHandle(parent)
            reader.close()

if __name__=='__main__':run(int(sys.argv[1]),float(sys.argv[2]),sys.argv[3],int(sys.argv[4]) if len(sys.argv)>4 else None)
