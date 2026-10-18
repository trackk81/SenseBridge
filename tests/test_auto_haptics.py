import array,math,json
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'source'))
from build_auto_haptics import synthesize
assert not any(synthesize([0]*24000,48000,1))
def tone(freq,amp=12000):return [round(amp*math.sin(2*math.pi*freq*n/48000)*math.exp(-n/9000)) for n in range(24000)]
a=synthesize(tone(90),48000,1);b=synthesize(tone(3000),48000,1)
assert a!=b and max(map(abs,a))>0 and max(map(abs,b))>0
quiet=synthesize(tone(90,1500),48000,1)
assert sum(x*x for x in quiet)<sum(x*x for x in a)
foot=synthesize(tone(90),48000,1,True)
assert max(map(abs,foot))<max(map(abs,a))
stereo=[v for x in tone(3000) for v in (x,-x)]
assert max(map(abs,synthesize(stereo,48000,2)))>0
dc=synthesize([20000]*48000,48000,1)
assert max(map(abs,dc[-4800:]))<=1
for data in (a,b,quiet,foot,dc):
 assert data[0]==data[-1]==0 and max(map(abs,data))<=4588
assert len(synthesize([0]*24000,24000,1))==48000
assert len(synthesize([0]*144000,48000,1))==96000
for args in (([0],0,1),([0],48000,0),([0],48000,2)):
 try:synthesize(*args);raise AssertionError('invalid input accepted')
 except ValueError:pass
print('PASS: silence, source-dependent waveforms, loudness, quieter footsteps, opposite-phase stereo, DC rejection, fades, rate conversion, duration and invalid-input bounds')
