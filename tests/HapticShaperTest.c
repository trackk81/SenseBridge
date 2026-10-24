#include <assert.h>
#include <stdio.h>
#include "../source/bloodborne/haptic_shaper.h"
static float energy(int mode){SbHapticShaper s={0};float sum=0;for(int i=0;i<4800;i++){float in[2]={.08f*sinf(i*6.2831853f*100/48000),0},out[2];sb_haptic_frame(&s,mode,in,2,out);sum+=out[0]*out[0];assert(out[1]==0);assert(isfinite(out[0])&&fabsf(out[0])<=.180001f);}return sum;}
int main(void){
 assert(sb_haptic_priority(1,1,1,1)==SB_HAPTIC_VISCERAL);assert(sb_haptic_priority(0,1,1,1)==SB_HAPTIC_RUMBLE);assert(sb_haptic_priority(0,0,1,1)==SB_HAPTIC_ITEM);
 assert(energy(SB_HAPTIC_SWING)<energy(SB_HAPTIC_RUMBLE));
 SbHapticShaper s={0};float out[2],in[2];
 for(int i=0;i<100000;i++){in[0]=i%97==0?NAN:24*sinf(i*.03f);in[1]=i%101==0?INFINITY:-in[0];sb_haptic_frame(&s,1+(i/997)%4,in,4,out);assert(isfinite(out[0])&&isfinite(out[1]));assert(fabsf(out[0])<=.180001f&&fabsf(out[1])<=.180001f);}
 in[0]=in[1]=0;for(int i=0;i<24000;i++)sb_haptic_frame(&s,SB_HAPTIC_IDLE,in,2,out);assert(fabsf(out[0])<.000001f&&fabsf(out[1])<.000001f);
 sb_haptic_reset(&s);sb_haptic_frame(&s,SB_HAPTIC_VISCERAL,in,2,out);assert(out[0]==0&&out[1]==0);
 SbRumbleTexture rt={0};float wave[2],acc=0;for(int i=0;i<4800;i++){sb_rumble_frame(&rt,1,1,wave);assert(isfinite(wave[0])&&fabsf(wave[0])<=.201f);acc+=wave[0]*wave[0];}assert(acc>1);
 for(int i=0;i<24000;i++)sb_rumble_frame(&rt,0,0,wave);assert(fabsf(wave[0])<.000001f);
 puts("PASS: priority, swing/impact contrast, stereo isolation, finite bounds, release and reset");return 0;
}
