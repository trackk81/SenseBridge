"""Exact-build, read-only equipped wrapper validation. No game writes."""
import ctypes as C
import struct

def decode(address,data,expected_owner=None):
    if len(data)!=0x40:return None
    owner,manager=struct.unpack_from('<QQ',data)
    weapon=struct.unpack_from('<I',data,0x20)[0]
    slot=data[0x28]
    self_pointer=struct.unpack_from('<Q',data,0x38)[0]
    if not owner or not manager or weapon<=0 or slot not in (0,1,2,3) or self_pointer!=address:return None
    if expected_owner is not None and owner!=expected_owner:return None
    return {'owner':owner,'manager':manager,'weapon_id':weapon,'slot':slot,
            'mask':1 if slot==0 else 2 if slot==1 else 0}

class WeaponSlots:
    def __init__(self,pid,anchor):
        self.k=C.WinDLL('kernel32',use_last_error=True)
        self.k.OpenProcess.argtypes=[C.c_ulong,C.c_bool,C.c_ulong];self.k.OpenProcess.restype=C.c_void_p
        self.k.ReadProcessMemory.argtypes=[C.c_void_p,C.c_void_p,C.c_void_p,C.c_size_t,C.POINTER(C.c_size_t)]
        self.k.CloseHandle.argtypes=[C.c_void_p]
        self.h=self.k.OpenProcess(0x10,False,pid);self.owner=None;self.manager=None
        if not self.h:raise C.WinError(C.get_last_error())
        initial=self.snapshot(anchor)
        if not initial or initial['slot'] not in (0,1):
            self.close();raise RuntimeError('Unverified arm anchor')
        self.owner=initial['owner'];self.manager=initial['manager']
    def snapshot(self,address):
        b=C.create_string_buffer(0x40);n=C.c_size_t()
        if not self.k.ReadProcessMemory(self.h,address,b,len(b),C.byref(n)) or n.value!=len(b):return None
        result=decode(address,b.raw,self.owner)
        if result and self.manager and result['manager']!=self.manager:return None
        return result
    def close(self):
        if self.h:self.k.CloseHandle(self.h);self.h=None

def test():
    b=bytearray(0x40);struct.pack_into('<QQ',b,0,100,200);struct.pack_into('<I',b,32,110300000);struct.pack_into('<Q',b,56,300)
    assert decode(300,b,100)['mask']==1
    b[40]=1;assert decode(300,b,100)['mask']==2
    b[40]=2;assert decode(300,b,100)['mask']==0
    assert decode(300,b,999) is None
    assert decode(301,b,100) is None
    b[40]=9;assert decode(300,b,100) is None
    assert decode(300,b[:20],100) is None
    print('PASS slot routing, back-slot exclusion, owner and self-pointer validation')

if __name__=='__main__':test()
