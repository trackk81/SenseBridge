"""Internal AC6 event-to-haptic trial. World effects, NOT player-only effects.

Uses the unmodified v0.1.6 output engine. No input injection, trigger binding,
speaker playback, game writes or DLL loading. Start diagnostic connector first.
"""
from pathlib import Path
import argparse, ctypes as C, ctypes.wintypes as W, json, os, secrets
import socket, struct, subprocess, time, msvcrt, hashlib, collections

ROOT = Path(__file__).resolve().parents[1]
DATA_ROOT = Path(os.environ.get('SENSEBRIDGE_AC6_DATA_ROOT', str(ROOT)))
OUT = ROOT / 'live-test'
from runtime_paths import application_root
ENGINE = application_root() / 'SenseBridge.Events.exe'
k = C.WinDLL('kernel32', use_last_error=True)
k.CreateFileW.argtypes = [W.LPCWSTR,W.DWORD,W.DWORD,C.c_void_p,W.DWORD,W.DWORD,W.HANDLE]
k.CreateFileW.restype = W.HANDLE
k.GetTickCount64.restype = C.c_ulonglong

def prepare():
    if (DATA_ROOT/'events.bin').is_file():
        bank=(DATA_ROOT/'events.bin').read_bytes();manifest=json.loads((DATA_ROOT/'event-map.json').read_text())
        labels=manifest['labels'];mapping={int(k):v for k,v in manifest['mapping'].items()}
        assert bank[:8]==b'SBEVENT1' and struct.unpack_from('<I',bank,8)[0]==len(labels) and 0<len(labels)<=1000
        assert all(0<k<2**32 and 0<=v<len(labels) for k,v in mapping.items())
        OUT.mkdir(exist_ok=True);(OUT/'events.bin').write_bytes(bank)
        (OUT/'trial-rules.json').write_text(json.dumps({'labels':labels,'rules':mapping},indent=2))
        return mapping,labels
    candidates = json.loads((DATA_ROOT/'release-prep/ac6-candidates.json').read_text())['rules']
    waves = (DATA_ROOT/'release-prep/ac6-candidate-waveforms.bin').read_bytes()
    by_label = {r['label']: r for r in candidates}
    labels = sorted(by_label)
    bank = bytearray(b'SBEVENT1' + struct.pack('<I',len(labels)))
    for label in labels:
        r=by_label[label]; pcm=waves[r['offset']:r['offset']+r['frames']*2]
        assert len(pcm)==r['frames']*2 and 0<r['frames']<=26400
        assert max(abs(x[0]) for x in struct.iter_unpack('<h',pcm))<=4588
        bank.extend(struct.pack('<III',r['frames'],0,0)); bank.extend(pcm)
    rules=json.loads((DATA_ROOT/'release-prep/ac6-event-rules.json').read_text())['rules']
    mapping={int(r['event_id']):labels.index(r['label']) for r in rules}
    # Append per-source AI textures; preserve existing mappings as fallback.
    texture_dir=DATA_ROOT/'research/ai-textures'
    if (texture_dir/'manifest.json').exists():
        textures=json.loads((texture_dir/'manifest.json').read_text())
        extra=(texture_dir/'events.bin').read_bytes()
        count=struct.unpack_from('<I',extra,8)[0]
        assert extra[:8]==b'SBEVENT1' and count==len(textures['entries'])
        base=len(labels); assert base+count<=1000
        bank[8:12]=struct.pack('<I',base+count)
        bank.extend(extra[12:])
        labels.extend(e['label'] for e in textures['entries'])
        mapping.update({int(event):base+clip for event,clip in textures['mapping'].items()})
    OUT.mkdir(exist_ok=True)
    (OUT/'events.bin').write_bytes(bank)
    (OUT/'trial-rules.json').write_text(json.dumps({'scope':'world-effects-experimental',
        'player_ownership_verified':False,'speaker':False,'triggers':False,
        'labels':labels,'rules':mapping},indent=2))
    return mapping,labels

