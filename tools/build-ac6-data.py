"""Generate data-only AC6 JSON; no original speech, executable code or local paths."""
from pathlib import Path
import base64, hashlib, json, struct
repo=Path(__file__).resolve().parents[1];work=repo.parent;out=repo/'release-artifacts';out.mkdir(exist_ok=True)
candidates=json.loads((work/'release-prep/ac6-candidates.json').read_text())['rules'];waves=(work/'release-prep/ac6-candidate-waveforms.bin').read_bytes()
by_label={r['label']:r for r in candidates};labels=sorted(by_label);bank=bytearray(b'SBEVENT1'+struct.pack('<I',len(labels)))
for label in labels:
 r=by_label[label];pcm=waves[r['offset']:r['offset']+r['frames']*2];assert len(pcm)==r['frames']*2
 bank.extend(struct.pack('<III',r['frames'],0,0));bank.extend(pcm)
mapping={str(r['event_id']):labels.index(r['label']) for r in json.loads((work/'release-prep/ac6-event-rules.json').read_text())['rules']}
textures=json.loads((work/'research/ai-textures/manifest.json').read_text());extra=(work/'research/ai-textures/events.bin').read_bytes();count=struct.unpack_from('<I',extra,8)[0];assert count==len(textures['entries'])
base=len(labels);bank[8:12]=struct.pack('<I',base+count);bank.extend(extra[12:]);labels.extend(r['label'] for r in textures['entries']);mapping.update({k:base+v for k,v in textures['mapping'].items()})
radio=json.loads((work/'research/radio-candidates.json').read_text());radio={'rules':[{k:r[k] for k in ('event_id','target','language','media_id','source_sha256')} for r in radio['rules']],'stop_targets':radio['stop_targets']}
payload={'events.bin':bytes(bank),'event-map.json':json.dumps(dict(labels=labels,mapping=mapping)).encode(),'radio-index.json':json.dumps(radio).encode()}
pack={'Format':'SenseBridge.GamePreset','Version':3,'Title':'ARMORED CORE VI FIRES OF RUBICON','Adapter':'ac6-audio-v1','SteamAppId':'1888160','Triggers':False,'Haptics':False,'Speaker':False,'Ac6':{'PackId':'ac6-audio-01','ExecutableSha256':'e003771e5548bca91882c1d7dfe2df3de97cee05c2c989d0c0e34b036ea610d6'},'Settings':{'gain':'1.5','speaker_volume':'20','left_force':'4','right_force':'4'},'Assets':[{'Name':n,'Sha256':hashlib.sha256(b).hexdigest(),'Data':base64.b64encode(b).decode()} for n,b in payload.items()]}
pack['Ac6']['ExecutableSha256']='e003771e5548bca91882c1d7dfe2df3de97cee05c2c989d0c0e34b036ea610d6'
path=out/'ArmoredCoreVI-1.0.sensebridge.json';path.write_text(json.dumps(pack,separators=(',',':')),encoding='utf-8')
entry={'Title':pack['Title'],'SteamAppId':pack['SteamAppId'],'Executable':'armoredcore6.exe','PackId':'ac6-audio-01','Adapter':pack['Adapter'],'Version':'1.0','MinimumAppVersion':'0.1.8','Url':'https://github.com/trackk81/SenseBridge/releases/download/game-data/'+path.name,'Sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'Size':path.stat().st_size}
(out/'ac6-catalog-entry.UNPUBLISHED.json').write_text(json.dumps(entry,indent=2))
print('DATA PACK',path.stat().st_size,'bytes;',len(labels),'haptic clips;',len(radio['rules']),'radio mappings')
