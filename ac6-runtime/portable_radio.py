"""Prepare radio from the user's installed AC6 archive; never download game audio.

Only the supplied allowlisted media hashes are decoded. Original game files are
opened read-only. The cache belongs to this PC and is never part of a data pack.
"""
from pathlib import Path
import argparse, array, base64, concurrent.futures, ctypes as C, hashlib, json
import os, struct, subprocess, tempfile, wave

def rsa_numbers(pem):
    raw=base64.b64decode(''.join(x for x in pem.splitlines() if not x.startswith('---')))
    def item(at,tag):
        if raw[at]!=tag:raise ValueError('Invalid public key')
        n=raw[at+1];at+=2
        if n&128:
            k=n&127;n=int.from_bytes(raw[at:at+k],'big');at+=k
        if at+n>len(raw):raise ValueError('Truncated public key')
        return raw[at:at+n],at+n,at
    _,end,at=item(0,0x30);n,at,_=item(at,2);e,at,_=item(at,2)
    if at!=end:raise ValueError('Public key trailing data')
    return int.from_bytes(n,'big'),int.from_bytes(e,'big')

def aes_ecb(data,key):
    if len(key)!=16 or len(data)%16:raise ValueError('AES bounds')
    b=C.WinDLL('bcrypt');ptr=C.c_void_p;u=C.c_ulong
    signatures={'BCryptOpenAlgorithmProvider':([C.POINTER(ptr),C.c_wchar_p,C.c_wchar_p,u],C.c_long),
      'BCryptSetProperty':([ptr,C.c_wchar_p,ptr,u,u],C.c_long),
      'BCryptGenerateSymmetricKey':([ptr,C.POINTER(ptr),ptr,u,ptr,u,u],C.c_long),
      'BCryptDecrypt':([ptr,ptr,u,ptr,ptr,u,ptr,u,C.POINTER(u),u],C.c_long),
      'BCryptDestroyKey':([ptr],C.c_long),'BCryptCloseAlgorithmProvider':([ptr,u],C.c_long)}
    for name,(args,ret) in signatures.items():getattr(b,name).argtypes=args;getattr(b,name).restype=ret
    def check(status):
        if status<0:raise OSError('CNG AES error '+hex(status&0xffffffff))
    alg=ptr();handle=ptr()
    try:
        check(b.BCryptOpenAlgorithmProvider(C.byref(alg),'AES',None,0))
        mode=C.create_unicode_buffer('ChainingModeECB');check(b.BCryptSetProperty(alg,'ChainingMode',mode,C.sizeof(mode),0))
        secret=C.create_string_buffer(key);check(b.BCryptGenerateSymmetricKey(alg,C.byref(handle),None,0,secret,len(key),0))
        src=C.create_string_buffer(data);out=C.create_string_buffer(len(data));written=u()
        check(b.BCryptDecrypt(handle,src,len(data),None,None,0,out,len(data),C.byref(written),0))
        if written.value!=len(data):raise ValueError('AES output size')
        return out.raw
    finally:
        if handle:b.BCryptDestroyKey(handle)
        if alg:b.BCryptCloseAlgorithmProvider(alg,0)

def path_hash(name):
    h=0
    for c in name.lower().replace('\\','/'):h=(h*133+ord(c))&0xffffffffffffffff
    return h

class Archive:
    def __init__(self,game,key):
        self.data=game/'sd/sd.bdt';header_path=game/'sd/sd.bhd'
        if not 0<header_path.stat().st_size<=64*1024*1024:raise ValueError('Archive header size')
        encrypted=header_path.read_bytes()
        n,e=rsa_numbers(key.read_text());block=(n.bit_length()+7)//8
        if not encrypted or len(encrypted)>64*1024*1024 or len(encrypted)%block:raise ValueError('Archive header size')
        self.header=b''.join(pow(int.from_bytes(encrypted[i:i+block],'big'),e,n).to_bytes(block-1,'big') for i in range(0,len(encrypted),block))
        h=self.header
        if h[:5]!=b'BHD5\xff':raise ValueError('Unsupported archive')
        count,offset=struct.unpack_from('<II',h,16)
        if count>100000 or offset+count*8>len(h):raise ValueError('Archive buckets')
        self.entries={}
        for b in range(count):
            size,at=struct.unpack_from('<II',h,offset+b*8)
            if size>1000000 or at+size*40>len(h):raise ValueError('Archive entries')
            for i in range(size):
                item=struct.unpack_from('<Qiiqqq',h,at+i*40)
                if item[0] in self.entries:raise ValueError('Duplicate archive entry')
                self.entries[item[0]]=item
    def read(self,language,media):
        if language not in ('ja','enus') or not isinstance(media,int) or not 0<media<2**32:raise ValueError('Media identity')
        ident=path_hash('/'+language+'/'+str(media)+'.wem')
        item=self.entries.get(ident)
        if item is None:raise ValueError('Media not in installed game')
        _,size,unpadded,start,_,crypt=item
        if not 0<size<=32*1024*1024 or not 0<=unpadded<=size or start<0 or start+size>self.data.stat().st_size:raise ValueError('Media bounds')
        with self.data.open('rb') as f:f.seek(start);raw=bytearray(f.read(size))
        if crypt:
            h=self.header
            if crypt<0 or crypt+20>len(h):raise ValueError('Crypto offset')
            key=h[crypt:crypt+16];count=struct.unpack_from('<I',h,crypt+16)[0]
            if count>10000 or crypt+20+count*16>len(h):raise ValueError('Crypto ranges')
            for i in range(count):
                begin,end=struct.unpack_from('<qq',h,crypt+20+i*16)
                if begin==-1 or end==-1 or begin==end:continue
                if not 0<=begin<=end<=size:raise ValueError('Crypto range')
                raw[begin:end]=aes_ecb(bytes(raw[begin:end]),key)
        return bytes(raw[:unpadded or size])

