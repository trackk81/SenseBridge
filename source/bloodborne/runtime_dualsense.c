/* Experimental USB DualSense feedback. Audio is the game's own mixed PCM;
   input/rumble gating is heuristic, never a semantic sound-event classifier. */
#include "runtime_dualsense.h"
#include "trigger_gate.h"
#include "speaker_packet.h"
#include "item_event.h"
#include "weapon_profile.h"
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
static pthread_mutex_t ds_lock=PTHREAD_MUTEX_INITIALIZER;
static SDL_AudioStream *stream;
static SDL_Gamepad *device;
static Uint64 action_until,shot_until,last_audio,last_open,last_config,last_log;
static unsigned previous_buttons;
static int previous_l2,last_effect=-1,enabled=1,triggers=1,haptics=1,registered;
static float gain=2.f,lowpass[2],highpass[2],last_input[2],gate,baseline=.005f;
static int strength=50,fire_percent=55;
static DsTriggerGate trigger_gate;
extern int ds_weapon_read(unsigned *id);
static SbWeaponProfile weapon_profile=SB_WEAPON_UNKNOWN;
static unsigned weapon_id=UINT32_MAX;
static Uint64 weapon_poll;
static void update_weapon(Uint64 now){
 if(weapon_poll && now-weapon_poll<16)return;
 weapon_poll=now;
 unsigned id=UINT32_MAX;
 if(!ds_weapon_read(&id))id=UINT32_MAX;
 SbWeaponProfile profile=sb_weapon_profile(id);
 if(id!=weapon_id || profile!=weapon_profile){
  weapon_id=id;weapon_profile=profile;trigger_gate=(DsTriggerGate){0};last_effect=-1;
  printf("DualSense: left weapon id=%u profile=%d fire_gate=%d\n",id,profile,sb_weapon_uses_fire_gate(profile));
 }
}
static int weapon_effect(void){
 return sb_weapon_uses_fire_gate(weapon_profile) || weapon_profile==SB_WEAPON_CONTINUOUS ? (int)weapon_profile:0;
}
static SDL_Thread *effect_thread;
static SDL_AtomicInt effect_stop;
static Uint64 last_pad;
static int pad_captured;
static int speaker_mode, speaker_volume=25, last_speaker=-1;
static Uint64 speaker_until;
static Uint64 vial_until;
static int vial_haptics=1;
static int queued_item=-1;
static uint64_t shared_tick,event_origin;
static int pending_item=-1;
static Uint64 pending_until;
static int visceral_enabled=1,visceral_mask,visceral_active=-1,visceral_pos;
static float visceral_gain=3.f;
static float *visceral_clips[5];static int visceral_frames[5],visceral_loaded;
static Uint64 last_attack,visceral_tick;
static void load_visceral_clips(void){
 if(visceral_loaded)return;const char *root=getenv("BB_DATA_DIR");if(!root)return;visceral_loaded=1;
 for(int i=0;i<5;i++){
  char path[1024];snprintf(path,sizeof(path),"%s/effects/visceral-%d.wav",root,i);
  SDL_AudioSpec src;Uint8 *data=NULL;Uint32 length=0;
  if(!SDL_LoadWAV(path,&src,&data,&length))continue;
  SDL_AudioSpec dst={.format=SDL_AUDIO_F32,.channels=2,.freq=48000};Uint8 *out=NULL;int bytes=0;
  if(length<=8000000&&SDL_ConvertAudioSamples(&src,data,length,&dst,&out,&bytes)&&bytes<=48000*8*10){visceral_clips[i]=(float*)out;visceral_frames[i]=bytes/8;}else SDL_free(out);
  SDL_free(data);
 }
}
/* Live v10 traces verified these exact decoder starts precede audible uses.
 * A decoder request is not an inventory callback: retain input association and
 * require the unique sound (plus the shared+unique pair for blood bullets). */
