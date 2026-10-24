using System;using System.Collections.Generic;using System.Diagnostics;using SenseBridge;
class ForegroundTest {
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 static void Main(){var bb=new GameProfile{Id="bb",Adapter="bloodborne-usb-v4",EngineExecutable=@"C:\TestFixtures\game\bb-probe.exe",UseTriggers=true};var steam=new GameProfile{Id="steam",Adapter="global-audio",SteamAppId="1",InstallDirectory=@"C:\TestFixtures\steam\game",UseHaptics=true};var list=new List<GameProfile>{bb,steam};
 Check(ProfileRouter.Match(list,@"C:\TestFixtures\game\bb-probe.exe")==bb,"exact path");Check(ProfileRouter.Match(list,@"C:\TestFixtures\steam\game\bin\game.exe")==steam,"Steam child path");Check(ProfileRouter.Match(list,@"C:\TestFixtures\steam\game-other\game.exe")==null,"directory boundary");Check(ProfileRouter.Match(list,@"C:\TestFixtures\steam\game\crashreporter.exe")==null,"helper exclusion");
 var sessions=new List<GameSession>{new GameSession{Pid=1,Profile=bb,Key="a"},new GameSession{Pid=2,Profile=steam,Key="b"}};
 Check(ProfileRouter.Choose(sessions,1,null).Profile==bb,"foreground A");Check(ProfileRouter.Choose(sessions,2,null).Profile==steam,"foreground B");Check(ProfileRouter.Choose(sessions,99,"a:")==null,"desktop must not keep background game");
 using(var self=Process.GetCurrentProcess()){var own=new GameProfile{Id="self",EngineExecutable=self.MainModule.FileName};using(var match=ProfileRouter.FindForeground(new List<GameProfile>{own},self.Id)){Check(match!=null&&match.Pid==self.Id,"single PID resolution");}}
 Console.WriteLine("PASS: foreground only; no background fallback; exact/Steam path matching; helper exclusion; single PID lookup");
 }
}
