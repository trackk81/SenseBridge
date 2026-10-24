/* AC6 offline playback diagnostics. Explicit start only; no auto-injection.
 * Original game functions are called unchanged. No controller output here.
 */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <share.h>
#include "MinHook.h"
typedef uint32_t (__fastcall *PostEventFn)(uint32_t,uint64_t,uint32_t,void*,void*,uint32_t,void*,uint32_t);
typedef int (__fastcall *RegisterFn)(uint64_t,const char*);
typedef int (__fastcall *SwitchFn)(uint32_t,uint32_t,uint64_t);
static PostEventFn real_post;
static RegisterFn real_register;
static SwitchFn real_switch;
typedef int32_t (__fastcall *FireCommitFn)(void*);
static FireCommitFn real_fire_commit;
typedef int (__fastcall *SourcePositionFn)(uint32_t,int32_t*,int);
static SourcePositionFn get_source_position;
typedef struct {uint32_t id,last_position;uint64_t object,started,last_ok;int used;} PositionTrack;
static PositionTrack positions[32];
static SRWLOCK position_lock=SRWLOCK_INIT;
typedef uint32_t (__fastcall *SequenceOpenFn)(uint64_t,uint32_t,void*,void*,int);
typedef int (__fastcall *SequenceControlFn)(uint32_t,int32_t,int);
typedef int (__fastcall *SequenceCloseFn)(uint32_t);
typedef int (__fastcall *PlayingItemFn)(uint32_t,uint32_t*,void**);
static SequenceOpenFn real_sequence_open;
static SequenceControlFn real_sequence_play,real_sequence_stop,real_sequence_pause;
static SequenceCloseFn real_sequence_close;
static PlayingItemFn get_playing_item;
typedef const wchar_t* (__fastcall *LanguageFn)(void);
static LanguageFn get_language;
typedef struct {uint32_t id,last_node;uint64_t object;int used,playing;} Sequence;
static Sequence sequences[128];
static SRWLOCK sequence_lock=SRWLOCK_INIT;
typedef struct { uint64_t tick, object; uint32_t kind,a,b,result; char name[80]; } Entry;
static Entry queue[4096];
static uint64_t head,tail;
static SRWLOCK queue_lock=SRWLOCK_INIT, control_lock=SRWLOCK_INIT;
static volatile LONG active,dropped;
static int hooks_ready;
static void *installed_targets[9];
static unsigned char installed_entries[9][16];
static HANDLE worker;
static FILE *log_file;
static void push(uint32_t kind,uint64_t object,uint32_t a,uint32_t b,uint32_t result,const char *name) {
 if(!InterlockedCompareExchange(&active,0,0))return;
 if(!TryAcquireSRWLockExclusive(&queue_lock)){InterlockedIncrement(&dropped);return;}
 if(head-tail<4096){
  Entry *e=&queue[head++%4096];ZeroMemory(e,sizeof(*e));
  e->tick=GetTickCount64();e->object=object;e->kind=kind;e->a=a;e->b=b;e->result=result;
  if(name)for(int i=0;i<79&&name[i];i++)e->name[i]=(name[i]>=32&&name[i]<=126&&name[i]!=',')?name[i]:'_';
 }else InterlockedIncrement(&dropped);
 ReleaseSRWLockExclusive(&queue_lock);
}
static int32_t __fastcall on_fire_commit(void *weapon){
 // This routine checks firing eligibility, consumes ammo, updates fire delay,
 // then runs the post-shot/recoil path. Preserve all original arguments/result.
 int32_t shots=real_fire_commit(weapon);
 if(shots>0&&shots<=64&&InterlockedCompareExchange(&active,0,0)){
  uint32_t id=0,alternate=0;
  __try {id=*(uint32_t*)((char*)weapon+0x10);alternate=*(uint32_t*)((char*)weapon+0x20);}
  __except(EXCEPTION_EXECUTE_HANDLER){return shots;}
  push(15,(uint64_t)weapon,(uint32_t)shots,id,alternate,"fire-commit-candidate");
 }
 return shots;
}
static uint32_t __fastcall on_post(uint32_t id,uint64_t object,uint32_t flags,void *cb,void *cookie,uint32_t n,void *sources,uint32_t playing) {
 int track=id==818944284&&InterlockedCompareExchange(&active,0,0);
 // AK_EnableGetSourcePlayPosition is an information flag, not a callback.
 // Preserve the game's callback, cookie and all existing flags.
 if(track)flags|=0x100000;
 uint32_t result=real_post(id,object,flags,cb,cookie,n,sources,playing);
 if(result)push(1,object,id,0,result,NULL);
 if(result&&track){
  AcquireSRWLockExclusive(&position_lock);
  for(int i=0;i<32;i++)if(!positions[i].used){
   PositionTrack *p=&positions[i];ZeroMemory(p,sizeof(*p));p->used=1;p->id=result;
   p->object=object;p->started=p->last_ok=GetTickCount64();p->last_position=0xffffffff;break;
  }
  ReleaseSRWLockExclusive(&position_lock);
 }
 if(result&&id==1315642802){
  AcquireSRWLockExclusive(&position_lock);
  for(int i=0;i<32;i++)if(positions[i].used&&positions[i].object==object){
   push(13,object,positions[i].id,0,1,"source-stop");positions[i].used=0;
  }
  ReleaseSRWLockExclusive(&position_lock);
 }
 return result;
}
static void sample_source_positions(void){
 PositionTrack snapshot[32];AcquireSRWLockShared(&position_lock);
 CopyMemory(snapshot,positions,sizeof(snapshot));ReleaseSRWLockShared(&position_lock);
 for(int i=0;i<32;i++)if(snapshot[i].used){
  int32_t ms=-1;int r=get_source_position(snapshot[i].id,&ms,0);
  uint64_t now=GetTickCount64();AcquireSRWLockExclusive(&position_lock);
  PositionTrack *p=&positions[i];
  if(p->used&&p->id==snapshot[i].id){
   if(r==1&&ms>=0){
    p->last_ok=now;
    if((uint32_t)ms!=p->last_position){p->last_position=(uint32_t)ms;push(12,p->object,p->id,(uint32_t)ms,1,"source-position");}
   }
   if(now-p->last_ok>500||now-p->started>30000){push(13,p->object,p->id,0,(uint32_t)r,"source-expired");p->used=0;}
  }
  ReleaseSRWLockExclusive(&position_lock);
 }
}
static int __fastcall on_register(uint64_t object,const char *name) {
 int result=real_register(object,name);if(result==1)push(2,object,0,0,result,name);return result;
}
static int __fastcall on_switch(uint32_t group,uint32_t value,uint64_t object) {
 int result=real_switch(group,value,object);if(result==1)push(3,object,group,value,result,NULL);return result;
}
static uint32_t __fastcall on_sequence_open(uint64_t object,uint32_t flags,void *cb,void *cookie,int type) {
 uint32_t id=real_sequence_open(object,flags,cb,cookie,type);
 if(id){AcquireSRWLockExclusive(&sequence_lock);
  int slot=-1;for(int i=0;i<128;i++)if(!sequences[i].used){slot=i;break;}
  if(slot>=0){Sequence *s=&sequences[slot];ZeroMemory(s,sizeof(*s));s->used=1;s->id=id;s->object=object;}
  ReleaseSRWLockExclusive(&sequence_lock);push(6,object,id,0,1,"sequence-open");
 }return id;
}
static void sequence_state(uint32_t id,int playing,uint32_t kind) {
 uint64_t object=0;AcquireSRWLockExclusive(&sequence_lock);
 for(int i=0;i<128;i++)if(sequences[i].used&&sequences[i].id==id){
  sequences[i].playing=playing;sequences[i].last_node=0;object=sequences[i].object;
  if(kind==9)sequences[i].used=0;break;
 }ReleaseSRWLockExclusive(&sequence_lock);push(kind,object,id,0,1,"sequence-state");
}
static int __fastcall on_sequence_play(uint32_t id,int32_t ms,int curve){
 int r=real_sequence_play(id,ms,curve);if(r==1)sequence_state(id,1,7);return r;
}
static int __fastcall on_sequence_stop(uint32_t id,int32_t ms,int curve){
 int r=real_sequence_stop(id,ms,curve);if(r==1)sequence_state(id,0,8);return r;
}
static int __fastcall on_sequence_pause(uint32_t id,int32_t ms,int curve){
 int r=real_sequence_pause(id,ms,curve);if(r==1)sequence_state(id,0,8);return r;
}
static int __fastcall on_sequence_close(uint32_t id){
 int r=real_sequence_close(id);if(r==1)sequence_state(id,0,9);return r;
}
static void sample_playing_items(void){
 Sequence snapshot[128];AcquireSRWLockShared(&sequence_lock);
 CopyMemory(snapshot,sequences,sizeof(snapshot));ReleaseSRWLockShared(&sequence_lock);
 for(int i=0;i<128;i++)if(snapshot[i].used&&snapshot[i].playing){
  uint32_t node=0;void *custom=NULL;
  int r=get_playing_item(snapshot[i].id,&node,&custom);
  if(r!=1)continue;
  AcquireSRWLockExclusive(&sequence_lock);
  Sequence *s=&sequences[i];
  if(s->used&&s->playing&&s->id==snapshot[i].id&&s->last_node!=node){
   s->last_node=node;push(10,s->object,s->id,node,1,"playing-item");
  }ReleaseSRWLockExclusive(&sequence_lock);
 }
}
static DWORD WINAPI write_trace(void *unused) {
 (void)unused;
 uint64_t bytes=0,next_heartbeat=0;
 while(1){
  if(get_playing_item&&InterlockedCompareExchange(&active,0,0))sample_playing_items();
  if(get_source_position&&InterlockedCompareExchange(&active,0,0))sample_source_positions();
  Entry local[64];unsigned n=0;
  AcquireSRWLockExclusive(&queue_lock);
  while(tail<head&&n<64)local[n++]=queue[tail++%4096];
  ReleaseSRWLockExclusive(&queue_lock);
  LONG lost=InterlockedExchange(&dropped,0);
  if(lost){int wrote=fprintf(log_file,"%llu,4,0,%ld,0,0,dropped\n",GetTickCount64(),lost);if(wrote<0)break;bytes+=wrote;}
  for(unsigned i=0;i<n;i++){
   Entry *e=&local[i];int wrote=fprintf(log_file,"%llu,%u,%llu,%u,%u,%u,%s\n",e->tick,e->kind,e->object,e->a,e->b,e->result,e->name);
   if(wrote<0)goto done;bytes+=wrote;
  }
  uint64_t now=GetTickCount64();
  if(now>=next_heartbeat){int wrote=fprintf(log_file,"%llu,0,0,0,0,1,heartbeat\n",now);if(wrote<0)break;bytes+=wrote;next_heartbeat=now+1000;
   // A live writer alone does not prove that successful audio events arrive.
   // Aggregate diagnostics only: preserve the original PostEvent result.
   if(get_language){const wchar_t *lang=get_language();char name[80]={0};
    if(lang){for(int j=0;j<79&&lang[j];j++)name[j]=(lang[j]>=32&&lang[j]<=126)?(char)lang[j]:'_';push(11,0,0,0,1,name);}
   }
  }
  if(fflush(log_file)||bytes>=64ULL*1024*1024)break;
  if(!InterlockedCompareExchange(&active,0,0)&&!n)break;
  Sleep(10);
 }
done:
 InterlockedExchange(&active,0);fclose(log_file);log_file=NULL;return 0;
}
__declspec(dllexport) DWORD WINAPI Ac6TraceStop(void *unused) {
 (void)unused;
 AcquireSRWLockExclusive(&control_lock);InterlockedExchange(&active,0);
 DWORD result=0;
 if(worker){if(WaitForSingleObject(worker,5000)==WAIT_OBJECT_0){CloseHandle(worker);worker=NULL;}else result=10;}
 // Hooks remain inert until process exit; never unload code a game thread may be using.
 ReleaseSRWLockExclusive(&control_lock);return result;
}
__declspec(dllexport) DWORD WINAPI Ac6TraceStatus(void *unused) {
 (void)unused;return InterlockedCompareExchange(&active,0,0)?1:0;
}
__declspec(dllexport) DWORD WINAPI Ac6TraceStart(const wchar_t *path) {
 DWORD result=0;
 AcquireSRWLockExclusive(&control_lock);
 if(worker){
  if(WaitForSingleObject(worker,0)!=WAIT_OBJECT_0){result=1;goto done;}
  CloseHandle(worker);worker=NULL;
 }
 if(!path||!path[0]||wcslen(path)>1800){result=2;goto done;}
 // External connector also requires exact executable SHA-256 and a complete
 // process/module protection inspection before loading this diagnostic module.
 if(GetModuleHandleW(L"EasyAntiCheat_x64.dll")||GetModuleHandleW(L"EasyAntiCheat_EOS.dll")){result=3;goto done;}
 HMODULE game=GetModuleHandleW(NULL);
 // Exact build is hash-pinned by the connector; independently check prologue.
 void *fire_commit=(char*)game+0x9072b0;
 const unsigned char fire_signature[]={0x40,0x53,0x48,0x83,0xec,0x20,0x48,0x8b,0xd9,0xe8,0xc2,0x39,0x00,0x00,0x84};
 if(!hooks_ready && memcmp(fire_commit,fire_signature,sizeof(fire_signature))){result=11;goto done;}
 if(hooks_ready){
  // Resume only our unchanged installation. Never overwrite a restored or
  // third-party-modified entry to make a failed health check pass.
  for(unsigned i=0;i<9;i++){
   if(!installed_targets[i]||memcmp(installed_targets[i],installed_entries[i],16)){result=11;goto done;}
  }
 }
 get_source_position=(SourcePositionFn)GetProcAddress(game,"?GetSourcePlayPosition@SoundEngine@AK@@YA?AW4AKRESULT@@KPEAJ_N@Z");
 if(!get_source_position){result=4;goto done;}
 void *post=(void*)GetProcAddress(game,"?PostEvent@SoundEngine@AK@@YAKK_KKP6AXW4AkCallbackType@@PEAUAkCallbackInfo@@@ZPEAXKPEAUAkExternalSourceInfo@@K@Z");
 void *reg=(void*)GetProcAddress(game,"?RegisterGameObj@SoundEngine@AK@@YA?AW4AKRESULT@@_KPEBD@Z");
 void *sw=(void*)GetProcAddress(game,"?SetSwitch@SoundEngine@AK@@YA?AW4AKRESULT@@KK_K@Z");
 void *seq_open=(void*)GetProcAddress(game,"?Open@DynamicSequence@SoundEngine@AK@@YAK_KKP6AXW4AkCallbackType@@PEAUAkCallbackInfo@@@ZPEAXW4DynamicSequenceType@123@@Z");
 void *seq_play=(void*)GetProcAddress(game,"?Play@DynamicSequence@SoundEngine@AK@@YA?AW4AKRESULT@@KJW4AkCurveInterpolation@@@Z");
 void *seq_stop=(void*)GetProcAddress(game,"?Stop@DynamicSequence@SoundEngine@AK@@YA?AW4AKRESULT@@KJW4AkCurveInterpolation@@@Z");
 void *seq_pause=(void*)GetProcAddress(game,"?Pause@DynamicSequence@SoundEngine@AK@@YA?AW4AKRESULT@@KJW4AkCurveInterpolation@@@Z");
 void *seq_close=(void*)GetProcAddress(game,"?Close@DynamicSequence@SoundEngine@AK@@YA?AW4AKRESULT@@K@Z");
 get_playing_item=(PlayingItemFn)GetProcAddress(game,"?GetPlayingItem@DynamicSequence@SoundEngine@AK@@YA?AW4AKRESULT@@KAEAKAEAPEAX@Z");
 get_language=(LanguageFn)GetProcAddress(game,"?GetCurrentLanguage@StreamMgr@AK@@YAPEB_WXZ");
 if(!post||!reg||!sw||!seq_open||!seq_play||!seq_stop||!seq_pause||!seq_close||!get_playing_item){result=4;goto done;}
 log_file=_wfsopen(path,L"wb",_SH_DENYWR);
 if(!log_file){result=5;goto done;}
 if(!hooks_ready){
  if(MH_Initialize()!=MH_OK){result=6;goto close_log;}
  if(MH_CreateHook(post,on_post,(void**)&real_post)!=MH_OK||
     MH_CreateHook(fire_commit,on_fire_commit,(void**)&real_fire_commit)!=MH_OK||
     MH_CreateHook(reg,on_register,(void**)&real_register)!=MH_OK||
     MH_CreateHook(sw,on_switch,(void**)&real_switch)!=MH_OK||
     MH_CreateHook(seq_open,on_sequence_open,(void**)&real_sequence_open)!=MH_OK||
     MH_CreateHook(seq_play,on_sequence_play,(void**)&real_sequence_play)!=MH_OK||
     MH_CreateHook(seq_stop,on_sequence_stop,(void**)&real_sequence_stop)!=MH_OK||
     MH_CreateHook(seq_pause,on_sequence_pause,(void**)&real_sequence_pause)!=MH_OK||
     MH_CreateHook(seq_close,on_sequence_close,(void**)&real_sequence_close)!=MH_OK){MH_Uninitialize();result=7;goto close_log;}
  if(MH_EnableHook(MH_ALL_HOOKS)!=MH_OK){MH_Uninitialize();result=8;goto close_log;}
  void *targets[]={post,fire_commit,reg,sw,seq_open,seq_play,seq_stop,seq_pause,seq_close};
  for(unsigned i=0;i<9;i++){
   installed_targets[i]=targets[i];memcpy(installed_entries[i],targets[i],16);
  }
  hooks_ready=1;
 }
 head=tail=0;dropped=0;ZeroMemory(positions,sizeof(positions));
 fprintf(log_file,"tick,kind,object,a,b,result,name\n");fflush(log_file);
 InterlockedExchange(&active,1);
 worker=CreateThread(NULL,0,write_trace,NULL,0,NULL);
 if(!worker){active=0;result=9;goto close_log;}
 goto done;
close_log:
 fclose(log_file);log_file=NULL;
done:
 ReleaseSRWLockExclusive(&control_lock);return result;
}
BOOL WINAPI DllMain(HINSTANCE instance,DWORD reason,void *reserved) {
 (void)reserved;if(reason==DLL_PROCESS_ATTACH)DisableThreadLibraryCalls(instance);return TRUE;
}
