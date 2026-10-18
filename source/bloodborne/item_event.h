#ifndef SB_ITEM_EVENT_H
#define SB_ITEM_EVENT_H
#include <stdint.h>
/* Exact compressed sound IDs: 0=izumi, 1=shared heal, 2=blood regain.
 * Shared heal alone never confirms use. Input only arms the local player gate. */
static int item_event_accept(int pending,int sound,uint64_t now,uint64_t until,
                            uint64_t *shared,uint64_t *origin){
 if(pending<0 || pending>1 || now>=until)return 0;
 if(pending==0 && sound==0){*origin=now;return 1;}
 if(pending==1 && sound==1){*shared=now;return 0;}
 if(pending==1 && sound==2 && *shared && now>=*shared && now-*shared<=250){
  *origin=*shared;return 1;
 }
 return 0;
}
#endif
