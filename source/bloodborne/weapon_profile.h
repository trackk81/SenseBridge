#ifndef SB_WEAPON_PROFILE_H
#define SB_WEAPON_PROFILE_H
#include <stdint.h>
typedef enum { SB_WEAPON_UNKNOWN, SB_WEAPON_FREE, SB_WEAPON_PISTOL,
 SB_WEAPON_SHOTGUN, SB_WEAPON_CANNON, SB_WEAPON_CONTINUOUS } SbWeaponProfile;
/* Known base EquipParamWeapon IDs. Reinforcement is base + level*100.
 * Unknown/modded rows must not inherit the firearm input threshold.
 * Reference ID catalogue: Noxde/Bloodborne-save-editor weapons.json. */
static SbWeaponProfile sb_weapon_profile(uint32_t id){
 uint32_t base=id/100000*100000,upgrade=id-base;
 if(upgrade>1000 || upgrade%100)return SB_WEAPON_UNKNOWN;
 switch(base){
 case 14000000:case 14100000:case 14200000:return SB_WEAPON_PISTOL;
 case 6000000:case 6100000:case 36000000:return SB_WEAPON_SHOTGUN;
 case 15000000:case 35000000:return SB_WEAPON_CANNON;
 case 18000000:case 18100000:case 33000000:return SB_WEAPON_CONTINUOUS;
 case 19000000:case 19100000:case 20000000:case 20100000:case 34000000:
  return SB_WEAPON_FREE;
 default:return SB_WEAPON_UNKNOWN;
 }
}
static int sb_weapon_uses_fire_gate(SbWeaponProfile p){
 return p==SB_WEAPON_PISTOL || p==SB_WEAPON_SHOTGUN || p==SB_WEAPON_CANNON;
}
#endif
