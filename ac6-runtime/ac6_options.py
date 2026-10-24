from pathlib import Path
import os,time
class Options:
    def __init__(self):
        self.path=Path(os.environ.get('SENSEBRIDGE_AC6_OPTIONS',str(Path(os.environ['APPDATA'])/'SenseBridge/ac6-options.ini')))
        self.defaults={'enabled':0,'haptics':0,'triggers':0,'speaker':0,'gain':1.5,'speaker_volume':20}
        self.values=dict(self.defaults)
        self.checked=0
    def read(self):
        now=time.monotonic()
        if now-self.checked<.25:return self.values
        self.checked=now
        try:
            lines=self.path.read_text(encoding='utf-8-sig').splitlines()
            d=dict(line.split('=',1) for line in lines if '=' in line)
            result={k:float(d.get(k,v)) for k,v in self.defaults.items()}
            for k in ('enabled','haptics','triggers','speaker'):
                if result[k] not in (0,1):raise ValueError('invalid flag')
            if not 0<=result['gain']<=4 or not 0<=result['speaker_volume']<=35:raise ValueError('invalid level')
            self.values=result
        except (OSError,ValueError):self.values=dict(self.defaults)
        return self.values
