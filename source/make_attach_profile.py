from pathlib import Path
import struct,re,json,hashlib,argparse
parser=argparse.ArgumentParser()
parser.add_argument("game_root",type=Path)
parser.add_argument("import_names",type=Path)
args=parser.parse_args()
root=args.game_root
out=Path(__file__).parent
f=(root/'out/boot-linked.bin').open('rb')
def q(n=1):
    v=struct.unpack('<'+'Q'*n,f.read(8*n));return v[0] if n==1 else v
assert f.read(8)==b'BBPROBE5'
size,entry,ns,nr,ni=q(5);q();q();q(4);nm=q();q(nm*7);nb=q();q(nb*3)
segs=[q(3) for _ in range(ns)]
names=[f.read(128).split(b'\0')[0].decode() for _ in range(ni)]
rels=[q(4) for _ in range(nr)]
image=f.read();assert len(image)==size
mapping=dict(re.findall(r'\{"([^"]+)",\s*"([^"]+)"\}',args.import_names.read_text(encoding='utf-8')))
hooks=['scePadReadState','scePadRead','scePadSetVibration','sceAudioOutOpen','sceAudioOutClose','sceAudioOutOutput','sceAudioOutOutputs','sceAjmBatchStartBuffer']
slots=[]
for i,name in enumerate(hooks):
    found=[t for t,k,v,a in rels if k==1 and mapping.get(names[v])==name and a==0]
    if name in ('scePadReadState','scePadSetVibration','sceAudioOutOutput','sceAjmBatchStartBuffer'): assert found,name
    assert all(t%8==0 and t+8<=size for t in found)
    slots += [(t,i) for t in found]
anchors=[]
for offset in (entry+0x100,entry+0x1000,entry+0x2000):
    assert not any(offset<=t<offset+64 or t<offset<t+8 for t,k,v,a in rels)
    anchors.append((offset,image[offset:offset+64]))
header='#define IMAGE_SIZE 0x%xULL\n'%size
header+='static const struct { uintptr_t offset; unsigned char bytes[64]; } anchors[]={\n'
header+=''.join('{0x%x,{%s}},\n'%(a,','.join(map(str,b))) for a,b in anchors)+'};\n'
header+='static const struct { uintptr_t offset; int hook; } slots[]={\n'
header+=''.join('{0x%x,%d},\n'%x for x in slots)+'};\n'
(out/'attach_profile.h').write_text(header)
profile={'executable':str(root/'bin/bb-probe.exe'),'sha256':hashlib.sha256((root/'bin/bb-probe.exe').read_bytes()).hexdigest(),'boot_sha256':hashlib.sha256((root/'out/boot-linked.bin').read_bytes()).hexdigest(),'slots':slots}
(out.parent/'attach-profile.json').write_text(json.dumps(profile,indent=2))
print('Validated profile:',len(slots),'import slots; image',hex(size))
