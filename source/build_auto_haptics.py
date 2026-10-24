"""Offline per-sound envelope-to-tactile synthesis. No semantic ML claims.
Only exact allowed effect identities enter this cache. No mixed game audio.
"""
from pathlib import Path
import array,hashlib,json,math,struct,subprocess,sys,wave,argparse
ROOT=Path(__file__).resolve().parent
RATE=48000

def synthesize(pcm,rate,channels,footstep=False,max_seconds=2):
    if rate<=0 or channels<1 or channels>8 or len(pcm)%channels:raise ValueError('Invalid PCM format')
    frames=len(pcm)//channels
    count=min(int(frames*RATE/rate),RATE*max_seconds)
    out=array.array('h');low=hi_low=fast=slow=lowenv=highenv=0.
    phase=0.;scale=.35 if footstep else 1.;dc=[0.]*channels
    for n in range(count):
        pos=n*rate/RATE;i=min(int(pos),frames-1);j=min(i+1,frames-1);f=pos-i
        # Preserve magnitude of opposite-phase stereo rather than cancel it.
        filtered=[]
        for c in range(channels):
            raw=(pcm[i*channels+c]*(1-f)+pcm[j*channels+c]*f)/32768.
            dc[c]+=.0026*(raw-dc[c]);filtered.append(raw-dc[c])
        x=max(map(abs,filtered));signed=filtered[0]
        low+=.025*(signed-low);hi_low+=.14*(signed-hi_low)
        high=abs(signed-hi_low)
        fast+=(x-fast)*(.06 if x>fast else .004)
        slow+=(x-slow)*.001
        lowenv+=(abs(low)-lowenv)*.008;highenv+=(high-highenv)*.008
        brightness=highenv/(lowenv+highenv+.002)
        onset=max(0,fast-slow)
        body=math.sqrt(max(0,lowenv))*math.sin(2*math.pi*82*n/RATE)
        phase+=2*math.pi*(155+65*brightness)/RATE
        detail=math.sqrt(max(0,fast))*brightness*math.sin(phase)
        strike=math.sqrt(onset)*math.sin(2*math.pi*115*n/RATE)
        signal=scale*(.12*body+.10*detail+.12*strike)
        fade=min(1,n/96,(count-1-n)/480) if count else 0
        value=.14*math.tanh(signal/.14)*max(0,fade)
        out.append(round(value*32767))
    return out

def main():
    parser=argparse.ArgumentParser(description='Build local audio-derived feedback cache from an exact sound catalog')
    parser.add_argument('--catalog',type=Path,required=True)
    parser.add_argument('--banks',type=Path,required=True)
    parser.add_argument('--effects',type=Path,required=True)
    parser.add_argument('--vgmstream',type=Path,required=True)
    parser.add_argument('--runtime',type=Path,required=True)
    parser.add_argument('--headers',type=Path,default=ROOT/'bloodborne')
    args=parser.parse_args()
    entries=json.loads(args.catalog.read_text(encoding='utf-8'))['entries']
    extra_names=['drink.wav','blood-collected.wav']+[f'visceral-{i}.wav' for i in range(5)]
    effects=args.effects
    material_count=len(entries)
    entries += [dict(signature=hashlib.sha256((effects/name).read_bytes()).hexdigest(),material=7 if i<2 else 8,local=name) for i,name in enumerate(extra_names)]
    digest=hashlib.sha256(''.join(e['signature']+':'+str(e['material']) for e in entries).encode()).digest()[:16]
    if material_count!=50:raise ValueError('Update item ID mapping for changed material catalog')
    decoded=args.runtime/'decoded-feedback';decoded.mkdir(parents=True,exist_ok=True)
    runtime=args.runtime;report=[]
    bank=bytearray(b'SBAUTO01'+struct.pack('<I',len(entries))+digest)
    for i,e in enumerate(entries):
        if 'local' in e:
            path=effects/e['local'];a={'name':e['local']}
        else:
            a=next((a for a in e['aliases'] if a['bank']=='sprj_main_eng.fsb'),e['aliases'][0])
            path=decoded/f'{i+1:03d}.wav'
            subprocess.run([str(args.vgmstream),'-i','-s',str(a['index']),'-o',str(path),str(args.banks/a['bank'])],check=True,stdout=subprocess.DEVNULL)
        with wave.open(str(path),'rb') as w:
            if w.getsampwidth()!=2:raise ValueError('PCM16 required')
            rate,channels=w.getframerate(),w.getnchannels();raw=w.readframes(w.getnframes())
        pcm=array.array('h');pcm.frombytes(raw)
        if sys.byteorder!='little':pcm.byteswap()
        output=synthesize(pcm,rate,channels,e['material'] in (5,6),10 if e['material']>=7 else 2)
        if len(output)<1:raise ValueError('Empty decoded clip')
        bank+=struct.pack('<II',len(output),e['material'])+struct.pack('<%dh'%len(output),*output)
        report.append(dict(id=i+1,name=a['name'],material=e['material'],frames=len(output),peak=max(map(abs,output))/32767,sha256=hashlib.sha256(output.tobytes()).hexdigest()))
    (runtime/'auto-haptics.bin').write_bytes(bank)
    header='#ifndef SB_AUTO_CATALOG_H\n#define SB_AUTO_CATALOG_H\n#define SB_AUTO_COUNT %d\n#define SB_AUTO_ITEM_FIRST 51\n#define SB_AUTO_VISCERAL_FIRST 53\nstatic const unsigned char sb_auto_digest[16]={%s};\n#endif\n'%(len(entries),','.join(map(str,digest)))
    (args.headers/'auto_haptic_catalog.h').write_text(header)
    (runtime/'auto-haptics-report.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(dict(clips=len(report),bytes=len(bank),unique_waveforms=len({r['sha256'] for r in report}),peak=max(r['peak'] for r in report))))

if __name__=='__main__':main()
