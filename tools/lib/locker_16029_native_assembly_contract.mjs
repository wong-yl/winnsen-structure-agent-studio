import {
  TRUSTED_NATIVE_RECIPE_REGISTRY,
  nativeRecipeDigest,
} from '../locker_16029_native_recipe_registry.mjs'

const RECIPE_ID = 'winnsen-16029-888w-14door-native-v1'
const WORKING_ROOT = '标准寄存柜1917×760×550(总装配).SLDASM'
const FINAL_ROOT = '标准寄存柜1917×888×550(总装配).SLDASM'
const IDENTITY_ROTATION = Object.freeze([1, 0, 0, 0, 1, 0, 0, 0, 1])
const LEFT_X_MM = -230.5
const RIGHT_X_MM = 230.5
const LEFT_LOCAL_TONGUE_X_MM = 175.5
const RIGHT_LOCAL_TONGUE_X_MM = -175.5
const TONGUE_Z_MM = -11.3
const SHELF_BASE_CENTER_Y_MM = 1705
const CROSSBAR_BASE_CENTER_Y_MM = 1700

const DOOR_MODULE_FILES = Object.freeze({
  parts: Object.freeze([
    'door_panel_left_W381_H254p428571.SLDPRT',
    'door_panel_right_W381_H254p428571.SLDPRT',
    'door_stiffener_H243p928571.SLDPRT',
    'hinge_latch_plate.SLDPRT',
    'u_hook_pad.SLDPRT',
    'plastic_bushing.SLDPRT',
    'door_hinge_pin.SLDPRT',
    'circlip.SLDPRT',
    'mechanical_lock_tongue.SLDPRT',
  ]),
  assemblies: Object.freeze([
    'door_weld_left_W381_H254p428571.SLDASM',
    'door_weld_right_W381_H254p428571.SLDASM',
    'ordinary_door_left_W381_H254p428571.SLDASM',
    'ordinary_door_right_W381_H254p428571.SLDASM',
  ]),
})

const OLD_DOOR_REFERENCE_LEAVES = Object.freeze([
  '插销固定板.SLDPRT',
  '插销固定板2╱12_右.SLDPRT',
  '插销固定板2╱12_左.SLDPRT',
  '插销固定板4╱12_右.SLDPRT',
  '插销固定板4╱12_左.SLDPRT',
  '插销固定板6╱12_右.SLDPRT',
  '插销固定板6╱12_左.SLDPRT',
  '储物柜门2╱12焊接_右.SLDASM',
  '储物柜门2╱12焊接_左.SLDASM',
  '储物柜门4╱12焊接_右.SLDASM',
  '储物柜门4╱12焊接_左.SLDASM',
  '储物柜门6╱12焊接_右.SLDASM',
  '储物柜门6╱12焊接_左.SLDASM',
  '储物柜门板2╱12_右.SLDPRT',
  '储物柜门板2╱12_W317.SLDPRT',
  '储物柜门板4╱12_右.SLDPRT',
  '储物柜门板4╱12_W317.SLDPRT',
  '储物柜门板6╱12_右.SLDPRT',
  '储物柜门板6╱12_W317.SLDPRT',
  '储物柜门装配_L2.SLDASM',
  '储物柜门装配_L4.SLDASM',
  '储物柜门装配_L6.SLDASM',
  '储物柜门装配_R2.SLDASM',
  '储物柜门装配_R4.SLDASM',
  '储物柜门装配_R6.SLDASM',
  '柜门加强筋2╱12.SLDPRT',
  '柜门加强筋4╱12.SLDPRT',
  '柜门加强筋6╱12.SLDPRT',
  '开口挡圈5.SLDPRT',
  '门轴销.SLDPRT',
  '塑料轴套(云绅模具).SLDPRT',
  '锁舌.SLDPRT',
  'U型锁钩垫板.SLDPRT',
])

const ROOT_DOOR_INSTANCES = Object.freeze([
  '储物柜门装配_L2-2',
  '储物柜门装配_L4-2',
  '储物柜门装配_L6-2',
  '储物柜门装配_R2-2',
  '储物柜门装配_R4-2',
  '储物柜门装配_R6-2',
])

const ROOT_SHELF_INSTANCES = Object.freeze([
  '箱体横层板L焊接-1',
  '箱体横层板L焊接-2',
  '箱体横层板R焊接-1',
  '箱体横层板R焊接-2',
])

