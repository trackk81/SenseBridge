from pathlib import Path
import json,re
from collections import defaultdict
from index_sound_assets import fnv,BANKS,ROOT
root=ROOT
assets=json.loads((root/'sound-asset-index.json').read_text(encoding='utf-8'))
pattern=re.compile(r'(iron|wood|meat)-(cut|knock|stab)-(iron|wood|stone|meat)\d*')
groups={'iron':1,'wood':2,'stone':3,'meat':4}
def label(name):
 m=pattern.fullmatch(name)
 if m:return groups[m[3]]
 if re.fullmatch(r'foot-stone-(l|r[1-3]|w[1-3])|foot_stone_(run|walk)_dry[0-2]',name):return 5
 if re.fullmatch(r'foot-wood-(l|r[1-3]|w[1-3])',name):return 6
 return 0
by_sig=defaultdict(list)
for a in assets:
 if len(a['signature64'])==128:by_sig[a['signature64']].append(a)
rows=[];excluded=[]
for sig,aliases in by_sig.items():
 matched=[(a,label(a['name'])) for a in aliases]
 labels={m for a,m in matched if m}
 if not labels:continue
 # Exact data shared with a differently named asset is ambiguous, not proof.
 if len(labels)!=1 or any(not m for a,m in matched):
  excluded.append([a['name'] for a in aliases]);continue
 rows.append(dict(signature=sig,hash=fnv(bytes.fromhex(sig)),material=labels.pop(),
                  aliases=[dict(bank=a['bank'],index=a['index'],name=a['name']) for a in aliases]))
rows.sort(key=lambda x:x['hash'])
lines=['/* Local-only generated sound identity table. Do not distribute game bytes. */',
 '#ifndef SB_MATERIAL_ENCODED_H','#define SB_MATERIAL_ENCODED_H',
 '#include <stdint.h>','#include <stddef.h>','#include <string.h>',
 'typedef struct { uint32_t hash; int material; unsigned char signature[64]; } SbMaterialSignature;',
 'static const SbMaterialSignature sb_material_signatures[]={']
for r in rows:lines.append('{%du,%d,{%s}},'%(r['hash'],r['material'],','.join(map(str,bytes.fromhex(r['signature'])))))
lines+=['};','static int sb_sound_encoded(const unsigned char *p,size_t n){',
 'if(!p||n<64)return 0;uint32_t h=2166136261u;for(unsigned i=0;i<64;i++){h^=p[i];h*=16777619u;}',
 'size_t lo=0,hi=sizeof(sb_material_signatures)/sizeof(sb_material_signatures[0]);',
 'while(lo<hi){size_t mid=lo+(hi-lo)/2;if(sb_material_signatures[mid].hash<h)lo=mid+1;else hi=mid;}',
 'for(;lo<sizeof(sb_material_signatures)/sizeof(sb_material_signatures[0])&&sb_material_signatures[lo].hash==h;lo++)',
 'if(!memcmp(p,sb_material_signatures[lo].signature,64))return (int)lo+1;',
 'return 0;}',
 'static inline int sb_material_encoded(const unsigned char *p,size_t n){int id=sb_sound_encoded(p,n);return id?sb_material_signatures[id-1].material:0;}', '#endif']
(Path(__file__).resolve().parent/'bloodborne/material_encoded.h').write_text('\n'.join(lines),encoding='utf-8')
(root/'material-sound-catalog.json').write_text(json.dumps(dict(entries=rows,excluded=excluded),indent=2),encoding='utf-8')
# Test fixtures: every indexed start, including footsteps, music, menus.
with (root/'material-classifier-fixtures.bin').open('wb') as f:
 lookup={r['signature']:r['material'] for r in rows}
 for sig in by_sig:f.write(bytes([lookup.get(sig,0)])+bytes.fromhex(sig))
print(json.dumps(dict(unique_signatures=len(rows),excluded=excluded,fixture_count=len(by_sig))))
