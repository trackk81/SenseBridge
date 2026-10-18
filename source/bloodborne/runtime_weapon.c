#include <windows.h>
#include "weapon_reader.h"
static uintptr_t game_image;
void ds_weapon_set_image(void *image,unsigned long long size){
 game_image=size>0x553e880?(uintptr_t)image:0;
}
static int safe_read(void *ctx,uintptr_t at,void *out,size_t n){
 SIZE_T got=0;
 return ReadProcessMemory((HANDLE)ctx,(void*)at,out,n,&got) && got==n;
}
int ds_weapon_read(unsigned *id){
 return sb_read_weapon(safe_read,GetCurrentProcess(),game_image,id);
}