const FRAME_CROSSBAR_INSTANCES = Object.freeze([
  '门框 横隔板-1',
  '门框 横隔板-2',
  '门框 横隔板-4',
  '门框 横隔板-6',
  '门框 横隔板-8',
  '门框 横隔板-10',
  '门框 横隔板R-1',
  '门框 横隔板R-2',
  '门框 横隔板R-4',
  '门框 横隔板R-6',
  '门框 横隔板R-8',
  '门框 横隔板R-10',
])

function clone(value) {
  return structuredClone(value)
}

function transform16(xMm, yMm, zMm) {
  return [
    ...IDENTITY_ROTATION,
    xMm / 1000,
    yMm / 1000,
    zMm / 1000,
    1, 0, 0, 0,
  ]
}

function validateTrustedRecipe(recipe) {
  const trusted = TRUSTED_NATIVE_RECIPE_REGISTRY[RECIPE_ID]
  if (recipe && (recipe.hardwarePolicy?.electricalComponentsAllowed !== false ||
      recipe.hardwarePolicy?.electricLockAllowed !== false ||
      recipe.hardwarePolicy?.controlBoardAllowed !== false ||
      recipe.hardwarePolicy?.oneDoorOneLockOpening !== true)) {
    throw new Error('electrical components are forbidden and one-door-one-lock is required')
  }
  if (!recipe || recipe.id !== RECIPE_ID || recipe.version !== 1 ||
      nativeRecipeDigest(recipe) !== nativeRecipeDigest(trusted) ||
      recipe.geometry?.cabinetWidthMm !== 888 || recipe.geometry?.cabinetHeightMm !== 1917 ||
      recipe.geometry?.cabinetDepthMm !== 550 || recipe.geometry?.columns !== 2 ||
      recipe.geometry?.doorCount !== 14 || recipe.geometry?.doorPanelWidthMm !== 381 ||
      JSON.stringify(recipe.geometry?.columnDoorCounts) !== '[7,7]' ||
      recipe.geometry?.rowSequence !== 'L1111111-R1111111') {
    throw new Error('trusted 888x14 recipe is required')
  }
  if (!Array.isArray(recipe.calculations?.internalBoundaryYmm) ||
      recipe.calculations.internalBoundaryYmm.length !== 6 ||
      !Array.isArray(recipe.calculations?.doorCenterYByColumnMm?.L) ||
      recipe.calculations.doorCenterYByColumnMm.L.length !== 7 ||
      JSON.stringify(recipe.calculations.doorCenterYByColumnMm.L) !==
        JSON.stringify(recipe.calculations.doorCenterYByColumnMm.R)) {
    throw new Error('trusted 888x14 recipe row calculations are incomplete')
  }
  return trusted
}

function doorRows(recipe) {
  const halfWidth = recipe.geometry.doorPanelWidthMm / 2
  const rows = []
  for (const side of ['L', 'R']) {
    const xMm = side === 'L' ? LEFT_X_MM : RIGHT_X_MM
    const localTongueX = side === 'L' ? LEFT_LOCAL_TONGUE_X_MM : RIGHT_LOCAL_TONGUE_X_MM
    const sourceAssembly = side === 'L'
      ? 'ordinary_door_left_W381_H254p428571.SLDASM'
      : 'ordinary_door_right_W381_H254p428571.SLDASM'
    recipe.calculations.doorCenterYByColumnMm[side].forEach((yMm, index) => {
      rows.push({
        side,
        row: index + 1,
        instanceRole: `door_${side}_${index + 1}`,
        sourceAssembly,
        xMm,
        yMm,
        zMm: 0,
        transform: transform16(xMm, yMm, 0),
        fixed: false,
        panelXMinMm: xMm - halfWidth,
        panelXMaxMm: xMm + halfWidth,
        panelYMinMm: yMm - (recipe.calculations.doorHeightMm / 2),
        panelYMaxMm: yMm + (recipe.calculations.doorHeightMm / 2),
        tongueLocalXmm: localTongueX,
        tongueGlobalXmm: xMm + localTongueX,
        tongueGlobalYmm: yMm,
        tongueGlobalZmm: TONGUE_Z_MM,
      })
    })
  }
  return rows
}

