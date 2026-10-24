"""Local-only NPC speech trial; original game audio is never packaged here."""
from pathlib import Path
import ctypes as C, ctypes.wintypes as W, json, os, array
from portable_radio import pcm_mono

class Spec(C.Structure):
    _fields_=[('format',C.c_uint),('channels',C.c_int),('freq',C.c_int)]

class RadioOutput:
    def __init__(self,engine_folder,game_pid,root,volume=.20):
        self.game_pid=game_pid;self.volume=min(.35,max(0,volume));self.language=None
        self.current=None;self.current_object=None;self.data=None;self.position=0;self.index={};self.ambiguous=set()
        self.events={};self.ambiguous_events=set()
        candidates=json.loads((root/'radio-candidates.json').read_text())
        self.stop_targets=candidates['stop_targets']
        for rule in candidates['rules']:
            key=(rule['language'],rule['target'])
            if key in self.index and self.index[key]['source_sha256']!=rule['source_sha256']:
                self.ambiguous.add(key)
            self.index[key]=rule
            event_key=(rule['language'],rule['event_id'])
            if event_key in self.events and self.events[event_key]['source_sha256']!=rule['source_sha256']:
                self.ambiguous_events.add(event_key)
            self.events[event_key]=rule
        self.directory=os.add_dll_directory(str(engine_folder))
        self.sdl=C.CDLL(str(engine_folder/'SDL3.dll'))
        signatures={
            'SDL_InitSubSystem':([C.c_uint],C.c_bool),
            'SDL_GetAudioPlaybackDevices':([C.POINTER(C.c_int)],C.POINTER(C.c_uint)),
            'SDL_GetAudioDeviceName':([C.c_uint],C.c_char_p),
            'SDL_GetAudioDeviceFormat':([C.c_uint,C.POINTER(Spec),C.POINTER(C.c_int)],C.c_bool),
            'SDL_OpenAudioDeviceStream':([C.c_uint,C.POINTER(Spec),C.c_void_p,C.c_void_p],C.c_void_p),
            'SDL_ResumeAudioStreamDevice':([C.c_void_p],C.c_bool),
            'SDL_ClearAudioStream':([C.c_void_p],C.c_bool),
            'SDL_GetAudioStreamQueued':([C.c_void_p],C.c_int),
            'SDL_PutAudioStreamData':([C.c_void_p,C.c_void_p,C.c_int],C.c_bool),
            'SDL_DestroyAudioStream':([C.c_void_p],None),
            'SDL_free':([C.c_void_p],None),'SDL_QuitSubSystem':([C.c_uint],None)}
        for name,(args,result) in signatures.items():
            fn=getattr(self.sdl,name);fn.argtypes=args;fn.restype=result
        self.stream=None
        try:
            if not self.sdl.SDL_InitSubSystem(0x10):raise RuntimeError('Radio audio initialization failed')
            count=C.c_int();ids=self.sdl.SDL_GetAudioPlaybackDevices(C.byref(count));matches=[]
            try:
                for i in range(count.value):
                    spec=Spec();name=(self.sdl.SDL_GetAudioDeviceName(ids[i]) or b'').decode(errors='replace')
                    if 'DualSense' in name and self.sdl.SDL_GetAudioDeviceFormat(ids[i],C.byref(spec),None) and spec.channels==4:matches.append(ids[i])
            finally:self.sdl.SDL_free(ids)
            if len(matches)!=1:raise RuntimeError('Radio requires exactly one four-channel DualSense device')
            spec=Spec(0x8120,4,48000)
            self.stream=self.sdl.SDL_OpenAudioDeviceStream(matches[0],C.byref(spec),None,None)
            if not self.stream or not self.sdl.SDL_ResumeAudioStreamDevice(self.stream):raise RuntimeError('Radio output stream failed')
        except:
            self.close();raise
        self.user=C.WinDLL('user32');self.user.GetForegroundWindow.restype=W.HWND
        self.user.GetWindowThreadProcessId.argtypes=[W.HWND,C.POINTER(W.DWORD)]

    def stop(self):
        self.current=None;self.current_object=None;self.data=None;self.position=0
        if self.stream:self.sdl.SDL_ClearAudioStream(self.stream)

    def foreground(self):
        pid=W.DWORD();self.user.GetWindowThreadProcessId(self.user.GetForegroundWindow(),C.byref(pid))
        return pid.value==self.game_pid

    def record(self,row,now):
        if len(row)!=7:return None
        if row[1]=='11':
            aliases={'ja':'ja','japanese':'ja','enus':'enus','english(us)':'enus','english':'enus','en-us':'enus'}
            language=aliases.get(row[6].strip().lower())
            if language!=self.language:self.stop();self.language=language
            return None
        if row[1] in ('8','9'):
            if self.current and self.current[0]==int(row[3]):self.stop()
            return None
        if row[1] not in ('1','10') or not self.foreground() or not 0<=now-int(row[0])<=150:return None
        if row[1]=='1':
            if int(row[5])<=0:return None
            if self.current and self.current_object==row[2] and self.current[1] in self.stop_targets.get(row[3],[]):self.stop()
            event_key=(self.language,int(row[3]));rule=self.events.get(event_key)
            if not rule or event_key in self.ambiguous_events:return None
            sequence,node=int(row[5]),rule['target']
        else:
            sequence,node=int(row[3]),int(row[4])
            key=(self.language,node);rule=self.index.get(key)
            if key in self.ambiguous:return None
        current=(sequence,node)
        if not node:
            if self.current and self.current[0]==sequence:self.stop()
            return None
        if current==self.current:return None
        if not rule:return None
        # Confirmed playing node + language, still a trial of NPC speech, not a
        # claim that every line has been semantically verified as radio chatter.
        mono=pcm_mono(rule['local_wave'])
        self.stop();self.current=current;self.current_object=row[2]
        self.data=array.array('f',(min(.35,max(-.35,x/32768*self.volume)) for x in mono))
        self.position=min(len(self.data),max(0,now-int(row[0]))*48)
        return {'radio_trial':True,'sequence':sequence,'node':node,'language':self.language,'duration':len(self.data)/48000}

    def pump(self):
        if not self.foreground():self.stop();return
        if self.data is None:return
        queued=self.sdl.SDL_GetAudioStreamQueued(self.stream)
        if queued<0:raise RuntimeError('Radio audio stream disconnected')
        if queued>=480*16:return
        chunk=self.data[self.position:self.position+480]
        if not len(chunk):self.data=None;return
        output=(C.c_float*(len(chunk)*4))()
        for i,value in enumerate(chunk):output[i*4]=output[i*4+1]=value # Haptic channels always zero.
        if not self.sdl.SDL_PutAudioStreamData(self.stream,output,C.sizeof(output)):raise RuntimeError('Radio output failed')
        self.position+=len(chunk)

    def close(self):
        if self.stream:self.sdl.SDL_ClearAudioStream(self.stream);self.sdl.SDL_DestroyAudioStream(self.stream);self.stream=None
        self.sdl.SDL_QuitSubSystem(0x10);self.directory.close()
