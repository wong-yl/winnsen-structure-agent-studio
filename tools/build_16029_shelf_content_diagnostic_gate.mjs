import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';

const root = process.cwd();
const outPath = path.join(root, 'data', 'locker_16029_shelf_content_diagnostic_gate.json');

const partPaths = {
  shelf: path.join(root, 'workers', 'generated_models', 'review_generation_requests', 'part-first-16029-v43-pilot', 'sw2020_part_first', 'shelf_cross_panel_left_v43', 'shelf_cross_panel_left_v43.SLDPRT'),
  shelfCopy: 'C:\\tmp\\16029_shelf_load_probe\\shelf_cross_panel_left_v43_copy.SLDPRT',
  door: path.join(root, 'workers', 'generated_models', 'review_generation_requests', 'part-first-16029-v43-pilot', 'sw2020_part_first', 'door_panel_2_12_right_v43', 'review_single_door_panel_W307_H298_sheetmetal.SLDPRT'),
  front: path.join(root, 'workers', 'generated_models', 'review_generation_requests', 'part-first-16029-v43-pilot', 'sw2020_part_first', 'front_frame_vertical_left_v43', 'front_frame_vertical_left_v43.SLDPRT'),
};

function readJson(relativePath) {
  return JSON.parse(fs.readFileSync(path.join(root, relativePath), 'utf8').replace(/^\uFEFF/, ''));
}

function readJsonIfExists(filePath) {
  try {
    return JSON.parse(fs.readFileSync(filePath, 'utf8').replace(/^\uFEFF/, ''));
  } catch (error) {
    return { unavailable: true, error: String(error.message || error) };
  }
}

function sha256(filePath) {
  return crypto.createHash('sha256').update(fs.readFileSync(filePath)).digest('hex').toUpperCase();
}

function mtimeUtc(filePath) {
  const escaped = filePath.replaceAll("'", "''");
  return execFileSync('powershell.exe', ['-NoProfile', '-Command', `(Get-Item -LiteralPath '${escaped}').LastWriteTimeUtc.ToString('o')`], { encoding: 'utf8' }).trim();
}

function fileMeta(filePath) {
  const stat = fs.statSync(filePath);
  const header = fs.readFileSync(filePath, { start: 0, end: 63 });
  return {
    path: filePath,
    exists: true,
    size_bytes: stat.size,
    last_write_utc: mtimeUtc(filePath),
    sha256: sha256(filePath),
    path_length: filePath.length,
    extension: path.extname(filePath),
    header_hex_64: [...header].map((b) => b.toString(16).padStart(2, '0').toUpperCase()).join(' '),
  };
}

function sectorOffset(sector, sectorSize) {
  return (sector + 1) * sectorSize;
}

function readSector(buffer, sector, sectorSize) {
  const offset = sectorOffset(sector, sectorSize);
  return buffer.subarray(offset, offset + sectorSize);
}

