#ifndef SB_WEAPON_READER_H
#define SB_WEAPON_READER_H
#include <stdint.h>
#include <stddef.h>
typedef int (*SbReadMemory)(void *,uintptr_t,void *,size_t);
/* Verified local CUSA03173 1.09 chain. Relocated image-relative addresses;
 * validate object types, player slot ownership, paired IDs and stable roots.
 * All reads are fault-safe. No writes to guest memory. */
static int sb_read_weapon(SbReadMemory read,void *ctx,uintptr_t image,uint32_t *id){
 uintptr_t world=0,player=0,equip=0,vt=0,slot=0,owner=0,again=0;
 uint32_t ids[2];
 *id=UINT32_MAX;
 if(!image)return 0;
#define RD(a,v) read(ctx,(a),&(v),sizeof(v))
 if(!RD(image+0x553e878,world) || world<0x10000 ||
    !RD(world,vt) || vt!=image+0x53301f0 ||
    !RD(world+0x60,player) || player<0x10000 ||
    !RD(player,vt) || vt!=image+0x538f310 ||
    !RD(world+0x38,slot) || slot<0x10000 ||
    !RD(slot,owner) || owner!=player ||
    !RD(player+0x18,again) || again!=slot ||
    !RD(player+0x4c0,equip) || equip<0x10000 ||
    !RD(equip,vt) || vt!=image+0x534ad80 ||
    !read(ctx,equip+0x240,ids,sizeof(ids)) || ids[0]!=ids[1] ||
    !RD(world+0x60,again) || again!=player ||
    !RD(image+0x553e878,again) || again!=world)return 0;
 *id=ids[0];return 1;
#undef RD
}
#endif
