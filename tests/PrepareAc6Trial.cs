using System;using System.IO;using System.Linq;using SenseBridge;
class PrepareAc6Trial {
 static void Main(string[] args){
  if(args.Length!=1)throw new ArgumentException("Pack required");
  if(File.Exists(Store.Library))File.Copy(Store.Library,Store.Library+".trial-"+DateTime.UtcNow.Ticks);
  var pack=Presets.Read(args[0],Store.Root);
  var games=Presets.Import(Store.Root,Store.LoadGames(),pack);
  var game=games.Single(g=>g.Adapter==Ac6Data.Adapter);
  Console.WriteLine("Imported AC6, all effects off; local Steam path: "+game.InstallDirectory);
 }
}