function cfbDirectory(filePath) {
  const buffer = fs.readFileSync(filePath);
  const magic = buffer.subarray(0, 8).toString('hex').toUpperCase();
  const expectedMagic = 'D0CF11E0A1B11AE1';
  if (magic !== expectedMagic) {
    return { readable: false, error: `unexpected compound header ${magic}` };
  }
  const sectorShift = buffer.readUInt16LE(30);
  const sectorSize = 1 << sectorShift;
  const miniSectorShift = buffer.readUInt16LE(32);
  const miniSectorSize = 1 << miniSectorShift;
  const directoryStartSector = buffer.readInt32LE(48);
  const miniStreamCutoffSize = buffer.readUInt32LE(56);
  const firstDifatSector = buffer.readInt32LE(68);
  const difatSectorCount = buffer.readUInt32LE(72);
  const difat = [];
  for (let i = 0; i < 109; i++) {
    const value = buffer.readInt32LE(76 + i * 4);
    if (value >= 0) difat.push(value);
  }
  let nextDifat = firstDifatSector;
  for (let i = 0; i < difatSectorCount && nextDifat >= 0; i++) {
    const sector = readSector(buffer, nextDifat, sectorSize);
    for (let j = 0; j < sectorSize - 4; j += 4) {
      const value = sector.readInt32LE(j);
      if (value >= 0) difat.push(value);
    }
    nextDifat = sector.readInt32LE(sectorSize - 4);
  }
  const fat = [];
  for (const fatSector of difat) {
    const sector = readSector(buffer, fatSector, sectorSize);
    for (let j = 0; j < sectorSize; j += 4) fat.push(sector.readInt32LE(j));
  }
  function chain(start, limit = 20000) {
    const sectors = [];
    let current = start;
    const seen = new Set();
    while (current >= 0 && current < fat.length && !seen.has(current) && sectors.length < limit) {
      seen.add(current);
      sectors.push(current);
      const next = fat[current];
      if (next === -2) break;
      current = next;
    }
    return sectors;
  }
  const directorySectors = chain(directoryStartSector);
  const directoryBuffer = Buffer.concat(directorySectors.map((sector) => readSector(buffer, sector, sectorSize)));
  const entries = [];
  for (let offset = 0; offset + 128 <= directoryBuffer.length; offset += 128) {
    const entry = directoryBuffer.subarray(offset, offset + 128);
    const nameLength = entry.readUInt16LE(64);
    if (nameLength < 2 || nameLength > 64) continue;
    const name = entry.subarray(0, nameLength - 2).toString('utf16le');
    const type = entry.readUInt8(66);
    if (!name || type === 0) continue;
    const startSector = entry.readInt32LE(116);
    const sizeLow = entry.readUInt32LE(120);
    const sizeHigh = entry.readUInt32LE(124);
    const size = Number((BigInt(sizeHigh) << 32n) + BigInt(sizeLow));
    entries.push({
      index: offset / 128,
      name,
      type,
      type_name: type === 1 ? 'storage' : type === 2 ? 'stream' : type === 5 ? 'root' : `type_${type}`,
      start_sector: startSector,
      size_bytes: size,
      short_stream_candidate: type === 2 && size > 0 && size < miniStreamCutoffSize,
    });
  }
  const streams = entries.filter((entry) => entry.type === 2);
  const storages = entries.filter((entry) => entry.type === 1);
  const suspicious = streams.filter((entry) => entry.size_bytes === 0 || entry.size_bytes > 2_000_000);
  return {
    readable: true,
    sector_size: sectorSize,
    mini_sector_size: miniSectorSize,
    directory_sector_count: directorySectors.length,
    difat_sector_count: difatSectorCount,
    fat_sector_count: difat.length,
    entry_count: entries.length,
    stream_count: streams.length,
    storage_count: storages.length,
    zero_size_stream_count: streams.filter((entry) => entry.size_bytes === 0).length,
    largest_streams: [...streams].sort((a, b) => b.size_bytes - a.size_bytes).slice(0, 12).map(({ name, size_bytes, short_stream_candidate }) => ({ name, size_bytes, short_stream_candidate })),
    stream_names: streams.map((entry) => entry.name).sort(),
    storage_names: storages.map((entry) => entry.name).sort(),
    suspicious_streams: suspicious.map(({ name, size_bytes }) => ({ name, size_bytes })),
  };
}

function extractStrings(filePath) {
  const buffer = fs.readFileSync(filePath);
  const ascii = [];
  const asciiPattern = /[\x20-\x7E]{5,}/g;
  const latin = buffer.toString('latin1');
  let match;
  while ((match = asciiPattern.exec(latin)) && ascii.length < 5000) ascii.push(match[0]);
  const utf16 = [];
  let run = [];
  for (let i = 0; i + 1 < buffer.length; i += 2) {
    const code = buffer.readUInt16LE(i);
    if (code >= 32 && code <= 126) run.push(String.fromCharCode(code));
    else {
      if (run.length >= 5) utf16.push(run.join(''));
      run = [];
    }
    if (utf16.length >= 5000) break;
  }
  const strings = [...new Set([...ascii, ...utf16])];
  const terms = ['SheetMetal', 'FlatPattern', 'EdgeFlange', 'SolidToSheetMetal', 'Imported', 'Import', 'STEP', 'Parasolid', 'shelf', 'front_frame', 'door_panel', 'Design Binder', 'External', 'Reference', 'Default'];
  const term_counts = {};
  for (const term of terms) {
    term_counts[term] = strings.filter((s) => s.toLowerCase().includes(term.toLowerCase())).length;
  }
  return {
    total_unique_visible_strings_sampled: strings.length,
    term_counts,
    relevant_strings_sample: strings.filter((s) => terms.some((term) => s.toLowerCase().includes(term.toLowerCase()))).slice(0, 80),
  };
}

