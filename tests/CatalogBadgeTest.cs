using System;using SenseBridge;
class CatalogBadgeTest {
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 static void Main(){
  var silk=new GameProfile{SteamAppId="1030300",Adapter="global-audio"};
  Check(PackDownloads.Supported(silk),"Published data must show support before installation");
  Check(PackDownloads.Available(silk),"Uninstalled data must remain downloadable");
  silk.Adapter="unity-audio-v1";
  Check(PackDownloads.Supported(silk)&&!PackDownloads.Available(silk),"Installation must not change support badge");
  Check(!PackDownloads.Supported(new GameProfile{SteamAppId="1888160",Adapter="global-audio"}),"Unpublished AC6 must not be advertised as published");
  Check(!PackDownloads.Supported(new GameProfile{SteamAppId="99999999",Adapter="unity-audio-v1"}),"Private installed pack must not imply published catalog support");
  Check(PackDownloads.Supported(new GameProfile{Adapter="bloodborne-usb-v4"}),"Built-in Bloodborne");
  Console.WriteLine("PASS: published/installed distinction, download routing, unpublished/private packs, built-in support");
 }
}
