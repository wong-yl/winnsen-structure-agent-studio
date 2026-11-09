"""Inspect physical shelf bores and the two dangling right weld mates."""
import shutil
import subprocess
from generate_model import STAGE, read, write, sha

target=STAGE/'scripts/shelf_mates_probe_r420';assert not target.exists()
shutil.copytree(STAGE/'scripts/shelf_reload_probe_r417',target)
p=target/'ShelfContextProbeR416.cs';t=p.read_text(encoding='utf-8')
key='row["native_save_performed"]=false;'
assert t.count(key)==1
t=t.replace(key,r'''var rightShelf=Doc("箱体横层板R.SLDPRT");rightShelf.ForceRebuild3(false);
            var rightWeld=Doc("箱体横层板R焊接.SLDASM");RmActivate(rightWeld,"shelf_right_mates");rightWeld.ForceRebuild3(false);
            row["right_weld"]=new{health=LayoutFeatureHealth(rightWeld),mates=LayoutMates(rightWeld),states=ShStates(rightWeld),parts=((Array)((AssemblyDoc)rightWeld).GetComponents(true)).Cast<Component2>().Select(v=>new{name=v.Name2,path=v.GetPathName(),axes=ShelfAllAxes(v).Select(a=>new{point=a.Point,direction=a.Direction,radius=a.Radius}).ToArray()}).ToArray()};Flush("right_shelf_mates_inventory");
            row["native_save_performed"]=false;''')
p.write_text(t,encoding='utf-8')
(target/'ShelfAllAxesR420.cs').write_text(r'''using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145 {
    static List<RmAxis> ShelfAllAxes(Component2 c){
        object info;var bodies=c.GetBodies3(0,out info) as Array;var rows=new List<RmAxis>();
        foreach(Body2 b in bodies??new object[0])foreach(Face2 f in (Array)b.GetFaces()){
            var s=(Surface)f.GetSurface();if(!s.IsCylinder())continue;var v=(double[])s.CylinderParams;
            if(Math.Abs(v[6]-.001)>1e-7)continue;
            var p=RmPoint(v.Take(3).ToArray(),c.Transform2);var n=RmVector(v.Skip(3).Take(3).ToArray(),c.Transform2);
            if(!rows.Any(a=>Math.Abs(Math.Abs(RmDot(a.Direction,n))-1)<1e-7&&ShelfAxisOffset(a.Point,p,n)<1e-7))rows.Add(new RmAxis{Face=f,Point=p,Direction=n,Radius=v[6]});
        }return rows;
    }
    static double ShelfAxisOffset(double[] a,double[] b,double[] n){var d=a.Zip(b,(x,y)=>x-y).ToArray();double q=RmDot(d,n);return Math.Sqrt(Math.Max(0,RmDot(d,d)-q*q));}
}
''',encoding='utf-8')
tool=STAGE/'OutdoorShelfMatesProbeR420.exe'
subprocess.run([r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe','/nologo','/target:exe','/platform:x64','/optimize+','/out:'+str(tool),'/reference:System.Web.Extensions.dll','/reference:'+str(STAGE/'SolidWorks.Interop.sldworks.dll'),'/reference:'+str(STAGE/'SolidWorks.Interop.swconst.dll'),*map(str,target.glob('*.cs'))],check=True)
(STAGE/'scripts/run_shelf_mates_probe_r420.mjs').write_text((STAGE/'scripts/run_shelf_reload_probe_r417.mjs').read_text(encoding='utf-8').replace('OutdoorShelfReloadProbeR417.exe',tool.name),encoding='utf-8')
c=read(STAGE/'evidence/shelf_reload_r417_contract.json');c['cases'][0]['id']='shelf_mates_r420'
write(STAGE/'evidence/shelf_mates_r420_contract.json',c)
write(STAGE/'evidence/shelf_mates_r420_build.json',{'status':'DIAGNOSTIC_NOT_ACCEPTED','tool':str(tool),'sha256':sha(tool),'sources':{str(p):sha(p) for p in target.glob('*.cs')}})
