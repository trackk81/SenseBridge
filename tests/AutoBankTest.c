#include "auto_haptic_bank.h"
#include <assert.h>
int main(int argc,char **argv){
 assert(argc==2);SbAutoBank b={0};assert(sb_auto_load(&b,argv[1]));
 assert(!sb_auto_clip(&b,0)&&!sb_auto_clip(&b,SB_AUTO_COUNT+1));
 for(int id=1;id<=SB_AUTO_COUNT;id++){
  const SbAutoClip *c=sb_auto_clip(&b,id);assert(c&&c->frames>0);
  assert(sb_auto_sample(c,0)==0&&sb_auto_sample(c,c->frames-1)==0&&sb_auto_sample(c,c->frames)==0);
 }
 assert(!sb_auto_load(&b,"nonexistent-test-bank.bin"));assert(b.loaded);
 /* Corrupt input must fail without replacing a previously valid bank. */
 FILE *f=fopen("invalid-auto-bank.tmp","wb");assert(f);fwrite("SBAUTO01",1,8,f);uint32_t n=0xffffffff;fwrite(&n,4,1,f);fclose(f);
 assert(!sb_auto_load(&b,"invalid-auto-bank.tmp"));assert(b.loaded);remove("invalid-auto-bank.tmp");
 sb_auto_free(&b);assert(!b.loaded&&!sb_auto_clip(&b,1));
 puts("PASS: manifest and bounded cache load, sample endpoints, invalid asset IDs, failed-load retention, free");
}
