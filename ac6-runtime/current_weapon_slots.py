"""Exact-build player-root resolver. Read-only; no trace-history anchors.

Observed on executable SHA256 e003771e5548bca91882c1d7dfe2df3de97cee05c2c989d0c0e34b036ea610d6.
Caller must perform the existing executable compatibility guard before use.
"""
import ctypes as C
import struct
import hashlib
from weapon_slots import decode

STORAGE = 5450560920

def resolve(read, storage=STORAGE):
    def pointer(address):
        b=read(address,8)
        return struct.unpack('<Q',b)[0] if len(b)==8 else 0
    root=pointer(storage)
    owner=pointer(root+0xa5a0) if root else 0
    module=pointer(owner+0x320) if owner else 0
    weapons=pointer(module+0x138) if module else 0
    if not weapons:return None
    arms={}
    for hand in (0,1):
        address=weapons+0x10+hand*0x480
        item=decode(address,read(address,0x40),owner)
        if not item or item['slot']!=hand:return None
        arms[address]=item
    if len({v['manager'] for v in arms.values()})!=1:return None
    # Reject torn reads during loading or equipment replacement.
    if pointer(storage)!=root or pointer(root+0xa5a0)!=owner or pointer(owner+0x320)!=module or pointer(module+0x138)!=weapons:return None
    return arms

class CurrentWeaponSlots:
    def __init__(self,pid):
        self.k=C.WinDLL('kernel32',use_last_error=True)
        self.k.OpenProcess.argtypes=[C.c_ulong,C.c_bool,C.c_ulong];self.k.OpenProcess.restype=C.c_void_p
        self.k.ReadProcessMemory.argtypes=[C.c_void_p,C.c_void_p,C.c_void_p,C.c_size_t,C.POINTER(C.c_size_t)]
        self.k.CloseHandle.argtypes=[C.c_void_p]
        self.h=self.k.OpenProcess(0x1010,False,pid)
        if not self.h:raise C.WinError(C.get_last_error())
        self.k.QueryFullProcessImageNameW.argtypes=[C.c_void_p,C.c_ulong,C.c_wchar_p,C.POINTER(C.c_ulong)]
        path=C.create_unicode_buffer(32768);size=C.c_ulong(len(path))
        try:
            if not self.k.QueryFullProcessImageNameW(self.h,0,path,C.byref(size)):raise C.WinError(C.get_last_error())
            with open(path.value,'rb') as exe:
                digest=hashlib.file_digest(exe,'sha256').hexdigest()
            if digest!='e003771e5548bca91882c1d7dfe2df3de97cee05c2c989d0c0e34b036ea610d6':
                raise RuntimeError('Unsupported executable; no memory sampled')
        except BaseException:
            self.close();raise
        self.owner=None;self.manager=None
    def read(self,address,size):
        b=C.create_string_buffer(size);n=C.c_size_t()
        if self.k.ReadProcessMemory(self.h,address,b,size,C.byref(n)) and n.value==size:return b.raw
        return b''
    def arms(self):
        result=resolve(self.read)
        self.owner=next(iter(result.values()))['owner'] if result else None
        self.manager=next(iter(result.values()))['manager'] if result else None
        return result or {}
    def snapshot(self,address):return self.arms().get(address)
    def close(self):
        if self.h:self.k.CloseHandle(self.h);self.h=None

def test():
    memory={}
    def putp(a,v):memory[a]=struct.pack('<Q',v)
    putp(STORAGE,100);putp(100+0xa5a0,200);putp(200+0x320,300);putp(300+0x138,10000)
    def arm(base,hand,owner=200):
        b=bytearray(0x40);struct.pack_into('<QQ',b,0,owner,500)
        struct.pack_into('<I',b,32,110110000+hand);b[40]=hand;struct.pack_into('<Q',b,56,base)
        memory[base]=bytes(b)
    arm(10016,0);arm(11168,1)
    read=lambda a,n:memory.get(a,b'')[:n]
    assert len(resolve(read))==2
    putp(300+0x138,20000);arm(20016,0);arm(21168,1)
    assert 10016 not in resolve(read) and 20016 in resolve(read)
    arm(21168,1,999);assert resolve(read) is None
    arm(21168,1);putp(100+0xa5a0,0);assert resolve(read) is None
    print('PASS current-root resolution, replacement, wrong owner and unloaded player')

if __name__=='__main__':
    import sys,json
    if len(sys.argv)==1:test()
    else:
        r=CurrentWeaponSlots(int(sys.argv[1]))
        try:print(json.dumps(r.arms(),indent=2))
        finally:r.close()
