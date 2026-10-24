from pathlib import Path
import os,subprocess,sys
root=Path(__file__).resolve().parent.parent
source=root/'source'; ds=source/'bloodborne';tool=Path(os.environ.get('SENSEBRIDGE_TOOLCHAIN','C:/msys64/clang64'))
os.environ['PATH']=str(tool/'bin')+os.pathsep+os.environ['PATH']
text=(ds/'runtime_dualsense.c').read_text(encoding='utf-8-sig')
text=text.replace('static pthread_mutex_t ds_lock=', 'extern const char *sb_getenv(const char*);\n#define getenv sb_getenv\nstatic pthread_mutex_t ds_lock=')
(source/'live_feedback.generated.c').write_text(text)
output=Path(sys.argv[1]) if len(sys.argv)>1 else root/'SenseBridge.Bloodborne.dll'
subprocess.run([str(tool/'bin/clang.exe'),'-shared','-O2','-Wall','-Wextra','-I'+str(ds),'-I'+str(tool/'include'),str(source/'live_bridge.c'),str(source/'live_feedback.generated.c'),str(ds/'runtime_weapon.c'),'-L'+str(tool/'lib'),'-lSDL3','-lwinpthread','-o',str(output)],check=True)
print('Built original-game adapter')
