using System;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

internal static class TrialAssemblyOfflineTests
{
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 64 };

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("ASSERTION_FAILED: " + message);
    }

    static void AssertThrowsContains(Action action, string expectedSubstring, string caseName)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            if (error.Message.IndexOf(expectedSubstring, StringComparison.Ordinal) >= 0) return;
            throw new InvalidOperationException(caseName + " threw unexpected message: " + error.Message);
        }
        throw new InvalidOperationException(caseName + " did not throw expected error containing: " + expectedSubstring);
    }

    static Dictionary<string, object> Collision(string componentA, string componentB, double volumeMm3)
    {
        return new Dictionary<string, object>
        {
            { "componentA", componentA },
            { "componentB", componentB },
            { "volumeMm3", volumeMm3 },
            { "intersectionBoundsMm", new[] { 0.0, 0.0, 0.0, 1.0, 1.0, 1.0 } }
        };
    }

    static Dictionary<string, object> AsMap(object value)
    {
        var map = Json.DeserializeObject(Json.Serialize(value)) as Dictionary<string, object>;
        Assert(map != null, "SERIALIZED_MAP_EXPECTED");
        return map;
    }

    static int TestCabinetEnvelope()
    {
        TrialAssembly.ValidateCabinetEnvelope(900, 2000, 500, 387, false);
        TrialAssembly.ValidateCabinetEnvelope(1000, 2000, 500, 437, false);
        AssertThrowsContains(() => TrialAssembly.ValidateCabinetEnvelope(800, 2000, 500, 337, false), "ASSEMBLY_TRIAL_MATRIX_RANGE_REQUIRED", "pilot_rejects_800W");
        AssertThrowsContains(() => TrialAssembly.ValidateCabinetEnvelope(1100, 2000, 500, 487, false), "ASSEMBLY_TRIAL_MATRIX_RANGE_REQUIRED", "pilot_rejects_1100W");
        AssertThrowsContains(() => TrialAssembly.ValidateCabinetEnvelope(1000, 2100, 500, 437, false), "ASSEMBLY_TRIAL_MATRIX_RANGE_REQUIRED", "pilot_rejects_2100H");

        TrialAssembly.ValidateCabinetEnvelope(700, 2000, 500, 287, true);
        TrialAssembly.ValidateCabinetEnvelope(800, 2000, 500, 337, true);
        TrialAssembly.ValidateCabinetEnvelope(1100, 2000, 500, 487, true);
        TrialAssembly.ValidateCabinetEnvelope(1200, 2000, 500, 537, true);
        AssertThrowsContains(() => TrialAssembly.ValidateCabinetEnvelope(699, 2000, 500, 286.5, true), "ASSEMBLY_TRIAL_WIDTH_ENVELOPE_RANGE_REQUIRED", "width_env_rejects_699");
        AssertThrowsContains(() => TrialAssembly.ValidateCabinetEnvelope(1201, 2000, 500, 537.5, true), "ASSEMBLY_TRIAL_WIDTH_ENVELOPE_RANGE_REQUIRED", "width_env_rejects_1201");
        AssertThrowsContains(() => TrialAssembly.ValidateCabinetEnvelope(800, 2100, 500, 337, true), "ASSEMBLY_TRIAL_WIDTH_ENVELOPE_RANGE_REQUIRED", "width_env_rejects_2100H");
        AssertThrowsContains(() => TrialAssembly.ValidateCabinetEnvelope(800, 2000, 550, 337, true), "ASSEMBLY_TRIAL_WIDTH_ENVELOPE_RANGE_REQUIRED", "width_env_rejects_550D");
        AssertThrowsContains(() => TrialAssembly.ValidateCabinetEnvelope(800, 2000, 500, 350, true), "ASSEMBLY_TRIAL_WIDTH_ENVELOPE_RANGE_REQUIRED", "width_env_rejects_bad_door");
        return 12;
    }

    static int TestMissingSlotCenters()
    {
        double unit = (2000.0 - 87.0) / 12.0;
        var grid = new List<double>();
        for (int i = 1; i <= 11; i++) grid.Add(28.5 + i * unit);

        double[] equalCaseMissing = TrialAssembly.ComputeMissingSlotCenters(grid, new[] { 506.75, 985.0, 1463.25 }, 0.01);
        Assert(equalCaseMissing.Length == 0, "equal_case_should_have_no_missing_slots");

        double[] unequalCaseMissing = TrialAssembly.ComputeMissingSlotCenters(grid, new[] { 1176.3, 793.7, 793.702 }, 0.01);
        Assert(unequalCaseMissing.Length == 2, "unequal_case_missing_count");
        Assert(Math.Abs(unequalCaseMissing[0] - 793.7) < 0.001 && Math.Abs(unequalCaseMissing[1] - 1176.3) < 0.001, "unequal_case_sorted_missing");

        double[] offsetMissing = TrialAssembly.ComputeMissingSlotCenters(grid, new[] { 985.02 }, 0.01);
        Assert(offsetMissing.Length == 1 && Math.Abs(offsetMissing[0] - 985.02) < 0.0001, "beyond_tolerance_detected");
        AssertThrowsContains(() => TrialAssembly.ComputeMissingSlotCenters(grid, new[] { Double.NaN }, 0.01), "FINITE", "nan_slot_rejected");
        return 5;
    }

    static int TestLockSeedMove()
    {
        double targetSeedY, targetD2;
        double[] remainingL = TrialAssembly.ComputeLockSeedMove(new[] { 187.917, 666.167, 1144.417, 1622.667 }, 1702.375, 159.4167, out targetSeedY, out targetD2);
        Assert(Math.Abs(targetSeedY - 1622.667) < 0.0001, "case2L_target_seed");
        Assert(Math.Abs(targetD2 - (159.4167 + (1702.375 - 1622.667))) < 0.0001, "case2L_target_d2");
        Assert(remainingL.Length == 3 && Math.Abs(remainingL[0] - 187.917) < 0.0001 && Math.Abs(remainingL[2] - 1144.417) < 0.0001, "case2L_remaining");

        double[] remainingR = TrialAssembly.ComputeLockSeedMove(new[] { 1558.9, 985.0, 411.1 }, 1702.375, 159.4167, out targetSeedY, out targetD2);
        Assert(Math.Abs(targetSeedY - 1558.9) < 0.0001, "case2R_target_seed");
        Assert(remainingR.Length == 2 && Math.Abs(remainingR[0] - 411.1) < 0.0001 && Math.Abs(remainingR[1] - 985.0) < 0.0001, "case2R_remaining");

        double[] singleRow = TrialAssembly.ComputeLockSeedMove(new[] { 1750.0, 1850.0 }, 1702.375, 159.4167, out targetSeedY, out targetD2);
        Assert(Math.Abs(targetSeedY - 1750.0) < 0.0001 && Math.Abs(targetD2 - 111.7917) < 0.001, "above_seed_fallback");
        Assert(singleRow.Length == 1 && Math.Abs(singleRow[0] - 1850.0) < 0.0001, "above_seed_remaining");

        AssertThrowsContains(() => TrialAssembly.ComputeLockSeedMove(new double[0], 1702.375, 159.4167, out targetSeedY, out targetD2), "ASSEMBLY_LOCK_SEED_INPUT_INVALID", "empty_lock_rows");
        return 7;
    }

    static int TestInterferenceClassification()
    {
        var kinds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "L_W437_H471p25_panel-2", "panel" },
            { "trial_hinge_latch_plate-2", "hinge_latch_bottom" },
            { "trial_top_latch_left-2", "hinge_latch_top" },
            { "trial_circlip-11", "circlip_top" },
            { "trial_door_hinge_pin-7", "hinge_pin" },
            { "门框 右-2", "door_frame" },
            { "门框 横隔板R-3", "crossbar" },
            { "箱体竖隔板L-2", "vertical_partition" },
            { "trial_mechanical_lock_tongue-7", "mechanical_lock_tongue" }
        };

        var baselineOnly = new[]
        {
            Collision("L_W437_H471p25_panel-2", "trial_hinge_latch_plate-2", 0.030044),
            Collision("L_W437_H471p25_panel-2", "trial_top_latch_left-2", 0.030044),
            Collision("trial_circlip-11", "trial_door_hinge_pin-7", 0.928393),
            Collision("trial_circlip-11", "trial_door_hinge_pin-7", 0.673889)
        };
        var baseMap = AsMap(TrialAssembly.ClassifyInterferenceCollisions(baselineOnly, kinds));
        Assert(Convert.ToInt32(baseMap["totalCollisionCount"]) == 4, "baseline_total");
        Assert(Convert.ToInt32(baseMap["baselineV35CollisionCount"]) == 4, "baseline_v35_count");
        Assert(Convert.ToInt32(baseMap["v35DoorHingeLatchBendFilletCount"]) == 2, "baseline_latch_count");
        Assert(Convert.ToInt32(baseMap["v35CirclipHingePinGrooveMismatchCount"]) == 2, "baseline_circlip_count");
        Assert(Convert.ToInt32(baseMap["structuralCollisionCount"]) == 0, "baseline_structural_zero");
        Assert(Convert.ToBoolean(baseMap["structuralCollisionsPass"]), "baseline_structural_pass");

        var mixed = new[]
        {
            Collision("L_W437_H471p25_panel-2", "trial_hinge_latch_plate-2", 0.030044),
            Collision("L_W437_H471p25_panel-2", "trial_hinge_latch_plate-2", 0.25),
            Collision("trial_circlip-11", "trial_door_hinge_pin-7", 1.5),
            Collision("门框 右-2", "门框 横隔板R-3", 2.6012),
            Collision("箱体竖隔板L-2", "trial_mechanical_lock_tongue-7", 46.2019)
        };
        var mixedMap = AsMap(TrialAssembly.ClassifyInterferenceCollisions(mixed, null));
        Assert(Convert.ToInt32(mixedMap["baselineV35CollisionCount"]) == 1, "mixed_fallback_v35_count");
        Assert(Convert.ToInt32(mixedMap["structuralCollisionCount"]) == 4, "mixed_structural_count");
        Assert(!Convert.ToBoolean(mixedMap["structuralCollisionsPass"]), "mixed_structural_fails");
        return 9;
    }

    static int TestDerivedFlatPatternRebuildPolicy()
    {
        Assert(TrialAssembly.ShouldAllowDerivedFlatPatternRebuildFailure("Default", "Default", true, "冷轧钢板", 7), "rebuilt_referenced_passes");
        Assert(!TrialAssembly.ShouldAllowDerivedFlatPatternRebuildFailure("Default", "Default", false, "", 0), "referenced_rebuild_failure_never_allowed");
        Assert(!TrialAssembly.ShouldAllowDerivedFlatPatternRebuildFailure("默认", "默认", false, "", 0), "referenced_chinese_rebuild_failure_never_allowed");
        Assert(!TrialAssembly.ShouldAllowDerivedFlatPatternRebuildFailure("DefaultSM-FLAT-PATTERN", "DefaultSM-FLAT-PATTERN", false, "", 0), "active_flat_pattern_never_allowed");
        Assert(!TrialAssembly.ShouldAllowDerivedFlatPatternRebuildFailure("OtherConfig", "Default", false, "", 0), "non_flat_derived_config_never_allowed");
        Assert(!TrialAssembly.ShouldAllowDerivedFlatPatternRebuildFailure("DefaultSM-FLAT-PATTERN", "Default", false, "冷轧钢板", 0), "flat_pattern_with_material_never_allowed");
        Assert(!TrialAssembly.ShouldAllowDerivedFlatPatternRebuildFailure("DefaultSM-FLAT-PATTERN", "Default", false, "", 3), "flat_pattern_with_custom_properties_never_allowed");
        Assert(TrialAssembly.ShouldAllowDerivedFlatPatternRebuildFailure("DefaultSM-FLAT-PATTERN", "Default", false, "", 0), "unreferenced_empty_flat_pattern_diagnostic_allowed");
        Assert(TrialAssembly.ShouldAllowDerivedFlatPatternRebuildFailure("SM-FLAT-PATTERN", "Default", false, null, 0), "secondary_empty_flat_pattern_diagnostic_allowed");
        return 9;
    }

    static int TestTopologyAndMetadataCacheHelpers()
    {
        Assert(TrialAssembly.CanReuseTopologySourceIdentity(true, 1, 27, 27, 44, 44), "matching_part_doc_and_topology_counts_reuse");
        Assert(!TrialAssembly.CanReuseTopologySourceIdentity(false, 1, 27, 27, 44, 44), "mismatched_iunknown_rejected");
        Assert(!TrialAssembly.CanReuseTopologySourceIdentity(true, 0, 27, 27, 44, 44), "non_same_sw_object_rejected");
        Assert(!TrialAssembly.CanReuseTopologySourceIdentity(true, 1, 0, 0, 0, 0), "zero_face_body_rejected");
        Assert(!TrialAssembly.CanReuseTopologySourceIdentity(true, 1, 27, 28, 44, 44), "face_count_drift_rejected");
        Assert(!TrialAssembly.CanReuseTopologySourceIdentity(true, 1, 27, 27, 44, 45), "edge_count_drift_rejected");
        var source = new Dictionary<string, object>(StringComparer.Ordinal) { { "identity", "door_L_01_bushing_top" }, { "materialName", "ABS" } };
        var cloned = TrialAssembly.CloneMetadataRecord(source, "door_L_02_bushing_top", "door_L_01_bushing_top");
        Assert(Convert.ToString(cloned["identity"]) == "door_L_02_bushing_top", "cloned_identity_updated");
        Assert(Convert.ToString(cloned["metadataSourceIdentity"]) == "door_L_01_bushing_top", "cloned_source_identity_recorded");
        Assert(Convert.ToBoolean(cloned["reusedDocumentMetadata"]), "cloned_reuse_flag_recorded");
        Assert(Convert.ToString(source["identity"]) == "door_L_01_bushing_top" && !source.ContainsKey("reusedDocumentMetadata"), "template_metadata_unmutated");
        return 10;
    }

    public static int Main()
    {
        try
        {
            int checks = 0;
            checks += TestCabinetEnvelope();
            checks += TestMissingSlotCenters();
            checks += TestLockSeedMove();
            checks += TestInterferenceClassification();
            checks += TestDerivedFlatPatternRebuildPolicy();
            checks += TestTopologyAndMetadataCacheHelpers();
            Console.WriteLine("TRIAL_ASSEMBLY_OFFLINE_PASS " + checks);
            return 0;
        }
        catch (Exception error)
        {
            Console.WriteLine("ERROR: " + error.GetType().FullName + " :: " + error.Message);
            Console.WriteLine(error.StackTrace);
            return 1;
        }
    }
}
