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
        private static Feature OrderFeature(ModelDoc2 model, string name)
        {
            for (Feature feature = model.FirstFeature() as Feature; feature != null; feature = feature.GetNextFeature() as Feature)
                if (feature.Name == name) return feature;
            throw new InvalidOperationException("ORDER_INTERFACE_FEATURE_MISSING " + name);
        }

        private static double[][] OrderSketchWorld(ISldWorks sw, Sketch sketch)
        {
            MathUtility math = (MathUtility)sw.GetMathUtility();
            MathTransform inverse = (MathTransform)sketch.ModelToSketchTransform.Inverse();
            return ((Array)sketch.GetSketchPoints2()).Cast<SketchPoint>().Select(point =>
                ((double[])((MathPoint)((MathPoint)math.CreatePoint(new[] { point.X, point.Y, point.Z })).MultiplyTransform(inverse)).ArrayData)
                .Select(value => value * 1000).ToArray()).ToArray();
        }

        private static double[] OrderShelfCenters()
        {
            var boundaries = ParametricContext.Object(ParametricContext.Object(ParametricContext.Plan, "contract"), "boundaries");
            var left = ((IEnumerable)boundaries["L"]).Cast<Dictionary<string, object>>()
                .Select(row => ParametricContext.Number(row, "shelfCenterYmm")).OrderBy(value => value).ToArray();
            var right = ((IEnumerable)boundaries["R"]).Cast<Dictionary<string, object>>()
                .Select(row => ParametricContext.Number(row, "shelfCenterYmm")).OrderBy(value => value).ToArray();
            ParametricContext.Require(left.Length == 11 && right.Length == 12 &&
                left.All(value => right.Count(other => Math.Abs(other - value) < .001) == 1) &&
                Enumerable.Range(1, right.Length - 1).All(index => Math.Abs(right[index] - right[index - 1] - 137) < .001) &&
                Math.Abs(right[0] - 213.5) < .001 && Math.Abs(right[11] - 1720.5) < .001,
                "ORDER_SIDE_RIB_BOUNDARY_GRID_CHANGED");
            return right;
        }

        private static double[] OrderFormingCenters(ModelDoc2 model)
        {
            var values = new List<double>();
            foreach (Body2 body in (Array)((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false))
                foreach (Face2 face in (Array)body.GetFaces())
                {
                    Feature owner = face.GetFeature() as Feature;
                    if (owner == null || (owner.Name != "拱桥成型模具1" && owner.Name != "阵列(线性)1")) continue;
                    double[] box = (double[])face.GetBox();
                    if (Math.Abs((box[4] - box[1]) * 1000 - 4) < .01)
                    {
                        double center = box[4] * 1000;
                        if (!values.Any(value => Math.Abs(value - center) < .01)) values.Add(center);
                    }
                }
            return values.OrderBy(value => value).ToArray();
        }

        private static void RepairOrderSideRib(ISldWorks sw, List<object> rows)
        {
            string path = Find("箱体侧板加强筋1.sldprt");
            double[] centers = OrderShelfCenters();
            ModelDoc2 model = Open(sw, path, false);
            try
            {
                ProductionActivate(sw, model);
                ParametricContext.Require(!model.IsOpenedReadOnly(), "ORDER_SIDE_RIB_READONLY");
                Dimension pitch = model.Parameter("D3@阵列(线性)1") as Dimension;
                Dimension count = model.Parameter("D1@阵列(线性)1") as Dimension;
                ParametricContext.Require(pitch != null && count != null && pitch.DrivenState == 2 && count.DrivenState == 2 &&
                    Math.Abs(pitch.SystemValue * 1000 - 152.5) < .001 && Math.Abs(count.SystemValue - 11) < .001,
                    "ORDER_SIDE_RIB_SOURCE_PATTERN_CHANGED");
                var beforeBands = OrderFormingCenters(model);
                ParametricContext.Require(beforeBands.Length == 11, "ORDER_SIDE_RIB_SOURCE_FORMING_COUNT_CHANGED");
                Feature tool = OrderFeature(model, "拱桥成型模具1").GetFirstSubFeature() as Feature;
                while (tool != null && tool.Name != "草图8") tool = tool.GetNextSubFeature() as Feature;
                ParametricContext.Require(tool != null, "ORDER_SIDE_RIB_TOOL_SKETCH_MISSING");
                Sketch sketch = tool.GetSpecificFeature2() as Sketch;
                ParametricContext.Require(sketch != null, "ORDER_SIDE_RIB_TOOL_SKETCH_INVALID");
                double[][] beforePoints = OrderSketchWorld(sw, sketch);
                double currentTop = beforePoints.Max(point => point[1]);
                double targetTop = centers[centers.Length - 1] - 2;
                ParametricContext.Require(Math.Abs(currentTop - 1703) < .001 && Math.Abs(targetTop - 1718.5) < .001,
                    "ORDER_SIDE_RIB_TOOL_DATUM_CHANGED");
                Array references = sketch.RelationManager.GetRelations((int)swSketchRelationFilterType_e.swExternal) as Array;
                ParametricContext.Require(references != null && references.Length == 2, "ORDER_SIDE_RIB_EXTERNAL_RELATIONS_CHANGED");
                foreach (SketchRelation relation in references)
                    ParametricContext.Require(sketch.RelationManager.DeleteRelation(relation), "ORDER_SIDE_RIB_TOOL_DETACH_FAILED");
                pitch.SystemValue = .137;
                count.SystemValue = 12;
                MathUtility math = (MathUtility)sw.GetMathUtility();
                double[] vector = (double[])((MathVector)((MathVector)math.CreateVector(new[] { 0.0, (targetTop - currentTop) / 1000, 0.0 }))
                    .MultiplyTransform(sketch.ModelToSketchTransform)).ArrayData;
                model.ClearSelection2(true);
                ParametricContext.Require(tool.Select2(false, 0), "ORDER_SIDE_RIB_TOOL_SELECT_FAILED");
                model.SketchModifyTranslate(0, 0, vector[0], vector[1]);
                model.ClearSelection2(true);
                double[][] afterPoints = OrderSketchWorld(sw, sketch);
                ParametricContext.Require(beforePoints.Length == afterPoints.Length && beforePoints.All(point => afterPoints.Any(other =>
                    Math.Abs(other[0] - point[0]) < .001 && Math.Abs(other[1] - point[1] - (targetTop - currentTop)) < .001 &&
                    Math.Abs(other[2] - point[2]) < .001)), "ORDER_SIDE_RIB_TOOL_MOVE_FAILED");
                ProductionPartHealth(model);
                double[] actualBands = OrderFormingCenters(model);
                ParametricContext.Require(actualBands.Length == centers.Length &&
                    centers.All(center => actualBands.Count(actual => Math.Abs(actual - center) < .01) == 1),
                    "ORDER_SIDE_RIB_FORMING_GRID_FAILED");
                Save(model);
                rows.Add(new { inspection = "order_side_rib_formings_repaired", path, originalPitchMm = 152.5,
                    pitchMm = pitch.SystemValue * 1000, originalCount = 11, count = count.SystemValue,
                    originalToolTopMm = currentTop, toolTopMm = afterPoints.Max(point => point[1]),
                    removedExternalRelations = references.Length, shelfCentersMm = centers, actualFormingCentersMm = actualBands,
                    bodyVolumeMm3 = ProductionVolume(model) * 1e9, savedSha256 = ParametricContext.Hash(path) });
            }
            finally { sw.CloseAllDocuments(true); Marshal.ReleaseComObject(model); }
            ModelDoc2 reopened = Open(sw, path, true);
            try
            {
                ProductionPartHealth(reopened);
                double[] actual = OrderFormingCenters(reopened);
                ParametricContext.Require(actual.Length == centers.Length && centers.All(center => actual.Count(value => Math.Abs(value - center) < .01) == 1),
                    "ORDER_SIDE_RIB_REOPEN_FORMING_GRID_FAILED");
                rows.Add(new { inspection = "order_side_rib_reopened", path, readOnly = reopened.IsOpenedReadOnly(),
                    formingCentersMm = actual, sha256 = ParametricContext.Hash(path), pass = true });
            }
            finally { sw.CloseAllDocuments(true); Marshal.ReleaseComObject(reopened); }
        }

        private static int OrderEndReliefFaceCount(ModelDoc2 model, bool upper)
        {
            int count = 0;
            foreach (Body2 body in (Array)((PartDoc)model).GetBodies2((int)swBodyType_e.swSolidBody, false))
                foreach (Face2 face in (Array)body.GetFaces())
                {
                    Feature owner = face.GetFeature() as Feature;
                    if (owner == null || owner.Name != "切除-拉伸2") continue;
                    double[] box = (double[])face.GetBox();
                    if (upper ? box[4] * 1000 > 1855 : box[1] * 1000 < 32) count++;
                }
            return count;
        }

        private static void RepairOrderFrameEnds(ISldWorks sw, List<object> rows)
        {
            string leftPath = Find("门框 竖隔板L.sldprt"), rightPath = Find("门框 竖隔板R.SLDPRT");
            double leftVolume, leftBefore, rightBefore;
            ModelDoc2 rightBaseline = Open(sw, rightPath, true);
            try
            {
                ProductionPartHealth(rightBaseline);
                rightBefore = ProductionVolume(rightBaseline);
            }
            finally { sw.CloseAllDocuments(true); Marshal.ReleaseComObject(rightBaseline); }
            ModelDoc2 left = Open(sw, leftPath, false);
            try
            {
                ProductionActivate(sw, left);
                ParametricContext.Require(!left.IsOpenedReadOnly(), "ORDER_FRAME_LEFT_READONLY");
                Feature seed = OrderFeature(left, "切除-拉伸2"), pattern = OrderFeature(left, "阵列(线性)1");
                ParametricContext.Require(seed.IsSuppressed() && pattern.IsSuppressed() && OrderEndReliefFaceCount(left, false) == 0 &&
                    OrderEndReliefFaceCount(left, true) == 0, "ORDER_FRAME_END_SOURCE_STATE_CHANGED");
                leftBefore = ProductionVolume(left);
                ParametricContext.Require(seed.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature,
                    (int)swInConfigurationOpts_e.swAllConfiguration, null), "ORDER_FRAME_END_SEED_RESTORE_FAILED");
                ProductionPartHealth(left);
                leftVolume = ProductionVolume(left);
                ParametricContext.Require(pattern.IsSuppressed() && OrderEndReliefFaceCount(left, false) == 5 &&
                    OrderEndReliefFaceCount(left, true) == 5 && (leftBefore - leftVolume) * 1e9 > 300 &&
                    (leftBefore - leftVolume) * 1e9 < 500,
                    "ORDER_FRAME_END_RELIEF_GEOMETRY_FAILED");
                Save(left);
                rows.Add(new { inspection = "order_frame_left_end_relief_restored", path = leftPath,
                    oldVolumeMm3 = leftBefore * 1e9, newVolumeMm3 = leftVolume * 1e9,
                    lowerEndFaces = 5, upperEndFaces = 5, oldPatternSuppressed = true,
                    patternSuppressed = pattern.IsSuppressed(), savedSha256 = ParametricContext.Hash(leftPath) });
            }
            finally { sw.CloseAllDocuments(true); Marshal.ReleaseComObject(left); }
            ModelDoc2 source = Open(sw, leftPath, true);
            ModelDoc2 right = null;
            try
            {
                ParametricContext.Require(source.IsOpenedReadOnly() &&
                    Math.Abs(ProductionVolume(source) - leftVolume) * 1e9 < .01,
                    "ORDER_FRAME_LEFT_SOURCE_REOPEN_FAILED");
                right = Open(sw, rightPath, false);
                ProductionActivate(sw, right);
                ParametricContext.Require(!right.IsOpenedReadOnly() && right.ForceRebuild3(false), "ORDER_FRAME_RIGHT_REBUILD_FAILED");
                ProductionPartHealth(right);
                double volume = ProductionVolume(right);
                double leftRemovedMm3 = (leftBefore - leftVolume) * 1e9;
                double rightRemovedMm3 = (rightBefore - volume) * 1e9;
                rows.Add(new { inspection = "order_frame_mirror_refresh_probe", leftVolumeMm3 = leftVolume * 1e9,
                    rightVolumeBeforeMm3 = rightBefore * 1e9, rightVolumeMm3 = volume * 1e9,
                    leftRemovedMm3, rightRemovedMm3, sourceOpenReadOnly = source.IsOpenedReadOnly(),
                    mirrorFeature = OrderFeature(right, "门框 竖隔板L镜向").Name });
                ParametricContext.Require(Math.Abs(rightRemovedMm3 - leftRemovedMm3) < .01,
                    "ORDER_FRAME_RIGHT_END_RELIEF_NOT_PROPAGATED");
                Save(right);
                rows.Add(new { inspection = "order_frame_right_mirror_refreshed", path = rightPath,
                    mirroredVolumeMm3 = volume * 1e9, removedEndVolumeMm3 = rightRemovedMm3,
                    savedSha256 = ParametricContext.Hash(rightPath) });
            }
            finally
            {
                sw.CloseAllDocuments(true);
                if (right != null) Marshal.ReleaseComObject(right);
                Marshal.ReleaseComObject(source);
            }
            foreach (string path in new[] { leftPath, rightPath })
            {
                ModelDoc2 reopened = Open(sw, path, true);
                try
                {
                    ProductionPartHealth(reopened);
                    double volume = ProductionVolume(reopened);
                    ParametricContext.Require(reopened.IsOpenedReadOnly() &&
                        (path == leftPath ? Math.Abs(volume - leftVolume) * 1e9 < .01 :
                            Math.Abs((rightBefore - volume) * 1e9 - (leftBefore - leftVolume) * 1e9) < .01),
                        "ORDER_FRAME_END_REOPEN_GEOMETRY_FAILED " + path);
                    rows.Add(new { inspection = "order_frame_end_reopened", path,
                        readOnly = true, volumeMm3 = volume * 1e9, sha256 = ParametricContext.Hash(path), pass = true });
                }
                finally { sw.CloseAllDocuments(true); Marshal.ReleaseComObject(reopened); }
            }
        }

        private static void RepairProductionOrderInterfaces(ISldWorks sw, List<object> rows)
        {
            RequireProductionProfile();
            ParametricContext.Require(ParametricContext.WidthMm == 1200 && ParametricContext.HeightMm == 1917 &&
                ParametricContext.DepthMm == 485, "ORDER_INTERFACE_PROFILE_SCOPE_CHANGED");
            RepairOrderSideRib(sw, rows);
            RepairOrderFrameEnds(sw, rows);
        }
    }
}
