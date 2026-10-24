#ifndef BB_TRIGGER_GATE_H
#define BB_TRIGGER_GATE_H
/* Both digital and analog guest input must use this result: gating only the
   button bit still lets the game fire from its analog L2 value. */
typedef struct { int armed, held; } DsTriggerGate;
static inline int ds_trigger_gate(DsTriggerGate *s,int raw,int press,int release,int blocked){
 if(blocked){s->armed=0;s->held=0;return 0;}
 if(raw<=release){s->armed=1;s->held=0;}
 else if(s->armed && !s->held && raw>=press){s->held=1;s->armed=0;}
 return s->held?255:0;
}
#endif
