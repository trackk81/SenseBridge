import json,os,time
from pathlib import Path
PATH=Path(os.environ['APPDATA'])/'SenseBridge'/'ac6-trigger-settings.json'
class TriggerSettings:
    def __init__(self):self.values={};self.next=0
    def get(self,weapon):
        if time.monotonic()>=self.next:
            self.next=time.monotonic()+.25
            try:
                data=json.loads(PATH.read_text())
                if isinstance(data,dict):self.values=data
            except (OSError,ValueError):pass
        value=self.values.get(str(weapon),{})
        if not isinstance(value,dict):value={}
        start=value.get('start',8)
        if type(start) not in (int,float) or not 0<=start<=90:start=8
        return round(start*255/100)