def packet_for(row,mapping,now):
    if len(row)!=7 or row[1]!='1': return None
    try:
        tick,event,result=int(row[0]),int(row[3]),int(row[5])
    except ValueError:return None
    # Ignore stale events, malformed data, failed posts and unmatched events.
    if result<=0 or not 0<=now-tick<=150 or event not in mapping:return None
    return mapping[event]

def selftest(mapping):
    event=next(iter(mapping));good=['1000','1','123',str(event),'0','1','']
    assert packet_for(good,mapping,1100)==mapping[event]
    assert packet_for(good,mapping,1200) is None
    assert packet_for(good,mapping,999) is None
    for index,value in [(1,'2'),(3,'0'),(3,'broken'),(5,'0')]:
        bad=good.copy();bad[index]=value;assert packet_for(bad,mapping,1100) is None
    assert packet_for([],mapping,1100) is None
    subprocess.run([str(ENGINE),'--validate',str(OUT/'events.bin')],check=True)
    print('PASS: bank limits, stale/future events, failed posts, non-play records, unknown events')

def shared_reader(path):
    handle=k.CreateFileW(str(path),0x80000000,3,None,3,0,None)
    if handle==C.c_void_p(-1).value:raise C.WinError(C.get_last_error())
    fd=msvcrt.open_osfhandle(handle,os.O_RDONLY|os.O_BINARY)
    return os.fdopen(fd,'rb',buffering=0)