function resultSummary() {
  const shelfResultPath = path.join(root, 'workers', 'generated_models', 'review_generation_requests', 'part-first-16029-v43-pilot', 'sw2020_part_first', 'shelf_cross_panel_left_v43', 'shelf_cross_panel_left_v43_result.json');
  const doorResultPath = path.join(root, 'workers', 'generated_models', 'review_generation_requests', 'part-first-16029-v43-pilot', 'sw2020_part_first', 'door_panel_2_12_right_v43', 'review_single_door_panel_W307_H298_sheetmetal_result.json');
  const frontResultPath = path.join(root, 'workers', 'generated_models', 'review_generation_requests', 'part-first-16029-v43-pilot', 'sw2020_part_first', 'front_frame_vertical_left_v43', 'front_frame_vertical_left_v43_result.json');
  const shelf = readJsonIfExists(shelfResultPath);
  const door = readJsonIfExists(doorResultPath);
  const front = readJsonIfExists(frontResultPath);
  return {
    shelf: {
      path: shelfResultPath,
      schema: shelf.schema || null,
      status: shelf.status || null,
      opened: shelf.opened,
      open_attempts: shelf.open_attempts,
      open_errors: shelf.open_errors,
      open_warnings: shelf.open_warnings,
      step_saved: shelf.step_saved,
      feature_count: Array.isArray(shelf.features) ? shelf.features.length : null,
      has_sheet_metal: shelf.has_sheet_metal ?? null,
      has_flat_pattern: shelf.has_flat_pattern ?? null,
      note: 'Shelf evidence is a legacy OpenDoc/STEP-export probe, not a native generator feature-tree result.',
    },
    door: {
      path: doorResultPath,
      schema: door.schema || null,
      status: door.status || null,
      releaseBlockerClosed: door.releaseBlockerClosed,
      opened: door.opened ?? null,
      savedPart: door.savedPart,
      hasSheetMetalFeature: door.hasSheetMetalFeature,
      hasFlatPatternFeature: door.hasFlatPatternFeature,
      feature_count: Array.isArray(door.features) ? door.features.length : null,
      edgeFlangeCreatedCount: door.edgeFlangeCreatedCount,
      holeCutCount: door.holeCutCount,
      foldedEnvelopeMeasuredDepthMm: door.foldedEnvelopeMeasuredDepthMm,
    },
    front: {
      path: frontResultPath,
      schema: front.schema || null,
      status: front.status || null,
      opened: front.opened,
      saved_native: front.saved_native,
      has_sheet_metal: front.has_sheet_metal,
      has_flat_pattern: front.has_flat_pattern,
      feature_count: Array.isArray(front.features) ? front.features.length : null,
      hole_cut_count: front.hole_cut_count,
      body_bbox_width_mm: front.body_bbox_width_mm,
      body_bbox_height_mm: front.body_bbox_height_mm,
      body_bbox_depth_mm: front.body_bbox_depth_mm,
    },
  };
}

function sourceReview() {
  const shelfTool = path.join(root, 'workers', 'solidworks_tools', 'Build16029ShelfCrossPanelEnvelopeSmoke.cs');
  const shelfToolText = fs.existsSync(shelfTool) ? fs.readFileSync(shelfTool, 'utf8') : '';
  const nativeShelfGeneratorExists = fs.existsSync(path.join(root, 'workers', 'solidworks_tools', 'Build16029ShelfCrossPanelSheetMetal.cs'));
  return {
    native_production_shelf_generator_found: nativeShelfGeneratorExists,
    envelope_fallback_tool: {
      path: shelfTool,
      exists: fs.existsSync(shelfTool),
      mentions_clean_envelope: shelfToolText.includes('clean_envelope'),
      mentions_not_production: shelfToolText.includes('not production') || shelfToolText.includes('fallback'),
      parameter_snippets: {
        width_mm: /WidthMm\s*=\s*([0-9.]+)/.exec(shelfToolText)?.[1] ?? null,
        height_mm: /HeightMm\s*=\s*([0-9.]+)/.exec(shelfToolText)?.[1] ?? null,
        depth_mm: /DepthMm\s*=\s*([0-9.]+)/.exec(shelfToolText)?.[1] ?? null,
      },
    },
  };
}

function partAnalysis(label, filePath) {
  return {
    label,
    metadata: fileMeta(filePath),
    ole: cfbDirectory(filePath),
    strings: extractStrings(filePath),
  };
}

const diffGate = readJson('data/locker_16029_shelf_load_original_copy_differential_gate.json');
const postcheck = readJson('data/locker_16029_shelf_load_original_copy_postcheck.json');
const analyses = {
  shelf: partAnalysis('shelf_cross_panel_left_v43', partPaths.shelf),
  shelf_copy: partAnalysis('shelf_cross_panel_left_v43_copy', partPaths.shelfCopy),
  door: partAnalysis('door_panel_2_12_right_v43', partPaths.door),
  front: partAnalysis('front_frame_vertical_left_v43', partPaths.front),
};
const results = resultSummary();
const source = sourceReview();

const streamNames = {
  shelf: new Set(analyses.shelf.ole.stream_names || []),
  door: new Set(analyses.door.ole.stream_names || []),
  front: new Set(analyses.front.ole.stream_names || []),
};
const shelfUniqueStreams = [...streamNames.shelf].filter((name) => !streamNames.door.has(name) && !streamNames.front.has(name)).sort();
const missingFromShelfButInControls = [...new Set([...streamNames.door, ...streamNames.front])].filter((name) => !streamNames.shelf.has(name)).sort();

