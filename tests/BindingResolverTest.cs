using System;using System.Collections.Generic;
namespace InControl {
 public class DeviceBindingSource {public string Control{get;set;}}
 public class KeyBindingSource {public string Control{get;set;}}
 public class PlayerAction {public List<object> Bindings{get;set;}}
}
class BindingResolverTest {
 static void Check(int actual,int expected){if(actual!=expected)throw new Exception("Binding mask mismatch");}
 static void Main(){
  var a=new InControl.PlayerAction{Bindings=new List<object>()};Check(BindingResolver.Mask(a),0);
  a.Bindings.Add(new InControl.DeviceBindingSource{Control="LeftTrigger"});Check(BindingResolver.Mask(a),1);
  a.Bindings[0]=new InControl.DeviceBindingSource{Control="RightTrigger"};Check(BindingResolver.Mask(a),2);
  a.Bindings.Add(new InControl.DeviceBindingSource{Control="LeftTrigger"});Check(BindingResolver.Mask(a),3);
  a.Bindings.Clear();a.Bindings.Add(new InControl.DeviceBindingSource{Control="Action1"});Check(BindingResolver.Mask(a),0);
  a.Bindings.Add(new InControl.KeyBindingSource{Control="LeftTrigger"});Check(BindingResolver.Mask(a),0);
  Check(BindingResolver.Mask(null),0);Check(BindingResolver.Mask(new object()),0);
  Console.WriteLine("PASS: L2/R2, remapping, two bindings, face-button and keyboard exclusion, missing action");
 }
}