def run(pid,trace,mapping,radio_trial=False,trigger_trial=False,attack_event_trial=False,source_position_trial=False,fire_commit_trial=False,parent_pid=None):
    token=secrets.token_hex(16)
    with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as reserve:
        reserve.bind(('127.0.0.1',0));port=reserve.getsockname()[1]
    config=OUT/'live.ini';stop=Path(str(config)+'.stop')
    if stop.exists():raise RuntimeError('Previous stop marker exists; explicitly clear it before starting a new trial')
    states=OUT/'trigger-states.tsv';states.write_text('blade\ttrial\t0\t200\t100\ngatling\ttrial\t1\t40\t70\n')
    if attack_event_trial and not (source_position_trial or fire_commit_trial) and pid!=45384:raise RuntimeError('Attack mapping belongs to the recorded session only')
    # R2 loop 818944284 precedes visible firing per user observation.
    # Keep output off across restart until discharge/bullet onset is established.
    config.write_text(f'bank={OUT / "events.bin"}\nstates={states}\ntoken={token}\nport={port}\nenabled=1\nhaptics=1\ngain=1.5\nspeaker={int(radio_trial)}\ntriggers=0\nleft_force={4 if attack_event_trial else 0}\nright_force={3 if fire_commit_trial else 0}\n')
    # No replay of old records on connection. Wait for live diagnostic heartbeat.
    reader=shared_reader(trace);reader.seek(0,2)
    log=(OUT/'relay-events.jsonl').open('w',buffering=1)
    output=(OUT/'engine-output.log').open('w')
    engine=None;radio=None;gate=None;physical_input=None;health=None;slots=None;trigger_output=None;health_at=0;hook_failed=False;udp=socket.socket(socket.AF_INET,socket.SOCK_DGRAM)
    back_reader=None;back=None
    from boost_haptics import BoostHaptics
    boost=BoostHaptics()
    from ac6_options import Options
    options=Options();last_options=None
    probe_addresses=set();probe_previous={};probe_at=0
    labels=json.loads((OUT/'trial-rules.json').read_text())['labels']
    user=C.WinDLL('user32');user.GetForegroundWindow.restype=W.HWND
    user.GetWindowThreadProcessId.argtypes=[W.HWND,C.POINTER(W.DWORD)]
    hello=0;last_trace=time.monotonic();pending=b'';recent={};sent=0;last_input_log=0;previous_input=None;quarantine_until=0
    parent_handle=None
    if parent_pid:
        k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];k.OpenProcess.restype=W.HANDLE
        k.GetExitCodeProcess.argtypes=[W.HANDLE,C.POINTER(W.DWORD)]
        k.CloseHandle.argtypes=[W.HANDLE]
        parent_handle=k.OpenProcess(0x1000,False,parent_pid)
        if not parent_handle:raise RuntimeError('SenseBridge parent unavailable')
    try:
        from current_weapon_slots import CurrentWeaponSlots
        from back_weapon_haptics import BackWeaponHaptics
        back_reader=CurrentWeaponSlots(pid);back=BackWeaponHaptics(back_reader)
        if trigger_trial or attack_event_trial:
            from trigger_gate import DualSenseInput
            physical_input=DualSenseInput(ENGINE.parent)
            from trigger_output import TriggerOutput
            trigger_output=TriggerOutput(physical_input)
            if attack_event_trial:
                if fire_commit_trial:
                    from fire_commit_trial import FireCommitTrial
                    gate=FireCommitTrial(pid)
                    from fire_hook_health import FireHookHealth
                    health=FireHookHealth(pid)
                    from current_weapon_slots import CurrentWeaponSlots
                    slots=CurrentWeaponSlots(pid);gate.slot_resolver=slots.snapshot
                    probe_addresses=set(slots.arms())
                    log.write(json.dumps({'tick':int(k.GetTickCount64()),'weapon_slot_owner':slots.owner,
                                          'arm_routing':'current player root; slot0:L2 slot1:R2; no historical weapon anchor'})+'\n')
                    initial=health.read()
                    if initial['state']!='connected':raise RuntimeError('Fire hook unavailable: '+str(initial))
                    log.write(json.dumps({'tick':int(k.GetTickCount64()),'fire_hook_health':initial})+'\n')
                elif source_position_trial:
                    from source_position_trial import SourcePositionTrial
                    gate=SourcePositionTrial(pid,json.loads((ROOT/'research/huben-acoustic-onsets.json').read_text()))
                else:
                    from attack_event_trial import AttackEventTrial
                    gate=AttackEventTrial(pid)
            log.write(json.dumps({'input_backend':'physical-dualsense-sdl','output_enabled':attack_event_trial,'mode':'event-gated-trial' if attack_event_trial else 'attack-correlation-observation'})+'\n')
        if radio_trial:
            from radio_output import RadioOutput
            radio=RadioOutput(ENGINE.parent,pid,Path(os.environ.get('SENSEBRIDGE_AC6_RADIO_CACHE',str(ROOT/'research'))))
        engine=subprocess.Popen([str(ENGINE),str(config),str(os.getpid()),str(pid)],stdout=output,stderr=subprocess.STDOUT,creationflags=0x08000000)
        while engine.poll() is None and not stop.exists():
            if parent_handle:
                parent_code=W.DWORD()
                if not k.GetExitCodeProcess(parent_handle,C.byref(parent_code)) or parent_code.value!=259:break
            now=time.monotonic();chunk=reader.read(65536)
            settings_now=options.read()
            if settings_now!=last_options:
                d=dict(line.split('=',1) for line in config.read_text().splitlines() if '=' in line)
                d['haptics']=str(int(settings_now['enabled']*settings_now['haptics']))
                d['speaker']=str(int(settings_now['enabled']*settings_now['speaker']))
                d['gain']=str(settings_now['gain']);d['speaker_volume']=str(settings_now['speaker_volume'])
                temp=config.with_suffix('.pending');temp.write_text(''.join(f'{key}={value}\n' for key,value in d.items()));temp.replace(config)
                last_options=settings_now.copy()
            haptic_on=settings_now['enabled']==1 and settings_now['haptics']==1
            speaker_on=settings_now['enabled']==1 and settings_now['speaker']==1
            if radio:
                radio.volume=settings_now['speaker_volume']/100
                if not speaker_on:radio.stop()
            foreground=W.DWORD();user.GetWindowThreadProcessId(user.GetForegroundWindow(),C.byref(foreground))
            for shot in back.sample(int(k.GetTickCount64()),haptic_on and foreground.value==pid and now-hello<=1.5):
                clip=labels.index('missile_launch')
                udp.sendto(f'{token} {clip} 1.0 1.0 0'.encode(),('127.0.0.1',port))
                log.write(json.dumps({'tick':int(k.GetTickCount64()),'shoulder_haptic':shot,'clip':clip,'texture':'bounded-generic-recoil'})+'\n')
            if slots and not hook_failed and now-probe_at>=.02:
                from charge_state_probe import snapshot as charge_snapshot
                probe_at=now
                probe_addresses=set(slots.arms())
                for address in tuple(probe_addresses):
                    candidate=charge_snapshot(slots,address)
                    if candidate!=probe_previous.get(address):
                        probe_previous[address]=candidate
                        if log.tell()<16*1024*1024:
                            log.write(json.dumps({'tick':int(k.GetTickCount64()),'charge_candidate':candidate,
                                                  'wrapper':address,'output_enabled':False})+'\n')
            if health and now-health_at>=.25:
                state=health.read();health_at=now
                (OUT/'fire-hook-status.json').write_text(json.dumps({'tick':int(k.GetTickCount64()),**state,'latched_failure':hook_failed}))
                if state['state']!='connected' and not hook_failed:
                    hook_failed=True;gate.allowed=False;gate.reset()
                    log.write(json.dumps({'tick':int(k.GetTickCount64()),'fire_hook_lost':state,'action':'disable trigger events; no automatic repatch'})+'\n')
            if physical_input:
                foreground=W.DWORD();user.GetWindowThreadProcessId(user.GetForegroundWindow(),C.byref(foreground))
                values=physical_input.snapshot()
                if values is None:raise RuntimeError('Physical DualSense input disconnected')
                observation={**values,'game_focused':foreground.value==pid}
                if gate:gate.input(values,int(k.GetTickCount64()),foreground.value==pid and int(k.GetTickCount64())>=quarantine_until)
                if observation!=previous_input or now-last_input_log>=1:
                    if log.tell()<16*1024*1024:
                        log.write(json.dumps({'tick':int(k.GetTickCount64()),'input_sample':True,**observation})+'\n')
                    previous_input=observation;last_input_log=now
            if chunk:pending+=chunk
            lines=pending.split(b'\n');pending=lines.pop()
            if len(pending)>4096:raise RuntimeError('Invalid diagnostic record')
            for line in lines:
                row=line.decode('ascii',errors='strict').strip().split(',')
                tick=int(k.GetTickCount64())
                if len(row)==7 and row[1]=='4':
                    boost.poll(tick,False)
                    if gate:gate.reset()
                    if radio:radio.stop()
                    recent.clear();quarantine_until=tick+500
                    log.write(json.dumps({'tick':tick,'trace_gap':row[3],'action':'discard active attack; wait for fresh input and start after 500ms'})+'\n')
                    continue
                if len(row)==7 and row[1]=='0' and 0<=tick-int(row[0])<1500:
                    last_trace=now
                    udp.sendto(f'{token} HELLO3 {pid}'.encode(),('127.0.0.1',port));hello=now
                if len(row)==7 and int(row[0])<quarantine_until:continue
                boost.record(row,tick)
                if gate:
                    command=gate.record(row,tick)
                    if slots and len(row)==7 and row[1]=='15' and slots.snapshot(int(row[2])):
                        if len(probe_addresses)<8:probe_addresses.add(int(row[2]))
                    if command and not hook_failed and now-hello<=1.5:
                        duration,power=(200,100) if command['state']==0 else (40,70)
                        command['pulse_accepted']=trigger_output.pulse(command['mask'],duration,power,tick)
                        log.write(json.dumps({'tick':tick,'attack_trigger':command})+'\n')
                if radio and (speaker_on or (len(row)==7 and row[1]=='11')):
                    radio_event=radio.record(row,tick)
                    if radio_event:log.write(json.dumps(radio_event)+'\n')
                clip=packet_for(row,mapping,tick)
                if not haptic_on or clip is None or now-hello>1.5 or now-recent.get(clip,0)<.075:continue
                recent[clip]=now
                mask=0
                gain=1.0 if labels[clip] in ('jet_boost','missile_launch') else .7
                udp.sendto(f'{token} {clip} {gain} 1.0 0'.encode(),('127.0.0.1',port))
                if sent<10000:log.write(json.dumps({'tick':tick,'event':row[3],'object':row[2],'clip':clip,'scope':'world-effects','trigger_mask':mask})+'\n')
                sent+=1
            if now-last_trace>2:raise RuntimeError('Diagnostic heartbeat stopped')
            if gate and now-hello<=1.5:
                command=gate.poll(int(k.GetTickCount64()))
                if command:
                    udp.sendto(f'{token} STATE {command["state"]} {command["mask"]}'.encode(),('127.0.0.1',port))
                    log.write(json.dumps({'tick':int(k.GetTickCount64()),'attack_trigger':command})+'\n')
            if trigger_output:
                safe=(physical_input is not None and previous_input is not None and previous_input.get('game_focused',False) and not hook_failed and now-hello<=1.5)
                output_result=trigger_output.pump(int(k.GetTickCount64()),safe)
                if output_result:log.write(json.dumps({'tick':int(k.GetTickCount64()),'trigger_output':output_result})+'\n')
            if boost.poll(int(k.GetTickCount64()),haptic_on and foreground.value==pid and now-hello<=1.5):
                clip=labels.index('jet_boost')
                udp.sendto(f'{token} {clip} 1.0 1.0 0'.encode(),('127.0.0.1',port))
                log.write(json.dumps({'tick':int(k.GetTickCount64()),'boost_haptic':True,'clip':clip})+'\n')
            if radio:radio.pump()
            time.sleep(.01)
        if engine.poll() not in (None,0):raise RuntimeError('Output engine failed; inspect engine-output.log')
    finally:
        stop.write_text('stop')
        if health:health.close()
        if parent_handle:k.CloseHandle(parent_handle)
        if slots:slots.close()
        if back_reader:back_reader.close()
        if trigger_output:
            try:trigger_output.close()
            except Exception as error:print('Trigger cleanup error:',error,flush=True)
        if physical_input:
            try:physical_input.close()
            except Exception as error:print('Input cleanup error:',error,flush=True)
        if radio:
            try:radio.close()
            except Exception as error:print('Radio cleanup error:',error,flush=True)
        if engine:
            try:engine.wait(timeout=5)
            except subprocess.TimeoutExpired:
                # Leave marker and process alive for normal cleanup; never kill the game.
                print('Engine cleanup still pending',flush=True)
        reader.close();udp.close();log.close();output.close()

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--self-test',action='store_true')
    parser.add_argument('--pid',type=int);parser.add_argument('--trace',type=Path)
    parser.add_argument('--radio-trial',action='store_true')
    parser.add_argument('--trigger-trial',action='store_true')
    parser.add_argument('--attack-event-trial',action='store_true')
    parser.add_argument('--source-position-trial',action='store_true')
    parser.add_argument('--fire-commit-trial',action='store_true')
    parser.add_argument('--output-dir',type=Path)
    parser.add_argument('--parent-pid',type=int)
    args=parser.parse_args()
    if args.output_dir:
        proposed=args.output_dir.resolve()
        if proposed.parent!=ROOT or proposed.name not in ('live-test','radio-test'):parser.error('Unsupported trial output directory')
        OUT=proposed
    mapping,labels=prepare()
    if args.self_test:selftest(mapping)
    else:
        if not args.pid or not args.trace:parser.error('--pid and --trace required')
        run(args.pid,args.trace,mapping,args.radio_trial,args.trigger_trial,args.attack_event_trial or args.source_position_trial or args.fire_commit_trial,args.source_position_trial,args.fire_commit_trial,args.parent_pid)