// The offline evidence distinguishes the failure from a path issue but does not expose
// feature topology, bend/flat-pattern validity, or the native production generator chain.
const classification = 'SHELF_CONTENT_DIAGNOSTIC_INCOMPLETE';

const originalMetaBefore = diffGate.original?.metadata;
const currentShelfMeta = analyses.shelf.metadata;
const originalCadUnchanged = originalMetaBefore?.sha256 === currentShelfMeta.sha256
  && originalMetaBefore?.last_write_utc === currentShelfMeta.last_write_utc;

const gate = {
  schema: 'winnsen.locker16029.shelf_content_diagnostic_gate.v1',
  generated_at: new Date().toISOString(),
  status: 'INCOMPLETE',
  classification,
  input_evidence: {
    original_copy_differential_gate: {
      path: 'data/locker_16029_shelf_load_original_copy_differential_gate.json',
      status: diffGate.status,
      classification: diffGate.classification,
      copy_same_hash: diffGate.copy?.same_hash_as_original,
      original_cad_unchanged: diffGate.original_cad_unchanged,
    },
    postcheck: {
      path: 'data/locker_16029_shelf_load_original_copy_postcheck.json',
      status: postcheck.status,
      classification: postcheck.classification,
      post_gate_modal_or_residual_observed: postcheck.post_gate_modal_or_residual_observed,
    },
  },
  offline_checks: {
    document_manager_available: false,
    document_manager_note: 'No SolidWorks Document Manager license/binary was used. Raw OLE compound parsing was attempted but this SW2020 file family does not expose a standard CFB header in any of the three control files; OLE parsing is therefore recorded as unavailable, not as a shelf-specific defect.',
    parts: analyses,
    comparison: {
      shelf_size_vs_controls: {
        shelf_bytes: analyses.shelf.metadata.size_bytes,
        door_bytes: analyses.door.metadata.size_bytes,
        front_bytes: analyses.front.metadata.size_bytes,
      },
      ole_stream_counts: {
        shelf: analyses.shelf.ole.stream_count,
        door: analyses.door.ole.stream_count,
        front: analyses.front.ole.stream_count,
      },
      shelf_unique_streams: shelfUniqueStreams,
      streams_missing_from_shelf_but_present_in_controls: missingFromShelfButInControls,
    },
  },
  generator_and_result_review: {
    result_jsons: results,
    source_review: source,
    decision_notes: [
      'Raw OLE/CFB parsing is unavailable for shelf, door, and front controls alike; it cannot establish either container readability or container corruption for this file family.',
      'Visible-string scanning has no direct external/derived-reference signal, but it is not a complete reference-resolution check.',
      'Shelf result evidence already records OpenDoc6 silent and interactive failure, with only legacy OpenDoc succeeding; current tests reproduce OpenDoc6 timeout on original and same-hash short-path copy.',
      'Unlike door/front, the shelf path lacks a native production sheet-metal generator/result with feature tree, sheet-metal, flat-pattern, and saved native evidence; the only shelf native tool found is a clean envelope fallback explicitly not production geometry.',
      'This offline evidence contains no direct proof of a feature-tree error, geometric self-intersection, bend/flat-pattern defect, or malformed production parameter. A geometry/complexity or generator defect is therefore not established.',
    ],
  },
  evidence_limitations: {
    feature_tree_available: false,
    native_geometry_validation_available: false,
    bend_and_flat_pattern_validation_available: false,
    native_production_generator_chain_available: source.native_production_shelf_generator_found,
    root_cause_established: false,
  },
  original_cad_unchanged: originalCadUnchanged,
  repair_copy: {
    created: false,
    path: 'C:\\tmp\\16029_shelf_load_probe\\repair_probe',
    note: 'No repair copy was created in this goal; only a next-step disposable repair strategy is proposed.',
  },
  release_boundary: {
    original_sldprt_modified: false,
    original_sldprt_saved_or_restored: false,
    candidate_sldasm_modified: false,
    placement_modified: false,
    assembly_generated: false,
    exact_api_called: false,
    closest_distance_called: false,
    full_cabinet_expanded: false,
    dxf_bom_released: false,
    native_bbox_is_not_exact_interference: true,
  },
  minimum_next_action: 'Define and review a disposable minimum native shelf generation-and-open validation goal in C:\\tmp. It must neither modify nor replace the original shelf SLDPRT, and it must not enter assembly, exact API, DXF/BOM release, or full-cabinet work.',
};

fs.writeFileSync(outPath, JSON.stringify(gate, null, 2));
console.log(outPath);
