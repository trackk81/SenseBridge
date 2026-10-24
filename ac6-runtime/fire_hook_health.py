"""Read-only health checks. Never repairs or rewrites game code."""
import ctypes as C
from ctypes import wintypes as W

ADDRESS=0x1409072b0
ORIGINAL=bytes.fromhex('40534883ec20488bd9e8c239000084c0')

def classify(data,baseline=None):
    if data==ORIGINAL:return 'original-code'
    if baseline is not None and data!=baseline:return 'changed-code'
    if len(data)!=16 or data[0]!=0xe9:return 'unexpected-code'
    return 'connected'

class FireHookHealth:
    def __init__(self,pid):
        self.k=C.WinDLL('kernel32',use_last_error=True)
        self.k.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD];self.k.OpenProcess.restype=W.HANDLE
        self.k.ReadProcessMemory.argtypes=[W.HANDLE,C.c_void_p,C.c_void_p,C.c_size_t,C.POINTER(C.c_size_t)]
        self.k.CloseHandle.argtypes=[W.HANDLE]
        self.handle=self.k.OpenProcess(0x10,False,pid)
        if not self.handle:raise C.WinError(C.get_last_error())
        self.baseline=None
    def read(self):
        b=C.create_string_buffer(16);n=C.c_size_t()
        if not self.k.ReadProcessMemory(self.handle,ADDRESS,b,16,C.byref(n)) or n.value!=16:
            return {'state':'unreadable'}
        data=b.raw;state=classify(data,self.baseline)
        if state=='connected' and self.baseline is None:self.baseline=data
        return {'state':state,'bytes':data.hex()}
    def close(self):
        if self.handle:self.k.CloseHandle(self.handle);self.handle=None

if __name__=='__main__':
    patch=b'\xe9'+b'\x01'*15
    assert classify(ORIGINAL)=='original-code'
    assert classify(patch)=='connected'
    assert classify(patch,patch)=='connected'
    assert classify(b'\xe9'+b'\x02'*15,patch)=='changed-code'
    assert classify(b'')=='unexpected-code'
    print('PASS original/changed/unexpected entry detection; read-only monitoring')