function boundaryRows(recipe, kind) {
  const shelf = kind === 'shelf'
  const baseY = shelf ? SHELF_BASE_CENTER_Y_MM : CROSSBAR_BASE_CENTER_Y_MM
  const rows = []
  for (const side of ['L', 'R']) {
    const source = shelf
      ? `箱体横层板${side}焊接.SLDASM`
      : `门框 横隔板${side === 'R' ? 'R' : ''}.SLDPRT`
    recipe.calculations.internalBoundaryYmm.forEach((centerYmm, index) => {
      const transformYmm = centerYmm - baseY
      rows.push({
        side,
        boundary: index + 1,
        instanceRole: `${kind}_${side}_${index + 1}`,
        source,
        centerYmm,
        transformYmm,
        transform: transform16(0, transformYmm, 0),
        fixed: shelf,
      })
    })
  }
  return rows
}

export function buildNativeAssemblyContract(recipe) {
  validateTrustedRecipe(recipe)
  const doorFiles = [...DOOR_MODULE_FILES.parts, ...DOOR_MODULE_FILES.assemblies]
  return clone({
    schema: 'winnsen.native_16029_assembly_contract.v1',
    purpose: 'structure_engineering_assistance',
    recipe: {
      id: recipe.id,
      version: recipe.version,
      digest: nativeRecipeDigest(recipe),
    },
    workingRootFileName: WORKING_ROOT,
    finalRootFileName: FINAL_ROOT,
    doors: doorRows(recipe),
    shelves: boundaryRows(recipe, 'shelf'),
    frameCrossbars: boundaryRows(recipe, 'frame_crossbar'),
    remove: {
      rootDoors: [...ROOT_DOOR_INSTANCES],
      rootShelfInstances: [...ROOT_SHELF_INSTANCES],
      frameCrossbarInstances: [...FRAME_CROSSBAR_INSTANCES],
    },
    mutationPolicy: {
      directChildrenOnly: true,
      exactInstanceNamesOnly: true,
      recursivePatternDeleteForbidden: true,
      changedCadAllowlist: [WORKING_ROOT, '门框焊接.sldasm'],
    },
    doorModuleInventory: {
      cadFileCount: doorFiles.length,
      assemblyFileCount: DOOR_MODULE_FILES.assemblies.length,
      partFileCount: DOOR_MODULE_FILES.parts.length,
      relativeLeafNames: doorFiles,
      sourceOutputDirectories: ['parts', 'assemblies'],
      importMode: 'verified_copy_to_flat_working_pack',
      importManifest: {
        schema: 'winnsen.16029.native_door_module_flat_import_manifest.v1',
        fileName: 'door_module_flat_import_manifest.json',
        workingPackImportStateBeforeRootAssembly: 'NOT_IMPORTED',
        rootAssemblerBoundary: 'ROOT_ASSEMBLER_MUST_TRANSACTIONALLY_IMPORT_EXACT_13_TO_FLAT_WORKING_PACK',
      },
    },
    workingPackInventory: {
      beforeDoorModuleImport: 75,
      importedDoorModuleFiles: 13,
      afterDoorModuleImport: 88,
      physicalFilesMayExceedReferenceClosureUntilFinalPack: true,
    },
    expectedWorkingAssembly: {
      topLevelComponentCount: 41,
      recursiveComponentCount: 294,
      activeRecursiveComponentCount: 294,
      suppressedComponentCount: 0,
      doorModuleCount: 14,
      shelfModuleCount: 12,
      activeFrameCrossbarCount: 12,
      mechanicalTongueCount: 14,
    },
    expectedFinalClosure: {
      cadFileCount: 55,
      assemblyFileCount: 14,
      partFileCount: 41,
      missingFileCount: 0,
      externalFileCount: 0,
    },
    featureHealthPolicy: {
      mode: 'exact_unique_known_issue_only',
      maximumIssueCount: 1,
      allowedIssue: {
        name: '箱体右侧板焊接-1',
        type: 'Reference',
        errorCode: 51,
        errorCode2: 51,
        warning: true,
      },
    },
    forbiddenFinalReferenceLeafNames: [...OLD_DOOR_REFERENCE_LEAVES],
    requiredChecks: [...recipe.requiredChecks],
    executionBoundary: {
      planningOnly: true,
      engineeringAssistanceReady: false,
      finalPackAndRenameRequired: true,
      rebuildSaveFreshReadonlyReopenRequired: true,
      relocatedReopenRequired: true,
    },
  })
}
