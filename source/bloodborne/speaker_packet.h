#ifndef DS_SPEAKER_PACKET_H
#define DS_SPEAKER_PACKET_H
#include <string.h>
/* SDL testcontroller DS5EffectsState_t: enable bits 1 at byte 0,
   speaker volume at 5, audio routing at 7. Byte 1 is enable bits 2. */
static void ds_speaker_packet(unsigned char packet[47],int active){
 memset(packet,0,47);packet[0]=0xa0;packet[5]=active?70:0;packet[7]=0x30;
}
#endif
