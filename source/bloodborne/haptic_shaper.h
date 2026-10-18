#ifndef SB_HAPTIC_SHAPER_H
#define SB_HAPTIC_SHAPER_H
#include <math.h>
#include <string.h>
enum { SB_HAPTIC_IDLE,SB_HAPTIC_SWING,SB_HAPTIC_RUMBLE,SB_HAPTIC_ITEM,SB_HAPTIC_VISCERAL,SB_HAPTIC_COUNT };
typedef struct { float hp[2],prev[2],body[2],detail[2],gate; } SbHapticLane;
typedef struct { SbHapticLane lane[SB_HAPTIC_COUNT]; } SbHapticShaper;
static void sb_haptic_reset(SbHapticShaper *s){memset(s,0,sizeof(*s));}
static int sb_haptic_priority(int visceral,int rumble,int item,int swing){return visceral?SB_HAPTIC_VISCERAL:rumble?SB_HAPTIC_RUMBLE:item?SB_HAPTIC_ITEM:swing?SB_HAPTIC_SWING:SB_HAPTIC_IDLE;}
/* Original game rumble magnitudes drive tactile carriers, not mixed music.
 * This is a rumble event, not a claim of confirmed damage or weapon contact. */
typedef struct {float low,high,phase_low,phase_high;} SbRumbleTexture;
static void sb_rumble_frame(SbRumbleTexture *s,float low,float high,float out[2]){
 if(!isfinite(low))low=0;if(!isfinite(high))high=0;
 low=fmaxf(0,fminf(1,low));high=fmaxf(0,fminf(1,high));
 s->low+=(low-s->low)*(low>s->low?.006f:.0015f);
 s->high+=(high-s->high)*(high>s->high?.009f:.002f);
 s->phase_low+=6.2831853f*75/48000;s->phase_high+=6.2831853f*165/48000;
 if(s->phase_low>6.2831853f)s->phase_low-=6.2831853f;
 if(s->phase_high>6.2831853f)s->phase_high-=6.2831853f;
 float body=sinf(s->phase_low)*s->low*.12f,detail=sinf(s->phase_high)*s->high*.08f;
 out[0]=body+detail*.55f;out[1]=body*.75f+detail;
}
/* 48 kHz stereo. Independent histories avoid carrying item/finisher energy
 * into the next attack. All lanes release smoothly instead of hard switching. */
static void sb_haptic_frame(SbHapticShaper *s,int mode,const float in[2],float gain,float out[2]){
 out[0]=out[1]=0;
 if(mode<0||mode>=SB_HAPTIC_COUNT)mode=SB_HAPTIC_IDLE;
 if(!isfinite(gain))gain=0;gain=fmaxf(0,fminf(4,gain));
 for(int k=1;k<SB_HAPTIC_COUNT;k++){
  SbHapticLane *l=&s->lane[k];float target=k==mode?1.f:0.f;
  l->gate+=(target-l->gate)*(target>l->gate?.008f:.006f);
  for(int c=0;c<2;c++){
   float x=k==mode&&isfinite(in[c])?fmaxf(-24,fminf(24,in[c])):0;
   l->hp[c]=.994778f*(l->hp[c]+x-l->prev[c]);l->prev[c]=x;
   l->body[c]+=.012f*(l->hp[c]-l->body[c]);
   l->detail[c]+=.028389f*(l->hp[c]-l->detail[c]);
   float shaped;
   if(k==SB_HAPTIC_SWING)shaped=.42f*l->detail[c];
   else if(k==SB_HAPTIC_RUMBLE)shaped=1.15f*l->body[c]+.55f*(l->detail[c]-l->body[c]);
   else if(k==SB_HAPTIC_ITEM)shaped=.55f*l->body[c]+.8f*(l->detail[c]-l->body[c]);
   else shaped=l->detail[c]; /* Keep the approved finisher waveform/gain. */
   out[c]+=shaped*gain*l->gate;
  }
 }
 for(int c=0;c<2;c++)out[c]=.18f*tanhf(out[c]/.18f);
}
#endif
