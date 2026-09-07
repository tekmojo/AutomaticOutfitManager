param([string]$TypeName,[string]$MethodPattern)
$managedDir='F:\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed'
[System.Reflection.Assembly]::LoadFrom((Join-Path $managedDir 'UnityEngine.CoreModule.dll')) | Out-Null
$asm=[System.Reflection.Assembly]::LoadFrom((Join-Path $managedDir 'Assembly-CSharp.dll'))
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
public static class AomIL {
 static Dictionary<short,OpCode> codes = new Dictionary<short,OpCode>();
 static AomIL(){foreach(var f in typeof(OpCodes).GetFields()) if(f.FieldType==typeof(OpCode)){var o=(OpCode)f.GetValue(null); codes[o.Value]=o;}}
 public static void Dump(MethodBase m){
  Console.WriteLine(m.DeclaringType.FullName+"::"+m);
  var body=m.GetMethodBody(); if(body==null)return; var b=body.GetILAsByteArray();
  for(int p=0;p<b.Length;){ int start=p; short n=b[p++];if(n==254)n=(short)(0xfe00|b[p++]);var o=codes[n];object v="";
   switch(o.OperandType){
    case OperandType.InlineNone:break;
    case OperandType.ShortInlineI:case OperandType.ShortInlineVar:case OperandType.ShortInlineBrTarget:v=b[p++];break;
    case OperandType.InlineVar:v=BitConverter.ToUInt16(b,p);p+=2;break;
    case OperandType.InlineI8:case OperandType.InlineR:v=BitConverter.ToInt64(b,p);p+=8;break;
    case OperandType.InlineSwitch:int count=BitConverter.ToInt32(b,p);p+=4+count*4;v="switch "+count;break;
    default:int t=BitConverter.ToInt32(b,p);p+=4;v=t;
     try{if(o.OperandType==OperandType.InlineString)v=m.Module.ResolveString(t);
      else if(o.OperandType==OperandType.InlineMethod||o.OperandType==OperandType.InlineField||o.OperandType==OperandType.InlineType||o.OperandType==OperandType.InlineTok)v=m.Module.ResolveMember(t,m.DeclaringType.GetGenericArguments(),m.IsGenericMethod?m.GetGenericArguments():null);
     }catch{} break;
   }
   Console.WriteLine(start.ToString("X4")+" "+o.Name+" "+v);
  }
 }
}
"@
$t=$asm.GetType($TypeName)
foreach($candidate in @($t)+@($t.GetNestedTypes([Reflection.BindingFlags]'Public,NonPublic'))){
 foreach($method in $candidate.GetMethods([Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly')) {
  if(($candidate.FullName+'::'+$method.Name) -match $MethodPattern){[AomIL]::Dump($method)}
 }
}
