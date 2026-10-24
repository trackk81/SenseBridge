"""Index local FSB5 ATRAC9 sample starts; never decode mixed game audio.
Layout: vgmstream src/meta/fsb5.c. Metadata/signatures stay local.
"""
from pathlib import Path
import struct, json, hashlib, csv, os
from collections import defaultdict, Counter
ROOT=Path(os.environ.get('SB_SOUND_WORK',str(Path(__file__).resolve().parents[1]/'local-sound-work')))
ROOT.mkdir(parents=True,exist_ok=True)
BANKS=Path(os.environ.get('SB_SOUND_BANKS','local-sound-banks'))

def fnv(data):
    h=2166136261
    for x in data:h=((h^x)*16777619)&0xffffffff
    return h

def index_bank(path):
    with path.open('rb') as f:
        header=f.read(60)
        if header[:4]!=b'FSB5':raise ValueError('not FSB5')
        version,n,hs,ns,ds,codec=struct.unpack_from('<6I',header,4)
        if version!=1 or codec!=13:raise ValueError(f'unsupported {version=} {codec=}')
        if 60+hs+ns+ds>path.stat().st_size:raise ValueError('bank truncated')
        headers=f.read(hs);names=f.read(ns);data=60+hs+ns
        pos=0;entries=[]
        for i in range(n):
            mode,=struct.unpack_from('<Q',headers,pos);pos+=8
            more=mode&1;chunks={}
            while more:
                chunk,=struct.unpack_from('<I',headers,pos);pos+=4
                more=chunk&1;length=(chunk>>1)&0xffffff;kind=chunk>>25
                if pos+length>hs:raise ValueError('chunk outside headers')
                chunks[kind]=headers[pos:pos+length].hex();pos+=length
            offset=((mode>>7)&0x7ffffff)<<5
            if offset>=ds:raise ValueError('sample outside data')
            name=f'sample_{i+1}'
            if ns:
                no,=struct.unpack_from('<I',names,i*4)
                if no>=ns:raise ValueError('name offset')
                name=names[no:names.index(0,no)].decode('utf-8',errors='replace')
            entries.append(dict(bank=path.name,index=i+1,name=name,offset=data+offset,
                                samples=mode>>34,chunks=chunks))
        if pos!=hs:raise ValueError('header size mismatch')
        for i,e in enumerate(entries):
            end=entries[i+1]['offset'] if i+1<n else data+ds
            e['encoded_bytes']=end-e['offset']
            if e['encoded_bytes']<0:raise ValueError('nonmonotonic offsets')
            f.seek(e['offset']);prefix=f.read(min(256,e['encoded_bytes']))
            e['prefix_bytes']=len(prefix);e['fnv256']=fnv(prefix)
            e['signature64']=prefix[:64].hex()
            e['prefix_sha256']=hashlib.sha256(prefix).hexdigest()
            e['decode_starts']=[]
            extra=bytes.fromhex(e['chunks'].get('9',''))
            # FSB AT9 extradata stores superframe bytes before FE config.
            if len(extra)==8 and extra[4]==0xfe:
                step=struct.unpack_from('<I',extra)[0]
                if 0<step<65536:
                    for skip in range(0,min(e['encoded_bytes'],step*8),step):
                        f.seek(e['offset']+skip)
                        part=f.read(min(256,e['encoded_bytes']-skip))
                        e['decode_starts'].append(dict(skip_bytes=skip,hash=fnv(part),bytes=len(part),signature64=part[:64].hex()))
        return entries

if __name__=='__main__':
    entries=[];errors=[]
    for p in sorted(BANKS.glob('*.fsb')):
        try:entries.extend(index_bank(p))
        except Exception as e:errors.append(dict(bank=p.name,error=str(e)))
    (ROOT/'sound-asset-index.json').write_text(json.dumps(entries,ensure_ascii=False),encoding='utf-8')
    by_hash=defaultdict(list)
    for e in entries:
        for start in e['decode_starts'] or [dict(hash=e['fnv256'],bytes=e['prefix_bytes'],skip_bytes=0)]:
            by_hash[(start['hash'],start['bytes'])].append(dict(e,skip_bytes=start['skip_bytes']))
    matches=[]
    for trace in sorted((ROOT.parent/'Bloodborne/runtime').glob('*.csv')):
        with trace.open(encoding='utf-8-sig') as f:
            rows=Counter((int(r['b']),int(r['c'])) for r in csv.DictReader(f) if r.get('kind')=='sound')
        for key,count in rows.items():
            candidates=by_hash.get(key,[])
            matches.append(dict(trace=trace.name,hash=key[0],prefix_bytes=key[1],count=count,
                                candidates=[dict(bank=e['bank'],index=e['index'],name=e['name'],skip_bytes=e['skip_bytes']) for e in candidates]))
    (ROOT/'sound-trace-matches.json').write_text(json.dumps(matches,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(dict(banks=504,samples=len(entries),errors=errors,trace_signatures=len(matches),matched=sum(bool(m['candidates']) for m in matches)),ensure_ascii=True))
