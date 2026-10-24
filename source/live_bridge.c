/* Local, version-pinned Bloodborne import adapter. Does not alter game files. */
#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <SDL3/SDL.h>
#include "runtime_dualsense.h"
#include "item_encoded.h"
#include "material_encoded.h"
#include "attach_profile.h"
#define ABI __attribute__((sysv_abi))
extern void ds_weapon_set_image(void*,unsigned long long);
static char sb_root[1024],sb_data[1200],sb_config[1300];
static INIT_ONCE sb_paths_once=INIT_ONCE_STATIC_INIT;
static BOOL CALLBACK sb_init_paths(PINIT_ONCE once,PVOID arg,PVOID *context){
 HMODULE self=NULL;(void)once;(void)arg;(void)context;
 if(!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,(LPCSTR)&sb_init_paths,&self))return FALSE;
 DWORD n=GetModuleFileNameA(self,sb_root,sizeof(sb_root));if(!n||n>=sizeof(sb_root))return FALSE;
 char *slash=strrchr(sb_root,'\\');if(!slash)return FALSE;*slash=0;
 snprintf(sb_data,sizeof(sb_data),"%s/Bloodborne/runtime",sb_root);
 snprintf(sb_config,sizeof(sb_config),"%s/dualsense.ini",sb_data);return TRUE;
}
const char *sb_getenv(const char *key){
 if(!InitOnceExecuteOnce(&sb_paths_once,sb_init_paths,NULL,NULL))return NULL;
 if(!strcmp(key,"BB_DATA_DIR"))return sb_data;
 if(!strcmp(key,"BB_DUALSENSE_CONFIG"))return sb_config;
 return getenv(key);
}
static void *original[8];
static volatile LONG ready;
static SRWLOCK pad_lock=SRWLOCK_INIT,port_lock=SRWLOCK_INIT;
static LONG pad_calls,audio_calls,item_calls,ajm_calls,signature_matches,missing_results,rejected_results;
typedef struct {int frames,channels,floating,type,used;} Port;
static Port ports[32];
static int read_mem(const void *p,void *out,size_t n){SIZE_T got;return ReadProcessMemory(GetCurrentProcess(),p,out,n,&got)&&got==n;}
typedef struct {unsigned buttons;unsigned char axes[4],l2,r2,rest[110];} PadData;
_Static_assert(sizeof(PadData)==120,"Guest pad layout");
static SDL_Gamepad *current_pad(void){
 int n=0;SDL_JoystickID *ids=SDL_GetGamepads(&n);
 /* Never guess which device controls the game when multiple pads are present. */
 SDL_Gamepad *p=n==1?SDL_GetGamepadFromID(ids[0]):NULL;SDL_free(ids);return p;
}
static void filter(PadData *d){
 if(!InterlockedCompareExchange(&ready,0,0))return;
 AcquireSRWLockExclusive(&pad_lock);
 SDL_Gamepad *p=current_pad();DWORD foreground=0;GetWindowThreadProcessId(GetForegroundWindow(),&foreground);
 int blocked=foreground!=GetCurrentProcessId();
 ds_filter_l2(p,&d->buttons,&d->l2,blocked);ds_pad(p,d->buttons,d->l2,blocked);
 InterlockedIncrement(&pad_calls);ReleaseSRWLockExclusive(&pad_lock);
}
static ABI int32_t pad_state(int32_t h,PadData*d){
 int32_t r=((int32_t(ABI*)(int32_t,PadData*))original[0])(h,d);if(!r&&d)filter(d);return r;
}
static ABI int32_t pad_read(int32_t h,PadData*d,int32_t n){
 int32_t r=((int32_t(ABI*)(int32_t,PadData*,int32_t))original[1])(h,d,n);if(r>0&&r<=n&&d)for(int i=0;i<r;i++)filter(d+i);return r;
}
static ABI int32_t pad_rumble(int32_t h,const uint8_t*p){
 if(InterlockedCompareExchange(&ready,0,0)&&p){AcquireSRWLockExclusive(&pad_lock);int handled=ds_rumble(current_pad(),p[0],p[1]);ReleaseSRWLockExclusive(&pad_lock);if(handled)return 0;}
 return ((int32_t(ABI*)(int32_t,const uint8_t*))original[2])(h,p);
}
static ABI int32_t audio_open(int32_t u,int32_t t,int32_t i,uint32_t n,uint32_t f,uint32_t p){
 int32_t r=((int32_t(ABI*)(int32_t,int32_t,int32_t,uint32_t,uint32_t,uint32_t))original[3])(u,t,i,n,f,p);
 static const int channels[]={1,2,8,1,2,8,8,8};unsigned fmt=p&255,id=r&65535;
 if(r>=0&&id<32&&fmt<8&&n<=2048&&f==48000){AcquireSRWLockExclusive(&port_lock);ports[id]=(Port){n,channels[fmt],fmt>=3&&fmt!=6,t,1};ReleaseSRWLockExclusive(&port_lock);}return r;
}
static ABI int32_t audio_close(int32_t h){
 int32_t r=((int32_t(ABI*)(int32_t))original[4])(h);unsigned id=h&65535;
 if(!r&&id<32){AcquireSRWLockExclusive(&port_lock);ports[id].used=0;ReleaseSRWLockExclusive(&port_lock);}return r;
}
static void feed(int32_t h,const void *data){
 if(!InterlockedCompareExchange(&ready,0,0)||!data)return;
 unsigned id=h&65535;if(id>=32)return;
 AcquireSRWLockShared(&port_lock);Port p=ports[id];ReleaseSRWLockShared(&port_lock);
 if(p.used&&p.type==0){ds_audio(data,p.frames,p.channels,p.floating,p.type);InterlockedIncrement(&audio_calls);}
}
static ABI int32_t audio_output(int32_t h,const void *data){
 feed(h,data);return ((int32_t(ABI*)(int32_t,const void*))original[5])(h,data);
}
typedef struct {int32_t handle;const void *data;} Output;
static ABI int32_t audio_outputs(const Output *p,uint32_t n){
 if(p&&n<=32)for(unsigned i=0;i<n;i++)feed(p[i].handle,p[i].data);
 return ((int32_t(ABI*)(const Output*,uint32_t))original[6])(p,n);
}
typedef struct {uint32_t word,size;} Chunk;
typedef struct {uint32_t word,size;void *address;} Buffer;
typedef struct {int kind,asset;unsigned instance;const void *source,*result;uint64_t flags;uint32_t offset;} Event;
static int find_sound(const void *input,uint32_t size,uint32_t *offset,int *asset){
 if(size<64||size>4*1024*1024)return -1;
 unsigned char *bytes=malloc(size);if(!bytes)return -1;
 int kind=-1;if(read_mem(input,bytes,size))for(uint32_t i=0;i<=size-64;i++){
  if(bytes[i]!=37&&bytes[i]!=33)continue;
  kind=item_encoded_id(bytes+i,size-i);*asset=sb_sound_encoded(bytes+i,size-i);
  if(kind>=0||*asset){*offset=i;break;}
 }
 free(bytes);return kind;
}
static int collect(const unsigned char *buffer,uint32_t size,Event events[16]){
 if(size>16*1024*1024)return 0;int count=0;
 for(size_t at=0;at+8<=size;){
  Chunk c;if(!read_mem(buffer+at,&c,8)||c.size>size-at-8)return 0;
  size_t end=at+8+c.size;at+=8;
  if((c.word&63)==0){
   Event e={.kind=-1,.instance=(c.word>>6)&0xfffff};int inputs=0,run=0;
   while(at<end){
    Chunk s;if(end-at<8||!read_mem(buffer+at,&s,8))return 0;unsigned tag=s.word&63;
    if(tag==3||tag==4){if(tag==4){run=1;e.flags=((uint64_t)((s.word>>6)&0xfffff)<<32)|s.size;}at+=8;continue;}
    if(tag!=1&&tag!=2&&tag!=6&&tag!=17&&tag!=18)return 0;
    Buffer b;if(end-at<16||!read_mem(buffer+at,&b,16))return 0;at+=16;
    if(tag==1){inputs++;e.kind=find_sound(b.address,b.size,&e.offset,&e.asset);e.source=b.address;if(e.kind>=0||e.asset)InterlockedIncrement(&signature_matches);}
    if(tag==18&&b.size>=8)e.result=b.address;
   }
   if(run&&inputs==1&&(e.kind>=0||e.asset)){if(e.result&&count<16)events[count++]=e;else InterlockedIncrement(&missing_results);}
  }else at=end;
 }
 return count;
}
static ABI int32_t ajm_start(uint32_t id,unsigned char *b,uint32_t n,int priority,void *err,uint32_t *out){
 InterlockedIncrement(&ajm_calls);
 Event events[16];int count=InterlockedCompareExchange(&ready,0,0)?collect(b,n,events):0;
 int32_t r=((int32_t(ABI*)(uint32_t,unsigned char*,uint32_t,int,void*,uint32_t*))original[7])(id,b,n,priority,err,out);
 if(!r)for(int i=0;i<count;i++){
  int result[4];int accepted=0;
  if(read_mem(events[i].result,result,8)&&result[1]==0){
   if(events[i].flags&(1ULL<<47)){
    /* A batch can decode the sound and then report a full output buffer.
       Require actual consumption/output, not an entirely empty result mask. */
    if(read_mem(events[i].result,result,16)&&!(result[0]&~0x18)&&result[2]>(int)events[i].offset&&result[3]>0)accepted=1;
   }else if(result[0]==0&&events[i].offset==0)accepted=1;
  }
  if(accepted){
   if(events[i].kind>=0)ds_item_decode_probe(events[i].kind,events[i].instance,events[i].source,0);
   if(events[i].asset>0)ds_sound_asset(events[i].asset,sb_sound_material(events[i].asset),events[i].instance);
   InterlockedIncrement(&item_calls);
  }else InterlockedIncrement(&rejected_results);
 }
 return r;
}
static void *replacements[]={pad_state,pad_read,pad_rumble,audio_open,audio_close,audio_output,audio_outputs,ajm_start};
static uintptr_t find_image(void){
 MEMORY_BASIC_INFORMATION m;uintptr_t at=0,last=0,found=0;
 while(VirtualQuery((void*)at,&m,sizeof(m))){
  uintptr_t base=(uintptr_t)m.AllocationBase;
  if(base&&base!=last&&m.Type==MEM_PRIVATE){
   last=base;int valid=1;for(unsigned i=0;i<sizeof(anchors)/sizeof(*anchors);i++){unsigned char b[64];if(!read_mem((void*)(base+anchors[i].offset),b,64)||memcmp(b,anchors[i].bytes,64)){valid=0;break;}}
   if(valid){if(found)return 0;found=base;}
  }
  uintptr_t next=(uintptr_t)m.BaseAddress+m.RegionSize;if(next<=at)break;at=next;
 }return found;
}
static DWORD WINAPI diagnostics(void *unused){
 (void)unused;for(;;){Sleep(2000);char path[1400];InitOnceExecuteOnce(&sb_paths_once,sb_init_paths,NULL,NULL);snprintf(path,sizeof(path),"%s/attach-%lu.status",sb_root,GetCurrentProcessId());FILE*f=fopen(path,"w");if(f){fprintf(f,"ready=%ld\npad_calls=%ld\naudio_calls=%ld\nitem_candidates=%ld\najm_calls=%ld\nsignature_matches=%ld\nmissing_results=%ld\nrejected_results=%ld\n",ready,pad_calls,audio_calls,item_calls,ajm_calls,signature_matches,missing_results,rejected_results);fclose(f);}}return 0;
}
typedef struct {int frames,channels,floating;} Init;
__declspec(dllexport) DWORD WINAPI SenseBridgeStart(void *arg){
 if(InterlockedCompareExchange(&ready,0,0))return 0;
 Init init;if(!read_mem(arg,&init,sizeof(init))||init.frames<1||init.frames>2048||(init.channels!=1&&init.channels!=2&&init.channels!=8)||init.floating<0||init.floating>1)return 10;
 uintptr_t base=find_image();if(!base)return 11;
 HMODULE exe=GetModuleHandleW(NULL);MEMORY_BASIC_INFORMATION m;
 for(unsigned i=0;i<sizeof(slots)/sizeof(*slots);i++){
  void *p;if(!read_mem((void*)(base+slots[i].offset),&p,8)||!VirtualQuery(p,&m,sizeof(m))||m.AllocationBase!=exe||!(m.Protect&(PAGE_EXECUTE|PAGE_EXECUTE_READ|PAGE_EXECUTE_READWRITE)))return 12;
  if(original[slots[i].hook]&&original[slots[i].hook]!=p)return 13;original[slots[i].hook]=p;
 }
 ports[0]=(Port){init.frames,init.channels,init.floating,0,1};ds_weapon_set_image((void*)base,IMAGE_SIZE);
 unsigned done=0;
 for(;done<sizeof(slots)/sizeof(*slots);done++){
  void **p=(void**)(base+slots[done].offset);DWORD old,tmp;
  if(!VirtualProtect(p,8,PAGE_READWRITE,&old))break;
  void *was=InterlockedCompareExchangePointer(p,replacements[slots[done].hook],original[slots[done].hook]);VirtualProtect(p,8,old,&tmp);
  if(was!=original[slots[done].hook])break;
 }
 if(done!=sizeof(slots)/sizeof(*slots)){
  while(done){done--;void **p=(void**)(base+slots[done].offset);DWORD old,tmp;if(VirtualProtect(p,8,PAGE_READWRITE,&old)){InterlockedCompareExchangePointer(p,original[slots[done].hook],replacements[slots[done].hook]);VirtualProtect(p,8,old,&tmp);}}return 14;
 }
 InterlockedExchange(&ready,1);HANDLE t=CreateThread(NULL,0,diagnostics,NULL,0,NULL);if(t)CloseHandle(t);return 0;
}
BOOL WINAPI DllMain(HINSTANCE h,DWORD reason,LPVOID reserved){(void)reserved;if(reason==DLL_PROCESS_ATTACH)DisableThreadLibraryCalls(h);return TRUE;}
#ifdef SB_PARSER_TEST
#include <assert.h>
int main(void){
 unsigned char input[64]={37,201,0,20,192,28,154,248,174,76,33,30,60,133,89,33,8,127,156,209,44,105,89,236,211,112,164,104,76,177,69,41,110,125,202,55,232,153,17,161,8,65,64,250,177,156,102,231,31,86,83,61,0,222,134,240,191,167,58,215,252,242,251,219};
 unsigned char batch[48]={0};int result[2]={0};Chunk job={42<<6,40},flags={4,1};Buffer in={1,64,input},side={18,8,result};Event events[16];
 memcpy(batch,&job,8);memcpy(batch+8,&in,16);memcpy(batch+24,&flags,8);memcpy(batch+32,&side,16);
 assert(collect(batch,48,events)==1&&events[0].kind==0&&events[0].instance==42&&events[0].result==result);
 assert(collect(batch,47,events)==0);assert(collect(batch,7,events)==0);
 job.size=UINT32_MAX;memcpy(batch,&job,8);assert(collect(batch,48,events)==0);
 job.size=40;memcpy(batch,&job,8);in.address=(void*)1;memcpy(batch+8,&in,16);assert(collect(batch,48,events)==0);
 in.address=input;in.size=63;memcpy(batch+8,&in,16);assert(collect(batch,48,events)==0);
 in.size=64;memcpy(batch+8,&in,16);input[0]^=1;assert(collect(batch,48,events)==0);input[0]^=1;
 unsigned char delayed[160]={0};memcpy(delayed+80,input,64);in.address=delayed;in.size=160;memcpy(batch+8,&in,16);
 assert(collect(batch,48,events)==1&&events[0].offset==80);
 /* Each allowed impact/footstep identity must survive the public adapter parser. */
 for(unsigned i=0;i<sizeof(sb_material_signatures)/sizeof(*sb_material_signatures);i++){
  memset(delayed,0,sizeof(delayed));memcpy(delayed+80,sb_material_signatures[i].signature,64);
  int matched=collect(batch,48,events);
  if(!(matched==1&&events[0].asset==sb_material_signatures[i].asset&&events[0].kind==-1&&events[0].offset==80))fprintf(stderr,"asset test row=%u count=%d asset=%d kind=%d offset=%u first=%u\n",i,matched,events[0].asset,events[0].kind,events[0].offset,sb_material_signatures[i].signature[0]);
  assert(matched==1&&events[0].asset==sb_material_signatures[i].asset&&events[0].kind==-1&&events[0].offset==80);
 }
 memset(delayed,0,sizeof(delayed));assert(collect(batch,48,events)==0);
 puts("PASS: item signature, job identity, result pointer, truncated chunks, oversized chunks, unreadable input, short input, unrelated sound");return 0;
}
#endif
