using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace Winnsen.StructureAgent.Generation
{
    internal static partial class NativeParametricAssembly
    {
        private static void RequireProductionProfile()
        {
            ParametricContext.Require(ParametricContext.Text(ParametricContext.Object(ParametricContext.Object(ParametricContext.Plan,"contract"),"input"),"productionProfile")==ProductionProcessVersion,"PRODUCTION_PROFILE_REQUIRED");
        }
        private static Dictionary<string,string> ProductionInventory(string folder)
        {
            return Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Where(IsPackageCad).ToDictionary(Path.GetFullPath,ParametricContext.Hash,StringComparer.OrdinalIgnoreCase);
        }
        private static void RequireProductionInventory(Dictionary<string,string> before,string error)
        {
            foreach(var pair in before) ParametricContext.Require(File.Exists(pair.Key)&&ParametricContext.Hash(pair.Key)==pair.Value,error+" "+pair.Key);
        }
        private static List<Component2> ProductionComponents(ModelDoc2 root)
        {
            var components=((Array)((AssemblyDoc)root).GetComponents(false)).Cast<Component2>().Where(c=>!c.IsSuppressed()).ToList();
            ParametricContext.Require(components.Count>0,"PRODUCTION_ROOT_COMPONENTS_MISSING");
            foreach(Component2 component in components)
            {
                string path=Path.GetFullPath(component.GetPathName());
                ParametricContext.CheckPath(path,ParametricContext.CadDirectory);
                ParametricContext.Require(Path.GetDirectoryName(path).Equals(ParametricContext.CadDirectory,StringComparison.OrdinalIgnoreCase)&&component.GetModelDoc2()!=null,"PRODUCTION_COMPONENT_NOT_LOCAL "+path);
            }
            return components;
        }
        private static Dictionary<string,double[]> ProductionTransforms(List<Component2> components)
        {
            return components.ToDictionary(c=>c.Name2,c=>(double[])c.GetTotalTransform(true).ArrayData,StringComparer.Ordinal);
        }
        private static void RequireProductionTransforms(ModelDoc2 root,Dictionary<string,double[]> before)
        {
            var after=ProductionTransforms(ProductionComponents(root));
            ParametricContext.Require(before.Count==after.Count&&before.All(p=>after.ContainsKey(p.Key)&&p.Value.Zip(after[p.Key],(a,b)=>Math.Abs(a-b)<1e-9).All(x=>x)),"PRODUCTION_COMPONENT_MOVED");
        }
        private static void ProductionActivate(ISldWorks sw,ModelDoc2 model)
        {
            int errors=0;
            ModelDoc2 active=sw.ActivateDoc3(model.GetTitle(),false,(int)swRebuildOnActivation_e.swDontRebuildActiveDoc,ref errors) as ModelDoc2;
            ParametricContext.Require(active!=null&&errors==0&&active.GetPathName().Equals(model.GetPathName(),StringComparison.OrdinalIgnoreCase),"PRODUCTION_DOCUMENT_ACTIVATION_FAILED");
            model.ClearSelection2(true);
        }
        private static double[] ProductionPoint(ISldWorks sw,double[] point,MathTransform transform)
        {
            MathUtility math=(MathUtility)sw.GetMathUtility();
            MathPoint source=(MathPoint)math.CreatePoint(point);
            MathPoint target=(MathPoint)source.MultiplyTransform(transform);
            try { return (double[])target.ArrayData; }
            finally { Marshal.ReleaseComObject(target);Marshal.ReleaseComObject(source);Marshal.ReleaseComObject(math); }
        }
        private static double ProductionVolume(ModelDoc2 model)
        {
            var bodies=((Array)((PartDoc)model).GetBodies2(0,false)).Cast<Body2>().ToArray();
            ParametricContext.Require(bodies.Length==1,"PRODUCTION_REQUIRES_ONE_SOLID");
            return ((double[])bodies[0].GetMassProperties(1))[3];
        }
        private static void ProductionPartHealth(ModelDoc2 model)
        {
            ParametricContext.Require(model.ForceRebuild3(false),"PRODUCTION_PART_REBUILD_FAILED "+model.GetPathName());
            var features=PhysicalFeatures(model);
            ParametricContext.Require(features.Any(f=>f.GetTypeName2()=="FlatPattern"),"PRODUCTION_FLAT_PATTERN_MISSING");
            ParametricContext.Require(!features.Any(f=>!f.IsSuppressed()&&f.GetErrorCode()>0),"PRODUCTION_FEATURE_ERROR "+model.GetPathName());
            var bodies=((Array)((PartDoc)model).GetBodies2(0,false)).Cast<Body2>().ToArray();
            ParametricContext.Require(bodies.Length==1&&bodies[0].IsSheetMetal()&&bodies[0].Check2()==0,"PRODUCTION_BODY_HEALTH_FAILED "+model.GetPathName());
        }
        private static List<Face2> ProductionCylinders(ModelDoc2 model,double radius,int axis)
        {
            return ProductionFaces(model).Where(f=>{Surface s=(Surface)f.GetSurface();if(!s.IsCylinder())return false;double[] c=(double[])s.CylinderParams;return Math.Abs(c[6]-radius)<1e-8&&Math.Abs(Math.Abs(c[axis+3])-1)<1e-7;}).ToList();
        }
        private static List<Face2> ProductionLocatorHoles(ModelDoc2 model,int axis)
        {
            return ProductionCylinders(model,.001,axis).Where(face=>{
                double[] range=ProductionFaceAxisLimits(face,axis);
                return Math.Abs(range[1]-range[0]-.0008)<1e-8&&Math.Abs(face.GetArea()-2*Math.PI*.001*.0008)<1e-11;
            }).ToList();
        }
        private static Face2 ProductionMaterialFace(ModelDoc2 model,double[] point,int normalAxis)
        {
            var found=ProductionFaces(model).Where(f=>{
                if(!((Surface)f.GetSurface()).IsPlane())return false;
                double[] n=(double[])f.Normal;if(Math.Abs(n[normalAxis])<.999999)return false;
                double[] q=(double[])f.GetClosestPointOn(point[0],point[1],point[2]);
                return q!=null&&Enumerable.Range(0,3).Sum(i=>(q[i]-point[i])*(q[i]-point[i]))<1e-16;
            }).ToArray();
            ParametricContext.Require(found.Length==1,"PRODUCTION_MATERIAL_FACE_AMBIGUOUS "+ParametricContext.Json.Serialize(point)+" count="+found.Length);
            return found[0];
        }
        private static Feature ProductionSketch(ISldWorks sw,ModelDoc2 model,Face2 face,string name,Action<Sketch> draw)
        {
            ProductionActivate(sw,model);
            var before=new HashSet<string>(PhysicalFeatures(model).Select(f=>f.Name));
            ParametricContext.Require(!before.Contains(name)&&((Entity)face).Select4(false,null),"PRODUCTION_SKETCH_FACE_REJECTED "+name);
            model.SketchManager.InsertSketch(true);
            Sketch sketch=model.GetActiveSketch2() as Sketch;
            ParametricContext.Require(sketch!=null,"PRODUCTION_SKETCH_NOT_ACTIVE");
            model.SketchManager.AddToDB=true;
            try { draw(sketch); }
            finally { model.SketchManager.AddToDB=false;model.ClearSelection2(true);model.SketchManager.InsertSketch(true); }
            Feature feature=PhysicalFeatures(model).Single(f=>!before.Contains(f.Name)&&f.GetSpecificFeature2() is Sketch);
            feature.Name=name;
            return feature;
        }
        private static void ProductionFixed(ModelDoc2 model,SketchSegment segment)
        {
            ParametricContext.Require(segment!=null,"PRODUCTION_SKETCH_SEGMENT_MISSING");
            model.ClearSelection2(true);
            ParametricContext.Require(segment.Select4(false,null),"PRODUCTION_SKETCH_SEGMENT_SELECT_FAILED");
            model.SketchAddConstraints("sgFIXED");
        }
        private static void ProductionLine(ISldWorks sw,ModelDoc2 model,Sketch sketch,double[] a,double[] b)
        {
            double[] p=ProductionPoint(sw,a,sketch.ModelToSketchTransform),q=ProductionPoint(sw,b,sketch.ModelToSketchTransform);
            ProductionFixed(model,model.SketchManager.CreateLine(p[0],p[1],0,q[0],q[1],0));
        }
        private static Feature ProductionCut(ModelDoc2 model,Feature sketch,string name,double depth,bool both)
        {
            model.ClearSelection2(true);ParametricContext.Require(sketch.Select2(false,0),"PRODUCTION_CUT_SKETCH_SELECT_FAILED");
            Feature cut=model.FeatureManager.FeatureCut3(!both,false,false,0,0,depth,depth,false,false,false,false,0,0,false,false,false,false,false,true,true,false,false,false,0,0,false);
            ParametricContext.Require(cut!=null,"PRODUCTION_NATIVE_CUT_FAILED "+name);cut.Name=name;
            model.ClearSelection2(true);ParametricContext.Require(model.ForceRebuild3(false),"PRODUCTION_CUT_REBUILD_FAILED "+name);
            return cut;
        }
        private static bool ProductionUpperPositive(List<Component2> instances)
        {
            bool? positive=null;
            foreach(Component2 component in instances)
            {
                double[] t=(double[])component.GetTotalTransform(true).ArrayData;
                ParametricContext.Require(Math.Abs(Math.Abs(t[4])-1)<1e-7,"PRODUCTION_DOOR_UP_AXIS_NOT_VERTICAL");
                bool sign=t[4]>0;
                ParametricContext.Require(!positive.HasValue||positive.Value==sign,"PRODUCTION_SHARED_DOOR_UP_CONFLICT");positive=sign;
            }
            return positive.Value;
        }
        private static void ApplyProductionDoor(ISldWorks sw,ModelDoc2 model,List<Component2> instances,List<object> rows)
        {
            string path=model.GetPathName();
            ParametricContext.Require(!model.IsOpenedReadOnly(),"PRODUCTION_PART_IS_READONLY "+path);
            var holes=ProductionCylinders(model,.0051,1).OrderBy(f=>ProductionFaceAxisLimits(f,1)[0]).ToArray();
            ParametricContext.Require(holes.Length==2,"PRODUCTION_PIN_SOURCE_COUNT");
            double[][] cylinders=holes.Select(f=>(double[])((Surface)f.GetSurface()).CylinderParams).ToArray();
            double[][] sourceRanges=holes.Select(f=>ProductionFaceAxisLimits(f,1)).ToArray();
            double nominal=cylinders[0][0],z=cylinders[0][2];
            ParametricContext.Require(Math.Abs(cylinders[1][0]-nominal)<1e-8&&Math.Abs(cylinders[1][2]-z)<1e-8&&Math.Abs(Math.Abs(nominal)*1000-(ParametricContext.DoorWidthMm/2-10))<.001,"PRODUCTION_PIN_NOMINAL_DRIFT");
            bool upperPositive=ProductionUpperPositive(instances);
            double[] ys={ProductionFaceAxisLimits(holes[0],1)[0],ProductionFaceAxisLimits(holes[1],1)[1]};
            var owners=holes.Select(f=>(Feature)f.GetFeature()).GroupBy(f=>f.Name).Select(g=>g.First()).ToArray();
            rows.Add(new{inspection="production_pin_source",path=path,owners=owners.Select(f=>new{name=f.Name,type=f.GetTypeName2(),underlyingType=f.GetTypeName()}).ToArray(),cylindersModelM=cylinders,axialRangesModelM=sourceRanges});
            ParametricContext.Require(owners.Length==1&&(owners[0].GetTypeName2()=="Cut"||(owners[0].GetTypeName2()=="ICE"&&owners[0].GetTypeName()=="Cut")),"PRODUCTION_PIN_COUPLED_CUT_CHANGED "+path);
            ProductionActivate(sw,model);
            ParametricContext.Require(owners[0].SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature,
                (int)swInConfigurationOpts_e.swAllConfiguration,null)&&model.ForceRebuild3(false),"PRODUCTION_PIN_ORIGINAL_SUPPRESS_FAILED");
            foreach(string configuration in ((Array)model.GetConfigurationNames()).Cast<object>().Select(Convert.ToString))
            {
                Array state=owners[0].IsSuppressed2((int)swInConfigurationOpts_e.swSpecifyConfiguration,new[]{configuration}) as Array;
                ParametricContext.Require(state!=null&&state.Length==1&&Convert.ToBoolean(state.GetValue(0)),"PRODUCTION_PIN_ORIGINAL_CONFIGURATION_NOT_SUPPRESSED "+configuration);
            }
            for(int index=0;index<2;index++)
            {
                bool upper=(index==1)==upperPositive;
                double x=nominal-(upper?Math.Sign(nominal)*.0005:0),y=ys[index];
                string name=upper?"生产门销_上端预偏0p5":"生产门销_下端原轴";
                Face2 face=ProductionMaterialFace(model,new[]{x,y,z},1);
                Feature sketch=ProductionSketch(sw,model,face,name+"_草图",s=>{double[] p=ProductionPoint(sw,new[]{x,y,z},s.ModelToSketchTransform);ProductionFixed(model,model.SketchManager.CreateCircleByRadius(p[0],p[1],0,.0051));});
                ProductionCut(model,sketch,name,.000801,false);
            }
            rows.Add(new{inspection="production_pin_change",path=path,nominalAxisXmm=nominal*1000,axisZmm=z*1000,sourceCylindersModelM=cylinders,sourceAxialRangesModelM=sourceRanges,upperPositiveLocalY=upperPositive,prebiasMm=.5,diameterMm=10.2,hardwareUnchanged=true,processBoundary="校形前加工形状；实际校形、装配力及生产效果未验证"});
            ApplyProductionNotch(sw,model,Math.Sign(nominal),rows);
            ApplyProductionLocatorRemoval(sw,model,0,2,rows);
            VerifyProductionDoor(model,instances,rows);
        }
        private static void ApplyProductionNotch(ISldWorks sw,ModelDoc2 model,int side,List<object> rows)
        {
            Face2 face=ProductionReturnFace(model,side);double[] box=ProductionFaceVertexBounds(face);
            double edge=side<0?box[3]:box[0],middle=(box[1]+box[4])/2,z=box[2],radius=.0015;
            double before=ProductionVolume(model);
            string name=side<0?"生产识别缺口_L_半圆_R1p5":"生产识别缺口_R_三角_3x1p5";
            Feature sketch=ProductionSketch(sw,model,face,name+"_草图",s=>{
                if(side<0) { double[] p=ProductionPoint(sw,new[]{edge,middle,z},s.ModelToSketchTransform);ProductionFixed(model,model.SketchManager.CreateCircleByRadius(p[0],p[1],0,radius)); }
                else { double[][] points={new[]{edge-side*.0005,middle-.002,z},new[]{edge+side*.0015,middle,z},new[]{edge-side*.0005,middle+.002,z}};for(int i=0;i<3;i++)ProductionLine(sw,model,s,points[i],points[(i+1)%3]); }
            });
            ProductionCut(model,sketch,name,.001,true);
            double removed=before-ProductionVolume(model),expected=(side<0?Math.PI*radius*radius/2:radius*radius)*.0008;
            ParametricContext.Require(Math.Abs(removed-expected)<1e-11,"PRODUCTION_NOTCH_VOLUME_MISMATCH "+removed+" expected "+expected);
            rows.Add(new{inspection="production_notch_change",path=model.GetPathName(),feature=name,side=side<0?"L":"R",freeEdgeXmm=edge*1000,midpointYmm=middle*1000,surfaceZmm=z*1000,removedVolumeMm3=removed*1e9,expectedVolumeMm3=expected*1e9,radiusMm=1.5,openingMm=3.0,depthMm=1.5});
        }
        private static List<double[]> ProductionFaceVertices(Face2 face)
        {
            var points=new List<double[]>();
            foreach(Edge edge in face.GetEdges() as Array ?? new object[0])
                foreach(Vertex vertex in new[]{edge.GetStartVertex() as Vertex,edge.GetEndVertex() as Vertex})
                {
                    if(vertex==null)continue;
                    double[] point=(double[])vertex.GetPoint();
                    if(!points.Any(p=>p.Zip(point,(a,b)=>Math.Abs(a-b)<1e-8).All(x=>x)))points.Add(point);
                }
            return points;
        }
        private static double[] ProductionFaceAxisLimits(Face2 face,int axis)
        {
            var coordinates=new List<double>();
            foreach(Edge edge in face.GetEdges() as Array ?? new object[0])
            {
                Curve curve=edge.GetCurve() as Curve;double[] parameters=edge.GetCurveParams2() as double[];
                ParametricContext.Require(curve!=null&&parameters!=null&&parameters.Length>=8,"PRODUCTION_EXACT_EDGE_UNAVAILABLE");
                foreach(double parameter in new[]{parameters[6],parameters[7]})
                {
                    double[] point=curve.Evaluate2(parameter,0) as double[];
                    ParametricContext.Require(point!=null&&point.Length>=3,"PRODUCTION_EXACT_EDGE_POINT_UNAVAILABLE");
                    coordinates.Add(point[axis]);
                }
            }
            ParametricContext.Require(coordinates.Count>0,"PRODUCTION_EXACT_FACE_AXIS_UNAVAILABLE");
            return new[]{coordinates.Min(),coordinates.Max()};
        }
        private static double[] ProductionFaceVertexBounds(Face2 face)
        {
            var points=ProductionFaceVertices(face);
            ParametricContext.Require(points.Count>=4,"PRODUCTION_FACE_EXACT_VERTICES_MISSING");
            return Enumerable.Range(0,3).Select(i=>points.Min(p=>p[i])).Concat(Enumerable.Range(0,3).Select(i=>points.Max(p=>p[i]))).ToArray();
        }
        private static Face2 ProductionReturnFace(ModelDoc2 model,int side)
        {
            var candidates=ProductionFaces(model).Where(f=>{
                if(!((Surface)f.GetSurface()).IsPlane())return false;
                double[] n=(double[])f.Normal;
                if(n[2]>-.999999||f.GetArea()<=.00002)return false;
                double[] b=ProductionFaceVertexBounds(f);
                return b[5]<-.01&&Math.Sign((b[0]+b[3])/2)==side&&b[1]<0&&b[4]>0&&Math.Abs((b[3]-b[0])-.005)<1e-7;
            }).ToArray();
            ParametricContext.Require(candidates.Length==1,"PRODUCTION_NOTCH_RETURN_FACE_AMBIGUOUS count="+candidates.Length);
            return candidates[0];
        }
        private static object VerifyProductionNotch(ModelDoc2 model,int side,string name)
        {
            Face2 material=ProductionReturnFace(model,side);
            double[] bounds=ProductionFaceVertexBounds(material);
            double edge=side<0?bounds[3]:bounds[0],middle=(bounds[1]+bounds[4])/2,z=bounds[2],radius=.0015,thickness=.0008;
            Face2[] boundaries=ProductionFaces(model).Where(f=>{Feature owner=(Feature)f.GetFeature();return owner!=null&&owner.Name==name;}).ToArray();
            ParametricContext.Require(boundaries.Length>0,"PRODUCTION_NOTCH_HAS_NO_SOLID_BOUNDARY");
            var measured=new List<object>();
            if(side<0)
            {
                var cylinders=boundaries.Where(f=>((Surface)f.GetSurface()).IsCylinder()).ToArray();
                ParametricContext.Require(cylinders.Length==1,"PRODUCTION_NOTCH_ARC_COUNT_INVALID");
                Face2 face=cylinders[0];double[] c=(double[])((Surface)face.GetSurface()).CylinderParams;
                ParametricContext.Require(Math.Abs(c[0]-edge)<1e-8&&Math.Abs(c[1]-middle)<1e-8&&Math.Abs(Math.Abs(c[5])-1)<1e-8&&Math.Abs(c[6]-radius)<1e-8,"PRODUCTION_NOTCH_RADIUS_OR_CENTER_INVALID");
                ParametricContext.Require(Math.Abs(face.GetArea()-Math.PI*radius*thickness)<1e-10,"PRODUCTION_NOTCH_HALF_ARC_AREA_INVALID");
                double[] inside={edge+side*radius,middle,z+thickness/2};
                double[] closest=(double[])face.GetClosestPointOn(inside[0],inside[1],inside[2]);
                ParametricContext.Require(closest!=null&&closest.Take(3).Zip(inside,(a,b)=>Math.Abs(a-b)<1e-8).All(x=>x),"PRODUCTION_NOTCH_ARC_WRONG_SIDE");
                var points=ProductionFaceVertices(face);
                var expected=new List<double[]>();
                foreach(double plane in new[]{z,z+thickness})foreach(int end in new[]{-1,1})expected.Add(new[]{edge,middle+end*radius,plane});
                ParametricContext.Require(points.Count==4&&expected.All(p=>points.Any(q=>p.Zip(q,(a,b)=>Math.Abs(a-b)<1e-8).All(x=>x))),"PRODUCTION_NOTCH_ARC_ENDPOINTS_INVALID");
                measured.Add(new{kind="semicircle",axis=c,verticesModelM=points,areaM2=face.GetArea()});
            }
            else
            {
                var slopes=boundaries.Where(f=>{if(!((Surface)f.GetSurface()).IsPlane())return false;double[] n=(double[])f.Normal;return Math.Abs(n[2])<1e-8&&Math.Abs(Math.Abs(n[0])-Math.Sqrt(.5))<1e-7&&Math.Abs(Math.Abs(n[1])-Math.Sqrt(.5))<1e-7;}).ToArray();
                ParametricContext.Require(slopes.Length==2,"PRODUCTION_NOTCH_TRIANGLE_FACE_COUNT_INVALID");
                var points=new List<double[]>();
                foreach(Face2 face in slopes)
                {
                    var vertices=ProductionFaceVertices(face);
                    ParametricContext.Require(vertices.Count==4&&Math.Abs(face.GetArea()-Math.Sqrt(2)*radius*thickness)<1e-10,"PRODUCTION_NOTCH_TRIANGLE_SLOPE_INVALID");
                    foreach(double[] p in vertices)if(!points.Any(q=>p.Zip(q,(a,b)=>Math.Abs(a-b)<1e-8).All(x=>x)))points.Add(p);
                    measured.Add(new{kind="triangle_slope",normal=face.Normal,verticesModelM=vertices,areaM2=face.GetArea()});
                }
                var expected=new List<double[]>();
                foreach(double plane in new[]{z,z+thickness})
                {
                    expected.Add(new[]{edge,middle-radius,plane});expected.Add(new[]{edge+side*radius,middle,plane});expected.Add(new[]{edge,middle+radius,plane});
                }
                ParametricContext.Require(points.Count==6&&expected.All(p=>points.Any(q=>p.Zip(q,(a,b)=>Math.Abs(a-b)<1e-8).All(x=>x))),"PRODUCTION_NOTCH_TRIANGLE_OPENING_OR_DEPTH_INVALID");
            }
            return new{freeEdgeXmm=edge*1000,midpointYmm=middle*1000,surfaceZmm=z*1000,openingMm=3.0,depthMm=1.5,thicknessMm=.8,boundaries=measured};
        }
        private static void VerifyProductionDoor(ModelDoc2 model,List<Component2> instances,List<object> rows)
        {
            ProductionPartHealth(model);
            var holes=ProductionCylinders(model,.0051,1).OrderBy(f=>ProductionFaceAxisLimits(f,1)[0]).ToArray();
            ParametricContext.Require(holes.Length==2,"PRODUCTION_PIN_FINAL_COUNT");
            bool positive=ProductionUpperPositive(instances);double nominal=ParametricContext.DoorWidthMm/2-10;
            int side=Path.GetFileName(model.GetPathName()).StartsWith("door_panel_left_")?-1:1;
            for(int i=0;i<2;i++) { double[] c=(double[])((Surface)holes[i].GetSurface()).CylinderParams;double expected=side*(nominal-(((i==1)==positive)?.5:0));ParametricContext.Require(Math.Abs(c[0]*1000-expected)<.001,"PRODUCTION_PIN_PREBIAS_INVALID"); }
            string name=side<0?"生产识别缺口_L_半圆_R1p5":"生产识别缺口_R_三角_3x1p5";
            Feature notch=PhysicalFeatures(model).SingleOrDefault(f=>f.Name==name);
            ParametricContext.Require(notch!=null&&!notch.IsSuppressed()&&notch.GetErrorCode()==0,"PRODUCTION_NOTCH_MISSING");
            object notchGeometry=VerifyProductionNotch(model,side,name);
            ParametricContext.Require(ProductionLocatorHoles(model,2).Count==0,"PRODUCTION_DOOR_LOCATOR_HOLES_REMAIN");
            rows.Add(new{inspection="production_door_geometry",path=model.GetPathName(),pinCylinders=holes.Select(f=>new{axis=((Surface)f.GetSurface()).CylinderParams,box=f.GetBox()}).ToArray(),notch=name,notchGeometry=notchGeometry,locatorHoleCount=0,pass=true});
        }
        private static void VerifyProductionPinBaseline(Dictionary<string,object> change,List<Component2> components,List<object> rows)
        {
            string path=ParametricContext.Text(change,"path");
            var instances=components.Where(c=>c.GetPathName().Equals(path,StringComparison.OrdinalIgnoreCase)).ToList();
            ModelDoc2 model=(ModelDoc2)instances.First().GetModelDoc2();
            var holes=ProductionCylinders(model,.0051,1).OrderBy(f=>ProductionFaceAxisLimits(f,1)[0]).ToArray();
            var source=((IEnumerable)change["sourceCylindersModelM"]).Cast<IEnumerable>().Select(a=>a.Cast<object>().Select(Convert.ToDouble).ToArray()).ToArray();
            var ranges=((IEnumerable)change["sourceAxialRangesModelM"]).Cast<IEnumerable>().Select(a=>a.Cast<object>().Select(Convert.ToDouble).ToArray()).ToArray();
            bool positive=ProductionUpperPositive(instances);
            ParametricContext.Require(holes.Length==2&&source.Length==2&&ranges.Length==2&&Convert.ToBoolean(change["upperPositiveLocalY"])==positive,"PRODUCTION_PIN_SOURCE_BINDING_INVALID");
            for(int i=0;i<2;i++)
            {
                double[] current=(double[])((Surface)holes[i].GetSurface()).CylinderParams,extent=ProductionFaceAxisLimits(holes[i],1);
                ParametricContext.Require(source[i].Length==7&&ranges[i].Length==2,"PRODUCTION_PIN_SOURCE_GEOMETRY_INVALID");
                double x=source[i][0]-(((i==1)==positive)?Math.Sign(source[i][0])*.0005:0);
                ParametricContext.Require(Math.Abs(current[0]-x)<1e-8&&Math.Abs(current[2]-source[i][2])<1e-8&&Math.Abs(current[6]-source[i][6])<1e-8&&
                    Math.Abs(Math.Abs(current[3]*source[i][3]+current[4]*source[i][4]+current[5]*source[i][5])-1)<1e-8&&ranges[i].Zip(extent,(a,b)=>Math.Abs(a-b)<1e-8).All(v=>v),"PRODUCTION_PIN_AXIS_OR_AXIAL_RANGE_DRIFT");
            }
            rows.Add(new{inspection="production_pin_baseline_verification",path=path,signedAxisAndSourceZVerified=true,axialRangesUnchanged=true,upperPrebiasMm=.5,pass=true});
        }
        private static void ApplyProductionLocatorRemoval(ISldWorks sw,ModelDoc2 model,int expected,int axis,List<object> rows)
        {
            ProductionActivate(sw,model);
            var holes=ProductionLocatorHoles(model,axis);
            ParametricContext.Require(holes.Count==expected,"PRODUCTION_LOCATOR_SOURCE_COUNT "+model.GetPathName()+" "+holes.Count);
            if(expected==0) { rows.Add(new{inspection="production_locator_absence",path=model.GetPathName(),measuredHoleCount=0,axis=axis,nonHoleCylinderCount=ProductionCylinders(model,.001,axis).Count});return; }
            var centers=holes.Select(f=>{double[] c=(double[])((Surface)f.GetSurface()).CylinderParams,b=ProductionFaceAxisLimits(f,axis);double[] p={c[0],c[1],c[2]};p[axis]=b[1];ParametricContext.Require(Math.Abs((b[1]-b[0])-.0008)<1e-7,"PRODUCTION_LOCATOR_THICKNESS_DRIFT");return p;}).ToArray();
            double volume=ProductionVolume(model);
            for(int i=0;i<holes.Count;i++)ParametricContext.Require(((Entity)holes[i]).Select4(i>0,null),"PRODUCTION_LOCATOR_SELECT_FAILED");
            var beforeFeatures=new HashSet<string>(PhysicalFeatures(model).Select(f=>f.Name));
            ParametricContext.Require(model.InsertDeleteFace2((int)swFaceDeleteOption_e.swFaceDelete_Patch),"PRODUCTION_LOCATOR_HEAL_FAILED");
            Feature healed=PhysicalFeatures(model).Single(f=>!beforeFeatures.Contains(f.Name)&&f.GetDefinition() is DeleteFaceFeatureData);healed.Name="生产_取消已验证定位孔";
            ParametricContext.Require(model.ForceRebuild3(false)&&ProductionLocatorHoles(model,axis).Count==0,"PRODUCTION_LOCATOR_HEAL_NOT_COMPLETE");
            double delta=ProductionVolume(model)-volume;
            ParametricContext.Require(Math.Abs(delta-expected*Math.PI*.001*.001*.0008)<1e-11,"PRODUCTION_LOCATOR_VOLUME_MISMATCH");
            rows.Add(new{inspection="production_locator_change",path=model.GetPathName(),axis=axis,removedHoleCount=expected,diameterMm=2.0,centersModelM=centers,addedVolumeMm3=delta*1e9,feature=healed.Name,replacement="actual_weld_contact_short_lines",strategy="direct_heal",sourcePath="",sourceFeature="",configuration=model.ConfigurationManager.ActiveConfiguration.Name,volumeBeforeM3=volume,volumeAfterM3=ProductionVolume(model),holeCountBefore=expected,holeCountAfter=0});
        }
        private static Dictionary<string,object> CaptureProductionShelfPair(ISldWorks sw,Dictionary<string,ModelDoc2> documents,List<Component2> components,List<object> rows)
        {
            string leftPath=Find("箱体横层板L.sldprt"),rightPath=Find("箱体横层板R.SLDPRT");
            ModelDoc2 left=documents[leftPath],right=documents[rightPath];
            var centers=new List<double[][]>();var volumes=new List<double>();var configurations=new List<string>();
            foreach(ModelDoc2 model in new[]{left,right})
            {
                ProductionActivate(sw,model);ProductionPartHealth(model);
                string configuration=model.ConfigurationManager.ActiveConfiguration.Name;
                ParametricContext.Require(components.Where(c=>c.GetPathName().Equals(model.GetPathName(),StringComparison.OrdinalIgnoreCase)).All(c=>c.ReferencedConfiguration==configuration),"PRODUCTION_SHELF_CONFIGURATION_CONFLICT");
                var holes=ProductionLocatorHoles(model,1);
                ParametricContext.Require(holes.Count==8,"PRODUCTION_SHELF_PAIR_SOURCE_COUNT "+model.GetPathName()+" "+holes.Count);
                centers.Add(holes.Select(f=>{double[] c=(double[])((Surface)f.GetSurface()).CylinderParams,b=ProductionFaceAxisLimits(f,1);return new[]{c[0],b[1],c[2]};}).ToArray());
                volumes.Add(ProductionVolume(model));configurations.Add(configuration);
            }
            Func<double[],double[],bool> mirrored=(a,b)=>Math.Abs(a[0]+b[0])<1e-8&&Math.Abs(a[1]-b[1])<1e-8&&Math.Abs(a[2]-b[2])<1e-8;
            ParametricContext.Require(centers[0].All(p=>centers[1].Count(q=>mirrored(p,q))==1)&&centers[1].All(p=>centers[0].Count(q=>mirrored(p,q))==1)&&Math.Abs(volumes[0]-volumes[1])<1e-11,"PRODUCTION_SHELF_PAIR_NOT_MIRRORED");
            object link=NativeParametricWidth.ProductionShelfMirrorLink(right,leftPath,configurations[0]);
            object row=new{inspection="production_locator_pair_source",leftPath=leftPath,rightPath=rightPath,leftConfiguration=configurations[0],rightConfiguration=configurations[1],leftVolumeBeforeM3=volumes[0],rightVolumeBeforeM3=volumes[1],leftCentersModelM=centers[0],rightCentersModelM=centers[1],mirrorLink=link};
            rows.Add(row);
            return (Dictionary<string,object>)ParametricContext.Json.DeserializeObject(ParametricContext.Json.Serialize(row));
        }
        private static void ApplyProductionShelfPair(ISldWorks sw,Dictionary<string,ModelDoc2> documents,Dictionary<string,object> baseline,List<object> rows)
        {
            string leftPath=ParametricContext.Text(baseline,"leftPath"),rightPath=ParametricContext.Text(baseline,"rightPath");
            ModelDoc2 left=documents[leftPath],right=documents[rightPath];
            ApplyProductionLocatorRemoval(sw,left,8,1,rows);
            ProductionActivate(sw,right);ProductionPartHealth(right);
            ParametricContext.Require(ProductionLocatorHoles(right,1).Count==0,"PRODUCTION_SHELF_MIRROR_LOCATORS_REMAIN");
            double before=ParametricContext.Number(baseline,"rightVolumeBeforeM3"),after=ProductionVolume(right),expected=8*Math.PI*.001*.001*.0008;
            ParametricContext.Require(Math.Abs(after-before-expected)<1e-11&&Math.Abs(after-ProductionVolume(left))<1e-11,"PRODUCTION_SHELF_MIRROR_VOLUME_MISMATCH");
            object link=NativeParametricWidth.ProductionShelfMirrorLink(right,leftPath,ParametricContext.Text(baseline,"leftConfiguration"));
            var details=(Dictionary<string,object>)ParametricContext.Json.DeserializeObject(ParametricContext.Json.Serialize(link));
            rows.Add(new{inspection="production_locator_change",path=rightPath,axis=1,removedHoleCount=8,diameterMm=2.0,centersModelM=baseline["rightCentersModelM"],addedVolumeMm3=(after-before)*1e9,feature=ParametricContext.Text(details,"feature"),replacement="actual_weld_contact_short_lines",strategy="mirrored_source_heal",sourcePath=leftPath,sourceFeature="生产_取消已验证定位孔",configuration=right.ConfigurationManager.ActiveConfiguration.Name,volumeBeforeM3=before,volumeAfterM3=after,holeCountBefore=8,holeCountAfter=0,mirrorLink=link});
        }
        private static void VerifyProductionLocatorChange(ISldWorks sw,Dictionary<string,object> change,List<Component2> components,List<object> rows)
        {
            string path=ParametricContext.Text(change,"path");ModelDoc2 model=(ModelDoc2)components.First(c=>c.GetPathName().Equals(path,StringComparison.OrdinalIgnoreCase)).GetModelDoc2();
            int axis=Convert.ToInt32(change["axis"]),count=Convert.ToInt32(change["removedHoleCount"]);
            ProductionActivate(sw,model);ProductionPartHealth(model);
            ParametricContext.Require(ProductionLocatorHoles(model,axis).Count==0,"PRODUCTION_LOCATOR_VERIFY_HOLES_REMAIN");
            string strategy=ParametricContext.Text(change,"strategy"),sourcePath=ParametricContext.Text(change,"sourcePath"),sourceFeature=ParametricContext.Text(change,"sourceFeature");object link=null;
            ModelDoc2 source=model;
            if(strategy=="mirrored_source_heal")
            {
                ParametricContext.Require(path.Equals(Find("箱体横层板R.SLDPRT"),StringComparison.OrdinalIgnoreCase)&&sourcePath.Equals(Find("箱体横层板L.sldprt"),StringComparison.OrdinalIgnoreCase)&&sourceFeature=="生产_取消已验证定位孔","PRODUCTION_LOCATOR_MIRROR_SOURCE_DRIFT");
                source=(ModelDoc2)components.First(c=>c.GetPathName().Equals(sourcePath,StringComparison.OrdinalIgnoreCase)).GetModelDoc2();
                link=NativeParametricWidth.ProductionShelfMirrorLink(model,sourcePath,source.ConfigurationManager.ActiveConfiguration.Name);
                ParametricContext.Require(ProductionLocatorHoles(source,axis).Count==0,"PRODUCTION_LOCATOR_MIRROR_SOURCE_HOLES_REMAIN");
            }
            else ParametricContext.Require(strategy=="direct_heal"&&sourcePath==""&&sourceFeature=="","PRODUCTION_LOCATOR_STRATEGY_INVALID");
            Feature healed=PhysicalFeatures(source).Single(f=>f.Name==(strategy=="direct_heal"?ParametricContext.Text(change,"feature"):sourceFeature));
            ParametricContext.Require(!healed.IsSuppressed()&&healed.GetDefinition() is DeleteFaceFeatureData&&healed.GetErrorCode()==0,"PRODUCTION_LOCATOR_HEAL_INVALID");
            ParametricContext.Require(!PhysicalFeatures(model).Any(f=>f.Name.Contains("小十字")),"PRODUCTION_OBSOLETE_LOCATOR_CROSS_REMAINS");
            ParametricContext.Require(count==8&&ParametricContext.Text(change,"replacement")=="actual_weld_contact_short_lines","PRODUCTION_LOCATOR_REPLACEMENT_INVALID");
            double volume=ProductionVolume(model);
            ParametricContext.Require(Math.Abs(volume-ParametricContext.Number(change,"volumeAfterM3"))<1e-11&&Math.Abs(volume-ParametricContext.Number(change,"volumeBeforeM3")-count*Math.PI*.001*.001*.0008)<1e-11&&model.ConfigurationManager.ActiveConfiguration.Name==ParametricContext.Text(change,"configuration")&&components.Where(c=>c.GetPathName().Equals(path,StringComparison.OrdinalIgnoreCase)).All(c=>c.ReferencedConfiguration==model.ConfigurationManager.ActiveConfiguration.Name),"PRODUCTION_LOCATOR_SAVED_GEOMETRY_DRIFT");
            rows.Add(new{inspection="production_locator_verification",path=path,holeCount=0,removedHoleCount=count,axis=axis,diameterMm=2.0,strategy=strategy,sourcePath=sourcePath,sourceFeature=sourceFeature,feature=ParametricContext.Text(change,"feature"),configuration=model.ConfigurationManager.ActiveConfiguration.Name,volumeM3=volume,sha256=ParametricContext.Hash(path),mirrorLink=link,replacement="actual_weld_contact_short_lines",pass=true});
        }
        private static void LocalizeProductionDoorPack(ISldWorks sw,List<object> rows)
        {
            RequireProductionProfile();
            string doorRoot=Path.Combine(ParametricContext.AttemptDirectory,"doors");var before=ProductionInventory(doorRoot);
            sw.CloseAllDocuments(true);
            var sources=before.Keys.GroupBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).ToArray();
            var copied=new List<Tuple<string,string,string>>();
            foreach(var group in sources)
            {
                ParametricContext.Require(group.Count()==1,"PRODUCTION_LOCALIZATION_NAME_COLLISION "+group.Key);
                string source=group.First(),target=Path.Combine(ParametricContext.CadDirectory,group.Key);
                ParametricContext.AssertMutation(target);ParametricContext.Require(!File.Exists(target),"PRODUCTION_LOCALIZATION_TARGET_EXISTS "+target);
                File.Copy(source,target,false);copied.Add(Tuple.Create(source,target,before[source]));
            }
            var files=copied.Select(t=>t.Item2).Concat(new[]{Find("标准寄存柜1917×1000×550(总装配).SLDASM")}).ToArray();
            var local=Directory.GetFiles(ParametricContext.CadDirectory).Where(IsPackageCad).ToDictionary(Path.GetFileName,p=>p,StringComparer.OrdinalIgnoreCase);
            foreach(string file in files)
            {
                Array dependencies=sw.GetDocumentDependencies2(file,false,false,false) as Array;if(dependencies==null)continue;
                for(int i=1;i<dependencies.Length;i+=2)
                {
                    string old=Convert.ToString(dependencies.GetValue(i)),target;
                    ParametricContext.Require(local.TryGetValue(Path.GetFileName(old),out target),"PRODUCTION_LOCAL_REFERENCE_MISSING "+old);
                    if(old.Equals(target,StringComparison.OrdinalIgnoreCase))continue;
                    ParametricContext.Require(sw.ReplaceReferencedDocument(file,old,target),"PRODUCTION_LOCAL_REFERENCE_REPLACE_FAILED "+file);
                    rows.Add(new{inspection="production_localized_reference",path=file,sourcePath=old,targetPath=target,productionProfile=ProductionProcessVersion});
                }
            }
            foreach(var copy in copied)rows.Add(new{inspection="production_localized_file",file=Path.GetFileName(copy.Item2),sourcePath=copy.Item1,path=copy.Item2,sourceSha256=copy.Item3,sha256=ParametricContext.Hash(copy.Item2),productionProfile=ProductionProcessVersion});
            RequireProductionInventory(before,"PRODUCTION_LOCALIZATION_CHANGED_DOOR_SOURCE");
        }
        private static void WriteProductionRevisions(Dictionary<string,string> before,Dictionary<string,ModelDoc2> documents,List<object> rows)
        {
            var after=ProductionInventory(ParametricContext.CadDirectory);
            ParametricContext.Require(before.Count==after.Count&&before.Keys.All(after.ContainsKey),"PRODUCTION_CHANGED_FILE_INVENTORY");
            foreach(var item in after.Where(p=>before[p.Key]!=p.Value))
            {
                ParametricContext.Require(Path.GetExtension(item.Key).Equals(".SLDPRT",StringComparison.OrdinalIgnoreCase)&&documents.ContainsKey(item.Key),"PRODUCTION_UNEXPECTED_CHANGED_DOCUMENT "+item.Key);
                ModelDoc2 model=documents[item.Key];var features=PhysicalFeatures(model);string role=PhysicalPartRole(Path.GetFileName(item.Key)),oldDoor="",doorEvidence="";
                foreach(string proof in VerifiedDoorProofPaths())
                {
                    var result=ParametricContext.Object(ParametricContext.Read(proof),"result");
                    foreach(Dictionary<string,object> edit in (IEnumerable)result["part_edits"])
                        if(Path.GetFileName(ParametricContext.Text(edit,"path")).Equals(Path.GetFileName(item.Key),StringComparison.OrdinalIgnoreCase)) { ParametricContext.Require(oldDoor=="","PRODUCTION_DOOR_PROOF_DUPLICATE");oldDoor=ParametricContext.Text(edit,"sha256_after_save");doorEvidence=ParametricContext.Hash(proof); }
                }
                bool sheet=((Array)((PartDoc)model).GetBodies2(0,false)).Cast<Body2>().All(b=>b.IsSheetMetal());
                rows.Add(new{inspection="production_part_revision",file=Path.GetFileName(item.Key),path=item.Key,role=role,oldSha256=before[item.Key],newProductionSha256=item.Value,oldDoorSha256=oldDoor,doorEvidenceSha256=doorEvidence,hasSheetMetal=sheet,hasFlatPattern=features.Any(f=>f.GetTypeName2()=="FlatPattern"),productionProfile=ProductionProcessVersion});
            }
        }
    }
}
