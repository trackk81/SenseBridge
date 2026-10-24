#ifndef SB_MATERIAL_HAPTIC_H
#define SB_MATERIAL_HAPTIC_H
#include <stdint.h>
#include <math.h>
static inline int sb_ai_movement_gate(uint64_t now,uint64_t moved){return moved&&now>=moved&&now-moved<400;}
typedef struct { uint64_t start; int material,active; } SbMaterialPulse;
static int sb_footstep_trigger(SbMaterialPulse *p,int material,uint64_t now,int allowed){
 if(!allowed||(material!=5&&material!=6))return 0;
 if(p->active&&now>=p->start&&now-p->start<140)return 0;
 p->start=now;p->material=material;p->active=1;return 1;
}
static float sb_footstep_wave(int material,unsigned sample){
 float t=sample/48000.f;
 if((material!=5&&material!=6)||t>=.075f)return 0;
 float hz=material==5?115.f:78.f;
 return .045f*(1-expf(-t*1200))*expf(-t*65)*fminf(1,(.075f-t)*1000)*sinf(6.2831853f*hz*t);
}
static int sb_material_trigger(SbMaterialPulse *p,int material,uint64_t now,
 uint64_t attack,int allowed){
 if(!allowed||((material<1||material>4)&&material!=9&&material!=10)||!attack||now<attack||now-attack>1500)return 0;
 if(p->active&&now>=p->start&&now-p->start<90)return 0;
 p->start=now;p->material=material;p->active=1;return 1;
}
/* Metal: brief low-frequency impact followed by two decaying resonances.
 * Synthesized tactile texture, not a claim of measured controller response.
 * Both ends are tapered; absolute amplitude stays below the existing cap. */
static float sb_metal_wave(unsigned sample){
 float t=sample/48000.f;if(t>=.24f)return 0;
 float strike=(1-expf(-t*2400))*expf(-t*105)*
  (.8f*sinf(6.2831853f*105*t)+.2f*sinf(6.2831853f*230*t));
 float r=fmaxf(0,t-.0025f);
 float ring=(1-expf(-r*650))*expf(-r*18)*
  (.64f*sinf(6.2831853f*165*r)+.36f*sinf(6.2831853f*223*r));
 float fade=fminf(1,(.24f-t)/.012f);
 return .16f*(.60f*strike+.40f*ring)*fade;
}
/* 48 kHz sample position, bounded tactile carrier; no speaker signal. */
static float sb_material_wave_variant(int material,unsigned sample,int metal_texture){
 if(material==1&&metal_texture)return sb_metal_wave(sample);
 float t=sample/48000.f;
 float duration=material==1?.14f:material==2?.09f:material==3?.11f:.10f;
 if(material<1||material>4||t>=duration)return 0;
 float hz=material==1?175:material==2?85:material==3?110:65;
 float envelope=(1-expf(-t*1800))*expf(-t*(material==1?32:48));
 float fade=fminf(1,(duration-t)*1000);
 return .16f*envelope*fade*(.78f*sinf(6.2831853f*hz*t)+.22f*sinf(6.2831853f*hz*1.73f*t));
}
static inline float sb_material_wave(int material,unsigned sample){return sb_material_wave_variant(material,sample,1);}
#endif
