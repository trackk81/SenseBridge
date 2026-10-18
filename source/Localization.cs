using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace SenseBridge {
static class BuildFeatures {
#if SENSEBRIDGE_RELEASE
 public static bool DebugTools {get{return false;}}
#else
 public static bool DebugTools {get{return true;}}
#endif
}
public sealed class AppPreferences {
 public string Language {get;set;}
 public bool DebugButton {get;set;}
 public AppPreferences(){Language="auto";DebugButton=false;}
 public static string FilePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,@"profiles\app.json");
 public static AppPreferences Current=Read();
 static AppPreferences Read(){try{return File.Exists(FilePath)?new JavaScriptSerializer().Deserialize<AppPreferences>(File.ReadAllText(FilePath)):new AppPreferences();}catch{return new AppPreferences();}}
 public void Save(){Store.Atomic(FilePath,Store.Json.Serialize(this));}
}
public static class L {
 [DllImport("kernel32.dll")]static extern ushort GetUserDefaultUILanguage();
 public static string Language=Detect();
 static string Detect(){string value=Environment.GetEnvironmentVariable("SENSEBRIDGE_UI_LANGUAGE")??AppPreferences.Current.Language;if(value=="ko"||value=="en"||value=="ja")return value;try{value=CultureInfo.GetCultureInfo(GetUserDefaultUILanguage()).TwoLetterISOLanguageName;}catch{value=CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;}return value=="ko"||value=="ja"?value:"en";}
 static readonly Dictionary<string,string> English=Load("en"),Japanese=Load("ja");
 static Dictionary<string,string> Load(string language){string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui."+language+".json");return File.Exists(path)?new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(path)):new Dictionary<string,string>();}
 public static string T(string value){string text;return Language=="ja"&&Japanese.TryGetValue(value,out text)?text:Language!="ko"&&English.TryGetValue(value,out text)?text:value;}
 static string Key(string value){foreach(var d in new[]{English,Japanese})foreach(var pair in d)if(pair.Value==value)return pair.Key;return value;}
 public static void Refresh(Control control){if(!(control is ListBox))control.Text=T(Key(control.Text));foreach(Control child in control.Controls)Refresh(child);}
 public static void Change(){Language=Detect();}
}
}
