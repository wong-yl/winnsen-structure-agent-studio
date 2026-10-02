import { createHash } from 'node:crypto'
export const LOCK_SOURCE_PATH = 'workers/native_model_requests/development/v1/tools/BuildLockTopology888x14.cs'
export function adaptLockSource(source) {
  if (createHash('sha256').update(source).digest('hex').toUpperCase() !== 'D317BBAAEE1F38898680796F711768CC28105C3304AB3FB06DA99347E982C7D2') throw new Error('native lock source revision changed')
  let text = source.replace(/\r\n?/g, '\n').replace('internal static class BuildLockTopology888x14', 'internal static partial class NativeParametricLocks')
  text = text.replace(': base(message)', ': base(status + ": " + message)')
  text = text.replace('private static readonly double[] ExpectedSlots = Enumerable.Range(0, 7)\n            .Select(index => DoorBottomMm + DoorHeightMm / 2.0 + index * DoorPitchMm).ToArray();', 'private static double[] ExpectedSlots { get { return ActiveRows; } }')
  text = text.replace('private static readonly double[] ExpectedCircles = ExpectedSlots.Select(value => value - 60.0).ToArray();', 'private static double[] ExpectedCircles { get { return ExpectedSlots.Select(value => value - 60.0).ToArray(); } }')
  text = text.replaceAll('value.slot_analyses.Count(row => row.full_fingerprint) == 7', 'value.slot_analyses.Count(row => row.full_fingerprint) == ExpectedSlots.Length')
  text = text.replaceAll('value.circle_analyses.Count(row => row.full_diameter5_pair) == 7', 'value.circle_analyses.Count(row => row.full_diameter5_pair) == ExpectedSlots.Length')
  text = text.replace('data.D1Spacing = DoorPitchMm / 1000.0;', 'data.D1Spacing = ActivePatternSpacingMm / 1000.0;').replace('data.D1TotalInstances = 7;', 'data.D1TotalInstances = 2;')
  text = text.replace('data.D1ReverseDirection = false;', 'data.D1ReverseDirection = ActivePatternReverse;')
  text = text.replace('data.GeometryPattern = sourceData.GeometryPattern;', 'data.GeometryPattern = true;')
  text = text.replace('if (created != null) created.Name = NativePatternName;', 'if (created != null) created.Name = "PARAMETRIC_LOCK_ROW_" + ActivePatternIndex;')
  text = text.replace('Require(startMoved && endMoved, "CIRCLE_DATUM_SETCOORDS_FAILED",', 'Console.WriteLine("CIRCLE_MOVE_DIAGNOSTIC " + new JavaScriptSerializer().Serialize(new { startMoved=startMoved,endMoved=endMoved,beforeA=a,beforeB=b,actualA=PointCoordinates(start),actualB=PointCoordinates(end),actualCircle=PointCoordinates(center),deltaMm=deltaMm }));\n            Require(startMoved && endMoved, "CIRCLE_DATUM_SETCOORDS_FAILED",')
  text = text.replace('string.Equals(type, "MirrorPart", StringComparison.OrdinalIgnoreCase))\n                snapshot.mirror_feature_count++;', 'string.Equals(type, "MirrorPart", StringComparison.OrdinalIgnoreCase) ||\n                string.Equals(type, "BrokenDerivedPartFolder", StringComparison.OrdinalIgnoreCase))\n                snapshot.mirror_feature_count++;')
  return text
}
