#define NOMINMAX
#include <winsock2.h>
#include <windows.h>
#include <SDL3/SDL.h>
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <fstream>
#include <map>
#include <sstream>
#include <string>
#include <vector>
#include <stdexcept>
using namespace std;
struct Clip {vector<short> h,s; unsigned trigger=0;};
struct Voice {unsigned id;double position;float gain,pitch;};
static vector<Clip> bank;
static map<string,string> cfg(string path){ifstream f(path);map<string,string>d;string s;while(getline(f,s)){if(!s.empty()&&s.back()=='\r')s.pop_back();auto i=s.find('=');if(i!=string::npos)d[s.substr(0,i)]=s.substr(i+1);}return d;}
static float number(map<string,string>&d,string k,float def,float lo,float hi){try{float x=stof(d.at(k));return isfinite(x)?clamp(x,lo,hi):def;}catch(...){return def;}}
static unsigned u32(ifstream&f){unsigned v;f.read((char*)&v,4);if(!f)throw runtime_error("Bank truncated");return v;}
static void load(string path){ifstream f(path,ios::binary);char magic[8];f.read(magic,8);if(!f||string(magic,8)!="SBEVENT1")throw runtime_error("Bank format");auto n=u32(f);if(n<1||n>1000)throw runtime_error("Bank count");bank.clear();size_t total=0;for(unsigned i=0;i<n;i++){Clip c;auto h=u32(f),s=u32(f);c.trigger=u32(f);if(h<1||h>96000||s>96000||c.trigger>3)throw runtime_error("Bank bounds");total+=(h+s)*2;if(total>64*1024*1024)throw runtime_error("Bank size");c.h.resize(h);c.s.resize(s);f.read((char*)c.h.data(),h*2);f.read((char*)c.s.data(),s*2);if(!f)throw runtime_error("Bank samples");for(short v:c.h)if(v>4588||v< -4588)throw runtime_error("Bank limiter");bank.push_back(std::move(c));}if(f.peek()!=EOF)throw runtime_error("Bank trailing data");}
struct StatePulse {int ms,power;};
static vector<StatePulse> stateRules(string path){vector<StatePulse> result;if(path.empty())return result;ifstream f(path);string line;while(getline(f,line)){if(line.empty())continue;istringstream row(line);string cell;vector<string> cells;while(getline(row,cell,'\t'))cells.push_back(cell);if(cells.size()!=5)throw runtime_error("State columns");int ms=stoi(cells[3]),power=stoi(cells[4]);if(ms<30||ms>350||power<1||power>100||result.size()>=512)throw runtime_error("State pulse bounds");result.push_back({ms,power});}return result;}
static void triggers(SDL_Gamepad*p,unsigned mask,int lf=0,int rf=0){Uint8 b[47]={};b[0]=12;b[10]=b[21]=5;for(int i=0;i<2;i++)if((mask&(1u<<i))||(i?rf:lf)){int o=i?10:21;b[o]=1;b[o+1]=100;b[o+2]=(Uint8)((i?rf:lf)*25);}SDL_SendGamepadEffect(p,b,sizeof(b));}
static void speaker(SDL_Gamepad*p,bool on){Uint8 b[47]={};b[0]=0xa0;b[5]=on?70:0;b[7]=0x30;SDL_SendGamepadEffect(p,b,sizeof(b));}
int main(int argc,char**argv){
 setvbuf(stdout,nullptr,_IONBF,0);SDL_Gamepad*pad=nullptr;SDL_AudioStream*stream=nullptr;HANDLE parent=nullptr,game=nullptr;SOCKET sock=INVALID_SOCKET;bool wsa=false;int result=0;
 try{
  if(argc==3&&string(argv[1])=="--validate-states"){auto states=stateRules(argv[2]);printf("PASS state pulses: %zu bounded rules\n",states.size());return 0;}
  if(argc==3&&string(argv[1])=="--validate"){load(argv[2]);printf("PASS bank: %zu bounded clips\n",bank.size());return 0;}
  if(argc!=4)throw runtime_error("Expected config, parent PID, game PID");
  string path=argv[1];ofstream events(path+".events.log",ios::trunc);unsigned logged=0;auto d=cfg(path);load(d.at("bank"));auto states=stateRules(d["states"]);string token=d.at("token");if(token.size()!=32)throw runtime_error("Session token");
  DWORD gamePid=stoul(argv[3]);parent=OpenProcess(SYNCHRONIZE,FALSE,stoul(argv[2]));game=OpenProcess(SYNCHRONIZE,FALSE,gamePid);if(!parent||!game)throw runtime_error("Session process missing");
  WSADATA wd;if(WSAStartup(MAKEWORD(2,2),&wd))throw runtime_error("Winsock");wsa=true;sock=socket(AF_INET,SOCK_DGRAM,IPPROTO_UDP);sockaddr_in addr={};addr.sin_family=AF_INET;addr.sin_addr.s_addr=htonl(INADDR_LOOPBACK);addr.sin_port=htons((u_short)stoul(d.at("port")));if(bind(sock,(sockaddr*)&addr,sizeof(addr)))throw runtime_error("Event port busy");u_long nonblock=1;ioctlsocket(sock,FIONBIO,&nonblock);
  SDL_SetHint(SDL_HINT_JOYSTICK_HIDAPI_PS5,"1");if(!SDL_Init(SDL_INIT_AUDIO|SDL_INIT_GAMEPAD))throw runtime_error(SDL_GetError());int n=0,matches=0;auto ids=SDL_GetGamepads(&n);
  for(int i=0;i<n;i++){auto p=SDL_OpenGamepad(ids[i]);if(p&&SDL_GetGamepadVendor(p)==0x054c&&(SDL_GetGamepadProduct(p)==0x0ce6||SDL_GetGamepadProduct(p)==0x0df2)&&SDL_GetGamepadConnectionState(p)==SDL_JOYSTICK_CONNECTION_WIRED){matches++;if(!pad)pad=p;else SDL_CloseGamepad(p);}else if(p)SDL_CloseGamepad(p);}SDL_free(ids);if(matches!=1)throw runtime_error("Connect exactly one USB DualSense / Edge");
  SDL_AudioDeviceID target=0;auto devices=SDL_GetAudioPlaybackDevices(&n);matches=0;for(int i=0;i<n;i++){SDL_AudioSpec s;auto name=SDL_GetAudioDeviceName(devices[i]);if(name&&string(name).find("DualSense")!=string::npos&&SDL_GetAudioDeviceFormat(devices[i],&s,nullptr)&&s.channels==4){target=devices[i];matches++;}}SDL_free(devices);if(matches!=1)throw runtime_error("Need one DualSense four-channel audio output");
  SDL_AudioSpec spec={SDL_AUDIO_F32,4,48000};stream=SDL_OpenAudioDeviceStream(target,&spec,nullptr,nullptr);if(!stream||!SDL_ResumeAudioStreamDevice(stream))throw runtime_error(SDL_GetError());speaker(pad,number(d,"speaker",0,0,1)>0);triggers(pad,0);
  vector<Voice> voices;Uint64 reload=0,triggerEnd=0,lastPacket=0;unsigned triggerMask=0;map<unsigned,Uint64> debounce;Uint64 started=SDL_GetTicks(),hello=started;bool connected=false;int leftForce=-1,rightForce=-1;
  Uint64 lastPulse=0;auto pulse=[&](unsigned mask,int ms,int power,Uint64 now){if(!connected||number(d,"triggers",0,0,1)==0||now-lastPulse<35)return;int lf=(int)number(d,"left_force",3,0,8),rf=(int)number(d,"right_force",3,0,8);if(!lf)mask&=~1u;if(!rf)mask&=~2u;if(!mask)return;lastPulse=now;triggerMask=mask;triggerEnd=now+ms;triggers(pad,mask,(mask&1)?max(1,lf*power/100):0,(mask&2)?max(1,rf*power/100):0);};
  while(WaitForSingleObject(parent,0)==WAIT_TIMEOUT&&WaitForSingleObject(game,0)==WAIT_TIMEOUT&&GetFileAttributesA((path+".stop").c_str())==INVALID_FILE_ATTRIBUTES){
   SDL_PumpEvents();if(!SDL_GamepadConnected(pad))throw runtime_error("Controller disconnected");auto now=SDL_GetTicks();DWORD foreground=0;GetWindowThreadProcessId(GetForegroundWindow(),&foreground);
   if(now-reload>=250){d=cfg(path);reload=now;if(d["enabled"]!="1"||d["token"]!=token)break;}
   if(now-hello>2000&&connected)throw runtime_error("Game audio bridge heartbeat lost");if(!connected&&now-started>4000)throw runtime_error("Game audio bridge not loaded; install connection and restart game");
   int lf=(int)number(d,"left_force",3,0,8),rf=(int)number(d,"right_force",3,0,8);leftForce=lf;rightForce=rf;
   char packet[192];for(int count=0;count<64;count++){int got=recv(sock,packet,sizeof(packet)-1,0);if(got<=0)break;packet[got]=0;istringstream hi(string(packet,got));string hk,ht;unsigned hp;if((hi>>hk>>ht>>hp)&&hk==token&&ht=="HELLO3"&&hp==gamePid){hello=now;if(!connected){connected=true;printf("READY | game audio bridge, %zu sounds\n",bank.size());}continue;}
    if(ht=="STATE"){istringstream stateIn(string(packet,got));string key,kind,extra;unsigned id,mask;if((stateIn>>key>>kind>>id>>mask)&&!(stateIn>>extra)&&key==token&&id<states.size()&&mask>0&&mask<=3&&foreground==gamePid&&connected){pulse(mask,states[id].ms,states[id].power,now);if(logged++<10000){events<<now<<" state="<<id<<" mask="<<mask<<" triggers="<<d["triggers"]<<"\n";events.flush();}}continue;}
    istringstream in(string(packet,got));string key,extra;unsigned id,mask;float gain,pitch;
    if(!(in>>key>>id>>gain>>pitch>>mask)||(in>>extra)||key!=token||id>=bank.size()||mask>3||!isfinite(gain)||!isfinite(pitch)||gain<=0||gain>1||pitch<.25f||pitch>4||foreground!=gamePid||!connected)continue;
    if(debounce.count(id)&&now-debounce[id]<25)continue;debounce[id]=now;lastPacket=now;if(voices.size()>=8)voices.erase(voices.begin());voices.push_back({id,0,gain,pitch});
    mask&=bank[id].trigger;if(leftForce==0)mask&=~1u;if(rightForce==0)mask&=~2u;if(mask)pulse(mask,100,100,now);printf("EVENT %u gain %.3f pitch %.3f trigger %u\n",id,gain,pitch,mask);if(logged++<10000){events<<now<<" id="<<id<<" mask="<<mask<<" triggers="<<d["triggers"]<<" gain="<<gain<<"\n";events.flush();}
   }
   if(number(d,"triggers",0,0,1)==0)triggerEnd=0;
   if(foreground!=gamePid){voices.clear();SDL_ClearAudioStream(stream);triggerEnd=0;}
   if(triggerMask&&now>=triggerEnd){triggers(pad,0);triggerMask=0;}
   // At most 10 ms ahead: bounded latency and no unbounded catch-up after stalls.
   if(SDL_GetAudioStreamQueued(stream)<480*16){float out[240*4]={};float hg=number(d,"gain",2,0,4),sg=number(d,"speaker_volume",25,0,100)/100.f;bool hap=number(d,"haptics",0,0,1)>0,spk=number(d,"speaker",0,0,1)>0;
    for(int frame=0;frame<240;frame++){float h=0,s=0;for(auto&v:voices){auto&c=bank[v.id];auto p=(size_t)v.position;if(p<c.h.size())h+=c.h[p]/32768.f*v.gain;if(p<c.s.size())s+=c.s[p]/32768.f*v.gain;v.position+=v.pitch;}out[frame*4]=out[frame*4+1]=spk?clamp(s*sg,-.5f,.5f):0;out[frame*4+2]=out[frame*4+3]=hap?.18f*tanhf(h*hg/.18f):0;}
    voices.erase(remove_if(voices.begin(),voices.end(),[](const Voice&v){return v.position>=max(bank[v.id].h.size(),bank[v.id].s.size());}),voices.end());if(!SDL_PutAudioStreamData(stream,out,sizeof(out)))throw runtime_error(SDL_GetError());
   }SDL_Delay(2);
  }
 }catch(const exception&e){printf("ERROR %s\n",e.what());result=1;}
 if(pad){triggers(pad,0);speaker(pad,false);}if(stream){SDL_ClearAudioStream(stream);SDL_DestroyAudioStream(stream);}if(pad)SDL_CloseGamepad(pad);SDL_Quit();if(sock!=INVALID_SOCKET)closesocket(sock);if(wsa)WSACleanup();if(parent)CloseHandle(parent);if(game)CloseHandle(game);return result;
}
