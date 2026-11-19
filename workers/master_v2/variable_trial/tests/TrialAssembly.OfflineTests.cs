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

    static Dictionary<string, object> FullCollision(string componentA, string componentB, double volumeMm3, object boundsMm)
    {
        return new Dictionary<string, object>
        {
            { "componentA", componentA },
            { "componentB", componentB },
            { "volumeMm3", volumeMm3 },
            { "intersectionBoundsMm", boundsMm }
        };
    }

    static int TestFullInterferencePositionZones()
    {
        var meta = new Dictionary<string, TrialAssembly.FullCabinetAssembly.InterferenceComponentMeta>(StringComparer.Ordinal)
        {
            { "trial_circlip-7", new TrialAssembly.FullCabinetAssembly.InterferenceComponentMeta { ComponentName = "trial_circlip-7", Role = "door_L_04_circlip_top", Kind = "circlip_top", Path = @"C:\case1\parts\trial_circlip.SLDPRT", Side = "L", DoorIndex = 4 } },
            { "trial_door_hinge_pin-5", new TrialAssembly.FullCabinetAssembly.InterferenceComponentMeta { ComponentName = "trial_door_hinge_pin-5", Role = "door_L_05_hinge_pin", Kind = "hinge_pin", Path = @"C:\case1\parts\trial_door_hinge_pin.SLDPRT", Side = "L", DoorIndex = 5 } },
            { "R_W367_H335p6_panel-4", new TrialAssembly.FullCabinetAssembly.InterferenceComponentMeta { ComponentName = "R_W367_H335p6_panel-4", Role = "door_R_04_panel", Kind = "panel", Path = @"C:\case1\parts\R_W367_H335p6_panel.SLDPRT", Side = "R", DoorIndex = 4 } },
            { "trial_plastic_bushing-19", new TrialAssembly.FullCabinetAssembly.InterferenceComponentMeta { ComponentName = "trial_plastic_bushing-19", Role = "door_R_04_bushing_top", Kind = "bushing_top", Path = @"C:\case1\parts\trial_plastic_bushing.SLDPRT", Side = "R", DoorIndex = 4 } }
        };

        var confirmedZones = new Dictionary<string, TrialAssembly.FullCabinetAssembly.InterferenceAllowedZone>(StringComparer.Ordinal)
        {
            {
                TrialAssembly.FullCabinetAssembly.MakeInterferencePairKey("door_L_04_circlip_top", "door_L_05_hinge_pin"),
                new TrialAssembly.FullCabinetAssembly.InterferenceAllowedZone
                {
                    Kind = "circlip_hinge_pin_groove",
                    TargetLabel = "door_L_04_circlip_top <-> door_L_05_hinge_pin groove",
                    Side = "L",
                    DoorIndex = 4,
                    AllowedBoundsMm = new[] { -400.5, 1136.5, -10.5, -393.5, 1138.1, -4.8 },
                    ZoneConfirmed = true
                }
            },
            {
                TrialAssembly.FullCabinetAssembly.MakeInterferencePairKey("door_R_04_panel", "door_R_04_bushing_top"),
                new TrialAssembly.FullCabinetAssembly.InterferenceAllowedZone
                {
                    Kind = "upper_pin_prebias_0p5",
                    TargetLabel = "door_R_04_panel upper_pin_prebias_0p5_hole",
                    Side = "R",
                    DoorIndex = 4,
                    AllowedBoundsMm = new[] { 398.0, 1394.4, -12.0, 402.5, 1395.6, -2.0 },
                    ZoneConfirmed = true
                }
            }
        };

        // Case 1: Both classes within confirmed spatial zones and within original volume limits -> APPROVED
        var legalCollisions = new[]
        {
            FullCollision("trial_circlip-7", "trial_door_hinge_pin-5", 0.9284, new[] { -400.0, 1136.9, -8.01, -399.29, 1137.7, -5.35 }),
            FullCollision("R_W367_H335p6_panel-4", "trial_plastic_bushing-19", 1.8099, new[] { 398.75, 1394.6, -11.58, 401.9, 1395.4, -2.42 })
        };
        var legalRes = AsMap(TrialAssembly.FullCabinetAssembly.EvaluateFullInterferenceCollisions(legalCollisions, meta, confirmedZones, true));
        Assert(Convert.ToBoolean(legalRes["interferencePass"]), "legal_both_classes_pass");
        Assert(Convert.ToInt32(legalRes["approvedCirclipHingePinCount"]) == 1, "legal_circlip_approved");
        Assert(Convert.ToInt32(legalRes["approvedUpperPinPrebiasCount"]) == 1, "legal_prebias_approved");
        Assert(Convert.ToInt32(legalRes["positionVerifiedCount"]) == 2, "legal_position_verified_count");
        Assert(Convert.ToInt32(legalRes["unapprovedInterferenceCount"]) == 0, "legal_zero_unapproved");

        // Case 2: Exact same legal volumes (0.9284 and 1.8099) but shifted OUTSIDE allowed zones -> REJECTED
        var wrongPositionCollisions = new[]
        {
            FullCollision("trial_circlip-7", "trial_door_hinge_pin-5", 0.9284, new[] { -400.0, 1180.0, -8.01, -399.29, 1180.8, -5.35 }),
            FullCollision("R_W367_H335p6_panel-4", "trial_plastic_bushing-19", 1.8099, new[] { 398.75, 1320.0, -11.58, 401.9, 1320.8, -2.42 })
        };
        var wrongPosRes = AsMap(TrialAssembly.FullCabinetAssembly.EvaluateFullInterferenceCollisions(wrongPositionCollisions, meta, confirmedZones, true));
        Assert(!Convert.ToBoolean(wrongPosRes["interferencePass"]), "wrong_position_must_fail");
        Assert(Convert.ToInt32(wrongPosRes["approvedCirclipHingePinCount"]) == 0, "wrong_position_circlip_rejected");
        Assert(Convert.ToInt32(wrongPosRes["approvedUpperPinPrebiasCount"]) == 0, "wrong_position_prebias_rejected");
        Assert(Convert.ToInt32(wrongPosRes["positionMismatchCount"]) == 2, "wrong_position_mismatch_count");
        Assert(Convert.ToInt32(wrongPosRes["unapprovedInterferenceCount"]) == 2, "wrong_position_unapproved_count");

        // Case 3a: Missing intersectionBoundsMm evidence with legal volumes -> REJECTED
        var missingBoundsCollisions = new[]
        {
            FullCollision("trial_circlip-7", "trial_door_hinge_pin-5", 0.9284, null),
            FullCollision("R_W367_H335p6_panel-4", "trial_plastic_bushing-19", 1.8099, new[] { 398.75, 1394.6 })
        };
        var missingBoundsRes = AsMap(TrialAssembly.FullCabinetAssembly.EvaluateFullInterferenceCollisions(missingBoundsCollisions, meta, confirmedZones, true));
        Assert(!Convert.ToBoolean(missingBoundsRes["interferencePass"]), "missing_bounds_must_fail");
        Assert(Convert.ToInt32(missingBoundsRes["missingPositionEvidenceCount"]) == 2, "missing_bounds_count");
        Assert(Convert.ToInt32(missingBoundsRes["unapprovedInterferenceCount"]) == 2, "missing_bounds_unapproved");

        // Case 3b: Missing or unconfirmed allowed zone with legal volumes -> REJECTED
        var unconfirmedZones = new Dictionary<string, TrialAssembly.FullCabinetAssembly.InterferenceAllowedZone>(StringComparer.Ordinal)
        {
            {
                TrialAssembly.FullCabinetAssembly.MakeInterferencePairKey("door_L_04_circlip_top", "door_L_05_hinge_pin"),
                new TrialAssembly.FullCabinetAssembly.InterferenceAllowedZone
                {
                    Kind = "circlip_hinge_pin_groove",
                    TargetLabel = "unconfirmed_groove",
                    AllowedBoundsMm = new[] { -400.5, 1136.5, -10.5, -393.5, 1138.1, -4.8 },
                    ZoneConfirmed = false
                }
            }
        };
        var unconfirmedRes = AsMap(TrialAssembly.FullCabinetAssembly.EvaluateFullInterferenceCollisions(legalCollisions, meta, unconfirmedZones, true));
        Assert(!Convert.ToBoolean(unconfirmedRes["interferencePass"]), "unconfirmed_or_missing_zone_must_fail");
        Assert(Convert.ToInt32(unconfirmedRes["missingPositionEvidenceCount"]) == 2, "unconfirmed_or_missing_zone_count");
        Assert(Convert.ToInt32(unconfirmedRes["unapprovedInterferenceCount"]) == 2, "unconfirmed_or_missing_zone_unapproved");

        // Case 4: Volume ceiling still enforced even when inside confirmed spatial zones -> REJECTED
        var overVolCollisions = new[]
        {
            FullCollision("trial_circlip-7", "trial_door_hinge_pin-5", 1.25, new[] { -400.0, 1136.9, -8.01, -399.29, 1137.7, -5.35 }),
            FullCollision("R_W367_H335p6_panel-4", "trial_plastic_bushing-19", 5.05, new[] { 398.75, 1394.6, -11.58, 401.9, 1395.4, -2.42 })
        };
        var overVolRes = AsMap(TrialAssembly.FullCabinetAssembly.EvaluateFullInterferenceCollisions(overVolCollisions, meta, confirmedZones, true));
        Assert(!Convert.ToBoolean(overVolRes["interferencePass"]), "over_volume_ceiling_must_fail");
        Assert(Convert.ToInt32(overVolRes["unapprovedInterferenceCount"]) == 2, "over_volume_ceiling_unapproved");

        return 18;
    }

    static int TestConfigurationAwareMaterials()
    {
        // Case 1: Same file referenced by two different configurations; second config lacks material -> MUST FAIL
        var dualConfigObs = new[]
        {
            new TrialAssembly.FullCabinetAssembly.PartConfigMaterialObservation
            {
                PartPath = @"C:\case1\parts\shared_bracket.SLDPRT",
                ConfigurationName = "Cfg_Left",
                CheckRole = "assembly_referenced",
                IsSheetMetal = false,
                MaterialDatabase = "云绅材料库",
                MaterialName = "冷轧钢板",
                IndependentMassKg = 0.42,
                DensityKgPerM3 = 7870.0
            },
            new TrialAssembly.FullCabinetAssembly.PartConfigMaterialObservation
            {
                PartPath = @"C:\case1\parts\shared_bracket.SLDPRT",
                ConfigurationName = "Cfg_Right",
                CheckRole = "assembly_referenced",
                IsSheetMetal = false,
                MaterialDatabase = "",
                MaterialName = "",
                IndependentMassKg = 0.05,
                DensityKgPerM3 = 1000.0
            }
        };
        var dualRes = AsMap(TrialAssembly.FullCabinetAssembly.EvaluateConfigurationMaterials(dualConfigObs));
        Assert(!Convert.ToBoolean(dualRes["pass"]), "dual_config_second_missing_must_fail");
        Assert(Convert.ToInt32(dualRes["uniquePartCount"]) == 1, "dual_config_unique_part_count");
        Assert(Convert.ToInt32(dualRes["checkedConfigurationCount"]) == 2, "dual_config_checked_count");
        Assert(Convert.ToInt32(dualRes["missingReferencedMaterialCount"]) == 1, "dual_config_missing_ref_count");
        Assert(Convert.ToInt32(dualRes["missingMaterialCount"]) == 1, "dual_config_missing_total");

        // Case 2: Folded configuration has material (7870 kg/m3), but derived flat-pattern config lacks material (1000 kg/m3) -> MUST FAIL
        var missingFlatMatObs = new[]
        {
            new TrialAssembly.FullCabinetAssembly.PartConfigMaterialObservation
            {
                PartPath = @"C:\case1\parts\L_W367_H278p5_panel.SLDPRT",
                ConfigurationName = "Default",
                CheckRole = "assembly_referenced",
                IsSheetMetal = true,
                MaterialDatabase = "云绅材料库",
                MaterialName = "冷轧钢板",
                IndependentMassKg = 0.7938,
                DensityKgPerM3 = 7870.0
            },
            new TrialAssembly.FullCabinetAssembly.PartConfigMaterialObservation
            {
                PartPath = @"C:\case1\parts\L_W367_H278p5_panel.SLDPRT",
                ConfigurationName = "DefaultSM-FLAT-PATTERN",
                CheckRole = "sheet_metal_flat_pattern",
                IsSheetMetal = true,
                MaterialDatabase = "",
                MaterialName = "",
                IndependentMassKg = 0.1008,
                DensityKgPerM3 = 1000.0
            }
        };
        var missingFlatMatRes = AsMap(TrialAssembly.FullCabinetAssembly.EvaluateConfigurationMaterials(missingFlatMatObs));
        Assert(!Convert.ToBoolean(missingFlatMatRes["pass"]), "flat_missing_material_must_fail");
        Assert(Convert.ToInt32(missingFlatMatRes["missingReferencedMaterialCount"]) == 0, "flat_missing_ref_ok");
        Assert(Convert.ToInt32(missingFlatMatRes["missingFlatPatternMaterialCount"]) == 1, "flat_missing_count_1");
        Assert(Convert.ToInt32(missingFlatMatRes["missingMaterialCount"]) == 1, "flat_missing_total_1");

        // Case 2b: Sheet-metal part with NO flat-pattern configuration readback at all -> MUST FAIL
        var missingFlatCfgRes = AsMap(TrialAssembly.FullCabinetAssembly.EvaluateConfigurationMaterials(new[] { missingFlatMatObs[0] }));
        Assert(!Convert.ToBoolean(missingFlatCfgRes["pass"]), "missing_flat_config_readback_must_fail");
        Assert(Convert.ToInt32(missingFlatCfgRes["missingFlatPatternConfigCount"]) == 1, "missing_flat_config_count_1");

        // Case 3: Complete materials on referenced config, sheet-metal flat-pattern config, and hardware config -> PASS
        var completeObs = new[]
        {
            new TrialAssembly.FullCabinetAssembly.PartConfigMaterialObservation
            {
                PartPath = @"C:\case1\parts\L_W367_H278p5_panel.SLDPRT",
                ConfigurationName = "Default",
                CheckRole = "assembly_referenced",
                IsSheetMetal = true,
                MaterialDatabase = "云绅材料库",
                MaterialName = "冷轧钢板",
                IndependentMassKg = 0.7938,
                DensityKgPerM3 = 7870.0
            },
            new TrialAssembly.FullCabinetAssembly.PartConfigMaterialObservation
            {
                PartPath = @"C:\case1\parts\L_W367_H278p5_panel.SLDPRT",
                ConfigurationName = "DefaultSM-FLAT-PATTERN",
                CheckRole = "sheet_metal_flat_pattern",
                IsSheetMetal = true,
                MaterialDatabase = "云绅材料库",
                MaterialName = "冷轧钢板",
                IndependentMassKg = 0.7938,
                DensityKgPerM3 = 7870.0
            },
            new TrialAssembly.FullCabinetAssembly.PartConfigMaterialObservation
            {
                PartPath = @"C:\case1\parts\trial_door_hinge_pin.SLDPRT",
                ConfigurationName = "默认",
                CheckRole = "assembly_referenced",
                IsSheetMetal = false,
                MaterialDatabase = "云绅材料库",
                MaterialName = "不锈钢201",
                IndependentMassKg = 0.0189,
                DensityKgPerM3 = 7930.0
            }
        };
        var completeRes = AsMap(TrialAssembly.FullCabinetAssembly.EvaluateConfigurationMaterials(completeObs));
        Assert(Convert.ToBoolean(completeRes["pass"]), "complete_materials_must_pass");
        Assert(Convert.ToInt32(completeRes["uniquePartCount"]) == 2, "complete_unique_parts");
        Assert(Convert.ToInt32(completeRes["checkedConfigurationCount"]) == 3, "complete_checked_configs");
        Assert(Convert.ToInt32(completeRes["missingMaterialCount"]) == 0, "complete_zero_missing_mat");
        Assert(Convert.ToInt32(completeRes["missingFlatPatternConfigCount"]) == 0, "complete_zero_missing_flat");

        return 16;
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
            checks += TestFullInterferencePositionZones();
            checks += TestConfigurationAwareMaterials();
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
