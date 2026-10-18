#ifndef BB_DUALSENSE_H
#define BB_DUALSENSE_H
#include <SDL3/SDL.h>
void ds_pad(SDL_Gamepad *pad, unsigned buttons, int l2, int capture);
void ds_filter_l2(SDL_Gamepad *pad,unsigned *buttons,unsigned char *l2,int blocked);
int ds_rumble(SDL_Gamepad *pad,unsigned low,unsigned high);
void ds_audio(const void *pcm,int frames,int channels,int floating,int port);
void ds_shutdown(void);
void ds_item_decode_probe(int item,unsigned instance,const void *source,unsigned long long samples);
#endif
