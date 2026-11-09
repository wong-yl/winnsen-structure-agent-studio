using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SolidWorks.Interop.sldworks;
internal static partial class OutdoorShadeR145
{
    static Dictionary<string,bool> CaptureCanopyDirections(ModelDoc2 assembly)
    {
        var result=new Dictionary<string,bool>();
        foreach(string file in new[]{"立柱连接架.SLDPRT","太阳能连接压板.SLDPRT"})
            foreach(var f in Features(Doc(file)).Where(f=>f.GetTypeName2()=="MoveFace"))
                result[file+"|"+f.Name]=((IMoveFaceFeatureData)f.GetDefinition()).ReverseDirection;
        var mate=RmFeature(assembly,"遮阳板联动_右立柱偏置");
        result["right_mate"]=((IDistanceMateFeatureData)mate.GetDefinition()).FlipDimension;
        return result;
    }
    static void CanopyPunchSpan(double width,List<object> log)
    {
        var frame=Doc("立柱连接架.SLDPRT");
        Set(frame,"D3@草图9",(width-150)/2,log);
        Set(frame,"D3@阵列(线性)1",(width-150)/4,log);
        Set(frame,"D1@草图10",(width-230)/2,log);
        Set(frame,"D3@阵列(线性)2",(width-230)/2,log);
    }
    static void PrepareCanopySpan(ModelDoc2 assembly,double width,Dictionary<string,bool> original,List<object> log)
    {
        RmActivate(assembly,"canopy_span_equation_controls");
        var eq=assembly.GetEquationMgr() as EquationMgr;
        foreach(int i in new[]{1,2,3,4,5}) {
            string text=eq.Equation[i];if(!text.Contains("abs("))eq.Equation[i]=text.Replace("(840mm - \"遮阳板宽度\")","abs(840mm - \"遮阳板宽度\")");
            Need(eq.Equation[i].Contains("abs("),"Canopy signed-distance equation was not accepted: "+i);
        }
        if(width<950)CanopyPunchSpan(width,log);
        foreach(var pair in original.Where(p=>p.Key!="right_mate")) {
            string[] keys=pair.Key.Split('|');var doc=Doc(keys[0]);var feature=RmFeature(doc,keys[1]);var data=(IMoveFaceFeatureData)feature.GetDefinition();
            RmActivate(doc,"canopy_direction/"+keys[1]);
            bool desired=width>1000?!pair.Value:pair.Value;bool modified=false;
            if(data.ReverseDirection!=desired){
                Need(data.AccessSelections(doc,null),"Cannot access canopy end direction");
                try{data.ReverseDirection=desired;modified=feature.ModifyDefinition(data,doc,null);}
                finally{if(!modified)data.ReleaseSelectionAccess();Need(doc.FeatureManager.EditRollback(1,""),"Cannot restore canopy feature history");}
            }
            Need(((IMoveFaceFeatureData)feature.GetDefinition()).ReverseDirection==desired,"Canopy direction did not update");
            data=(IMoveFaceFeatureData)feature.GetDefinition();log.Add(new{role="canopy_end_direction",document=keys[0],feature=keys[1],reverse=desired,modify_return=modified,translation=data.TriadTranslationParameters,distance=data.Distance});
        }
        var mate=RmFeature(assembly,"遮阳板联动_右立柱偏置");var definition=(IDistanceMateFeatureData)mate.GetDefinition();
        bool flip=width>1000?!original["right_mate"]:original["right_mate"];bool mateModified=false;
        if(definition.FlipDimension!=flip){
            RmActivate(assembly,"canopy_right_mate_direction");assembly.ClearSelection2(true);Need(mate.Select2(false,0),"Cannot select right column mate");
            var equations=assembly.GetEquationMgr() as EquationMgr;var indices=Enumerable.Range(0,equations.GetCount()).Where(i=>equations.Equation[i].Contains("D1@遮阳板联动_右立柱偏置")).ToArray();Need(indices.Length==1,"Unique right column equation required");
            string expression=equations.Equation[indices[0]];int oldCount=equations.GetCount();int deletion=equations.Delete(indices[0]);Need(equations.GetCount()==oldCount-1,"Cannot release right column equation: "+deletion);
            Need(assembly.Extension.DeleteSelection2(0),"Cannot replace right column locating mate");assembly.ClearSelection2(true);
            var component=((Array)((AssemblyDoc)assembly).GetComponents(true)).Cast<Component2>().Single(c=>c.Name2=="右立柱-1");
            string plane="右视@"+component.Name2+"@"+Path.GetFileNameWithoutExtension(assembly.GetPathName());
            Need(assembly.Extension.SelectByID2(plane,"PLANE",0,0,0,false,1,null,0),"Cannot select right column datum");Need(Features(assembly).Single(f=>f.Name=="右视"&&f.GetTypeName2()=="RefPlane").Select2(true,1),"Cannot select canopy datum");
            double distance=Math.Abs(1000-width)/2000;int error=0;var created=((AssemblyDoc)assembly).AddMate5(5,2,flip,distance,distance,distance,0,0,0,0,0,false,false,0,out error);
            Need(created!=null&&error==1,"Cannot create directed right column mate: "+error);assembly.EditRebuild3();
            var dimension=(Dimension)((DisplayDimension)created.DisplayDimension).GetDimension2(0);mate=RmFeature(assembly,dimension.GetNameForSelection().Split('@')[1]);mate.Name="遮阳板联动_右立柱偏置";
            Need(equations.Add2(-1,expression,true)>=0,"Cannot restore right column equation");mateModified=true;assembly.ClearSelection2(true);
        }
        Need(((IDistanceMateFeatureData)mate.GetDefinition()).FlipDimension==flip,"Right column offset direction did not update");
        log.Add(new{role="right_column_direction",flip=flip,modify_return=mateModified});
    }
    static void FinishCanopySpan(ModelDoc2 assembly,double width,List<object> log)
    {
        var frame=Doc("立柱连接架.SLDPRT");RmActivate(frame,"canopy_punch_finish");CanopyPunchSpan(width,log);bool rebuilt=frame.ForceRebuild3(false);
        log.Add(new{role="canopy_final_span",rebuild=rebuilt,frame=RmBox(frame),health=LayoutFeatureHealth(frame)});Flush("canopy_final_span");Need(rebuilt,"Linked connector punches failed");
    }
}
