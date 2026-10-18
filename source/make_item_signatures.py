"""Generate local-only event signatures from eight matching encoded audio buffers.
Inputs are build-specific, legally supplied by the developer; never commit them.
"""
from pathlib import Path
import argparse
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('buffers',nargs=8,type=Path,help='Encoded buffers in adapter event-ID order 0..7')
a=p.parse_args()
lines=['#include <stddef.h>','#include <string.h>','static int item_encoded_id(const unsigned char *p,size_t n){','if(!p||n<64)return -1;']
for i,path in enumerate(a.buffers):
 data=path.read_bytes()[:64]
 if len(data)!=64: raise ValueError('Buffer must contain at least 64 bytes')
 lines += ['static const unsigned char s%d[64]={%s};'%(i,','.join(map(str,data))),'if(!memcmp(p,s%d,64))return %d;'%(i,i)]
lines+=['return -1;','}']
(Path(__file__).parent/'bloodborne/item_encoded.h').write_text('\n'.join(lines),encoding='utf-8')