def pcm_mono(path):
    with wave.open(str(path),'rb') as w:
        rate=w.getframerate();channels=w.getnchannels();frames=w.getnframes()
        if w.getsampwidth()!=2 or channels not in (1,2) or not 8000<=rate<=192000 or not .2<=frames/rate<=60:raise ValueError('Speech format/duration')
        samples=array.array('h',w.readframes(frames))
    if len(samples)!=frames*channels:raise ValueError('Speech truncated')
    if channels==2:samples=array.array('h',((samples[i]+samples[i+1])//2 for i in range(0,len(samples),2)))
    if rate!=48000:
        src=samples;samples=array.array('h');size=round(len(src)*48000/rate)
        for i in range(size):
            at=i*rate/48000;j=min(int(at),len(src)-1);f=at-j
            samples.append(round(src[j]*(1-f)+src[min(j+1,len(src)-1)]*f))
    return samples

def prepare(game,manifest,cache,decoder,key,limit=None):
    if not 0<manifest.stat().st_size<=20*1024*1024:raise ValueError('Radio index size')
    index=json.loads(manifest.read_text(encoding='utf-8-sig'));rules=index['rules']
    if not isinstance(rules,list) or len(rules)>20000:raise ValueError('Radio rule count')
    unique={}
    for r in rules:
        digest=r['source_sha256']
        if len(digest)!=64 or any(c not in '0123456789abcdef' for c in digest):raise ValueError('Media hash')
        unique.setdefault(digest,r)
    if limit:unique=dict(list(unique.items())[:limit])
    archive=Archive(game,key)
    cache.mkdir(parents=True,exist_ok=True)
    def decode(pair):
        digest,r=pair;target=cache/(digest+'.wav')
        stamp=cache/(digest+'.sha256')
        if target.exists() and target.stat().st_size<=5760044 and stamp.exists() and stamp.stat().st_size==64 and hashlib.sha256(target.read_bytes()).hexdigest()==stamp.read_text():return
        raw=archive.read(r['language'],r['media_id'])
        if hashlib.sha256(raw).hexdigest()!=digest:raise ValueError('Game audio version/hash mismatch')
        with tempfile.TemporaryDirectory(prefix='decode-',dir=cache) as td:
            td=Path(td);source=td/'input.wem';output=td/'output.wav';source.write_bytes(raw)
            subprocess.run([str(decoder),'-i','-W','1','-o',str(output),str(source)],stdout=subprocess.DEVNULL,stderr=subprocess.PIPE,check=True,timeout=90,creationflags=0x08000000)
            samples=pcm_mono(output);normalized=td/'mono.wav'
            with wave.open(str(normalized),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(48000);w.writeframes(samples.tobytes())
            digest_pcm=hashlib.sha256(normalized.read_bytes()).hexdigest();os.replace(normalized,target);stamp.write_text(digest_pcm)
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        for i,_ in enumerate(pool.map(decode,unique.items())):
            if i%100==0:print('RADIO_PREPARE',i+1,len(unique),flush=True)
    ready=dict(index);ready['rules']=[dict(r,local_wave=str(cache/(r['source_sha256']+'.wav'))) for r in rules if r['source_sha256'] in unique]
    temp=cache/'radio-candidates.json.tmp';temp.write_text(json.dumps(ready));os.replace(temp,cache/'radio-candidates.json')
    print('RADIO_READY',len(unique),flush=True)

if __name__=='__main__':
    ap=argparse.ArgumentParser();ap.add_argument('--game',type=Path,required=True);ap.add_argument('--manifest',type=Path,required=True);ap.add_argument('--cache',type=Path,required=True);ap.add_argument('--decoder',type=Path,required=True);ap.add_argument('--key',type=Path,required=True);ap.add_argument('--limit',type=int)
    a=ap.parse_args();prepare(a.game,a.manifest,a.cache,a.decoder,a.key,a.limit)
