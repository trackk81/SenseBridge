#ifndef SB_AUTO_HAPTIC_BANK_H
#define SB_AUTO_HAPTIC_BANK_H
#include "auto_haptic_catalog.h"
#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
typedef struct {int16_t *pcm;uint32_t frames,material;} SbAutoClip;
typedef struct {SbAutoClip clips[SB_AUTO_COUNT];int loaded;} SbAutoBank;
static void sb_auto_free(SbAutoBank *b){for(unsigned i=0;i<SB_AUTO_COUNT;i++)free(b->clips[i].pcm);memset(b,0,sizeof(*b));}
static int sb_auto_load(SbAutoBank *b,const char *path){
 FILE *f=fopen(path,"rb");if(!f)return 0;
 SbAutoBank temp={0};unsigned char magic[8],digest[16];uint32_t count;
 int ok=fread(magic,1,8,f)==8&&!memcmp(magic,"SBAUTO01",8)&&fread(&count,4,1,f)==1&&count==SB_AUTO_COUNT&&fread(digest,1,16,f)==16&&!memcmp(digest,sb_auto_digest,16);
 for(unsigned i=0;ok&&i<count;i++){
  SbAutoClip *c=&temp.clips[i];
  ok=fread(&c->frames,4,1,f)==1&&c->frames>0&&c->frames<=480000&&fread(&c->material,4,1,f)==1&&c->material>=1&&c->material<=8;
  if(ok){c->pcm=malloc(c->frames*2);ok=c->pcm&&fread(c->pcm,2,c->frames,f)==c->frames;}
  if(ok)for(unsigned n=0;n<c->frames;n++)if(c->pcm[n]>4588||c->pcm[n]<-4588){ok=0;break;}
 }
 if(ok)ok=fgetc(f)==EOF&&!ferror(f);fclose(f);
 if(!ok){sb_auto_free(&temp);return 0;}
 temp.loaded=1;sb_auto_free(b);*b=temp;return 1;
}
static const SbAutoClip *sb_auto_clip(const SbAutoBank *b,int id){return b->loaded&&id>0&&id<=SB_AUTO_COUNT?&b->clips[id-1]:NULL;}
static float sb_auto_sample(const SbAutoClip *clip,unsigned n){return clip&&n<clip->frames?clip->pcm[n]/32768.f:0;}
#endif
