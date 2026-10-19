using System;
using System.Collections;
using System.Reflection;
public static class BindingResolver {
 public static int Mask(object action){
  if(action==null||action.GetType().FullName!="InControl.PlayerAction")return 0;
  try{
   var property=action.GetType().GetProperty("Bindings");var bindings=property.GetValue(action,null) as IEnumerable;int mask=0;if(bindings==null)return 0;
   foreach(var binding in bindings){if(binding==null||binding.GetType().FullName!="InControl.DeviceBindingSource")continue;var control=binding.GetType().GetProperty("Control").GetValue(binding,null);string name=control==null?"":control.ToString();if(name=="LeftTrigger")mask|=1;if(name=="RightTrigger")mask|=2;}
   return mask;
  }catch{return 0;}
 }
}