void ds_item_decode_probe(int item,unsigned instance,const void *source,unsigned long long samples){
 pthread_mutex_lock(&ds_lock);
 Uint64 now=SDL_GetTicks();
 if(item>=3&&item<=7){
  int cue=item-3;
  if(samples==0)printf("DualSense: finisher candidate cue=%d tick=%llu attack_age=%llu enabled_mask=%d\n",cue,(unsigned long long)now,(unsigned long long)(last_attack?now-last_attack:99999),visceral_mask);
  if(samples==0&&enabled&&haptics&&visceral_enabled&&(visceral_mask&(1<<cue))&&device&&!pad_captured&&now-last_pad<300&&last_attack&&now-last_attack<4000&&now-visceral_tick>300&&visceral_frames[cue]>0){
   visceral_active=cue;visceral_tick=now;visceral_pos=-1;
   printf("DualSense: finisher haptic ACCEPTED cue=%d tick=%llu\n",cue,(unsigned long long)now);
  }
  pthread_mutex_unlock(&ds_lock);return;
 }
 if(enabled && device && !pad_captured && now-last_pad<300 && samples==0 &&
    item_event_accept(pending_item,item,now,pending_until,&shared_tick,&event_origin)){
  queued_item=pending_item;pending_item=-1;
  printf("DualSense: item sound event ACCEPTED kind=%d tick=%llu origin=%llu instance=%u source=%p\n",
   queued_item,(unsigned long long)now,(unsigned long long)event_origin,instance,source);
 }
 pthread_mutex_unlock(&ds_lock);
}
static int isolated_audio,clip_ids[2]={0,1},loaded_ids[2]={-1,-1},active_clip=-1,clip_pos;
static float *clips[2];static int clip_frames[2];
static void load_item_clips(void){
 const char* root=getenv("BB_DATA_DIR");if(!root)return;
 const char* names[]={"drink.wav","blood-collected.wav","heal-switch.wav","healing.wav","est-drink.wav"};
 for(int i=0;i<2;i++)if(loaded_ids[i]!=clip_ids[i]){
  if(active_clip==i)active_clip=-1;
  SDL_free(clips[i]);clips[i]=NULL;clip_frames[i]=0;loaded_ids[i]=clip_ids[i];
  char path[1024];snprintf(path,sizeof(path),"%s/effects/%s",root,names[clip_ids[i]]);
  SDL_AudioSpec src;Uint8*data=NULL;Uint32 length=0;
  if(!SDL_LoadWAV(path,&src,&data,&length)){printf("DualSense: item clip load failed %s\n",path);continue;}
  SDL_AudioSpec dst={.format=SDL_AUDIO_F32,.channels=2,.freq=48000};Uint8*out=NULL;int bytes=0;
  if(length<=8000000 && SDL_ConvertAudioSamples(&src,data,length,&dst,&out,&bytes) && bytes<=48000*8*10){clips[i]=(float*)out;clip_frames[i]=bytes/8;printf("DualSense: isolated clip %d loaded %s frames=%d\n",i,names[clip_ids[i]],clip_frames[i]);}else SDL_free(out);
  SDL_free(data);
 }
}
static float haptic_target(Uint64 now){return now<action_until || (vial_haptics && now<vial_until) || visceral_active>=0 ? 1.f:0.f;}
static float item_source(float mixed,float sample,int use_clip){return use_clip?sample:mixed;}
static float speaker_gate;
static float speaker_target(Uint64 now){
 return enabled && speaker_mode && speaker_volume>0 && !pad_captured && now-last_pad<300 && (speaker_mode==2 || now<speaker_until) ? 1.f:0.f;
}
static void speaker_route(int active){
 if(!device)return;
 unsigned char p[47];ds_speaker_packet(p,active);
 if(SDL_SendGamepadEffect(device,p,sizeof(p))){last_speaker=active;printf("DualSense: speaker route %s (enable byte 0, volume %d)\n",active?"ON":"OFF",p[5]);}
 else fprintf(stderr,"DualSense: speaker route failed: %s\n",SDL_GetError());
}
static int effect_worker(void *unused);
static void ds_exit(void);
static void effect(SDL_Gamepad *g,int kind){
 unsigned char packet[47]={0};packet[0]=8;
 if(kind==0)packet[21]=5;
 else if(kind==SB_WEAPON_CONTINUOUS){
  /* Continuous resistance, no break point and no digital fire threshold. */
  packet[21]=1;packet[22]=50;packet[23]=(unsigned char)(strength*2);
 }
 else {
  /* Native weapon trigger: resistance wall followed by release past the wall.
     Protocol: mode 0x25, two zone bits (start/end), force minus one.
     Keep armed before the pull; audio events must not overwrite this effect. */
  unsigned zones=(1u<<2)|(1u<<5);
  int adjusted=strength+(kind==SB_WEAPON_SHOTGUN?8:kind==SB_WEAPON_CANNON?15:0);
  if(adjusted>80)adjusted=80;
  unsigned force=(unsigned)((adjusted*8+79)/80);
  if(force<1)force=1;if(force>8)force=8;
  packet[21]=0x25;packet[22]=(unsigned char)zones;packet[23]=(unsigned char)(zones>>8);
  packet[24]=(unsigned char)(force-1);
 }
 if(!SDL_SendGamepadEffect(g,packet,sizeof(packet))){fprintf(stderr,"DualSense: trigger write failed: %s\n",SDL_GetError());return;}
 printf("DualSense: weapon trigger SENT profile=%d mode=%#x strength=%d\n",kind,packet[21],strength);
 last_effect=kind;
}
static void config(Uint64 now){
 if(last_config && now-last_config<500)return;last_config=now;
 const char *path=getenv("BB_DUALSENSE_CONFIG");if(!path)return;
 FILE *f=fopen(path,"rb");if(!f)return;
 int old_strength=strength;
 char line[160],key[80];float value;
 while(fgets(line,sizeof(line),f))if(sscanf(line,"%79[^=]=%f",key,&value)==2 && isfinite(value)){
 if(!strcmp(key,"enabled"))enabled=value!=0;
 if(!strcmp(key,"triggers"))triggers=value!=0;
 if(!strcmp(key,"haptics"))haptics=value!=0;
 if(!strcmp(key,"vial_haptics"))vial_haptics=value!=0;
 if(!strcmp(key,"visceral_haptics"))visceral_enabled=value!=0;
 if(!strcmp(key,"visceral_gain"))visceral_gain=fmaxf(0,fminf(6,value));
 if(!strcmp(key,"visceral_sound_mask"))visceral_mask=(int)fmaxf(0,fminf(31,value));
 if(!strcmp(key,"isolated_audio"))isolated_audio=value!=0;
 if(!strcmp(key,"triangle_clip"))clip_ids[0]=(int)fmaxf(0,fminf(4,value));
 if(!strcmp(key,"up_clip"))clip_ids[1]=(int)fmaxf(0,fminf(4,value));
 if(!strcmp(key,"speaker_mode"))speaker_mode=(int)fmaxf(0,fminf(2,value));
 if(!strcmp(key,"speaker_volume"))speaker_volume=(int)fmaxf(0,fminf(100,value));
 if(!strcmp(key,"haptic_gain"))gain=fmaxf(0,fminf(4,value));
 if(!strcmp(key,"fire_percent"))fire_percent=(int)fmaxf(35,fminf(85,value));
 if(!strcmp(key,"trigger_strength"))strength=(int)fmaxf(0,fminf(80,value));
 }
 fclose(f);
 if(isolated_audio)load_item_clips();
 load_visceral_clips();
 if(!enabled||!haptics||!visceral_enabled)visceral_active=-1;
 if(old_strength!=strength)last_effect=-1;
}
static int effect_worker(void *unused){
 (void)unused;
 while(!SDL_GetAtomicInt(&effect_stop)){
  pthread_mutex_lock(&ds_lock);
  Uint64 now=SDL_GetTicks();
  config(now);
  update_weapon(now);
  if(!enabled){pending_item=queued_item=-1;if(stream){SDL_ClearAudioStream(stream);SDL_DestroyAudioStream(stream);stream=NULL;}action_until=shot_until=speaker_until=vial_until=0;gate=speaker_gate=0;active_clip=-1;}
  if(device && SDL_GamepadConnected(device)){
   int audible=enabled && speaker_mode && speaker_volume && !pad_captured && now-last_pad<300;
   if(audible!=last_speaker)speaker_route(audible);
   int desired=enabled && triggers && !pad_captured && now-last_pad<300 && strength>0 ? weapon_effect():0;
   if(desired!=last_effect)effect(device,desired);
  }
  pthread_mutex_unlock(&ds_lock);SDL_Delay(8);
 }
 return 0;
}
static void ds_exit(void){
 SDL_SetAtomicInt(&effect_stop,1);
 if(effect_thread){SDL_WaitThread(effect_thread,NULL);effect_thread=NULL;}
 ds_shutdown();
}
void ds_shutdown(void){
 pthread_mutex_lock(&ds_lock);
 if(device && SDL_GamepadConnected(device))effect(device,0);
 if(device && SDL_GamepadConnected(device))speaker_route(0);
 if(stream){SDL_ClearAudioStream(stream);SDL_DestroyAudioStream(stream);stream=NULL;}
 device=NULL;visceral_active=-1;last_attack=0;trigger_gate=(DsTriggerGate){0};pthread_mutex_unlock(&ds_lock);
}
void ds_pad(SDL_Gamepad *pad,unsigned buttons,int l2,int capture){
 pthread_mutex_lock(&ds_lock);Uint64 now=SDL_GetTicks();config(now);update_weapon(now);
 if(pad!=device){
  if(device && SDL_GamepadConnected(device))speaker_route(0);
  if(device && SDL_GamepadConnected(device))effect(device,0);
  if(stream){SDL_DestroyAudioStream(stream);stream=NULL;}
  device=NULL;visceral_active=-1;last_attack=0;last_effect=-1;previous_buttons=0;previous_l2=0;
  last_speaker=-1;speaker_until=vial_until=0;speaker_gate=0;active_clip=-1;pending_item=queued_item=-1;
  if(pad && SDL_GetGamepadVendor(pad)==0x054c &&
     (SDL_GetGamepadProduct(pad)==0x0ce6 || SDL_GetGamepadProduct(pad)==0x0df2) && SDL_GetGamepadConnectionState(pad)==SDL_JOYSTICK_CONNECTION_WIRED){
   device=pad;printf("DualSense: USB %s, experimental audio/input feedback enabled\n",SDL_GetGamepadName(pad));
   if(!registered){atexit(ds_exit);registered=1;}
   if(!effect_thread)effect_thread=SDL_CreateThread(effect_worker,"DualSense effects",NULL);
  }
 }
 if(!device){pthread_mutex_unlock(&ds_lock);return;}
 int down=l2>35 || (buttons&0x100);
 if(capture || !enabled){visceral_active=-1;last_attack=0;action_until=shot_until=speaker_until=vial_until=0;gate=speaker_gate=0;active_clip=pending_item=queued_item=-1;if(stream)SDL_ClearAudioStream(stream);down=0;}
 else {
  unsigned pressed=buttons & ~previous_buttons;
  if(pressed&0x800)last_attack=now;
  if((pressed&0x1010) && (vial_haptics || speaker_mode==1)){
   pending_item=pressed&0x10?1:0;pending_until=now+2500;shared_tick=0;queued_item=-1;
   active_clip=-1;vial_until=speaker_until=0;gate=speaker_gate=0;
   printf("DualSense: item awaiting unique sound event input=%s\n",pending_item?"up":"triangle");
  }
  if(pressed&(0x400|0x800|0x200))action_until=now+650;
  if(down && !previous_l2){shot_until=now+400;action_until=now+650;}
 }
 last_pad=now;pad_captured=capture;
 int desired=enabled && triggers && !capture && strength>0 ? weapon_effect():0;
 if(desired!=last_effect)effect(device,desired);
 previous_buttons=buttons;previous_l2=down;
 pthread_mutex_unlock(&ds_lock);
}
void ds_filter_l2(SDL_Gamepad *pad,unsigned *buttons,unsigned char *l2,int blocked){
 pthread_mutex_lock(&ds_lock);Uint64 now=SDL_GetTicks();config(now);update_weapon(now);
 int supported=pad && SDL_GetGamepadVendor(pad)==0x054c &&
  (SDL_GetGamepadProduct(pad)==0x0ce6 || SDL_GetGamepadProduct(pad)==0x0df2) &&
  SDL_GetGamepadConnectionState(pad)==SDL_JOYSTICK_CONNECTION_WIRED;
 if(supported && enabled && triggers && strength>0 && sb_weapon_uses_fire_gate(weapon_profile)){
  int was=trigger_gate.held,raw=*l2;
  int value=ds_trigger_gate(&trigger_gate,raw,(fire_percent*255+50)/100,51,blocked);
  *l2=(unsigned char)value;
  *buttons=(*buttons & ~0x100u)|(value?0x100u:0);
  if(value && !was)printf("DualSense: gated L2 press raw=%d threshold=%d%%\n",raw,fire_percent);
 }else trigger_gate=(DsTriggerGate){0};
 pthread_mutex_unlock(&ds_lock);
}
int ds_rumble(SDL_Gamepad *pad,unsigned low,unsigned high){
 pthread_mutex_lock(&ds_lock);
 if(enabled && device==pad && device && (low||high))action_until=SDL_GetTicks()+350;
 int handled=enabled && haptics && device && device==pad && SDL_GamepadConnected(device) && stream;
 pthread_mutex_unlock(&ds_lock);return handled;
}
static void open_stream(Uint64 now){
 if(stream || now-last_open<3000)return;last_open=now;
 int count=0,matches=0;SDL_AudioDeviceID target=0,*ids=SDL_GetAudioPlaybackDevices(&count);
 for(int i=0;i<count;i++){
  SDL_AudioSpec spec={0};const char *name=SDL_GetAudioDeviceName(ids[i]);
  if(name && strstr(name,"DualSense") && SDL_GetAudioDeviceFormat(ids[i],&spec,NULL) && spec.channels==4){matches++;target=ids[i];}
 }
 SDL_free(ids);
 if(matches!=1){printf("DualSense: need one 4-channel endpoint, found %d\n",matches);return;}
 SDL_AudioSpec spec={.format=SDL_AUDIO_F32,.channels=4,.freq=48000};
 stream=SDL_OpenAudioDeviceStream(target,&spec,NULL,NULL);
 if(stream && !SDL_ResumeAudioStreamDevice(stream)){SDL_DestroyAudioStream(stream);stream=NULL;}
 printf("DualSense: USB audio %s\n",stream?"active (speaker and haptics separately controlled)":SDL_GetError());
}
static float sample_value(const void *pcm,int floating,size_t n){
 if(floating){float v;memcpy(&v,(const char*)pcm+n*4,4);return isfinite(v)?fmaxf(-1,fminf(1,v)):0;}
 Sint16 v;memcpy(&v,(const char*)pcm+n*2,2);return v/32768.f;
}
static float filter_sample(float input,int c){
 highpass[c]=.994778f*(highpass[c]+input-last_input[c]);last_input[c]=input;
 lowpass[c]+=.028389f*(highpass[c]-lowpass[c]);
 return .18f*tanhf(lowpass[c]*gain*gate/.18f);
}
void ds_audio(const void *pcm,int frames,int channels,int floating,int port){
 if(port!=0 || !pcm || frames<1 || frames>2048 || channels<1 || channels>8)return;
 pthread_mutex_lock(&ds_lock);Uint64 now=SDL_GetTicks();
 if(!enabled || !device || !SDL_GamepadConnected(device)){pthread_mutex_unlock(&ds_lock);return;}
 if(pad_captured || now-last_pad>=300)pending_item=queued_item=-1;
 if(pending_item>=0 && now>=pending_until){printf("DualSense: item rejected (no matching sound event)\n");pending_item=-1;}
 if(now-last_audio>100){memset(lowpass,0,sizeof(lowpass));memset(highpass,0,sizeof(highpass));memset(last_input,0,sizeof(last_input));gate=0;}
 last_audio=now;
 float energy=0;
 for(int f=0;f<frames;f++)for(int c=0;c<channels;c++)energy+=fabsf(sample_value(pcm,floating,(size_t)f*channels+c));
 energy/=frames*channels;
 if(now<shot_until && energy>.012f && energy>baseline*1.7f){
  shot_until=0;action_until=now+300;
  printf("DualSense: input + audio transient haptics, energy %.4f baseline %.4f\n",energy,baseline);
 }
 baseline+=.025f*(energy-baseline);
 if(!haptics && !speaker_mode){if(stream)SDL_ClearAudioStream(stream);pthread_mutex_unlock(&ds_lock);return;}
 open_stream(now);
 if(!stream){pthread_mutex_unlock(&ds_lock);return;}
 if(visceral_active>=0&&visceral_pos<0){visceral_pos=(int)(now-visceral_tick)*48;printf("DualSense: finisher haptic OUTPUT cue=%d offset=%dms\n",visceral_active,visceral_pos/48);}
 // Start at the original sound timeline, never replay the recognition delay.
 if(queued_item>=0){
  int kind=queued_item;queued_item=-1;
  Uint64 elapsed_ms=now>=event_origin?now-event_origin:0;
  if(elapsed_ms<500 && (vial_haptics || speaker_mode==1)){
   int elapsed=(int)elapsed_ms*48;
   active_clip=isolated_audio?kind:-1;clip_pos=elapsed;
   int remaining=isolated_audio?clip_frames[kind]-elapsed:1800*48-elapsed;
   if(remaining<0)remaining=0;
   Uint64 end=now+(Uint64)remaining/48;
   if(vial_haptics)vial_until=end;if(speaker_mode==1)speaker_until=end;
   memset(lowpass,0,sizeof(lowpass));memset(highpass,0,sizeof(highpass));memset(last_input,0,sizeof(last_input));
   printf("DualSense: item event OUTPUT kind=%d tick=%llu offset=%dms queued=%d bytes\n",
    kind,(unsigned long long)now,elapsed/48,SDL_GetAudioStreamQueued(stream));
  }
 }
 float output[2048*4]={0};float peak=0;
 for(int f=0;f<frames;f++){
  if(visceral_active>=0&&visceral_pos>=visceral_frames[visceral_active])visceral_active=-1;
  float audible=speaker_target(now);
  speaker_gate+=(audible-speaker_gate)*.002f;
  float target=haptic_target(now);gate+=(target-gate)*(target>gate?.004f:.001f);
  int playing=isolated_audio && active_clip>=0 && clip_pos<clip_frames[active_clip];
  for(int c=0;c<2;c++){
   float input=sample_value(pcm,floating,(size_t)f*channels+(channels==1?0:c));
   if(channels>=3)input+=sample_value(pcm,floating,(size_t)f*channels+2)*.5f;
   float isolated=playing?clips[active_clip][clip_pos*2+c]:0;
   float haptic_input=item_source(input,isolated,isolated_audio && now<vial_until);
   if(visceral_active>=0&&visceral_pos>=0)haptic_input=visceral_clips[visceral_active][visceral_pos*2+c]*visceral_gain;
   // 40 Hz high pass, 220 Hz low pass; bounded waveform, never full-band audio.
   float v=filter_sample(haptic_input,c);if(!haptics || pad_captured || now-last_pad>=300)v=0;
   output[f*4+2+c]=v;peak=fmaxf(peak,fabsf(v));
   float speaker_input=item_source(input,isolated,isolated_audio && speaker_mode==1);
   output[f*4+c]=fmaxf(-.8f,fminf(.8f,speaker_input))*speaker_gate*(speaker_volume/100.f);
  }
  if(playing)clip_pos++;
  if(visceral_active>=0)visceral_pos++;
 }
 if(SDL_GetAudioStreamQueued(stream)>48000*4*(int)sizeof(float)/12)SDL_ClearAudioStream(stream);
 if(!SDL_PutAudioStreamData(stream,output,frames*4*sizeof(float))){SDL_DestroyAudioStream(stream);stream=NULL;}
 if(peak>.002f && now-last_log>1000){printf("DualSense: gated haptic peak %.4f\n",peak);last_log=now;}
 pthread_mutex_unlock(&ds_lock);
}
